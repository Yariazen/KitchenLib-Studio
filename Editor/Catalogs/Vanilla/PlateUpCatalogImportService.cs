using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using KitchenLib.Studio.Editor.Bootstrap;
using KitchenLib.Studio.Editor.Infrastructure;
using KitchenLib.Studio.Editor.Snapshots;

namespace KitchenLib.Studio.Editor.Catalogs.Vanilla
{
    internal interface IPlateUpCatalogImportPipeline
    {
        VanillaGdoScanResult Scan(PlateUpInstallation installation, IProgress<VanillaGdoScanProgress> progress, CancellationToken token);
        byte[] Build(PlateUpInstallation installation, VanillaGdoScanResult scan, CancellationToken token);
        void Publish(string destination, byte[] content, CancellationToken token);
    }

    internal sealed class PlateUpCatalogImportResult
    {
        private PlateUpCatalogImportResult(bool succeeded, bool cancelled, string error)
        {
            Succeeded = succeeded; Cancelled = cancelled; Error = error;
        }
        public bool Succeeded { get; }
        public bool Cancelled { get; }
        public string Error { get; }
        public static PlateUpCatalogImportResult Success() => new PlateUpCatalogImportResult(true, false, null);
        public static PlateUpCatalogImportResult CancelledResult() => new PlateUpCatalogImportResult(false, true, null);
        public static PlateUpCatalogImportResult Failure(string error) => new PlateUpCatalogImportResult(false, false, error);
    }

    internal sealed class PlateUpCatalogImportService
    {
        private readonly IPlateUpCatalogImportPipeline _pipeline;
        private int _active;

        public PlateUpCatalogImportService(IPlateUpCatalogImportPipeline pipeline)
        {
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        }

        public async Task<PlateUpCatalogImportResult> ImportAsync(PlateUpInstallation installation, string destination, IProgress<VanillaGdoScanProgress> progress, CancellationToken cancellationToken)
        {
            if (installation == null) throw new ArgumentNullException(nameof(installation));
            if (Interlocked.CompareExchange(ref _active, 1, 0) != 0)
                return PlateUpCatalogImportResult.Failure("A PlateUp catalog import is already running.");
            try
            {
                byte[] content = await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    VanillaGdoScanResult scan = _pipeline.Scan(installation, progress, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    return _pipeline.Build(installation, scan, cancellationToken);
                }, CancellationToken.None);
                cancellationToken.ThrowIfCancellationRequested();
                _pipeline.Publish(destination, content, cancellationToken);
                return PlateUpCatalogImportResult.Success();
            }
            catch (OperationCanceledException) { return PlateUpCatalogImportResult.CancelledResult(); }
            catch (Exception exception) { return PlateUpCatalogImportResult.Failure(exception.ToString()); }
            finally { Interlocked.Exchange(ref _active, 0); }
        }

