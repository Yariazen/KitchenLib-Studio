using UnityEditor;

namespace KitchenLib.Studio.Editor.Bootstrap
{
    internal static class PlateUpInstallationSettings
    {
        private const string PreferenceKey = "KitchenLibStudio.VanillaCatalog.PlateUpRoot";
        private static readonly PlateUpInstallationDiscovery Discovery = new PlateUpInstallationDiscovery();
        private static readonly EditorPreferenceStore Preferences = new EditorPreferenceStore();
        private static readonly PlateUpCatalogInstallationSettings Settings = new PlateUpCatalogInstallationSettings(Preferences, Discovery.ValidateSavedRoot);
        private static readonly PlateUpInstallationResolver Resolver = new PlateUpInstallationResolver(
            Preferences, Discovery.ValidateSavedRoot, Discovery.Discover,
            PlateUpInstallationDiscovery.StandardGameRoot(PlateUpPlatformLayout.CurrentPlatform,
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86)),
            () => EditorUtility.OpenFolderPanel("Select PlateUp installation", SavedRoot ?? string.Empty, string.Empty));

        public static string SavedRoot => Settings.SavedRoot;

        // Publication commits the explicit installation used by batch/API callers, too.
        internal static void SetSelectedRoot(string root)
        {
            if (string.IsNullOrEmpty(root)) Preferences.Delete();
            else Preferences.Set(root);
        }

        [MenuItem("KitchenLib Studio/Catalogues/Change PlateUp Installation…")]
        public static void ChangeInstallation()
        {
            string selected = EditorUtility.OpenFolderPanel("Select PlateUp installation", SavedRoot ?? string.Empty, string.Empty);
            if (string.IsNullOrEmpty(selected)) return;
            if (!Settings.TrySave(selected))
                EditorUtility.DisplayDialog("Invalid PlateUp installation", string.Join("\n", Discovery.ValidateSavedRoot(selected).Diagnostics), "OK");
        }

        public static void ClearInstallation() => Settings.Clear();
        public static PlateUpInstallation ResolveInstallation(bool allowPicker) => Resolve(allowPicker).Installation;

        public static PlateUpInstallationResolution Resolve(bool allowPicker)
        {
            PlateUpInstallationResolution resolution = Resolver.Resolve(allowPicker);
            if (resolution.Installation == null && allowPicker && resolution.Diagnostics.Count != 0)
                EditorUtility.DisplayDialog("PlateUp installation not found", string.Join("\n", resolution.Diagnostics), "OK");
            return resolution;
        }

        private sealed class EditorPreferenceStore : IPlateUpCatalogPreferenceStore
        {
            public string Get() => EditorPrefs.HasKey(PreferenceKey) ? EditorPrefs.GetString(PreferenceKey) : null;
            public void Set(string value) => EditorPrefs.SetString(PreferenceKey, value);
            public void Delete() => EditorPrefs.DeleteKey(PreferenceKey);
        }
    }
}
