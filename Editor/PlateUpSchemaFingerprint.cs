using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace KitchenLib.Studio.Editor
{
    public sealed class PlateUpSchemaFingerprint
    {
        private PlateUpSchemaFingerprint(string unityVersion, string assemblyName, string assemblySha256, string schemaFingerprint)
        {
            UnityVersion = unityVersion;
            AssemblyName = assemblyName;
            AssemblySha256 = assemblySha256;
            SchemaFingerprint = schemaFingerprint;
        }

        public string UnityVersion { get; }
        public string AssemblyName { get; }
        public string AssemblySha256 { get; }
        public string SchemaFingerprint { get; }

        public static PlateUpSchemaFingerprint FromInstalledAssembly(string gameRoot, string unityVersion, string schemaFingerprint)
        {
            string assemblyPath = Path.Combine(PlateUpPlatformLayout.ResolveLayout(gameRoot).ManagedDirectory, "Kitchen.GameData.dll");
            if (!File.Exists(assemblyPath)) throw new FileNotFoundException("Installed Kitchen.GameData.dll was not found.", assemblyPath);
            return new PlateUpSchemaFingerprint(unityVersion, "Kitchen.GameData", HashFile(assemblyPath), schemaFingerprint);
        }

        public static PlateUpSchemaFingerprint LoadAndValidate(string schemaPath, string gameRoot)
        {
            if (!File.Exists(schemaPath)) throw new FileNotFoundException("The shipped PlateUp schema was not found.", schemaPath);
            SchemaMetadata metadata = JsonUtility.FromJson<SchemaMetadata>(File.ReadAllText(schemaPath, Encoding.UTF8));
            if (metadata == null || string.IsNullOrEmpty(metadata.sourceSha256) || string.IsNullOrEmpty(metadata.globalFingerprint))
                throw new InvalidDataException("The shipped PlateUp schema metadata is incomplete.");
            PlateUpSchemaFingerprint installed = FromInstalledAssembly(gameRoot, metadata.targetUnityVersion, metadata.globalFingerprint);
            if (!string.Equals(metadata.sourceAssembly, installed.AssemblyName, StringComparison.Ordinal) ||
                !string.Equals(metadata.sourceSha256, installed.AssemblySha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The installed Kitchen.GameData.dll does not match the shipped PlateUp schema. Regenerate the schema before importing a catalog.");
            return installed;
        }

        internal static string HashFile(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", string.Empty);
        }

        [Serializable]
        private sealed class SchemaMetadata
        {
            public string targetUnityVersion;
            public string sourceAssembly;
            public string sourceSha256;
            public string globalFingerprint;
        }
    }
}
