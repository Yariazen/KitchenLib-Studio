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
        /// <summary>Bounded data proposed by the projector, not filesystem or bundle-writing access.</summary>
        public IReadOnlyDictionary<string, StudioAuthoringValue> Values { get; }
        /// <summary>Structured projection diagnostics.</summary>
        public IReadOnlyList<StudioValidationFinding> Findings { get; }
        /// <summary>True when this result has no Error findings; built-in validation still applies.</summary>
        public bool IsSuccess => !Findings.Any(finding => finding.Severity == StudioValidationSeverity.Error);
    }
}
