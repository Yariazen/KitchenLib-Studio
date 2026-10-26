using System.Linq;
using System;
using KitchenData;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

using KitchenLib.Studio.Editor.Overlays;
using KitchenLib.Studio.Editor.Validation;

namespace KitchenLib.Studio.Editor.Authoring
{
    [CustomEditor(typeof(GameDataObject), true)]
    [CanEditMultipleObjects]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "KitchenLib.Studio.Editor", "KitchenLib.Studio.Editor", "GenericGdoEditor")]
    internal sealed class GenericGdoEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.AddToClassList("kls-gdo-inspector");
            if (targets.Length != 1)
            {
                root.Add(new HelpBox("KitchenLib Studio GDO editing supports one selected asset at a time.", HelpBoxMessageType.Warning)
                    { name = "multi-object-diagnostic" });
                root.SetEnabled(false);
                return root;
            }

            var gdo = (GameDataObject)target;
            GdoTypeDescriptorView descriptor;
            try { descriptor = GdoDescriptorCatalog.ForTarget(gdo); }
            catch (InvalidOperationException exception)
            {
                root.Add(new HelpBox(exception.Message, HelpBoxMessageType.Error) { name = "unknown-type-diagnostic" });
                root.SetEnabled(false);
                return root;
            }
            bool refreshQueued = false;
            Action refresh = null;
            refresh = () =>
            {
                if (refreshQueued) return;
                refreshQueued = true;
                root.schedule.Execute(() =>
                {
                    root.Unbind();
                    root.Clear();
                    Populate(root, gdo, descriptor, refresh);
                    refreshQueued = false;
                });
            };
            Populate(root, gdo, descriptor, refresh);
            root.RegisterCallback<SerializedPropertyChangeEvent>(_ => refresh());
            return root;
        }

        private void Populate(VisualElement root, GameDataObject gdo, GdoTypeDescriptorView descriptor, Action refresh)
        {
            GdoInspectorModel model = GdoInspectorModel.Build(descriptor);
            GdoOverlayAuthoringAsset companion = GdoOverlayAuthoringRepository.Find(gdo);
            root.Add(new GdoInspectorHeader(gdo, descriptor, model, serializedObject, companion, refresh));
            GdoInspectorDiagnosticMap diagnostics = GdoInspectorDiagnostics.Map(
                GdoValidator.Validate(new[] { gdo }).Concat(GdoDescriptorValueValidator.Validate(
                    gdo, companion, descriptor)), model);
            if (diagnostics.Count > 0)
            {
                var summary = new VisualElement { name = "diagnostic-summary" };
                summary.Add(new HelpBox(diagnostics.Count + " validation issue(s); highest severity: " + diagnostics.HighestSeverity + ".",
                    MessageType(diagnostics.HighestSeverity)));
                foreach (ValidationIssue issue in diagnostics.Unmapped) summary.Add(new HelpBox(issue.Message, MessageType(issue.Severity)));
                root.Add(summary);
            }
            if (!descriptor.IsAvailable)
            {
                foreach (KitchenLib.Studio.Generated.GdoDiagnosticData diagnostic in descriptor.Data.Diagnostics)
                    root.Add(new HelpBox(diagnostic.Path + ": " + diagnostic.Message, HelpBoxMessageType.Error));
            }
            GdoOverlayAuthoringAsset Overlay() => companion ?? (companion = GdoOverlayAuthoringRepository.GetOrCreate(gdo));
            foreach (GdoInspectorSection section in model.Sections)
                root.Add(new GdoInspectorSectionElement(section, serializedObject, gdo, Overlay, diagnostics, refresh));
            root.Bind(serializedObject);
            if (!descriptor.IsAvailable) root.SetEnabled(false);
        }

        private static HelpBoxMessageType MessageType(ValidationSeverity severity) => severity == ValidationSeverity.Info
            ? HelpBoxMessageType.Info : severity == ValidationSeverity.Warning ? HelpBoxMessageType.Warning : HelpBoxMessageType.Error;
    }
}
