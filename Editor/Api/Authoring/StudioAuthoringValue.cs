using System;

namespace KitchenLib.Studio.Editor.Api.Authoring
{
    /// <summary>Stable serialized discriminants for scalar authored values.</summary>
    public enum StudioAuthoringValueKind
    {
        /// <summary>An explicitly absent value.</summary>
        Null = 0,
        /// <summary>A Boolean value.</summary>
        Boolean = 1,
        /// <summary>A signed 64-bit integer.</summary>
        Integer = 2,
        /// <summary>A finite double-precision number.</summary>
        Number = 3,
        /// <summary>Text, including the empty string.</summary>
        String = 4,
        /// <summary>An opaque stable authored-object identifier, never an asset path or reader.</summary>
        Reference = 5
    }

    /// <summary>An immutable, typed scalar value with ordinal text/reference equality.</summary>
    public sealed class StudioAuthoringValue : IEquatable<StudioAuthoringValue>
    {
        private readonly object value;
        private StudioAuthoringValue(StudioAuthoringValueKind kind, object value) { Kind = kind; this.value = value; }

        /// <summary>The explicit null value; C# null is not an authored value.</summary>
        public static StudioAuthoringValue Null { get; } = new StudioAuthoringValue(StudioAuthoringValueKind.Null, null);
        /// <summary>The stored value's stable discriminant.</summary>
        public StudioAuthoringValueKind Kind { get; }
        /// <summary>Creates a Boolean value.</summary>
        public static StudioAuthoringValue FromBoolean(bool value) => new StudioAuthoringValue(StudioAuthoringValueKind.Boolean, value);
        /// <summary>Creates a signed 64-bit integer value.</summary>
        public static StudioAuthoringValue FromInteger(long value) => new StudioAuthoringValue(StudioAuthoringValueKind.Integer, value);
        /// <summary>Creates a number; NaN and infinity are rejected.</summary>
        public static StudioAuthoringValue FromNumber(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value));
            return new StudioAuthoringValue(StudioAuthoringValueKind.Number, value);
        }
        /// <summary>Creates text; null is rejected and empty text is preserved.</summary>
        public static StudioAuthoringValue FromString(string value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            return new StudioAuthoringValue(StudioAuthoringValueKind.String, value);
        }
        /// <summary>Creates an opaque, non-empty authored-object reference.</summary>
        public static StudioAuthoringValue FromReference(string value) =>
            new StudioAuthoringValue(StudioAuthoringValueKind.Reference, StudioApiGuard.Required(value, nameof(value)));
        /// <summary>Reads a Boolean or throws InvalidOperationException for another kind.</summary>
        public bool AsBoolean() => Read<bool>(StudioAuthoringValueKind.Boolean);
        /// <summary>Reads an integer or throws InvalidOperationException for another kind.</summary>
        public long AsInteger() => Read<long>(StudioAuthoringValueKind.Integer);
        /// <summary>Reads a number or throws InvalidOperationException for another kind.</summary>
        public double AsNumber() => Read<double>(StudioAuthoringValueKind.Number);
        /// <summary>Reads text or throws InvalidOperationException for another kind.</summary>
        public string AsString() => Read<string>(StudioAuthoringValueKind.String);
        /// <summary>Reads a reference or throws InvalidOperationException for another kind.</summary>
        public string AsReference() => Read<string>(StudioAuthoringValueKind.Reference);
        private T Read<T>(StudioAuthoringValueKind expected)
        {
            if (Kind != expected) throw new InvalidOperationException("The authored value is " + Kind + ", not " + expected + ".");
            return (T)value;
        }
        /// <summary>Compares kind and scalar content; strings use ordinal equality.</summary>
        public bool Equals(StudioAuthoringValue other) => other != null && Kind == other.Kind && Equals(value, other.value);
        /// <summary>Compares authored values.</summary>
        public override bool Equals(object obj) => Equals(obj as StudioAuthoringValue);
        /// <summary>Returns a hash compatible with value equality; it is not a persistent identifier.</summary>
        public override int GetHashCode() => unchecked(((int)Kind * 397) ^ (value == null ? 0 : value.GetHashCode()));
    }
}
