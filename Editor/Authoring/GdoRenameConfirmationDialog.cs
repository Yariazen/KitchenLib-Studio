using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

using KitchenLib.Studio.Editor.Packaging;

namespace KitchenLib.Studio.Editor.Authoring
{
    internal sealed class GdoRenameIdentityChange
    {
        public string OldCanonicalIdentity { get; }
        public string NewCanonicalIdentity { get; }
        public int OldId { get; }
        public int NewId { get; }

        internal GdoRenameIdentityChange(string oldIdentity, string newIdentity, int oldId, int newId)
        {
            OldCanonicalIdentity = oldIdentity; NewCanonicalIdentity = newIdentity;
            OldId = oldId; NewId = newId;
        }
    }

    internal sealed class GdoRenameConfirmationModel
    {
        public string OldName { get; }
        public string NewName { get; }
        public string Warning { get; }
        public bool RequiresCompatibilityWarning => true;
        public IReadOnlyList<GdoRenameIdentityChange> Changes { get; }
        public int AffectedCount => Changes.Count;

        public GdoRenameConfirmationModel(GdoRenamePreview preview)
        {
            if (preview == null) throw new ArgumentNullException(nameof(preview));
            OldName = preview.OldName; NewName = preview.NewName; Warning = preview.Warning;
            Changes = Array.AsReadOnly(new[] { new GdoRenameIdentityChange(
                GdoProjectIdentity.Canonical(preview.Namespace, preview.OldName),
                GdoProjectIdentity.Canonical(preview.Namespace, preview.NewName), preview.OldId, preview.NewId) });
        }

        public GdoRenameConfirmationModel(GdoProjectRenamePreview preview)
        {
            if (preview == null) throw new ArgumentNullException(nameof(preview));
            OldName = preview.OldNamespace; NewName = preview.NewNamespace; Warning = preview.Warning;
            Changes = Array.AsReadOnly(preview.GdoChanges.Select(change => new GdoRenameIdentityChange(
                GdoProjectIdentity.Canonical(preview.OldNamespace, change.AssetName),
                GdoProjectIdentity.Canonical(preview.NewNamespace, change.AssetName), change.OldId, change.NewId)).ToArray());
        }
    }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "KitchenLib.Studio.Editor", "KitchenLib.Studio.Editor", "GdoRenameConfirmationDialog")]
    internal sealed class GdoRenameConfirmationDialog : EditorWindow
    {
        private Action _confirmed;
        private Label _error;
        public GdoRenameConfirmationModel Model { get; private set; }

        public static GdoRenameConfirmationDialog Open(GdoRenameConfirmationModel model, Action confirmed)
        {
            var dialog = CreateInstance<GdoRenameConfirmationDialog>();
            dialog.titleContent = new GUIContent("Confirm Rename");
            dialog.Model = model ?? throw new ArgumentNullException(nameof(model));
            dialog._confirmed = confirmed;
            dialog.minSize = new Vector2(600, 320);
            dialog.ShowUtility();
            return dialog;
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            if (Model == null) return;
            rootVisualElement.style.paddingLeft = 12; rootVisualElement.style.paddingRight = 12;
            rootVisualElement.style.paddingTop = 12; rootVisualElement.style.paddingBottom = 12;
            rootVisualElement.Add(new Label(Model.OldName + " → " + Model.NewName));
            rootVisualElement.Add(new Label("Affected GDOs: " + Model.AffectedCount));
            var warning = new Label(Model.Warning) { name = "compatibility-warning" };
            warning.style.whiteSpace = WhiteSpace.Normal;
            rootVisualElement.Add(warning);
            var changes = new ScrollView(); changes.style.flexGrow = 1;
            foreach (GdoRenameIdentityChange change in Model.Changes)
            {
                var row = new Label(change.OldCanonicalIdentity + "\n→ " + change.NewCanonicalIdentity);
                row.style.whiteSpace = WhiteSpace.Normal;
                row.style.marginBottom = 8;
                changes.Add(row);
            }
            rootVisualElement.Add(changes);
            _error = new Label { name = "rename-error" }; _error.style.whiteSpace = WhiteSpace.Normal;
            rootVisualElement.Add(_error);
            var consent = new Toggle("I understand this clears Undo/Redo history") { name = "compatibility-consent" };
            rootVisualElement.Add(consent);
            var actions = new VisualElement(); actions.style.flexDirection = FlexDirection.Row;
            actions.Add(new Button(Close) { text = "Cancel" });
            var rename = new Button(() => Confirm(consent.value)) { name = "confirm-rename", text = "Rename" };
            rename.SetEnabled(false);
            consent.RegisterValueChangedCallback(evt => rename.SetEnabled(evt.newValue));
            actions.Add(rename);
            rootVisualElement.Add(actions);
        }

        public bool Confirm(bool compatibilityAccepted)
        {
            if (!compatibilityAccepted) return false;
            try { _confirmed?.Invoke(); Close(); return true; }
            catch (Exception error)
            {
                if (_error == null) CreateGUI();
                _error.text = error.Message;
                return false;
            }
        }
    }
}
