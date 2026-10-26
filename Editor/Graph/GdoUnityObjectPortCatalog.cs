using System;
using System.Collections.Generic;
using System.Linq;

using KitchenLib.Studio.Editor.Authoring;

namespace KitchenLib.Studio.Editor.Graph
{
    internal static class GdoUnityObjectPortCatalog
    {
        public static IReadOnlyList<GdoGraphPortDescriptor> ForDescriptor(GdoTypeDescriptorView descriptor)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            return GdoInspectorModel.Build(descriptor).Section(GdoInspectorFieldKind.Authored).Fields
                .Where(field => field.Descriptor?.Value?.Kind == "UnityObject" &&
                    IsGameObject(field.Descriptor.Value.RuntimeTypeName))
                .Select(field => new GdoGraphPortDescriptor(field.Key, field.Key, field.Key,
                    field.Descriptor.Value.RuntimeTypeName, field.Descriptor.IsRequired, false,
                    GdoGraphPortCardinality.Single, GdoGraphCollectionKind.None,
                    field.Descriptor.Value, field.Descriptor.Value))
                .OrderBy(port => port.Path, StringComparer.Ordinal).ToArray();
        }

        private static bool IsGameObject(string runtimeTypeName)
        {
            Type type = GdoGraphAssetCommands.ResolveType(runtimeTypeName);
            return type == null
                ? string.Equals(runtimeTypeName, "UnityEngine.GameObject", StringComparison.Ordinal)
                : typeof(UnityEngine.GameObject).IsAssignableFrom(type);
        }
    }
}
