using System;
using System.Collections.Generic;
using System.Linq;

namespace KitchenLib.Studio.Editor
{
    public sealed class GdoInspectorDiagnosticMap
    {
        internal GdoInspectorDiagnosticMap(IDictionary<string, IReadOnlyList<ValidationIssue>> fields,
            IReadOnlyList<ValidationIssue> unmapped, IReadOnlyList<ValidationIssue> all)
        {
            _fields = new Dictionary<string, IReadOnlyList<ValidationIssue>>(fields, StringComparer.Ordinal);
            Unmapped = unmapped;
            Count = all.Count;
            HighestSeverity = all.Count == 0 ? ValidationSeverity.Info : all.Max(issue => issue.Severity);
        }

        private readonly IReadOnlyDictionary<string, IReadOnlyList<ValidationIssue>> _fields;
        public IReadOnlyList<ValidationIssue> Unmapped { get; }
        public int Count { get; }
        public ValidationSeverity HighestSeverity { get; }
        public IReadOnlyList<ValidationIssue> ForField(string key) => _fields.TryGetValue(key, out IReadOnlyList<ValidationIssue> issues)
            ? issues : new ValidationIssue[0];
    }

    public static class GdoInspectorDiagnostics
    {
        public static GdoInspectorDiagnosticMap Map(IEnumerable<ValidationIssue> issues, GdoInspectorModel model)
        {
            if (issues == null) throw new ArgumentNullException(nameof(issues));
            if (model == null) throw new ArgumentNullException(nameof(model));
            GdoInspectorField[] fields = model.Sections.SelectMany(section => section.Fields).ToArray();
            var mapped = new Dictionary<string, List<ValidationIssue>>(StringComparer.Ordinal);
            var unmapped = new List<ValidationIssue>();
            ValidationIssue[] ordered = issues.Where(issue => issue != null)
                .OrderByDescending(issue => issue.Severity).ThenBy(issue => issue.PropertyPath, StringComparer.Ordinal).ToArray();
            foreach (ValidationIssue issue in ordered)
            {
                GdoInspectorField owner = fields.Where(field => Owns(field, issue.PropertyPath))
                    .OrderByDescending(field => field.Key.Length).FirstOrDefault();
                if (owner == null) { unmapped.Add(issue); continue; }
                if (!mapped.TryGetValue(owner.Key, out List<ValidationIssue> bucket)) mapped.Add(owner.Key, bucket = new List<ValidationIssue>());
                bucket.Add(issue);
            }
            return new GdoInspectorDiagnosticMap(mapped.ToDictionary(pair => pair.Key,
                pair => (IReadOnlyList<ValidationIssue>)pair.Value.ToArray(), StringComparer.Ordinal), unmapped, ordered);
        }

        private static bool Owns(GdoInspectorField field, string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            return path == field.Key || path == field.Name || path.StartsWith(field.Key + ".", StringComparison.Ordinal) ||
                path.StartsWith(field.Key + "[", StringComparison.Ordinal) || path.StartsWith(field.Name + ".", StringComparison.Ordinal) ||
                path.StartsWith(field.Name + "[", StringComparison.Ordinal);
        }
    }
}
