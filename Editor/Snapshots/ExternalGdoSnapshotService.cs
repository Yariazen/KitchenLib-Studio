using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

using KitchenLib.Studio.Editor.Catalogs.Mods;

namespace KitchenLib.Studio.Editor.Snapshots
{
    internal enum ExternalGdoSnapshotState { Loading, Ready, Unsupported, Unavailable, Failed }

    internal sealed class ExternalGdoSnapshotResult
    {
        public ExternalGdoSnapshotResult(ExternalGdoSnapshotState state, Texture2D texture = null,
            string diagnostic = null)
        {
            State = state;
            Texture = texture;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public ExternalGdoSnapshotState State { get; }
        public Texture2D Texture { get; }
        public string Diagnostic { get; }
    }

    internal static class ExternalGdoSnapshotResults
    {
        public static ExternalGdoSnapshotResult Unsupported() => new ExternalGdoSnapshotResult(
            ExternalGdoSnapshotState.Unsupported, diagnostic: "No snapshot for this GDO type.");
    }

    internal sealed class ExternalGdoSnapshotServiceDependencies
    {
        public Func<string, SnapshotCacheEntry> CacheGet { get; set; }
        public Action<string, byte[], SnapshotCacheMetadata> CachePut { get; set; }
        public Func<string, CancellationToken, Task<SnapshotRenderResult>> Render { get; set; }
        public Func<byte[], Texture2D> LoadTexture { get; set; }
        public Action<Texture2D> DestroyTexture { get; set; }
        public Func<bool> CanRender { get; set; }
    }

    internal interface IExternalGdoSnapshotService : IDisposable
    {
        IDisposable Request(string providerKey, int gdoId, string runtimeType,
            Action<ExternalGdoSnapshotResult> completed);
    }

    internal sealed class CompletedSnapshotSubscription : IDisposable
    { public static readonly CompletedSnapshotSubscription Instance = new CompletedSnapshotSubscription(); public void Dispose() { } }

    internal static class ExternalGdoSnapshotCacheIdentity
    {
        public const string RendererVersion = "2";
        public const string SettingsFingerprint = "studio-v1";
        public const int ThumbnailSize = 128;

        public static string VanillaSubject(string gameObjectKey) =>
            "vanilla:" + (gameObjectKey ?? string.Empty);

        public static string BuildKey(string gameObjectKey, string sourceFingerprint) => SnapshotCache.BuildKey(
            VanillaSubject(gameObjectKey), sourceFingerprint, RendererVersion,
            new SnapshotRenderOptions(ThumbnailSize, ThumbnailSize), SettingsFingerprint);

        public static string BuildKey(VanillaAssetCatalogData catalog, string gameObjectKey,
            SnapshotRenderOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            return SnapshotCache.BuildKey(VanillaSubject(gameObjectKey),
                SourceFingerprint(catalog, gameObjectKey), RendererVersion, options, SettingsFingerprint);
        }

        public static string SourceFingerprint(VanillaAssetCatalogData catalog, string gameObjectKey)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            VanillaAssetRecordData record = null;
            foreach (VanillaAssetRecordData candidate in catalog.records ?? Array.Empty<VanillaAssetRecordData>())
                if (candidate != null && string.Equals(candidate.key, gameObjectKey, StringComparison.Ordinal))
                { record = candidate; break; }
            if (record?.source == null) return catalog.sourceFingerprint ?? string.Empty;
            string relativePath = (record.source.relativePath ?? string.Empty).Replace('\\', '/');
            string sourceHash = null;
            foreach (VanillaAssetSourceData source in catalog.sources ?? Array.Empty<VanillaAssetSourceData>())
                if (source != null && string.Equals((source.relativePath ?? string.Empty).Replace('\\', '/'),
                    relativePath, StringComparison.OrdinalIgnoreCase))
                { sourceHash = source.sha256; break; }
            if (string.IsNullOrEmpty(sourceHash)) sourceHash = catalog.sourceFingerprint ?? string.Empty;
            return string.Join("\n", sourceHash, relativePath.ToLowerInvariant(),
                record.source.pathId.ToString(CultureInfo.InvariantCulture));
        }
    }

    internal sealed class ExternalGdoSnapshotService : IExternalGdoSnapshotService
    {
        private readonly VanillaAssetCatalogData _catalog;
        private readonly ExternalGdoSnapshotServiceDependencies _dependencies;
        private readonly Dictionary<string, Work> _work = new Dictionary<string, Work>(StringComparer.Ordinal);
        private readonly Queue<Work> _queue = new Queue<Work>();
        private readonly HashSet<Texture2D> _ownedTextures = new HashSet<Texture2D>();
        private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();
        private bool _rendering;
        private bool _disposed;

