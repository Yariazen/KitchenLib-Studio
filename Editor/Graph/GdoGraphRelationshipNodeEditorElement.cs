using System;
using System.Linq;
using KitchenLib.Studio.Generated;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

using KitchenLib.Studio.Editor.Authoring;
using KitchenLib.Studio.Editor.Overlays;

namespace KitchenLib.Studio.Editor.Graph
{
    internal sealed class GdoGraphRelationshipNodeEditorElement : VisualElement
    {
        private readonly GdoGraphConnectionController _controller;
        private readonly string _recordId;
        private readonly GdoValueDescriptorData _descriptor;
        private readonly string _rootPath;
        private readonly Action _structureChanged;
        private GdoOverlayValue _value;
        private string _removedCollectionPath;
        private int _removedCollectionIndex = -1;

        public GdoGraphRelationshipNodeEditorElement(GdoBundleGraphAsset graph, GdoGraphRelationshipRecord record,
            GdoValueDescriptorData descriptor, Action structureChanged = null)
        {
            _controller = new GdoGraphConnectionController(graph ?? throw new ArgumentNullException(nameof(graph)));
            _recordId = record?.Id ?? throw new ArgumentNullException(nameof(record));
            _descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            _rootPath = record.FieldPath;
            _structureChanged = structureChanged;
            _value = record.Value ?? GdoOverlayValueEditor.CreateDefault(descriptor);
            Rebuild();
        }

        public int EditableMemberCount { get; private set; }

        public void CommitMember(string memberName, GdoOverlayValue replacement)
        {
            Commit(GdoOverlayValueEditor.ReplaceStructMember(_value, memberName, replacement));
        }

        public void CommitCollectionRemoval(string collectionPath, int removedIndex, GdoOverlayValue replacement)
        {
            _removedCollectionPath = collectionPath;
            _removedCollectionIndex = removedIndex;
            Commit(replacement);
        }

        private void Commit(GdoOverlayValue replacement)
        {
            try
            {
                if (_removedCollectionIndex >= 0)
                    _controller.SetRelationshipValueAfterCollectionRemoval(_recordId, _removedCollectionPath,
                        _removedCollectionIndex, replacement);
                else
                    _controller.SetRelationshipValue(_recordId, replacement);
            }
            finally
            {
                _removedCollectionPath = null;
                _removedCollectionIndex = -1;
            }
            _value = replacement;
            Rebuild();
            _structureChanged?.Invoke();
        }

        private void Rebuild()
        {
            Clear();
            EditableMemberCount = 0;
            DrawValue(this, string.Empty, _rootPath, _descriptor, _value, Commit);
        }

        private void DrawValue(VisualElement parent, string label, string path, GdoValueDescriptorData descriptor,
            GdoOverlayValue value, Action<GdoOverlayValue> changed)
        {
            if (descriptor.Kind == "GdoReference")
                return;
            if (!GdoInspectorModel.ContainsGdoReference(descriptor))
            {
                EditableMemberCount++;
                parent.Add(GdoInspectorValueElement.Create(label, descriptor, value, changed));
                return;
            }
            if (descriptor.Kind == "Nullable")
            {
                bool hasValue = value != null && value.Kind != GdoOverlayValueKind.Null;
                var toggle = new Toggle(label + " Has Value") { value = hasValue };
                toggle.RegisterValueChangedCallback(evt => changed(evt.newValue
                    ? GdoOverlayValueEditor.CreateDefault(descriptor.Element) : GdoOverlayValue.Null()));
                parent.Add(toggle);
                if (hasValue) DrawValue(parent, label, path, descriptor.Element, value, changed);
                return;
            }
            if (descriptor.Kind == "Struct")
            {
                var foldout = new Foldout { text = label, value = true };
                foreach (GdoValueMemberDescriptorData member in descriptor.Members)
                {
                    GdoOverlayMember existing = value.Members.FirstOrDefault(item => item.Name == member.Name);
                    GdoOverlayValue memberValue = existing?.Value ?? GdoOverlayValueEditor.CreateDefault(member.Value);
                    DrawValue(foldout, member.Name, path + "." + member.Name, member.Value, memberValue,
                        replacement => changed(GdoOverlayValueEditor.ReplaceStructMember(value, member.Name, replacement)));
                }
                parent.Add(foldout);
                return;
            }
            if (descriptor.Kind == "List" || descriptor.Kind == "Array" || descriptor.Kind == "Set")
            {
                var foldout = new Foldout { text = label, value = true };
                for (int index = 0; index < value.Values.Count; index++)
                {
                    int captured = index;
                    var row = new VisualElement();
                    DrawValue(row, "Element " + index, path + "[" + index + "]", descriptor.Element, value.Values[index],
                        replacement => changed(GdoOverlayValueEditor.ReplaceCollectionElement(value, captured, replacement)));
                    row.Add(new Button(() =>
                    {
                        GdoOverlayValue collection = GdoOverlayValueEditor.RemoveCollectionElement(value, captured);
                        _removedCollectionPath = path;
                        _removedCollectionIndex = captured;
                        changed(collection);
                    }) { text = "Remove" });
                    foldout.Add(row);
                }
                foldout.Add(new Button(() => changed(GdoOverlayValueEditor.InsertCollectionElement(value, value.Values.Count,
                    GdoOverlayValueEditor.CreateDefault(descriptor.Element)))) { text = "Add" });
                parent.Add(foldout);
                return;
            }
            parent.Add(new HelpBox("Unsupported mixed relationship shape: " + descriptor.Kind, HelpBoxMessageType.Error));
        }
    }
}
