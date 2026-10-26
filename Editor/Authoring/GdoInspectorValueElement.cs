using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using KitchenLib.Studio.Generated;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

using KitchenLib.Studio.Editor.Overlays;

namespace KitchenLib.Studio.Editor.Authoring
{
    internal static class GdoInspectorValueElement
    {
        public static GdoOverlayValue ApplyScalar(string value) => GdoOverlayValue.Scalar(value ?? string.Empty);

        public static GdoOverlayValue ApplyEnum(GdoValueDescriptorData descriptor, string selectedName)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            EnumChoice choice = descriptor.EnumValues.Select(ParseEnum).FirstOrDefault(item => item.Name == selectedName);
            if (choice == null) throw new ArgumentException("Unknown enum choice '" + selectedName + "'.", nameof(selectedName));
            return GdoOverlayValue.Enum(choice.Value);
        }

        public static VisualElement Create(string label, GdoValueDescriptorData descriptor, GdoOverlayValue value,
            Action<GdoOverlayValue> changed)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            if (changed == null) throw new ArgumentNullException(nameof(changed));
            value = GdoOverlayValueEditor.Normalize(descriptor, value);
            var root = new VisualElement();
            root.AddToClassList("kls-gdo-value-root");
            var content = new VisualElement();
            content.AddToClassList("kls-gdo-value");
            root.Add(content);

