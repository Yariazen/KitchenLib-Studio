using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class ImportedGdoSnapshotServiceTests
    {
        [Test]
        public void LoadsImportedPackagePngByProviderQualifiedIdentity()
        {
            string root = Path.Combine(Path.GetTempPath(), "imported-snapshot-" + Guid.NewGuid().ToString("N"));
            string provider = Path.Combine(root, "workshop_2913877103");
            Directory.CreateDirectory(Path.Combine(provider, "snapshots"));
            var source = new Texture2D(1, 1); source.SetPixel(0, 0, Color.red); source.Apply();
            File.WriteAllBytes(Path.Combine(provider, "snapshots", "42.png"), source.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(source);
            ModGdoCatalogData data = ModGdoRelationshipValueTests.CreateCatalog();
            data.entries[0].previewRelativePath = "snapshots/42.png";
            File.WriteAllBytes(Path.Combine(provider, "mod-gdo-catalog.json"), ModGdoCatalogFile.Serialize(data));
            var repository = new ExternalGdoCatalogRepository(
                new VanillaGdoCatalogRepository(Path.Combine(root, "missing-vanilla.json")), root);
            repository.Reload();
            ExternalGdoSnapshotResult result = null;
            try
            {
                using (var service = new ImportedGdoSnapshotService(null, repository))
                {
                    service.Request("workshop:2913877103", 42, "KitchenData.Item", value => result = value);
                    Assert.That(result.State, Is.EqualTo(ExternalGdoSnapshotState.Ready));
                    Assert.That(result.Texture, Is.Not.Null);
                }
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        [Test]
        public void RepeatedRequestsReuseTheDecodedImportedTexture()
        {
            string root = Path.Combine(Path.GetTempPath(), "imported-snapshot-cache-" + Guid.NewGuid().ToString("N"));
            string provider = Path.Combine(root, "workshop_2913877103");
            Directory.CreateDirectory(Path.Combine(provider, "snapshots"));
            var source = new Texture2D(1, 1); source.SetPixel(0, 0, Color.red); source.Apply();
            File.WriteAllBytes(Path.Combine(provider, "snapshots", "42.png"), source.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(source);
            ModGdoCatalogData data = ModGdoRelationshipValueTests.CreateCatalog();
            data.entries[0].previewRelativePath = "snapshots/42.png";
            File.WriteAllBytes(Path.Combine(provider, "mod-gdo-catalog.json"), ModGdoCatalogFile.Serialize(data));
            var repository = new ExternalGdoCatalogRepository(
                new VanillaGdoCatalogRepository(Path.Combine(root, "missing-vanilla.json")), root);
            repository.Reload();
            try
            {
                using (var service = new ImportedGdoSnapshotService(null, repository))
                {
                    ExternalGdoSnapshotResult first = null;
                    ExternalGdoSnapshotResult second = null;
                    service.Request("workshop:2913877103", 42, "KitchenData.Item", value => first = value);
                    service.Request("workshop:2913877103", 42, "KitchenData.Item", value => second = value);

                    Assert.That(second.Texture, Is.SameAs(first.Texture));
                    Assert.That(first.Texture.hideFlags, Is.EqualTo(HideFlags.HideAndDontSave));
                }
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        [Test]
        public void MalformedSnapshotIndexReturnsDiagnosticWithoutStartingFallback()
        {
            string path = Path.Combine(Path.GetTempPath(), "snapshot-index-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(path, "broken");
            bool fallbackStarted = false;
            try
            {
                MethodInfo method = typeof(GdoBundleGraphView).GetMethod("CreateIndexedSnapshotService",
                    BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(method, Is.Not.Null);
                var service = (IExternalGdoSnapshotService)method.Invoke(null, new object[]
                {
                    path,
                    new Func<Task<IExternalGdoSnapshotService>>(() =>
                    {
                        fallbackStarted = true;
                        return Task.FromResult<IExternalGdoSnapshotService>(null);
                    })
                });
                ExternalGdoSnapshotResult result = null;
                service.Request(ExternalGdoCatalogRepository.VanillaProviderKey, 42, "KitchenData.Item",
                    value => result = value);

                Assert.That(result.State, Is.EqualTo(ExternalGdoSnapshotState.Unavailable));
                Assert.That(result.Diagnostic, Does.Contain("index"));
                Assert.That(fallbackStarted, Is.False);
                service.Dispose();
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        [Test]
        public void VanillaOnlyServiceRejectsImportedProviderWithoutStartingFallback()
        {
            bool fallbackStarted = false;
            var service = new IndexedExternalGdoSnapshotService(
                new ExternalGdoSnapshotIndexData(), new SnapshotCache(),
                () =>
                {
                    fallbackStarted = true;
                    return Task.FromResult<IExternalGdoSnapshotService>(null);
                });
            ExternalGdoSnapshotResult result = null;

            service.Request("workshop:2913877103", 42, "KitchenData.Item", value => result = value);

            Assert.That(result.State, Is.EqualTo(ExternalGdoSnapshotState.Unavailable));
            Assert.That(result.Diagnostic, Does.Contain("provider"));
            Assert.That(fallbackStarted, Is.False);
            service.Dispose();
        }

        [Test]
        public void UnsupportedTypeDoesNotForwardToVanillaService()
        {
            var vanilla = new RecordingSnapshotService();
            var repository = new ExternalGdoCatalogRepository(
                new VanillaGdoCatalogRepository(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json")),
                Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
            ExternalGdoSnapshotResult result = null;

            using (var service = new ImportedGdoSnapshotService(vanilla, repository))
                service.Request(ExternalGdoCatalogRepository.VanillaProviderKey, 42,
                    "KitchenData.Process", value => result = value);

            Assert.That(result.State, Is.EqualTo(ExternalGdoSnapshotState.Unsupported));
            Assert.That(result.Diagnostic, Is.EqualTo("No snapshot for this GDO type."));
            Assert.That(vanilla.RequestCount, Is.Zero);
        }

        [Test]
        public void IndexedFallbackPreservesEligibleRuntimeType()
        {
            var fallback = new RecordingSnapshotService();
            var service = new IndexedExternalGdoSnapshotService(
                new ExternalGdoSnapshotIndexData(), new SnapshotCache(),
                () => Task.FromResult<IExternalGdoSnapshotService>(fallback));

            service.Request(ExternalGdoCatalogRepository.VanillaProviderKey, 42,
                "KitchenData.ItemGroup", _ => { });

            Assert.That(fallback.RequestCount, Is.EqualTo(1));
            Assert.That(fallback.LastRuntimeType, Is.EqualTo("KitchenData.ItemGroup"));
            service.Dispose();
        }

        [Test]
        public void IndexedServiceRejectsUnsupportedTypeBeforeCacheOrFallback()
        {
            string root = Path.Combine(Path.GetTempPath(), "unsupported-index-" + Guid.NewGuid().ToString("N"));
            var cache = new SnapshotCache(root);
            const string gameObjectKey = "plateup/game-object/test";
            const string fingerprint = "source";
            string key = SnapshotCache.BuildKey(ExternalGdoSnapshotCacheIdentity.VanillaSubject(gameObjectKey),
                fingerprint, ExternalGdoSnapshotCacheIdentity.RendererVersion,
                new SnapshotRenderOptions(ExternalGdoSnapshotCacheIdentity.ThumbnailSize,
                    ExternalGdoSnapshotCacheIdentity.ThumbnailSize),
                ExternalGdoSnapshotCacheIdentity.SettingsFingerprint);
            var source = new Texture2D(1, 1);
            source.SetPixel(0, 0, Color.red);
            source.Apply();
            cache.Put(key, source.EncodeToPNG(), new SnapshotCacheMetadata());
            UnityEngine.Object.DestroyImmediate(source);
            bool fallbackStarted = false;
            ExternalGdoSnapshotResult result = null;
            try
            {
                using (var service = new IndexedExternalGdoSnapshotService(
                    new ExternalGdoSnapshotIndexData
                    {
                        entries = new[] { new ExternalGdoSnapshotIndexEntry
                        {
                            gdoId = 42,
                            gameObjectKey = gameObjectKey,
                            sourceFingerprint = fingerprint
                        } }
                    }, cache, () =>
                    {
                        fallbackStarted = true;
                        return Task.FromResult<IExternalGdoSnapshotService>(new RecordingSnapshotService());
                    }))
                    service.Request(ExternalGdoCatalogRepository.VanillaProviderKey, 42,
                        "KitchenData.Process", value => result = value);

                Assert.That(result.State, Is.EqualTo(ExternalGdoSnapshotState.Unsupported));
                Assert.That(result.Texture, Is.Null);
                Assert.That(fallbackStarted, Is.False);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        private sealed class RecordingSnapshotService : IExternalGdoSnapshotService
        {
            public int RequestCount;
            public string LastRuntimeType;

            public IDisposable Request(string providerKey, int gdoId, string runtimeType,
                Action<ExternalGdoSnapshotResult> completed)
            {
                RequestCount++;
                LastRuntimeType = runtimeType;
                return NoopDisposable.Instance;
            }

            public void Dispose() { }
        }

        private sealed class NoopDisposable : IDisposable
        {
            public static readonly NoopDisposable Instance = new NoopDisposable();
            public void Dispose() { }
        }
    }
}
