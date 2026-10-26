using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using UnityEngine;

using KitchenLib.Studio.Editor.Bootstrap;

namespace KitchenLib.Studio.Editor.Snapshots
{
    internal sealed class LegacyWaterPlateUpPreviewDecoder : IPlateUpPreviewDecoder
    {
        public VanillaPreviewModel Decode(string gameRoot, VanillaAssetRecord root,
            VanillaAssetCatalogData catalog, IProgress<SnapshotProgress> progress,
            CancellationToken cancellationToken)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            PlateUpSourceFingerprint.Validate(gameRoot, catalog);
            return Decode(gameRoot, root.Source, progress, cancellationToken);
        }

        public VanillaPreviewModel Decode(string gameRoot, VanillaObjectIdentity root,
            IProgress<SnapshotProgress> progress, CancellationToken cancellationToken)
        {
            string dataRoot = PlateUpPlatformLayout.ResolveLayout(gameRoot).DataRoot;
            string assetPath = Path.Combine(dataRoot, root.RelativePath);
            string classData = KitchenLibStudioGeneratedPaths.PackageFilePath(
                "Editor/ThirdParty/AssetRipper.TypeTreeData/classdata.tpk");
            var manager = new AssetsManager();
            var meshes = new List<PreviewMeshData>();
            var meshIndexes = new Dictionary<long, int>();
            try
            {
                manager.LoadClassPackage(classData);
                AssetsFileInstance instance = manager.LoadAssetsFile(assetPath, false);
                manager.LoadClassDatabaseFromPackage(instance.file.Metadata.UnityVersion);
                PreviewNodeData node = ReadGameObject(manager, instance, gameRoot, root.RelativePath, root.PathId,
                    meshes, meshIndexes, new HashSet<long>(), cancellationToken);
                return new VanillaPreviewModel(node, meshes.ToArray(), new[] { "Preview shaders use an editor fallback." });
            }
            finally { manager.UnloadAll(true); }
        }

        private static PreviewNodeData ReadGameObject(AssetsManager manager, AssetsFileInstance instance,
            string gameRoot, string relativePath, long pathId, IList<PreviewMeshData> meshes,
            IDictionary<long, int> meshIndexes, ISet<long> stack, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (!stack.Add(pathId)) throw new InvalidDataException("GameObject hierarchy cycle detected.");
            AssetTypeValueField go = Read(manager, instance, pathId);
            var node = new PreviewNodeData { Name = go["m_Name"].AsString };
            AssetTypeValueField active = go["m_IsActive"];
            node.Active = active == null || active.IsDummy || active.AsBool;
            var children = new List<PreviewNodeData>();
            var materials = new List<string>();
            foreach (AssetTypeValueField component in ArrayChildren(go["m_Component"]))
            {
                AssetTypeValueField pointer = component["component"] ?? component;
                long componentId = PathId(pointer);
                if (componentId == 0) continue;
                AssetFileInfo info = instance.file.GetAssetInfo(componentId);
                if (info == null) continue;
                AssetTypeValueField fields = manager.GetBaseField(instance, info, AssetReadFlags.None);
                if (info.TypeId == (int)AssetClassID.Transform || info.TypeId == (int)AssetClassID.RectTransform)
                {
                    node.LocalPosition = Vector3(fields["m_LocalPosition"]);
                    node.LocalRotation = QuaternionValue(fields["m_LocalRotation"]);
                    node.LocalScale = Vector3(fields["m_LocalScale"]);
                    foreach (AssetTypeValueField child in ArrayChildren(fields["m_Children"]))
                    {
                        long transformId = PathId(child);
                        if (transformId == 0) continue;
                        AssetTypeValueField childTransform = Read(manager, instance, transformId);
                        long childGo = PathId(childTransform["m_GameObject"]);
                        if (childGo != 0) children.Add(ReadGameObject(manager, instance, gameRoot, relativePath,
                            childGo, meshes, meshIndexes, stack, token));
                    }
                }
                else if (info.TypeId == (int)AssetClassID.MeshFilter)
                {
                    long meshId = PathId(fields["m_Mesh"]);
                    if (meshId != 0)
                    {
                        if (!meshIndexes.TryGetValue(meshId, out int index))
                        {
                            index = meshes.Count;
                            meshIndexes.Add(meshId, index);
                            meshes.Add(PlateUpMeshDecoder.DecodeInstalled(gameRoot, relativePath, meshId, token));
                        }
                        node.MeshIndex = index;
                    }
                }
                else if (info.TypeId == (int)AssetClassID.MeshRenderer || info.TypeId == (int)AssetClassID.SkinnedMeshRenderer)
                {
                    foreach (AssetTypeValueField material in ArrayChildren(fields["m_Materials"]))
                    {
                        long materialId = PathId(material);
                        materials.Add(materialId == 0 ? "Missing" : Read(manager, instance, materialId)["m_Name"].AsString);
                    }
                }
            }
            node.MaterialNames = materials.ToArray();
            node.Children = children.ToArray();
            stack.Remove(pathId);
            return node;
        }

        private static AssetTypeValueField Read(AssetsManager manager, AssetsFileInstance instance, long pathId)
        {
            AssetFileInfo info = instance.file.GetAssetInfo(pathId);
            if (info == null) throw new KeyNotFoundException("Object path ID not found: " + pathId);
            return manager.GetBaseField(instance, info, AssetReadFlags.None);
        }

        private static IEnumerable<AssetTypeValueField> ArrayChildren(AssetTypeValueField field)
        {
            if (field?.Children == null) return Enumerable.Empty<AssetTypeValueField>();
            AssetTypeValueField array = field.Children.FirstOrDefault(child => child.FieldName == "Array");
            return array?.Children ?? field.Children;
        }

        private static long PathId(AssetTypeValueField pointer) => pointer == null ? 0 :
            (pointer["m_PathID"] ?? pointer).AsLong;
        private static Vector3 Vector3(AssetTypeValueField value) => new Vector3(value["x"].AsFloat, value["y"].AsFloat, value["z"].AsFloat);
        private static Quaternion QuaternionValue(AssetTypeValueField value) => new Quaternion(value["x"].AsFloat, value["y"].AsFloat, value["z"].AsFloat, value["w"].AsFloat);
    }
}
