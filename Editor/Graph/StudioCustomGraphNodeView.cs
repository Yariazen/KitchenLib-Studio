using System;
using System.Globalization;
using System.Linq;
using KitchenLib.Studio.Editor.Api.Authoring;
using KitchenLib.Studio.Editor.Api.Graph;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace KitchenLib.Studio.Editor.Graph
{
    internal sealed class StudioCustomGraphNodeView : Node
    {
        private readonly Action remove;
        private readonly GdoGraphConnectionController connections;

        internal StudioCustomGraphNodeView(GdoBundleGraphAsset graph, StudioCustomGraphNodeRecord record,
            Action changed, Action remove)
        {
            this.remove = remove;
            connections = new GdoGraphConnectionController(graph);
            capabilities &= ~Capabilities.Deletable;
            StudioGraphNodeDefinition definition = StudioGraphNodeRegistry.Find(record.TypeId);
            title = definition?.DisplayName ?? "Unavailable custom node";
            tooltip = record.TypeId;
            if (definition == null)
                extensionContainer.Add(new Label("Definition unavailable. Stored fields and connections are retained; building is blocked.")
                    { name = "custom-node-diagnostic", style = { whiteSpace = WhiteSpace.Normal, maxWidth = 260 } });
            else
            {
                foreach (StudioGraphFieldDefinition field in definition.Fields)
                    AddField(graph, record, field, changed);
                foreach (StudioGraphPortDefinition port in definition.Ports)
                    AddPort(port.Key, port.Label, port.Direction == StudioGraphPortDirection.Input ? Direction.Input : Direction.Output,
                        port.Cardinality == StudioGraphPortCardinality.Single ? Port.Capacity.Single : Port.Capacity.Multi, port.SemanticTypeId);
            }
            // Unknown ports are visible but not connectable. Their keyed endpoints remain editable by removal.
            foreach (GdoGraphEdgeRecord edge in graph.Edges)
            {
                if (edge.OutputNodeId == record.Id && (definition == null || !definition.Ports.Any(port =>
                    port.Key == edge.OutputPortPath && port.Direction == StudioGraphPortDirection.Output)))
                    AddUnknownPort(edge.OutputPortPath, Direction.Output);
                if (edge.InputNodeId == record.Id && (definition == null || !definition.Ports.Any(port =>
                    port.Key == edge.InputPortPath && port.Direction == StudioGraphPortDirection.Input)))
                    AddUnknownPort(edge.InputPortPath, Direction.Input);
            }
            SetPosition(record.Position);
            RefreshExpandedState();
            RefreshPorts();
        }

        private void AddUnknownPort(string key, Direction direction)
        {
            VisualElement container = direction == Direction.Input ? inputContainer : outputContainer;
            if (container.Query<Port>().ToList().Any(port => (string)port.userData == key)) return;
            AddPort(key, "Unavailable: " + key, direction, Port.Capacity.Multi, "unavailable");
        }

        private void AddPort(string key, string label, Direction direction, Port.Capacity capacity, string semanticType)
        {
            var port = Port.Create<Edge>(Orientation.Horizontal, direction, capacity, typeof(object));
            port.userData = key;
            port.portName = label;
            GdoGraphTypePalette.Apply(port, semanticType);
            (direction == Direction.Input ? inputContainer : outputContainer).Add(port);
        }

        private void AddField(GdoBundleGraphAsset graph, StudioCustomGraphNodeRecord record,
            StudioGraphFieldDefinition field, Action changed)
        {
            var row = new VisualElement();
            StudioAuthoringValue value = StudioAuthoringValue.Null;
            StudioCustomGraphFieldRecord stored = record.Fields.FirstOrDefault(item => item?.Key == field.Key);
            if (stored != null && (!stored.TryRead(out value) ||
                (value.Kind != StudioAuthoringValueKind.Null && value.Kind != field.ValueKind)))
            {
                row.Add(new Label(field.Label + ": unsupported stored value (retained)"));
                extensionContainer.Add(row);
                return;
            }
            Action<StudioAuthoringValue> save = authored =>
            {
                connections.SetCustomValue(record.Id, field.Key, authored);
                changed?.Invoke();
            };
            if (field.ValueKind == StudioAuthoringValueKind.Boolean)
            {
                var toggle = new Toggle(field.Label) { name = "custom-field-" + field.Key,
                    value = value.Kind == StudioAuthoringValueKind.Boolean && value.AsBoolean() };
                toggle.RegisterValueChangedCallback(evt => save(StudioAuthoringValue.FromBoolean(evt.newValue)));
                row.Add(toggle);
                if (value.Kind == StudioAuthoringValueKind.Null)
                    row.Add(new Button(() => save(StudioAuthoringValue.FromBoolean(false))) { text = "Set false" });
            }
            else if (field.ValueKind != StudioAuthoringValueKind.Null)
            {
                var text = new TextField(field.Label) { name = "custom-field-" + field.Key, isDelayed = true, value = Display(value) };
                text.RegisterValueChangedCallback(evt =>
                {
                    if (TryParse(field.ValueKind, evt.newValue, out StudioAuthoringValue authored)) save(authored);
                    else text.SetValueWithoutNotify(Display(value));
                });
                row.Add(text);
            }
            row.Add(new Button(() => save(StudioAuthoringValue.Null)) { text = "Clear value", tooltip = "Store an explicit null value" });
            extensionContainer.Add(row);
        }

        private static string Display(StudioAuthoringValue value)
        {
            switch (value.Kind)
            {
                case StudioAuthoringValueKind.String: return value.AsString();
                case StudioAuthoringValueKind.Reference: return value.AsReference();
                case StudioAuthoringValueKind.Integer: return value.AsInteger().ToString(CultureInfo.InvariantCulture);
                case StudioAuthoringValueKind.Number: return value.AsNumber().ToString("R", CultureInfo.InvariantCulture);
                default: return string.Empty;
            }
        }

        private static bool TryParse(StudioAuthoringValueKind kind, string text, out StudioAuthoringValue value)
        {
            value = null;
            switch (kind)
            {
                case StudioAuthoringValueKind.String: value = StudioAuthoringValue.FromString(text); break;
                case StudioAuthoringValueKind.Reference:
                    if (string.IsNullOrEmpty(text)) return false;
                    value = StudioAuthoringValue.FromReference(text); break;
                case StudioAuthoringValueKind.Integer:
                    if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long integer)) return false;
                    value = StudioAuthoringValue.FromInteger(integer); break;
                case StudioAuthoringValueKind.Number:
                    if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) ||
                        double.IsNaN(number) || double.IsInfinity(number)) return false;
                    value = StudioAuthoringValue.FromNumber(number); break;
                default: return false;
            }
            return true;
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            evt.menu.AppendAction("Remove custom node", _ => remove?.Invoke());
            evt.StopPropagation();
        }
    }
}
