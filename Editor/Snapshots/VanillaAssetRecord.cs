using System;
using System.Collections.Generic;
using System.Linq;

namespace KitchenLib.Studio.Editor
{
    public sealed class VanillaAssetRecord
    {
        internal VanillaAssetRecord(VanillaAssetRecordData data, IEnumerable<VanillaAssetEdgeData> edges)
        {
            Key = data.key; DisplayName = data.displayName; Kind = data.kind;
            Structural = data.structural; HierarchyRoot = data.hierarchyRoot;
            Source = new VanillaObjectIdentity(data.source.relativePath, data.source.pathId, data.source.classId, data.source.className);
            Metadata = (data.metadata ?? Array.Empty<VanillaAssetMetadataData>()).ToDictionary(item => item.name, item => item.value, StringComparer.Ordinal);
            Dependencies = (edges ?? Enumerable.Empty<VanillaAssetEdgeData>()).Select(edge => edge.targetKey).ToArray();
        }
        public string Key { get; } public string DisplayName { get; } public string Kind { get; }
        public bool Structural { get; } public bool HierarchyRoot { get; }
        public VanillaObjectIdentity Source { get; }
        public IReadOnlyDictionary<string, string> Metadata { get; }
        public IReadOnlyList<string> Dependencies { get; }
    }
}
