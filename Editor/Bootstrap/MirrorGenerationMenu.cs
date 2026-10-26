using System;
using System.IO;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace KitchenLib.Studio.Editor
{
    public static class MirrorGenerationMenu
    {
        [MenuItem("KitchenLib Studio/Generate Mirrors")]
        public static void RegenerateMirrors()
        {
            PlateUpInstallation installation = PlateUpInstallationSettings.ResolveInstallation(true);
            if (installation == null) return;
            try
            {
                if (EditorUtility.DisplayCancelableProgressBar("KitchenLib Studio", "Reading the selected PlateUp installation…", 0)) return;
                string result = GenerateMirrors(installation, CancellationToken.None,
                    () => EditorUtility.DisplayCancelableProgressBar("KitchenLib Studio", "Mirrors generated. Publish the complete generation?", 0.9f));
                Debug.Log(result);
            }
            catch (OperationCanceledException) { Debug.Log("Mirror generation cancelled. The previous generation was preserved."); }
            catch (Exception exception) { EditorUtility.DisplayDialog("Mirror generation failed", exception.Message, "OK"); }
            finally { EditorUtility.ClearProgressBar(); }
        }

        public static string GenerateMirrors(string plateUpRoot) => GenerateMirrors(
            new PlateUpInstallation(plateUpRoot, null, null, null, null, null), CancellationToken.None);

        public static string GenerateMirrors(PlateUpInstallation installation, CancellationToken cancellation) => GenerateMirrors(installation, cancellation, null);

        private static string GenerateMirrors(PlateUpInstallation installation, CancellationToken cancellation, Func<bool> cancelBeforePublication)
        {
            AtomicGeneratedDirectory.RecoverInterruptedPublication();
            string staging = Path.Combine(KitchenLibStudioGeneratedPaths.StateDirectory, "Staging", Guid.NewGuid().ToString("N"));
            bool wasEnabled = KitchenLibStudioDefineSymbols.MirrorsEnabled;
            try
            {
                PlateUpMirrorGenerationResult result = new PlateUpMirrorGenerator(cancellation).Generate(installation, staging);
                cancellation.ThrowIfCancellationRequested();
                if (cancelBeforePublication?.Invoke() == true) throw new OperationCanceledException();
                EditorApplication.LockReloadAssemblies();
                AssetDatabase.DisallowAutoRefresh();
                try
                {
                    AtomicGeneratedDirectory.Publish(staging, KitchenLibStudioGeneratedPaths.AssetDirectory, () => {
                        KitchenLibStudioMirrorState.WriteAtomically(result.ToState());
                        KitchenLibStudioBootstrap.CompletePublication();
                    }, cancellation);
                }
                catch
                {
                    KitchenLibStudioDefineSymbols.SetMirrorsEnabled(wasEnabled);
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    throw;
                }
                finally { AssetDatabase.AllowAutoRefresh(); EditorApplication.UnlockReloadAssemblies(); }
                return "Generated " + result.TypeCount + " PlateUp mirror types from the selected local installation.";
            }
            finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
        }

        public static string ResolveGameDataAssembly(string plateUpRoot)
        {
            if (string.IsNullOrWhiteSpace(plateUpRoot)) throw new ArgumentException("PlateUp root is required.", nameof(plateUpRoot));
            string path = Path.Combine(PlateUpPlatformLayout.ResolveLayout(plateUpRoot).ManagedDirectory, "Kitchen.GameData.dll");
            if (!File.Exists(path)) throw new FileNotFoundException("Kitchen.GameData.dll was not found beneath the selected PlateUp installation.", path);
            return path;
        }
    }
}
