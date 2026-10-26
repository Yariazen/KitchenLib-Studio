namespace KitchenLib.Studio.Editor.Api.Graph
{
    /// <summary>Stable graph port directions.</summary>
    public enum StudioGraphPortDirection
    {
        /// <summary>Receives a connection.</summary>
        Input = 0,
        /// <summary>Supplies a connection.</summary>
        Output = 1
    }
    /// <summary>Stable limits for connections on a port.</summary>
    public enum StudioGraphPortCardinality
    {
        /// <summary>At most one connection.</summary>
        Single = 0,
        /// <summary>Zero or more connections.</summary>
        Multiple = 1
    }
    /// <summary>Immutable semantic port descriptor independent of graph rendering types.</summary>
    public sealed class StudioGraphPortDefinition
    {
        /// <summary>Creates a port with non-empty ordinal key and semantic type identifier.</summary>
        public StudioGraphPortDefinition(string key, string label, StudioGraphPortDirection direction,
            StudioGraphPortCardinality cardinality, string semanticTypeId)
        {
            Key = StudioApiGuard.Required(key, nameof(key));
            Label = StudioApiGuard.Required(label, nameof(label));
            Direction = StudioApiGuard.EnumValue(direction, nameof(direction));
            Cardinality = StudioApiGuard.EnumValue(cardinality, nameof(cardinality));
            SemanticTypeId = StudioApiGuard.Required(semanticTypeId, nameof(semanticTypeId));
        }
        /// <summary>Stable serialized port key.</summary>
        public string Key { get; }
        /// <summary>Human-readable port label.</summary>
        public string Label { get; }
        /// <summary>Connection direction.</summary>
        public StudioGraphPortDirection Direction { get; }
        /// <summary>Maximum supported connection multiplicity.</summary>
        public StudioGraphPortCardinality Cardinality { get; }
        /// <summary>Extension-owned stable semantic type; no CLR type lookup is implied.</summary>
        public string SemanticTypeId { get; }
    }
}
