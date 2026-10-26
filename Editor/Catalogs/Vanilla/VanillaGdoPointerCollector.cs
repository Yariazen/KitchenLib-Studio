using System;
using System.Collections.Generic;
using AssetsTools.NET;

namespace KitchenLib.Studio.Editor.Catalogs.Vanilla
{
    internal static class VanillaGdoPointerCollector
    {
        private const int MaxDepth = 32;
        private static readonly HashSet<string> Excluded = new HashSet<string>(StringComparer.Ordinal)
        {
            "m_Script", "m_GameObject", "Prefab", "IconPrefab", "DisplayPrefab", "Visual",
            "_IconPrefab", "ID", "m_Name"
        };

        internal static IReadOnlyList<PlateUpAssetPointer> Collect(AssetTypeValueField root,
            string sourceRelativePath, long sourcePathId, Func<int, string> externalFileResolver)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (externalFileResolver == null) throw new ArgumentNullException(nameof(externalFileResolver));
            var result = new List<PlateUpAssetPointer>();
            if (root.Children == null) return result;
            foreach (AssetTypeValueField child in root.Children)
                Visit(child, child.FieldName, sourceRelativePath, externalFileResolver, result, 0);
            return result;
        }

        private static void Visit(AssetTypeValueField field, string path, string sourceRelativePath,
            Func<int, string> externalFileResolver, ICollection<PlateUpAssetPointer> result, int depth)
        {
            if (field == null || field.IsDummy || depth > MaxDepth || Excluded.Contains(field.FieldName)) return;
            AssetTypeValueField file = Child(field, "m_FileID");
            AssetTypeValueField target = Child(field, "m_PathID");
            if (file != null && target != null)
            {
                int fileId = file.AsInt;
                long pathId = target.AsLong;
                string targetFile = fileId == 0 ? sourceRelativePath : externalFileResolver(fileId);
                if (pathId != 0 && !string.IsNullOrWhiteSpace(targetFile) && !string.IsNullOrWhiteSpace(path))
                    result.Add(new PlateUpAssetPointer(path, targetFile, pathId));
                return;
            }
            if (field.Children == null) return;
            if (string.Equals(field.FieldName, "Array", StringComparison.Ordinal))
            {
                for (int index = 0; index < field.Children.Count; index++)
                    Visit(field.Children[index], path + "[" + index + "]", sourceRelativePath,
                        externalFileResolver, result, depth + 1);
                return;
            }
            foreach (AssetTypeValueField child in field.Children)
            {
                string childPath = string.Equals(child.FieldName, "Array", StringComparison.Ordinal)
                    ? path
                    : string.IsNullOrEmpty(path) ? child.FieldName : path + "." + child.FieldName;
                Visit(child, childPath, sourceRelativePath, externalFileResolver, result, depth + 1);
            }
        }

        private static AssetTypeValueField Child(AssetTypeValueField field, string name)
        {
            if (field.Children == null) return null;
            foreach (AssetTypeValueField child in field.Children)
                if (string.Equals(child.FieldName, name, StringComparison.Ordinal)) return child;
            return null;
        }
    }
}
