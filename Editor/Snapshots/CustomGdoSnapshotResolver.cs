using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KitchenData;
using UnityEngine;

namespace KitchenLib.Studio.Editor.Snapshots
{
    internal sealed class GdoPrefabCandidate
    {
        public GdoPrefabCandidate(string fieldPath, GameObject value) { FieldPath = fieldPath; Value = value; }
        public string FieldPath { get; }
        public GameObject Value { get; }
    }

    internal sealed class SnapshotResolution
    {
        private SnapshotResolution(GameObject gameObject, string fieldPath, bool requiresSelection, string diagnostic)
        { GameObject = gameObject; FieldPath = fieldPath; RequiresSelection = requiresSelection; Diagnostic = diagnostic ?? string.Empty; }
        public GameObject GameObject { get; }
        public string FieldPath { get; }
        public bool RequiresSelection { get; }
        public string Diagnostic { get; }
        public bool Succeeded => GameObject != null;
        internal static SnapshotResolution Success(GdoPrefabCandidate candidate) => new SnapshotResolution(candidate.Value, candidate.FieldPath, false, string.Empty);
        internal static SnapshotResolution Failure(bool requiresSelection, string diagnostic) => new SnapshotResolution(null, null, requiresSelection, diagnostic);
    }

    internal sealed class CustomGdoSnapshotResolver
    {
        public IReadOnlyList<GdoPrefabCandidate> Candidates(GameDataObject gdo)
        {
            if (gdo == null) throw new ArgumentNullException(nameof(gdo));
            return gdo.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(field => field.FieldType == typeof(GameObject) && !field.IsDefined(typeof(NonSerializedAttribute), true))
                .Select(field => new GdoPrefabCandidate(field.Name, field.GetValue(gdo) as GameObject))
                .Where(candidate => candidate.Value != null)
                .OrderBy(candidate => candidate.FieldPath, StringComparer.Ordinal)
                .ToArray();
        }

        public SnapshotResolution Resolve(GameDataObject gdo, string optionalFieldPath = null)
        {
            IReadOnlyList<GdoPrefabCandidate> candidates = Candidates(gdo);
            if (!string.IsNullOrWhiteSpace(optionalFieldPath))
            {
                GdoPrefabCandidate selected = candidates.FirstOrDefault(value => string.Equals(value.FieldPath, optionalFieldPath, StringComparison.Ordinal));
                return selected == null
                    ? SnapshotResolution.Failure(false, "The selected GDO prefab field is missing or empty: " + optionalFieldPath)
                    : SnapshotResolution.Success(selected);
            }
            if (candidates.Count == 1) return SnapshotResolution.Success(candidates[0]);
            if (candidates.Count == 0) return SnapshotResolution.Failure(false, "The custom GDO has no populated GameObject prefab fields.");
            return SnapshotResolution.Failure(true, "Multiple populated GameObject prefab fields require a selection.");
        }
    }
}
