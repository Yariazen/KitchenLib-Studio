using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace KitchenLib.Studio.Editor
{
    public interface IPlateUpCatalogImportPipeline
    {
        VanillaGdoScanResult Scan(PlateUpInstallation installation, IProgress<VanillaGdoScanProgress> progress, CancellationToken token);
        byte[] Build(PlateUpInstallation installation, VanillaGdoScanResult scan, CancellationToken token);
        void Publish(string destination, byte[] content, CancellationToken token);
    }

    public sealed class PlateUpCatalogImportResult
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

    public sealed class PlateUpCatalogImportService
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
            catch (Exception exception) { return PlateUpCatalogImportResult.Failure(exception.Message); }
            finally { Interlocked.Exchange(ref _active, 0); }
        }
    }

    public sealed class DefaultPlateUpCatalogImportPipeline : IPlateUpCatalogImportPipeline
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
