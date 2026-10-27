using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEngine;

using KitchenLib.Studio.Editor.Snapshots;

namespace KitchenLib.Studio.Editor.Catalogs.Vanilla
{
    internal sealed class VanillaCatalogPublicationPaths
    {
        public VanillaCatalogPublicationPaths(string gdoCatalog, string snapshotCatalog, string snapshotIndex,
            string cacheRoot, string stagingRoot)
        {
            GdoCatalog = Path.GetFullPath(gdoCatalog);
            SnapshotCatalog = Path.GetFullPath(snapshotCatalog);
            SnapshotIndex = Path.GetFullPath(snapshotIndex);
            CacheRoot = Path.GetFullPath(cacheRoot);
            StagingRoot = Path.GetFullPath(stagingRoot);
        }

        public string GdoCatalog { get; }
        public string SnapshotCatalog { get; }
        public string SnapshotIndex { get; }
        public string CacheRoot { get; }
        public string StagingRoot { get; }
    }

    internal static class VanillaCatalogPublication
    {
        public static void Publish(VanillaCatalogGeneration generation, VanillaCatalogPublicationPaths paths,
            CancellationToken token, Action<string, string> replace = null, Action<string> cleanup = null)
        {
            if (generation == null) throw new ArgumentNullException(nameof(generation));
            if (paths == null) throw new ArgumentNullException(nameof(paths));
            replace = replace ?? ReplaceFile;
            cleanup = cleanup ?? (path => Directory.Delete(path, true));
            string operation = Path.Combine(paths.StagingRoot, "catalog-" + Guid.NewGuid().ToString("N"));
            string stage = Path.Combine(operation, "stage");
            string backup = Path.Combine(operation, "backup");
            string stageCache = Path.Combine(stage, "Snapshots");
            try
            {
                Directory.CreateDirectory(stageCache);
                Directory.CreateDirectory(backup);
                string stagedGdo = Path.Combine(stage, "gdo.json");
                string stagedSnapshots = Path.Combine(stage, "snapshots.json");
                string stagedIndex = Path.Combine(stage, "index.json");
                File.WriteAllBytes(stagedGdo, VanillaGdoCatalogFile.Serialize(generation.Gdos));
                File.WriteAllBytes(stagedSnapshots, VanillaAssetCatalogFile.Serialize(generation.Snapshots));
                File.WriteAllBytes(stagedIndex, ExternalGdoSnapshotIndexFile.Serialize(generation.Index));
                foreach (VanillaSnapshotPng png in generation.Pngs.Where(value => value.IsRendered))
                {
                    token.ThrowIfCancellationRequested();
                    if (!SnapshotCache.IsPng(png.PngBytes))
                        throw new InvalidDataException("Staged snapshot is not a PNG: " + png.CacheKey);
                    File.WriteAllBytes(Path.Combine(stageCache, png.CacheKey + ".png"), png.PngBytes);
                    File.WriteAllText(Path.Combine(stageCache, png.CacheKey + ".json"),
                        JsonUtility.ToJson(png.Metadata, false) + "\n", new UTF8Encoding(false));
                }
                ValidateStage(generation, stagedGdo, stagedSnapshots, stagedIndex, stageCache);
                token.ThrowIfCancellationRequested();

                BackupFile(paths.GdoCatalog, Path.Combine(backup, "gdo.json"));
                BackupFile(paths.SnapshotCatalog, Path.Combine(backup, "snapshots.json"));
                BackupFile(paths.SnapshotIndex, Path.Combine(backup, "index.json"));
                string backupCache = Path.Combine(backup, "Snapshots");
                if (Directory.Exists(paths.CacheRoot)) CopyDirectory(paths.CacheRoot, backupCache);
                try
                {
                    token.ThrowIfCancellationRequested();
                    replace(stagedGdo, paths.GdoCatalog);
                    token.ThrowIfCancellationRequested();
                    replace(stagedSnapshots, paths.SnapshotCatalog);
                    token.ThrowIfCancellationRequested();
                    replace(stagedIndex, paths.SnapshotIndex);
                    token.ThrowIfCancellationRequested();
                    if (Directory.Exists(paths.CacheRoot)) Directory.Delete(paths.CacheRoot, true);
                    Directory.CreateDirectory(Path.GetDirectoryName(paths.CacheRoot));
                    Directory.Move(stageCache, paths.CacheRoot);
                }
                catch
                {
                    RestoreFile(paths.GdoCatalog, Path.Combine(backup, "gdo.json"));
                    RestoreFile(paths.SnapshotCatalog, Path.Combine(backup, "snapshots.json"));
                    RestoreFile(paths.SnapshotIndex, Path.Combine(backup, "index.json"));
                    if (Directory.Exists(paths.CacheRoot)) Directory.Delete(paths.CacheRoot, true);
                    if (Directory.Exists(backupCache)) CopyDirectory(backupCache, paths.CacheRoot);
                    throw;
                }
            }
            finally
            {
                if (Directory.Exists(operation))
                {
                    try { cleanup(operation); }
                    catch (Exception exception)
                    {
                        Debug.LogWarning("Vanilla catalogue publication succeeded or rolled back, but its staging " +
                            "directory could not be removed: " + exception.Message);
                    }
                }
            }
        }

