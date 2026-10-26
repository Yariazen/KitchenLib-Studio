using System;
using System.Collections.Generic;
using System.Linq;
using KitchenLib.Studio.Generated;

namespace KitchenLib.Studio.Editor
{
    public enum GdoInspectorFieldKind { Authored, Relationship, RuntimeDerived, Unsupported }

    public sealed class GdoInspectorField
    {
        internal GdoInspectorField(string declaringTypeName, string name, GdoInspectorFieldKind kind,
            bool isReadOnly, string description, GdoFieldDescriptorData descriptor)
        {
            DeclaringTypeName = declaringTypeName;
            Name = name;
            Kind = kind;
            IsReadOnly = isReadOnly;
            Description = description ?? string.Empty;
            Descriptor = descriptor;
        }

        public string DeclaringTypeName { get; }
        public string Name { get; }
        public string Key => DeclaringTypeName + "." + Name;
        public GdoInspectorFieldKind Kind { get; }
        public bool IsReadOnly { get; }
        public string Description { get; }
        public GdoFieldDescriptorData Descriptor { get; }
    }

    public sealed class GdoInspectorGroup
    {
        internal GdoInspectorGroup(string declaringTypeName, IEnumerable<GdoInspectorField> fields)
        {
            DeclaringTypeName = declaringTypeName;
            Fields = fields.ToArray();
        }

        public string DeclaringTypeName { get; }
        public IReadOnlyList<GdoInspectorField> Fields { get; }
    }

    public sealed class GdoInspectorSection
    {
        internal GdoInspectorSection(GdoInspectorFieldKind kind, IEnumerable<GdoInspectorGroup> groups)
        {
            Kind = kind;
            Groups = groups.ToArray();
            Fields = Groups.SelectMany(group => group.Fields).ToArray();
        }

        public GdoInspectorFieldKind Kind { get; }
        public IReadOnlyList<GdoInspectorGroup> Groups { get; }
        public IReadOnlyList<GdoInspectorField> Fields { get; }
    }

    public sealed class GdoInspectorModel
    {
        private static readonly GdoInspectorFieldKind[] SectionOrder =
        {
            GdoInspectorFieldKind.Authored,
            GdoInspectorFieldKind.Relationship,
            GdoInspectorFieldKind.RuntimeDerived,
            GdoInspectorFieldKind.Unsupported
        };

        private GdoInspectorModel(string runtimeTypeName, IEnumerable<GdoInspectorSection> sections)
        {
            RuntimeTypeName = runtimeTypeName;
            Sections = sections.ToArray();
        }

        public string RuntimeTypeName { get; }
        public IReadOnlyList<GdoInspectorSection> Sections { get; }

        public GdoInspectorSection Section(GdoInspectorFieldKind kind) => Sections.Single(section => section.Kind == kind);

        public static GdoInspectorModel Build(GdoTypeDescriptorView descriptor)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            List<GdoTypeDescriptorView> hierarchy = Hierarchy(descriptor);
            var fields = new List<GdoInspectorField>();
            foreach (GdoTypeDescriptorView type in hierarchy)
            {
                foreach (GdoFieldDescriptorData field in type.Data.Fields.OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    bool unsupported = ContainsKind(field.Value, "Unsupported");
                    bool stableId = field.Name == "ID" && field.DeclaringTypeName == "KitchenData.GameDataObject";
                    GdoInspectorFieldKind kind = stableId
                        ? GdoInspectorFieldKind.RuntimeDerived
                        : unsupported
                        ? GdoInspectorFieldKind.Unsupported
                        : ContainsGdoReference(field.Value) ? GdoInspectorFieldKind.Relationship : GdoInspectorFieldKind.Authored;
                    fields.Add(new GdoInspectorField(field.DeclaringTypeName, field.Name, kind, unsupported || stableId,
                        stableId ? "Generated once from the project identity and preserved across renames."
                        : unsupported ? DescribeUnsupported(field.Value, field.DeclaringTypeName + "." + field.Name) : string.Empty, field));
                }
            }

