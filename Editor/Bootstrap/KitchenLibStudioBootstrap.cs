using System.IO;
using UnityEditor;
using UnityEngine;

namespace KitchenLib.Studio.Editor
{
    [InitializeOnLoad]
    public static class KitchenLibStudioBootstrap
    {
        private static double _nextValidation;

        static KitchenLibStudioBootstrap()
        {
            // Disable synchronously: an absent publication must never await the first-run window.
            InvalidateMissingOutput();
            EditorApplication.delayCall += Reconcile;
            // Unity compiles before InitializeOnLoad on a cold start. Leave the persisted gate
            // closed on normal exit, then validate and reopen it in the next editor session.
            EditorApplication.quitting += () => KitchenLibStudioDefineSymbols.SetMirrorsEnabled(false);
            EditorApplication.update += CheckOutput;
        }

        private static void CheckOutput()
        {
            if (EditorApplication.timeSinceStartup < _nextValidation) return;
            _nextValidation = EditorApplication.timeSinceStartup + 1;
            InvalidateMissingOutput();
        }

        public static void InvalidateMissingOutput()
        {
            if (KitchenLibStudioMirrorState.Read()?.HasCompleteOutput() != true)
                KitchenLibStudioDefineSymbols.SetMirrorsEnabled(false);
        }

        public static void Reconcile()
        {
            bool complete = KitchenLibStudioMirrorState.Read()?.HasCompleteOutput() == true;
            if (complete)
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                complete = KitchenLibStudioMirrorState.Read()?.HasCompleteOutput() == true;
            }
            bool wasEnabled = KitchenLibStudioDefineSymbols.MirrorsEnabled;
            KitchenLibStudioDefineSymbols.SetMirrorsEnabled(complete);
            if (complete && !wasEnabled) EditorUtility.RequestScriptReload();
            if (!complete && !Application.isBatchMode && !SessionState.GetBool("KitchenLibStudio.SetupShown", false))
            {
                SessionState.SetBool("KitchenLibStudio.SetupShown", true);
                KitchenLibStudioSetupWindow.ShowWindow();
            }
        }

        // Call after all generated files and their complete state record have been published.
        public static void CompletePublication()
        {
            if (KitchenLibStudioMirrorState.Read()?.HasCompleteOutput() != true)
            {
                KitchenLibStudioDefineSymbols.SetMirrorsEnabled(false);
                throw new InvalidDataException("Mirror publication is incomplete. Publish every generated file and its completion record before enabling authoring.");
            }
            Reconcile();
        }
    }

    internal sealed class KitchenLibStudioGeneratedAssetWatcher : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        { KitchenLibStudioBootstrap.InvalidateMissingOutput(); }
    }

    public sealed class KitchenLibStudioSetupWindow : EditorWindow
    {
        [MenuItem("KitchenLib Studio/Setup…")]
        public static void ShowWindow() => GetWindow<KitchenLibStudioSetupWindow>(true, "KitchenLib Studio Setup");

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Select your PlateUp installation, then generate the local mirrors to enable authoring. Generated files can be deleted and rebuilt.", MessageType.Info);
            EditorGUILayout.LabelField("PlateUp installation", PlateUpInstallationSettings.SavedRoot ?? "Not selected");
            if (GUILayout.Button("Select PlateUp Installation…")) PlateUpInstallationSettings.ChangeInstallation();
            if (GUILayout.Button("Generate Mirrors")) MirrorGenerationMenu.RegenerateMirrors();
        }
    }
}
