using System;
using System.Globalization;
using System.IO;
using NUnit.Framework;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class SnapshotCacheTests
    {
        [Test]
        public void CacheKeyIncludesEveryRenderingInput()
        {
            string baseline = SnapshotCache.BuildKey("local:abc", "source-a", "renderer-1", new SnapshotRenderOptions(128, 128), "studio-a");
            Assert.That(SnapshotCache.BuildKey("local:abc", "source-b", "renderer-1", new SnapshotRenderOptions(128, 128), "studio-a"), Is.Not.EqualTo(baseline));
            Assert.That(SnapshotCache.BuildKey("local:abc", "source-a", "renderer-2", new SnapshotRenderOptions(128, 128), "studio-a"), Is.Not.EqualTo(baseline));
            Assert.That(SnapshotCache.BuildKey("local:abc", "source-a", "renderer-1", new SnapshotRenderOptions(256, 128), "studio-a"), Is.Not.EqualTo(baseline));
            Assert.That(SnapshotCache.BuildKey("local:abc", "source-a", "renderer-1", new SnapshotRenderOptions(128, 128), "studio-b"), Is.Not.EqualTo(baseline));
        }

        [Test]
        public void VanillaCacheKeyIgnoresUnrelatedCatalogFingerprintChanges()
        {
            VanillaAssetCatalogData first = Catalog("aggregate-a", "source-a", 42);
            VanillaAssetCatalogData refreshed = Catalog("aggregate-b", "source-a", 42);
            var options = new SnapshotRenderOptions(128, 128);

            string baseline = ExternalGdoSnapshotCacheIdentity.BuildKey(first, GameObjectKey, options);

            Assert.That(ExternalGdoSnapshotCacheIdentity.BuildKey(refreshed, GameObjectKey, options),
                Is.EqualTo(baseline));
            Assert.That(ExternalGdoSnapshotCacheIdentity.BuildKey(
                Catalog("aggregate-b", "source-b", 42), GameObjectKey, options), Is.Not.EqualTo(baseline));
            Assert.That(ExternalGdoSnapshotCacheIdentity.BuildKey(
                Catalog("aggregate-b", "source-a", 43), GameObjectKey, options), Is.Not.EqualTo(baseline));
        }

        [Test]
        public void VanillaCacheKeyIsCultureInvariant()
        {
            CultureInfo original = CultureInfo.CurrentCulture;
            var custom = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            custom.NumberFormat.NegativeSign = "~";
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                string invariant = ExternalGdoSnapshotCacheIdentity.BuildKey(
                    Catalog("aggregate", "source", -42), GameObjectKey, new SnapshotRenderOptions(128, 128));
                CultureInfo.CurrentCulture = custom;
                Assert.That(ExternalGdoSnapshotCacheIdentity.BuildKey(
                    Catalog("aggregate", "source", -42), GameObjectKey, new SnapshotRenderOptions(128, 128)),
                    Is.EqualTo(invariant));
            }
            finally { CultureInfo.CurrentCulture = original; }
        }

        [Test]
        public void AtomicFailurePreservesPreviousPngAndSidecarStoresDiagnostics()
        {
            string root = Path.Combine(Path.GetTempPath(), "snapshot-cache-" + Guid.NewGuid().ToString("N"));
            var cache = new SnapshotCache(root); string key = new string('a', 64);
            try
            {
                cache.Put(key, Png(1), new SnapshotCacheMetadata { width = 1, height = 1,
                    diagnostics = new[] { "shader fallback", "static skinned fallback" } });
                byte[] previous = File.ReadAllBytes(cache.PngPath(key));
                Assert.Throws<IOException>(() => cache.Put(key, Png(2), new SnapshotCacheMetadata(), (_, __) => throw new IOException("injected")));
                Assert.That(File.ReadAllBytes(cache.PngPath(key)), Is.EqualTo(previous));
                SnapshotCacheEntry entry = cache.TryGet(key);
                Assert.That(entry.Metadata.diagnostics, Has.Some.Contains("shader").And.Some.Contains("skinned"));
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        [Test]
        public void CorruptPngMissesAndCleanupIsBounded()
        {
            string root = Path.Combine(Path.GetTempPath(), "snapshot-cache-" + Guid.NewGuid().ToString("N"));
            var cache = new SnapshotCache(root);
            try
            {
                for (int index = 0; index < 4; index++) cache.Put(index.ToString("x64"), Png((byte)index), new SnapshotCacheMetadata());
                File.WriteAllBytes(cache.PngPath(0.ToString("x64")), new byte[] { 1, 2, 3 });
                Assert.That(cache.TryGet(0.ToString("x64")), Is.Null);
                Assert.That(cache.Cleanup(2), Is.EqualTo(2));
                Assert.That(Directory.GetFiles(root, "*.png").Length, Is.LessThanOrEqualTo(2));
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        private static byte[] Png(byte marker) => new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, marker };

        private const string GameObjectKey = "plateup/game-object/fixture";

        private static VanillaAssetCatalogData Catalog(string aggregate, string sourceHash, long pathId) =>
            new VanillaAssetCatalogData
            {
                sourceFingerprint = aggregate,
                sources = new[]
                {
                    new VanillaAssetSourceData { relativePath = "sharedassets1.assets", sha256 = sourceHash }
                },
                records = new[]
                {
                    new VanillaAssetRecordData
                    {
                        key = GameObjectKey,
                        source = new VanillaObjectIdentityData
                        {
                            relativePath = "sharedassets1.assets",
                            pathId = pathId
                        }
                    }
                }
            };
    }
}