        public async Task<PlateUpCatalogImportResult> ImportCombinedAsync(PlateUpInstallation installation,
            VanillaCatalogPublicationPaths paths, IProgress<VanillaGdoScanProgress> progress,
            CancellationToken cancellationToken)
        {
            if (installation == null) throw new ArgumentNullException(nameof(installation));
            if (paths == null) throw new ArgumentNullException(nameof(paths));
            if (Interlocked.CompareExchange(ref _active, 1, 0) != 0)
                return PlateUpCatalogImportResult.Failure("A PlateUp catalog import is already running.");
            try
            {
                var snapshotReader = new AssetsToolsSnapshotCatalogReader();
                object[] models = await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    VanillaCatalogImportProgress.Report(progress, VanillaCatalogImportPhase.Scan);
                    VanillaGdoScanResult gdoScan = _pipeline.Scan(installation, progress, cancellationToken);
                    byte[] gdoBytes = _pipeline.Build(installation, gdoScan, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    VanillaCatalogImportProgress.Report(progress, VanillaCatalogImportPhase.Decode);
                    SnapshotCatalogScanResult snapshotScan = snapshotReader.Scan(
                        installation.GameRoot, null, cancellationToken);
                    return new object[] { VanillaGdoCatalogFile.Deserialize(gdoBytes), snapshotScan.Data };
                }, CancellationToken.None);
                cancellationToken.ThrowIfCancellationRequested();
                var gdos = (VanillaGdoCatalogData)models[0];
                var snapshots = (VanillaAssetCatalogData)models[1];
                EnrichSnapshotRuntimeTypes(gdos, snapshots);
                var repository = new VanillaAssetCatalogRepository(snapshots);
                var renderer = new GameObjectSnapshotService(installation.GameRoot, snapshots, repository);
                var batch = new VanillaSnapshotBatchRenderer(ExternalGdoSnapshotSourceResolver.Resolve,
                    (session, key, token) => Task.FromResult(renderer.RenderVanillaBlocking(session, key,
                        new SnapshotRenderOptions(ExternalGdoSnapshotCacheIdentity.ThumbnailSize,
                            ExternalGdoSnapshotCacheIdentity.ThumbnailSize), token)));
                VanillaCatalogImportProgress.Report(progress, VanillaCatalogImportPhase.Render);
                var decoder = new AssetsToolsPlateUpPreviewDecoder();
                System.Collections.Generic.IReadOnlyList<VanillaSnapshotPng> pngs;
                using (IPlateUpPreviewDecodeSession session = decoder.OpenSession(installation.GameRoot, snapshots))
                    pngs = await batch.RenderAsync(snapshots, session, null, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                var generation = new VanillaCatalogGeneration(gdos, snapshots,
                    RenderedIndex(snapshots, pngs), pngs);
                VanillaCatalogImportProgress.Report(progress, VanillaCatalogImportPhase.Publish);
                VanillaCatalogPublication.Publish(generation, paths, cancellationToken);
                VanillaCatalogImportProgress.Report(progress, VanillaCatalogImportPhase.Publish, true);
                return PlateUpCatalogImportResult.Success();
            }
            catch (OperationCanceledException) { return PlateUpCatalogImportResult.CancelledResult(); }
            catch (Exception exception) { return PlateUpCatalogImportResult.Failure(exception.ToString()); }
            finally { Interlocked.Exchange(ref _active, 0); }
        }

        public PlateUpCatalogImportResult ImportCombinedBlocking(PlateUpInstallation installation,
            VanillaCatalogPublicationPaths paths, IProgress<VanillaGdoScanProgress> progress,
            CancellationToken cancellationToken)
        {
            if (installation == null) throw new ArgumentNullException(nameof(installation));
            if (paths == null) throw new ArgumentNullException(nameof(paths));
            if (Interlocked.CompareExchange(ref _active, 1, 0) != 0)
                return PlateUpCatalogImportResult.Failure("A PlateUp catalog import is already running.");
            try
            {
                var snapshotReader = new AssetsToolsSnapshotCatalogReader();
                object[] models = Task.Run(() =>
                {
                    VanillaCatalogImportProgress.Report(progress, VanillaCatalogImportPhase.Scan);
                    VanillaGdoScanResult gdoScan = _pipeline.Scan(installation, progress, cancellationToken);
                    byte[] gdoBytes = _pipeline.Build(installation, gdoScan, cancellationToken);
                    VanillaCatalogImportProgress.Report(progress, VanillaCatalogImportPhase.Decode);
                    SnapshotCatalogScanResult snapshotScan = snapshotReader.Scan(
                        installation.GameRoot, null, cancellationToken);
                    return new object[] { VanillaGdoCatalogFile.Deserialize(gdoBytes), snapshotScan.Data };
                }, cancellationToken).GetAwaiter().GetResult();
                var gdos = (VanillaGdoCatalogData)models[0];
                var snapshots = (VanillaAssetCatalogData)models[1];
                EnrichSnapshotRuntimeTypes(gdos, snapshots);
                var repository = new VanillaAssetCatalogRepository(snapshots);
                var renderer = new GameObjectSnapshotService(installation.GameRoot, snapshots, repository);
                var options = new SnapshotRenderOptions(ExternalGdoSnapshotCacheIdentity.ThumbnailSize,
                    ExternalGdoSnapshotCacheIdentity.ThumbnailSize);
                var batch = new VanillaSnapshotBatchRenderer(ExternalGdoSnapshotSourceResolver.Resolve,
                    (session, key, token) => Task.FromResult(renderer.RenderVanillaBlocking(session, key, options, token)));
                VanillaCatalogImportProgress.Report(progress, VanillaCatalogImportPhase.Render);
                var decoder = new AssetsToolsPlateUpPreviewDecoder();
                System.Collections.Generic.IReadOnlyList<VanillaSnapshotPng> pngs;
                using (IPlateUpPreviewDecodeSession session = decoder.OpenSession(installation.GameRoot, snapshots))
                    pngs = batch.RenderAsync(snapshots, session, null, cancellationToken).GetAwaiter().GetResult();
                VanillaCatalogImportProgress.Report(progress, VanillaCatalogImportPhase.Publish);
                VanillaCatalogPublication.Publish(new VanillaCatalogGeneration(gdos, snapshots,
                    RenderedIndex(snapshots, pngs), pngs), paths, cancellationToken);
                VanillaCatalogImportProgress.Report(progress, VanillaCatalogImportPhase.Publish, true);
                return PlateUpCatalogImportResult.Success();
            }
            catch (OperationCanceledException) { return PlateUpCatalogImportResult.CancelledResult(); }
            catch (Exception exception) { return PlateUpCatalogImportResult.Failure(exception.ToString()); }
            finally { Interlocked.Exchange(ref _active, 0); }
        }

