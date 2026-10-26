using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

using KitchenLib.Studio.Editor.Packaging;

namespace KitchenLib.Studio.Editor.Authoring
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "KitchenLib.Studio.Editor", "KitchenLib.Studio.Editor", "GdoProjectIdentityDialog")]
    internal sealed class GdoProjectIdentityDialog : EditorWindow
    {
        private Action<string, string> _accepted;
        private string _initialAuthor;
        private string _initialProject;
        private TextField _author;
        private TextField _project;
        private Label _error;

        public static GdoProjectIdentityDialog OpenCreate(string initialAuthor, string initialProject,
            Action<string, string> accepted)
        {
            var dialog = CreateInstance<GdoProjectIdentityDialog>();
            dialog.titleContent = new GUIContent("Create Bundle Project");
            dialog._initialAuthor = initialAuthor;
            dialog._initialProject = initialProject;
            dialog._accepted = accepted;
            dialog.minSize = new Vector2(520f, 190f);
            dialog.ShowUtility();
            return dialog;
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 12f;
            rootVisualElement.style.paddingRight = 12f;
            rootVisualElement.style.paddingTop = 12f;
            rootVisualElement.style.paddingBottom = 12f;
            _author = new TextField("Author Namespace") { name = "author-namespace", value = _initialAuthor ?? string.Empty };
            rootVisualElement.Add(_author);
            _project = new TextField("Mod Namespace") { name = "mod-namespace", value = _initialProject ?? string.Empty };
            rootVisualElement.Add(_project);
            _error = new Label { name = "identity-error" };
            _error.style.whiteSpace = WhiteSpace.Normal;
            rootVisualElement.Add(_error);
            var actions = new VisualElement(); actions.style.flexDirection = FlexDirection.Row;
            actions.Add(new Button(Close) { text = "Cancel" });
            actions.Add(new Button(() => Accept(_author.value, _project.value))
                { name = "accept-identity", text = "Continue" });
            rootVisualElement.Add(actions);
        }

        public bool Accept(string authorNamespace, string projectName)
        {
            try
            {
                GdoProjectIdentity.ValidateName(authorNamespace, nameof(authorNamespace));
                GdoProjectIdentity.ValidateName(projectName, nameof(projectName));
                _accepted?.Invoke(authorNamespace.Trim(), projectName.Trim());
                Close();
                return true;
            }
            catch (Exception exception)
            {
                if (_error == null) CreateGUI();
                _error.text = exception.Message;
                return false;
            }
        }
    }
}