        public ExternalGdoSnapshotService(VanillaAssetCatalogData catalog,
            ExternalGdoSnapshotServiceDependencies dependencies)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _dependencies = dependencies ?? throw new ArgumentNullException(nameof(dependencies));
            if (_dependencies.CacheGet == null || _dependencies.CachePut == null ||
                _dependencies.Render == null || _dependencies.LoadTexture == null ||
                _dependencies.DestroyTexture == null || _dependencies.CanRender == null)
                throw new ArgumentException("All snapshot service dependencies are required.", nameof(dependencies));
        }

        public static ExternalGdoSnapshotService CreateDefault(VanillaAssetCatalogData catalog,
            VanillaAssetCatalogRepository repository, string gameRoot, SnapshotCache cache = null)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (repository == null) throw new ArgumentNullException(nameof(repository));
            cache = cache ?? new SnapshotCache();
            var renderer = new GameObjectSnapshotService(gameRoot, catalog, repository);
            var options = new SnapshotRenderOptions(
                ExternalGdoSnapshotCacheIdentity.ThumbnailSize,
                ExternalGdoSnapshotCacheIdentity.ThumbnailSize);
            return new ExternalGdoSnapshotService(catalog, new ExternalGdoSnapshotServiceDependencies
            {
                CacheGet = cache.TryGet,
                CachePut = (key, bytes, metadata) => cache.Put(key, bytes, metadata),
                Render = (key, token) => renderer.RenderAsync(SnapshotSubject.VanillaGameObjectKey(key), options, token),
                LoadTexture = LoadPng,
                DestroyTexture = texture => UnityEngine.Object.DestroyImmediate(texture),
                CanRender = () => !string.IsNullOrEmpty(gameRoot)
            });
        }

        public IDisposable Request(string providerKey, int gdoId, string runtimeType,
            Action<ExternalGdoSnapshotResult> completed)
        {
            if (completed == null) throw new ArgumentNullException(nameof(completed));
            if (_disposed) return CompletedSnapshotSubscription.Instance;
            if (!ExternalGdoSnapshotEligibility.IsEligible(runtimeType))
            {
                completed(ExternalGdoSnapshotResults.Unsupported());
                return CompletedSnapshotSubscription.Instance;
            }
            if (!string.Equals(providerKey, ExternalGdoCatalogRepository.VanillaProviderKey,
                StringComparison.Ordinal))
            {
                completed(new ExternalGdoSnapshotResult(ExternalGdoSnapshotState.Unavailable,
                    diagnostic: "This snapshot service only supports the vanilla provider."));
                return CompletedSnapshotSubscription.Instance;
            }
            ExternalGdoSnapshotSourceResolution source = ExternalGdoSnapshotSourceResolver.Resolve(_catalog, gdoId);
            if (!source.Succeeded)
            {
                completed(new ExternalGdoSnapshotResult(ExternalGdoSnapshotState.Unavailable,
                    diagnostic: source.Diagnostic));
                return CompletedSnapshotSubscription.Instance;
            }

            if (_work.TryGetValue(source.GameObjectKey, out Work existing))
            {
                if (existing.Result != null) completed(existing.Result);
                else
                {
                    existing.Subscribers.Add(completed);
                    completed(Loading());
                }
                return new Subscription(existing, completed);
            }

            string cacheKey = ExternalGdoSnapshotCacheIdentity.BuildKey(_catalog, source.GameObjectKey,
                new SnapshotRenderOptions(ExternalGdoSnapshotCacheIdentity.ThumbnailSize,
                    ExternalGdoSnapshotCacheIdentity.ThumbnailSize));
            SnapshotCacheEntry cached = _dependencies.CacheGet(cacheKey);
            if (cached != null)
            {
                try
                {
                    Texture2D texture = LoadOwned(cached.PngBytes);
                    var ready = new ExternalGdoSnapshotResult(ExternalGdoSnapshotState.Ready, texture);
                    _work.Add(source.GameObjectKey, new Work(source.GameObjectKey, cacheKey) { Result = ready });
                    completed(ready);
                    return CompletedSnapshotSubscription.Instance;
                }
                catch (InvalidOperationException) { }
            }
            if (!_dependencies.CanRender())
            {
                completed(new ExternalGdoSnapshotResult(ExternalGdoSnapshotState.Unavailable,
                    diagnostic: "PlateUp is unavailable and this snapshot is not cached."));
                return CompletedSnapshotSubscription.Instance;
            }

            var work = new Work(source.GameObjectKey, cacheKey);
            work.Subscribers.Add(completed);
            _work.Add(source.GameObjectKey, work);
            _queue.Enqueue(work);
            completed(Loading());
            StartNext();
            return new Subscription(work, completed);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _cancellation.Cancel();
            _queue.Clear();
            foreach (Work value in _work.Values) value.Subscribers.Clear();
            foreach (Texture2D texture in _ownedTextures)
                if (texture != null) _dependencies.DestroyTexture(texture);
            _ownedTextures.Clear();
            _work.Clear();
            _cancellation.Dispose();
        }

        private async void StartNext()
        {
            if (_disposed || _rendering || _queue.Count == 0) return;
            Work work = _queue.Dequeue();
            _rendering = true;
            try
            {
                SnapshotRenderResult rendered = await _dependencies.Render(work.GameObjectKey, _cancellation.Token);
                if (_disposed || _cancellation.IsCancellationRequested) return;
                var metadata = new SnapshotCacheMetadata
                {
                    width = rendered.Width,
                    height = rendered.Height,
                    subjectIdentity = ExternalGdoSnapshotCacheIdentity.VanillaSubject(work.GameObjectKey),
                    sourceFingerprint = ExternalGdoSnapshotCacheIdentity.SourceFingerprint(
                        _catalog, work.GameObjectKey),
                    rendererVersion = ExternalGdoSnapshotCacheIdentity.RendererVersion,
                    diagnostics = Array.Empty<string>()
                };
                _dependencies.CachePut(work.CacheKey, rendered.PngBytes, metadata);
                Texture2D texture = LoadOwned(rendered.PngBytes);
                Complete(work, new ExternalGdoSnapshotResult(ExternalGdoSnapshotState.Ready, texture));
            }
            catch (OperationCanceledException)
            {
                if (!_disposed) Complete(work, new ExternalGdoSnapshotResult(
                    ExternalGdoSnapshotState.Unavailable, diagnostic: "Snapshot generation was cancelled."));
            }
            catch (Exception exception)
            {
                if (!_disposed) Complete(work, new ExternalGdoSnapshotResult(
                    ExternalGdoSnapshotState.Failed, diagnostic: exception.Message));
            }
            finally
            {
                _rendering = false;
                if (!_disposed) StartNext();
            }
        }

        private Texture2D LoadOwned(byte[] png)
        {
            Texture2D texture = _dependencies.LoadTexture(png);
            if (texture == null) throw new InvalidOperationException("Snapshot PNG could not be loaded.");
            texture.hideFlags = HideFlags.HideAndDontSave;
            _ownedTextures.Add(texture);
            return texture;
        }

        private static Texture2D LoadPng(byte[] png)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (png != null && texture.LoadImage(png, true)) return texture;
            UnityEngine.Object.DestroyImmediate(texture);
            return null;
        }

        private static ExternalGdoSnapshotResult Loading() =>
            new ExternalGdoSnapshotResult(ExternalGdoSnapshotState.Loading,
                diagnostic: "Generating snapshot…");

        private static void Complete(Work work, ExternalGdoSnapshotResult result)
        {
            work.Result = result;
            Action<ExternalGdoSnapshotResult>[] subscribers = work.Subscribers.ToArray();
            work.Subscribers.Clear();
            foreach (Action<ExternalGdoSnapshotResult> subscriber in subscribers)
                try { subscriber(result); }
                catch (Exception exception) { Debug.LogException(exception); }
        }

        private sealed class Work
        {
            public Work(string gameObjectKey, string cacheKey)
            { GameObjectKey = gameObjectKey; CacheKey = cacheKey; }
            public string GameObjectKey { get; }
            public string CacheKey { get; }
            public List<Action<ExternalGdoSnapshotResult>> Subscribers { get; } =
                new List<Action<ExternalGdoSnapshotResult>>();
            public ExternalGdoSnapshotResult Result { get; set; }
        }

        private sealed class Subscription : IDisposable
        {
            private Work _work;
            private Action<ExternalGdoSnapshotResult> _callback;
            public Subscription(Work work, Action<ExternalGdoSnapshotResult> callback)
            { _work = work; _callback = callback; }
            public void Dispose()
            {
                if (_work != null && _callback != null) _work.Subscribers.Remove(_callback);
                _work = null;
                _callback = null;
            }
        }

    }
}
