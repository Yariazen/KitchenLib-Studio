using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace KitchenLib.Studio.Editor
{
    public sealed class GdoNameDialog : EditorWindow
    {
        private Action<string> _accepted;
        private Func<string, string> _destination;
        private string _initialName;
        private TextField _name;
        private Label _error;

        public static GdoNameDialog Open(string title, string initialName, Func<string, string> destination,
            Action<string> accepted)
        {
            var dialog = CreateInstance<GdoNameDialog>();
            dialog.titleContent = new GUIContent(title);
            dialog._initialName = initialName;
            dialog._destination = destination;
            dialog._accepted = accepted;
            dialog.minSize = new Vector2(520, 190);
            dialog.ShowUtility();
            return dialog;
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 12; rootVisualElement.style.paddingRight = 12;
            rootVisualElement.style.paddingTop = 12; rootVisualElement.style.paddingBottom = 12;
            _name = new TextField("Name") { name = "gdo-name", value = _initialName ?? "" };
            rootVisualElement.Add(_name);
            if (_destination != null)
            {
                rootVisualElement.Add(new Label("Destination (derived from name)"));
                var path = new Label { name = "derived-destination" };
                path.style.whiteSpace = WhiteSpace.Normal;
                rootVisualElement.Add(path);
                _name.RegisterValueChangedCallback(evt => path.text = _destination(evt.newValue));
                path.text = _destination(_name.value);
            }
            _error = new Label { name = "name-error" };
            _error.style.whiteSpace = WhiteSpace.Normal;
            rootVisualElement.Add(_error);
            var actions = new VisualElement(); actions.style.flexDirection = FlexDirection.Row;
            actions.Add(new Button(Close) { text = "Cancel" });
            actions.Add(new Button(() => Accept(_name.value)) { name = "accept-name", text = "Continue" });
            rootVisualElement.Add(actions);
        }

        public bool Accept(string value)
        {
            try
            {
                GdoProjectIdentity.ValidateName(value, nameof(value));
                _accepted?.Invoke(value);
                Close();
                return true;
            }
            catch (Exception error)
            {
                if (_error == null) CreateGUI();
                _error.text = error.Message;
                return false;
            }
        }
    }
}
