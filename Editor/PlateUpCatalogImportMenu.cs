using System;
using UnityEditor;

namespace KitchenLib.Studio.Editor
{
    public interface IPlateUpCatalogPreferenceStore { string Get(); void Set(string value); void Delete(); }

    public sealed class PlateUpCatalogInstallationSettings
    {
        private readonly IPlateUpCatalogPreferenceStore _store;
        private readonly Func<string, PlateUpInstallationValidation> _validate;
        public PlateUpCatalogInstallationSettings(IPlateUpCatalogPreferenceStore store, Func<string, PlateUpInstallationValidation> validate)
        { _store = store; _validate = validate; }
        public string SavedRoot => _store.Get();
        public bool TrySave(string path)
        {
            PlateUpInstallationValidation result = _validate(path);
            if (!result.IsValid) return false;
            _store.Set(result.Installation.GameRoot);
            return true;
        }
        public void Clear() => _store.Delete();
    }

    public static class PlateUpCatalogImportMenu
    {
        private const string PreferenceKey = "KitchenLibStudio.VanillaCatalog.PlateUpRoot";
        private static readonly PlateUpInstallationDiscovery Discovery = new PlateUpInstallationDiscovery();
        private static readonly EditorPreferenceStore Preferences = new EditorPreferenceStore();
        private static readonly PlateUpCatalogInstallationSettings Settings = new PlateUpCatalogInstallationSettings(Preferences, Discovery.ValidateSavedRoot);
        private static readonly PlateUpInstallationResolver Resolver = new PlateUpInstallationResolver(
            Preferences, Discovery.ValidateSavedRoot, Discovery.Discover,
            () => EditorUtility.OpenFolderPanel("Select PlateUp installation", Settings.SavedRoot ?? string.Empty, string.Empty));

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

        [MenuItem("KitchenLib Studio/Catalogues/Change PlateUp Installation…")]
        public static void ChangeInstallation()
        {
            string selected = EditorUtility.OpenFolderPanel("Select PlateUp installation", Settings.SavedRoot ?? string.Empty, string.Empty);
            if (string.IsNullOrEmpty(selected)) return;
            if (!Settings.TrySave(selected)) EditorUtility.DisplayDialog("Invalid PlateUp installation", string.Join("\n", Discovery.ValidateSavedRoot(selected).Diagnostics), "OK");
        }

        public static void ClearInstallation()
        {
            Settings.Clear();
            AssessSavedInstallation(VanillaGdoCatalogRepository.Current);
        }

        public static PlateUpInstallation ResolveInstallation(bool allowPicker)
        {
            PlateUpInstallationResolution resolution = Resolver.Resolve(allowPicker);
            VanillaGdoCatalogRepository.Current.AssessInstallation(resolution.Installation,
                resolution.Source == PlateUpInstallationSource.Saved);
            if (resolution.Installation == null && allowPicker && resolution.Diagnostics.Count != 0)
                EditorUtility.DisplayDialog("PlateUp installation not found", string.Join("\n", resolution.Diagnostics), "OK");
            return resolution.Installation;
        }

        internal static void AssessSavedInstallation(VanillaGdoCatalogRepository repository)
        {
            string saved = Settings.SavedRoot;
            if (string.IsNullOrEmpty(saved))
            {
                repository.AssessInstallation(null, false);
                return;
            }
            repository.AssessInstallation(Discovery.ValidateSavedRoot(saved).Installation, true);
        }

        private sealed class EditorPreferenceStore : IPlateUpCatalogPreferenceStore
        {
            public string Get() => EditorPrefs.HasKey(PreferenceKey) ? EditorPrefs.GetString(PreferenceKey) : null;
            public void Set(string value) => EditorPrefs.SetString(PreferenceKey, value);
            public void Delete() => EditorPrefs.DeleteKey(PreferenceKey);
        }
    }
}
