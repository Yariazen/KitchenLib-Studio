using System;
using UnityEditor;
using UnityEngine;

namespace KitchenLib.Studio.Editor
{
    // The authoring assembly registers after mirror compilation. Bootstrap never depends on it.
    public static class KitchenLibStudioCatalogueBridge
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
