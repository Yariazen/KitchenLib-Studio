using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using KitchenLib.Studio.Generated;

namespace KitchenLib.Studio.Editor
{
    public static class GdoOverlayValueEditor
    {
        public static GdoOverlayValue CreateDefault(GdoValueDescriptorData descriptor)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            switch (descriptor.Kind)
            {
                case "Scalar": return GdoOverlayValue.Scalar(DefaultScalar(descriptor.RuntimeTypeName));
                case "String": return GdoOverlayValue.String(string.Empty);
                case "Enum": return GdoOverlayValue.Enum(DefaultEnum(descriptor.EnumValues));
                case "GdoReference":
                case "Nullable":
                case "UnityObject": return GdoOverlayValue.Null();
                case "Struct": return GdoOverlayValue.Struct(descriptor.Members.Select(member =>
                    new GdoOverlayMember(member.Name, CreateDefault(member.Value))));
                case "List":
                case "Array": return GdoOverlayValue.List(new GdoOverlayValue[0]);
                case "Set": return GdoOverlayValue.Set(new GdoOverlayValue[0]);
                case "LocalizationMap": return GdoOverlayValue.LocalizationMap(new GdoLocaleEntry[0]);
                default: throw new NotSupportedException("Cannot create an editor value for " + descriptor.Kind +
                    " shape '" + descriptor.RuntimeTypeName + "'.");
            }
        }

