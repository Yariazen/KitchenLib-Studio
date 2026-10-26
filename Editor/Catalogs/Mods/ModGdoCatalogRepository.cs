using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using KitchenLib.Studio.Editor.Authoring;

namespace KitchenLib.Studio.Editor.Catalogs.Mods
{
    internal enum ModGdoCatalogState { Missing, Ready, Malformed, IncompatibleSchema, StaleCapture, StaleBuild, DuplicateRuntimeId }
    internal enum ModGdoProviderProvenance { Unknown, KitchenLib, Workshop, Assembly }
    internal enum ModGdoPreviewState { Missing, Pending, Ready, Failed }

    internal sealed class ModGdoProvider
    {
        internal ModGdoProvider(string key, string displayName, string version, string workshopId,
            string sourceAssembly, ModGdoProviderProvenance provenance)
        {
            Key = key; DisplayName = displayName; Version = version; WorkshopId = workshopId;
            SourceAssembly = sourceAssembly; Provenance = provenance;
        }
        public string Key { get; }
        public string DisplayName { get; }
        public string Version { get; }
        public string WorkshopId { get; }
        public string SourceAssembly { get; }
        public ModGdoProviderProvenance Provenance { get; }
    }

    internal sealed class ModGdoCatalogEntry
    {
        internal ModGdoCatalogEntry(ModGdoEntryData data, ModGdoProvider provider)
        {
            Provider = provider; RuntimeId = data.runtimeId; DisplayName = data.displayName;
            RuntimeType = data.runtimeType; SourceAssembly = data.sourceAssembly;
            CustomRegistrationType = data.customRegistrationType; HasPrefab = data.hasPrefab;
            PreviewState = ParsePreviewState(data.previewState); PreviewRelativePath = data.previewRelativePath;
        }
        public ModGdoProvider Provider { get; }
        public int RuntimeId { get; }
        public string DisplayName { get; }
        public string RuntimeType { get; }
        public string SourceAssembly { get; }
        public string CustomRegistrationType { get; }
        public bool HasPrefab { get; }
        public ModGdoPreviewState PreviewState { get; }
        public string PreviewRelativePath { get; }

        private static ModGdoPreviewState ParsePreviewState(string value)
        {
            if (Enum.TryParse(value, true, out ModGdoPreviewState state)) return state;
            return ModGdoPreviewState.Missing;
        }
    }

    internal sealed class ModGdoCatalogRepository
    {
        private readonly string _path;
        private readonly string _expectedCaptureToken;
        private readonly string _expectedSteamBuildId;
        private ModGdoCatalogEntry[] _entries = new ModGdoCatalogEntry[0];
        private Dictionary<string, ModGdoCatalogEntry> _byIdentity = new Dictionary<string, ModGdoCatalogEntry>();

        public ModGdoCatalogRepository(string path, string expectedCaptureToken = null, string expectedSteamBuildId = null)
        {
            _path = path; _expectedCaptureToken = expectedCaptureToken; _expectedSteamBuildId = expectedSteamBuildId;
            Status = ModGdoCatalogState.Missing;
        }

        public ModGdoCatalogState Status { get; private set; }
        public string StatusMessage { get; private set; }
        public IReadOnlyList<ModGdoCatalogEntry> Entries => _entries;
        public event Action Changed;

