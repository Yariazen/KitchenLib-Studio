using System.Collections.Generic;
using System.Linq;

using KitchenLib.Studio.Editor.Overlays;
using KitchenLib.Studio.Editor.Validation;

namespace KitchenLib.Studio.Editor.Graph
{
    internal sealed class GdoGraphProjectionDiagnostic
    {
        public GdoGraphProjectionDiagnostic(string category, string ownerNodeId, string path, string message, ValidationSeverity severity = ValidationSeverity.Error)
        { Category = category; OwnerNodeId = ownerNodeId; Path = path; Message = message; Severity = severity; }
        public ValidationSeverity Severity { get; }
        public string Category { get; }
        public string OwnerNodeId { get; }
        public string Path { get; }
        public string Message { get; }
    }

    internal sealed class GdoGraphProjectedField
    {
        public GdoGraphProjectedField(string ownerNodeId, string declaringType, string fieldName, GdoOverlayValue value)
        { OwnerNodeId = ownerNodeId; DeclaringType = declaringType; FieldName = fieldName; Value = value; }
        public string OwnerNodeId { get; }
        public string DeclaringType { get; }
        public string FieldName { get; }
        public string FieldKey => DeclaringType + "." + FieldName;
        public GdoOverlayValue Value { get; }
    }

    internal sealed class GdoGraphProjectionResult
    {
        public GdoGraphProjectionResult(IEnumerable<GdoGraphProjectedField> fields,
            IEnumerable<GdoGraphProjectionDiagnostic> diagnostics)
        {
            Fields = fields.OrderBy(value => value.OwnerNodeId, System.StringComparer.Ordinal)
                .ThenBy(value => value.FieldKey, System.StringComparer.Ordinal).ToArray();
            Diagnostics = diagnostics.OrderBy(value => value.OwnerNodeId, System.StringComparer.Ordinal)
                .ThenBy(value => value.Path, System.StringComparer.Ordinal)
                .ThenBy(value => value.Category, System.StringComparer.Ordinal).ToArray();
        }
        public IReadOnlyList<GdoGraphProjectedField> Fields { get; }
        public IReadOnlyList<GdoGraphProjectionDiagnostic> Diagnostics { get; }
    }
}
