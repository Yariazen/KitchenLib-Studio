using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace KitchenLib.Studio.Editor
{
    public interface IKitchenLibStudioRegenerationStageRunner
    { void Run(string stage, PlateUpInstallation installation, CancellationToken token); }
    public interface IKitchenLibStudioRegenerationFileSystem { void Delete(string path); }

    public sealed class RegenerationResult
    {
        private RegenerationResult(bool succeeded, string failedStage, string error, IReadOnlyList<string> completed)
        { Succeeded = succeeded; FailedStage = failedStage; Error = error; CompletedStages = completed; }
        public bool Succeeded { get; } public string FailedStage { get; } public string Error { get; }
        public IReadOnlyList<string> CompletedStages { get; }
        internal static RegenerationResult Success(IReadOnlyList<string> stages) => new RegenerationResult(true, null, null, stages);
        internal static RegenerationResult Failure(string stage, Exception exception, IReadOnlyList<string> stages) =>
            new RegenerationResult(false, stage, exception.Message, stages);
    }

    public sealed class KitchenLibStudioRegeneration
    {
        private static readonly string[] Stages = { "mirrors", "gdo-catalogue", "snapshot-catalogue", "asset-refresh" };
        private readonly string _projectRoot; private readonly IKitchenLibStudioRegenerationStageRunner _runner;
        private readonly IKitchenLibStudioRegenerationFileSystem _files;
        public KitchenLibStudioRegeneration(string projectRoot, IKitchenLibStudioRegenerationStageRunner runner = null,
            IKitchenLibStudioRegenerationFileSystem files = null)
        {
            _projectRoot = Path.GetFullPath(projectRoot ?? throw new ArgumentNullException(nameof(projectRoot)));
            _runner = runner ?? new DefaultRunner(); _files = files ?? new DefaultFiles();
        }

        public RegenerationResult RegenerateAll(PlateUpInstallation installation, CancellationToken token)
        {
            if (installation == null) throw new ArgumentNullException(nameof(installation));
            var completed = new List<string>();
            foreach (string stage in Stages)
            {
                try { token.ThrowIfCancellationRequested(); _runner.Run(stage, installation, token); completed.Add(stage); }
                catch (Exception exception) { return RegenerationResult.Failure(stage, exception, completed.ToArray()); }
            }
            return RegenerationResult.Success(completed);
        }

        public RegenerationResult CleanAndRegenerate(PlateUpInstallation installation, CancellationToken token)
        {
            try
            {
                foreach (string target in CleanTargets(_projectRoot)) { token.ThrowIfCancellationRequested(); _files.Delete(target); }
            }
            catch (Exception exception)
            {
                return RegenerationResult.Failure("clean-generated-environment", exception, Array.Empty<string>());
            }
            return RegenerateAll(installation, token);
        }

        public static IReadOnlyList<string> CleanTargets(string projectRoot)
        {
            string root = Path.GetFullPath(projectRoot ?? throw new ArgumentNullException(nameof(projectRoot)));
            string unity = Path.Combine(root, "KitchenLib Studio");
            return new[]
            {
                Path.Combine(unity, "Assets", "KitchenLibStudio", "Generated"),
                Path.Combine(unity, "Assets", "KitchenLibStudio", "Generated.meta"),
                Path.Combine(unity, "Assets", GdoBundleBuilder.StagingRoot.Substring("Assets/".Length).Replace('/', Path.DirectorySeparatorChar)),
                Path.Combine(unity, GdoBundleBuilder.StagingRoot.Replace('/', Path.DirectorySeparatorChar) + ".meta"),
                Path.Combine(unity, "Library", "KitchenLibStudio", "Snapshots")
            };
        }

        [MenuItem("KitchenLib Studio/Maintenance/Regenerate All")]
        public static void RegenerateAllMenu() => RunMenu(false);
        [MenuItem("KitchenLib Studio/Maintenance/Clean Generated Environment…")]
        public static void CleanAndRegenerateMenu()
        {
            if (EditorUtility.DisplayDialog("Clean generated KitchenLib Studio environment?",
                "This removes only KitchenLib Studio generated mirrors, catalogues, cache, and staging files. ModContent and validation proof assets are preserved.",
                "Clean and regenerate", "Cancel")) RunMenu(true);
        }

        public static void RegenerateAllBatch()
        {
            string requested = Environment.GetEnvironmentVariable("KITCHENLIB_STUDIO_PLATEUP_ROOT");
            var discovery = new PlateUpInstallationDiscovery();
            PlateUpInstallation installation;
            if (string.IsNullOrWhiteSpace(requested))
            {
                installation = PlateUpCatalogImportMenu.ResolveInstallation(false);
                if (installation == null) throw new InvalidOperationException("Select a PlateUp installation or set KITCHENLIB_STUDIO_PLATEUP_ROOT before regeneration.");
            }
            else
            {
                PlateUpInstallationValidation validation = discovery.ValidateSavedRoot(requested);
                if (!validation.IsValid) throw new InvalidOperationException(string.Join(Environment.NewLine, validation.Diagnostics));
                installation = validation.Installation;
            }
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            RegenerationResult result = new KitchenLibStudioRegeneration(root).RegenerateAll(installation, CancellationToken.None);
            if (!result.Succeeded) throw new InvalidOperationException(result.FailedStage + ": " + result.Error);
        }

        private static void RunMenu(bool clean)
        {
            PlateUpInstallation installation = PlateUpCatalogImportMenu.ResolveInstallation(true); if (installation == null) return;
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            RegenerationResult result = clean
                ? new KitchenLibStudioRegeneration(root).CleanAndRegenerate(installation, CancellationToken.None)
                : new KitchenLibStudioRegeneration(root).RegenerateAll(installation, CancellationToken.None);
            if (!result.Succeeded) EditorUtility.DisplayDialog("KitchenLib Studio regeneration failed", result.FailedStage + ": " + result.Error, "OK");
            else Debug.Log("KitchenLib Studio generated environment regenerated successfully.");
        }

        private sealed class DefaultRunner : IKitchenLibStudioRegenerationStageRunner
        {
            public void Run(string stage, PlateUpInstallation installation, CancellationToken token)
            {
                switch (stage)
                {
                    case "mirrors": MirrorGenerationMenu.GenerateMirrors(installation.GameRoot); break;
                    case "gdo-catalogue": PlateUpCatalogImportController.ImportGdoBatch(installation, token); break;
                    case "snapshot-catalogue": PlateUpCatalogImportController.ImportSnapshotBatch(installation, token); break;
                    case "asset-refresh": AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate); break;
                    default: throw new ArgumentOutOfRangeException(nameof(stage));
                }
            }
        }
        private sealed class DefaultFiles : IKitchenLibStudioRegenerationFileSystem
        {
            public void Delete(string path)
            {
                if (Directory.Exists(path)) Directory.Delete(path, true); else if (File.Exists(path)) File.Delete(path);
            }
        }
    }
}
