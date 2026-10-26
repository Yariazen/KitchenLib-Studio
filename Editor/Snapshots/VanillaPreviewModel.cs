using System;
using UnityEngine;

namespace KitchenLib.Studio.Editor
{
    public enum PreviewRendererKind { None, Mesh, SkinnedMesh, Sprite }
    public enum PreviewSpritePackingRotation { None, FlipHorizontal, FlipVertical, Rotate180, Rotate90 }

    public struct VanillaObjectIdentity
    {
        public VanillaObjectIdentity(string relativePath, long pathId, int classId, string className)
        { RelativePath = relativePath; PathId = pathId; ClassId = classId; ClassName = className; }
        public string RelativePath { get; }
        public long PathId { get; }
        public int ClassId { get; }
        public string ClassName { get; }
    }

    public sealed class PreviewNodeData
    {
        public string Name;
        public bool Active = true;
        public Vector3 LocalPosition;
        public Quaternion LocalRotation = Quaternion.identity;
        public Vector3 LocalScale = Vector3.one;
        public int MeshIndex = -1;
        public int SpriteIndex = -1;
        public PreviewRendererKind RendererKind;
        public bool RendererEnabled = true;
        public int[] MaterialIndices = Array.Empty<int>();
        public string[] MaterialNames = Array.Empty<string>();
        public PreviewNodeData[] Children = Array.Empty<PreviewNodeData>();
    }

    public sealed class VanillaPreviewModel
    {
        public VanillaPreviewModel(PreviewNodeData root, PreviewMeshData[] meshes, string[] diagnostics)
            : this(root, meshes, Array.Empty<PreviewMaterialData>(), Array.Empty<PreviewTextureData>(),
                Array.Empty<PreviewSpriteData>(), diagnostics) { }
        public VanillaPreviewModel(PreviewNodeData root, PreviewMeshData[] meshes, PreviewMaterialData[] materials,
            PreviewTextureData[] textures, PreviewSpriteData[] sprites, string[] diagnostics)
        {
            Root = root; Meshes = meshes ?? Array.Empty<PreviewMeshData>();
            Materials = materials ?? Array.Empty<PreviewMaterialData>(); Textures = textures ?? Array.Empty<PreviewTextureData>();
            Sprites = sprites ?? Array.Empty<PreviewSpriteData>(); Diagnostics = diagnostics ?? Array.Empty<string>();
        }
        public PreviewNodeData Root { get; }
        public PreviewMeshData[] Meshes { get; }
        public PreviewMaterialData[] Materials { get; }
        public PreviewTextureData[] Textures { get; }
        public PreviewSpriteData[] Sprites { get; }
        public string[] Diagnostics { get; }
    }

    public sealed class PreviewMeshData
    {
        public PreviewMeshData(string name, Vector3[] vertices, Vector3[] normals, Vector4[] tangents,
            Vector2[] uv0, int[][] submeshIndices, Bounds bounds)
        {
            Name = name ?? string.Empty;
            Vertices = vertices ?? Array.Empty<Vector3>();
            Normals = normals ?? Array.Empty<Vector3>();
            Tangents = tangents ?? Array.Empty<Vector4>();
            Uv0 = uv0 ?? Array.Empty<Vector2>();
            SubmeshIndices = submeshIndices ?? Array.Empty<int[]>();
            Bounds = bounds;
        }

        public string Name { get; }
        public Vector3[] Vertices { get; }
        public Vector3[] Normals { get; }
        public Vector4[] Tangents { get; }
        public Vector2[] Uv0 { get; }
        public int[][] SubmeshIndices { get; }
        public Bounds Bounds { get; }
    }

    public enum PreviewTextureEncoding { Raw }

    public sealed class PreviewTextureData
    {
        public PreviewTextureData(string name, int width, int height, TextureFormat format, bool linear,
            byte[] data, PreviewTextureEncoding encoding, bool isCubemap = false)
        {
            Name = name ?? string.Empty;
            Width = width;
            Height = height;
            Format = format;
            Linear = linear;
            Data = data ?? Array.Empty<byte>();
            Encoding = encoding;
            IsCubemap = isCubemap;
        }

        public string Name { get; }
        public int Width { get; }
        public int Height { get; }
        public TextureFormat Format { get; }
        public bool Linear { get; }
        public byte[] Data { get; }
        public PreviewTextureEncoding Encoding { get; }
        public bool IsCubemap { get; }
    }

    public sealed class PreviewMaterialFloatData
    {
        public PreviewMaterialFloatData(string name, float value) { Name = name ?? string.Empty; Value = value; }
        public string Name { get; } public float Value { get; }
    }

    public sealed class PreviewMaterialColorData
    {
        public PreviewMaterialColorData(string name, Color value) { Name = name ?? string.Empty; Value = value; }
        public string Name { get; } public Color Value { get; }
    }

    public sealed class PreviewMaterialVectorData
    {
        public PreviewMaterialVectorData(string name, Vector4 value) { Name = name ?? string.Empty; Value = value; }
        public string Name { get; } public Vector4 Value { get; }
    }

    public sealed class PreviewMaterialTextureData
    {
        public PreviewMaterialTextureData(string name, int textureIndex, Vector2 scale, Vector2 offset)
        { Name = name ?? string.Empty; TextureIndex = textureIndex; Scale = scale; Offset = offset; }
        public string Name { get; } public int TextureIndex { get; } public Vector2 Scale { get; } public Vector2 Offset { get; }
    }

    public sealed class PreviewMaterialData
    {
        public PreviewMaterialData(string name, string shaderName, PreviewMaterialFloatData[] floats,
            PreviewMaterialColorData[] colors, PreviewMaterialVectorData[] vectors, PreviewMaterialTextureData[] textures)
        {
            Name = name ?? string.Empty; ShaderName = shaderName ?? string.Empty;
            Floats = floats ?? Array.Empty<PreviewMaterialFloatData>(); Colors = colors ?? Array.Empty<PreviewMaterialColorData>();
            Vectors = vectors ?? Array.Empty<PreviewMaterialVectorData>(); Textures = textures ?? Array.Empty<PreviewMaterialTextureData>();
        }
        public string Name { get; } public string ShaderName { get; }
        public PreviewMaterialFloatData[] Floats { get; } public PreviewMaterialColorData[] Colors { get; }
        public PreviewMaterialVectorData[] Vectors { get; } public PreviewMaterialTextureData[] Textures { get; }
    }

    public sealed class PreviewSpriteData
    {
        public PreviewSpriteData(string name, int textureIndex, Rect rect, Vector2 pivot, Vector4 border,
            float pixelsPerUnit, PreviewSpritePackingRotation packingRotation)
        {
            Name = name ?? string.Empty; TextureIndex = textureIndex; Rect = rect; Pivot = pivot; Border = border;
            PixelsPerUnit = pixelsPerUnit; PackingRotation = packingRotation;
        }
        public string Name { get; } public int TextureIndex { get; } public Rect Rect { get; }
        public Vector2 Pivot { get; } public Vector4 Border { get; } public float PixelsPerUnit { get; }
        public PreviewSpritePackingRotation PackingRotation { get; }
    }
}
