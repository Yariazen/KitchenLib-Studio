using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using UnityEngine;

namespace KitchenLib.Studio.Editor
{
    public static class PlateUpMeshDecoder
    {
        private const int MaximumVertices = 10_000_000;
        private const int MaximumIndices = 30_000_000;

        public static PreviewMeshData DecodeInstalled(string gameRoot, string relativeAssetPath, long pathId,
            CancellationToken cancellationToken)
        {
            string classDataPath = KitchenLibStudioGeneratedPaths.PackageFilePath(
                "Editor/ThirdParty/AssetsTools.NET/classdata.tpk");
            return DecodeInstalled(gameRoot, relativeAssetPath, pathId, cancellationToken, classDataPath);
        }

        public static PreviewMeshData DecodeInstalled(string gameRoot, string relativeAssetPath, long pathId,
            CancellationToken cancellationToken, string classDataPath)
        {
            string dataRoot = PlateUpPlatformLayout.ResolveLayout(gameRoot).DataRoot;
            return WithField(gameRoot, relativeAssetPath, pathId, cancellationToken, classDataPath,
                fields => Decode(fields, dataRoot, cancellationToken));
        }

        private static PreviewMeshData Decode(AssetTypeValueField fields, string dataRoot, CancellationToken cancellationToken)
        {
            string name = fields["m_Name"].AsString;
            AssetTypeValueField compressed = fields["m_CompressedMesh"];
            float[] packedVertices = UnpackFloats(compressed["m_Vertices"]);
            Vector3[] vertices;
            Vector3[] normals;
            Vector4[] tangents;
            Vector2[] uv0;
            byte[] rawIndices;
            if (packedVertices.Length != 0)
            {
                if (packedVertices.Length % 3 != 0) throw new InvalidDataException("Compressed vertex count is invalid.");
                vertices = new Vector3[packedVertices.Length / 3];
                for (int index = 0; index < vertices.Length; index++)
                    vertices[index] = new Vector3(packedVertices[index * 3], packedVertices[index * 3 + 1], packedVertices[index * 3 + 2]);
                normals = Array.Empty<Vector3>(); tangents = Array.Empty<Vector4>(); uv0 = Array.Empty<Vector2>();
                rawIndices = Array.Empty<byte>();
            }
            else
            {
                DecodeVertexData(fields, dataRoot, cancellationToken, out vertices, out normals, out tangents, out uv0);
                rawIndices = ReadBytes(fields["m_IndexBuffer"]);
            }
            int vertexCount = vertices.Length;
            if (vertexCount == 0) throw new InvalidDataException("Mesh has no vertices.");
            if (vertexCount > MaximumVertices) throw new InvalidDataException("Mesh vertex count exceeds the preview limit.");

            uint[] packedIndices = UnpackInts(compressed["m_Triangles"]);
            int indexSize = !fields["m_IndexFormat"].IsDummy && fields["m_IndexFormat"].AsInt != 0 ? 4 : 2;
            if (packedIndices.Length == 0)
            {
                if (rawIndices.Length % indexSize != 0) throw new InvalidDataException("Mesh index buffer is truncated.");
                packedIndices = new uint[rawIndices.Length / indexSize];
                for (int index = 0; index < packedIndices.Length; index++)
                    packedIndices[index] = indexSize == 2
                        ? BitConverter.ToUInt16(rawIndices, index * 2)
                        : BitConverter.ToUInt32(rawIndices, index * 4);
            }
            if (packedIndices.Length > MaximumIndices) throw new InvalidDataException("Mesh index count exceeds the preview limit.");
            AssetTypeValueField[] submeshes = ArrayChildren(fields["m_SubMeshes"]).ToArray();
            var indices = new int[submeshes.Length][];
            for (int submeshIndex = 0; submeshIndex < submeshes.Length; submeshIndex++)
            {
                AssetTypeValueField submesh = submeshes[submeshIndex];
                int start = checked((int)(submesh["firstByte"].AsUInt / indexSize));
                int count = checked((int)submesh["indexCount"].AsUInt);
                if (start < 0 || count < 0 || start > packedIndices.Length - count)
                    throw new InvalidDataException("Submesh index range is outside the decoded index buffer: start=" +
                        start + ", count=" + count + ", available=" + packedIndices.Length + ", rawBytes=" + rawIndices.Length + ".");
                indices[submeshIndex] = new int[count];
                for (int index = 0; index < count; index++)
                {
                    uint value = packedIndices[start + index];
                    if (value >= vertexCount) throw new InvalidDataException("Mesh index is outside the vertex buffer.");
                    indices[submeshIndex][index] = (int)value;
                }
            }

            AssetTypeValueField boundsField = fields["m_LocalAABB"];
            Vector3 center = ReadVector3(boundsField["m_Center"]);
            Vector3 extents = ReadVector3(boundsField["m_Extent"]);
            return new PreviewMeshData(name, vertices, normals, tangents, uv0, indices, new Bounds(center, extents * 2f));
        }