        public bool Reload()
        {
            if (!File.Exists(_path)) return Fail(ModGdoCatalogState.Missing, "The mod GDO catalogue is missing.");
            try
            {
                ModGdoCatalogData data = ModGdoCatalogFile.Read(_path);
                if (!string.IsNullOrEmpty(_expectedCaptureToken) && data.captureToken != _expectedCaptureToken)
                    return Fail(ModGdoCatalogState.StaleCapture, "The mod GDO catalogue belongs to another capture request.");
                if (!string.IsNullOrEmpty(_expectedSteamBuildId) && data.steamBuildId != _expectedSteamBuildId)
                    return Fail(ModGdoCatalogState.StaleBuild, "The mod GDO catalogue belongs to another PlateUp build.");
                if (data.entries.GroupBy(entry => entry.runtimeId).Any(group => group.Count() > 1))
                    return Fail(ModGdoCatalogState.DuplicateRuntimeId, "Multiple captured GDOs use the same runtime ID.");

                Dictionary<string, ModGdoProvider> providers = data.providers.ToDictionary(item => item.key, FromData, StringComparer.Ordinal);
                _entries = data.entries.Select(item => new ModGdoCatalogEntry(item, ResolveProvider(item, providers))).ToArray();
                _byIdentity = _entries.ToDictionary(item => Identity(item.Provider.Key, item.RuntimeId, item.RuntimeType), StringComparer.Ordinal);
                Status = ModGdoCatalogState.Ready;
                StatusMessage = "Mod GDO catalogue is ready.";
                Changed?.Invoke();
                return true;
            }
            catch (InvalidDataException exception)
            {
                return Fail(exception.Message.Contains("incompatible") ? ModGdoCatalogState.IncompatibleSchema : ModGdoCatalogState.Malformed, exception.Message);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is IOException || exception is UnauthorizedAccessException)
            {
                return Fail(ModGdoCatalogState.Malformed, exception.Message);
            }
        }

        public ModGdoCatalogEntry Find(string providerKey, int runtimeId, string runtimeType)
        {
            _byIdentity.TryGetValue(Identity(providerKey, runtimeId, runtimeType), out ModGdoCatalogEntry entry);
            return entry;
        }

        public IReadOnlyList<ModGdoCatalogEntry> Search(string query, string expectedType = null, string exactRuntimeType = null)
        {
            string value = query ?? string.Empty;
            return _entries
                .Where(item => string.IsNullOrEmpty(expectedType) || IsAssignable(item.RuntimeType, expectedType))
                .Where(item => string.IsNullOrEmpty(exactRuntimeType) || item.RuntimeType == exactRuntimeType)
                .Where(item => item.DisplayName.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    item.RuntimeType.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    item.Provider.DisplayName.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    item.RuntimeId.ToString().IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.RuntimeId).ToArray();
        }

        private bool Fail(ModGdoCatalogState state, string message)
        {
            _entries = new ModGdoCatalogEntry[0]; _byIdentity.Clear(); Status = state; StatusMessage = message;
            Changed?.Invoke(); return false;
        }

        private static ModGdoProvider ResolveProvider(ModGdoEntryData entry, IDictionary<string, ModGdoProvider> providers)
        {
            if (!string.IsNullOrEmpty(entry.providerKey) && providers.TryGetValue(entry.providerKey, out ModGdoProvider provider)) return provider;
            string assembly = string.IsNullOrWhiteSpace(entry.sourceAssembly) ? "Unknown" : entry.sourceAssembly;
            return new ModGdoProvider("assembly:" + assembly, assembly, null, null, assembly, ModGdoProviderProvenance.Assembly);
        }

        private static ModGdoProvider FromData(ModGdoProviderData data)
        {
            ModGdoProviderProvenance provenance;
            if (!Enum.TryParse(data.provenance, true, out provenance)) provenance =
                string.IsNullOrEmpty(data.workshopId) ? ModGdoProviderProvenance.Unknown : ModGdoProviderProvenance.Workshop;
            return new ModGdoProvider(data.key, data.displayName, data.version, data.workshopId, data.sourceAssembly, provenance);
        }

        private static bool IsAssignable(string runtimeType, string expectedType)
        {
            if (runtimeType == expectedType) return true;
            return GdoDescriptorCatalog.ForRuntimeType(runtimeType) != null && GdoDescriptorCatalog.IsAssignable(runtimeType, expectedType);
        }

        private static string Identity(string providerKey, int runtimeId, string runtimeType)
            => (providerKey ?? string.Empty) + "|" + runtimeId + "|" + (runtimeType ?? string.Empty);
    }
}
