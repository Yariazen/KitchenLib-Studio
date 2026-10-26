using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using KitchenData;
using UnityEngine;

namespace KitchenLib.Studio.Editor
{
    public static class GdoLinkManifestWriter
    {
        public static string Write(
            IEnumerable<GameDataObject> targets,
            IEnumerable<GdoLinkAuthoringAsset> companions,
            string mirrorManifestPath)
        {
            if (targets == null) throw new ArgumentNullException(nameof(targets));
            if (companions == null) throw new ArgumentNullException(nameof(companions));

            MirrorManifest mirror = ReadMirrorManifest(mirrorManifestPath);
            GameDataObject[] builtTargets = targets.Where(target => target != null).ToArray();
            if (builtTargets.GroupBy(target => target.ID).Any(group => group.Count() != 1))
            {
                throw new InvalidOperationException("Built GDO target IDs must be unique.");
            }

            GdoLinkAuthoringAsset[] authored = companions.Where(companion => companion != null).ToArray();
            if (authored.GroupBy(companion => companion.Target).Any(group => group.Count() != 1))
            {
                throw new InvalidOperationException("A target may have only one link authoring companion.");
            }

            var manifest = new GdoLinkManifest
            {
                schemaVersion = 1,
                mirrorFingerprint = mirror.globalFingerprint
            };

            foreach (GdoLinkAuthoringAsset companion in authored.OrderBy(item => item.Target.ID))
            {
                if (!(companion.Target is Dish) || !builtTargets.Contains(companion.Target))
                {
                    throw new InvalidOperationException("Only built KitchenData.Dish targets are supported.");
                }

                int[] ids = companion.MinimumIngredientIds.OrderBy(id => id).ToArray();
                if (ids.Any(id => id == 0) || ids.Distinct().Count() != ids.Length)
                {
                    throw new InvalidOperationException("MinimumIngredients IDs must be non-zero and unique.");
                }

                manifest.targets.Add(new GdoLinkTarget
                {
                    gdoId = companion.Target.ID,
                    gdoType = "Dish",
                    links = new List<GdoLinkField>
                    {
                        new GdoLinkField
                        {
                            field = "MinimumIngredients",
                            kind = "GdoHashSet",
                            elementType = "Item",
                            ids = ids.ToList()
                        }
                    }
                });
            }

            return Format(manifest);
        }

        private static MirrorManifest ReadMirrorManifest(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                throw new InvalidOperationException("Mirror metadata does not exist.");
            }

            MirrorManifest manifest;
            try
            {
                manifest = JsonUtility.FromJson<MirrorManifest>(File.ReadAllText(path, Encoding.UTF8));
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException("Mirror metadata is malformed.", exception);
            }

            MirrorType dish = manifest?.types?.SingleOrDefault(type => type.runtimeTypeName == "KitchenData.Dish");
            MirrorLinkField link = dish?.fields?.SingleOrDefault(field => field.name == "MinimumIngredients");
            bool versionFour = manifest != null && manifest.schemaVersion == 4 && link?.value?.kind == "Set" &&
                link.value.element?.kind == "GdoReference" && link.value.element.expectedTypeName == "KitchenData.Item";
            if (manifest == null || !versionFour ||
                string.IsNullOrEmpty(manifest.globalFingerprint) || manifest.globalFingerprint.Length != 64 ||
                link == null)
            {
                throw new InvalidOperationException("Mirror metadata is incompatible with GDO link schema 1.");
            }

            return manifest;
        }

        private static string Format(GdoLinkManifest manifest)
        {
            var text = new StringBuilder();
            text.Append("{\n");
            text.Append("  \"schemaVersion\": 1,\n");
            text.Append("  \"mirrorFingerprint\": \"").Append(manifest.mirrorFingerprint).Append("\",\n");
            text.Append("  \"targets\": [\n");
            for (int targetIndex = 0; targetIndex < manifest.targets.Count; targetIndex++)
            {
                GdoLinkTarget target = manifest.targets[targetIndex];
                GdoLinkField link = target.links.Single();
                text.Append("    {\n");
                text.Append("      \"gdoId\": ").Append(target.gdoId).Append(",\n");
                text.Append("      \"gdoType\": \"").Append(target.gdoType).Append("\",\n");
                text.Append("      \"links\": [\n");
                text.Append("        {\n");
                text.Append("          \"field\": \"").Append(link.field).Append("\",\n");
                text.Append("          \"kind\": \"").Append(link.kind).Append("\",\n");
                text.Append("          \"elementType\": \"").Append(link.elementType).Append("\",\n");
                text.Append("          \"ids\": [").Append(string.Join(", ", link.ids)).Append("]\n");
                text.Append("        }\n");
                text.Append("      ]\n");
                text.Append("    }");
                if (targetIndex + 1 < manifest.targets.Count) text.Append(',');
                text.Append('\n');
            }
            text.Append("  ]\n");
            text.Append("}\n");
            return text.ToString();
        }

        [Serializable]
        private sealed class MirrorManifest
        {
            public int schemaVersion;
            public string globalFingerprint;
            public List<MirrorType> types;
        }

        [Serializable]
        private sealed class MirrorType
        {
            public string runtimeTypeName;
            public List<MirrorLinkField> fields;
        }

        [Serializable]
        private sealed class MirrorLinkField
        {
            public string name;
            public MirrorValue value;
        }

        [Serializable]
        private sealed class MirrorValue
        {
            public string kind;
            public string expectedTypeName;
            public MirrorValue element;
        }
    }
}
