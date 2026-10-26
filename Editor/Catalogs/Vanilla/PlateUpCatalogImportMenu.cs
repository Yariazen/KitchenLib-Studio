using System;
using UnityEditor;

using KitchenLib.Studio.Editor.Bootstrap;

namespace KitchenLib.Studio.Editor.Catalogs.Vanilla
{
    internal static class PlateUpCatalogImportMenu
    {
        public static void Import()
        {
            PlateUpInstallation installation = ResolveInstallation(true);
            if (installation != null) PlateUpCatalogImportController.Import(installation);
        }

        [MenuItem("KitchenLib Studio/Catalogues/Refresh All")]
        public static void Refresh()
        {
            PlateUpInstallation installation = ResolveInstallation(true);
            if (installation != null) PlateUpCatalogImportController.Import(installation);
        }

        [MenuItem("KitchenLib Studio/Catalogues/Refresh Vanilla GDO Catalogue")]
        public static void RefreshVanillaGdoCatalogue()
        {
            PlateUpInstallation installation = ResolveInstallation(true);
            if (installation == null) return;
            PlateUpCatalogImportController.ImportGdoBatch(installation, System.Threading.CancellationToken.None);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }

        [MenuItem("KitchenLib Studio/Catalogues/Refresh Snapshot Catalogue")]
        public static void RefreshSnapshotCatalogue()
        {
            PlateUpInstallation installation = ResolveInstallation(true);
            if (installation == null) return;
            PlateUpCatalogImportController.ImportSnapshotBatch(installation, System.Threading.CancellationToken.None);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }

        public static void ChangeInstallation() => PlateUpInstallationSettings.ChangeInstallation();

        public static void ClearInstallation()
        {
            PlateUpInstallationSettings.ClearInstallation();
            AssessSavedInstallation(VanillaGdoCatalogRepository.Current);
        }

        public static PlateUpInstallation ResolveInstallation(bool allowPicker)
        {
            PlateUpInstallationResolution resolution = PlateUpInstallationSettings.Resolve(allowPicker);
            VanillaGdoCatalogRepository.Current.AssessInstallation(resolution.Installation,
                resolution.Source == PlateUpInstallationSource.Saved);
            return resolution.Installation;
        }

        internal static void AssessSavedInstallation(VanillaGdoCatalogRepository repository)
        {
            string saved = PlateUpInstallationSettings.SavedRoot;
            repository.AssessInstallation(string.IsNullOrEmpty(saved) ? null :
                new PlateUpInstallationDiscovery().ValidateSavedRoot(saved).Installation, !string.IsNullOrEmpty(saved));
        }
    }
}
