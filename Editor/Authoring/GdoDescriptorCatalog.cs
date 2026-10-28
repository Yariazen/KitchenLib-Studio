using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using KitchenData;
using KitchenLib.Studio.Generated;

namespace KitchenLib.Studio.Editor.Authoring
{
    internal sealed class GdoTypeDescriptorView
    {
        internal GdoTypeDescriptorView(GdoTypeDescriptorData data) { Data = data; }
        public GdoTypeDescriptorData Data { get; }
        public string RuntimeTypeName => Data.RuntimeTypeName;
        public string BaseTypeName => Data.BaseTypeName;
        public string DescriptorFingerprint => Data.DescriptorFingerprint;
        public bool IsAvailable => Data.Availability == "Available";
    }

    internal static class GdoDescriptorCatalog
    {
        public static string GlobalFingerprint => GdoSchemaRegistry.GlobalFingerprint;

        public static GdoTypeDescriptorView ForTarget(GameDataObject target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            Type type = target.GetType();
            GdoTypeDescriptorData descriptor = GdoSchemaRegistry.Types.FirstOrDefault(item => item.RuntimeTypeName == type.FullName);
            if (descriptor != null) return new GdoTypeDescriptorView(descriptor);
            throw new InvalidOperationException("No generated GDO descriptor exists for '" + target.GetType().FullName + "'. Regenerate KitchenLib Studio against the installed PlateUp version.");
        }

        public static GdoTypeDescriptorView ForRuntimeType(string runtimeTypeName)
        {
            GdoTypeDescriptorData descriptor = GdoSchemaRegistry.Types.FirstOrDefault(item => item.RuntimeTypeName == runtimeTypeName);
            return descriptor == null ? null : new GdoTypeDescriptorView(descriptor);
        }

        public static bool IsAssignable(string concreteRuntimeType, string expectedRuntimeType)
        {
            if (string.IsNullOrEmpty(concreteRuntimeType) || string.IsNullOrEmpty(expectedRuntimeType)) return false;
            string current = Normalize(concreteRuntimeType);
            string expected = Normalize(expectedRuntimeType);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            while (visited.Add(current))
            {
                if (current == expected) return true;
                GdoTypeDescriptorData descriptor = GdoSchemaRegistry.Types.FirstOrDefault(item => Normalize(item.RuntimeTypeName) == current);
                if (descriptor == null || string.IsNullOrEmpty(descriptor.BaseTypeName)) return false;
                current = Normalize(descriptor.BaseTypeName);
            }
            return false;
        }

        private static string Normalize(string runtimeType)
        {
            int generic = runtimeType.IndexOf('<');
            return generic < 0 ? runtimeType : runtimeType.Substring(0, generic);
        }
    }

    internal static class GdoFieldDescriptorCompatibility
    {
        private static readonly FieldInfo AuthoringStorageField = FindField("AuthoringStorage");
        private static readonly FieldInfo SerializedPropertyPathField = FindField("SerializedPropertyPath");

        public static string AuthoringStorage(GdoFieldDescriptorData descriptor) =>
            ReadString(AuthoringStorageField, descriptor) ?? "Unsupported";

        public static string SerializedPropertyPath(GdoFieldDescriptorData descriptor) =>
            ReadString(SerializedPropertyPathField, descriptor);

        private static FieldInfo FindField(string name) => typeof(GdoFieldDescriptorData).GetField(name,
            BindingFlags.Instance | BindingFlags.Public);

        private static string ReadString(FieldInfo field, GdoFieldDescriptorData descriptor) =>
            descriptor == null || field == null ? null : field.GetValue(descriptor) as string;
    }
}