        private static Vector3 ReadVector3(AssetTypeValueField field) =>
            new Vector3(field["x"].AsFloat, field["y"].AsFloat, field["z"].AsFloat);

        private static void DecodeVertexData(AssetTypeValueField fields, string dataRoot, CancellationToken cancellationToken,
            out Vector3[] vertices, out Vector3[] normals, out Vector4[] tangents, out Vector2[] uv0)
        {
            AssetTypeValueField vertexData = fields["m_VertexData"];
            int count = checked((int)vertexData["m_VertexCount"].AsUInt);
            if (count <= 0 || count > MaximumVertices) throw new InvalidDataException("Mesh vertex count is outside preview limits.");
            byte[] bytes = ReadBytes(vertexData["m_DataSize"]);
            AssetTypeValueField stream = fields["m_StreamData"];
            if (bytes.Length == 0 && stream != null && !stream.IsDummy && stream["size"].AsUInt > 0)
                bytes = PlateUpSerializedResourceReader.Read(dataRoot, stream["path"].AsString,
                    checked((long)stream["offset"].AsULong), checked((int)stream["size"].AsUInt), cancellationToken);
            if (bytes.Length == 0) throw new InvalidDataException("Mesh vertex data is empty.");

            AssetTypeValueField[] channels = ArrayChildren(vertexData["m_Channels"]).ToArray();
            int streamCount = channels.Where(channel => (channel["dimension"].AsByte & 0x0F) != 0)
                .Select(channel => channel["stream"].AsByte + 1).DefaultIfEmpty(0).Max();
            var strides = new int[streamCount];
            var streamOffsets = new int[streamCount];
            foreach (AssetTypeValueField channel in channels)
            {
                int dimension = channel["dimension"].AsByte & 0x0F;
                if (dimension == 0) continue;
                int streamIndex = channel["stream"].AsByte;
                strides[streamIndex] += dimension * ComponentSize(channel["format"].AsByte);
            }
            int running = 0;
            for (int index = 0; index < streamCount; index++)
            {
                streamOffsets[index] = running;
                running = checked(running + count * strides[index]);
                running = (running + 15) & ~15;
            }
            if (running > bytes.Length + 15) throw new InvalidDataException("Mesh vertex streams exceed the serialized buffer.");

            vertices = new Vector3[count]; normals = Array.Empty<Vector3>(); tangents = Array.Empty<Vector4>(); uv0 = Array.Empty<Vector2>();
            for (int channelIndex = 0; channelIndex < channels.Length; channelIndex++)
            {
                AssetTypeValueField channel = channels[channelIndex];
                int dimension = channel["dimension"].AsByte & 0x0F;
                if (dimension == 0 || (channelIndex != 0 && channelIndex != 1 && channelIndex != 2 && channelIndex != 4)) continue;
                int format = channel["format"].AsByte;
                int componentSize = ComponentSize(format);
                int streamIndex = channel["stream"].AsByte;
                int channelOffset = channel["offset"].AsByte;
                if (channelIndex == 1) normals = new Vector3[count];
                if (channelIndex == 2) tangents = new Vector4[count];
                if (channelIndex == 4) uv0 = new Vector2[count];
                for (int vertex = 0; vertex < count; vertex++)
                {
                    int offset = checked(streamOffsets[streamIndex] + vertex * strides[streamIndex] + channelOffset);
                    if (offset < 0 || offset + dimension * componentSize > bytes.Length)
                        throw new InvalidDataException("Mesh channel exceeds the serialized vertex buffer.");
                    float x = ReadComponent(bytes, offset, format);
                    float y = dimension > 1 ? ReadComponent(bytes, offset + componentSize, format) : 0f;
                    float z = dimension > 2 ? ReadComponent(bytes, offset + componentSize * 2, format) : 0f;
                    float w = dimension > 3 ? ReadComponent(bytes, offset + componentSize * 3, format) : 0f;
                    if (channelIndex == 0) vertices[vertex] = new Vector3(x, y, z);
                    else if (channelIndex == 1) normals[vertex] = new Vector3(x, y, z);
                    else if (channelIndex == 2) tangents[vertex] = new Vector4(x, y, z, w);
                    else if (channelIndex == 4) uv0[vertex] = new Vector2(x, y);
                }
            }
        }

