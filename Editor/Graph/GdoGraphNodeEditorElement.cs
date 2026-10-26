using System;
using System.Linq;
using KitchenData;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

using KitchenLib.Studio.Editor.Authoring;
using KitchenLib.Studio.Editor.Overlays;

namespace KitchenLib.Studio.Editor.Graph
{
    internal sealed class GdoGraphNodeEditorElement : VisualElement
    {
        public const int DefaultFieldLimit = 256;

        public GdoGraphNodeEditorElement(GameDataObject target, int fieldLimit = DefaultFieldLimit, Action changed = null)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (fieldLimit < 1) throw new ArgumentOutOfRangeException(nameof(fieldLimit));
            name = "gdo-node-editor";
            AddToClassList("kls-gdo-node-editor");
            var serialized = new SerializedObject(target);
            var id = new IntegerField("Stable GDO ID") { name = "stable-gdo-id", value = target.ID };
            id.SetEnabled(false);
            Add(id);
            GdoInspectorModel model = GdoInspectorModel.Build(GdoDescriptorCatalog.ForTarget(target));
            GdoInspectorField[] intrinsic = model.Section(GdoInspectorFieldKind.Authored).Fields
                .Where(field => field.Descriptor?.Value?.Kind != "UnityObject" ||
                    field.Descriptor.Value.RuntimeTypeName != "UnityEngine.GameObject").ToArray();
            foreach (GdoInspectorField field in intrinsic.Take(fieldLimit))
                Add(new GdoIntrinsicFieldElement(field, serialized, target,
                    () => GdoOverlayAuthoringRepository.GetOrCreate(target), changed));
            if (intrinsic.Length > fieldLimit)
                Add(new HelpBox("Only the first " + fieldLimit + " intrinsic fields are shown in this node.", HelpBoxMessageType.Warning)
                    { name = "field-limit" });

            this.Bind(serialized);
        }
    }
}
