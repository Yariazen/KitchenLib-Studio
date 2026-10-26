using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace KitchenLib.Studio.Editor
{
    public sealed class ReconstructedPreview : IDisposable
    {
        private readonly List<UnityEngine.Object> _owned;
        internal ReconstructedPreview(GameObject root, List<UnityEngine.Object> owned) { Root = root; _owned = owned; }
        public GameObject Root { get; private set; }
        public void Dispose()
        {
            for (int index = _owned.Count - 1; index >= 0; index--)
                if (_owned[index] != null) UnityEngine.Object.DestroyImmediate(_owned[index]);
            _owned.Clear(); Root = null;
        }
    }

    public static class VanillaPreviewReconstructor
    {
        public static ReconstructedPreview Reconstruct(VanillaPreviewModel model)
        {
            if (model?.Root == null) throw new ArgumentNullException(nameof(model));
            var owned = new List<UnityEngine.Object>();
            try
            {
                var unityMeshes = new Mesh[model.Meshes.Length];
                for (int index = 0; index < unityMeshes.Length; index++)
                {
                    PreviewMeshData data = model.Meshes[index];
                    var mesh = new Mesh { name = data.Name, hideFlags = HideFlags.HideAndDontSave };
                    owned.Add(mesh);
                    mesh.vertices = data.Vertices;
                    if (data.Normals.Length == data.Vertices.Length) mesh.normals = data.Normals; else mesh.RecalculateNormals();
                    if (data.Tangents.Length == data.Vertices.Length) mesh.tangents = data.Tangents;
                    if (data.Uv0.Length == data.Vertices.Length) mesh.uv = data.Uv0;
                    mesh.subMeshCount = data.SubmeshIndices.Length;
                    for (int submesh = 0; submesh < data.SubmeshIndices.Length; submesh++)
                    {
                        foreach (int vertexIndex in data.SubmeshIndices[submesh])
                            if (vertexIndex < 0 || vertexIndex >= data.Vertices.Length)
                                throw new System.IO.InvalidDataException("Preview mesh index is outside the vertex buffer.");
                        mesh.SetTriangles(data.SubmeshIndices[submesh], submesh);
                    }
                    mesh.bounds = data.Bounds;
                    unityMeshes[index] = mesh;
                }
                Texture[] textures = BuildTextures(model.Textures, owned);
                Material[] materials = BuildMaterials(model.Materials, textures, owned);
                Sprite[] sprites = BuildSprites(model.Sprites, textures, owned);
                GameObject root = Build(model.Root, null, unityMeshes, materials, sprites, owned);
                owned.Insert(0, root);
                return new ReconstructedPreview(root, owned);
            }
            catch
            {
                for (int index = owned.Count - 1; index >= 0; index--) if (owned[index] != null) UnityEngine.Object.DestroyImmediate(owned[index]);
                throw;
            }
        }

        private static Texture[] BuildTextures(PreviewTextureData[] source, List<UnityEngine.Object> owned)
        {
            var result = new Texture[source.Length];
            for (int index = 0; index < source.Length; index++)
            {
                PreviewTextureData data = source[index];
                Texture texture;
                if (data.IsCubemap)
                {
                    if (data.Width != data.Height) throw new System.IO.InvalidDataException("Cubemap faces must be square.");
                    var cubemap = new Cubemap(data.Width, data.Format, false) { name = data.Name, hideFlags = HideFlags.HideAndDontSave };
                    if (data.Data.Length % 6 != 0) throw new System.IO.InvalidDataException("Cubemap pixel data does not contain six equal faces.");
                    int faceSize = data.Data.Length / 6;
                    for (int face = 0; face < 6; face++)
                        cubemap.SetPixelData(data.Data, 0, (CubemapFace)face, face * faceSize);
                    cubemap.Apply(false, true); texture = cubemap;
                }
                else
                {
                    var texture2D = new Texture2D(data.Width, data.Height, data.Format, false, data.Linear)
                        { name = data.Name, hideFlags = HideFlags.HideAndDontSave };
                    texture2D.LoadRawTextureData(data.Data); texture2D.Apply(false, true); texture = texture2D;
                }
                owned.Add(texture); result[index] = texture;
            }
            return result;
        }

        private static Material[] BuildMaterials(PreviewMaterialData[] source, Texture[] textures, List<UnityEngine.Object> owned)
        {
            var result = new Material[source.Length];
            for (int index = 0; index < source.Length; index++)
            {
                PreviewMaterialData data = source[index];
                Shader shader = Shader.Find(data.ShaderName) ?? Shader.Find("Standard") ?? Shader.Find("Hidden/InternalErrorShader");
                var material = new Material(shader) { name = data.Name, hideFlags = HideFlags.HideAndDontSave };
                owned.Add(material);
                foreach (PreviewMaterialFloatData value in data.Floats) if (material.HasProperty(value.Name)) material.SetFloat(value.Name, value.Value);
                foreach (PreviewMaterialColorData value in data.Colors) if (material.HasProperty(value.Name)) material.SetColor(value.Name, value.Value);
                foreach (PreviewMaterialVectorData value in data.Vectors) if (material.HasProperty(value.Name)) material.SetVector(value.Name, value.Value);
                foreach (PreviewMaterialTextureData value in data.Textures)
                {
                    if (!material.HasProperty(value.Name) || value.TextureIndex < 0 || value.TextureIndex >= textures.Length) continue;
                    material.SetTexture(value.Name, textures[value.TextureIndex]);
                    material.SetTextureScale(value.Name, value.Scale); material.SetTextureOffset(value.Name, value.Offset);
                }
                ApplyPlateUpFallbackProperties(material, data);
                result[index] = material;
            }
            return result;
        }

        private static void ApplyPlateUpFallbackProperties(Material material, PreviewMaterialData data)
        {
            PreviewMaterialColorData color0 = data.Colors.FirstOrDefault(value =>
                string.Equals(value.Name, "_Color0", StringComparison.Ordinal));
            if (color0 != null && material.HasProperty("_Color"))
            {
                Color color = color0.Value;
                if (color.a <= 0f) color.a = 1f;
                material.SetColor("_Color", color);
            }
            CopyFloat(data, "_Shininess", material, "_Glossiness");
            CopyFloat(data, "_Specular", material, "_Metallic");
            if (data.Name.IndexOf("Transparent", StringComparison.OrdinalIgnoreCase) < 0) return;

            Color transparent = color0 == null ? new Color(0.18f, 0.62f, 1f, 0.35f) : material.color;
            if (transparent.a <= 0f || transparent.a >= 1f) transparent.a = 0.35f;
            if (material.HasProperty("_Color")) material.SetColor("_Color", transparent);
            if (material.HasProperty("_Mode")) material.SetFloat("_Mode", 3f);
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        private static void CopyFloat(PreviewMaterialData data, string sourceName, Material material,
            string targetName)
        {
            PreviewMaterialFloatData value = data.Floats.FirstOrDefault(item =>
                string.Equals(item.Name, sourceName, StringComparison.Ordinal));
            if (value != null && material.HasProperty(targetName)) material.SetFloat(targetName, value.Value);
        }

        private static Sprite[] BuildSprites(PreviewSpriteData[] source, Texture[] textures, List<UnityEngine.Object> owned)
        {
            var result = new Sprite[source.Length];
            for (int index = 0; index < source.Length; index++)
            {
                PreviewSpriteData data = source[index];
                if (data.PackingRotation != PreviewSpritePackingRotation.None)
                    throw new System.IO.InvalidDataException("Packed or rotated sprites cannot be reconstructed safely.");
                if (data.TextureIndex < 0 || data.TextureIndex >= textures.Length)
                    throw new System.IO.InvalidDataException("Sprite texture index is outside the texture table.");
                Vector2 normalizedPivot = new Vector2(data.Pivot.x / data.Rect.width, data.Pivot.y / data.Rect.height);
                Texture2D texture = textures[data.TextureIndex] as Texture2D;
                if (texture == null) throw new System.IO.InvalidDataException("Sprite texture is not a Texture2D.");
                Sprite sprite = Sprite.Create(texture, data.Rect, normalizedPivot,
                    data.PixelsPerUnit <= 0 ? 100f : data.PixelsPerUnit, 0, SpriteMeshType.FullRect, data.Border);
                sprite.name = data.Name; sprite.hideFlags = HideFlags.HideAndDontSave; owned.Add(sprite); result[index] = sprite;
            }
            return result;
        }

        private static GameObject Build(PreviewNodeData data, Transform parent, Mesh[] meshes, Material[] materials,
            Sprite[] sprites, List<UnityEngine.Object> owned)
        {
            var result = new GameObject(data.Name) { hideFlags = HideFlags.HideAndDontSave };
            if (parent != null) result.transform.SetParent(parent, false);
            result.transform.localPosition = data.LocalPosition; result.transform.localRotation = data.LocalRotation; result.transform.localScale = data.LocalScale;
            result.SetActive(data.Active);
            if (data.RendererKind == PreviewRendererKind.Sprite)
            {
                if (data.SpriteIndex < 0 || data.SpriteIndex >= sprites.Length)
                    throw new System.IO.InvalidDataException("Preview sprite index is outside the sprite table.");
                SpriteRenderer spriteRenderer = result.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = sprites[data.SpriteIndex]; spriteRenderer.enabled = data.RendererEnabled;
                Material[] assigned = ResolveMaterials(data, materials);
                if (assigned.Length != 0) spriteRenderer.sharedMaterials = assigned;
            }
            else if (data.MeshIndex >= 0)
            {
                if (data.MeshIndex >= meshes.Length) throw new System.IO.InvalidDataException("Preview mesh index is outside the mesh table.");
                result.AddComponent<MeshFilter>().sharedMesh = meshes[data.MeshIndex];
                MeshRenderer renderer = result.AddComponent<MeshRenderer>();
                renderer.enabled = data.RendererEnabled;
                Material[] assigned = ResolveMaterials(data, materials);
                if (assigned.Length == 0)
                {
                    int count = Math.Max(1, data.MaterialNames.Length); assigned = new Material[count];
                    Shader shader = Shader.Find("Standard") ?? Shader.Find("Hidden/InternalErrorShader");
                    for (int index = 0; index < count; index++)
                    {
                        var material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave,
                            name = index < data.MaterialNames.Length ? data.MaterialNames[index] : "Preview Material" };
                        material.color = material.name.IndexOf("Transparent", StringComparison.OrdinalIgnoreCase) >= 0
                            ? new Color(0.18f, 0.62f, 1f, 0.7f) : new Color(0.82f, 0.84f, 0.88f, 1f);
                        assigned[index] = material; owned.Add(material);
                    }
                }
                renderer.sharedMaterials = assigned;
            }
            foreach (PreviewNodeData child in data.Children) Build(child, result.transform, meshes, materials, sprites, owned);
            return result;
        }

        private static Material[] ResolveMaterials(PreviewNodeData data, Material[] materials)
        {
            var result = new Material[data.MaterialIndices.Length];
            for (int index = 0; index < result.Length; index++)
            {
                int materialIndex = data.MaterialIndices[index];
                if (materialIndex < 0 || materialIndex >= materials.Length)
                    throw new System.IO.InvalidDataException("Preview material index is outside the material table.");
                result[index] = materials[materialIndex];
            }
            return result;
        }
    }
}
