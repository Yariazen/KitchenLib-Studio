using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class ExternalGdoSnapshotServiceTests
    {
        private readonly List<Texture2D> _textures = new List<Texture2D>();

        [TearDown]
        public void TearDown()
        {
            foreach (Texture2D texture in _textures)
                if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
            _textures.Clear();
        }

        [Test]
        public void CacheHitSkipsRendererWithoutInstallation()
        {
            int renders = 0;
            Texture2D expected = Texture();
            var dependencies = Dependencies(
                _ => new SnapshotCacheEntry("cached.png", Png(1), new SnapshotCacheMetadata()),
                (_, __) => { renders++; return Task.FromResult(RenderResult()); },
                _ => expected,
                false);
            var results = new List<ExternalGdoSnapshotResult>();
            using (var service = new ExternalGdoSnapshotService(Catalog((42, "plateup/game-object/shared")), dependencies))
                service.Request(ExternalGdoCatalogRepository.VanillaProviderKey, 42, "KitchenData.Item", results.Add);

            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].State, Is.EqualTo(ExternalGdoSnapshotState.Ready));
            Assert.That(results[0].Texture, Is.SameAs(expected));
            Assert.That(renders, Is.Zero);
        }

        [Test]
        public void MaterialFallbackRendererInvalidatesVersionOneCacheEntries()
        {
            string current = ExternalGdoSnapshotCacheIdentity.BuildKey("plateup/game-object/flour", "source");
            string versionOne = SnapshotCache.BuildKey(
                ExternalGdoSnapshotCacheIdentity.VanillaSubject("plateup/game-object/flour"), "source", "1",
                new SnapshotRenderOptions(ExternalGdoSnapshotCacheIdentity.ThumbnailSize,
                    ExternalGdoSnapshotCacheIdentity.ThumbnailSize),
                ExternalGdoSnapshotCacheIdentity.SettingsFingerprint);

            Assert.That(current, Is.Not.EqualTo(versionOne));
        }

        [UnityTest]
        public IEnumerator DuplicateGameObjectRequestsShareOneRender()
        {
            int renders = 0;
            var completion = new TaskCompletionSource<SnapshotRenderResult>();
            var dependencies = Dependencies(_ => null, (_, __) =>
            {
                renders++;
                return completion.Task;
            }, _ => Texture(), true);
            var first = new List<ExternalGdoSnapshotResult>();
            var second = new List<ExternalGdoSnapshotResult>();
            using (var service = new ExternalGdoSnapshotService(
                Catalog((42, "plateup/game-object/shared"), (43, "plateup/game-object/shared")), dependencies))
            {
                service.Request(ExternalGdoCatalogRepository.VanillaProviderKey, 42, "KitchenData.Item", first.Add);
                service.Request(ExternalGdoCatalogRepository.VanillaProviderKey, 43, "KitchenData.Item", second.Add);
                Assert.That(first[0].State, Is.EqualTo(ExternalGdoSnapshotState.Loading));
                Assert.That(second[0].State, Is.EqualTo(ExternalGdoSnapshotState.Loading));
                Assert.That(renders, Is.EqualTo(1));

                completion.SetResult(RenderResult());
                yield return Await(() => first.Count > 1 && second.Count > 1 &&
                    first[first.Count - 1].State == ExternalGdoSnapshotState.Ready &&
                    second[second.Count - 1].State == ExternalGdoSnapshotState.Ready);
                Assert.That(first[first.Count - 1].Texture, Is.SameAs(second[second.Count - 1].Texture));
            }
        }

        [UnityTest]
        public IEnumerator QueueRunsOnlyOneRenderAtATime()
        {
            int active = 0;
            int maximum = 0;
            var completions = new Queue<TaskCompletionSource<SnapshotRenderResult>>();
            var dependencies = Dependencies(_ => null, (_, __) =>
            {
                active++;
                maximum = Math.Max(maximum, active);
                var completion = new TaskCompletionSource<SnapshotRenderResult>();
                completions.Enqueue(completion);
                return completion.Task.ContinueWith(task => { active--; return task.Result; });
            }, _ => Texture(), true);
            using (var service = new ExternalGdoSnapshotService(
                Catalog((42, "plateup/game-object/one"), (43, "plateup/game-object/two")), dependencies))
            {
                service.Request(ExternalGdoCatalogRepository.VanillaProviderKey, 42, "KitchenData.Item", _ => { });
                service.Request(ExternalGdoCatalogRepository.VanillaProviderKey, 43, "KitchenData.Item", _ => { });
                Assert.That(completions.Count, Is.EqualTo(1));
                completions.Dequeue().SetResult(RenderResult());
                yield return Await(() => completions.Count == 1);
                completions.Dequeue().SetResult(RenderResult());
                yield return Await(() => active == 0);
            }
            Assert.That(maximum, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator WorkerFaultPublishesFailureThenStartsNextRequest()
        {
            int renders = 0;
            var first = new List<ExternalGdoSnapshotResult>();
            var second = new List<ExternalGdoSnapshotResult>();
            var dependencies = Dependencies(_ => null, (_, __) =>
            {
                renders++;
                return renders == 1
                    ? Task.FromException<SnapshotRenderResult>(new InvalidOperationException("decode failed"))
                    : Task.FromResult(RenderResult());
            }, _ => Texture(), true);

            using (var service = new ExternalGdoSnapshotService(
                Catalog((42, "plateup/game-object/one"), (43, "plateup/game-object/two")), dependencies))
            {
                service.Request(ExternalGdoCatalogRepository.VanillaProviderKey, 42, "KitchenData.Item", first.Add);
                service.Request(ExternalGdoCatalogRepository.VanillaProviderKey, 43, "KitchenData.Item", second.Add);
                yield return Await(() => first.Count > 1 && second.Count > 1);
            }

            Assert.That(first[first.Count - 1].State, Is.EqualTo(ExternalGdoSnapshotState.Failed));
            Assert.That(first[first.Count - 1].Diagnostic, Does.Contain("decode failed"));
            Assert.That(second[second.Count - 1].State, Is.EqualTo(ExternalGdoSnapshotState.Ready));
            Assert.That(renders, Is.EqualTo(2));
        }

        [Test]
        public void MissingInstallationReturnsUnavailableWithoutRendering()
        {
            int renders = 0;
            var results = new List<ExternalGdoSnapshotResult>();
            var dependencies = Dependencies(_ => null, (_, __) =>
            {
                renders++;
                return Task.FromResult(RenderResult());
            }, _ => Texture(), false);
            using (var service = new ExternalGdoSnapshotService(Catalog((42, "plateup/game-object/shared")), dependencies))
                service.Request(ExternalGdoCatalogRepository.VanillaProviderKey, 42, "KitchenData.Item", results.Add);

            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].State, Is.EqualTo(ExternalGdoSnapshotState.Unavailable));
            Assert.That(results[0].Diagnostic, Is.Not.Empty);
            Assert.That(renders, Is.Zero);
        }

        [Test]
        public void InvalidCachedImageFallsBackToGeneration()
        {
            int renders = 0;
            var results = new List<ExternalGdoSnapshotResult>();
            var dependencies = Dependencies(
                _ => new SnapshotCacheEntry("cached.png", Png(1), new SnapshotCacheMetadata()),
                (_, __) =>
                {
                    renders++;
                    return new TaskCompletionSource<SnapshotRenderResult>().Task;
                },
                _ => null,
                true);
            using (var service = new ExternalGdoSnapshotService(Catalog((42, "plateup/game-object/shared")), dependencies))
                service.Request(ExternalGdoCatalogRepository.VanillaProviderKey, 42, "KitchenData.Item", results.Add);

            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].State, Is.EqualTo(ExternalGdoSnapshotState.Loading));
            Assert.That(renders, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DisposedServiceIgnoresLateCompletion()
        {
            var completion = new TaskCompletionSource<SnapshotRenderResult>();
            var results = new List<ExternalGdoSnapshotResult>();
            var service = new ExternalGdoSnapshotService(Catalog((42, "plateup/game-object/shared")),
                Dependencies(_ => null, (_, __) => completion.Task, _ => Texture(), true));
            service.Request(ExternalGdoCatalogRepository.VanillaProviderKey, 42, "KitchenData.Item", results.Add);
            service.Dispose();

            completion.SetResult(RenderResult());
            yield return null;
            yield return null;

            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].State, Is.EqualTo(ExternalGdoSnapshotState.Loading));
        }

        [UnityTest]
        public IEnumerator DisposeDuringWorkerDecodeSuppressesCacheAndCallback()
        {
            int cacheWrites = 0;
            var completion = new TaskCompletionSource<SnapshotRenderResult>();
            var results = new List<ExternalGdoSnapshotResult>();
            var service = new ExternalGdoSnapshotService(Catalog((42, "plateup/game-object/shared")),
                Dependencies(_ => null, (_, __) => completion.Task, _ => Texture(), true,
                    (_, __, ___) => cacheWrites++));
            service.Request(ExternalGdoCatalogRepository.VanillaProviderKey, 42, "KitchenData.Item", results.Add);
            service.Dispose();

            completion.SetResult(RenderResult());
            yield return null;
            yield return null;

            Assert.That(cacheWrites, Is.Zero);
            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].State, Is.EqualTo(ExternalGdoSnapshotState.Loading));
        }

        [UnityTest]
        public IEnumerator InstalledFlourAndWaterRenderThroughExternalSnapshotPipeline()
        {
            string requestedRoot = Environment.GetEnvironmentVariable("KITCHENLIB_STUDIO_PLATEUP_ROOT");
            if (string.IsNullOrWhiteSpace(requestedRoot))
                Assert.Ignore("Set KITCHENLIB_STUDIO_PLATEUP_ROOT to run the installed external snapshot oracle.");
            PlateUpInstallationValidation validation =
                new PlateUpInstallationDiscovery().ValidateManualRoot(requestedRoot);
            Assert.That(validation.IsValid, Is.True, string.Join(Environment.NewLine, validation.Diagnostics));
            PlateUpInstallation installation = validation.Installation;
            string catalogPath = Path.Combine(Application.dataPath, "KitchenLibStudio", "Generated",
                "vanilla-snapshot-catalog.json");
            VanillaAssetCatalogData catalog = VanillaAssetCatalogFile.Read(catalogPath);
            var repository = new VanillaAssetCatalogRepository(catalogPath);
            Assert.That(repository.Reload(), Is.True);
            var renderer = new GameObjectSnapshotService(installation.GameRoot, catalog, repository);
            var options = new SnapshotRenderOptions(128, 128);

            foreach (int gdoId in new[] { 1378842682, 1657174953 })
            {
                ExternalGdoSnapshotSourceResolution source =
                    ExternalGdoSnapshotSourceResolver.Resolve(catalog, gdoId);
                Assert.That(source.Succeeded, Is.True, source.Diagnostic);
                Task<SnapshotRenderResult> render = renderer.RenderAsync(
                    SnapshotSubject.VanillaGameObjectKey(source.GameObjectKey), options,
                    CancellationToken.None);
                yield return Await(() => render.IsCompleted, 4000);
                Assert.That(render.IsFaulted, Is.False, render.Exception?.ToString());
                SnapshotRenderResult result = render.Result;
                Assert.That(result.PngBytes, Is.Not.Empty, "GDO " + gdoId + " did not render a PNG.");
            }
        }

        [Test]
        public void UnsupportedTypeReturnsBeforeSnapshotDependenciesRun()
        {
            int cacheReads = 0;
            int capabilityChecks = 0;
            int renders = 0;
            ExternalGdoSnapshotResult result = null;
            ExternalGdoSnapshotServiceDependencies dependencies = Dependencies(
                _ => { cacheReads++; return null; },
                (_, __) => { renders++; return Task.FromResult(RenderResult()); },
                _ => Texture(), true);
            dependencies.CanRender = () => { capabilityChecks++; return true; };

            using (var service = new ExternalGdoSnapshotService(
                Catalog((42, "plateup/game-object/shared")), dependencies))
                service.Request(ExternalGdoCatalogRepository.VanillaProviderKey, 42,
                    "KitchenData.Process", value => result = value);

            Assert.That(result.State, Is.EqualTo(ExternalGdoSnapshotState.Unsupported));
            Assert.That(result.Diagnostic, Is.EqualTo("No snapshot for this GDO type."));
            Assert.That(cacheReads, Is.Zero);
            Assert.That(capabilityChecks, Is.Zero);
            Assert.That(renders, Is.Zero);
        }

        private ExternalGdoSnapshotServiceDependencies Dependencies(
            Func<string, SnapshotCacheEntry> cacheGet,
            Func<string, CancellationToken, Task<SnapshotRenderResult>> render,
            Func<byte[], Texture2D> load,
            bool canRender,
            Action<string, byte[], SnapshotCacheMetadata> cachePut = null)
        {
            return new ExternalGdoSnapshotServiceDependencies
            {
                CacheGet = cacheGet,
                CachePut = cachePut ?? ((_, __, ___) => { }),
                Render = render,
                LoadTexture = load,
                DestroyTexture = texture =>
                {
                    _textures.Remove(texture);
                    if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
                },
                CanRender = () => canRender
            };
        }

        private Texture2D Texture()
        {
            var texture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            _textures.Add(texture);
            return texture;
        }

        private static SnapshotRenderResult RenderResult() =>
            new SnapshotRenderResult(Png(2), 128, 128, 1, new Bounds(Vector3.zero, Vector3.one));

        private static byte[] Png(byte marker) => new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, marker };

        private static IEnumerator Await(Func<bool> condition, int maximumFrames = 600)
        {
            for (int index = 0; index < maximumFrames && !condition(); index++) yield return null;
            Assert.That(condition(), Is.True, "Timed out waiting for the snapshot service.");
        }

        private static VanillaAssetCatalogData Catalog(params (int id, string target)[] values)
        {
            var records = new List<VanillaAssetRecordData>();
            var edges = new List<VanillaAssetEdgeData>();
            foreach ((int id, string target) value in values)
            {
                string gdoKey = "plateup/gdo/" + value.id;
                records.Add(new VanillaAssetRecordData { key = gdoKey, displayName = gdoKey, kind = "gdo",
                    gdoId = value.id, source = new VanillaObjectIdentityData() });
                if (!records.Exists(record => record.key == value.target))
                    records.Add(new VanillaAssetRecordData { key = value.target, displayName = value.target,
                        kind = "gameObject", source = new VanillaObjectIdentityData() });
                edges.Add(new VanillaAssetEdgeData { sourceKey = gdoKey, relation = "gameObject",
                    slot = "IconPrefab", targetKey = value.target });
            }
            return new VanillaAssetCatalogData { sourceFingerprint = "fixture", records = records.ToArray(), edges = edges.ToArray() };
        }
    }
}
