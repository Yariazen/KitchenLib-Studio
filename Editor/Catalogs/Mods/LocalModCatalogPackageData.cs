using System;

namespace KitchenLib.Studio.Editor.Catalogs.Mods
{
    [Serializable]
    internal sealed class LocalModCatalogPackageManifestData
    {
        public int schemaVersion;
        public string packageKind;
        public bool localOnly;
        public string providerKey;
        public string providerName;
        public string workshopId;
        public string modVersion;
        public string kitchenLibVersion;
        public string plateUpBuildId;
        public string sourceAssembly;
        public string sourceAssemblySha256;
        public string sourceAssetBundleSha256;
        public string catalogPath;
        public LocalModCatalogVisualData[] visuals;
    }

    [Serializable]
    internal sealed class LocalModCatalogVisualData
    {
        public int runtimeId;
        public string prefabPath;
        public string snapshotPath;
    }

    [Serializable]
    internal sealed class BakedPrefabData
    {
        public int schemaVersion;
        public int runtimeId;
        public string displayName;
        public BakedPrefabNodeData[] nodes;
        public BakedMeshData[] meshes;
        public BakedRendererData[] renderers;
        public BakedMaterialData[] materials;
        public BakedTextureData[] textures;
    }

    [Serializable]
    internal sealed class BakedPrefabNodeData
    {
        public string name;
        public int parentIndex;
        public bool active;
        public float[] localPosition;
        public float[] localRotation;
        public float[] localScale;
    }

    [Serializable]
    internal sealed class BakedMeshData
    {
        public string key;
        public string relativePath;
        public bool skinned;
    }

    [Serializable]
    internal sealed class BakedRendererData
    {
        public int nodeIndex;
        public string meshKey;
        public string[] materialKeys;
        public bool enabled;
    }

    [Serializable]
    internal sealed class BakedMaterialData
    {
        public string key;
        public string shaderName;
        public string propertyPath;
    }

    [Serializable]
    internal sealed class BakedTextureData
    {
        public string key;
        public string relativePath;
        public string format;
    }
}
