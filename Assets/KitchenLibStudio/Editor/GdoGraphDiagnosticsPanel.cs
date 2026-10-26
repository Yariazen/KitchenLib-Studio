using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace KitchenLib.Studio.Editor
{
    public sealed class GdoGraphDiagnosticsPanel : VisualElement
    {
        private readonly Button _header;
        private readonly ScrollView _items = new ScrollView();
        public GdoGraphDiagnosticsPanel()
        {
            name = "gdo-graph-diagnostics";
            style.minHeight = 28;
            _header = new Button(Toggle) { name = "diagnostics-toggle", text = "No graph selected" };
            _header.style.unityTextAlign = UnityEngine.TextAnchor.MiddleLeft;
            Add(_header);
            _items.style.maxHeight = 180;
            Add(_items);
            SetExpanded(false);
        }

        public bool IsExpanded { get; private set; }
        public string StatusText => _header.text;

        public void Bind(IReadOnlyList<GdoGraphDiagnostic> diagnostics, Action<string> focus, string readyStatus = null)
        {
            _items.Clear();
            foreach (GdoGraphDiagnostic diagnostic in diagnostics)
            {
                var button = new Button(() => focus?.Invoke(diagnostic.ElementId))
                { text = diagnostic.Severity + ": " + diagnostic.Message, tooltip = diagnostic.PropertyPath };
                button.SetEnabled(!string.IsNullOrEmpty(diagnostic.ElementId));
                _items.Add(button);
            }
            if (diagnostics.Count == 0)
            {
                _header.text = string.IsNullOrEmpty(readyStatus) ? "Graph valid" : readyStatus;
                SetExpanded(false);
            }
            else
            {
                _header.text = diagnostics.Count + (diagnostics.Count == 1 ? " issue needs attention" : " issues need attention");
                SetExpanded(true);
            }
        }

        private void Toggle() => SetExpanded(!IsExpanded);
        private void SetExpanded(bool value)
        {
            IsExpanded = value;
            _items.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
