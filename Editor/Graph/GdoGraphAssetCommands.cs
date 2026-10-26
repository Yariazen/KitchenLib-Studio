using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KitchenData;
using KitchenLib.Studio.Generated;
using UnityEditor;
using UnityEngine;

using KitchenLib.Studio.Editor.Bootstrap;

using KitchenLib.Studio.Editor.Overlays;
using KitchenLib.Studio.Editor.Packaging;

namespace KitchenLib.Studio.Editor.Graph
{
    internal static class GdoGraphAssetCommands
    {
        public static IReadOnlyList<string> SupportedRuntimeTypes => GdoSchemaRegistry.Types
            .Where(type => type.Availability == "Available" && ResolveType(type.RuntimeTypeName) != null)
            .Select(type => type.RuntimeTypeName).OrderBy(value => value, StringComparer.Ordinal).ToArray();

        public static GdoGraphNodeRecord CreateGdo(GdoBundleGraphAsset graph, string runtimeTypeName,
            string gdoName, Rect position)
        {
            RequireOwnedLayout(graph);
            GdoProjectIdentity.ValidateName(gdoName, nameof(gdoName));
            RequireUniqueName(graph, gdoName);
            int calculatedId = GdoProjectIdentity.RuntimeId(
                graph.AuthorNamespace, graph.BundleName, gdoName);
            if (graph.Nodes.Any(node => (node.Kind == GdoGraphNodeKind.External ? node.ExternalId : node.RuntimeId) == calculatedId))
                throw new ArgumentException("Calculated GDO ID collides with a local or known external GDO.", nameof(gdoName));
            string folder = GdoBundleProjectRepository.GdoFolder(graph);
            string fileName = gdoName + ".asset";
            string absoluteFolder = Path.Combine(KitchenLibStudioGeneratedPaths.ProjectRoot, folder);
            if (Directory.EnumerateFileSystemEntries(absoluteFolder).Any(path =>
                string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("An asset already exists with this GDO name.", nameof(gdoName));
            string assetPath = folder + "/" + fileName;
            if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
                throw new ArgumentException("An asset already exists at the GDO path.", nameof(assetPath));
            Type type = ResolveType(runtimeTypeName);
            if (type == null || !typeof(GameDataObject).IsAssignableFrom(type) || type.IsAbstract)
                throw new ArgumentException("No creatable generated mirror type exists for '" + runtimeTypeName + "'.", nameof(runtimeTypeName));
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create graph GDO");
            try
            {
                EnsureFolder(Path.GetDirectoryName(assetPath)?.Replace('\\', '/'));
                var asset = (GameDataObject)ScriptableObject.CreateInstance(type);
                asset.name = gdoName;
                asset.ID = calculatedId;
                AssetDatabase.CreateAsset(asset, assetPath);
                Undo.RegisterCreatedObjectUndo(asset, "Create GDO asset");
                GdoOverlayAuthoringAsset companion = GdoOverlayAuthoringRepository.GetOrCreate(asset);
                Undo.RegisterCreatedObjectUndo(companion, "Create GDO companion");
                GdoGraphNodeRecord node = GdoBundleGraphRepository.AddMember(graph, asset, position);
                Undo.CollapseUndoOperations(group);
                return node;
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
        }

        public static GdoGraphNodeRecord AddExistingGdo(GdoBundleGraphAsset graph, GameDataObject asset, Rect position)
        {
            RequireOwnedLayout(graph);
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            string path = AssetDatabase.GetAssetPath(asset);
            if (!path.StartsWith(GdoBundleProjectRepository.GdoFolder(graph) + "/", StringComparison.Ordinal))
                throw new ArgumentException("GDO must be saved inside this project's GDOs folder.", nameof(asset));
            GdoProjectIdentity.ValidateName(asset.name, nameof(asset));
            RequireUniqueName(graph, asset.name);
            return GdoBundleGraphRepository.AddMember(graph, asset, position);
        }

        public static void SetIntrinsicValue(GameDataObject target, string declaringType, string fieldName, GdoOverlayValue value)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            GdoOverlayAuthoringAsset companion = GdoOverlayAuthoringRepository.GetOrCreate(target);
            Undo.RecordObject(companion, "Edit graph GDO intrinsic value");
            companion.UpsertField(declaringType, fieldName, value);
            EditorUtility.SetDirty(companion);
        }

        internal static Type ResolveType(string runtimeTypeName)
        {
            string clrName = runtimeTypeName?.Replace('/', '+');
            return AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(clrName, false)).FirstOrDefault(type => type != null);
        }

        private static void RequireOwnedLayout(GdoBundleGraphAsset graph)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            if (GdoBundleProjectRepository.ValidateLayout(graph).Count != 0)
                throw new ArgumentException("Graph must have a valid bundle project layout.", nameof(graph));
        }

        private static void RequireUniqueName(GdoBundleGraphAsset graph, string name)
        {
            string canonical = GdoProjectIdentity.Canonical(graph.BundleName, name);
            if (graph.Nodes.Any(node => node.Kind == GdoGraphNodeKind.Local && !node.IsBroken &&
                GdoProjectIdentity.Canonical(graph.BundleName, node.Asset.name) == canonical))
                throw new ArgumentException("A GDO with this name already belongs to the project.", nameof(name));
        }

        private static void EnsureFolder(string path)
        {
            if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
