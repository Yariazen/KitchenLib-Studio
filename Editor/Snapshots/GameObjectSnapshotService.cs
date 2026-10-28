using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace KitchenLib.Studio.Editor.Snapshots
{
    internal sealed class SnapshotRenderTiming
    {
        public SnapshotRenderTiming(TimeSpan decode, TimeSpan render) { Decode = decode; Render = render; }
        public TimeSpan Decode { get; }
        public TimeSpan Render { get; }
    }

    internal sealed class GameObjectSnapshotService
    {
        private readonly string _gameRoot;
        private readonly VanillaAssetCatalogData _catalog;
        private readonly VanillaAssetCatalogRepository _repository;
        private readonly IPlateUpPreviewDecoder _decoder;
        private readonly CustomGdoSnapshotResolver _gdoResolver;
        private readonly Func<VanillaPreviewModel, SnapshotRenderOptions, SnapshotRenderResult> _renderVanilla;

        public GameObjectSnapshotService() : this(null, null, null, null, null, null) { }
        public GameObjectSnapshotService(string gameRoot, VanillaAssetCatalogData catalog,
            VanillaAssetCatalogRepository repository, IPlateUpPreviewDecoder decoder = null,
            CustomGdoSnapshotResolver gdoResolver = null,
            Func<VanillaPreviewModel, SnapshotRenderOptions, SnapshotRenderResult> renderVanilla = null)
        {
            _gameRoot = gameRoot; _catalog = catalog; _repository = repository;
            _decoder = decoder ?? new AssetsToolsPlateUpPreviewDecoder();
            _gdoResolver = gdoResolver ?? new CustomGdoSnapshotResolver();
            _renderVanilla = renderVanilla ?? ReconstructAndRenderVanilla;
        }

        public Task<SnapshotRenderResult> RenderAsync(SnapshotSubject subject, SnapshotRenderOptions options,
            CancellationToken cancellationToken)
        {
            if (subject == null) throw new ArgumentNullException(nameof(subject));
            if (options == null) throw new ArgumentNullException(nameof(options));
            cancellationToken.ThrowIfCancellationRequested();
            switch (subject.Kind)
            {
                case SnapshotSubjectKind.LocalGameObject:
                    return Task.FromResult(RenderLocal(subject.GameObject, options, cancellationToken));
                case SnapshotSubjectKind.CustomGdo:
                    SnapshotResolution resolution = _gdoResolver.Resolve(subject.Gdo, subject.PrefabFieldPath);
                    if (!resolution.Succeeded) throw new InvalidOperationException(resolution.Diagnostic);
                    return Task.FromResult(RenderLocal(resolution.GameObject, options, cancellationToken));
                case SnapshotSubjectKind.VanillaGameObjectKey:
                    if (SynchronizationContext.Current == null)
                        throw new InvalidOperationException("Vanilla snapshots require the Unity editor synchronization context.");
                    return RenderVanillaAsync(subject.VanillaKey, options, cancellationToken);
                default: throw new ArgumentOutOfRangeException();
            }
        }

        private async Task<SnapshotRenderResult> RenderVanillaAsync(string key, SnapshotRenderOptions options,
            CancellationToken token)
        {
            if (_repository == null || _catalog == null || string.IsNullOrEmpty(_gameRoot))
                throw new InvalidOperationException("Vanilla snapshot dependencies are not configured.");
            VanillaAssetRecord record = _repository.Find(key);
            if (record == null) throw new InvalidOperationException("Vanilla GameObject key was not found: " + key);
            token.ThrowIfCancellationRequested();
            VanillaPreviewModel model = await Task.Run(
                () => _decoder.Decode(_gameRoot, record, _catalog, null, token), token);
            token.ThrowIfCancellationRequested();
            return _renderVanilla(model, options);
        }

        internal SnapshotRenderResult RenderVanillaBlocking(string key, SnapshotRenderOptions options,
            CancellationToken token)
        {
            if (_repository == null || _catalog == null || string.IsNullOrEmpty(_gameRoot))
                throw new InvalidOperationException("Vanilla snapshot dependencies are not configured.");
            VanillaAssetRecord record = _repository.Find(key);
            if (record == null) throw new InvalidOperationException("Vanilla GameObject key was not found: " + key);
            token.ThrowIfCancellationRequested();
            VanillaPreviewModel model = Task.Run(
                () => _decoder.Decode(_gameRoot, record, _catalog, null, token), token).GetAwaiter().GetResult();
            token.ThrowIfCancellationRequested();
            return _renderVanilla(model, options);
        }

        internal SnapshotRenderResult RenderVanillaBlocking(IPlateUpPreviewDecodeSession session, string key,
            SnapshotRenderOptions options, CancellationToken token, Action<SnapshotRenderTiming> timing = null)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (_repository == null) throw new InvalidOperationException("Vanilla snapshot repository is not configured.");
            VanillaAssetRecord record = _repository.Find(key);
            if (record == null) throw new InvalidOperationException("Vanilla GameObject key was not found: " + key);
            var watch = Stopwatch.StartNew();
            TimeSpan decode = TimeSpan.Zero;
            TimeSpan render = TimeSpan.Zero;
            try
            {
                VanillaPreviewModel model = session.Decode(record, null, token);
                decode = watch.Elapsed;
                token.ThrowIfCancellationRequested();
                watch.Restart();
                try { return _renderVanilla(model, options); }
                finally { render = watch.Elapsed; }
            }
            finally
            {
                if (decode == TimeSpan.Zero) decode = watch.Elapsed;
                timing?.Invoke(new SnapshotRenderTiming(decode, render));
            }
        }

        private static SnapshotRenderResult ReconstructAndRenderVanilla(VanillaPreviewModel model,
            SnapshotRenderOptions options)
        {
            using (ReconstructedPreview preview = VanillaPreviewReconstructor.Reconstruct(model))
                return GameObjectSnapshotRenderer.Render(preview.Root, options);
        }

        private static SnapshotRenderResult RenderLocal(GameObject source, SnapshotRenderOptions options, CancellationToken token)
        {
            if (source == null) throw new InvalidOperationException("Snapshot GameObject is missing.");
            token.ThrowIfCancellationRequested();
            GameObject clone = UnityEngine.Object.Instantiate(source);
            try
            {
                clone.name = source.name;
                foreach (Transform value in clone.GetComponentsInChildren<Transform>(true)) value.gameObject.hideFlags = HideFlags.HideAndDontSave;
                token.ThrowIfCancellationRequested();
                return GameObjectSnapshotRenderer.Render(clone, options);
            }
            finally { if (clone != null) UnityEngine.Object.DestroyImmediate(clone); }
        }
    }
}
