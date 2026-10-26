using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace KitchenLib.Studio.Editor
{
    public static class BakedPrefabFile
    {
        public const int SchemaVersion = 1;
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false, true);

        public static byte[] Serialize(BakedPrefabData data)
        {
            Validate(data);
            return Utf8NoBom.GetBytes(JsonUtility.ToJson(data, true) + "\n");
        }

        public static BakedPrefabData Read(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A baked prefab path is required.", nameof(path));
            BakedPrefabData data;
            try { data = JsonUtility.FromJson<BakedPrefabData>(Utf8NoBom.GetString(File.ReadAllBytes(path))); }
            catch (Exception exception) when (exception is ArgumentException || exception is DecoderFallbackException)
            { throw new InvalidDataException("The baked prefab is not valid UTF-8 JSON.", exception); }
            Validate(data);
            return data;
        }

        public static void Validate(BakedPrefabData data)
        {
            if (data == null || data.schemaVersion != SchemaVersion || data.runtimeId == 0 ||
                string.IsNullOrWhiteSpace(data.displayName) || data.nodes == null || data.nodes.Length == 0 ||
                data.meshes == null || data.renderers == null || data.materials == null || data.textures == null)
                throw new InvalidDataException("The baked prefab is incomplete or incompatible.");

            for (int index = 0; index < data.nodes.Length; index++)
            {
                BakedPrefabNodeData node = data.nodes[index];
                if (node == null || string.IsNullOrWhiteSpace(node.name) || node.parentIndex >= index ||
                    node.parentIndex < -1 || node.localPosition == null || node.localPosition.Length != 3 ||
                    node.localRotation == null || node.localRotation.Length != 4 ||
                    node.localScale == null || node.localScale.Length != 3)
                    throw new InvalidDataException("The baked prefab contains an invalid hierarchy node.");
            }
        }
    }
}
