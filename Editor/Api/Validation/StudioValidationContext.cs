using System.Collections.Generic;
using System.Linq;
using KitchenLib.Studio.Editor.Api.Graph;

namespace KitchenLib.Studio.Editor.Api.Validation
{
    /// <summary>Bounded read-only authored product context shared by validators and projectors.</summary>
    public sealed class StudioValidationContext
    {
        /// <summary>Copies the product's nodes, rejecting duplicate ordinal node IDs.</summary>
        public StudioValidationContext(string productId, IEnumerable<StudioGraphNodeData> nodes)
        {
            ProductId = StudioApiGuard.Required(productId, nameof(productId));
            Nodes = StudioApiGuard.List(nodes, nameof(nodes));
            StudioApiGuard.Unique(Nodes.Select(node => node.NodeId), nameof(nodes));
        }
        /// <summary>Stable authored product identifier, never a generated path.</summary>
        public string ProductId { get; }
        /// <summary>Snapshot of authored nodes, including unrecognized node types.</summary>
        public IReadOnlyList<StudioGraphNodeData> Nodes { get; }
    }
}
