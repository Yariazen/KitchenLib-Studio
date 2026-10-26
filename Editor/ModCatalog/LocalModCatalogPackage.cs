using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using UnityEngine;

namespace KitchenLib.Studio.Editor
{
    public sealed class LocalModCatalogImportResult
    {
        public string ProviderKey { get; }
        public string RootPath { get; }
        public string CatalogPath { get; }

        internal LocalModCatalogImportResult(string providerKey, string rootPath, string catalogPath)
        { ProviderKey = providerKey; RootPath = rootPath; CatalogPath = catalogPath; }

        public string GetAbsolutePath(string relativePath) => LocalModCatalogPackage.ResolveContainedPath(RootPath, relativePath);
    }

    public static class LocalModCatalogPackage
    {
        public const int SchemaVersion = 1;
        public const string PackageKind = "kitchenlib-studio-local-mod-catalog";
        public const string ManifestPath = "manifest.json";
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false, true);
        public static event Action Imported;

        public static void Write(string packagePath, LocalModCatalogPackageManifestData manifest,
            ModGdoCatalogData catalog, IReadOnlyDictionary<string, byte[]> payloads)
        {
            ValidateManifest(manifest);
            ModGdoCatalogFile.Validate(catalog);
            if (payloads == null) throw new ArgumentNullException(nameof(payloads));
            string fullPath = Path.GetFullPath(packagePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            using (FileStream stream = File.Create(fullPath))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                WriteEntry(archive, ManifestPath, Utf8NoBom.GetBytes(JsonUtility.ToJson(manifest, true) + "\n"));
                WriteEntry(archive, manifest.catalogPath, ModGdoCatalogFile.Serialize(catalog));
                foreach (KeyValuePair<string, byte[]> payload in payloads.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                    WriteEntry(archive, payload.Key, payload.Value);
            }
        }

        public static LocalModCatalogImportResult Import(string packagePath, string cacheRoot)
        {
            string root = ValidateCacheRoot(cacheRoot);
            string staging = Path.Combine(root, ".import-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            try
            {
                using (FileStream packageStream = File.OpenRead(packagePath))
                using (var archive = new ZipArchive(packageStream, ZipArchiveMode.Read))
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        string destination = ResolveContainedPath(staging, entry.FullName);
                        if (string.IsNullOrEmpty(entry.Name)) { Directory.CreateDirectory(destination); continue; }
                        Directory.CreateDirectory(Path.GetDirectoryName(destination));
                        using (Stream source = entry.Open())
                        using (FileStream target = File.Create(destination)) source.CopyTo(target);
                    }

                LocalModCatalogPackageManifestData manifest = ReadManifest(Path.Combine(staging, ManifestPath));
                ModGdoCatalogData catalog = ModGdoCatalogFile.Read(ResolveContainedPath(staging, manifest.catalogPath));
                ValidatePackageFiles(staging, manifest, catalog);
                if (string.Equals(manifest.workshopId, IngredientLibImportRequirements.WorkshopId, StringComparison.Ordinal))
                    IngredientLibImportRequirements.Validate(manifest, catalog);

                string destinationRoot = Path.Combine(root, SafeDirectoryName(manifest.providerKey));
                ReplaceDirectory(staging, destinationRoot);
                staging = null;
                Imported?.Invoke();
                return new LocalModCatalogImportResult(manifest.providerKey, destinationRoot,
                    ResolveContainedPath(destinationRoot, manifest.catalogPath));
            }
            finally
            {
                if (!string.IsNullOrEmpty(staging) && Directory.Exists(staging)) Directory.Delete(staging, true);
            }
        }

        public static string DefaultCacheRoot(string projectRoot)
            => Path.Combine(Path.GetFullPath(projectRoot), "Library", "KitchenLibStudio", "ImportedModCatalogs");

        internal static string ResolveContainedPath(string root, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
                throw new InvalidDataException("Package paths must be relative.");
            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string fullPath = Path.GetFullPath(Path.Combine(fullRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("A package entry escapes the import root.");
            return fullPath;
        }

        private static void ValidatePackageFiles(string root, LocalModCatalogPackageManifestData manifest, ModGdoCatalogData catalog)
        {
            ModGdoProviderData provider = catalog.providers.SingleOrDefault(item => item != null &&
                string.Equals(item.key, manifest.providerKey, StringComparison.Ordinal));
            if (provider == null || catalog.providers.Length != 1 ||
                !string.Equals(provider.workshopId ?? string.Empty, manifest.workshopId ?? string.Empty, StringComparison.Ordinal) ||
                !string.Equals(provider.sourceAssembly, manifest.sourceAssembly, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(catalog.steamBuildId, manifest.plateUpBuildId, StringComparison.Ordinal) ||
                catalog.entries.Any(entry => !string.Equals(entry.providerKey, manifest.providerKey, StringComparison.Ordinal)))
                throw new InvalidDataException("The catalogue does not match the package's selected provider.");

            foreach (LocalModCatalogVisualData visual in manifest.visuals)
            {
                string prefabPath = ResolveContainedPath(root, visual.prefabPath);
                string snapshotPath = ResolveContainedPath(root, visual.snapshotPath);
                if (!File.Exists(prefabPath) || !File.Exists(snapshotPath))
                    throw new InvalidDataException("A declared baked visual is missing from the package.");
                BakedPrefabData prefab = BakedPrefabFile.Read(prefabPath);
                if (prefab.runtimeId != visual.runtimeId)
                    throw new InvalidDataException("A baked visual does not match its declared runtime ID.");
            }
        }

        private static LocalModCatalogPackageManifestData ReadManifest(string path)
        {
            if (!File.Exists(path)) throw new InvalidDataException("The package has no manifest.");
            LocalModCatalogPackageManifestData manifest;
            try { manifest = JsonUtility.FromJson<LocalModCatalogPackageManifestData>(Utf8NoBom.GetString(File.ReadAllBytes(path))); }
            catch (Exception exception) { throw new InvalidDataException("The package manifest is invalid.", exception); }
            ValidateManifest(manifest);
            return manifest;
        }

        private static void ValidateManifest(LocalModCatalogPackageManifestData manifest)
        {
            if (manifest == null || manifest.schemaVersion != SchemaVersion || manifest.packageKind != PackageKind ||
                !manifest.localOnly || string.IsNullOrWhiteSpace(manifest.providerKey) ||
                string.IsNullOrWhiteSpace(manifest.providerName) || string.IsNullOrWhiteSpace(manifest.modVersion) ||
                string.IsNullOrWhiteSpace(manifest.kitchenLibVersion) || string.IsNullOrWhiteSpace(manifest.plateUpBuildId) ||
                string.IsNullOrWhiteSpace(manifest.sourceAssembly) || !IsSha256(manifest.sourceAssemblySha256) ||
                !IsOptionalSha256(manifest.sourceAssetBundleSha256) || string.IsNullOrWhiteSpace(manifest.catalogPath) ||
                manifest.visuals == null)
                throw new InvalidDataException("The local mod catalogue manifest is incomplete or incompatible.");
            ResolveContainedPath(Path.GetTempPath(), manifest.catalogPath);
            foreach (LocalModCatalogVisualData visual in manifest.visuals)
            {
                if (visual == null || visual.runtimeId == 0) throw new InvalidDataException("The manifest contains an invalid visual.");
                ResolveContainedPath(Path.GetTempPath(), visual.prefabPath);
                ResolveContainedPath(Path.GetTempPath(), visual.snapshotPath);
            }
        }

        private static string ValidateCacheRoot(string cacheRoot)
        {
            if (string.IsNullOrWhiteSpace(cacheRoot)) throw new ArgumentException("A cache root is required.", nameof(cacheRoot));
            string full = Path.GetFullPath(cacheRoot);
            if (full.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(part => string.Equals(part, "Assets", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Imported mod catalogues must be stored outside Unity's Assets folder.");
            Directory.CreateDirectory(full);
            return full;
        }

        private static void WriteEntry(ZipArchive archive, string relativePath, byte[] content)
        {
            ResolveContainedPath(Path.GetTempPath(), relativePath);
            if (content == null) throw new InvalidDataException("Package payloads cannot be null.");
            ZipArchiveEntry entry = archive.CreateEntry(relativePath.Replace('\\', '/'), System.IO.Compression.CompressionLevel.Optimal);
            using (Stream stream = entry.Open()) stream.Write(content, 0, content.Length);
        }

        private static bool IsSha256(string value)
            => value != null && value.Length == 64 && value.All(character => Uri.IsHexDigit(character));

        private static bool IsOptionalSha256(string value) => string.IsNullOrEmpty(value) || IsSha256(value);

        private static string SafeDirectoryName(string providerKey)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            return new string(providerKey.Select(character => invalid.Contains(character) || character == ':' ? '_' : character).ToArray());
        }

        private static void ReplaceDirectory(string staging, string destination)
        {
            string backup = destination + ".previous";
            if (Directory.Exists(backup)) Directory.Delete(backup, true);
            if (Directory.Exists(destination)) Directory.Move(destination, backup);
            try
            {
                Directory.Move(staging, destination);
                if (Directory.Exists(backup)) Directory.Delete(backup, true);
            }
            catch
            {
                if (Directory.Exists(destination)) Directory.Delete(destination, true);
                if (Directory.Exists(backup)) Directory.Move(backup, destination);
                throw;
            }
        }
    }
}