        private static void ValidateStage(VanillaCatalogGeneration generation, string gdo, string snapshots,
            string index, string cacheRoot)
        {
            VanillaGdoCatalogFile.Read(gdo);
            VanillaAssetCatalogFile.Read(snapshots);
            ExternalGdoSnapshotIndexData roundTrip = ExternalGdoSnapshotIndexFile.Read(index);
            if (roundTrip?.entries == null) throw new InvalidDataException("Snapshot index is incomplete.");
            VanillaSnapshotPng[] rendered = generation.Pngs.Where(value => value.IsRendered).ToArray();
            var renderedById = new System.Collections.Generic.Dictionary<int, VanillaSnapshotPng>();
            foreach (VanillaSnapshotPng png in rendered)
            {
                if (string.IsNullOrEmpty(png.GameObjectKey) || png.GdoIds.Count == 0)
                    throw new InvalidDataException("Rendered snapshot has no catalogue identity: " + png.CacheKey);
                foreach (int gdoId in png.GdoIds)
                    if (gdoId == 0 || renderedById.ContainsKey(gdoId))
                        throw new InvalidDataException("Rendered snapshot contains a duplicate or invalid GDO ID: " + gdoId);
                    else renderedById.Add(gdoId, png);
                var cache = new SnapshotCache(cacheRoot);
                if (cache.TryGet(png.CacheKey) == null)
                    throw new InvalidDataException("Staged snapshot cache pair is incomplete: " + png.CacheKey);
            }
            var indexedIds = new System.Collections.Generic.HashSet<int>();
            foreach (ExternalGdoSnapshotIndexEntry entry in roundTrip.entries)
            {
                if (entry == null || entry.gdoId == 0 || !indexedIds.Add(entry.gdoId))
                    throw new InvalidDataException("Snapshot index contains a duplicate or invalid GDO ID.");
                if (!renderedById.TryGetValue(entry.gdoId, out VanillaSnapshotPng png) ||
                    !string.Equals(entry.gameObjectKey, png.GameObjectKey, StringComparison.Ordinal) ||
                    !string.Equals(ExternalGdoSnapshotCacheIdentity.BuildKey(
                        entry.gameObjectKey, entry.sourceFingerprint), png.CacheKey, StringComparison.Ordinal))
                    throw new InvalidDataException("Snapshot index does not match the rendered cache for GDO ID " +
                        entry.gdoId + ".");
            }
            if (indexedIds.Count != renderedById.Count)
                throw new InvalidDataException("Snapshot index does not cover every rendered GDO ID.");
        }

        private static void BackupFile(string source, string backup)
        {
            if (File.Exists(source)) { Directory.CreateDirectory(Path.GetDirectoryName(backup)); File.Copy(source, backup, true); }
        }

        private static void RestoreFile(string destination, string backup)
        {
            if (File.Exists(backup)) { Directory.CreateDirectory(Path.GetDirectoryName(destination)); File.Copy(backup, destination, true); }
            else if (File.Exists(destination)) File.Delete(destination);
        }

        private static void ReplaceFile(string source, string destination)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            if (File.Exists(destination)) File.Replace(source, destination, null); else File.Move(source, destination);
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (string file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
            foreach (string directory in Directory.GetDirectories(source))
                CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }
    }
}
