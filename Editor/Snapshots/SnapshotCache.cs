using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace KitchenLib.Studio.Editor.Snapshots
{
    [Serializable]
    internal sealed class SnapshotCacheMetadata
    {
        public int width;
        public int height;
        public string subjectIdentity;
        public string sourceFingerprint;
        public string rendererVersion;
        public string[] diagnostics = Array.Empty<string>();
    }

    internal sealed class SnapshotCacheEntry
    {
        public SnapshotCacheEntry(string pngPath, byte[] pngBytes, SnapshotCacheMetadata metadata)
        { PngPath = pngPath; PngBytes = pngBytes; Metadata = metadata; }
        public string PngPath { get; }
        public byte[] PngBytes { get; }
        public SnapshotCacheMetadata Metadata { get; }
    }

    internal sealed class SnapshotCache
    {
        private static readonly byte[] PngSignature = { 137, 80, 78, 71, 13, 10, 26, 10 };
        private readonly string _root;
        public SnapshotCache(string root = null)
        {
            _root = string.IsNullOrEmpty(root)
                ? Bootstrap.KitchenLibStudioGeneratedPaths.SnapshotCacheRoot
                : Path.GetFullPath(root);
        }
        public event Action Changed;
        public string Root => _root;
        public string PngPath(string key) => Path.Combine(_root, ValidateKey(key) + ".png");
        public string MetadataPath(string key) => Path.Combine(_root, ValidateKey(key) + ".json");

        public static string BuildKey(string subjectIdentity, string sourceFingerprint, string rendererVersion,
            SnapshotRenderOptions options, string settingsFingerprint)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            string value = string.Join("\n", subjectIdentity ?? string.Empty, sourceFingerprint ?? string.Empty,
                rendererVersion ?? string.Empty, options.Width.ToString(), options.Height.ToString(), settingsFingerprint ?? string.Empty);
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", string.Empty).ToLowerInvariant();
        }

        public void Put(string key, byte[] pngBytes, SnapshotCacheMetadata metadata,
            Action<string, string> replace = null)
        {
            if (!IsPng(pngBytes)) throw new InvalidDataException("Snapshot cache payload is not a PNG.");
            Directory.CreateDirectory(_root); string png = PngPath(key); string json = MetadataPath(key);
            string token = Guid.NewGuid().ToString("N"); string tempPng = png + "." + token + ".tmp"; string tempJson = json + "." + token + ".tmp";
            try
            {
                File.WriteAllBytes(tempPng, pngBytes);
                File.WriteAllText(tempJson, JsonUtility.ToJson(metadata ?? new SnapshotCacheMetadata(), false) + "\n", new UTF8Encoding(false));
                Action<string, string> publish = replace ?? Replace;
                publish(tempPng, png); publish(tempJson, json); Changed?.Invoke();
            }
            finally { DeleteIfPresent(tempPng); DeleteIfPresent(tempJson); }
        }

        public SnapshotCacheEntry TryGet(string key)
        {
            string png = PngPath(key); string json = MetadataPath(key);
            if (!File.Exists(png) || !File.Exists(json)) return null;
            try
            {
                byte[] bytes = File.ReadAllBytes(png); if (!IsPng(bytes)) return null;
                SnapshotCacheMetadata metadata = JsonUtility.FromJson<SnapshotCacheMetadata>(File.ReadAllText(json));
                return metadata == null ? null : new SnapshotCacheEntry(png, bytes, metadata);
            }
            catch (IOException) { return null; }
            catch (ArgumentException) { return null; }
        }

        public int Cleanup(int maximumEntries)
        {
            if (maximumEntries < 0) throw new ArgumentOutOfRangeException(nameof(maximumEntries));
            if (!Directory.Exists(_root)) return 0;
            FileInfo[] pngs = new DirectoryInfo(_root).GetFiles("*.png").OrderByDescending(value => value.LastWriteTimeUtc).ThenBy(value => value.Name, StringComparer.Ordinal).ToArray();
            int removed = 0;
            foreach (FileInfo png in pngs.Skip(maximumEntries))
            {
                DeleteIfPresent(png.FullName); DeleteIfPresent(Path.ChangeExtension(png.FullName, ".json")); removed++;
            }
            return removed;
        }

        private static string ValidateKey(string key)
        {
            if (string.IsNullOrEmpty(key) || key.Length != 64 || key.Any(value => !Uri.IsHexDigit(value))) throw new ArgumentException("Snapshot cache key must be a SHA-256 hex value.", nameof(key));
            return key.ToLowerInvariant();
        }
        internal static bool IsPng(byte[] bytes) => bytes != null && bytes.Length >= PngSignature.Length && !PngSignature.Where((value, index) => bytes[index] != value).Any();
        private static void Replace(string source, string destination)
        {
            if (File.Exists(destination)) File.Replace(source, destination, null); else File.Move(source, destination);
        }
        private static void DeleteIfPresent(string path) { if (File.Exists(path)) File.Delete(path); }
    }
}
