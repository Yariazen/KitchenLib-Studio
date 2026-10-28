using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

using KitchenLib.Studio.Editor.Bootstrap;
using KitchenLib.Studio.Editor.Snapshots;

namespace KitchenLib.Studio.Editor.Catalogs.Vanilla
{
    [InitializeOnLoad]
    internal static class PlateUpCatalogImportController
    {
        private static CancellationTokenSource _cancellation;
        private static readonly object Gate = new object();
        private static Task _activeOperation = Task.CompletedTask;
        private static int _operationGeneration;
        public static event Action CatalogChanged;

        static PlateUpCatalogImportController()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Cancel;
            EditorApplication.quitting += Cancel;
        }

        public static async void Import(PlateUpInstallation installation)
        {
            if (installation == null) return;
            string schema = KitchenLibStudioGeneratedPaths.SchemaPath;
            CancellationTokenSource cancellation;
            int generation;
            Task<PlateUpCatalogImportResult> operation;
            lock (Gate)
            {
                _cancellation?.Cancel();
                cancellation = new CancellationTokenSource();
                _cancellation = cancellation;
                generation = ++_operationGeneration;
                var progress = new Progress<VanillaGdoScanProgress>(item =>
                {
                    if (generation != _operationGeneration) return;
                    if (EditorUtility.DisplayCancelableProgressBar("Import PlateUp Vanilla Catalog", item.RelativeAssetPath,
                        item.TotalObjects <= 0 ? 0f : (float)item.CompletedObjects / item.TotalObjects))
                        cancellation.Cancel();
                });
                var snapshotProgress = new Progress<VanillaSnapshotBatchProgress>(item =>
                {
                    if (generation != _operationGeneration) return;
                    string current = string.IsNullOrEmpty(item.GameObjectKey) ? string.Empty : ": " + item.GameObjectKey;
                    string label = "Rendering snapshot " + item.Completed + " of " + item.Total + current;
                    if (EditorUtility.DisplayCancelableProgressBar("Import PlateUp Vanilla Catalog", label,
                        item.Total <= 0 ? 1f : (float)item.Completed / item.Total))
                        cancellation.Cancel();
                });
                Task previous = _activeOperation;
                operation = RunQueued(previous, installation, schema, progress, snapshotProgress, cancellation.Token);
                _activeOperation = operation;
            }
            PlateUpCatalogImportResult result;
            try { result = await operation; }
            finally
            {
                lock (Gate)
                {
                    if (generation == _operationGeneration)
                    {
                        EditorUtility.ClearProgressBar();
                        _cancellation = null;
                    }
                }
                cancellation.Dispose();
            }
            if (generation != _operationGeneration) return;
            if (result.Succeeded)
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                CatalogChanged?.Invoke();
                Debug.Log("PlateUp vanilla GDO catalog imported.");
            }
            else if (!result.Cancelled) EditorUtility.DisplayDialog("PlateUp catalog import failed", result.Error, "OK");
        }

        public static void Cancel()
        {
            lock (Gate) _cancellation?.Cancel();
        }

        private static async Task<PlateUpCatalogImportResult> RunQueued(Task previous, PlateUpInstallation installation,
            string schema, IProgress<VanillaGdoScanProgress> progress,
            IProgress<VanillaSnapshotBatchProgress> snapshotProgress, CancellationToken token)
        {
            try { await previous; }
            catch { }
            if (token.IsCancellationRequested) return PlateUpCatalogImportResult.CancelledResult();
            try
            {
                KitchenLibStudioBootstrap.RequireCurrentOutput(installation);
                var service = new PlateUpCatalogImportService(new DefaultPlateUpCatalogImportPipeline(schema));
                return await service.ImportCombinedAsync(installation, PublicationPaths(), progress, token,
                    snapshotProgress);
            }
            catch (OperationCanceledException) { return PlateUpCatalogImportResult.CancelledResult(); }
            catch (Exception exception) { return PlateUpCatalogImportResult.Failure(exception.Message); }
        }

        public static void ImportBatch()
        {
            var discovery = new PlateUpInstallationDiscovery();
            string requestedRoot = Environment.GetEnvironmentVariable("KITCHENLIB_STUDIO_PLATEUP_ROOT");
            PlateUpInstallation installation;
            if (!string.IsNullOrWhiteSpace(requestedRoot))
            {
                PlateUpInstallationValidation validation = discovery.ValidateManualRoot(requestedRoot);
                if (!validation.IsValid) throw new InvalidOperationException(string.Join(Environment.NewLine, validation.Diagnostics));
                installation = validation.Installation;
            }
            else
            {
                PlateUpDiscoveryResult result = discovery.Discover();
                if (result.Installations.Count != 1)
                    throw new InvalidOperationException(string.Join(Environment.NewLine, result.Diagnostics));
                installation = result.Installations[0];
            }

            ImportCombinedBatch(installation, CancellationToken.None);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }

        public static void ImportGdoBatch(PlateUpInstallation installation, CancellationToken token)
        {
            ImportCombinedBatch(installation, token);
        }

        public static void ImportSnapshotBatch(PlateUpInstallation installation, CancellationToken token)
        {
            ImportCombinedBatch(installation, token);
        }

        public static void ImportCombinedBatch(PlateUpInstallation installation, CancellationToken token)
        {
            KitchenLibStudioBootstrap.RequireCurrentOutput(installation);
            var service = new PlateUpCatalogImportService(
                new DefaultPlateUpCatalogImportPipeline(KitchenLibStudioGeneratedPaths.SchemaPath));
            PlateUpCatalogImportResult result = service.ImportCombinedBlocking(installation, PublicationPaths(), null, token);
            if (result.Cancelled) throw new OperationCanceledException(token);
            if (!result.Succeeded) throw new InvalidOperationException(result.Error);
            CatalogChanged?.Invoke();
        }

        private static VanillaCatalogPublicationPaths PublicationPaths() =>
            new VanillaCatalogPublicationPaths(KitchenLibStudioGeneratedPaths.VanillaGdoCatalogPath,
                KitchenLibStudioGeneratedPaths.VanillaSnapshotCatalogPath,
                KitchenLibStudioGeneratedPaths.VanillaSnapshotIndexPath,
                KitchenLibStudioGeneratedPaths.SnapshotCacheRoot,
                KitchenLibStudioGeneratedPaths.CatalogueStagingRoot);
    }
}
