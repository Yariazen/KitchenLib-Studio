namespace KitchenLib.Studio.Editor
{
    public sealed class GdoGraphDiagnostic
    {
        public GdoGraphDiagnostic(ValidationSeverity severity, string category, string elementId,
            string assetPath, string propertyPath, string message)
        {
            Severity = severity;
            Category = category ?? string.Empty;
            ElementId = elementId ?? string.Empty;
            AssetPath = assetPath ?? string.Empty;
            PropertyPath = propertyPath ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public ValidationSeverity Severity { get; }
        public string Category { get; }
        public string ElementId { get; }
        public string AssetPath { get; }
        public string PropertyPath { get; }
        public string Message { get; }
        public string SortKey => ((int)Severity).ToString("D2") + "|" + ElementId + "|" + PropertyPath + "|" + Category;
    }
}
