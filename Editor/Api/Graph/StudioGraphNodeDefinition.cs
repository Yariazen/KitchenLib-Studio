using System.Collections.Generic;
using System.Linq;
using KitchenLib.Studio.Editor.Api.Build;
using KitchenLib.Studio.Editor.Api.Validation;

namespace KitchenLib.Studio.Editor.Api.Graph
{
    /// <summary>Immutable description of a node rendered and managed by Studio.</summary>
    public sealed class StudioGraphNodeDefinition
    {
        /// <summary>Creates a definition with unique ordinal field/port keys and a required projector.</summary>
        public StudioGraphNodeDefinition(string typeId, string displayName,
            IEnumerable<StudioGraphFieldDefinition> fields, IEnumerable<StudioGraphPortDefinition> ports,
            IStudioGraphNodeProjector projector, IStudioGraphNodeValidator validator = null)
        {
            TypeId = StudioApiGuard.Required(typeId, nameof(typeId));
            DisplayName = StudioApiGuard.Required(displayName, nameof(displayName));
            Fields = StudioApiGuard.List(fields, nameof(fields));
            Ports = StudioApiGuard.List(ports, nameof(ports));
            StudioApiGuard.Unique(Fields.Select(field => field.Key), nameof(fields));
            StudioApiGuard.Unique(Ports.Select(port => port.Key), nameof(ports));
            Projector = projector ?? throw new System.ArgumentNullException(nameof(projector));
            Validator = validator;
        }
        /// <summary>Stable extension-owned namespaced identifier, independent of CLR names.</summary>
        public string TypeId { get; }
        /// <summary>Human-readable node label.</summary>
        public string DisplayName { get; }
        /// <summary>Snapshot of fields in presentation order.</summary>
        public IReadOnlyList<StudioGraphFieldDefinition> Fields { get; }
        /// <summary>Snapshot of ports in presentation order.</summary>
        public IReadOnlyList<StudioGraphPortDefinition> Ports { get; }
        /// <summary>Optional extension validator; null means no extension validation.</summary>
        public IStudioGraphNodeValidator Validator { get; }
        /// <summary>Required declarative build projector.</summary>
        public IStudioGraphNodeProjector Projector { get; }
    }
}