            Action<GdoOverlayValue> commit = null;
            commit = replacement =>
            {
                changed(replacement);
                content.Clear();
                Draw(content, label, descriptor, replacement, commit);
            };
            Draw(content, label, descriptor, value, commit);
            return root;
        }

        private static void Draw(VisualElement content, string label, GdoValueDescriptorData descriptor,
            GdoOverlayValue value, Action<GdoOverlayValue> changed)
        {
            switch (descriptor.Kind)
            {
                case "Scalar": DrawScalar(content, label, descriptor, value, changed); break;
                case "String": DrawString(content, label, value, changed); break;
                case "Enum": DrawEnum(content, label, descriptor, value, changed); break;
                case "GdoReference": content.Add(new GdoReferenceField(label, descriptor.ExpectedTypeName, value, changed)); break;
                case "Struct": DrawStruct(content, label, descriptor, value, changed); break;
                case "List":
                case "Array":
                case "Set": DrawCollection(content, label, descriptor, value, changed); break;
                case "LocalizationMap": DrawLocales(content, label, value, changed); break;
                case "Nullable": DrawNullable(content, label, descriptor, value, changed); break;
                case "UnityObject":
                    var objectField = new ObjectField(label) { objectType = typeof(UnityEngine.Object), allowSceneObjects = false };
                    objectField.SetEnabled(false);
                    content.Add(objectField);
                    break;
                default: content.Add(new HelpBox("Unsupported field shape: " + descriptor.Kind + " (" + descriptor.RuntimeTypeName + ")", HelpBoxMessageType.Error)); break;
            }
        }

        private static void DrawScalar(VisualElement root, string label, GdoValueDescriptorData descriptor,
            GdoOverlayValue value, Action<GdoOverlayValue> changed)
        {
            if (descriptor.RuntimeTypeName == "System.Boolean")
            {
                var field = new Toggle(label) { value = string.Equals(value.ScalarValue, "true", StringComparison.OrdinalIgnoreCase) };
                field.RegisterValueChangedCallback(evt => changed(GdoOverlayValue.Scalar(evt.newValue ? "true" : "false")));
                root.Add(field);
                return;
            }
            if (descriptor.RuntimeTypeName == "System.Int16" || descriptor.RuntimeTypeName == "System.UInt16" ||
                descriptor.RuntimeTypeName == "System.Int32" || descriptor.RuntimeTypeName == "System.Byte" ||
                descriptor.RuntimeTypeName == "System.SByte")
            {
                var field = new IntegerField(label) { value = ParseInt(value.ScalarValue), isDelayed = true };
                field.RegisterValueChangedCallback(evt => changed(GdoOverlayValue.Scalar(evt.newValue.ToString(CultureInfo.InvariantCulture))));
                root.Add(field);
                return;
            }
            if (descriptor.RuntimeTypeName == "System.Int64" || descriptor.RuntimeTypeName == "System.UInt32")
            {
                var field = new LongField(label) { value = ParseLong(value.ScalarValue), isDelayed = true };
                field.RegisterValueChangedCallback(evt => changed(GdoOverlayValue.Scalar(evt.newValue.ToString(CultureInfo.InvariantCulture))));
                root.Add(field);
                return;
            }
            if (descriptor.RuntimeTypeName == "System.Single")
            {
                var field = new FloatField(label) { value = ParseFloat(value.ScalarValue), isDelayed = true };
                field.RegisterValueChangedCallback(evt => changed(GdoOverlayValue.Scalar(evt.newValue.ToString("R", CultureInfo.InvariantCulture))));
                root.Add(field);
                return;
            }
            if (descriptor.RuntimeTypeName == "System.Double")
            {
                var field = new DoubleField(label) { value = ParseDouble(value.ScalarValue), isDelayed = true };
                field.RegisterValueChangedCallback(evt => changed(GdoOverlayValue.Scalar(evt.newValue.ToString("R", CultureInfo.InvariantCulture))));
                root.Add(field);
                return;
            }
            var text = new TextField(label) { value = value.ScalarValue ?? string.Empty, isDelayed = true };
            text.RegisterValueChangedCallback(evt => changed(ApplyScalar(evt.newValue)));
            root.Add(text);
        }

        private static void DrawString(VisualElement root, string label, GdoOverlayValue value, Action<GdoOverlayValue> changed)
        {
            var field = new TextField(label) { value = value.ScalarValue ?? string.Empty, isDelayed = true };
            field.RegisterValueChangedCallback(evt => changed(GdoOverlayValue.String(evt.newValue)));
            root.Add(field);
        }

        private static void DrawEnum(VisualElement root, string label, GdoValueDescriptorData descriptor,
            GdoOverlayValue value, Action<GdoOverlayValue> changed)
        {
            List<EnumChoice> choices = descriptor.EnumValues.Select(ParseEnum).ToList();
            if (choices.Count == 0) choices.Add(new EnumChoice(value.ScalarValue ?? "0", ParseLong(value.ScalarValue)));
            long current = ParseLong(value.ScalarValue);
            int index = choices.FindIndex(choice => choice.Value == current);
            if (index < 0) { choices.Add(new EnumChoice(current.ToString(CultureInfo.InvariantCulture), current)); index = choices.Count - 1; }
            var popup = new PopupField<string>(label, choices.Select(choice => choice.Name).ToList(), index);
            popup.RegisterValueChangedCallback(evt => changed(ApplyEnum(descriptor, evt.newValue)));
            root.Add(popup);
        }

        private static void DrawStruct(VisualElement root, string label, GdoValueDescriptorData descriptor,
            GdoOverlayValue value, Action<GdoOverlayValue> changed)
        {
            var foldout = new Foldout { text = label, value = true };
            foreach (GdoValueMemberDescriptorData member in descriptor.Members)
            {
                GdoOverlayMember authored = value.Members.FirstOrDefault(item => item.Name == member.Name);
                GdoOverlayValue memberValue = authored == null ? GdoOverlayValueEditor.CreateDefault(member.Value) : authored.Value;
                foldout.Add(Create(member.Name, member.Value, memberValue,
                    replacement => changed(GdoOverlayValueEditor.ReplaceStructMember(value, member.Name, replacement))));
            }
            root.Add(foldout);
        }

        private static void DrawCollection(VisualElement root, string label, GdoValueDescriptorData descriptor,
            GdoOverlayValue value, Action<GdoOverlayValue> changed)
        {
            var foldout = new Foldout { text = label, value = true };
            for (int index = 0; index < value.Values.Count; index++)
            {
                int captured = index;
                var row = new VisualElement();
                row.Add(Create("Element " + index, descriptor.Element, value.Values[index], replacement =>
                    TryCollectionChange(row, () => GdoOverlayValueEditor.ReplaceCollectionElement(value, captured, replacement), changed)));
                if (value.Kind == GdoOverlayValueKind.List)
                {
                    var up = new Button(() => changed(GdoOverlayValueEditor.MoveCollectionElement(value, captured, captured - 1)))
                        { name = "move-up", text = "Up" };
                    up.SetEnabled(captured > 0);
                    row.Add(up);
                    var down = new Button(() => changed(GdoOverlayValueEditor.MoveCollectionElement(value, captured, captured + 1)))
                        { name = "move-down", text = "Down" };
                    down.SetEnabled(captured < value.Values.Count - 1);
                    row.Add(down);
                    row.Add(new Button(() => changed(GdoOverlayValueEditor.DuplicateCollectionElement(value, captured)))
                        { name = "duplicate-element", text = "Duplicate" });
                }
                row.Add(new Button(() => changed(GdoOverlayValueEditor.RemoveCollectionElement(value, captured))) { text = "Remove" });
                foldout.Add(row);
            }
            foldout.Add(new Button(() => TryCollectionChange(foldout, () =>
                GdoOverlayValueEditor.InsertCollectionElement(value, value.Values.Count,
                    GdoOverlayValueEditor.CreateDefault(descriptor.Element)), changed)) { name = "add-element", text = "Add" });
            root.Add(foldout);
        }

        private static void DrawLocales(VisualElement root, string label, GdoOverlayValue value, Action<GdoOverlayValue> changed)
        {
            var foldout = new Foldout { text = label, value = true };
            foreach (GdoLocaleEntry locale in value.Locales)
            {
                GdoLocaleEntry captured = locale;
                var row = new Foldout { text = "Locale " + locale.Locale, value = true };
                var key = new IntegerField("Locale") { name = "locale-key", value = locale.Locale, isDelayed = true };
                key.RegisterValueChangedCallback(evt =>
                {
                    try { changed(GdoOverlayValueEditor.RenameLocale(value, captured.Locale, evt.newValue)); }
                    catch (ArgumentException exception)
                    {
                        key.SetValueWithoutNotify(captured.Locale);
                        HelpBox existing = row.Q<HelpBox>("locale-key-error");
                        if (existing != null) existing.RemoveFromHierarchy();
                        row.Add(new HelpBox(exception.Message, HelpBoxMessageType.Error) { name = "locale-key-error" });
                    }
                });
                row.Add(key);
                row.Add(LocaleText("Display Name", "locale-display-name", locale.DisplayName,
                    text => changed(GdoOverlayValueEditor.UpsertLocale(value,
                        new GdoLocaleEntry(captured.Locale, text, captured.Description, captured.FlavourText)))));
                row.Add(LocaleText("Description", "locale-description", locale.Description,
                    text => changed(GdoOverlayValueEditor.UpsertLocale(value,
                        new GdoLocaleEntry(captured.Locale, captured.DisplayName, text, captured.FlavourText)))));
                row.Add(LocaleText("Flavour Text", "locale-flavour-text", locale.FlavourText,
                    text => changed(GdoOverlayValueEditor.UpsertLocale(value,
                        new GdoLocaleEntry(captured.Locale, captured.DisplayName, captured.Description, text)))));
                row.Add(new Button(() => changed(GdoOverlayValueEditor.RemoveLocale(value, captured.Locale)))
                    { name = "remove-locale", text = "Remove Locale" });
                foldout.Add(row);
            }
            foldout.Add(new Button(() =>
            {
                int locale = value.Locales.Count == 0 ? 0 : value.Locales.Max(item => item.Locale) + 1;
                changed(GdoOverlayValueEditor.UpsertLocale(value, new GdoLocaleEntry(locale, string.Empty)));
            }) { name = "add-locale", text = "Add Locale" });
            root.Add(foldout);
        }

        private static TextField LocaleText(string label, string name, string value, Action<string> changed)
        {
            var field = new TextField(label) { name = name, value = value ?? string.Empty, isDelayed = true };
            field.RegisterValueChangedCallback(evt => changed(evt.newValue));
            return field;
        }

        private static void TryCollectionChange(VisualElement row, Func<GdoOverlayValue> operation, Action<GdoOverlayValue> changed)
        {
            HelpBox existing = row.Q<HelpBox>("collection-error");
            if (existing != null) existing.RemoveFromHierarchy();
            try { changed(operation()); }
            catch (ArgumentException exception)
            {
                row.Add(new HelpBox(exception.Message, HelpBoxMessageType.Error) { name = "collection-error" });
            }
        }

        private static void DrawNullable(VisualElement root, string label, GdoValueDescriptorData descriptor,
            GdoOverlayValue value, Action<GdoOverlayValue> changed)
        {
            bool hasValue = value.Kind != GdoOverlayValueKind.Null;
            var toggle = new Toggle(label + " Has Value") { value = hasValue };
            toggle.RegisterValueChangedCallback(evt => changed(evt.newValue
                ? GdoOverlayValueEditor.CreateDefault(descriptor.Element) : GdoOverlayValue.Null()));
            root.Add(toggle);
            if (hasValue) root.Add(Create(label, descriptor.Element, value, changed));
        }

        private static EnumChoice ParseEnum(string text)
        {
            int separator = text.LastIndexOf('=');
            return separator < 0 ? new EnumChoice(text, 0) :
                new EnumChoice(text.Substring(0, separator), ParseLong(text.Substring(separator + 1)));
        }

        private static long ParseLong(string value) => long.TryParse(value, NumberStyles.Integer,
            CultureInfo.InvariantCulture, out long result) ? result : 0;

        private static int ParseInt(string value) => int.TryParse(value, NumberStyles.Integer,
            CultureInfo.InvariantCulture, out int result) ? result : 0;

        private static float ParseFloat(string value) => float.TryParse(value, NumberStyles.Float,
            CultureInfo.InvariantCulture, out float result) ? result : 0f;

        private static double ParseDouble(string value) => double.TryParse(value, NumberStyles.Float,
            CultureInfo.InvariantCulture, out double result) ? result : 0d;

        private sealed class EnumChoice
        {
            public EnumChoice(string name, long value) { Name = name; Value = value; }
            public string Name { get; }
            public long Value { get; }
        }
    }
}