        private static void EnrichSnapshotRuntimeTypes(VanillaGdoCatalogData gdos,
            VanillaAssetCatalogData snapshots)
        {
            var byId = (gdos.entries ?? Array.Empty<VanillaGdoCatalogEntryData>())
                .GroupBy(value => value.id).ToDictionary(group => group.Key, group => group.First());
            foreach (VanillaAssetRecordData record in snapshots.records ?? Array.Empty<VanillaAssetRecordData>())
                if (record != null && record.kind == "gdo" && byId.TryGetValue(record.gdoId, out VanillaGdoCatalogEntryData gdo))
                {
                    record.runtimeType = gdo.runtimeType;
                    record.assemblyName = gdo.assemblyName;
                }
        }

        private static ExternalGdoSnapshotIndexData RenderedIndex(VanillaAssetCatalogData snapshots,
            System.Collections.Generic.IReadOnlyList<VanillaSnapshotPng> pngs)
        {
            var renderedIds = new System.Collections.Generic.HashSet<int>(pngs.Where(value => value.IsRendered)
                .SelectMany(value => value.GdoIds));
            ExternalGdoSnapshotIndexData index = ExternalGdoSnapshotIndexFile.Build(snapshots);
            index.entries = (index.entries ?? Array.Empty<ExternalGdoSnapshotIndexEntry>())
                .Where(value => renderedIds.Contains(value.gdoId)).ToArray();
            return index;
        }
    }

    internal sealed class DefaultPlateUpCatalogImportPipeline : IPlateUpCatalogImportPipeline
    {
        private readonly IPlateUpAssetReader _reader;
        private readonly string _schemaPath;
        public DefaultPlateUpCatalogImportPipeline(string schemaPath) : this(new AssetsToolsPlateUpAssetReader(), schemaPath) { }
        internal DefaultPlateUpCatalogImportPipeline(IPlateUpAssetReader reader, string schemaPath) { _reader = reader; _schemaPath = schemaPath; }

        public VanillaGdoScanResult Scan(PlateUpInstallation installation, IProgress<VanillaGdoScanProgress> progress, CancellationToken token)
            => _reader.Scan(installation.GameRoot, progress, token);

        public byte[] Build(PlateUpInstallation installation, VanillaGdoScanResult scan, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            PlateUpSchemaFingerprint schema = PlateUpSchemaFingerprint.LoadAndValidate(_schemaPath, installation.GameRoot);
            return VanillaGdoCatalogFile.Serialize(VanillaGdoCatalogBuilder.Build(installation, scan, schema));
        }

        public void Publish(string destination, byte[] content, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            VanillaGdoCatalogFile.PublishAtomic(destination, content);
        }
    }
}
