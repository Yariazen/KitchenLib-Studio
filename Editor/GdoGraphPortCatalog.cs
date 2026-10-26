using System;
using System.Collections.Generic;
using System.Linq;
using KitchenLib.Studio.Generated;

namespace KitchenLib.Studio.Editor
{
    public static class GdoGraphPortCatalog
    {
        public static IReadOnlyList<GdoGraphPortDescriptor> ForDescriptor(GdoTypeDescriptorView descriptor)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            var result = new List<GdoGraphPortDescriptor>();
            foreach (GdoInspectorField field in GdoInspectorModel.Build(descriptor)
                .Section(GdoInspectorFieldKind.Relationship).Fields)
            {
                string fieldKey = field.Key;
                Collect(field.Descriptor.Value, field.Descriptor.Value, fieldKey, fieldKey, fieldKey,
                    field.Descriptor.IsRequired, false, GdoGraphCollectionKind.None, result);
            }
            return result.OrderBy(value => value.Path, StringComparer.Ordinal).ToArray();
        }

        private static void Collect(GdoValueDescriptorData root, GdoValueDescriptorData value, string fieldKey,
            string path, string relationshipPath, bool required, bool structured,
            GdoGraphCollectionKind collectionKind, ICollection<GdoGraphPortDescriptor> result)
        {
            if (value == null) return;
            if (value.Kind == "GdoReference")
            {
                bool multiple = collectionKind != GdoGraphCollectionKind.None && !structured;
                result.Add(new GdoGraphPortDescriptor(fieldKey, path, relationshipPath,
                    value.ExpectedTypeName, required, structured,
                    multiple ? GdoGraphPortCardinality.Multiple : GdoGraphPortCardinality.Single,
                    collectionKind, root, value));
                return;
            }
            if (value.Kind == "Nullable")
            {
                Collect(root, value.Element, fieldKey, path, relationshipPath, false, structured, collectionKind, result);
                return;
            }
            if (value.Kind == "List" || value.Kind == "Array" || value.Kind == "Set")
            {
                GdoGraphCollectionKind kind = value.Kind == "Set" ? GdoGraphCollectionKind.Set :
                    value.Kind == "Array" ? GdoGraphCollectionKind.Array : GdoGraphCollectionKind.List;
                string elementPath = path + "[]";
                bool elementStructured = value.Element != null && value.Element.Kind != "GdoReference" &&
                    !(value.Element.Kind == "Nullable" && value.Element.Element != null && value.Element.Element.Kind == "GdoReference");
                bool childStructured = structured || elementStructured;
                string childRelationshipPath = structured ? relationshipPath : elementStructured ? elementPath : fieldKey;
                Collect(root, value.Element, fieldKey, elementPath, childRelationshipPath,
                    required, childStructured, kind, result);
                return;
            }
            if (value.Kind == "Struct")
            {
                string ownerPath = structured ? relationshipPath : path;
                foreach (GdoValueMemberDescriptorData member in value.Members)
                    Collect(root, member.Value, fieldKey, path + "." + member.Name, ownerPath,
                        required, true, collectionKind, result);
            }
        }
    }
}
