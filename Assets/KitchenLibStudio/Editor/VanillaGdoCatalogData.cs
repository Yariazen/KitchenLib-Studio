using System;

namespace KitchenLib.Studio.Editor
{
    [Serializable]
    public sealed class VanillaGdoCatalogData
    {
        public int schemaVersion;
        public string providerId;
        public string providerName;
        public int steamAppId;
        public string steamBuildId;
        public string importerVersion;
        public string unityVersion;
        public string sourceAssemblyName;
        public string sourceAssemblySha256;
        public string schemaFingerprint;
        public VanillaGdoCatalogAssetData[] assets;
        public VanillaGdoCatalogEntryData[] entries;
        public ExternalGdoRelationshipData[] relationships;
        public VanillaGdoCatalogDiagnosticData[] diagnostics;
    }

    [Serializable]
    public sealed class VanillaGdoCatalogAssetData
    {
        public string relativePath;
        public string sha256;
    }

    [Serializable]
    public sealed class VanillaGdoCatalogEntryData
    {
        public int id;
        public string name;
        public string runtimeType;
        public string assemblyName;
        public VanillaGdoCatalogLocationData[] locations;
    }

    [Serializable]
    public sealed class VanillaGdoCatalogLocationData
    {
        public string relativePath;
        public long pathId;
    }

    [Serializable]
    public sealed class VanillaGdoCatalogDiagnosticData
    {
        public string severity;
        public string code;
        public string message;
        public string relativePath;
        public long pathId;
    }
}
