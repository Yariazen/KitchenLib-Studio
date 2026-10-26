using System;

namespace KitchenLib.Studio.Editor.Snapshots
{
    [Serializable] internal sealed class VanillaAssetCatalogData
    {
        public int schemaVersion; public string providerId; public string providerName; public int steamAppId;
        public string steamBuildId; public string importerVersion; public string unityVersion; public string sourceFingerprint;
        public VanillaAssetSourceData[] sources; public VanillaAssetRecordData[] records;
        public VanillaAssetEdgeData[] edges; public VanillaAssetDiagnosticData[] diagnostics;
    }
    [Serializable] internal sealed class VanillaAssetSourceData { public string relativePath; public string sha256; public string containerKind; public string unityVersion; }
    [Serializable] internal sealed class VanillaObjectIdentityData { public string relativePath; public long pathId; public int classId; public string className; }
    [Serializable] internal sealed class VanillaAssetRecordData
    { public string key; public string displayName; public string kind; public bool hierarchyRoot; public bool structural; public VanillaObjectIdentityData source; public VanillaAssetMetadataData[] metadata; public int gdoId; public string runtimeType; public string assemblyName; }
    [Serializable] internal sealed class VanillaAssetMetadataData { public string name; public string value; }
    [Serializable] internal sealed class VanillaAssetEdgeData { public string sourceKey; public string relation; public string slot; public string targetKey; }
    [Serializable] internal sealed class VanillaAssetDiagnosticData { public string severity; public string code; public string message; public string key; }
}
