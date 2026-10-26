using System;
using System.Collections.Generic;
using System.Linq;
using KitchenData;
using UnityEditor;
using UnityEngine;

using KitchenLib.Studio.Editor.Graph;
using KitchenLib.Studio.Editor.Packaging;

namespace KitchenLib.Studio.Editor.Overlays
{
    internal static class GdoOverlayAuthoringRepository
    {
        public const string Root = "Assets/ModContent/Editor/KitchenLibStudioOverlays";

        public static GdoOverlayAuthoringAsset GetOrCreate(GameDataObject target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            string path = CompanionPath(target);
            GdoOverlayAuthoringAsset existing = Find(target);
            if (existing != null) return existing;
            EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));
            GdoOverlayAuthoringAsset created = ScriptableObject.CreateInstance<GdoOverlayAuthoringAsset>();
            created.Initialise(target); AssetDatabase.CreateAsset(created, path); AssetDatabase.SaveAssets();
            return created;
        }

        public static GdoOverlayAuthoringAsset Find(GameDataObject target)
        {
            if (target == null) return null;
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(target));
            if (string.IsNullOrEmpty(guid)) return null;
            string path = ExistingPath(target);
            return AssetDatabase.LoadAssetAtPath<GdoOverlayAuthoringAsset>(path);
        }

        public static IReadOnlyList<GdoOverlayAuthoringAsset> FindForTargets(IEnumerable<GameDataObject> targets) =>
            targets.Where(target => target != null).Distinct().Select(Find).Where(value => value != null).OrderBy(value => value.Target.ID).ToArray();

        internal static string ExistingPath(GameDataObject target)
        {
            string owned = CompanionPath(target);
            if (AssetDatabase.LoadAssetAtPath<GdoOverlayAuthoringAsset>(owned) != null) return owned;
            string shared = Root + "/" + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(target)) + ".asset";
            return AssetDatabase.LoadAssetAtPath<GdoOverlayAuthoringAsset>(shared) != null ? shared : owned;
        }

        internal static string CompanionPath(GameDataObject target)
        {
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(target));
            if (string.IsNullOrEmpty(guid)) throw new InvalidOperationException("Target must be saved before overlay data can be created.");
            string assetPath = AssetDatabase.GetAssetPath(target);
            int gdos = assetPath.LastIndexOf("/GDOs/", StringComparison.Ordinal);
            if (gdos >= 0)
            {
                string project = assetPath.Substring(0, gdos);
                string graphPath = project + "/" + System.IO.Path.GetFileName(project) + ".asset";
                if (AssetDatabase.LoadAssetAtPath<GdoBundleGraphAsset>(graphPath) != null)
                    return project + "/Editor/Overlays/" + guid + ".asset";
            }
            return Root + "/" + guid + ".asset";
        }

        public static void MoveCompanionsIntoProject(GdoBundleGraphAsset graph)
        {
            if (GdoBundleProjectRepository.ValidateLayout(graph).Count != 0)
                throw new ArgumentException("Graph must have a valid bundle project layout.", nameof(graph));
            var moved = new Stack<KeyValuePair<string, string>>();
            try
            {
                foreach (GdoGraphNodeRecord node in graph.Nodes.Where(n => n.Kind == GdoGraphNodeKind.Local && !n.IsBroken))
                {
                    string source = ExistingPath(node.Asset), destination = CompanionPath(node.Asset);
                    if (source == destination || AssetDatabase.LoadMainAssetAtPath(source) == null) continue;
                    EnsureFolder(System.IO.Path.GetDirectoryName(destination).Replace('\\', '/'));
                    string error = AssetDatabase.MoveAsset(source, destination);
                    if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
                    moved.Push(new KeyValuePair<string, string>(source, destination));
                }
            }
            catch
            {
                while (moved.Count != 0)
                {
                    var move = moved.Pop();
                    string error = AssetDatabase.MoveAsset(move.Value, move.Key);
                    if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException("Companion migration rollback failed: " + error);
                }
                throw;
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent); AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

    }
}
