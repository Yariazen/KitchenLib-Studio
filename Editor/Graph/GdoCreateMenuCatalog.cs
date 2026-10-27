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
        private const string CommonRoot = "KitchenData.GameDataObject";

        public static IReadOnlyList<GdoCreateMenuEntry> Build(IEnumerable<string> creatableRuntimeTypes) =>
            Build(creatableRuntimeTypes, value => GdoDescriptorCatalog.ForRuntimeType(value)?.BaseTypeName);

        internal static IReadOnlyList<GdoCreateMenuEntry> Build(
            IEnumerable<string> creatableRuntimeTypes, Func<string, string> baseTypeName)
        {
            if (creatableRuntimeTypes == null) throw new ArgumentNullException(nameof(creatableRuntimeTypes));
            if (baseTypeName == null) throw new ArgumentNullException(nameof(baseTypeName));
            string[] types = creatableRuntimeTypes.Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            var families = types.ToDictionary(value => value,
                value => Family(value, baseTypeName), StringComparer.Ordinal);
            Dictionary<string, int> shortNameCounts = types.Concat(families.Values).Distinct(StringComparer.Ordinal)
                .GroupBy(ShortName, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

            GdoCreateMenuEntry[] entries = types.Select(type =>
                {
                    string family = families[type];
                    string leaf = Label(type, shortNameCounts);
                    if (string.Equals(family, type, StringComparison.Ordinal)) leaf = "Create " + leaf;
                    return new GdoCreateMenuEntry(type, Label(family, shortNameCounts) + "/" + leaf);
                })
                .OrderBy(value => value.MenuPath, StringComparer.Ordinal)
                .ThenBy(value => value.RuntimeTypeName, StringComparer.Ordinal).ToArray();

            if (entries.Select(value => value.RuntimeTypeName).Distinct(StringComparer.Ordinal).Count() != entries.Length)
                throw new InvalidOperationException("Create GDO catalogue contains duplicate runtime types.");
            if (entries.Select(value => value.MenuPath).Distinct(StringComparer.Ordinal).Count() != entries.Length)
                throw new InvalidOperationException("Create GDO catalogue contains colliding menu paths. Qualify the conflicting type labels.");
            return entries;
        }

        private static string Family(string type, Func<string, string> baseTypeName)
        {
            string current = type;
            var visited = new HashSet<string>(StringComparer.Ordinal);
            while (visited.Add(current))
            {
                string baseType = baseTypeName(current);
                if (string.Equals(baseType, CommonRoot, StringComparison.Ordinal)) return current;
                if (string.IsNullOrWhiteSpace(baseType)) return type;
                current = baseType;
            }
            return type;
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
