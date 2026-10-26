using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

using KitchenLib.Studio.Editor.Catalogs.Mods;

namespace KitchenLib.Studio.Editor.Snapshots
{
    [Serializable] internal sealed class ExternalGdoSnapshotIndexData
    { public int schemaVersion = 1; public ExternalGdoSnapshotIndexEntry[] entries = Array.Empty<ExternalGdoSnapshotIndexEntry>(); }
    [Serializable] internal sealed class ExternalGdoSnapshotIndexEntry
    { public int gdoId; public string gameObjectKey; public string sourceFingerprint; }

    internal static class ExternalGdoSnapshotIndexFile
    {
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);
        public static ExternalGdoSnapshotIndexData Build(VanillaAssetCatalogData catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var entries = new List<ExternalGdoSnapshotIndexEntry>();
            VanillaAssetRecordData[] records = catalog.records ?? Array.Empty<VanillaAssetRecordData>();
            var recordsByKey = records.Where(value => value != null && !string.IsNullOrEmpty(value.key))
                .ToDictionary(value => value.key, StringComparer.Ordinal);
            var edgesBySource = (catalog.edges ?? Array.Empty<VanillaAssetEdgeData>()).Where(value => value != null)
                .GroupBy(value => value.sourceKey).ToDictionary(value => value.Key, value => value.ToArray(), StringComparer.Ordinal);
            var sourceHashes = (catalog.sources ?? Array.Empty<VanillaAssetSourceData>()).Where(value => value != null)
                .ToDictionary(value => (value.relativePath ?? string.Empty).Replace('\\', '/'),
                    value => value.sha256, StringComparer.OrdinalIgnoreCase);
            string[] slots = { "IconPrefab", "DisplayPrefab", "Prefab" };
            foreach (VanillaAssetRecordData gdo in records
                .Where(value => value != null && value.kind == "gdo" && value.gdoId != 0))
            {
                if (!edgesBySource.TryGetValue(gdo.key, out VanillaAssetEdgeData[] edges)) continue;
                VanillaAssetRecordData target = null;
                foreach (string slot in slots)
                {
                    VanillaAssetEdgeData edge = edges.FirstOrDefault(value => value.relation == "gameObject" && value.slot == slot);
                    if (edge != null && recordsByKey.TryGetValue(edge.targetKey, out target) && target.kind == "gameObject") break;
                    target = null;
                }
                if (target?.source == null) continue;
                string relative = (target.source.relativePath ?? string.Empty).Replace('\\', '/');
                sourceHashes.TryGetValue(relative, out string hash);
                string fingerprint = string.Join("\n", hash ?? catalog.sourceFingerprint ?? string.Empty,
                    relative.ToLowerInvariant(), target.source.pathId.ToString(System.Globalization.CultureInfo.InvariantCulture));
                entries.Add(new ExternalGdoSnapshotIndexEntry { gdoId = gdo.gdoId,
                    gameObjectKey = target.key, sourceFingerprint = fingerprint });
            }
            return new ExternalGdoSnapshotIndexData { entries = entries.OrderBy(value => value.gdoId).ToArray() };
        }
        public static byte[] Serialize(ExternalGdoSnapshotIndexData data) =>
            Utf8.GetBytes(JsonUtility.ToJson(data ?? throw new ArgumentNullException(nameof(data)), true) + "\n");
        public static ExternalGdoSnapshotIndexData Read(string path) =>
            JsonUtility.FromJson<ExternalGdoSnapshotIndexData>(File.ReadAllText(path, Utf8));
        public static void Publish(string path, VanillaAssetCatalogData catalog)
        { Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path, Serialize(Build(catalog))); }
    }

    internal sealed class IndexedExternalGdoSnapshotService : IExternalGdoSnapshotService
    {
        private readonly Dictionary<int, ExternalGdoSnapshotIndexEntry> _entries;
        private readonly SnapshotCache _cache;
        private readonly Func<System.Threading.Tasks.Task<IExternalGdoSnapshotService>> _fallbackFactory;
        private readonly List<Texture2D> _textures = new List<Texture2D>();
        private IExternalGdoSnapshotService _fallback;
        private System.Threading.Tasks.Task<IExternalGdoSnapshotService> _fallbackTask;
        private bool _disposed;
        public IndexedExternalGdoSnapshotService(ExternalGdoSnapshotIndexData index, SnapshotCache cache,
            Func<System.Threading.Tasks.Task<IExternalGdoSnapshotService>> fallbackFactory)
        { _entries = (index?.entries ?? Array.Empty<ExternalGdoSnapshotIndexEntry>()).ToDictionary(value => value.gdoId); _cache = cache; _fallbackFactory = fallbackFactory; }
        public IDisposable Request(string providerKey, int gdoId, string runtimeType,
            Action<ExternalGdoSnapshotResult> completed)
        {
            if (!ExternalGdoSnapshotEligibility.IsEligible(runtimeType))
            {
                completed(ExternalGdoSnapshotResults.Unsupported());
                return CompletedSnapshotSubscription.Instance;
            }
            if (!string.Equals(providerKey, ExternalGdoCatalogRepository.VanillaProviderKey,
                StringComparison.Ordinal))
            {
                completed(new ExternalGdoSnapshotResult(ExternalGdoSnapshotState.Unavailable,
                    diagnostic: "This snapshot index only supports the vanilla provider."));
                return CompletedSnapshotSubscription.Instance;
            }
            if (_entries.TryGetValue(gdoId, out ExternalGdoSnapshotIndexEntry entry))
            {
                string key = SnapshotCache.BuildKey(ExternalGdoSnapshotCacheIdentity.VanillaSubject(entry.gameObjectKey),
                    entry.sourceFingerprint, ExternalGdoSnapshotCacheIdentity.RendererVersion,
                    new SnapshotRenderOptions(ExternalGdoSnapshotCacheIdentity.ThumbnailSize, ExternalGdoSnapshotCacheIdentity.ThumbnailSize),
                    ExternalGdoSnapshotCacheIdentity.SettingsFingerprint);
                SnapshotCacheEntry cached = _cache.TryGet(key);
                if (cached != null)
                {
                    var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
                    if (texture.LoadImage(cached.PngBytes, true)) { _textures.Add(texture); completed(new ExternalGdoSnapshotResult(ExternalGdoSnapshotState.Ready, texture)); return CompletedSnapshotSubscription.Instance; }
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }
            if (_fallback != null) return _fallback.Request(providerKey, gdoId, runtimeType, completed);
            completed(new ExternalGdoSnapshotResult(ExternalGdoSnapshotState.Loading,
                diagnostic: "Loading snapshot catalogue…"));
            StartFallback(gdoId, runtimeType, completed);
            return CompletedSnapshotSubscription.Instance;
        }
        private async void StartFallback(int gdoId, string runtimeType,
            Action<ExternalGdoSnapshotResult> completed)
        {
            try
            {
                _fallbackTask = _fallbackTask ?? _fallbackFactory();
                IExternalGdoSnapshotService service = await _fallbackTask;
                if (_disposed) { service.Dispose(); return; }
                _fallback = service;
                _fallback.Request(ExternalGdoCatalogRepository.VanillaProviderKey, gdoId,
                    runtimeType, completed);
            }
            catch (Exception exception)
            { if (!_disposed) completed(new ExternalGdoSnapshotResult(ExternalGdoSnapshotState.Failed, diagnostic: exception.Message)); }
        }
        public void Dispose()
        { _disposed = true; _fallback?.Dispose(); foreach (Texture2D texture in _textures) if (texture != null) UnityEngine.Object.DestroyImmediate(texture); _textures.Clear(); }
    }
}
