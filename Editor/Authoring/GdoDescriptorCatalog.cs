using System;
using System.Linq;
using System.Collections.Generic;
using KitchenData;
using KitchenLib.Studio.Generated;

namespace KitchenLib.Studio.Editor.Authoring
{
    internal sealed class GdoTypeDescriptorView
    {
        internal GdoTypeDescriptorView(GdoTypeDescriptorData data) { Data = data; }
        public GdoTypeDescriptorData Data { get; }
        public string RuntimeTypeName => Data.RuntimeTypeName;
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
}
