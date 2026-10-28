using System;
using System.Collections.Generic;

using KitchenLib.Studio.Editor.Snapshots;

namespace KitchenLib.Studio.Editor.Catalogs.Vanilla
{
    internal enum VanillaCatalogImportPhase { Scan, Decode, Render, Publish }

    internal static class VanillaCatalogImportProgress
    {
        private const int PhaseCount = 4;

        public static void Report(IProgress<VanillaGdoScanProgress> progress,
            VanillaCatalogImportPhase phase, bool complete = false)
        {
            progress?.Report(new VanillaGdoScanProgress(
                phase.ToString(), complete ? PhaseCount : (int)phase, PhaseCount));
        }
    }

    internal sealed class VanillaSnapshotBatchProgress
    {
        public VanillaSnapshotBatchProgress(string gameObjectKey, int completed, int total)
        { GameObjectKey = gameObjectKey; Completed = completed; Total = total; }
        public string GameObjectKey { get; }
        public int Completed { get; }
        public int Total { get; }
    }

    internal sealed class VanillaSnapshotPng
    {
        public VanillaSnapshotPng(string cacheKey, string gameObjectKey, IReadOnlyList<int> gdoIds,
            byte[] pngBytes, SnapshotCacheMetadata metadata, string diagnostic = null)
        {
            CacheKey = cacheKey;
            GameObjectKey = gameObjectKey;
            GdoIds = gdoIds ?? Array.Empty<int>();
            PngBytes = pngBytes;
            Metadata = metadata;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public string CacheKey { get; }
        public string GameObjectKey { get; }
        public IReadOnlyList<int> GdoIds { get; }
        public byte[] PngBytes { get; }
        public SnapshotCacheMetadata Metadata { get; }
        public string Diagnostic { get; }
        public bool IsRendered => !string.IsNullOrEmpty(CacheKey) && PngBytes != null;
    }

    internal sealed class VanillaCatalogGeneration
    {
        public VanillaCatalogGeneration(VanillaGdoCatalogData gdos, VanillaAssetCatalogData snapshots,
            ExternalGdoSnapshotIndexData index, IReadOnlyList<VanillaSnapshotPng> pngs)
        {
            Gdos = gdos ?? throw new ArgumentNullException(nameof(gdos));
            Snapshots = snapshots ?? throw new ArgumentNullException(nameof(snapshots));
            Index = index ?? throw new ArgumentNullException(nameof(index));
            Pngs = pngs ?? throw new ArgumentNullException(nameof(pngs));
        }

        public VanillaGdoCatalogData Gdos { get; }
        public VanillaAssetCatalogData Snapshots { get; }
        public ExternalGdoSnapshotIndexData Index { get; }
        public IReadOnlyList<VanillaSnapshotPng> Pngs { get; }
    }
}
