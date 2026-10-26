using System.Collections.Generic;
using System.Linq;
using KitchenLib.Studio.Editor.Api.Authoring;
using KitchenLib.Studio.Editor.Api.Validation;

namespace KitchenLib.Studio.Editor.Api.Build
{
    /// <summary>Immutable declarative projection result; Studio retains ownership of build validation and output.</summary>
    public sealed class StudioBuildProjection
    {
        /// <summary>Copies ordinal output values and diagnostics. Error findings mark the result unsuccessful.</summary>
        public StudioBuildProjection(IEnumerable<KeyValuePair<string, StudioAuthoringValue>> values,
            IEnumerable<StudioValidationFinding> findings)
        {
            Values = StudioApiGuard.Values(values, nameof(values));
            Findings = StudioApiGuard.List(findings, nameof(findings));
        }
        /// <summary>
        /// Values keyed by declared output-port keys. At most 256 outputs are accepted; text and reference
        /// payloads are limited to 65536 characters. A GdoReferenceSemanticTypeId output must be a Reference
        /// containing an existing authored GDO node ID. Studio resolves it through relationship projection.
        /// These values never specify files, runtime manifests, or bundle-writing instructions.
        /// </summary>
        public IReadOnlyDictionary<string, StudioAuthoringValue> Values { get; }
        /// <summary>Structured projection diagnostics.</summary>
        public IReadOnlyList<StudioValidationFinding> Findings { get; }
        /// <summary>True when this result has no Error findings; built-in validation still applies.</summary>
        public bool IsSuccess => !Findings.Any(finding => finding.Severity == StudioValidationSeverity.Error);
    }
}
