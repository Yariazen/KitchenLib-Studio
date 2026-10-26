using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace KitchenLib.Studio.Editor
{
    public sealed class VanillaGdoCatalogRepository
    {
        private readonly string _path;
        private Dictionary<int, VanillaGdoCatalogEntry> _byId = new Dictionary<int, VanillaGdoCatalogEntry>();
        private VanillaGdoCatalog _catalog;
        private VanillaGdoCatalogStatus _status;
        private FileStamp _stamp;
        private static readonly Lazy<VanillaGdoCatalogRepository> Shared = new Lazy<VanillaGdoCatalogRepository>(CreateCurrent);

        public VanillaGdoCatalogRepository(string path) { _path = path; _status = new VanillaGdoCatalogStatus(VanillaGdoCatalogState.Missing, "Catalog has not been loaded."); _stamp = FileStamp.Read(path); }
        public static VanillaGdoCatalogRepository Current => Shared.Value;
        public VanillaGdoCatalog Catalog { get { RefreshIfChanged(); return _catalog; } }
        public VanillaGdoCatalogStatus Status { get { RefreshIfChanged(); return _status; } }
        public event Action Changed;

        public bool Reload()
        {
            bool loaded = ReloadCore();
            _stamp = FileStamp.Read(_path);
            Changed?.Invoke();
            return loaded;
        }

        public bool RefreshIfChanged()
        {
            FileStamp current = FileStamp.Read(_path);
            if (_stamp.Equals(current)) return false;
            Reload();
            return true;
        }

        private bool ReloadCore()
        {
            if (!File.Exists(_path)) return Fail(VanillaGdoCatalogState.Missing, "The vanilla GDO catalog is missing.");
            try
            {
                VanillaGdoCatalogData data = VanillaGdoCatalogFile.Read(_path);
                if (!string.Equals(data.schemaFingerprint, GdoDescriptorCatalog.GlobalFingerprint, StringComparison.Ordinal))
                    return Fail(VanillaGdoCatalogState.DescriptorMismatch, "The vanilla catalog was generated for different GDO descriptors.");
                if (data.entries.Any(item => item == null || string.IsNullOrEmpty(item.name) || string.IsNullOrEmpty(item.runtimeType)) || data.entries.GroupBy(item => item.id).Any(group => group.Count() > 1))
                    return Fail(VanillaGdoCatalogState.Malformed, "Catalog entries are incomplete or contain duplicate IDs.");
                if (data.entries.Any(item => !string.Equals(item.assemblyName, "Kitchen.GameData", StringComparison.Ordinal) ||
                        GdoDescriptorCatalog.ForRuntimeType(item.runtimeType) == null))
                    return Fail(VanillaGdoCatalogState.DescriptorMismatch, "The vanilla catalog contains unavailable GDO runtime types.");
                _catalog = new VanillaGdoCatalog(data);
                _byId = _catalog.Entries.ToDictionary(item => item.Id);
                _status = new VanillaGdoCatalogStatus(VanillaGdoCatalogState.Ready, "Vanilla GDO catalog is ready.");
                return true;
            }
            catch (InvalidDataException exception) when (exception.InnerException != null)
            { return Fail(VanillaGdoCatalogState.Malformed, exception.Message); }
            catch (InvalidDataException exception) { return Fail(VanillaGdoCatalogState.IncompatibleSchema, exception.Message); }
            catch (Exception exception) when (exception is ArgumentException || exception is IOException || exception is UnauthorizedAccessException)
            { return Fail(VanillaGdoCatalogState.Malformed, exception.Message); }
        }

        public VanillaGdoCatalogEntry FindById(int id) { RefreshIfChanged(); _byId.TryGetValue(id, out VanillaGdoCatalogEntry entry); return entry; }

        public IReadOnlyList<VanillaGdoCatalogEntry> Search(string query, string expectedType = null,
            string exactRuntimeType = null)
        {
            string value = query ?? string.Empty;
            RefreshIfChanged();
            if (_catalog == null) return new VanillaGdoCatalogEntry[0];
            return _catalog.Entries.Where(item => string.IsNullOrEmpty(expectedType) || GdoDescriptorCatalog.IsAssignable(item.RuntimeType, expectedType))
                .Where(item => string.IsNullOrEmpty(exactRuntimeType) ||
                    string.Equals(item.RuntimeType, exactRuntimeType, StringComparison.Ordinal))
                .Where(item => item.Name.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0 || item.RuntimeType.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0 || item.Id.ToString().IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.Id).ToArray();
        }

        public IReadOnlyList<string> RuntimeTypes(string expectedType = null)
        {
            RefreshIfChanged();
            if (_catalog == null) return new string[0];
            return _catalog.Entries
                .Where(item => string.IsNullOrEmpty(expectedType) ||
                    GdoDescriptorCatalog.IsAssignable(item.RuntimeType, expectedType))
                .Select(item => item.RuntimeType)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public void AssessInstallation(PlateUpInstallation installation, bool savedInstallationConfigured)
        {
            if (savedInstallationConfigured && installation == null)
                _status = new VanillaGdoCatalogStatus(VanillaGdoCatalogState.SavedInstallUnavailable, "The saved PlateUp installation is unavailable.");
            else if (_catalog != null && installation != null && !string.IsNullOrEmpty(_catalog.Data.steamBuildId) &&
                     !string.IsNullOrEmpty(installation.BuildId) && _catalog.Data.steamBuildId != installation.BuildId)
                _status = new VanillaGdoCatalogStatus(VanillaGdoCatalogState.StaleBuild, "The vanilla catalog was generated from a different PlateUp build.");
            else if (_catalog != null)
                _status = new VanillaGdoCatalogStatus(VanillaGdoCatalogState.Ready, "Vanilla GDO catalog is ready.");
            Changed?.Invoke();
        }

        private bool Fail(VanillaGdoCatalogState state, string message)
        {
            _catalog = null;
            _byId.Clear();
            _status = new VanillaGdoCatalogStatus(state, message);
            return false;
        }
        private static VanillaGdoCatalogRepository CreateCurrent()
        {
            var repository = new VanillaGdoCatalogRepository(KitchenLibStudioGeneratedPaths.VanillaGdoCatalogPath);
            PlateUpCatalogImportController.CatalogChanged += () => repository.Reload();
            repository.Reload();
            PlateUpCatalogImportMenu.AssessSavedInstallation(repository);
            return repository;
        }

        private readonly struct FileStamp : IEquatable<FileStamp>
        {
            private readonly bool _exists;
            private readonly long _length;
            private readonly long _lastWriteTicks;
            private FileStamp(bool exists, long length, long lastWriteTicks) { _exists = exists; _length = length; _lastWriteTicks = lastWriteTicks; }
            public static FileStamp Read(string path)
            {
                if (!File.Exists(path)) return new FileStamp(false, 0, 0);
                var info = new FileInfo(path);
                return new FileStamp(true, info.Length, info.LastWriteTimeUtc.Ticks);
            }
            public bool Equals(FileStamp other) => _exists == other._exists && _length == other._length && _lastWriteTicks == other._lastWriteTicks;
        }
    }

    internal sealed class VanillaGdoCatalogAssetPostprocessor : AssetPostprocessor
    {
        private static readonly string CatalogAssetPath = KitchenLibStudioGeneratedPaths.GeneratedAssetPath(
            KitchenLibStudioGeneratedPaths.VanillaGdoCatalogFileName);

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (!ContainsCatalog(importedAssets) && !ContainsCatalog(deletedAssets) &&
                !ContainsCatalog(movedAssets) && !ContainsCatalog(movedFromAssetPaths)) return;
            VanillaGdoCatalogRepository repository = VanillaGdoCatalogRepository.Current;
            if (repository.RefreshIfChanged()) PlateUpCatalogImportMenu.AssessSavedInstallation(repository);
        }

        private static bool ContainsCatalog(IEnumerable<string> paths) =>
            paths != null && paths.Any(path => string.Equals(path, CatalogAssetPath, StringComparison.OrdinalIgnoreCase));
    }
}
