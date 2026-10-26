using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace KitchenLib.Studio.Editor.Catalogs.Mods
{
    [Serializable]
    internal sealed class ModCatalogCaptureRequestData
    {
        public int schemaVersion;
        public string captureToken;
        public int steamAppId;
        public string steamBuildId;
        public string responseDirectory;
        public ModCatalogCaptureWorkshopItemData[] workshopItems;
    }

    [Serializable]
    internal sealed class ModCatalogCaptureWorkshopItemData
    {
        public string workshopId;
        public string providerKey;
        public ModCatalogCaptureFileData[] assemblies;
        public ModCatalogCaptureFileData[] assetBundles;
    }

    [Serializable]
    internal sealed class ModCatalogCaptureFileData
    {
        public string relativePath;
        public long length;
        public string sha256;
    }

    internal static class ModCatalogCaptureProtocol
    {
        public const int SchemaVersion = 1;
        public const string RequestFolderName = "requests";
        public const string ResponseFolderName = "responses";
        public const string ResponseCatalogName = "mod-gdo-catalog.json";
        public const string CompletionMarkerName = "complete.token";
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false, true);

        public static void WriteRequestAtomic(string path, ModCatalogCaptureRequestData request)
        {
            Validate(request);
            string directory = Path.GetDirectoryName(Path.GetFullPath(path));
            Directory.CreateDirectory(directory);
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllBytes(temporary, Utf8NoBom.GetBytes(JsonUtility.ToJson(request, true) + "\n"));
                if (File.Exists(path)) File.Replace(temporary, path, null, true);
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        public static ModCatalogCaptureRequestData ReadRequest(string path)
        {
            ModCatalogCaptureRequestData request;
            try { request = JsonUtility.FromJson<ModCatalogCaptureRequestData>(Utf8NoBom.GetString(File.ReadAllBytes(path))); }
            catch (Exception exception) when (exception is ArgumentException || exception is DecoderFallbackException)
            { throw new InvalidDataException("The mod-catalogue capture request is not valid UTF-8 JSON.", exception); }
            Validate(request);
            return request;
        }

        public static void Validate(ModCatalogCaptureRequestData request)
        {
            if (request == null || request.schemaVersion != SchemaVersion || request.steamAppId != ModGdoCatalogFile.PlateUpSteamAppId ||
                string.IsNullOrWhiteSpace(request.captureToken) || string.IsNullOrWhiteSpace(request.steamBuildId) ||
                string.IsNullOrWhiteSpace(request.responseDirectory) || request.workshopItems == null)
                throw new InvalidDataException("The mod-catalogue capture request is incomplete or incompatible.");
        }
    }
}
