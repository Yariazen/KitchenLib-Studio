using System;
using System.Collections.Generic;
using System.Linq;
using KitchenLib.Studio.Editor.Api.Authoring;
using KitchenLib.Studio.Editor.Api.Validation;

namespace KitchenLib.Studio.Editor.Api.Build
{
    /// <summary>A build-only replacement of one existing field on a local authored GDO.</summary>
    public sealed class StudioBuildFieldChange
    {
        /// <summary>Creates a replacement. Studio checks identity, kind, resolution and schema before use.</summary>
        public StudioBuildFieldChange(string nodeId, string fieldKey, StudioAuthoringValue value)
        {
            NodeId = StudioApiGuard.Required(nodeId, nameof(nodeId));
            FieldKey = StudioApiGuard.Required(fieldKey, nameof(fieldKey));
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }
        /// <summary>Local GDO asset GUID from the context.</summary>
        public string NodeId { get; }
        /// <summary>Exact ordinal key from the context.</summary>
        public string FieldKey { get; }
        /// <summary>Replacement with the same value kind; references resolve to local context GDO GUIDs.</summary>
        public StudioAuthoringValue Value { get; }
    }

    /// <summary>Immutable bounded replacements and diagnostics; Error findings block the entire build.</summary>
    public sealed class StudioBuildProcessorResult
    {
        /// <summary>Copies at most 256 changes and 256 findings; null collections and entries are rejected.</summary>
        public StudioBuildProcessorResult(IEnumerable<StudioBuildFieldChange> changes,
            IEnumerable<StudioValidationFinding> findings)
        {
            Changes = Copy(changes, nameof(changes));
            Findings = Copy(findings, nameof(findings));
        }
        /// <summary>Replacements applied atomically per processor to temporary copies only.</summary>
        public IReadOnlyList<StudioBuildFieldChange> Changes { get; }
        /// <summary>Structured findings; these cannot replace or downgrade Studio's own findings.</summary>
        public IReadOnlyList<StudioValidationFinding> Findings { get; }

        private static IReadOnlyList<T> Copy<T>(IEnumerable<T> values, string parameter) where T : class
        {
            if (values == null) throw new ArgumentNullException(parameter);
            T[] copy = values.Take(257).ToArray();
            if (copy.Length > 256 || copy.Any(value => value == null))
                throw new ArgumentException("At most 256 non-null entries are supported.", parameter);
            return Array.AsReadOnly(copy);
        }
    }
}
