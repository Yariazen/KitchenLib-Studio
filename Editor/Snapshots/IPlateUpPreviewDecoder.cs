using System;
using System.Threading;

namespace KitchenLib.Studio.Editor.Snapshots
{
    internal sealed class SnapshotProgress
    {
        public SnapshotProgress(string stage, int completed, int total) { Stage = stage; Completed = completed; Total = total; }
        public string Stage { get; }
        public int Completed { get; }
        public int Total { get; }
    }

    internal interface IPlateUpPreviewDecoder
    {
        VanillaPreviewModel Decode(string gameRoot, VanillaAssetRecord root,
            VanillaAssetCatalogData catalog, IProgress<SnapshotProgress> progress,
            CancellationToken cancellationToken);
        VanillaPreviewModel Decode(string gameRoot, VanillaObjectIdentity root,
            IProgress<SnapshotProgress> progress, CancellationToken cancellationToken);
    }
}
