using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class BackgroundVanillaSnapshotTests
    {
        private readonly List<string> _paths = new List<string>();

        [TearDown]
        public void TearDown()
        {
            foreach (string path in _paths)
                if (File.Exists(path)) File.Delete(path);
            _paths.Clear();
        }

        [UnityTest]
        public IEnumerator VanillaDecodeRunsOnWorkerAndRenderReturnsToEditorThread()
        {
            int editorThread = Thread.CurrentThread.ManagedThreadId;
            var decoder = new FakeDecoder();
            int renderThread = -1;
            GameObjectSnapshotService service = Service(decoder, (_, __) =>
            {
                renderThread = Thread.CurrentThread.ManagedThreadId;
                return Result();
            });

            var task = service.RenderAsync(SnapshotSubject.VanillaGameObjectKey(Key), Options(),
                CancellationToken.None);
            yield return Await(() => task.IsCompleted);

            Assert.That(task.IsFaulted, Is.False, task.Exception?.ToString());
            Assert.That(decoder.ThreadId, Is.Not.EqualTo(editorThread));
            Assert.That(renderThread, Is.EqualTo(editorThread));
        }

        [UnityTest]
        public IEnumerator VanillaRequestReturnsBeforeBlockedDecodeCompletes()
        {
            var decoder = new FakeDecoder { Gate = new ManualResetEventSlim(false) };
            GameObjectSnapshotService service = Service(decoder, (_, __) => Result());

            var task = service.RenderAsync(SnapshotSubject.VanillaGameObjectKey(Key), Options(),
                CancellationToken.None);
            yield return Await(() => decoder.Started);
            Assert.That(task.IsCompleted, Is.False);

            decoder.Gate.Set();
            yield return Await(() => task.IsCompleted);
            Assert.That(task.IsFaulted, Is.False, task.Exception?.ToString());
        }

        [UnityTest]
        public IEnumerator SynchronousDecoderStillRendersOnEditorThread()
        {
            int editorThread = Thread.CurrentThread.ManagedThreadId;
            int renderThread = -1;
            GameObjectSnapshotService service = Service(new FakeDecoder(), (_, __) =>
            {
                renderThread = Thread.CurrentThread.ManagedThreadId;
                return Result();
            });

            var task = service.RenderAsync(SnapshotSubject.VanillaGameObjectKey(Key), Options(),
                CancellationToken.None);
            yield return Await(() => task.IsCompleted);
            Assert.That(renderThread, Is.EqualTo(editorThread));
        }

        [UnityTest]
        public IEnumerator CancellationBeforeWorkerCompletionSkipsReconstruction()
        {
            var decoder = new FakeDecoder { Gate = new ManualResetEventSlim(false) };
            bool rendered = false;
            GameObjectSnapshotService service = Service(decoder, (_, __) =>
            {
                rendered = true;
                return Result();
            });
            var cancellation = new CancellationTokenSource();

            var task = service.RenderAsync(SnapshotSubject.VanillaGameObjectKey(Key), Options(),
                cancellation.Token);
            yield return Await(() => decoder.Started);
            cancellation.Cancel();
            decoder.Gate.Set();
            yield return Await(() => task.IsCompleted);

            Assert.That(task.IsCanceled, Is.True);
            Assert.That(rendered, Is.False);
        }

        [Test]
        public void MissingEditorSynchronizationContextRejectsVanillaRequest()
        {
            SynchronizationContext original = SynchronizationContext.Current;
            try
            {
                SynchronizationContext.SetSynchronizationContext(null);
                GameObjectSnapshotService service = Service(new FakeDecoder(), (_, __) => Result());
                Assert.Throws<InvalidOperationException>(() => service.RenderAsync(
                    SnapshotSubject.VanillaGameObjectKey(Key), Options(), CancellationToken.None));
            }
            finally { SynchronizationContext.SetSynchronizationContext(original); }
        }

        private GameObjectSnapshotService Service(IPlateUpPreviewDecoder decoder,
            Func<VanillaPreviewModel, SnapshotRenderOptions, SnapshotRenderResult> render)
        {
            string path = Path.Combine(Path.GetTempPath(), "background-snapshot-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllBytes(path, VanillaAssetCatalogFile.Serialize(Catalog()));
            _paths.Add(path);
            var repository = new VanillaAssetCatalogRepository(path);
            Assert.That(repository.Reload(), Is.True);
            return new GameObjectSnapshotService("fixture", Catalog(), repository, decoder, null, render);
        }

        private static VanillaAssetCatalogData Catalog() => new VanillaAssetCatalogData
        {
            schemaVersion = 2,
            providerId = "plateup",
            providerName = "PlateUp",
            steamAppId = 1599600,
            sourceFingerprint = new string('A', 64),
            sources = Array.Empty<VanillaAssetSourceData>(),
            records = new[]
            {
                new VanillaAssetRecordData
                {
                    key = Key,
                    displayName = "Fixture",
                    kind = "gameObject",
                    source = new VanillaObjectIdentityData
                    {
                        relativePath = "fixture.assets",
                        pathId = 1,
                        classId = 1,
                        className = "GameObject"
                    },
                    metadata = Array.Empty<VanillaAssetMetadataData>()
                }
            },
            edges = Array.Empty<VanillaAssetEdgeData>(),
            diagnostics = Array.Empty<VanillaAssetDiagnosticData>()
        };

        private static SnapshotRenderOptions Options() => new SnapshotRenderOptions(32, 32);
        private static SnapshotRenderResult Result() => new SnapshotRenderResult(
            new byte[] { 1 }, 32, 32, 1, new Bounds(Vector3.zero, Vector3.one));

        private static IEnumerator Await(Func<bool> condition)
        {
            for (int index = 0; index < 200 && !condition(); index++) yield return null;
            Assert.That(condition(), Is.True, "Timed out waiting for background snapshot work.");
        }

        private const string Key = "plateup/game-object/fixture";

        private sealed class FakeDecoder : IPlateUpPreviewDecoder
        {
            public ManualResetEventSlim Gate;
            public volatile bool Started;
            public int ThreadId = -1;

            public VanillaPreviewModel Decode(string gameRoot, VanillaAssetRecord root,
                VanillaAssetCatalogData catalog, IProgress<SnapshotProgress> progress,
                CancellationToken cancellationToken)
            {
                ThreadId = Thread.CurrentThread.ManagedThreadId;
                Started = true;
                Gate?.Wait();
                cancellationToken.ThrowIfCancellationRequested();
                return new VanillaPreviewModel(new PreviewNodeData { Name = "Fixture" },
                    Array.Empty<PreviewMeshData>(), Array.Empty<string>());
            }

            public VanillaPreviewModel Decode(string gameRoot, VanillaObjectIdentity root,
                IProgress<SnapshotProgress> progress, CancellationToken cancellationToken) =>
                throw new NotSupportedException();
        }
    }
}
