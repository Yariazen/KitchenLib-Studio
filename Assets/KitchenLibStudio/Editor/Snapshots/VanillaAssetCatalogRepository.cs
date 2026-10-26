using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace KitchenLib.Studio.Editor
{
    public sealed class VanillaAssetCatalogRepository
    {
        private readonly string _path;
        private Dictionary<string, VanillaAssetRecord> _records = new Dictionary<string, VanillaAssetRecord>(StringComparer.Ordinal);
        private Dictionary<string, string[]> _forward = new Dictionary<string, string[]>(StringComparer.Ordinal);
        private Dictionary<string, string[]> _reverse = new Dictionary<string, string[]>(StringComparer.Ordinal);
        public VanillaAssetCatalogRepository(string path) { _path = path; }
        public VanillaAssetCatalogRepository(VanillaAssetCatalogData data) { Reload(data); }
        public event Action Changed;
        public IReadOnlyDictionary<string, VanillaAssetRecord> AssetsByKey => _records;
        public bool Reload()
        {
            if (!File.Exists(_path)) return false;
            return Reload(VanillaAssetCatalogFile.Read(_path));
        }
        public bool Reload(VanillaAssetCatalogData data)
        {
            if (data == null) return false;
            _forward = data.edges.GroupBy(edge => edge.sourceKey).ToDictionary(group => group.Key,
                group => group.Select(edge => edge.targetKey).Distinct().ToArray(), StringComparer.Ordinal);
            _reverse = data.edges.GroupBy(edge => edge.targetKey).ToDictionary(group => group.Key,
                group => group.Select(edge => edge.sourceKey).Distinct().ToArray(), StringComparer.Ordinal);
            _records = data.records.ToDictionary(record => record.key,
                record => new VanillaAssetRecord(record, data.edges.Where(edge => edge.sourceKey == record.key)), StringComparer.Ordinal);
            Changed?.Invoke(); return true;
        }
        public VanillaAssetRecord Find(string key) => key != null && _records.TryGetValue(key, out VanillaAssetRecord value) ? value : null;
        public IReadOnlyList<VanillaAssetRecord> Search(string query, VanillaAssetKind? kind = null, bool includeStructural = false)
        {
            string text = (query ?? string.Empty).Trim();
            return _records.Values.Where(record => (includeStructural || !record.Structural) &&
                (!kind.HasValue || string.Equals(record.Kind, KindName(kind.Value), StringComparison.Ordinal)) &&
                (text.Length == 0 || record.Key.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0 ||
                 (record.DisplayName ?? string.Empty).IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0))
                .OrderByDescending(record => record.HierarchyRoot).ThenBy(record => record.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(record => record.Key, StringComparer.Ordinal).ToArray();
        }
        public IReadOnlyList<VanillaAssetRecord> Dependencies(string key) => Resolve(_forward, key);
        public IReadOnlyList<VanillaAssetRecord> Dependents(string key) => Resolve(_reverse, key);
        private IReadOnlyList<VanillaAssetRecord> Resolve(Dictionary<string, string[]> index, string key) =>
            key != null && index.TryGetValue(key, out string[] values) ? values.Select(Find).Where(value => value != null).ToArray() : Array.Empty<VanillaAssetRecord>();
        private static string KindName(VanillaAssetKind kind) => kind == VanillaAssetKind.GameObject ? "gameObject" :
            char.ToLowerInvariant(kind.ToString()[0]) + kind.ToString().Substring(1);
    }
}
