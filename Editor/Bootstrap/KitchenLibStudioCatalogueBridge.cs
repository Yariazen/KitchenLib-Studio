using System;
using UnityEditor;
using UnityEngine;

namespace KitchenLib.Studio.Editor.Bootstrap
{
    // The authoring assembly registers after mirror compilation. Bootstrap never depends on it.
    internal static class KitchenLibStudioCatalogueBridge
    {
        private const string PendingRootKey = "KitchenLibStudio.PendingCatalogueRoot";
        private static Action<PlateUpInstallation> _refresh;

        public static void Register(Action<PlateUpInstallation> refresh)
        {
            _refresh = refresh ?? throw new ArgumentNullException(nameof(refresh));
            EditorApplication.delayCall += Resume;
        }

        public static void Request(PlateUpInstallation installation)
        {
            if (installation == null) throw new ArgumentNullException(nameof(installation));
            SessionState.SetString(PendingRootKey, installation.GameRoot);
            EditorApplication.delayCall += Resume;
        }

        [MenuItem("KitchenLib Studio/Catalogues/Refresh All")]
        public static void RefreshAll()
        {
            PlateUpInstallation installation = PlateUpInstallationSettings.ResolveInstallation(true);
            if (installation == null) return;
            KitchenLibStudioMirrorState state = KitchenLibStudioMirrorState.Read();
            if (state?.IsCurrent(installation) == true)
            {
                Request(installation);
                return;
            }
            if (!EditorUtility.DisplayDialog("Generate PlateUp mirrors?",
                "Refreshing catalogues requires mirrors for the selected PlateUp installation. " +
                "Generate them now, then refresh the catalogues automatically?",
                "Generate Mirrors", "Cancel")) return;
            RegenerationResult result = new KitchenLibStudioRegeneration(
                KitchenLibStudioGeneratedPaths.ProjectRoot).RegenerateAll(installation,
                    System.Threading.CancellationToken.None);
            if (!result.Succeeded)
                EditorUtility.DisplayDialog("KitchenLib Studio regeneration failed",
                    result.FailedStage + ": " + result.Error, "OK");
        }

        private static void Resume()
        {
            string root = SessionState.GetString(PendingRootKey, string.Empty);
            if (string.IsNullOrEmpty(root) || _refresh == null || EditorApplication.isCompiling
                || KitchenLibStudioMirrorState.Read()?.HasCompleteOutput() != true) return;
            SessionState.EraseString(PendingRootKey);
            try
            {
                PlateUpInstallationValidation validation = new PlateUpInstallationDiscovery().ValidateSavedRoot(root);
                if (!validation.IsValid) throw new InvalidOperationException(string.Join("\n", validation.Diagnostics));
                _refresh(validation.Installation);
            }
            catch (Exception exception) { Debug.LogError("KitchenLib Studio catalogue refresh failed: " + exception.Message); }
        }
    }
}
