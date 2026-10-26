using System;
using System.IO;
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
            string destination = KitchenLibStudioGeneratedPaths.VanillaGdoCatalogPath;
            string snapshotDestination = KitchenLibStudioGeneratedPaths.VanillaSnapshotCatalogPath;
            string snapshotIndexDestination = KitchenLibStudioGeneratedPaths.VanillaSnapshotIndexPath;
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
                Task previous = _activeOperation;
                operation = RunQueued(previous, installation, destination, snapshotDestination, snapshotIndexDestination, schema, progress, cancellation.Token);
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
            string destination, string snapshotDestination, string snapshotIndexDestination, string schema, IProgress<VanillaGdoScanProgress> progress, CancellationToken token)
        {
            try { await previous; }
            catch { }
            if (token.IsCancellationRequested) return PlateUpCatalogImportResult.CancelledResult();
            byte[][] snapshotPayload;
            try
            {
                KitchenLibStudioBootstrap.RequireCurrentOutput(installation);
                snapshotPayload = await Task.Run(() =>
                {
                    SnapshotCatalogScanResult scan = new AssetsToolsSnapshotCatalogReader().Scan(installation.GameRoot, null, token);
                    token.ThrowIfCancellationRequested();
                    return new[] { VanillaAssetCatalogFile.Serialize(scan.Data),
                        ExternalGdoSnapshotIndexFile.Serialize(ExternalGdoSnapshotIndexFile.Build(scan.Data)) };
                }, CancellationToken.None);
            }
            catch (OperationCanceledException) { return PlateUpCatalogImportResult.CancelledResult(); }
            catch (Exception exception) { return PlateUpCatalogImportResult.Failure(exception.Message); }
            byte[] previousGdo = File.Exists(destination) ? File.ReadAllBytes(destination) : null;
            byte[] previousSnapshot = File.Exists(snapshotDestination) ? File.ReadAllBytes(snapshotDestination) : null;
            var service = new PlateUpCatalogImportService(new DefaultPlateUpCatalogImportPipeline(schema));
            PlateUpCatalogImportResult result = await service.ImportAsync(installation, destination, progress, token);
            if (!result.Succeeded) return result;
            try
            {
                token.ThrowIfCancellationRequested();
                VanillaAssetCatalogFile.PublishAtomic(snapshotDestination, snapshotPayload[0]);
                File.WriteAllBytes(snapshotIndexDestination, snapshotPayload[1]);
                return PlateUpCatalogImportResult.Success();
            }
            catch (OperationCanceledException)
            {
                Restore(destination, previousGdo); Restore(snapshotDestination, previousSnapshot);
                return PlateUpCatalogImportResult.CancelledResult();
            }
            catch (Exception exception)
            {
                Restore(destination, previousGdo); Restore(snapshotDestination, previousSnapshot);
                return PlateUpCatalogImportResult.Failure(exception.Message);
            }
        }

        private static void Restore(string path, byte[] previous)
        {
            if (previous == null) { if (File.Exists(path)) File.Delete(path); return; }
            Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path, previous);
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

            ImportGdoBatch(installation, CancellationToken.None);
            ImportSnapshotBatch(installation, CancellationToken.None);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }

        public static void ImportGdoBatch(PlateUpInstallation installation, CancellationToken token)
        {
            KitchenLibStudioBootstrap.RequireCurrentOutput(installation);
            var pipeline = new DefaultPlateUpCatalogImportPipeline(KitchenLibStudioGeneratedPaths.SchemaPath);
            VanillaGdoScanResult scan = pipeline.Scan(installation, null, token); byte[] content = pipeline.Build(installation, scan, token);
            pipeline.Publish(KitchenLibStudioGeneratedPaths.VanillaGdoCatalogPath, content, token);
        }

        public static void ImportSnapshotBatch(PlateUpInstallation installation, CancellationToken token)
        {
            KitchenLibStudioBootstrap.RequireCurrentOutput(installation);
            SnapshotCatalogScanResult scan = new AssetsToolsSnapshotCatalogReader().Scan(installation.GameRoot, null, token);
            VanillaAssetCatalogFile.PublishAtomic(KitchenLibStudioGeneratedPaths.VanillaSnapshotCatalogPath,
                VanillaAssetCatalogFile.Serialize(scan.Data));
            ExternalGdoSnapshotIndexFile.Publish(KitchenLibStudioGeneratedPaths.VanillaSnapshotIndexPath, scan.Data);
        }
    }
}
