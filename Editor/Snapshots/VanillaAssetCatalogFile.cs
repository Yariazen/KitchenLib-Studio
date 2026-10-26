using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace KitchenLib.Studio.Editor.Snapshots
{
    internal static class VanillaAssetCatalogFile
    {
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false, true);
        public static byte[] Serialize(VanillaAssetCatalogData data)
        {
            Normalize(data);
            Validate(data);
            return Utf8NoBom.GetBytes(JsonUtility.ToJson(data, true) + "\n");
        }
        public static VanillaAssetCatalogData Read(string path)
        { VanillaAssetCatalogData data = Deserialize(File.ReadAllBytes(path)); Normalize(data); Validate(data); return data; }
        public static void PublishAtomic(string destination, byte[] content) => PublishAtomic(destination, content, Replace);
        public static void PublishAtomic(string destination, byte[] content, Action<string, string> replacement)
        {
            if (string.IsNullOrEmpty(destination)) throw new ArgumentException("Destination is required.", nameof(destination));
            if (content == null) throw new ArgumentNullException(nameof(content));
            Validate(Deserialize(content));
            string directory = Path.GetDirectoryName(Path.GetFullPath(destination)); Directory.CreateDirectory(directory);
            string temporary = Path.Combine(directory, Path.GetFileName(destination) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            try { File.WriteAllBytes(temporary, content); Validate(Deserialize(File.ReadAllBytes(temporary))); replacement(temporary, destination); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        public static void Validate(VanillaAssetCatalogData data)
        {
            if (data == null || data.schemaVersion != 2 || data.providerId != "plateup" || data.steamAppId != 1599600 ||
                string.IsNullOrEmpty(data.sourceFingerprint) || data.sources == null || data.records == null || data.edges == null || data.diagnostics == null)
                throw new InvalidDataException("The vanilla asset catalogue is incomplete or incompatible.");
            if (data.records.Any(item => item == null || string.IsNullOrEmpty(item.key) || item.source == null)) throw new InvalidDataException("Asset record is incomplete.");
            if (data.records.GroupBy(item => item.key, StringComparer.Ordinal).Any(group => group.Count() != 1)) throw new InvalidDataException("Asset keys must be unique.");
            if (data.records.GroupBy(item => (item.source.relativePath ?? string.Empty) + "\n" + item.source.pathId).Any(group => group.Count() != 1)) throw new InvalidDataException("Source identities must be unique.");
            var keys = new HashSet<string>(data.records.Select(item => item.key), StringComparer.Ordinal);
            if (data.edges.Any(edge => edge == null || !keys.Contains(edge.sourceKey) || !keys.Contains(edge.targetKey))) throw new InvalidDataException("Asset edge target is unresolved.");
        }
        private static VanillaAssetCatalogData Deserialize(byte[] bytes)
        { try { return JsonUtility.FromJson<VanillaAssetCatalogData>(Utf8NoBom.GetString(bytes)); } catch (Exception exception) { throw new InvalidDataException("Asset catalogue JSON is invalid.", exception); } }
        private static void Normalize(VanillaAssetCatalogData data)
        {
            if (data == null) return;
            if (data.sources != null)
            {
                data.sources = data.sources.OrderBy(item => item.relativePath, StringComparer.Ordinal).ToArray();
                data.sourceFingerprint = PlateUpSourceFingerprint.ComputeStrings(
                    data.sources.Select(item => item.sha256));
            }
            if (data.records != null)
                data.records = data.records.OrderBy(item => item.key, StringComparer.Ordinal).ToArray();
            if (data.edges != null)
                data.edges = data.edges.OrderBy(item => item.sourceKey, StringComparer.Ordinal)
                    .ThenBy(item => item.relation, StringComparer.Ordinal).ThenBy(item => item.slot, StringComparer.Ordinal)
                    .ThenBy(item => item.targetKey, StringComparer.Ordinal).ToArray();
            if (data.diagnostics != null)
                data.diagnostics = data.diagnostics.OrderBy(item => item.key ?? string.Empty, StringComparer.Ordinal)
                    .ThenBy(item => item.code, StringComparer.Ordinal).ToArray();
        }
        private static void Replace(string source, string destination)
        {
            if (!File.Exists(destination)) { File.Move(source, destination); return; }
            string backup = destination + ".bak";
            try { File.Replace(source, destination, backup, true); } finally { if (File.Exists(backup)) File.Delete(backup); }
        }
    }
}
