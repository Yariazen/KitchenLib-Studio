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
    internal sealed class AssetsToolsPlateUpPreviewDecoder : IPlateUpPreviewDecoder
    {
        private const int MaximumNodes = 10000;
        private const int MaximumProperties = 4096;
        private readonly string _classDataPath;

        public AssetsToolsPlateUpPreviewDecoder() : this(KitchenLibStudioGeneratedPaths.PackageFilePath(
            "Editor/ThirdParty/AssetsTools.NET/classdata.tpk")) { }

        public AssetsToolsPlateUpPreviewDecoder(string classDataPath)
        {
            if (string.IsNullOrWhiteSpace(classDataPath))
                throw new ArgumentException("Class database path is required.", nameof(classDataPath));
            _classDataPath = Path.GetFullPath(classDataPath);
        }

        public VanillaPreviewModel Decode(string gameRoot, VanillaAssetRecord root, VanillaAssetCatalogData catalog,
            IProgress<SnapshotProgress> progress, CancellationToken cancellationToken)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (!string.Equals(root.Kind, "gameObject", StringComparison.Ordinal))
                throw new ArgumentException("Snapshot catalogue record must be a GameObject.", nameof(root));
            PlateUpSourceFingerprint.Validate(gameRoot, catalog);
            if (!(catalog.records ?? Array.Empty<VanillaAssetRecordData>()).Any(value => value.key == root.Key &&
                value.source.relativePath == root.Source.RelativePath && value.source.pathId == root.Source.PathId))
                throw new InvalidDataException("Snapshot record does not belong to the validated catalogue.");
            return Decode(gameRoot, root.Source, progress, cancellationToken);
        }

        public VanillaPreviewModel Decode(string gameRoot, VanillaObjectIdentity root,
            IProgress<SnapshotProgress> progress, CancellationToken cancellationToken)
        {
            string dataRoot = PlateUpPlatformLayout.ResolveLayout(gameRoot).DataRoot;
            string assetPath = Path.Combine(dataRoot, root.RelativePath);
            var manager = new AssetsManager();
            var context = new DecodeContext(gameRoot, _classDataPath, manager, progress, cancellationToken);
            context.Diagnostics.Add("Unavailable player shaders use an editor fallback.");
            try
            {
                manager.LoadClassPackage(_classDataPath);
                AssetsFileInstance instance = manager.LoadAssetsFile(assetPath, false);
                manager.LoadClassDatabaseFromPackage(instance.file.Metadata.UnityVersion);
                PreviewNodeData node = ReadGameObject(context, instance, root.PathId, new HashSet<string>());
                return new VanillaPreviewModel(node, context.Meshes.ToArray(), context.Materials.ToArray(),
                    context.Textures.ToArray(), context.Sprites.ToArray(), context.Diagnostics.Distinct().ToArray());
            }
            finally { manager.UnloadAll(true); }
        }

        private static PreviewNodeData ReadGameObject(DecodeContext context, AssetsFileInstance instance,
            long pathId, ISet<string> stack)
        {
            context.Token.ThrowIfCancellationRequested();
            if (++context.NodeCount > MaximumNodes) throw new InvalidDataException("GameObject hierarchy exceeds the preview node limit.");
            string identity = Identity(instance, pathId);
            if (!stack.Add(identity)) throw new InvalidDataException("GameObject hierarchy cycle detected.");
            AssetTypeValueField go = Read(context.Manager, instance, pathId);
            var node = new PreviewNodeData { Name = String(go["m_Name"], "GameObject " + pathId) };
            node.Active = Bool(go["m_IsActive"], true);
            var children = new List<PreviewNodeData>();
            var materialIndices = new List<int>();
            var materialNames = new List<string>();
            foreach (AssetTypeValueField component in ArrayChildren(go["m_Component"]))
            {
                AssetTypeValueField pointer = component["component"] ?? component;
                AssetExternal external = Resolve(context.Manager, instance, pointer);
                if (external.info == null || Missing(external.baseField)) continue;
                AssetTypeValueField fields = external.baseField;
                int typeId = external.info.TypeId;
                if (typeId == (int)AssetClassID.Transform || typeId == (int)AssetClassID.RectTransform)
                {
                    node.LocalPosition = Vector3Value(fields["m_LocalPosition"], Vector3.zero);
                    node.LocalRotation = QuaternionValue(fields["m_LocalRotation"]);
                    node.LocalScale = Vector3Value(fields["m_LocalScale"], Vector3.one);
                    foreach (AssetTypeValueField child in ArrayChildren(fields["m_Children"]))
                    {
                        AssetExternal childTransform = Resolve(context.Manager, external.file, child);
                        if (childTransform.info == null || Missing(childTransform.baseField)) continue;
                        AssetExternal childGo = Resolve(context.Manager, childTransform.file, childTransform.baseField["m_GameObject"]);
                        if (childGo.info != null) children.Add(ReadGameObject(context, childGo.file, childGo.info.PathId, stack));
                    }
                }
                else if (typeId == (int)AssetClassID.MeshFilter)
                {
                    AssetExternal mesh = Resolve(context.Manager, external.file, fields["m_Mesh"]);
                    if (mesh.info != null) node.MeshIndex = DecodeMesh(context, mesh);
                }
                else if (typeId == (int)AssetClassID.MeshRenderer)
                {
                    node.RendererKind = PreviewRendererKind.Mesh; node.RendererEnabled = Bool(fields["m_Enabled"], true);
                    ReadMaterials(context, external.file, fields, materialIndices, materialNames);
                }
                else if (typeId == (int)AssetClassID.SkinnedMeshRenderer)
                {
                    node.RendererKind = PreviewRendererKind.SkinnedMesh; node.RendererEnabled = Bool(fields["m_Enabled"], true);
                    AssetExternal mesh = Resolve(context.Manager, external.file, fields["m_Mesh"]);
                    if (mesh.info != null) node.MeshIndex = DecodeMesh(context, mesh);
                    ReadMaterials(context, external.file, fields, materialIndices, materialNames);
                    context.Diagnostics.Add("Static skinned-mesh fallback: " + node.Name);
                }
                else if (typeId == (int)AssetClassID.SpriteRenderer)
                {
                    node.RendererKind = PreviewRendererKind.Sprite; node.RendererEnabled = Bool(fields["m_Enabled"], true);
                    AssetExternal sprite = Resolve(context.Manager, external.file, fields["m_Sprite"]);
                    if (sprite.info != null) node.SpriteIndex = DecodeSprite(context, sprite);
                    ReadMaterials(context, external.file, fields, materialIndices, materialNames);
                }
            }
            node.MaterialIndices = materialIndices.ToArray(); node.MaterialNames = materialNames.ToArray();
            node.Children = children.ToArray(); stack.Remove(identity); return node;
        }

        private static int DecodeMesh(DecodeContext context, AssetExternal external)
        {
            string key = Identity(external.file, external.info.PathId);
            if (context.MeshIndexes.TryGetValue(key, out int existing)) return existing;
            int index = context.Meshes.Count; context.MeshIndexes.Add(key, index);
            context.Meshes.Add(PlateUpMeshDecoder.DecodeInstalled(context.GameRoot,
                Path.GetFileName(external.file.path), external.info.PathId, context.Token, context.ClassDataPath));
            return index;
        }

        private static void ReadMaterials(DecodeContext context, AssetsFileInstance instance, AssetTypeValueField renderer,
            ICollection<int> indices, ICollection<string> names)
        {
            foreach (AssetTypeValueField pointer in ArrayChildren(renderer["m_Materials"]))
            {
                AssetExternal material = Resolve(context.Manager, instance, pointer);
                if (material.info == null) { names.Add("Missing"); continue; }
                names.Add(String(material.baseField["m_Name"], "Material " + material.info.PathId));
                indices.Add(DecodeMaterial(context, material));
            }
        }

        private static int DecodeMaterial(DecodeContext context, AssetExternal external)
        {
            string identity = Identity(external.file, external.info.PathId);
            if (context.MaterialIndexes.TryGetValue(identity, out int existing)) return existing;
            AssetTypeValueField fields = external.baseField;
            string shaderName = "Standard";
            AssetExternal shader = Resolve(context.Manager, external.file, fields["m_Shader"]);
            if (shader.info != null) shaderName = String(shader.baseField["m_Name"], "Standard");
            AssetTypeValueField saved = fields["m_SavedProperties"];
            var floats = new List<PreviewMaterialFloatData>(); var colors = new List<PreviewMaterialColorData>();
            var vectors = new List<PreviewMaterialVectorData>(); var textures = new List<PreviewMaterialTextureData>();
            foreach (AssetTypeValueField pair in BoundedArray(saved["m_Floats"], MaximumProperties))
                floats.Add(new PreviewMaterialFloatData(PairName(pair), PairValue(pair).AsFloat));
            foreach (AssetTypeValueField pair in BoundedArray(saved["m_Colors"], MaximumProperties))
            {
                AssetTypeValueField value = PairValue(pair); string name = PairName(pair); Vector4 vector = Vector4Value(value);
                colors.Add(new PreviewMaterialColorData(name, new Color(vector.x, vector.y, vector.z, vector.w)));
                vectors.Add(new PreviewMaterialVectorData(name, vector));
            }
            foreach (AssetTypeValueField pair in BoundedArray(saved["m_TexEnvs"], MaximumProperties))
            {
                AssetTypeValueField value = PairValue(pair); AssetExternal texture = Resolve(context.Manager, external.file, value["m_Texture"]);
                int textureIndex = texture.info == null ? -1 : DecodeTexture(context, texture);
                textures.Add(new PreviewMaterialTextureData(PairName(pair), textureIndex,
                    Vector2Value(value["m_Scale"], Vector2.one), Vector2Value(value["m_Offset"], Vector2.zero)));
            }
            int index = context.Materials.Count; context.MaterialIndexes.Add(identity, index);
            context.Materials.Add(new PreviewMaterialData(String(fields["m_Name"], "Material " + external.info.PathId), shaderName,
                floats.ToArray(), colors.ToArray(), vectors.ToArray(), textures.ToArray()));
            return index;
        }

        private static int DecodeTexture(DecodeContext context, AssetExternal external)
        {
            string identity = Identity(external.file, external.info.PathId);
            if (context.TextureIndexes.TryGetValue(identity, out int existing)) return existing;
            int index = context.Textures.Count; context.TextureIndexes.Add(identity, index);
            PreviewTextureData decoded = PlateUpTextureDecoder.DecodeInstalled(context.GameRoot,
                Path.GetFileName(external.file.path), external.info.PathId, context.Token, context.ClassDataPath);
            context.Textures.Add(external.info.TypeId == (int)AssetClassID.Cubemap
                ? new PreviewTextureData(decoded.Name, decoded.Width, decoded.Height, decoded.Format, decoded.Linear,
                    decoded.Data, decoded.Encoding, true)
                : decoded);
            return index;
        }

        private static int DecodeSprite(DecodeContext context, AssetExternal external)
        {
            string identity = Identity(external.file, external.info.PathId);
            if (context.SpriteIndexes.TryGetValue(identity, out int existing)) return existing;
            AssetTypeValueField fields = external.baseField; AssetTypeValueField renderData = fields["m_RD"];
            AssetExternal texture = Resolve(context.Manager, external.file, renderData["texture"]);
            if (texture.info == null) texture = Resolve(context.Manager, external.file, fields["m_Texture"]);
            int textureIndex = texture.info == null ? -1 : DecodeTexture(context, texture);
            AssetTypeValueField rectField = renderData["textureRect"]; if (Missing(rectField)) rectField = fields["m_Rect"];
            uint settings = Missing(renderData["settingsRaw"]) ? 0 : renderData["settingsRaw"].AsUInt;
            // Unity 2020 SpriteSettings packs rotation into bits 2..5. Bit 6 is mesh type
            // and must not be mistaken for a rotated atlas entry.
            PreviewSpritePackingRotation rotation = ((settings >> 2) & 0xF) == 0 ? PreviewSpritePackingRotation.None : PreviewSpritePackingRotation.Rotate90;
            if (rotation != PreviewSpritePackingRotation.None)
                context.Diagnostics.Add("Packed or rotated sprite is unsupported: " + String(fields["m_Name"], external.info.PathId.ToString()));
            var data = new PreviewSpriteData(String(fields["m_Name"], "Sprite " + external.info.PathId), textureIndex,
                RectValue(rectField), Vector2Value(fields["m_Pivot"], Vector2.zero), Vector4Value(fields["m_Border"]),
                Missing(fields["m_PixelsToUnits"]) ? 100f : fields["m_PixelsToUnits"].AsFloat, rotation);
            int index = context.Sprites.Count; context.SpriteIndexes.Add(identity, index); context.Sprites.Add(data); return index;
        }

        private static AssetTypeValueField Read(AssetsManager manager, AssetsFileInstance instance, long pathId)
        {
            AssetFileInfo info = instance.file.GetAssetInfo(pathId);
            if (info == null) throw new KeyNotFoundException("Object path ID not found: " + pathId);
            return manager.GetBaseField(instance, info, AssetReadFlags.None);
        }
        private static AssetExternal Resolve(AssetsManager manager, AssetsFileInstance instance, AssetTypeValueField pointer)
        {
            if (Missing(pointer)) return default(AssetExternal);
            AssetTypeValueField file = pointer["m_FileID"]; AssetTypeValueField path = pointer["m_PathID"];
            if (Missing(path) || path.AsLong == 0) return default(AssetExternal);
            return manager.GetExtAsset(instance, Missing(file) ? 0 : file.AsInt, path.AsLong, false, AssetReadFlags.None);
        }
        private static IEnumerable<AssetTypeValueField> BoundedArray(AssetTypeValueField field, int maximum)
        {
            AssetTypeValueField[] values = ArrayChildren(field).ToArray();
            if (values.Length > maximum) throw new InvalidDataException("Serialized property count exceeds the preview limit."); return values;
        }
        private static IEnumerable<AssetTypeValueField> ArrayChildren(AssetTypeValueField field)
        {
            if (Missing(field) || field.Children == null) return Enumerable.Empty<AssetTypeValueField>();
            AssetTypeValueField array = field.Children.FirstOrDefault(child => child.FieldName == "Array"); return array?.Children ?? field.Children;
        }
        private static AssetTypeValueField PairValue(AssetTypeValueField pair) => pair["second"] ?? pair.Children.Last();
        private static string PairName(AssetTypeValueField pair) => String(pair["first"] ?? pair.Children.First(), string.Empty);
        private static bool Missing(AssetTypeValueField value) => value == null || value.IsDummy;
        private static bool Bool(AssetTypeValueField value, bool fallback) => Missing(value) ? fallback : value.AsBool;
        private static string String(AssetTypeValueField value, string fallback) => Missing(value) || string.IsNullOrEmpty(value.AsString) ? fallback : value.AsString;
        private static Vector2 Vector2Value(AssetTypeValueField value, Vector2 fallback) => Missing(value) ? fallback : new Vector2(value["x"].AsFloat, value["y"].AsFloat);
        private static Vector3 Vector3Value(AssetTypeValueField value, Vector3 fallback) => Missing(value) ? fallback : new Vector3(value["x"].AsFloat, value["y"].AsFloat, value["z"].AsFloat);
        private static Vector4 Vector4Value(AssetTypeValueField value) => Missing(value) ? Vector4.zero : new Vector4(
            Float(value, "x", "r"), Float(value, "y", "g"), Float(value, "z", "b"), Float(value, "w", "a"));
        private static float Float(AssetTypeValueField value, string first, string second)
        {
            AssetTypeValueField field = value[first]; if (Missing(field)) field = value[second]; return Missing(field) ? 0f : field.AsFloat;
        }
        private static Quaternion QuaternionValue(AssetTypeValueField value) => Missing(value) ? Quaternion.identity : new Quaternion(value["x"].AsFloat, value["y"].AsFloat, value["z"].AsFloat, value["w"].AsFloat);
        private static Rect RectValue(AssetTypeValueField value) => Missing(value) ? new Rect() : new Rect(value["x"].AsFloat, value["y"].AsFloat, value["width"].AsFloat, value["height"].AsFloat);
        private static string Identity(AssetsFileInstance instance, long pathId) => (instance.path ?? instance.name) + "\n" + pathId;

        private sealed class DecodeContext
        {
            public DecodeContext(string gameRoot, string classDataPath, AssetsManager manager,
                IProgress<SnapshotProgress> progress, CancellationToken token)
            { GameRoot = gameRoot; ClassDataPath = classDataPath; Manager = manager; Progress = progress; Token = token; }
            public readonly string GameRoot; public readonly string ClassDataPath; public readonly AssetsManager Manager; public readonly IProgress<SnapshotProgress> Progress; public readonly CancellationToken Token;
            public readonly List<PreviewMeshData> Meshes = new List<PreviewMeshData>(); public readonly Dictionary<string, int> MeshIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            public readonly List<PreviewMaterialData> Materials = new List<PreviewMaterialData>(); public readonly Dictionary<string, int> MaterialIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            public readonly List<PreviewTextureData> Textures = new List<PreviewTextureData>(); public readonly Dictionary<string, int> TextureIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            public readonly List<PreviewSpriteData> Sprites = new List<PreviewSpriteData>(); public readonly Dictionary<string, int> SpriteIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            public readonly List<string> Diagnostics = new List<string>(); public int NodeCount;
        }
    }
}
