using System;
using System.Collections.Generic;
using System.Linq;
using KitchenData;
using KitchenLib.Studio.Generated;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

using KitchenLib.Studio.Editor.Authoring;
using KitchenLib.Studio.Editor.Overlays;

namespace KitchenLib.Studio.Editor.Graph
{
    internal sealed class GdoGraphRelationshipNodeView : Node
    {
        private readonly GdoGraphRelationshipRecord _record;
        private GdoValueDescriptorData _descriptor;
        private readonly Action _removeRelationship;
        private readonly Action<int> _moveRelationship;
        private readonly Func<int, bool> _canMoveRelationship;
        private readonly Action<bool> _expandedChanged;
        private readonly string _relationshipName;
        private readonly string _ownerName;
        private readonly Button _moveUp;
        private readonly Button _moveDown;
        private readonly Button _collapse;
        private bool _groupExpanded = true;
        private bool _lastBodyVisible;

        public GdoGraphRelationshipNodeView(GdoGraphRelationshipRecord record, GdoBundleGraphAsset graph,
            Action<GdoRelationshipEditKind> changed = null, Action removeRelationship = null,
            Action<int> moveRelationship = null, Func<int, bool> canMoveRelationship = null,
            Action<bool> expandedChanged = null)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            RecordId = record.Id;
            capabilities &= ~Capabilities.Deletable;
            capabilities &= ~Capabilities.Movable;
            _record = record;
            _removeRelationship = removeRelationship;
            _moveRelationship = moveRelationship;
            _canMoveRelationship = canMoveRelationship;
            _expandedChanged = expandedChanged;
            AddToClassList("kls-gdo-node--relationship");
            viewDataKey = record.Id;
            _relationshipName = Humanize(ShortName(record.FieldPath).Replace("[]", string.Empty));
            titleContainer.style.backgroundColor = new Color(0.45f, 0.32f, 0.16f, 1f);
            GdoGraphNodeRecord owner = graph.Nodes.FirstOrDefault(node => node.Id == record.OwnerNodeId);
            _ownerName = owner == null ? "Missing owner" : owner.IsBroken ? owner.CachedName : owner.Asset.name;
            GdoGraphHeaderButton.HideNativeCollapse(this);
            _collapse = GdoGraphHeaderButton.Create("relationship-collapse-button", "▸",
                "Show additional properties", "kls-graph-disclosure-button", () => SetExpanded(!BodyVisible));
            titleContainer.Insert(0, _collapse);
            _moveUp = CreateMoveButton("relationship-move-up", "↑", -1);
            _moveDown = CreateMoveButton("relationship-move-down", "↓", 1);
            var reorder = new VisualElement { name = "relationship-reorder-controls" };
            reorder.AddToClassList("kls-graph-reorder-cluster");
            reorder.Add(_moveUp);
            reorder.Add(_moveDown);
            titleButtonContainer.Add(reorder);
            if (owner != null && !owner.IsBroken)
            {
                GdoGraphPortDescriptor[] definitions = GdoGraphPortCatalog.ForDescriptor(GdoDescriptorCatalog.ForTarget(owner.Asset))
                    .Where(port => port.UsesRelationshipNode && port.RelationshipPath == record.FieldPath)
                    .ToArray();
                GdoGraphPortDescriptor first = definitions.FirstOrDefault();
                if (first != null)
                {
                    _descriptor = first.RootValue.Kind == "List" ||
                        first.RootValue.Kind == "Array" || first.RootValue.Kind == "Set" ? first.RootValue.Element : first.RootValue;
                    RebuildReferencePorts();
                    EditorBody = new GdoGraphRelationshipNodeEditorElement(graph, record, _descriptor, editKind =>
                    {
                        if (editKind == GdoRelationshipEditKind.PortTopology) RebuildReferencePorts();
                        changed?.Invoke(editKind);
                    });
                    extensionContainer.Add(EditorBody);
                }
            }
            IsExpanded = record.Expanded;
            ApplyExpandedState();
            SetPosition(record.Position);
            RefreshPorts();
            RefreshMoveControls();
        }

        public string RecordId { get; }
        public bool IsExpanded { get; private set; }
        public GdoGraphRelationshipNodeEditorElement EditorBody { get; private set; }
        public event Action BodyVisibilityChanged;
        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            base.BuildContextualMenu(evt);
            evt.menu.AppendAction("Remove relationship", _ => _removeRelationship?.Invoke(),
                _removeRelationship == null ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
        }

        public IReadOnlyList<string> ReferencePortPaths => inputContainer.Children().OfType<Port>()
            .Select(port => port.userData as string).Where(path => path != null).ToArray();

        public void SetGroupExpanded(bool value)
        {
            _groupExpanded = value;
            ApplyExpandedState();
        }

