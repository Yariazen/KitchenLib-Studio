using System;
using System.Collections.Generic;
using System.Linq;
using KitchenData;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace KitchenLib.Studio.Editor
{
    public sealed class GdoGraphNodeView : Node
    {
        private readonly GameDataObject _asset;
        private readonly Action<bool> _expandedChanged;
        private readonly Action _graphChanged;
        private readonly Action _renameGdo;
        private readonly Func<IReadOnlyList<GdoRelationshipChoice>> _relationshipChoices;
        private readonly Action<string> _addRelationship;
        private readonly Func<string, bool> _canAddRelationship;
        private readonly Action _removeMember;
        private readonly bool _groupedDisplay;
        private readonly Action<bool> _propertiesExpandedChanged;
        private readonly Button _presentationButton;
        private bool _groupPresentationExpanded = true;
        private bool _lastBodyVisible;

        public GdoGraphNodeView(GdoGraphNodeRecord record, Action<bool> expandedChanged = null, Action graphChanged = null,
            Action renameGdo = null, Func<IReadOnlyList<GdoRelationshipChoice>> relationshipChoices = null,
            Action<string> addRelationship = null, Func<string, bool> canAddRelationship = null, Action removeMember = null,
            bool groupedDisplay = false, Action<bool> propertiesExpandedChanged = null)
        {
            RecordId = record?.Id ?? throw new ArgumentNullException(nameof(record));
            InitialPosition = record.Position;
            IsBroken = record.IsBroken;
            _asset = record.Asset;
            _expandedChanged = expandedChanged;
            _graphChanged = graphChanged;
            _renameGdo = renameGdo;
            _relationshipChoices = relationshipChoices;
            _addRelationship = addRelationship;
            _canAddRelationship = canAddRelationship;
            _removeMember = removeMember;
            _groupedDisplay = groupedDisplay;
            _propertiesExpandedChanged = propertiesExpandedChanged;
            capabilities &= ~Capabilities.Deletable;
            if (groupedDisplay) capabilities &= ~Capabilities.Movable;
            AddToClassList("kls-gdo-node--local");
            if (IsBroken) AddToClassList("kls-gdo-node--broken");
            viewDataKey = RecordId;
            title = groupedDisplay ? "Properties" : IsBroken ? record.CachedName + " (Missing)" : record.Asset.name;
            tooltip = IsBroken ? "The source GDO asset is missing. The graph record is retained for repair." : record.RuntimeTypeName;
            titleContainer.style.backgroundColor = IsBroken
                ? new Color(0.48f, 0.20f, 0.20f, 1f)
                : new Color(0.18f, 0.34f, 0.48f, 1f);
            AddOutput(record.RuntimeTypeName);
            if (!IsBroken)
                AddInputs(record.Asset);
            SetPosition(record.Position);
            IsExpanded = groupedDisplay ? record.PropertiesExpanded : record.Expanded;
            if (IsExpanded && _asset != null)
            {
                EditorBody = new GdoGraphNodeEditorElement(_asset, GdoGraphNodeEditorElement.DefaultFieldLimit, _graphChanged);
                extensionContainer.Add(EditorBody);
            }
            expanded = IsExpanded;
            RefreshExpandedState();
            RefreshPorts();
            if (_groupedDisplay)
            {
                GdoGraphHeaderButton.HideNativeCollapse(this);
                _presentationButton = GdoGraphHeaderButton.Create("properties-presentation-button", "▸",
                    "Show full details", "kls-graph-disclosure-button",
                    () => PresentationCycleRequested?.Invoke());
                titleContainer.Insert(0, _presentationButton);
                ApplyVisualState();
            }
            else
                (this.Q<VisualElement>("collapse-button") ?? titleContainer.Query<VisualElement>()
                    .ToList().FirstOrDefault(element => element.ClassListContains("collapse-button")))
                    ?.RegisterCallback<ClickEvent>(_ => schedule.Execute(SyncExpandedState));
        }

        public string RecordId { get; }
        public Rect InitialPosition { get; }
        public bool IsBroken { get; }
        public bool IsExpanded { get; private set; }
        public GdoGraphNodeEditorElement EditorBody { get; private set; }
        public event Action BodyVisibilityChanged;
        public event Action PresentationCycleRequested;

        internal void SetPresentationIndicator(GdoGraphPresentationMode mode)
        {
            if (_presentationButton == null) return;
            string glyph = mode == GdoGraphPresentationMode.Full ? "▾" :
                mode == GdoGraphPresentationMode.Connected ? "−" : "▸";
            string tooltip = mode == GdoGraphPresentationMode.Full ? "Show connected nodes only" :
                mode == GdoGraphPresentationMode.Connected ? "Show all nodes" : "Show full details";
            GdoGraphHeaderButton.SetGlyph(_presentationButton, glyph, tooltip);
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            base.BuildContextualMenu(evt);
            evt.menu.AppendAction("Remove from graph", _ => _removeMember?.Invoke(),
                _removeMember == null ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
            if (IsBroken) return;
            evt.menu.AppendAction("Rename GDO", _ => _renameGdo?.Invoke(),
                _renameGdo == null ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
            if (_groupedDisplay) return;
            if (_relationshipChoices == null || _addRelationship == null)
            {
                evt.menu.AppendAction("Add Relationship", _ => { }, DropdownMenuAction.Status.Disabled);
                return;
            }
            IReadOnlyList<GdoRelationshipChoice> choices = _relationshipChoices();
            if (choices.Count == 0)
                evt.menu.AppendAction("Add Relationship", _ => { }, DropdownMenuAction.Status.Disabled);
            foreach (GdoRelationshipChoice choice in choices)
                evt.menu.AppendAction("Add Relationship/" + choice.Label, _ => _addRelationship(choice.FieldPath),
                    _ => _canAddRelationship == null || _canAddRelationship(choice.FieldPath)
                        ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
        }

        public void SetExpanded(bool value)
        {
            if (_groupedDisplay && value) _groupPresentationExpanded = true;
            ApplyExpanded(value, true);
        }

        public void SetGroupExpanded(bool value)
        {
            if (!_groupedDisplay) { ApplyExpanded(value, false); return; }
            _groupPresentationExpanded = value;
            ApplyVisualState();
        }

        internal void SetConnectedPorts(ISet<string> inputPaths, bool outputConnected, bool connectedOnly)
        {
            foreach (Port port in inputContainer.Children().OfType<Port>())
                port.style.display = connectedOnly && (inputPaths == null ||
                    !inputPaths.Contains(port.userData as string)) ? DisplayStyle.None : DisplayStyle.Flex;
            foreach (Port port in outputContainer.Children().OfType<Port>())
                port.style.display = connectedOnly && !outputConnected ? DisplayStyle.None : DisplayStyle.Flex;
            RefreshPorts();
        }

        public void SyncExpandedState()
        {
            if (IsExpanded == expanded) return;
            ApplyExpanded(expanded, true);
        }

        private void ApplyExpanded(bool value, bool notify)
        {
            IsExpanded = value;
            if (value && EditorBody == null && _asset != null)
            {
                EditorBody = new GdoGraphNodeEditorElement(_asset, GdoGraphNodeEditorElement.DefaultFieldLimit, _graphChanged);
                extensionContainer.Add(EditorBody);
            }
            ApplyVisualState();
            if (notify)
            {
                if (_groupedDisplay) _propertiesExpandedChanged?.Invoke(value);
                else _expandedChanged?.Invoke(value);
            }
        }

        public bool BodyVisible => !_groupedDisplay || (_groupPresentationExpanded && IsExpanded);

        private void ApplyVisualState()
        {
            expanded = IsExpanded;
            RefreshExpandedState();
            if (!_groupedDisplay) return;
            extensionContainer.style.display = BodyVisible ? DisplayStyle.Flex : DisplayStyle.None;
            if (_lastBodyVisible != BodyVisible)
            {
                _lastBodyVisible = BodyVisible;
                BodyVisibilityChanged?.Invoke();
            }
        }

        private void AddOutput(string runtimeTypeName)
        {
            Port port = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(GameDataObject));
            port.portName = ShortName(runtimeTypeName);
            port.userData = "Value";
            GdoGraphTypePalette.Apply(port, runtimeTypeName);
            outputContainer.Add(port);
        }

        private void AddInputs(GameDataObject asset)
        {
            GdoTypeDescriptorView descriptor;
            try { descriptor = GdoDescriptorCatalog.ForTarget(asset); }
            catch { return; }
            foreach (GdoGraphPortDescriptor definition in GdoGraphPortCatalog.ForDescriptor(descriptor)
                .Where(port => !port.UsesRelationshipNode))
            {
                Port.Capacity capacity = definition.Cardinality == GdoGraphPortCardinality.Multiple
                    ? Port.Capacity.Multi : Port.Capacity.Single;
                Port port = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, capacity, typeof(GameDataObject));
                port.portName = Humanize(ShortName(definition.Path)) + " · " + ShortName(definition.ExpectedTypeName);
                port.tooltip = definition.Path + "\nConnect: " + definition.ExpectedTypeName;
                port.userData = definition.Path;
                GdoGraphTypePalette.Apply(port, definition.ExpectedTypeName);
                inputContainer.Add(port);
            }
            foreach (GdoGraphPortDescriptor definition in GdoUnityObjectPortCatalog.ForDescriptor(descriptor))
            {
                Port port = Port.Create<Edge>(Orientation.Horizontal, Direction.Input,
                    Port.Capacity.Single, typeof(GameObject));
                port.portName = Humanize(ShortName(definition.Path)) + " · GameObject";
                port.tooltip = definition.Path + "\nConnect: UnityEngine.GameObject";
                port.userData = definition.Path;
                GdoGraphTypePalette.Apply(port, typeof(GameObject).FullName);
                inputContainer.Add(port);
            }
        }

        private static string ShortName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return "GDO";
            int index = typeName.LastIndexOf('.');
            return index < 0 ? typeName : typeName.Substring(index + 1);
        }

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
