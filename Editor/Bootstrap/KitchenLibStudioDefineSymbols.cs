using System;
using System.Linq;
using UnityEditor;

namespace KitchenLib.Studio.Editor.Bootstrap
{
    internal static class KitchenLibStudioDefineSymbols
    {
        public const string MirrorsSymbolPrefix = "KITCHENLIB_STUDIO_MIRRORS";
        public const string MirrorsSymbol = "KITCHENLIB_STUDIO_MIRRORS_V3";
        public static bool MirrorsEnabled => PlayerSettings.GetScriptingDefineSymbolsForGroup(
            BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget)).Split(';').Contains(MirrorsSymbol);

        public static void SetMirrorsEnabled(bool enabled)
        {
            // Cover every installed/known build-target group so switching targets cannot revive stale mirrors.
            foreach (BuildTargetGroup group in Enum.GetValues(typeof(BuildTargetGroup)))
            {
                if (group == BuildTargetGroup.Unknown) continue;
                var field = typeof(BuildTargetGroup).GetField(group.ToString());
                if (field == null || Attribute.IsDefined(field, typeof(ObsoleteAttribute))) continue;
                string current = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
                var symbols = current.Split(';').Where(value => !string.IsNullOrWhiteSpace(value)).ToList();
                bool present = symbols.Contains(MirrorsSymbol);
                bool stale = symbols.Any(value => value.StartsWith(MirrorsSymbolPrefix, StringComparison.Ordinal) && value != MirrorsSymbol);
                if (present == enabled && !stale) continue;
                symbols.RemoveAll(value => value.StartsWith(MirrorsSymbolPrefix, StringComparison.Ordinal));
                if (enabled) symbols.Add(MirrorsSymbol);
                PlayerSettings.SetScriptingDefineSymbolsForGroup(group, string.Join(";", symbols));
            }
        }
    }
}