            HashSet<string> hierarchyTypes = new HashSet<string>(hierarchy.Select(item => item.RuntimeTypeName), StringComparer.Ordinal);
            foreach (GdoRuntimeFieldMappingData mapping in GdoSchemaRegistry.RuntimeMappings
                .Where(item => hierarchyTypes.Contains(item.DeclaringTypeName))
                .OrderBy(item => item.DeclaringTypeName, StringComparer.Ordinal)
                .ThenBy(item => item.RuntimeFieldName, StringComparer.Ordinal))
            {
                fields.Add(new GdoInspectorField(mapping.DeclaringTypeName, mapping.RuntimeFieldName,
                    GdoInspectorFieldKind.RuntimeDerived, true,
                    "Populated from " + mapping.AuthoringFieldName + " by PlateUp " + mapping.PopulationMethod + ".", null));
            }

            GdoInspectorSection[] sections = SectionOrder.Select(kind => new GdoInspectorSection(kind,
                fields.Where(field => field.Kind == kind)
                    .GroupBy(field => field.DeclaringTypeName, StringComparer.Ordinal)
                    .OrderBy(group => HierarchyIndex(hierarchy, group.Key))
                    .ThenBy(group => group.Key, StringComparer.Ordinal)
                    .Select(group => new GdoInspectorGroup(group.Key, group.OrderBy(field => field.Name, StringComparer.Ordinal))))).ToArray();
            return new GdoInspectorModel(descriptor.RuntimeTypeName, sections);
        }

        public static bool ContainsGdoReference(GdoValueDescriptorData value) =>
            value != null && (value.Kind == "GdoReference" || ContainsGdoReference(value.Element) ||
                value.Members.Any(member => ContainsGdoReference(member.Value)));

        private static bool ContainsKind(GdoValueDescriptorData value, string kind) =>
            value != null && (value.Kind == kind || ContainsKind(value.Element, kind) ||
                value.Members.Any(member => ContainsKind(member.Value, kind)));

        private static string DescribeUnsupported(GdoValueDescriptorData value, string path)
        {
            var descriptions = new List<string>();
            CollectUnsupported(value, path, descriptions);
            return string.Join(" ", descriptions);
        }

        private static void CollectUnsupported(GdoValueDescriptorData value, string path, ICollection<string> descriptions)
        {
            if (value == null) return;
            if (value.Kind == "Unsupported")
                descriptions.Add("Unsupported serialized shape '" + value.RuntimeTypeName + "' at '" + path + "'.");
            if (value.Element != null) CollectUnsupported(value.Element, path + "[]", descriptions);
            foreach (GdoValueMemberDescriptorData member in value.Members)
                CollectUnsupported(member.Value, path + "." + member.Name, descriptions);
        }

        private static List<GdoTypeDescriptorView> Hierarchy(GdoTypeDescriptorView descriptor)
        {
            var result = new List<GdoTypeDescriptorView>();
            for (GdoTypeDescriptorView current = descriptor; current != null;)
            {
                result.Add(current);
                string baseName = current.Data.BaseTypeName;
                current = string.IsNullOrEmpty(baseName) ? null : GdoDescriptorCatalog.ForRuntimeType(baseName) ??
                    GdoDescriptorCatalog.ForRuntimeType(GenericDefinitionName(baseName));
            }
            result.Reverse();
            return result;
        }

        private static string GenericDefinitionName(string name)
        {
            int genericArguments = name.IndexOf('<');
            return genericArguments < 0 ? name : name.Substring(0, genericArguments);
        }

        private static int HierarchyIndex(IReadOnlyList<GdoTypeDescriptorView> hierarchy, string declaringType)
        {
            for (int index = 0; index < hierarchy.Count; index++)
                if (hierarchy[index].RuntimeTypeName == declaringType) return index;
            return int.MaxValue;
        }
    }
}
