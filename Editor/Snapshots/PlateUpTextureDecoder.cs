using System;
using System.IO;
using System.Threading;
using AssetsTools.NET;
using UnityEngine;

namespace KitchenLib.Studio.Editor
{
    public static class PlateUpTextureDecoder
    {
        public static PreviewTextureData DecodeInstalled(string gameRoot, string relativeAssetPath, long pathId,
            CancellationToken cancellationToken)
        {
            string classDataPath = KitchenLibStudioGeneratedPaths.PackageFilePath(
                "Editor/ThirdParty/AssetsTools.NET/classdata.tpk");
            return DecodeInstalled(gameRoot, relativeAssetPath, pathId, cancellationToken, classDataPath);
        }

        public static PreviewTextureData DecodeInstalled(string gameRoot, string relativeAssetPath, long pathId,
            CancellationToken cancellationToken, string classDataPath)
        {
            string dataRoot = PlateUpPlatformLayout.ResolveLayout(gameRoot).DataRoot;
            return PlateUpMeshDecoder.WithField(gameRoot, relativeAssetPath, pathId, cancellationToken, classDataPath,
                fields => Decode(fields, dataRoot, cancellationToken));
        }

        private static PreviewTextureData Decode(AssetTypeValueField fields, string dataRoot, CancellationToken cancellationToken)
        {
            string name = fields["m_Name"].AsString;
            int width = fields["m_Width"].AsInt;
            int height = fields["m_Height"].AsInt;
            if (width <= 0 || height <= 0 || width > 32768 || height > 32768)
                throw new InvalidDataException("Texture dimensions are outside preview limits.");
            int formatValue = fields["m_TextureFormat"].AsInt;
            if (!Enum.IsDefined(typeof(TextureFormat), formatValue))
                throw new NotSupportedException("Preview texture format is not supported: " + formatValue);
            bool linear = !fields["m_ColorSpace"].IsDummy && fields["m_ColorSpace"].AsInt == 0;
            AssetTypeValueField stream = fields["m_StreamData"];
            byte[] data;
            if (!stream.IsDummy && !string.IsNullOrEmpty(stream["path"].AsString) && stream["size"].AsUInt > 0)
            {
                data = PlateUpSerializedResourceReader.Read(dataRoot, stream["path"].AsString,
                    checked((long)stream["offset"].AsULong), checked((int)stream["size"].AsUInt), cancellationToken);
            }
            else
            {
                AssetTypeValueField image = fields["image data"];
                if (image.IsDummy) image = fields["m_ImageData"];
                data = image.AsByteArray;
            }
            if (data == null || data.Length == 0) throw new InvalidDataException("Texture pixel data is empty.");
            return new PreviewTextureData(name, width, height, (TextureFormat)formatValue, linear, data, PreviewTextureEncoding.Raw);
        }
    }
}
