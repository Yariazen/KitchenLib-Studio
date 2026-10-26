using System;
using System.Collections.Generic;
using KitchenLib.Studio.Editor.Api.Authoring;

namespace KitchenLib.Studio.Editor.Api.Graph
{
    /// <summary>Read-only authored node snapshot, including keys unknown to its current definition.</summary>
    public sealed class StudioGraphNodeData
    {
        /// <summary>Copies values using ordinal keys and copies connected endpoint records.</summary>
        public StudioGraphNodeData(string nodeId, string typeId,
            IEnumerable<KeyValuePair<string, StudioAuthoringValue>> values, IEnumerable<StudioGraphConnection> connections)
        {
            NodeId = StudioApiGuard.Required(nodeId, nameof(nodeId));
            TypeId = StudioApiGuard.Required(typeId, nameof(typeId));
            Values = StudioApiGuard.Values(values, nameof(values));
            Connections = StudioApiGuard.List(connections, nameof(connections));
        }
        /// <summary>Stable authored node identifier.</summary>
        public string NodeId { get; }
        /// <summary>Stable definition identifier, retained even if its provider is unavailable.</summary>
        public string TypeId { get; }
        /// <summary>Immutable ordinal map; unknown field keys are preserved.</summary>
        public IReadOnlyDictionary<string, StudioAuthoringValue> Values { get; }
        /// <summary>Immutable local-port to remote-endpoint records.</summary>
        public IReadOnlyList<StudioGraphConnection> Connections { get; }
    }

    /// <summary>A stable endpoint identifying an authored node and port.</summary>
    public sealed class StudioGraphEndpoint
    {
        /// <summary>Creates an endpoint from non-empty ordinal identifiers.</summary>
        public StudioGraphEndpoint(string nodeId, string portKey)
        {
            NodeId = StudioApiGuard.Required(nodeId, nameof(nodeId));
            PortKey = StudioApiGuard.Required(portKey, nameof(portKey));
        }
        /// <summary>Connected node identifier.</summary>
        public string NodeId { get; }
        /// <summary>Connected port key.</summary>
        public string PortKey { get; }
    }

    /// <summary>A connection from one local port to a remote authored endpoint.</summary>
    public sealed class StudioGraphConnection
    {
        /// <summary>Creates a connection without resolving or discarding unknown endpoints.</summary>
        public StudioGraphConnection(string portKey, StudioGraphEndpoint endpoint)
        {
            PortKey = StudioApiGuard.Required(portKey, nameof(portKey));
            Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
        }
        /// <summary>Local port key.</summary>
        public string PortKey { get; }
        /// <summary>Remote connected endpoint.</summary>
        public StudioGraphEndpoint Endpoint { get; }
    }
}
