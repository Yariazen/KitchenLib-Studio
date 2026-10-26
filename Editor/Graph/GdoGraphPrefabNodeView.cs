using System;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace KitchenLib.Studio.Editor.Graph
{
    internal sealed class GdoGraphPrefabNodeView : Node
    {
        private readonly Action _remove;

        public GdoGraphPrefabNodeView(GdoBundleGraphAsset graph, GdoGraphPrefabRecord record,
            Action changed, Action remove)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            if (record == null) throw new ArgumentNullException(nameof(record));
            RecordId = record.Id;
            _remove = remove;
            capabilities &= ~Capabilities.Deletable;
            title = record.DisplayName;
            tooltip = "Prefab · " + (record.SourceMode == GdoGraphPrefabSourceMode.Primitive
                ? record.PrimitiveType.ToString() : "Workspace");
            AddToClassList("kls-prefab-node");
            var output = Port.Create<Edge>(Orientation.Horizontal, Direction.Output,
                Port.Capacity.Multi, typeof(GameObject));
            output.portName = "Prefab · GameObject";
            output.userData = "Value";
            GdoGraphTypePalette.Apply(output, typeof(GameObject).FullName);
            outputContainer.Add(output);

            var displayName = new TextField("Name") { name = "prefab-name", value = record.DisplayName, isDelayed = true };
            displayName.RegisterValueChangedCallback(evt =>
            {
                if (string.IsNullOrWhiteSpace(evt.newValue)) { displayName.SetValueWithoutNotify(record.DisplayName); return; }
                GdoBundleGraphRepository.RenamePrefab(graph, record.Id, evt.newValue);
                changed?.Invoke();
            });
            extensionContainer.Add(displayName);

            var mode = new EnumField("Source", record.SourceMode) { name = "prefab-source-mode" };
            mode.RegisterValueChangedCallback(evt =>
            {
                GdoBundleGraphRepository.SetPrefabSourceMode(graph, record.Id,
                    (GdoGraphPrefabSourceMode)evt.newValue);
                changed?.Invoke();
            });
            extensionContainer.Add(mode);
            if (record.SourceMode == GdoGraphPrefabSourceMode.Primitive)
            {
                var primitive = new EnumField("Primitive", record.PrimitiveType) { name = "prefab-primitive-type" };
                var scale = new Vector3Field("Scale") { name = "prefab-scale", value = record.Scale };
                var material = new ObjectField("Material") { name = "prefab-material", objectType = typeof(Material),
                    allowSceneObjects = false, value = record.Material };
                var collider = new Toggle("Collider") { name = "prefab-collider", value = record.IncludeCollider };
                Action commit = () =>
                {
                    GdoBundleGraphRepository.ConfigurePrimitive(graph, record.Id,
                        (PrimitiveType)primitive.value, scale.value, material.value as Material, collider.value);
                    changed?.Invoke();
                };
                primitive.RegisterValueChangedCallback(_ => commit());
                scale.RegisterValueChangedCallback(_ => commit());
                material.RegisterValueChangedCallback(_ => commit());
                collider.RegisterValueChangedCallback(_ => commit());
                extensionContainer.Add(primitive); extensionContainer.Add(scale);
                extensionContainer.Add(material); extensionContainer.Add(collider);
            }
            else
            {
                var source = new ObjectField("Prefab") { name = "workspace-prefab", objectType = typeof(GameObject),
                    allowSceneObjects = false, value = record.WorkspacePrefab };
                source.RegisterValueChangedCallback(evt =>
                {
                    var prefab = evt.newValue as GameObject;
                    string path = AssetDatabase.GetAssetPath(prefab);
                    string guid = AssetDatabase.AssetPathToGUID(path);
                    if (prefab == null || string.IsNullOrEmpty(guid)) return;
                    GdoBundleGraphRepository.ConfigureWorkspacePrefab(graph, record.Id, prefab, guid);
                    changed?.Invoke();
                });
                extensionContainer.Add(source);
            }
            SetPosition(record.Position);
            expanded = record.Expanded;
            RefreshExpandedState();
            RefreshPorts();
        }

        public string RecordId { get; }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            base.BuildContextualMenu(evt);
            evt.menu.AppendAction("Remove Prefab", _ => _remove?.Invoke(),
                _remove == null ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
        }
    }
}
