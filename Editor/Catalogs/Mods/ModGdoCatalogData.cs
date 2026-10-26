using System;

namespace KitchenLib.Studio.Editor.Catalogs.Mods
{
    [Serializable]
    internal sealed class ModGdoCatalogData
    {
        public int schemaVersion;
        public string captureToken;
        public int steamAppId;
        public string steamBuildId;
        public string bridgeVersion;
        public ModGdoProviderData[] providers;
        public ModGdoEntryData[] entries;
        public ExternalGdoRelationshipData[] relationships;
        public ModGdoCatalogDiagnosticData[] diagnostics;
    }

    [Serializable]
    internal sealed class ModGdoProviderData
    {
        public string key;
        public string displayName;
        public string version;
        public string workshopId;
        public string sourceAssembly;
        public string provenance;
    }

    [Serializable]
    internal sealed class ModGdoEntryData
    {
        public string providerKey;
        public int runtimeId;
        public string displayName;
        public string runtimeType;
        public string sourceAssembly;
        public string customRegistrationType;
        public bool hasPrefab;
        public string previewState;
        public string previewRelativePath;
        public ModGdoReferenceData[] references;
        public ModGdoRelationshipValueData[] relationshipValues;
    }

    [Serializable]
    internal sealed class ModGdoReferenceData
    {
        public string path;
        public string providerKey;
        public int runtimeId;
        public string runtimeType;
    }

    [Serializable]
    internal sealed class ModGdoRelationshipValueData
    {
        public string name;
        public string path;
        public string declaredType;
        public string runtimeType;
        public string kind;
        public int collectionIndex = -1;
        public string rawValue;
        public ModGdoResolvedIdentityData resolvedGdo;
        public ModGdoRelationshipValueData[] children;
    }

    [Serializable]
    internal sealed class ModGdoResolvedIdentityData
    {
        public string providerKey;
        public int runtimeId;
        public string runtimeType;
    }

    [Serializable]
    internal sealed class ModGdoCatalogDiagnosticData
    {
        public string severity;
        public string code;
        public string message;
        public string providerKey;
        public int runtimeId;
    }
}
