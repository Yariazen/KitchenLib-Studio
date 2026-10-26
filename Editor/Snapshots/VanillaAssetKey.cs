using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace KitchenLib.Studio.Editor
{
    public enum VanillaAssetKind { Gdo, GameObject, Transform, MeshFilter, Renderer, Material, Shader, Texture2D, Cubemap, Sprite, Mesh }

    public static class VanillaAssetKey
    {
        public static string Generate(string provider, VanillaAssetKind kind, string displayName,
            IEnumerable<string> qualifiers, long pathId)
        {
            string prefix = Slug(provider);
            if (string.IsNullOrEmpty(prefix)) throw new ArgumentException("Provider must produce a key segment.", nameof(provider));
            string name = Slug(displayName);
            string suffix = name;
            if (string.IsNullOrEmpty(suffix)) suffix = "unnamed-" + pathId.ToString("x", CultureInfo.InvariantCulture);
            else
            {
                string qualifier = (qualifiers ?? Enumerable.Empty<string>()).Select(Slug).FirstOrDefault(value => !string.IsNullOrEmpty(value));
                if (!string.IsNullOrEmpty(qualifier)) suffix += "/" + qualifier;
            }
            return prefix + "/" + KindSegment(kind) + "/" + suffix;
        }

        private static string KindSegment(VanillaAssetKind kind)
        {
            switch (kind)
            {
                case VanillaAssetKind.GameObject: return "game-object";
                case VanillaAssetKind.Texture2D: return "texture-2d";
                case VanillaAssetKind.MeshFilter: return "mesh-filter";
                default: return Slug(kind.ToString());
            }
        }

        private static string Slug(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            string normalized = value.Normalize(NormalizationForm.FormD);
            var result = new StringBuilder(normalized.Length);
            bool separator = false;
            foreach (char character in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
                if (char.IsLetterOrDigit(character))
                {
                    if (separator && result.Length != 0) result.Append('-');
                    result.Append(char.ToLowerInvariant(character)); separator = false;
                }
                else separator = true;
            }
            return result.ToString().Trim('-');
        }
    }
}
