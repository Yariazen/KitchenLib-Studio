using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace KitchenLib.Studio.Editor
{
    public interface IPlateUpSnapshotCatalogReader
    {
        SnapshotCatalogScanResult Scan(string gameRoot, IProgress<SnapshotProgress> progress, CancellationToken cancellationToken);
    }
    public sealed class SnapshotCatalogScanResult
    {
        public SnapshotCatalogScanResult(VanillaAssetCatalogData data) { Data = data; }
        public VanillaAssetCatalogData Data { get; }
        public bool HasFatalDiagnostics => (Data.diagnostics ?? Array.Empty<VanillaAssetDiagnosticData>()).Any(item => item.severity == "fatal");
    }
}
