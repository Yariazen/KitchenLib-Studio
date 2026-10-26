namespace KitchenLib.Studio.Editor.Snapshots
{
    internal static class VanillaAssetCatalogBuilder
    {
        public static VanillaAssetCatalogData Build(VanillaAssetCatalogData data)
        { VanillaAssetCatalogFile.Validate(data); return data; }
    }
}
