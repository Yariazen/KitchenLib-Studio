using System;
using KitchenData;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

using KitchenLib.Studio.Editor.Overlays;

namespace KitchenLib.Studio.Editor.Authoring
{
    internal sealed class GdoIntrinsicFieldElement : VisualElement
    {
        private readonly GdoInspectorField _field;
        private readonly GameDataObject _target;
        private readonly Func<GdoOverlayAuthoringAsset> _overlay;
        private readonly Action _changed;

        public GdoIntrinsicFieldElement(GdoInspectorField field, SerializedObject serializedObject,
            GameDataObject target, Func<GdoOverlayAuthoringAsset> overlay, Action changed)
        {
            _field = field ?? throw new ArgumentNullException(nameof(field));
            _target = target ?? throw new ArgumentNullException(nameof(target));
            _overlay = overlay ?? throw new ArgumentNullException(nameof(overlay));
            _changed = changed;
            name = field.Key;
            AddToClassList("kls-gdo-field");

            if (field.Descriptor.AuthoringStorage == "Native")
            {
                SerializedProperty property = serializedObject?.FindProperty(field.Descriptor.SerializedPropertyPath);
                if (property != null)
                {
                    var propertyField = new PropertyField(property, field.Name);
                    propertyField.RegisterValueChangeCallback(_ => _changed?.Invoke());
                    Add(propertyField);
                }
                else Add(new HelpBox("Native authoring invariant failed for " + field.Key +
                    ": serialized path '" + field.Descriptor.SerializedPropertyPath + "' was not found.",
                    HelpBoxMessageType.Error));
                return;
            }

            if (field.Descriptor.AuthoringStorage != "Sidecar")
            {
                Add(new HelpBox("Field is not supported by native or sidecar authoring: " + field.Key + ".",
                    HelpBoxMessageType.Error));
                return;
            }

            GdoOverlayAuthoringAsset current = GdoOverlayAuthoringRepository.Find(target);
            GdoOverlayValue value = current != null && current.TryGetField(field.DeclaringTypeName, field.Name, out GdoOverlayField authored)
                ? authored.Value : GdoOverlayValueEditor.CreateDefault(field.Descriptor.Value);
            Add(GdoInspectorValueElement.Create(field.Name, field.Descriptor.Value, value, CommitOverlay));
        }

        public void CommitOverlay(GdoOverlayValue value)
        {
            GdoOverlayAuthoringAsset companion = _overlay();
            Undo.RecordObject(companion, "Edit " + _field.Name);
            companion.UpsertField(_field.DeclaringTypeName, _field.Name, value);
            EditorUtility.SetDirty(companion);
            _changed?.Invoke();
        }
    }
}
