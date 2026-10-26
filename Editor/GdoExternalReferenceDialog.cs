using System;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace KitchenLib.Studio.Editor
{
    public sealed class GdoExternalReferenceDialog : EditorWindow
    {
        private Action<GdoBundleGraphAsset, string, int, string> _accepted;
        private GdoBundleGraphAsset _targetGraph;
        private IntegerField _id;
        private TextField _type;
        private string _expectedType;
        private string _initialType;
        private ExternalGdoCatalogRepository _repository;
        private int _currentId;

        public GdoBundleGraphAsset TargetGraph => _targetGraph;
        public ExternalGdoPicker Picker { get; private set; }

        public static GdoExternalReferenceDialog Open(GdoBundleGraphAsset targetGraph,
            Action<GdoBundleGraphAsset, string, int, string> accepted)
            => Open(targetGraph, accepted, null, ExternalGdoCatalogRepository.Current);

        public static GdoExternalReferenceDialog Open(GdoBundleGraphAsset targetGraph,
            Action<GdoBundleGraphAsset, string, int, string> accepted, string expectedType,
            ExternalGdoCatalogRepository repository, int currentId = 0, string initialType = null)
        {
            var window = CreateInstance<GdoExternalReferenceDialog>();
            window.titleContent = new GUIContent("External GDO Reference");
            window._accepted = accepted;
            window._targetGraph = targetGraph;
            window._expectedType = expectedType;
            window._initialType = initialType;
            window._repository = repository ?? ExternalGdoCatalogRepository.Current;
            window._currentId = currentId;
            window.minSize = new Vector2(520, 420);
            window.maxSize = new Vector2(820, 720);
            window.ShowUtility();
            return window;
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 12; rootVisualElement.style.paddingRight = 12;
            rootVisualElement.style.paddingTop = 12; rootVisualElement.style.paddingBottom = 12;
            rootVisualElement.style.flexGrow = 1f;
            var manualAccept = new Button(AcceptFromFields)
            {
                name = "accept-manual-reference",
                text = _currentId == 0 ? "Add Reference" : "Save Reference"
            };
            manualAccept.style.display = DisplayStyle.None;
            Picker = new ExternalGdoPicker(_expectedType, _repository ?? ExternalGdoCatalogRepository.Current, Accept, false);
            Picker.ManualExpandedChanged += expanded =>
                manualAccept.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
            rootVisualElement.Add(Picker);
            _id = new IntegerField("External GDO ID") { value = _currentId == 0 ? 1 : _currentId };
            _type = new TextField("Expected GDO type") { name = "external-expected-type", value = _initialType ?? _expectedType ?? "KitchenData.Item" };
            _type.SetEnabled(string.IsNullOrEmpty(_expectedType));
            ExternalGdoCatalogEntry current = _repository?.Entries.FirstOrDefault(item => item.RuntimeId == _currentId);
            if (current != null) Picker.ShowSelection(current);
            Picker.ManualContent.Add(_id);
            Picker.ManualContent.Add(_type);
            var actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.justifyContent = Justify.FlexEnd;
            actions.style.marginTop = 8f;
            actions.Add(new Button(Close) { name = "cancel-external-reference", text = "Cancel" });
            actions.Add(manualAccept);
            rootVisualElement.Add(actions);
            rootVisualElement.schedule.Execute(Picker.FocusSearch);
        }

        private void AcceptFromFields() => Accept(ExternalGdoCatalogRepository.VanillaProviderKey, _id.value, _type.value);

        public void Accept(ExternalGdoCatalogEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            Accept(entry.ProviderKey, entry.RuntimeId, string.IsNullOrEmpty(_expectedType) ? entry.RuntimeType : _expectedType);
        }

        public void Accept(string providerKey, int runtimeId, string expectedType)
        {
            if (runtimeId == 0 || string.IsNullOrWhiteSpace(expectedType))
            {
                ShowNotification(new GUIContent("Enter a non-zero runtime ID and expected type."));
                return;
            }
            _accepted?.Invoke(_targetGraph, providerKey, runtimeId, expectedType.Trim());
            Close();
        }
    }
}
