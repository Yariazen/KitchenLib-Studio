using System;
using System.Linq;

namespace KitchenLib.Studio.Editor
{
    public sealed class ExternalGdoSnapshotSourceResolution
    {
        private ExternalGdoSnapshotSourceResolution(bool succeeded, string gameObjectKey, string slot,
            string diagnostic)
        {
            Succeeded = succeeded;
            GameObjectKey = gameObjectKey;
            Slot = slot;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public bool Succeeded { get; }
        public string GameObjectKey { get; }
        public string Slot { get; }
        public string Diagnostic { get; }

        internal static ExternalGdoSnapshotSourceResolution Success(string key, string slot) =>
            new ExternalGdoSnapshotSourceResolution(true, key, slot, string.Empty);

        internal static ExternalGdoSnapshotSourceResolution Failure(string diagnostic) =>
            new ExternalGdoSnapshotSourceResolution(false, null, null, diagnostic);
    }

    public static class ExternalGdoSnapshotSourceResolver
    {
        private static readonly string[] PreferredSlots = { "IconPrefab", "DisplayPrefab", "Prefab" };

        public static ExternalGdoSnapshotSourceResolution Resolve(VanillaAssetCatalogData catalog, int gdoId)
        {
            if (catalog == null)
                return ExternalGdoSnapshotSourceResolution.Failure("The vanilla snapshot catalogue is unavailable.");
            if (gdoId == 0)
                return ExternalGdoSnapshotSourceResolution.Failure("External GDO ID must be non-zero.");

            VanillaAssetRecordData[] records = catalog.records ?? Array.Empty<VanillaAssetRecordData>();
            VanillaAssetRecordData[] matches = records.Where(record =>
                record != null && string.Equals(record.kind, "gdo", StringComparison.Ordinal) && record.gdoId == gdoId).ToArray();
            if (matches.Length == 0)
                return ExternalGdoSnapshotSourceResolution.Failure("The external GDO is not present in the snapshot catalogue.");
            if (matches.Length != 1)
                return ExternalGdoSnapshotSourceResolution.Failure("The snapshot catalogue contains multiple records for this GDO ID.");

            VanillaAssetRecordData gdo = matches[0];
            VanillaAssetEdgeData[] edges = catalog.edges ?? Array.Empty<VanillaAssetEdgeData>();
            foreach (string slot in PreferredSlots)
            {
                string[] targets = edges.Where(edge => edge != null && edge.sourceKey == gdo.key &&
                        string.Equals(edge.relation, "gameObject", StringComparison.Ordinal) &&
                        string.Equals(edge.slot, slot, StringComparison.Ordinal))
                    .Select(edge => edge.targetKey).Where(key => !string.IsNullOrEmpty(key))
                    .Distinct(StringComparer.Ordinal).OrderBy(key => key, StringComparer.Ordinal).ToArray();
                foreach (string target in targets)
                    if (records.Any(record => record != null && record.key == target &&
                        string.Equals(record.kind, "gameObject", StringComparison.Ordinal)))
                        return ExternalGdoSnapshotSourceResolution.Success(target, slot);
            }
            return ExternalGdoSnapshotSourceResolution.Failure(
                "The external GDO has no supported IconPrefab, DisplayPrefab, or Prefab GameObject.");
        }
    }
}
