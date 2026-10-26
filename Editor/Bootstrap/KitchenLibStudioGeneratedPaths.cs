using System.IO;
using UnityEngine;

namespace KitchenLib.Studio.Editor
{
    public static class KitchenLibStudioGeneratedPaths
    {
        public const string AssetRoot = "Assets/KitchenLibStudio.Generated";
        public const string StateRoot = "Library/KitchenLibStudio";
        public static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        public static string AssetDirectory => Path.Combine(ProjectRoot, AssetRoot);
        public static string StateDirectory => Path.Combine(ProjectRoot, StateRoot);
        public static string MirrorStatePath => Path.Combine(StateDirectory, "mirror-state.json");
    }
}
