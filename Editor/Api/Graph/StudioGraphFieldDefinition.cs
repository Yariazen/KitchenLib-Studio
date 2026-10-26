using System;
using KitchenLib.Studio.Editor.Api.Authoring;

namespace KitchenLib.Studio.Editor.Api.Graph
{
    /// <summary>Immutable scalar field descriptor with a stable ordinal key.</summary>
    public sealed class StudioGraphFieldDefinition
    {
        /// <summary>Creates a field. The default must match the kind or be explicit Null.</summary>
        public StudioGraphFieldDefinition(string key, string label, StudioAuthoringValueKind valueKind,
            StudioAuthoringValue defaultValue, bool required = false)
        {
            Key = StudioApiGuard.Required(key, nameof(key));
            Label = StudioApiGuard.Required(label, nameof(label));
            ValueKind = StudioApiGuard.EnumValue(valueKind, nameof(valueKind));
            DefaultValue = defaultValue ?? throw new ArgumentNullException(nameof(defaultValue));
            if (defaultValue.Kind != StudioAuthoringValueKind.Null && defaultValue.Kind != valueKind)
                throw new ArgumentException("The default value must match the field kind or be explicit Null.", nameof(defaultValue));
            Required = required;
        }
        /// <summary>Stable serialized field key, compared ordinally.</summary>
        public string Key { get; }
        /// <summary>Human-readable field label.</summary>
        public string Label { get; }
        /// <summary>Supported scalar value kind.</summary>
        public StudioAuthoringValueKind ValueKind { get; }
        /// <summary>Initial value; Null may leave a required field awaiting author input.</summary>
        public StudioAuthoringValue DefaultValue { get; }
        /// <summary>Whether authoring validation must require a non-null value.</summary>
        public bool Required { get; }
    }
}
