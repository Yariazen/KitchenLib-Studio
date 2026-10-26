using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KitchenLib.Studio.Editor.Overlays
{
    internal enum GdoOverlayValueKind { Null, Scalar, Enum, String, GdoReference, Struct, List, Set, LocalizationMap }

    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "KitchenLib.Studio.Editor", "KitchenLib.Studio.Editor", "GdoLocaleEntry")]
    internal sealed class GdoLocaleEntry
    {
        [SerializeField] private int _locale;
        [SerializeField] private string _displayName;
        [SerializeField] private string _description;
        [SerializeField] private string _flavourText;

        public int Locale => _locale;
        public string DisplayName => _displayName;
        public string Description => _description;
        public string FlavourText => _flavourText;

        public GdoLocaleEntry(int locale, string displayName)
            : this(locale, displayName, string.Empty, string.Empty) { }

        public GdoLocaleEntry(int locale, string displayName, string description, string flavourText)
        {
            _locale = locale;
            _displayName = displayName ?? string.Empty;
            _description = description ?? string.Empty;
            _flavourText = flavourText ?? string.Empty;
        }
    }

    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "KitchenLib.Studio.Editor", "KitchenLib.Studio.Editor", "GdoOverlayMember")]
    internal sealed class GdoOverlayMember
    {
        [SerializeField] private string _name;
        [SerializeField] private GdoOverlayValue _value;
        public string Name => _name;
        public GdoOverlayValue Value => _value;
        public GdoOverlayMember(string name, GdoOverlayValue value) { _name = name; _value = value; }
    }

    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "KitchenLib.Studio.Editor", "KitchenLib.Studio.Editor", "GdoOverlayValue")]
    internal sealed class GdoOverlayValue
    {
        [SerializeField] private GdoOverlayValueKind _kind;
        [SerializeField] private string _scalar;
        [SerializeField] private GdoReferenceToken _reference;
        [SerializeField] private List<GdoOverlayMember> _members = new List<GdoOverlayMember>();
        [SerializeField] private List<GdoOverlayValue> _values = new List<GdoOverlayValue>();
        [SerializeField] private List<GdoLocaleEntry> _locales = new List<GdoLocaleEntry>();

        public GdoOverlayValueKind Kind => _kind;
        public string ScalarValue => _scalar;
        public GdoReferenceToken ReferenceValue => _reference;
        public IReadOnlyList<GdoOverlayMember> Members => _members;
        public IReadOnlyList<GdoOverlayValue> Values => _values;
        public IReadOnlyList<GdoLocaleEntry> Locales => _locales;

        public static GdoOverlayValue Null() => new GdoOverlayValue { _kind = GdoOverlayValueKind.Null };
        public static GdoOverlayValue Scalar(string value) => new GdoOverlayValue
        {
            _kind = GdoOverlayValueKind.Scalar,
            _scalar = value ?? throw new ArgumentNullException(nameof(value))
        };
        public static GdoOverlayValue Enum(long value) => new GdoOverlayValue { _kind = GdoOverlayValueKind.Enum, _scalar = value.ToString(System.Globalization.CultureInfo.InvariantCulture) };
        public static GdoOverlayValue String(string value) => new GdoOverlayValue { _kind = GdoOverlayValueKind.String, _scalar = value ?? string.Empty };

        public static GdoOverlayValue Reference(GdoReferenceToken token) => new GdoOverlayValue
        {
            _kind = GdoOverlayValueKind.GdoReference,
            _reference = token ?? throw new ArgumentNullException(nameof(token))
        };

        public static GdoOverlayValue Struct(IEnumerable<GdoOverlayMember> members)
        {
            if (members == null) throw new ArgumentNullException(nameof(members));
            List<GdoOverlayMember> ordered = members
                .Select(member => member ?? throw new ArgumentException("Struct members cannot be null.", nameof(members)))
                .OrderBy(member => member.Name, StringComparer.Ordinal)
                .ToList();
            if (ordered.Any(member => string.IsNullOrWhiteSpace(member.Name)) ||
                ordered.Select(member => member.Name).Distinct(StringComparer.Ordinal).Count() != ordered.Count)
                throw new ArgumentException("Struct member names must be non-empty and unique.", nameof(members));
            return new GdoOverlayValue { _kind = GdoOverlayValueKind.Struct, _members = ordered };
        }

        public static GdoOverlayValue List(IEnumerable<GdoOverlayValue> values) =>
            Collection(GdoOverlayValueKind.List, values);

        public static GdoOverlayValue Set(IEnumerable<GdoOverlayValue> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            List<GdoOverlayValue> materialized = Materialize(values);
            string[] keys = materialized.Select(CanonicalKey).ToArray();
            if (keys.Distinct(StringComparer.Ordinal).Count() != keys.Length)
                throw new ArgumentException("Set values must be structurally unique.", nameof(values));
            return new GdoOverlayValue { _kind = GdoOverlayValueKind.Set, _values = materialized };
        }

        public static GdoOverlayValue LocalizationMap(IEnumerable<GdoLocaleEntry> entries)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            List<GdoLocaleEntry> ordered = entries.OrderBy(entry => entry.Locale).ToList();
            if (ordered.Select(entry => entry.Locale).Distinct().Count() != ordered.Count)
                throw new ArgumentException("Locales must be unique.", nameof(entries));
            return new GdoOverlayValue { _kind = GdoOverlayValueKind.LocalizationMap, _locales = ordered };
        }

        private static GdoOverlayValue Collection(GdoOverlayValueKind kind, IEnumerable<GdoOverlayValue> values) =>
            new GdoOverlayValue { _kind = kind, _values = Materialize(values) };

        private static List<GdoOverlayValue> Materialize(IEnumerable<GdoOverlayValue> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            return values.Select(value => value ?? throw new ArgumentException("Values cannot contain null.", nameof(values))).ToList();
        }

        private static string CanonicalKey(GdoOverlayValue value)
        {
            switch (value.Kind)
            {
                case GdoOverlayValueKind.Null: return "null";
                case GdoOverlayValueKind.Scalar:
                case GdoOverlayValueKind.Enum:
                case GdoOverlayValueKind.String: return value.Kind + ":" + value.ScalarValue;
                case GdoOverlayValueKind.GdoReference:
                    return "ref:" + value.ReferenceValue.ExpectedType + ":" + value.ReferenceValue.Id;
                case GdoOverlayValueKind.Struct:
                    return "struct:{" + string.Join(",", value.Members.OrderBy(member => member.Name, StringComparer.Ordinal)
                        .Select(member => member.Name + "=" + CanonicalKey(member.Value))) + "}";
                case GdoOverlayValueKind.List:
                case GdoOverlayValueKind.Set:
                    return value.Kind + ":[" + string.Join(",", value.Values.Select(CanonicalKey)) + "]";
                case GdoOverlayValueKind.LocalizationMap:
                    return "locales:[" + string.Join(",", value.Locales.OrderBy(entry => entry.Locale)
                        .Select(entry => entry.Locale + "=" + entry.DisplayName)) + "]";
                default: throw new ArgumentOutOfRangeException();
            }
        }
    }
}
