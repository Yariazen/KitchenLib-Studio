using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace KitchenLib.Studio.Editor.Bootstrap
{
    internal sealed class PlateUpSchemaFingerprint
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
            if (!File.Exists(schemaPath)) throw new FileNotFoundException("The locally generated PlateUp schema was not found.", schemaPath);
            SchemaMetadata metadata = JsonUtility.FromJson<SchemaMetadata>(File.ReadAllText(schemaPath, Encoding.UTF8));
            if (metadata == null || string.IsNullOrEmpty(metadata.sourceSha256) || string.IsNullOrEmpty(metadata.globalFingerprint))
                throw new InvalidDataException("The locally generated PlateUp schema metadata is incomplete.");
            PlateUpSchemaFingerprint installed = FromInstalledAssembly(gameRoot, metadata.targetUnityVersion, metadata.globalFingerprint);
            if (!string.Equals(metadata.sourceAssembly, installed.AssemblyName, StringComparison.Ordinal) ||
                !string.Equals(metadata.sourceSha256, installed.AssemblySha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The selected installation changed since local mirror generation. Regenerate mirrors before importing its catalog.");
            return installed;
        }

        public static string HashFile(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", string.Empty);
        }

        internal static string HashText(string value)
        {
            using (SHA256 hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", string.Empty);
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
