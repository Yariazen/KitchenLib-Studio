using System;
using System.Collections.Generic;
using System.IO;

namespace KitchenLib.Studio.Editor.Catalogs.Mods
{
    internal static class IngredientLibImportRequirements
    {
        public const string WorkshopId = "2913877103";
        public const string ProviderKey = "workshop:" + WorkshopId;

        public static void Validate(LocalModCatalogPackageManifestData manifest, ModGdoCatalogData catalog)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (!string.Equals(manifest.workshopId, WorkshopId, StringComparison.Ordinal) ||
                !string.Equals(manifest.providerKey, ProviderKey, StringComparison.Ordinal) ||
                !string.Equals(manifest.sourceAssembly, "IngredientLib", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(manifest.sourceAssetBundleSha256))
                throw new InvalidDataException("The package does not identify IngredientLib Workshop item 2913877103.");

            var visualIds = new HashSet<int>();
            foreach (LocalModCatalogVisualData visual in manifest.visuals)
                if (!visualIds.Add(visual.runtimeId))
                    throw new InvalidDataException("The IngredientLib package contains duplicate baked visuals.");

            bool foundProvider = false;
            foreach (ModGdoProviderData provider in catalog.providers)
                if (string.Equals(provider.key, ProviderKey, StringComparison.Ordinal) &&
                    string.Equals(provider.workshopId, WorkshopId, StringComparison.Ordinal))
                    foundProvider = true;
            if (!foundProvider) throw new InvalidDataException("The catalogue does not contain the IngredientLib provider.");

            foreach (ModGdoEntryData entry in catalog.entries)
            {
                if (!string.Equals(entry.providerKey, ProviderKey, StringComparison.Ordinal))
                    throw new InvalidDataException("An IngredientLib package may only contain its selected provider.");
                if (entry.hasPrefab && !visualIds.Contains(entry.runtimeId))
                    throw new InvalidDataException("Every IngredientLib GDO with a prefab requires a baked visual.");
                if (entry.references != null && entry.references.Length > 0 &&
                    (entry.relationshipValues == null || entry.relationshipValues.Length == 0))
                    throw new InvalidDataException("Every relationship-bearing IngredientLib GDO requires a relationship tree.");
            }

            var entriesById = new Dictionary<int, ModGdoEntryData>();
            foreach (ModGdoEntryData entry in catalog.entries) entriesById[entry.runtimeId] = entry;
            foreach (ModGdoEntryData entry in catalog.entries)
            {
                var paths = new HashSet<string>(StringComparer.Ordinal);
                ValidateRelationships(entry.relationshipValues, paths, entriesById);
            }
        }

        private static void ValidateRelationships(IEnumerable<ModGdoRelationshipValueData> values,
            HashSet<string> paths, IReadOnlyDictionary<int, ModGdoEntryData> entriesById)
        {
            if (values == null) return;
            foreach (ModGdoRelationshipValueData value in values)
            {
                if (value == null || string.IsNullOrWhiteSpace(value.path) || !paths.Add(value.path))
                    throw new InvalidDataException("The IngredientLib package contains duplicate or invalid relationship paths.");
                if (value.resolvedGdo != null && entriesById.TryGetValue(value.resolvedGdo.runtimeId, out ModGdoEntryData target) &&
                    (!string.Equals(value.resolvedGdo.providerKey, target.providerKey, StringComparison.Ordinal) ||
                     !string.Equals(value.resolvedGdo.runtimeType, target.runtimeType, StringComparison.Ordinal)))
                    throw new InvalidDataException("A resolved IngredientLib relationship target does not match its catalogue entry.");
                ValidateRelationships(value.children, paths, entriesById);
            }
        }
    }
}
