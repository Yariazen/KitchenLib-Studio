using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace KitchenLib.Studio.Editor
{
    public sealed class GdoGraphToolbar : VisualElement
    {
        private readonly ObjectField _graph;
        private readonly ToolbarButton _build;
        private readonly ToolbarButton _frameAll;
        private readonly ToolbarSearchField _search;
        public GdoGraphToolbar(Action<GdoBundleGraphAsset> selectGraph, Func<string, bool> search,
            Action build = null, Action frameAll = null)
        {
            name = "gdo-graph-toolbar";
            var graphRow = new Toolbar { name = "gdo-graph-primary-toolbar" };
            _graph = new ObjectField("Bundle Graph")
                { name = "bundle-graph-selector", objectType = typeof(GdoBundleGraphAsset), allowSceneObjects = false };
            _graph.style.flexGrow = 1;
            _graph.RegisterValueChangedCallback(evt => selectGraph?.Invoke(evt.newValue as GdoBundleGraphAsset));
            graphRow.Add(_graph);
            _search = new ToolbarSearchField { name = "gdo-graph-search", tooltip = "Find a GDO by name, ID, or type" };
            _search.RegisterCallback<KeyDownEvent>(evt => { if (evt.keyCode == UnityEngine.KeyCode.Return) search?.Invoke(_search.value); });
            graphRow.Add(_search);
            graphRow.Add(new ToolbarButton(() => search?.Invoke(_search.value)) { name = "find-gdo", text = "Find" });
            _frameAll = new ToolbarButton(() => frameAll?.Invoke()) { name = "frame-all-gdos", text = "Frame All" };
            graphRow.Add(_frameAll);
            _build = new ToolbarButton(() => build?.Invoke()) { name = "build-gdo-bundle", text = "Build Bundle" };
            graphRow.Add(_build);
            Add(graphRow);
        }

        public void SetGraphWithoutNotify(GdoBundleGraphAsset graph) => _graph.SetValueWithoutNotify(graph);

        public void SetGraphState(bool hasGraph, bool canBuild, string buildReason)
        {
            _frameAll.SetEnabled(hasGraph);
            _search.SetEnabled(hasGraph);
            _build.SetEnabled(canBuild);
            _build.tooltip = canBuild ? "Validate and build this data-only asset bundle" : buildReason;
        }
    }
}
