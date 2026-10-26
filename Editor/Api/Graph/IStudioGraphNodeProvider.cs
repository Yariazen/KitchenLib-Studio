using System.Collections.Generic;

namespace KitchenLib.Studio.Editor.Api.Graph
{
    /// <summary>Explicit source of declarative node definitions, registered once per editor domain.</summary>
    public interface IStudioGraphNodeProvider
    {
        /// <summary>Returns non-null definitions in any order; registration normalizes ordinal type IDs.</summary>
        IEnumerable<StudioGraphNodeDefinition> GetDefinitions();
    }
}
