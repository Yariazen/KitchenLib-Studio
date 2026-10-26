using System.Collections.Generic;
using KitchenLib.Studio.Editor.Api.Graph;

namespace KitchenLib.Studio.Editor.Api.Build
{
    /// <summary>Immutable snapshot of the selected product's local GDOs and replaceable companion values.</summary>
    public sealed class StudioBuildContext
    {
        internal StudioBuildContext(string productId, IEnumerable<StudioGraphNodeData> nodes)
        {
            ProductId = productId;
            Nodes = StudioApiGuard.List(nodes, nameof(nodes));
        }
        /// <summary>Authored graph GUID, or the stable token selected-assets for an internal direct build.</summary>
        public string ProductId { get; }
        /// <summary>
        /// Local GDOs keyed by asset GUID. Values use declaring-type|field-name keys and contain only
        /// supported existing scalar companion fields. Connections are empty. No Unity objects or paths
        /// are exposed. Later processors see accepted replacements from earlier processors.
        /// </summary>
        public IReadOnlyList<StudioGraphNodeData> Nodes { get; }
    }
}
