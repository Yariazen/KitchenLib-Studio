using System.IO;
using UnityEditor;
using UnityEngine;

namespace KitchenLib.Studio.Editor
{
    [InitializeOnLoad]
    public static class KitchenLibStudioBootstrap
    {
        private static double _nextValidation;
        private static bool _reconciling;

        static KitchenLibStudioBootstrap()
        {
            AtomicGeneratedDirectory.RecoverInterruptedPublication();
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
            _nextValidation = EditorApplication.timeSinceStartup + 5;
            Reconcile();
        }

        public static void InvalidateMissingOutput()
        {
            KitchenLibStudioMirrorState state = KitchenLibStudioMirrorState.Read();
            if (state?.HasCompleteOutput() != true || state.generatorVersion != PlateUpMirrorGenerator.Version)
                KitchenLibStudioDefineSymbols.SetMirrorsEnabled(false);
        }

        public static void Reconcile()
        {
            if (_reconciling) return;
            _reconciling = true;
            try
            {
                bool wasEnabled = KitchenLibStudioDefineSymbols.MirrorsEnabled;
                bool current = HasCurrentOutput();
                if (current && !wasEnabled)
                {
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    current = HasCurrentOutput();
                }
                KitchenLibStudioDefineSymbols.SetMirrorsEnabled(current);
                if (current)
                {
                    SessionState.SetBool("KitchenLibStudio.SetupShown", false);
                    if (!wasEnabled) EditorUtility.RequestScriptReload();
                }
                else if (!Application.isBatchMode && !SessionState.GetBool("KitchenLibStudio.SetupShown", false))
                {
                    SessionState.SetBool("KitchenLibStudio.SetupShown", true);
                    KitchenLibStudioSetupWindow.ShowWindow();
                }
            }
            finally { _reconciling = false; }
        }

        public static bool TryValidateCurrentOutput(out string error)
        {
            KitchenLibStudioMirrorState state = KitchenLibStudioMirrorState.Read();
            if (state?.HasCompleteOutput() != true)
            { error = "Generate KitchenLib Studio mirrors before building or importing content."; return false; }
            PlateUpInstallation installation = PlateUpInstallationSettings.ResolveInstallation(false);
            if (installation == null || !state.IsCurrent(installation))
            { error = "The generated mirrors do not match the selected PlateUp installation. Regenerate them before building or importing content."; return false; }
            error = null;
            return true;
        }

        public static void RequireCurrentOutput(PlateUpInstallation installation)
        {
            KitchenLibStudioMirrorState state = KitchenLibStudioMirrorState.Read();
            if (state?.IsCurrent(installation) != true)
                throw new InvalidDataException("The generated mirrors do not match this PlateUp installation. Regenerate them before importing content.");
        }

        private static bool HasCurrentOutput() => TryValidateCurrentOutput(out _);

        // Call after all generated files and their complete state record have been published.
        public static void CompletePublication()
        {
            if (KitchenLibStudioMirrorState.Read()?.HasCompleteOutput() != true)
            {
                KitchenLibStudioDefineSymbols.SetMirrorsEnabled(false);
                throw new InvalidDataException("Mirror publication is incomplete. Publish every generated file and its completion record before enabling authoring.");
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Reconcile();
            if (!KitchenLibStudioDefineSymbols.MirrorsEnabled)
                throw new InvalidDataException("The selected installation changed during mirror publication. Generate mirrors again when the installation is ready.");
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
            EditorGUILayout.HelpBox(KitchenLibStudioAttribution.TypeTreeNotice, MessageType.Info);
            EditorGUILayout.LabelField("PlateUp installation", PlateUpInstallationSettings.SavedRoot ?? "Not selected");
            if (GUILayout.Button("Select PlateUp Installation…")) PlateUpInstallationSettings.ChangeInstallation();
            if (GUILayout.Button("Generate Mirrors")) MirrorGenerationMenu.RegenerateMirrors();
        }
    }
}
