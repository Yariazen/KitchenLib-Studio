using System.Collections.Generic;
using KitchenLib.Studio.Editor.Api.Graph;

namespace KitchenLib.Studio.Editor.Api.Validation
{
    /// <summary>Validates a read-only authored node without editor UI or asset access.</summary>
    public interface IStudioGraphNodeValidator
    {
        /// <summary>Returns non-null structured findings; an empty list indicates no extension findings.</summary>
        IReadOnlyList<StudioValidationFinding> Validate(StudioGraphNodeData node, StudioValidationContext context);
    }
}
