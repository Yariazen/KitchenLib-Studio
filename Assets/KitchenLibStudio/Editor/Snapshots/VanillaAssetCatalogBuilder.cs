namespace KitchenLib.Studio.Editor
{
    public static class VanillaAssetCatalogBuilder
    {
        public static VanillaAssetCatalogData Build(VanillaAssetCatalogData data)
        { VanillaAssetCatalogFile.Validate(data); return data; }
    }
}
