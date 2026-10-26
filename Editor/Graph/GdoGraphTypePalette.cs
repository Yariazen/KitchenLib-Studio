using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace KitchenLib.Studio.Editor.Graph
{
    internal static class GdoGraphTypePalette
    {
        private static readonly IReadOnlyDictionary<string, Color> Common =
            new Dictionary<string, Color>(StringComparer.Ordinal)
            {
                ["KitchenData.Item"] = Rgb(230, 159, 0),
                ["KitchenData.ItemGroup"] = Rgb(240, 200, 8),
                ["KitchenData.Process"] = Rgb(0, 158, 115),
                ["KitchenData.Dish"] = Rgb(213, 94, 0),
                ["KitchenData.Appliance"] = Rgb(0, 114, 178),
                ["UnityEngine.GameObject"] = Rgb(86, 180, 233),
                ["KitchenData.PlayerCosmetic"] = Rgb(204, 121, 167)
            };

        public static Color UnresolvedColor { get; } = Rgb(145, 150, 158);
        public static Color InvalidColor { get; } = Rgb(218, 72, 72);

        public static IReadOnlyDictionary<string, Color> Build(IEnumerable<string> runtimeTypes)
        {
            string[] types = (runtimeTypes ?? Enumerable.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim()).Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal).ToArray();
            var result = new Dictionary<string, Color>(StringComparer.Ordinal);
            var used = new HashSet<Color32>(Common.Values.Select(value => (Color32)value));
            foreach (string type in types)
            {
                if (Common.TryGetValue(type, out Color common))
                {
                    result[type] = common;
                    continue;
                }
                uint hash = Fnv1A(type);
                float hue = (hash % 3600) / 3600f;
                Color candidate = Color.HSVToRGB(hue, 0.62f, 0.90f);
                while (!used.Add((Color32)candidate))
                {
                    hue = (hue + 0.61803398875f) % 1f;
                    candidate = Color.HSVToRGB(hue, 0.62f, 0.90f);
                }
                result[type] = candidate;
            }
            return result;
        }

        public static Color For(string runtimeType)
        {
            if (string.IsNullOrWhiteSpace(runtimeType)) return UnresolvedColor;
            string normalized = runtimeType.Trim();
            return Common.TryGetValue(normalized, out Color color)
                ? color : Build(new[] { normalized })[normalized];
        }

        public static void Apply(Port port, string runtimeType)
        {
            if (port == null) throw new ArgumentNullException(nameof(port));
            Color color = For(runtimeType);
            port.portColor = color;
        }

        private static uint Fnv1A(string value)
        {
            uint hash = 2166136261;
            foreach (char character in value)
            {
                hash ^= character;
                hash *= 16777619;
            }
            return hash;
        }

        private static Color Rgb(byte red, byte green, byte blue) =>
            new Color(red / 255f, green / 255f, blue / 255f, 1f);
    }
}
