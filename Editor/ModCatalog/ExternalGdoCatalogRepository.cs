using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace KitchenLib.Studio.Editor
{
    public sealed class ExternalGdoProvider
    {
        public ExternalGdoProvider(string key, string displayName) { Key = key; DisplayName = displayName; }
        public string Key { get; }
        public string DisplayName { get; }
    }

    public sealed class ExternalGdoCatalogEntry
    {
        internal ExternalGdoCatalogEntry(string providerKey, string providerName, int runtimeId,
            string runtimeType, string displayName, string catalogRoot, string previewRelativePath)
        {
            ProviderKey = providerKey; ProviderName = providerName; RuntimeId = runtimeId;
            RuntimeType = runtimeType; DisplayName = displayName; CatalogRoot = catalogRoot;
            PreviewRelativePath = previewRelativePath;
        }
        public string ProviderKey { get; }
        public string ProviderName { get; }
        public int RuntimeId { get; }
        public string RuntimeType { get; }
        public string DisplayName { get; }
        public string CatalogRoot { get; }
        public string PreviewRelativePath { get; }
    }

    public enum ExternalGdoRelationshipDirection { Outgoing, Incoming }

    public sealed class ExternalGdoRelatedEntry
    {
        internal ExternalGdoRelatedEntry(ExternalGdoRelationshipDirection direction, string label,
            string fieldPath, ExternalGdoCatalogEntry entry)
        { Direction = direction; Label = label; FieldPath = fieldPath; Entry = entry; }
        public ExternalGdoRelationshipDirection Direction { get; }
        public string Label { get; }
        public string FieldPath { get; }
        public ExternalGdoCatalogEntry Entry { get; }
    }

    public sealed class ExternalGdoCatalogRepository
    {
        public const string VanillaProviderKey = "vanilla:plateup";
        private readonly VanillaGdoCatalogRepository _vanilla;
        private readonly string _cacheRoot;
        private ExternalGdoCatalogEntry[] _entries = new ExternalGdoCatalogEntry[0];
        private ExternalGdoProvider[] _providers = new ExternalGdoProvider[0];
        private string[] _diagnostics = new string[0];
        private bool _reloading;
        private Dictionary<string, ExternalGdoRelatedEntry[]> _related =
            new Dictionary<string, ExternalGdoRelatedEntry[]>(StringComparer.Ordinal);
        private static readonly Lazy<ExternalGdoCatalogRepository> Shared =
            new Lazy<ExternalGdoCatalogRepository>(CreateCurrent);

        public ExternalGdoCatalogRepository(VanillaGdoCatalogRepository vanilla, string cacheRoot)
        {
            _vanilla = vanilla ?? throw new ArgumentNullException(nameof(vanilla));
            _cacheRoot = cacheRoot;
            _vanilla.Changed += OnVanillaChanged;
        }

        public static ExternalGdoCatalogRepository Current => Shared.Value;
        public IReadOnlyList<ExternalGdoCatalogEntry> Entries => _entries;
        public IReadOnlyList<ExternalGdoProvider> Providers => _providers;
        public IReadOnlyList<string> Diagnostics => _diagnostics;
        public event Action Changed;

        public bool Reload()
        {
            if (_reloading) return _entries.Length > 0;
            _reloading = true;
            try { return ReloadCore(); }
            finally { _reloading = false; }
        }

        private bool ReloadCore()
        {
            var entries = new Dictionary<string, ExternalGdoCatalogEntry>(StringComparer.Ordinal);
            var providers = new Dictionary<string, ExternalGdoProvider>(StringComparer.Ordinal);
            var diagnostics = new List<string>();
            var relationships = new List<ExternalGdoRelationshipData>();
            _vanilla.Reload();
            if (_vanilla.Catalog != null)
            {
                providers[VanillaProviderKey] = new ExternalGdoProvider(VanillaProviderKey, "PlateUp");
                foreach (VanillaGdoCatalogEntry item in _vanilla.Catalog.Entries)
                    entries[Identity(VanillaProviderKey, item.Id, item.RuntimeType)] = new ExternalGdoCatalogEntry(
                        VanillaProviderKey, "PlateUp", item.Id, item.RuntimeType, item.Name, null, null);
                relationships.AddRange(_vanilla.Catalog.Data.relationships ?? Array.Empty<ExternalGdoRelationshipData>());
            }
            if (!string.IsNullOrWhiteSpace(_cacheRoot) && Directory.Exists(_cacheRoot))
            {
                foreach (string path in CataloguePaths(_cacheRoot, diagnostics))
                {
                    try
                    {
                        ModGdoCatalogData data = ModGdoCatalogFile.Read(path);
                        string root = Path.GetDirectoryName(path);
                        var sourceProviders = data.providers.ToDictionary(item => item.key, StringComparer.Ordinal);
                        foreach (ModGdoEntryData item in data.entries)
                        {
                            if (!sourceProviders.TryGetValue(item.providerKey, out ModGdoProviderData provider)) continue;
                            providers[provider.key] = new ExternalGdoProvider(provider.key, provider.displayName);
                            string identity = Identity(provider.key, item.runtimeId, item.runtimeType);
                            if (entries.ContainsKey(identity))
                            {
                                diagnostics.Add("Duplicate external GDO identity '" + identity + "' in " + path + ".");
                                continue;
                            }
                            entries.Add(identity, new ExternalGdoCatalogEntry(provider.key, provider.displayName, item.runtimeId,
                                item.runtimeType, item.displayName, root, item.previewRelativePath));
                        }
                        relationships.AddRange(data.relationships ?? Array.Empty<ExternalGdoRelationshipData>());
                    }
                    catch (Exception exception) when (exception is InvalidDataException || exception is IOException || exception is UnauthorizedAccessException)
                    { diagnostics.Add(path + ": " + exception.Message); }
                }
            }
            _entries = entries.Values.ToArray();
            _related = BuildRelated(entries, relationships, diagnostics);
            _providers = providers.Values.OrderBy(item => item.Key == VanillaProviderKey ? 0 : 1)
                .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray();
            _diagnostics = diagnostics.ToArray();
            Changed?.Invoke();
            return _entries.Length > 0;
        }

        private void OnVanillaChanged()
        {
            if (!_reloading) Reload();
        }

        private static Dictionary<string, ExternalGdoRelatedEntry[]> BuildRelated(
            IReadOnlyDictionary<string, ExternalGdoCatalogEntry> entries,
            IEnumerable<ExternalGdoRelationshipData> relationships, ICollection<string> diagnostics)
        {
            var related = new Dictionary<string, List<ExternalGdoRelatedEntry>>(StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (ExternalGdoRelationshipData relationship in relationships)
            {
                string sourceKey = Identity(relationship.source.providerKey, relationship.source.runtimeId,
                    relationship.source.runtimeType);
                string targetKey = Identity(relationship.target.providerKey, relationship.target.runtimeId,
                    relationship.target.runtimeType);
                if (!entries.TryGetValue(sourceKey, out ExternalGdoCatalogEntry source) ||
                    !entries.TryGetValue(targetKey, out ExternalGdoCatalogEntry target))
                {
                    diagnostics.Add("Related GDO target is missing for '" + relationship.fieldPath + "'.");
                    continue;
                }
                string row = sourceKey + "\n" + relationship.fieldPath + "\n" + targetKey;
                if (!seen.Add(row)) continue;
                AddRelated(related, sourceKey, new ExternalGdoRelatedEntry(
                    ExternalGdoRelationshipDirection.Outgoing, relationship.label, relationship.fieldPath, target));
                AddRelated(related, targetKey, new ExternalGdoRelatedEntry(
                    ExternalGdoRelationshipDirection.Incoming, relationship.label, relationship.fieldPath, source));
            }
            return related.ToDictionary(pair => pair.Key, pair => pair.Value
                .OrderBy(item => item.Direction).ThenBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Entry.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Entry.ProviderKey, StringComparer.Ordinal)
                .ThenBy(item => item.Entry.RuntimeId).ToArray(), StringComparer.Ordinal);
        }

        private static void AddRelated(IDictionary<string, List<ExternalGdoRelatedEntry>> values,
            string key, ExternalGdoRelatedEntry entry)
        {
            if (!values.TryGetValue(key, out List<ExternalGdoRelatedEntry> list))
                values.Add(key, list = new List<ExternalGdoRelatedEntry>());
            list.Add(entry);
        }

        private static IEnumerable<string> CataloguePaths(string root, ICollection<string> diagnostics)
        {
            try
            {
                return Directory.GetFiles(root, "mod-gdo-catalog.json", SearchOption.AllDirectories)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                diagnostics.Add(root + ": " + exception.Message);
                return Array.Empty<string>();
            }
        }

        private static string Identity(string providerKey, int runtimeId, string runtimeType) =>
            providerKey + "\n" + runtimeId.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\n" + runtimeType;

        public ExternalGdoCatalogEntry Find(string providerKey, int runtimeId, string runtimeType) => _entries.FirstOrDefault(item =>
            item.ProviderKey == (string.IsNullOrEmpty(providerKey) ? VanillaProviderKey : providerKey) &&
            item.RuntimeId == runtimeId && (string.IsNullOrEmpty(runtimeType) || item.RuntimeType == runtimeType));

        public IReadOnlyList<ExternalGdoRelatedEntry> Related(string providerKey, int runtimeId, string runtimeType)
        {
            string key = Identity(string.IsNullOrEmpty(providerKey) ? VanillaProviderKey : providerKey,
                runtimeId, runtimeType);
            return _related.TryGetValue(key, out ExternalGdoRelatedEntry[] values)
                ? values : Array.Empty<ExternalGdoRelatedEntry>();
        }

        public IReadOnlyList<ExternalGdoCatalogEntry> Search(string query, string expectedType = null,
            string exactRuntimeType = null, string providerKey = null)
        {
            string value = query ?? string.Empty;
            return _entries.Where(item => string.IsNullOrEmpty(providerKey) || item.ProviderKey == providerKey)
                .Where(item => string.IsNullOrEmpty(expectedType) || GdoDescriptorCatalog.IsAssignable(item.RuntimeType, expectedType))
                .Where(item => string.IsNullOrEmpty(exactRuntimeType) || item.RuntimeType == exactRuntimeType)
                .Where(item => item.DisplayName.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    item.RuntimeType.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    item.ProviderName.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    item.RuntimeId.ToString().Contains(value))
                .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.ProviderName).ToArray();
        }

        public IReadOnlyList<string> RuntimeTypes(string expectedType = null, string providerKey = null) =>
            Search(string.Empty, expectedType, null, providerKey).Select(item => item.RuntimeType)
                .Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToArray();

        private static ExternalGdoCatalogRepository CreateCurrent()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var result = new ExternalGdoCatalogRepository(VanillaGdoCatalogRepository.Current,
                LocalModCatalogPackage.DefaultCacheRoot(projectRoot));
            LocalModCatalogPackage.Imported += () => result.Reload();
            result.Reload();
            return result;
        }
    }
}
