using System;
using System.IO;
using System.Text;
using UnityEngine;

using KitchenLib.Studio.Editor.Catalogs.Mods;

namespace KitchenLib.Studio.Editor.Catalogs.Vanilla
{
    internal static class VanillaGdoCatalogFile
    {
        public const int SchemaVersion = 2;
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false, true);

        public static byte[] Serialize(VanillaGdoCatalogData data)
        {
            Validate(data);
            return Utf8NoBom.GetBytes(JsonUtility.ToJson(data, true) + "\n");
        }

        public static VanillaGdoCatalogData Read(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("A catalog path is required.", nameof(path));
            VanillaGdoCatalogData data = Deserialize(File.ReadAllBytes(path));
            Validate(data);
            return data;
        }

        public static void PublishAtomic(string destination, byte[] content)
        {
            PublishAtomic(destination, content, ReplaceOnSameVolume);
        }

        public static void PublishAtomic(string destination, byte[] content, Action<string, string> replacement)
        {
            if (string.IsNullOrEmpty(destination)) throw new ArgumentException("A destination is required.", nameof(destination));
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (replacement == null) throw new ArgumentNullException(nameof(replacement));
            Validate(Deserialize(content));
            string directory = Path.GetDirectoryName(Path.GetFullPath(destination));
            Directory.CreateDirectory(directory);
            string temporary = Path.Combine(directory, Path.GetFileName(destination) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.WriteAllBytes(temporary, content);
                Validate(Deserialize(File.ReadAllBytes(temporary)));
                replacement(temporary, destination);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        public static void Validate(VanillaGdoCatalogData data)
        {
            if (data == null || data.schemaVersion != SchemaVersion || data.providerId != "plateup" || data.steamAppId != 1599600 ||
                string.IsNullOrEmpty(data.sourceAssemblySha256) || data.assets == null || data.entries == null || data.diagnostics == null)
                throw new InvalidDataException("The vanilla GDO catalog is incomplete or incompatible.");
            ExternalGdoRelationshipSchema.Validate(data.relationships);
        }

        internal static VanillaGdoCatalogData Deserialize(byte[] content)
        {
            try
            {
                return JsonUtility.FromJson<VanillaGdoCatalogData>(Utf8NoBom.GetString(content));
            }
            catch (Exception exception) when (exception is ArgumentException || exception is DecoderFallbackException)
            {
                throw new InvalidDataException("The vanilla GDO catalog is not valid UTF-8 JSON.", exception);
            }
        }

        private static void ReplaceOnSameVolume(string source, string destination)
        {
            if (!File.Exists(destination))
            {
                File.Move(source, destination);
                return;
            }
            string backup = destination + ".bak";
            try
            {
                File.Replace(source, destination, backup, true);
            }
            finally
            {
                if (File.Exists(backup)) File.Delete(backup);
            }
        }
    }
}