        private static IEnumerable<AssetTypeValueField> ArrayChildren(AssetTypeValueField field)
        {
            if (field?.Children == null) return Enumerable.Empty<AssetTypeValueField>();
            AssetTypeValueField array = field.Children.FirstOrDefault(child => string.Equals(child.FieldName, "Array", StringComparison.Ordinal));
            return array?.Children ?? field.Children;
        }

        private static int ComponentSize(int format)
        {
            if (format == 0 || format == 10 || format == 11) return 4;
            if (format == 1 || format == 4 || format == 5 || format == 8 || format == 9) return 2;
            if (format >= 2 && format <= 7) return 1;
            throw new NotSupportedException("Unsupported Unity vertex format: " + format);
        }

        private static float ReadComponent(byte[] data, int offset, int format)
        {
            switch (format)
            {
                case 0: return BitConverter.ToSingle(data, offset);
                case 1: return HalfToSingle(BitConverter.ToUInt16(data, offset));
                case 2: return data[offset] / 255f;
                case 3: return Math.Max(-1f, (sbyte)data[offset] / 127f);
                case 4: return BitConverter.ToUInt16(data, offset) / 65535f;
                case 5: return Math.Max(-1f, BitConverter.ToInt16(data, offset) / 32767f);
                case 6: return data[offset];
                case 7: return (sbyte)data[offset];
                case 8: return BitConverter.ToUInt16(data, offset);
                case 9: return BitConverter.ToInt16(data, offset);
                case 10: return BitConverter.ToUInt32(data, offset);
                case 11: return BitConverter.ToInt32(data, offset);
                default: throw new NotSupportedException("Unsupported Unity vertex format: " + format);
            }
        }

        private static float HalfToSingle(ushort value)
        {
            uint sign = (uint)(value & 0x8000) << 16;
            uint exponent = (uint)(value >> 10) & 0x1F;
            uint mantissa = (uint)value & 0x03FF;
            uint bits;
            if (exponent == 0)
            {
                if (mantissa == 0) bits = sign;
                else
                {
                    exponent = 1;
                    while ((mantissa & 0x0400) == 0) { mantissa <<= 1; exponent--; }
                    mantissa &= 0x03FF;
                    bits = sign | ((exponent + 112) << 23) | (mantissa << 13);
                }
            }
            else if (exponent == 31) bits = sign | 0x7F800000 | (mantissa << 13);
            else bits = sign | ((exponent + 112) << 23) | (mantissa << 13);
            return BitConverter.ToSingle(BitConverter.GetBytes(bits), 0);
        }

        private static float[] UnpackFloats(AssetTypeValueField packed)
        {
            int count = checked((int)packed["m_NumItems"].AsUInt);
            int bitSize = packed["m_BitSize"].AsByte;
            float start = packed["m_Start"].AsFloat;
            float range = packed["m_Range"].AsFloat;
            return DecodePackedFloats(count, bitSize, start, range, ReadBytes(Child(packed, "m_Data")));
        }

        public static float[] DecodePackedFloats(int count, int bitSize, float start, float range, byte[] data)
        {
            if (count < 0 || count > MaximumVertices * 4) throw new InvalidDataException("Packed float count exceeds the preview limit.");
            if (bitSize < 0 || bitSize > 32) throw new InvalidDataException("Packed bit size is invalid.");
            long requiredBits = checked((long)bitSize * count);
            if (requiredBits > (long)(data?.Length ?? 0) * 8) throw new InvalidDataException("Packed data is truncated.");
            uint[] values = UnpackBits(data, bitSize, count);
            var result = new float[count];
            double scale = bitSize == 0 ? 0d : range / ((1UL << bitSize) - 1UL);
            for (int index = 0; index < count; index++) result[index] = bitSize == 0 ? start : (float)(values[index] * scale + start);
            return result;
        }