        internal void SetConnectedPorts(ISet<string> inputPaths, bool connectedOnly)
        {
            foreach (Port port in inputContainer.Children().OfType<Port>())
                port.style.display = connectedOnly && (inputPaths == null ||
                    !inputPaths.Contains(port.userData as string)) ? DisplayStyle.None : DisplayStyle.Flex;
            RefreshPorts();
        }

        public void SetExpanded(bool value, bool notify = true)
        {
            if (value) _groupExpanded = true;
            if (IsExpanded == value)
            {
                ApplyExpandedState();
                return;
            }
            IsExpanded = value;
            ApplyExpandedState();
            if (notify) _expandedChanged?.Invoke(value);
        }

        public void RefreshMoveControls()
        {
            title = _relationshipName + " #" + _record.Order;
            tooltip = _ownerName + "\n" + _record.FieldPath + "\nOrder: " + _record.Order;
            _moveUp.SetEnabled(_canMoveRelationship != null && _canMoveRelationship(-1));
            _moveDown.SetEnabled(_canMoveRelationship != null && _canMoveRelationship(1));
        }

        private Button CreateMoveButton(string name, string text, int delta)
        {
            return GdoGraphHeaderButton.Create(name, text, delta < 0 ? "Move up" : "Move down",
                "kls-graph-reorder-button", () => _moveRelationship?.Invoke(delta));
        }

        private void ApplyExpandedState()
        {
            expanded = IsExpanded;
            RefreshExpandedState();
            extensionContainer.style.display = BodyVisible ? DisplayStyle.Flex : DisplayStyle.None;
            GdoGraphHeaderButton.SetGlyph(_collapse, BodyVisible ? "▾" : "▸",
                BodyVisible ? "Hide additional properties" : "Show additional properties");
            if (_lastBodyVisible != BodyVisible)
            {
                _lastBodyVisible = BodyVisible;
                BodyVisibilityChanged?.Invoke();
            }
        }

        public bool BodyVisible => _groupExpanded && IsExpanded;

        private void RebuildReferencePorts()
        {
            if (_descriptor == null) return;
            var desired = new List<ReferencePortSpec>();
            CollectReferencePorts(_descriptor, _record.Value ?? GdoOverlayValueEditor.CreateDefault(_descriptor),
                _record.FieldPath, desired);
            Dictionary<string, Port> existing = inputContainer.Children().OfType<Port>()
                .Where(port => port.userData is string).ToDictionary(port => (string)port.userData, StringComparer.Ordinal);
            foreach (ReferencePortSpec spec in desired)
            {
                if (existing.Remove(spec.Path)) continue;
                Port input = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, typeof(GameDataObject));
                input.portName = Humanize(ShortName(spec.Path)) + " · " + ShortName(spec.ExpectedType);
                input.tooltip = spec.Path + "\nConnect: " + spec.ExpectedType;
                input.userData = spec.Path;
                GdoGraphTypePalette.Apply(input, spec.ExpectedType);
                inputContainer.Add(input);
            }
            foreach (Port obsolete in existing.Values) obsolete.RemoveFromHierarchy();
            RefreshPorts();
        }

        private static void CollectReferencePorts(GdoValueDescriptorData descriptor, GdoOverlayValue value,
            string path, ICollection<ReferencePortSpec> result)
        {
            if (descriptor.Kind == "GdoReference")
            {
                result.Add(new ReferencePortSpec(path, descriptor.ExpectedTypeName));
                return;
            }
            if (descriptor.Kind == "Nullable")
            {
                if (value != null && value.Kind != GdoOverlayValueKind.Null)
                    CollectReferencePorts(descriptor.Element, value, path, result);
                return;
            }
            if (descriptor.Kind == "Struct")
            {
                foreach (GdoValueMemberDescriptorData member in descriptor.Members)
                {
                    GdoOverlayMember authored = value.Members.FirstOrDefault(item => item.Name == member.Name);
                    CollectReferencePorts(member.Value, authored?.Value ?? GdoOverlayValueEditor.CreateDefault(member.Value),
                        path + "." + member.Name, result);
                }
                return;
            }
            if (descriptor.Kind == "List" || descriptor.Kind == "Array" || descriptor.Kind == "Set")
                for (int index = 0; index < value.Values.Count; index++)
                    CollectReferencePorts(descriptor.Element, value.Values[index], path + "[" + index + "]", result);
        }

        private sealed class ReferencePortSpec
        {
            public ReferencePortSpec(string path, string expectedType) { Path = path; ExpectedType = expectedType; }
            public string Path { get; }
            public string ExpectedType { get; }
        }
        private static string ShortName(string value)
        { int dot = value == null ? -1 : value.LastIndexOf('.'); return dot < 0 ? value ?? string.Empty : value.Substring(dot + 1); }
        private static string Humanize(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var result = new System.Text.StringBuilder(value.Length + 8);
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (index > 0 && char.IsUpper(character) && !char.IsUpper(value[index - 1])) result.Append(' ');
                result.Append(character);
            }
            return result.ToString();
        }
    }
}
