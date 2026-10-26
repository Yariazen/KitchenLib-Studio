using System;
using System.Collections.Generic;
using System.Linq;
using KitchenLib.Studio.Editor.Authoring;

namespace KitchenLib.Studio.Editor.Graph
{
    internal sealed class GdoCreateMenuEntry
    {
        public GdoCreateMenuEntry(string runtimeTypeName, string menuPath)
        { RuntimeTypeName = runtimeTypeName; MenuPath = menuPath; }

        public string RuntimeTypeName { get; }
        public string MenuPath { get; }
    }

    internal static class GdoCreateMenuCatalog
    {
        public static IReadOnlyList<GdoCreateMenuEntry> Build(IEnumerable<string> creatableRuntimeTypes) =>
            Build(creatableRuntimeTypes, value => GdoDescriptorCatalog.ForRuntimeType(value)?.BaseTypeName);

        internal static IReadOnlyList<GdoCreateMenuEntry> Build(
            IEnumerable<string> creatableRuntimeTypes, Func<string, string> baseTypeName)
        {
            if (creatableRuntimeTypes == null) throw new ArgumentNullException(nameof(creatableRuntimeTypes));
            if (baseTypeName == null) throw new ArgumentNullException(nameof(baseTypeName));
            string[] types = creatableRuntimeTypes.Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            var creatable = new HashSet<string>(types, StringComparer.Ordinal);
            var families = types.ToDictionary(value => value,
                value => Family(value, creatable, baseTypeName), StringComparer.Ordinal);
            Dictionary<string, int> shortNameCounts = types.Concat(families.Values).Distinct(StringComparer.Ordinal)
                .GroupBy(ShortName, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

            return types.Select(type => new GdoCreateMenuEntry(type,
                    Label(families[type], shortNameCounts) + "/" + Label(type, shortNameCounts)))
                .OrderBy(value => value.MenuPath, StringComparer.Ordinal)
                .ThenBy(value => value.RuntimeTypeName, StringComparer.Ordinal).ToArray();
        }

        private static string Family(string type, ISet<string> creatable, Func<string, string> baseTypeName)
        {
            string family = type;
            string current = type;
            var visited = new HashSet<string>(StringComparer.Ordinal);
            while (!string.IsNullOrWhiteSpace(current) && visited.Add(current))
            {
                string baseType = baseTypeName(current);
                if (string.IsNullOrWhiteSpace(baseType)) break;
                if (creatable.Contains(baseType)) family = baseType;
                current = baseType;
            }
            return family;
        }

        private static string Label(string type, IReadOnlyDictionary<string, int> shortNameCounts)
        {
            string shortName = ShortName(type);
            return shortNameCounts[shortName] > 1 ? type : shortName;
        }

        private static string ShortName(string type)
        {
            int index = type.LastIndexOf('.');
            return index < 0 ? type : type.Substring(index + 1);
        }
    }
}