        public static GdoOverlayValue Normalize(GdoValueDescriptorData descriptor, GdoOverlayValue value)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            if (value == null) return CreateDefault(descriptor);
            GdoOverlayValueKind expected;
            switch (descriptor.Kind)
            {
                case "Scalar": expected = GdoOverlayValueKind.Scalar; break;
                case "String": expected = GdoOverlayValueKind.String; break;
                case "Enum": expected = GdoOverlayValueKind.Enum; break;
                case "GdoReference": return value.Kind == GdoOverlayValueKind.GdoReference || value.Kind == GdoOverlayValueKind.Null ? value : CreateDefault(descriptor);
                case "Struct": expected = GdoOverlayValueKind.Struct; break;
                case "List":
                case "Array": expected = GdoOverlayValueKind.List; break;
                case "Set": expected = GdoOverlayValueKind.Set; break;
                case "LocalizationMap": expected = GdoOverlayValueKind.LocalizationMap; break;
                case "Nullable": return value.Kind == GdoOverlayValueKind.Null ? value : Normalize(descriptor.Element, value);
                case "UnityObject": return value.Kind == GdoOverlayValueKind.Null ? value : CreateDefault(descriptor);
                default: return CreateDefault(descriptor);
            }
            if (value.Kind != expected) return CreateDefault(descriptor);
            return expected == GdoOverlayValueKind.Scalar && !IsValidScalar(descriptor.RuntimeTypeName, value.ScalarValue)
                ? CreateDefault(descriptor) : value;
        }

        public static bool IsValidScalar(string runtimeTypeName, string value)
        {
            switch (runtimeTypeName)
            {
                case "System.Boolean": return bool.TryParse(value, out _);
                case "System.Byte": return byte.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
                case "System.SByte": return sbyte.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
                case "System.Int16": return short.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
                case "System.UInt16": return ushort.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
                case "System.Int32": return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
                case "System.UInt32": return uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
                case "System.Int64": return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
                case "System.UInt64": return ulong.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
                case "System.Single": return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
                case "System.Double": return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
                case "System.Decimal": return decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
                case "System.Char": return value != null && value.Length == 1;
                default: return value != null;
            }
        }

        public static GdoOverlayValue ReplaceStructMember(GdoOverlayValue root, string memberName, GdoOverlayValue value)
        {
            RequireKind(root, GdoOverlayValueKind.Struct);
            List<GdoOverlayMember> members = root.Members.Where(member => member.Name != memberName).ToList();
            members.Add(new GdoOverlayMember(memberName, value ?? throw new ArgumentNullException(nameof(value))));
            return GdoOverlayValue.Struct(members);
        }

        public static GdoOverlayValue InsertCollectionElement(GdoOverlayValue root, int index, GdoOverlayValue value)
        {
            List<GdoOverlayValue> values = Collection(root);
            if (index < 0 || index > values.Count) throw new ArgumentOutOfRangeException(nameof(index));
            values.Insert(index, value ?? throw new ArgumentNullException(nameof(value)));
            return RebuildCollection(root.Kind, values);
        }

        public static GdoOverlayValue ReplaceCollectionElement(GdoOverlayValue root, int index, GdoOverlayValue value)
        {
            List<GdoOverlayValue> values = Collection(root);
            if (index < 0 || index >= values.Count) throw new ArgumentOutOfRangeException(nameof(index));
            values[index] = value ?? throw new ArgumentNullException(nameof(value));
            return RebuildCollection(root.Kind, values);
        }

        public static GdoOverlayValue RemoveCollectionElement(GdoOverlayValue root, int index)
        {
            List<GdoOverlayValue> values = Collection(root);
            if (index < 0 || index >= values.Count) throw new ArgumentOutOfRangeException(nameof(index));
            values.RemoveAt(index);
            return RebuildCollection(root.Kind, values);
        }

        public static GdoOverlayValue MoveCollectionElement(GdoOverlayValue root, int fromIndex, int toIndex)
        {
            if (root == null || root.Kind != GdoOverlayValueKind.List)
                throw new ArgumentException("Only lists have meaningful ordering.", nameof(root));
            List<GdoOverlayValue> values = root.Values.ToList();
            if (fromIndex < 0 || fromIndex >= values.Count) throw new ArgumentOutOfRangeException(nameof(fromIndex));
            if (toIndex < 0 || toIndex >= values.Count) throw new ArgumentOutOfRangeException(nameof(toIndex));
            GdoOverlayValue value = values[fromIndex];
            values.RemoveAt(fromIndex);
            values.Insert(toIndex, value);
            return GdoOverlayValue.List(values);
        }

        public static GdoOverlayValue DuplicateCollectionElement(GdoOverlayValue root, int index)
        {
            if (root == null || root.Kind != GdoOverlayValueKind.List)
                throw new ArgumentException("Set entries cannot be duplicated.", nameof(root));
            if (index < 0 || index >= root.Values.Count) throw new ArgumentOutOfRangeException(nameof(index));
            return InsertCollectionElement(root, index + 1, root.Values[index]);
        }

        public static GdoOverlayValue UpsertLocale(GdoOverlayValue root, GdoLocaleEntry entry)
        {
            RequireKind(root, GdoOverlayValueKind.LocalizationMap);
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            List<GdoLocaleEntry> entries = root.Locales.Where(item => item.Locale != entry.Locale).ToList();
            entries.Add(entry);
            return GdoOverlayValue.LocalizationMap(entries);
        }

        public static GdoOverlayValue RemoveLocale(GdoOverlayValue root, int locale)
        {
            RequireKind(root, GdoOverlayValueKind.LocalizationMap);
            if (!root.Locales.Any(item => item.Locale == locale))
                throw new ArgumentException("Locale '" + locale + "' does not exist.", nameof(locale));
            return GdoOverlayValue.LocalizationMap(root.Locales.Where(item => item.Locale != locale));
        }

        public static GdoOverlayValue RenameLocale(GdoOverlayValue root, int currentLocale, int newLocale)
        {
            RequireKind(root, GdoOverlayValueKind.LocalizationMap);
            GdoLocaleEntry current = root.Locales.SingleOrDefault(item => item.Locale == currentLocale);
            if (current == null) throw new ArgumentException("Locale '" + currentLocale + "' does not exist.", nameof(currentLocale));
            if (currentLocale != newLocale && root.Locales.Any(item => item.Locale == newLocale))
                throw new ArgumentException("Locale '" + newLocale + "' already exists.", nameof(newLocale));
            return UpsertLocale(RemoveLocale(root, currentLocale),
                new GdoLocaleEntry(newLocale, current.DisplayName, current.Description, current.FlavourText));
        }

        private static List<GdoOverlayValue> Collection(GdoOverlayValue root)
        {
            if (root == null || (root.Kind != GdoOverlayValueKind.List && root.Kind != GdoOverlayValueKind.Set))
                throw new ArgumentException("Value must be a list or set.", nameof(root));
            return root.Values.ToList();
        }

        private static GdoOverlayValue RebuildCollection(GdoOverlayValueKind kind, IEnumerable<GdoOverlayValue> values) =>
            kind == GdoOverlayValueKind.Set ? GdoOverlayValue.Set(values) : GdoOverlayValue.List(values);

        private static void RequireKind(GdoOverlayValue value, GdoOverlayValueKind kind)
        {
            if (value == null || value.Kind != kind) throw new ArgumentException("Value must be " + kind + ".", nameof(value));
        }

        private static string DefaultScalar(string runtimeTypeName) =>
            runtimeTypeName == "System.Boolean" ? "false" : runtimeTypeName == "System.Char" ? "\0" : "0";

        private static long DefaultEnum(IReadOnlyList<string> values)
        {
            if (values == null || values.Count == 0) return 0;
            string value = values[0];
            int separator = value.LastIndexOf('=');
            return separator >= 0 && long.TryParse(value.Substring(separator + 1), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out long parsed) ? parsed : 0;
        }
    }
}
