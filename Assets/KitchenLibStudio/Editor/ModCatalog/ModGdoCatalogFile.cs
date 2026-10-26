using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace KitchenLib.Studio.Editor
{
    public static class ModGdoCatalogFile
    {
        public const int SchemaVersion = 2;
        public const int PlateUpSteamAppId = 1599600;
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false, true);

        public static byte[] Serialize(ModGdoCatalogData data)
        {
            Validate(data);
            using (var stream = new MemoryStream())
            {
                CreateSerializer().WriteObject(stream, data);
                byte[] json = stream.ToArray();
                byte[] result = new byte[json.Length + 1];
                Buffer.BlockCopy(json, 0, result, 0, json.Length);
                result[result.Length - 1] = (byte)'\n';
                return result;
            }
        }

        public static ModGdoCatalogData Read(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A catalogue path is required.", nameof(path));
            ModGdoCatalogData data = Deserialize(File.ReadAllBytes(path));
            Validate(data);
            return data;
        }

        public static void PublishAtomic(string destination, byte[] content)
            => PublishAtomic(destination, content, ReplaceOnSameVolume);

        public static void PublishAtomic(string destination, byte[] content, Action<string, string> replacement)
        {
            if (string.IsNullOrWhiteSpace(destination)) throw new ArgumentException("A destination is required.", nameof(destination));
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

        public static void Validate(ModGdoCatalogData data)
        {
            if (data == null || data.schemaVersion != SchemaVersion || data.steamAppId != PlateUpSteamAppId ||
                string.IsNullOrWhiteSpace(data.captureToken) || string.IsNullOrWhiteSpace(data.steamBuildId) ||
                string.IsNullOrWhiteSpace(data.bridgeVersion) || data.providers == null || data.entries == null ||
                data.diagnostics == null)
                throw new InvalidDataException("The mod GDO catalogue is incomplete or incompatible.");

            ExternalGdoRelationshipSchema.Validate(data.relationships);

            foreach (ModGdoProviderData provider in data.providers)
                if (provider == null || string.IsNullOrWhiteSpace(provider.key) || string.IsNullOrWhiteSpace(provider.displayName))
                    throw new InvalidDataException("The mod GDO catalogue contains an invalid provider.");

            foreach (ModGdoEntryData entry in data.entries)
            {
                if (entry == null || entry.runtimeId == 0 || string.IsNullOrWhiteSpace(entry.providerKey) ||
                    string.IsNullOrWhiteSpace(entry.displayName) ||
                    string.IsNullOrWhiteSpace(entry.runtimeType) || string.IsNullOrWhiteSpace(entry.sourceAssembly))
                    throw new InvalidDataException("The mod GDO catalogue contains an invalid entry.");
                foreach (ModGdoReferenceData reference in entry.references ?? Array.Empty<ModGdoReferenceData>())
                    if (reference == null || string.IsNullOrWhiteSpace(reference.path) ||
                        string.IsNullOrWhiteSpace(reference.providerKey) || reference.runtimeId == 0 ||
                        string.IsNullOrWhiteSpace(reference.runtimeType))
                        throw new InvalidDataException("The mod GDO catalogue contains an ambiguous reference.");
            }
        }

        private static ModGdoCatalogData Deserialize(byte[] content)
        {
            try
            {
                Utf8NoBom.GetString(content);
                using (var stream = new MemoryStream(content, false))
                    return (ModGdoCatalogData)CreateSerializer().ReadObject(stream);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is DecoderFallbackException ||
                                               exception is SerializationException)
            {
                throw new InvalidDataException("The mod GDO catalogue is not valid UTF-8 JSON.", exception);
            }
        }

        private static DataContractJsonSerializer CreateSerializer() => new DataContractJsonSerializer(
            typeof(ModGdoCatalogData), new DataContractJsonSerializerSettings { MaxItemsInObjectGraph = int.MaxValue });

        private static void ReplaceOnSameVolume(string source, string destination)
        {
            if (!File.Exists(destination))
            {
                File.Move(source, destination);
                return;
            }
            string backup = destination + ".bak";
            try { File.Replace(source, destination, backup, true); }
            finally { if (File.Exists(backup)) File.Delete(backup); }
        }
    }
}
