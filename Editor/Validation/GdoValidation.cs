using System;
using System.Collections.Generic;
using System.Linq;
using KitchenData;
using UnityEditor;

using KitchenLib.Studio.Editor.Overlays;

namespace KitchenLib.Studio.Editor.Validation
{
    internal enum ValidationSeverity
    {
        Info,
        Warning,
        Error,
        Fatal
    }

    internal sealed class ValidationIssue
    {
        public ValidationIssue(ValidationSeverity severity, string assetPath, string propertyPath, string message)
        {
            Severity = severity;
            AssetPath = assetPath;
            PropertyPath = propertyPath;
            Message = message;
        }

        public ValidationSeverity Severity { get; }
        public string AssetPath { get; }
        public string PropertyPath { get; }
        public string Message { get; }

        public override string ToString()
        {
            return string.Format("{0}: {1} ({2}.{3})", Severity, Message, AssetPath, PropertyPath);
        }
    }

    internal static class GdoValidator
    {
        public static IReadOnlyList<ValidationIssue> Validate(IEnumerable<GameDataObject> assets)
        {
            if (assets == null)
            {
                throw new ArgumentNullException(nameof(assets));
            }

            GameDataObject[] materialized = assets.Where(asset => asset != null).ToArray();
            var issues = new List<ValidationIssue>();

            foreach (GameDataObject asset in materialized.Where(asset => asset.ID == 0))
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    AssetDatabase.GetAssetPath(asset),
                    "ID",
                    "GDO ID must be non-zero."));
            }

            foreach (IGrouping<int, GameDataObject> duplicate in materialized.Where(asset => asset.ID != 0).GroupBy(asset => asset.ID).Where(group => group.Count() > 1))
            {
                foreach (GameDataObject asset in duplicate)
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error,
                        AssetDatabase.GetAssetPath(asset),
                        "ID",
                        string.Format("GDO ID {0} is duplicated.", duplicate.Key)));
                }
            }

            return issues;
        }

        public static IReadOnlyList<ValidationIssue> ValidateLinks(
            IEnumerable<GameDataObject> assets,
            IEnumerable<GdoOverlayAuthoringAsset> companions)
        {
            GameDataObject[] materialized = assets.Where(asset => asset != null).ToArray();
            GdoOverlayAuthoringAsset[] authored = companions.Where(companion => companion != null).ToArray();
            var issues = new List<ValidationIssue>();
            foreach (Dish dish in materialized.OfType<Dish>())
            {
                int count = authored.Count(companion => companion.Target == dish);
                if (count != 1)
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error,
                        AssetDatabase.GetAssetPath(dish),
                        "MinimumIngredients",
                        count == 0
                            ? "Dish requires one link authoring companion."
                            : "Dish has duplicate link authoring companions."));
                }
            }

            foreach (GdoOverlayAuthoringAsset companion in authored.Where(companion => !materialized.Contains(companion.Target)))
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    AssetDatabase.GetAssetPath(companion),
                    "Target",
                    "Link authoring companion points to a non-built target."));
            }

            return issues;
        }
    }
}
