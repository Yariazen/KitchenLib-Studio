using System.Linq;
using KitchenData;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

using KitchenLib.Studio.Editor.Graph;
using KitchenLib.Studio.Editor.Overlays;

namespace KitchenLib.Studio.Editor.Authoring
{
    internal sealed class GdoInspectorHeader : VisualElement
    {
        public GdoInspectorHeader(GameDataObject target, GdoTypeDescriptorView descriptor, GdoInspectorModel model,
            SerializedObject serializedObject, GdoOverlayAuthoringAsset companion, System.Action refresh)
        {
            AddToClassList("kls-gdo-header");
            Add(new Label(target.name) { name = "display-name" });
            Add(new Label(descriptor.RuntimeTypeName) { name = "runtime-type" });
            bool drift = companion != null && companion.DescriptorFingerprint != descriptor.DescriptorFingerprint;
            string status = !descriptor.IsAvailable ? "Unavailable" : drift ? "Schema drift" : companion == null ? "Generated descriptor ready" : "Compatible";
            Add(new Label("Schema: " + descriptor.DescriptorFingerprint + " • " + status) { name = "schema-status" });
            if (drift)
                Add(new Button(() =>
                {
                    Undo.RecordObject(companion, "Accept current GDO descriptor");
                    companion.AcceptCurrentDescriptor();
                    EditorUtility.SetDirty(companion);
                    refresh?.Invoke();
                }) { name = "accept-schema", text = "Accept Current Descriptor After Review" });
            Add(new Label("Inheritance: " + string.Join(" → ", model.Sections.SelectMany(section => section.Groups)
                .Select(group => group.DeclaringTypeName).Distinct())) { name = "inheritance-path" });
            GdoBundleGraphAsset graph = GdoBundleGraphRepository.FindGraphsContaining(target).FirstOrDefault();
            if (graph != null)
                Add(new Button(() => GdoBundleGraphWindow.Open(graph, graph.Nodes.First(node => node.Asset == target).Id))
                    { name = "open-bundle-graph", text = "Open / Focus in Bundle Graph" });
            SerializedProperty id = serializedObject.FindProperty("ID");
            if (id != null)
            {
                var stableId = new PropertyField(id, "Stable GDO ID") { name = "stable-gdo-id" };
                stableId.SetEnabled(false);
                Add(stableId);
            }
            foreach (GdoInspectorField field in model.Section(GdoInspectorFieldKind.Authored).Fields)
            {
                if (field.Descriptor == null || field.Descriptor.Value.Kind != "UnityObject" || field.Descriptor.Value.RuntimeTypeName != "UnityEngine.GameObject") continue;
                if (field.Descriptor.AuthoringStorage != "Native") continue;
                SerializedProperty property = serializedObject.FindProperty(field.Descriptor.SerializedPropertyPath);
                if (property == null || property.objectReferenceValue == null) continue;
                Object referenced = property.objectReferenceValue;
                Add(new Button(() => Selection.activeObject = referenced) { text = "Locate " + field.Name });
                break;
            }
        }
    }
}
