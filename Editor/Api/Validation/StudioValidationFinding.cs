namespace KitchenLib.Studio.Editor.Api.Validation
{
    /// <summary>Stable severity discriminants for extension diagnostics.</summary>
    public enum StudioValidationSeverity
    {
        /// <summary>Informational finding.</summary>
        Info = 0,
        /// <summary>Non-blocking authoring warning.</summary>
        Warning = 1,
        /// <summary>Blocking validation or projection failure.</summary>
        Error = 2
    }

    /// <summary>Immutable structured diagnostic with an optional authored-object location.</summary>
    public sealed class StudioValidationFinding
    {
        /// <summary>Creates a finding; optional location identifiers must be non-empty when supplied.</summary>
        public StudioValidationFinding(string code, StudioValidationSeverity severity, string message,
            string nodeId = null, string fieldKey = null, string portKey = null)
        {
            Code = StudioApiGuard.Required(code, nameof(code));
            Severity = StudioApiGuard.EnumValue(severity, nameof(severity));
            Message = StudioApiGuard.Required(message, nameof(message));
            NodeId = nodeId == null ? null : StudioApiGuard.Required(nodeId, nameof(nodeId));
            FieldKey = fieldKey == null ? null : StudioApiGuard.Required(fieldKey, nameof(fieldKey));
            PortKey = portKey == null ? null : StudioApiGuard.Required(portKey, nameof(portKey));
        }
        /// <summary>Stable extension-owned diagnostic code, compared ordinally.</summary>
        public string Code { get; }
        /// <summary>Diagnostic severity.</summary>
        public StudioValidationSeverity Severity { get; }
        /// <summary>Human-readable explanation.</summary>
        public string Message { get; }
        /// <summary>Optional authored node ID.</summary>
        public string NodeId { get; }
        /// <summary>Optional authored field key.</summary>
        public string FieldKey { get; }
        /// <summary>Optional authored port key.</summary>
        public string PortKey { get; }
    }
}