        private static uint[] UnpackInts(AssetTypeValueField packed)
        {
            int count = checked((int)packed["m_NumItems"].AsUInt);
            if (count < 0 || count > MaximumIndices) throw new InvalidDataException("Packed integer count exceeds the preview limit.");
            return UnpackBits(ReadBytes(Child(packed, "m_Data")), packed["m_BitSize"].AsByte, count);
        }

        private static AssetTypeValueField Child(AssetTypeValueField parent, string name)
        {
            AssetTypeValueField value = parent[name];
            return value ?? parent.Children?.FirstOrDefault(child => string.Equals(child.FieldName, name, StringComparison.Ordinal));
        }

        private static byte[] ReadBytes(AssetTypeValueField field)
        {
            if (field == null) throw new InvalidDataException("Packed byte field is missing.");
            var result = new List<byte>();
            CollectBytes(field, result);
            return result.ToArray();
        }

        private static void CollectBytes(AssetTypeValueField field, ICollection<byte> result)
        {
            if (field == null || string.Equals(field.FieldName, "size", StringComparison.OrdinalIgnoreCase)) return;
            if (field.Value != null)
            {
                byte[] bytes = field.AsByteArray;
                if (bytes != null) { foreach (byte value in bytes) result.Add(value); return; }
                if (field.Children == null || field.Children.Count == 0) { result.Add(field.AsByte); return; }
            }
            if (field.Children == null) return;
            foreach (AssetTypeValueField child in field.Children) CollectBytes(child, result);
        }

        private static uint[] UnpackBits(byte[] data, int bitSize, int count)
        {
            if (bitSize < 0 || bitSize > 32) throw new InvalidDataException("Packed bit size is invalid.");
            long requiredBits = checked((long)bitSize * count);
            if (requiredBits > (long)(data?.Length ?? 0) * 8) throw new InvalidDataException("Packed data is truncated.");
            var result = new uint[count];
            long bitPosition = 0;
            for (int item = 0; item < count; item++)
            {
                uint value = 0;
                for (int bit = 0; bit < bitSize; bit++, bitPosition++)
                    if ((data[bitPosition >> 3] & (1 << (int)(bitPosition & 7))) != 0) value |= 1u << bit;
                result[item] = value;
            }
            return result;
        }

        internal static T WithField<T>(string gameRoot, string relativeAssetPath, long pathId,
            CancellationToken cancellationToken, Func<AssetTypeValueField, T> decode)
        {
            string classDataPath = KitchenLibStudioGeneratedPaths.PackageFilePath(
                "Editor/ThirdParty/AssetsTools.NET/classdata.tpk");
            return WithField(gameRoot, relativeAssetPath, pathId, cancellationToken, classDataPath, decode);
        }

        internal static T WithField<T>(string gameRoot, string relativeAssetPath, long pathId,
            CancellationToken cancellationToken, string classDataPath, Func<AssetTypeValueField, T> decode)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string dataRoot = PlateUpPlatformLayout.ResolveLayout(gameRoot).DataRoot;
            string assetPath = Path.Combine(dataRoot, relativeAssetPath);
            var manager = new AssetsManager();
            try
            {
                manager.LoadClassPackage(classDataPath);
                AssetsFileInstance instance = manager.LoadAssetsFile(assetPath, false);
                manager.LoadClassDatabaseFromPackage(instance.file.Metadata.UnityVersion);
                AssetFileInfo info = instance.file.GetAssetInfo(pathId);
                if (info == null) throw new KeyNotFoundException("Asset path ID was not found: " + pathId);
                AssetTypeValueField fields = manager.GetBaseField(instance, info, AssetReadFlags.None);
                if (fields == null || fields.IsDummy) throw new InvalidDataException("Serialized fields could not be read.");
                return decode(fields);
            }
            finally { manager.UnloadAll(true); }
        }
    }
}
