using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;

namespace KitchenLib.Studio.Editor
{
    /// <summary>A metadata-only closure. No installed game assembly is loaded or executed.</summary>
    public sealed class PlateUpMirrorModel : IDisposable
    {
        private readonly DefaultAssemblyResolver _resolver;
        private readonly Dictionary<string, TypeDefinition> _types = new Dictionary<string, TypeDefinition>(StringComparer.Ordinal);
        public IReadOnlyList<TypeDefinition> Types { get; private set; }
        public string SourceSha256 { get; private set; }
        public string SourceFingerprint { get; private set; }
        public IReadOnlyList<string> SourceAssemblies { get; private set; }

        private PlateUpMirrorModel(DefaultAssemblyResolver resolver, AssemblyDefinition source)
        { _resolver = resolver; }

        private sealed class MirrorAssemblyResolver : DefaultAssemblyResolver
        { public void RegisterSource(AssemblyDefinition assembly) => RegisterAssembly(assembly); }

        public static PlateUpMirrorModel Read(string kitchenGameDataAssembly, string managedDirectory)
        {
            var resolver = new MirrorAssemblyResolver();
            resolver.AddSearchDirectory(Path.GetFullPath(managedDirectory));
            PlateUpMirrorModel model = null;
            try
            {
                var source = AssemblyDefinition.ReadAssembly(kitchenGameDataAssembly,
                    new ReaderParameters { AssemblyResolver = resolver, ReadingMode = ReadingMode.Immediate, InMemory = true });
                resolver.RegisterSource(source);
                model = new PlateUpMirrorModel(resolver, source);
                foreach (TypeDefinition type in Flatten(source.MainModule.Types).Where(type => type.IsPublic || type.IsNestedPublic))
                    if (IsGameDataObject(type)) model.Add(type);
                if (model._types.Count == 0) throw new InvalidDataException("The selected assembly contains no KitchenData.GameDataObject types.");
                model.Types = model._types.Values.OrderBy(type => type.Module.Assembly.Name.Name, StringComparer.Ordinal)
                    .ThenBy(type => type.FullName, StringComparer.Ordinal).ToArray();
                model.SourceSha256 = PlateUpSchemaFingerprint.HashFile(kitchenGameDataAssembly);
                model.SourceAssemblies = model.Types.Select(type => type.Module.FileName).Append(source.MainModule.FileName)
                    .Distinct(StringComparer.Ordinal).OrderBy(Path.GetFileName, StringComparer.Ordinal)
                    .Select(path => Path.GetFileName(path) + "|" + PlateUpSchemaFingerprint.HashFile(path)).ToArray();
                model.SourceFingerprint = PlateUpSchemaFingerprint.HashText(string.Join("\n", model.SourceAssemblies));
                return model;
            }
            catch { if (model != null) model.Dispose(); else resolver.Dispose(); throw; }
        }

        private void Add(TypeReference reference)
        {
            if (reference == null || reference is GenericParameter) return;
            if (reference is GenericInstanceType generic)
            {
                foreach (TypeReference argument in generic.GenericArguments) Add(argument);
                reference = generic.ElementType;
            }
            else if (reference is TypeSpecification specification) { Add(specification.ElementType); return; }
            if (IsPlatformType(reference)) return;
            TypeDefinition type = reference.Resolve();
            if (type == null) throw new InvalidDataException("Cannot resolve required mirror type: " + reference.FullName);
            string key = type.Module.Assembly.Name.Name + ":" + type.FullName;
            if (_types.ContainsKey(key)) return;
            _types.Add(key, type);
            Add(type.DeclaringType);
            Add(type.BaseType);
            foreach (GenericParameter parameter in type.GenericParameters)
                foreach (GenericParameterConstraint constraint in parameter.Constraints) Add(constraint.ConstraintType);
            foreach (FieldDefinition field in Fields(type)) Add(field.FieldType);
            foreach (PropertyDefinition property in Properties(type)) Add(property.PropertyType);
        }

        internal static IEnumerable<TypeDefinition> Flatten(IEnumerable<TypeDefinition> types)
        {
            foreach (TypeDefinition type in types)
            { yield return type; foreach (TypeDefinition nested in Flatten(type.NestedTypes)) yield return nested; }
        }

        internal static IEnumerable<FieldDefinition> Fields(TypeDefinition type) => type.Fields.Where(field =>
            !field.IsStatic && !field.IsLiteral && (field.IsPublic || HasAttribute(field, "UnityEngine.SerializeField")
            || HasAttribute(field, "UnityEngine.SerializeReference") || HasAttribute(field, "Sirenix.Serialization.OdinSerializeAttribute")));

        internal static IEnumerable<PropertyDefinition> Properties(TypeDefinition type) => type.Properties.Where(property =>
            property.Parameters.Count == 0 && ((property.GetMethod?.IsPublic == true && !property.GetMethod.IsStatic)
            || (property.SetMethod?.IsPublic == true && !property.SetMethod.IsStatic)
            || HasAttribute(property, "Sirenix.Serialization.OdinSerializeAttribute")
            || Fields(type).Any(field => field.Name == "<" + property.Name + ">k__BackingField")));

        internal static bool HasAttribute(ICustomAttributeProvider provider, string name) =>
            provider.HasCustomAttributes && provider.CustomAttributes.Any(attribute => attribute.AttributeType.FullName == name);

        internal static bool IsPlatformType(TypeReference type)
        {
            string assembly = type.Scope?.Name ?? string.Empty;
            return assembly == "mscorlib" || assembly == "netstandard" || assembly == "System" || assembly.StartsWith("System.", StringComparison.Ordinal)
                || assembly == "UnityEngine" || assembly.StartsWith("UnityEngine.", StringComparison.Ordinal)
                || assembly == "Unity.TextMeshPro" || assembly.StartsWith("Sirenix.", StringComparison.Ordinal);
        }

        internal static bool IsGameDataObject(TypeReference type) => DerivesFrom(type, "KitchenData.GameDataObject");
        internal static bool DerivesFrom(TypeReference type, string name)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            while (type != null && !(type is GenericParameter) && seen.Add(type.FullName))
            {
                if (type.FullName == name || (type is GenericInstanceType generic && generic.ElementType.FullName == name)) return true;
                type = type.Resolve()?.BaseType;
            }
            return false;
        }

        public void Dispose() { _resolver.Dispose(); }
    }
}
