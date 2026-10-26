using System;

using KitchenLib.Studio.Editor.Catalogs.Mods;

namespace KitchenLib.Studio.Editor.Catalogs.Vanilla
{
    [Serializable]
    internal sealed class VanillaGdoCatalogData
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
    internal sealed class VanillaGdoCatalogAssetData
    {
        public string relativePath;
        public string sha256;
    }

    [Serializable]
    internal sealed class VanillaGdoCatalogEntryData
    {
        public int id;
        public string name;
        public string runtimeType;
        public string assemblyName;
        public VanillaGdoCatalogLocationData[] locations;
    }

    [Serializable]
    internal sealed class VanillaGdoCatalogLocationData
    {
        public string relativePath;
        public long pathId;
    }

    [Serializable]
    internal sealed class VanillaGdoCatalogDiagnosticData
    {
        public string severity;
        public string code;
        public string message;
        public string relativePath;
        public long pathId;
    }
}
