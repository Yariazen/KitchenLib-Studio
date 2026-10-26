using System;
using System.Linq;
using KitchenData;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

using KitchenLib.Studio.Editor.Graph;
using KitchenLib.Studio.Editor.Overlays;
using KitchenLib.Studio.Editor.Validation;

namespace KitchenLib.Studio.Editor.Authoring
{
    internal sealed class GdoInspectorSectionElement : VisualElement
    {
        public GdoInspectorSectionElement(GdoInspectorSection section, SerializedObject serializedObject, GameDataObject target,
            Func<GdoOverlayAuthoringAsset> overlay, GdoInspectorDiagnosticMap diagnostics, Action refreshDiagnostics)
        {
            Kind = section.Kind;
            AddToClassList("kls-gdo-section");
            AddToClassList("kls-section-" + section.Kind.ToString().ToLowerInvariant());
            Add(new Label(Title(section.Kind)));
            if (section.Kind == GdoInspectorFieldKind.Relationship)
            {
                var notice = new HelpBox("Relationships are owned by the bundle graph.", HelpBoxMessageType.Info);
                notice.AddToClassList("kls-relationship-notice");
                Add(notice);
                GdoBundleGraphAsset graph = GdoBundleGraphRepository.FindGraphsContaining(target).FirstOrDefault();
                if (graph != null)
                    Add(new Button(() => GdoBundleGraphWindow.Open(graph, graph.Nodes.First(node => node.Asset == target).Id))
                        { name = "open-bundle-graph", text = "Open / Focus in Bundle Graph" });
            }
            foreach (GdoInspectorGroup group in section.Groups)
            {
                var foldout = new Foldout { text = group.DeclaringTypeName, value = true };
                foreach (GdoInspectorField field in group.Fields)
                {
                    VisualElement row = section.Kind == GdoInspectorFieldKind.Relationship
                        ? RelationshipSummary(field) : CreateField(field, serializedObject, target, overlay, refreshDiagnostics);
                    foreach (ValidationIssue issue in diagnostics.ForField(field.Key))
                        row.Add(new HelpBox(issue.Message, MessageType(issue.Severity)));
                    foldout.Add(row);
                }
                Add(foldout);
            }
        }

        public GdoInspectorFieldKind Kind { get; }

        private static VisualElement RelationshipSummary(GdoInspectorField field)
        {
            var row = new VisualElement { name = field.Key };
            row.AddToClassList("kls-gdo-field");
            row.Add(new Label(field.Name + " — connect this field in the bundle graph"));
            row.SetEnabled(false);
            return row;
        }

        private static VisualElement CreateField(GdoInspectorField field, SerializedObject serializedObject, GameDataObject target,
            Func<GdoOverlayAuthoringAsset> overlay, Action refreshDiagnostics)
        {
            var row = new VisualElement { name = field.Key };
            row.AddToClassList("kls-gdo-field");
            if (field.Kind == GdoInspectorFieldKind.RuntimeDerived)
            {
                row.Add(new Label(field.Name)); row.Add(new HelpBox(field.Description, HelpBoxMessageType.Info)); row.SetEnabled(false); return row;
            }
            if (field.Kind == GdoInspectorFieldKind.Unsupported)
            {
                row.Add(new HelpBox(field.Key + ": " + field.Description, HelpBoxMessageType.Error)); row.SetEnabled(false); return row;
            }
            row.Add(new GdoIntrinsicFieldElement(field, serializedObject, target, overlay, refreshDiagnostics));
            return row;
        }

        private static string Title(GdoInspectorFieldKind kind) => kind == GdoInspectorFieldKind.Authored ? "Authored Fields" :
            kind == GdoInspectorFieldKind.Relationship ? "Relationships" :
            kind == GdoInspectorFieldKind.RuntimeDerived ? "Runtime-Derived Fields" : "Unsupported Fields";

        private static HelpBoxMessageType MessageType(ValidationSeverity severity) => severity == ValidationSeverity.Info
            ? HelpBoxMessageType.Info : severity == ValidationSeverity.Warning ? HelpBoxMessageType.Warning : HelpBoxMessageType.Error;
    }
}
