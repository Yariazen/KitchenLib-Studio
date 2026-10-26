using System.Collections.Generic;
using System.Linq;

namespace KitchenLib.Studio.Editor
{
    public sealed class GdoGraphProjectionDiagnostic
    {
        public GdoGraphProjectionDiagnostic(string category, string ownerNodeId, string path, string message)
        { Category = category; OwnerNodeId = ownerNodeId; Path = path; Message = message; }
        public string Category { get; }
        public string OwnerNodeId { get; }
        public string Path { get; }
        public string Message { get; }
    }

    public sealed class GdoGraphProjectedField
    {
        public GdoGraphProjectedField(string ownerNodeId, string declaringType, string fieldName, GdoOverlayValue value)
        { OwnerNodeId = ownerNodeId; DeclaringType = declaringType; FieldName = fieldName; Value = value; }
        public string OwnerNodeId { get; }
        public string DeclaringType { get; }
        public string FieldName { get; }
        public string FieldKey => DeclaringType + "." + FieldName;
        public GdoOverlayValue Value { get; }
    }

    public sealed class GdoGraphProjectionResult
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
