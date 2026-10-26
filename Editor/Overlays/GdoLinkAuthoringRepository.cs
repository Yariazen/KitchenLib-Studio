using System;
using System.Collections.Generic;
using System.Linq;
using KitchenData;
using UnityEditor;
using UnityEngine;

namespace KitchenLib.Studio.Editor.Overlays
{
    internal static class GdoLinkAuthoringRepository
    {
        public const string Root = "Assets/ModContent/Editor/KitchenLibStudioLinks";

        public static GdoLinkAuthoringAsset GetOrCreate(GameDataObject target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            string path = CompanionPath(target);
            GdoLinkAuthoringAsset existing = AssetDatabase.LoadAssetAtPath<GdoLinkAuthoringAsset>(path);
            if (existing != null)
            {
                return existing;
            }

            EnsureFolder("Assets/ModContent");
            EnsureFolder(Root);
            var created = ScriptableObject.CreateInstance<GdoLinkAuthoringAsset>();
            created.Initialise(target);
            AssetDatabase.CreateAsset(created, path);
            AssetDatabase.SaveAssets();
            return created;
        }

        public static GdoLinkAuthoringAsset Find(GameDataObject target)
        {
            if (target == null)
            {
                return null;
            }

            string assetPath = AssetDatabase.GetAssetPath(target);
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            return string.IsNullOrEmpty(guid)
                ? null
                : AssetDatabase.LoadAssetAtPath<GdoLinkAuthoringAsset>(Root + "/" + guid + ".asset");
        }

        public static IReadOnlyList<GdoLinkAuthoringAsset> FindForTargets(IEnumerable<GameDataObject> targets)
        {
            if (targets == null)
            {
                throw new ArgumentNullException(nameof(targets));
            }

            return targets
                .Where(target => target != null)
                .Distinct()
                .Select(Find)
                .Where(companion => companion != null)
                .OrderBy(companion => companion.Target.ID)
                .ToArray();
        }

        private static string CompanionPath(GameDataObject target)
        {
            string assetPath = AssetDatabase.GetAssetPath(target);
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid))
            {
                throw new InvalidOperationException("Target must be saved as a project asset before link data can be created.");
            }

            return Root + "/" + guid + ".asset";
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string name = System.IO.Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
