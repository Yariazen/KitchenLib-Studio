using System;
using System.Collections.Generic;
using System.IO;
using KitchenLib.Studio.Editor.Bootstrap;
using KitchenLib.Studio.Editor.Overlays;
using KitchenLib.Studio.Editor.Packaging;
using UnityEditor;
using UnityEngine;

namespace KitchenLib.Studio.Editor.Graph
{
    internal static class GdoGraphAssetDeletion
    {
        public static void Delete(GdoBundleGraphAsset graph, string nodeId) =>
            Delete(graph, nodeId, AssetDatabase.DeleteAsset);

        internal static void Delete(GdoBundleGraphAsset graph, string nodeId, Func<string, bool> deleteAsset)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            if (deleteAsset == null) throw new ArgumentNullException(nameof(deleteAsset));
            GdoGraphNodeRecord node = graph.FindNode(nodeId);
            if (node == null) throw new ArgumentException("Graph node does not exist.", nameof(nodeId));
            if (node.Kind != GdoGraphNodeKind.Local || node.IsBroken)
                throw new InvalidOperationException("Only a resolved local GDO can be deleted with its owned assets.");

            string assetPath = AssetDatabase.GetAssetPath(node.Asset);
            RequireOwned(assetPath, GdoBundleProjectRepository.GdoFolder(graph), "GDO asset");
            string companionPath = GdoOverlayAuthoringRepository.ExistingPath(node.Asset);
            RequireOwned(companionPath, GdoBundleProjectRepository.ProjectFolder(graph) + "/Editor/Overlays", "GDO companion");

            string graphPath = AssetDatabase.GetAssetPath(graph);
            bool dirty = EditorUtility.IsDirty(graph);
            AssetDatabase.SaveAssetIfDirty(graph);
            var snapshots = new[] { Snapshot.Capture(graphPath), Snapshot.Capture(assetPath), Snapshot.Capture(companionPath) };
            string graphJson = EditorJsonUtility.ToJson(graph);
            try
            {
                GdoBundleGraphRepository.RemoveMember(graph, nodeId, true);
                if (!deleteAsset(companionPath)) throw new InvalidOperationException("Could not delete GDO companion: " + companionPath);
                if (!deleteAsset(assetPath)) throw new InvalidOperationException("Could not delete GDO asset: " + assetPath);
                AssetDatabase.SaveAssetIfDirty(graph);
                Undo.ClearUndo(graph);
            }
            catch
            {
                foreach (Snapshot snapshot in snapshots) snapshot.Restore();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset(graphPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                EditorJsonUtility.FromJsonOverwrite(graphJson, graph);
                if (dirty) EditorUtility.SetDirty(graph); else EditorUtility.ClearDirty(graph);
                throw;
            }
        }

        private static void RequireOwned(string path, string folder, string label)
        {
            string prefix = folder.TrimEnd('/') + "/";
            if (string.IsNullOrEmpty(path) || !path.StartsWith(prefix, StringComparison.Ordinal))
                throw new InvalidOperationException(label + " is outside the bundle project's owned folder.");
        }

        private sealed class Snapshot
        {
            private readonly string _assetPath;
            private readonly IReadOnlyDictionary<string, byte[]> _files;

            private Snapshot(string assetPath, IReadOnlyDictionary<string, byte[]> files)
            { _assetPath = assetPath; _files = files; }

            public static Snapshot Capture(string assetPath)
            {
                if (AssetDatabase.LoadMainAssetAtPath(assetPath) == null)
                    throw new InvalidOperationException("Owned asset is missing: " + assetPath);
                string absolute = Absolute(assetPath);
                var files = new Dictionary<string, byte[]>(StringComparer.Ordinal)
                {
                    [absolute] = File.ReadAllBytes(absolute)
                };
                string meta = absolute + ".meta";
                if (File.Exists(meta)) files[meta] = File.ReadAllBytes(meta);
                return new Snapshot(assetPath, files);
            }

            public void Restore()
            {
                foreach (KeyValuePair<string, byte[]> file in _files)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(file.Key));
                    File.WriteAllBytes(file.Key, file.Value);
                }
                AssetDatabase.ImportAsset(_assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            }

            private static string Absolute(string assetPath) => Path.Combine(
                KitchenLibStudioGeneratedPaths.ProjectRoot,
                assetPath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
