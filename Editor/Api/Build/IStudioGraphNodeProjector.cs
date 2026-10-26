using KitchenLib.Studio.Editor.Api.Graph;
using KitchenLib.Studio.Editor.Api.Validation;

namespace KitchenLib.Studio.Editor.Api.Build
{
    /// <summary>Converts authored node data to bounded declarative output; it cannot run a build.</summary>
    public interface IStudioGraphNodeProjector
    {
        /// <summary>Returns a non-null projection containing output values and/or structured diagnostics.</summary>
        StudioBuildProjection Project(StudioGraphNodeData node, StudioValidationContext context);
    }
}
