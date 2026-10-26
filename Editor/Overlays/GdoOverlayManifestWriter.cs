using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using KitchenData;
using KitchenLib.Studio.Generated;
using UnityEngine;

using KitchenLib.Studio.Editor.Authoring;
using KitchenLib.Studio.Editor.Validation;

namespace KitchenLib.Studio.Editor.Overlays
{
    internal static class GdoOverlayManifestWriter
    {
        public static string Write(IEnumerable<GameDataObject> targets, IEnumerable<GdoOverlayAuthoringAsset> overlays, string schemaPath)
        {
            if (targets == null) throw new ArgumentNullException(nameof(targets));
            if (overlays == null) throw new ArgumentNullException(nameof(overlays));
            if (string.IsNullOrEmpty(schemaPath) || !File.Exists(schemaPath))
                throw new InvalidOperationException("Mirror metadata does not exist.");
            GameDataObject[] built = targets.Where(value => value != null).ToArray();
            if (built.GroupBy(value => value.ID).Any(group => group.Count() > 1))
                throw new InvalidOperationException("Built GDO target IDs must be unique.");
            GdoOverlayAuthoringAsset[] authored = overlays.Where(value => value != null).ToArray();
            if (authored.GroupBy(value => value.Target).Any(group => group.Count() > 1))
                throw new InvalidOperationException("A target may have only one overlay.");
            foreach (GdoOverlayAuthoringAsset overlay in authored)
            {
                if (overlay.Target == null || !built.Contains(overlay.Target))
                    throw new InvalidOperationException("Overlay target is not included in the bundle.");
                if (overlay.Fields.GroupBy(FieldKey, StringComparer.Ordinal).Any(group => group.Count() > 1))
                    throw new InvalidOperationException("Overlay fields must be unique.");
                if (!GdoOverlayLimits.TryValidate(overlay.Fields.Select(field => field.Value), out GdoOverlayPersistenceDiagnostic limitIssue))
                    throw new InvalidOperationException(limitIssue.Category + " at " + limitIssue.Path + ": " + limitIssue.Message);
                HashSet<string> supplied = new HashSet<string>(overlay.Fields.Select(FieldKey), StringComparer.Ordinal);
                GdoTypeDescriptorView targetDescriptor = GdoDescriptorCatalog.ForTarget(overlay.Target);
                if (!targetDescriptor.IsAvailable)
                    throw new InvalidOperationException("GDO type is unavailable: " + targetDescriptor.RuntimeTypeName + ".");
                string missing = RequiredReconstructed(overlay.Target.GetType()).FirstOrDefault(field => !supplied.Contains(field));
                if (missing != null) throw new InvalidOperationException("Missing required reconstructed field " + missing.Replace('|', '.') + ".");
                foreach (GdoOverlayField field in overlay.Fields)
                {
                    GdoFieldDescriptorData metadata = FindField(field.DeclaringType, field.FieldName);
                    if (metadata == null) throw new InvalidOperationException("Unknown schema field " + field.DeclaringType + "." + field.FieldName + ".");
                    if (metadata.Classification == "Native" || metadata.Classification == "RuntimeDerived")
                        throw new InvalidOperationException("Field is not sidecar-authored: " + field.DeclaringType + "." + field.FieldName + ".");
                }
                ValidationIssue issue = GdoDescriptorValueValidator.Validate(overlay.Target, overlay, targetDescriptor).FirstOrDefault();
                if (issue != null) throw new InvalidOperationException(issue.PropertyPath + ": " + issue.Message);
            }

            var text = new StringBuilder();
            text.Append("{\n  \"schemaVersion\": 3,\n  \"globalFingerprint\": ").Append(Quote(GdoDescriptorCatalog.GlobalFingerprint));
            text.Append(",\n  \"targets\": [\n");
            GdoOverlayAuthoringAsset[] ordered = authored.OrderBy(value => value.Target.ID).ToArray();
            for (int i = 0; i < ordered.Length; i++)
            {
                GdoOverlayAuthoringAsset overlay = ordered[i];
                text.Append("    {\n      \"gdoId\": ").Append(overlay.Target.ID).Append(",\n      \"gdoType\": ")
                    .Append(Quote(overlay.Target.GetType().FullName)).Append(",\n      \"descriptorFingerprint\": ")
                    .Append(Quote(GdoDescriptorCatalog.ForTarget(overlay.Target).DescriptorFingerprint)).Append(",\n      \"fields\": [\n");
                GdoOverlayField[] fields = overlay.Fields.OrderBy(value => value.DeclaringType, StringComparer.Ordinal)
                    .ThenBy(value => value.FieldName, StringComparer.Ordinal).ToArray();
                for (int j = 0; j < fields.Length; j++)
                {
                    GdoOverlayField field = fields[j];
                    text.Append("        {\"declaringType\":").Append(Quote(field.DeclaringType)).Append(",\"field\":")
                        .Append(Quote(field.FieldName)).Append(",\"value\":");
                    AppendValue(text, field.Value);
                    text.Append('}'); if (j + 1 < fields.Length) text.Append(','); text.Append('\n');
                }
                text.Append("      ]\n    }"); if (i + 1 < ordered.Length) text.Append(','); text.Append('\n');
            }
            return text.Append("  ]\n}\n").ToString();
        }

        private static void AppendValue(StringBuilder text, GdoOverlayValue value)
        {
            text.Append("{\"kind\":").Append(Quote(value.Kind.ToString()));
            switch (value.Kind)
            {
                case GdoOverlayValueKind.Null: break;
                case GdoOverlayValueKind.Scalar:
                case GdoOverlayValueKind.Enum:
                case GdoOverlayValueKind.String: text.Append(",\"value\":").Append(Quote(value.ScalarValue)); break;
                case GdoOverlayValueKind.GdoReference:
                    text.Append(",\"expectedType\":").Append(Quote(value.ReferenceValue.ExpectedType)).Append(",\"id\":").Append(value.ReferenceValue.Id); break;
                case GdoOverlayValueKind.Struct:
                    text.Append(",\"members\":["); AppendMembers(text, value.Members.OrderBy(member => member.Name, StringComparer.Ordinal)); text.Append(']'); break;
                case GdoOverlayValueKind.List:
                    text.Append(",\"values\":["); AppendValues(text, value.Values); text.Append(']'); break;
                case GdoOverlayValueKind.Set:
                    text.Append(",\"values\":["); AppendValues(text, value.Values.OrderBy(Canonical, StringComparer.Ordinal)); text.Append(']'); break;
                case GdoOverlayValueKind.LocalizationMap:
                    text.Append(",\"locales\":[");
                    GdoLocaleEntry[] locales = value.Locales.OrderBy(entry => entry.Locale).ToArray();
                    for (int i = 0; i < locales.Length; i++) { if (i > 0) text.Append(','); text.Append("{\"locale\":").Append(locales[i].Locale).Append(",\"displayName\":").Append(Quote(locales[i].DisplayName)).Append(",\"description\":").Append(Quote(locales[i].Description)).Append(",\"flavourText\":").Append(Quote(locales[i].FlavourText)).Append('}'); }
                    text.Append(']'); break;
                default: throw new InvalidOperationException("Unsupported overlay node kind.");
            }
            text.Append('}');
        }

        private static void AppendMembers(StringBuilder text, IEnumerable<GdoOverlayMember> members)
        { int i = 0; foreach (GdoOverlayMember member in members) { if (i++ > 0) text.Append(','); text.Append("{\"name\":").Append(Quote(member.Name)).Append(",\"value\":"); AppendValue(text, member.Value); text.Append('}'); } }
        private static void AppendValues(StringBuilder text, IEnumerable<GdoOverlayValue> values)
        { int i = 0; foreach (GdoOverlayValue value in values) { if (i++ > 0) text.Append(','); AppendValue(text, value); } }
        private static string Canonical(GdoOverlayValue value) { var text = new StringBuilder(); AppendValue(text, value); return text.ToString(); }
        private static string Quote(string value) => "\"" + (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t") + "\"";

        private static string FieldKey(GdoOverlayField field) => field.DeclaringType + "|" + field.FieldName;
        private static GdoFieldDescriptorData FindField(string declaringType, string fieldName)
        {
            GdoTypeDescriptorView descriptor = GdoDescriptorCatalog.ForRuntimeType(declaringType);
            return descriptor == null ? null : descriptor.Data.Fields.SingleOrDefault(field => field.Name == fieldName);
        }
        private static IEnumerable<string> RequiredReconstructed(Type targetType)
        {
            while (targetType != null)
            {
                GdoTypeDescriptorView descriptor = GdoDescriptorCatalog.ForRuntimeType(RuntimeName(targetType));
                if (descriptor != null)
                    foreach (GdoFieldDescriptorData field in descriptor.Data.Fields.Where(field => field.IsRequired))
                        yield return field.DeclaringTypeName + "|" + field.Name;
                targetType = targetType.BaseType;
            }
        }
        private static string RuntimeName(Type type) => type.IsGenericType ? type.GetGenericTypeDefinition().FullName : type.FullName;
        private static Type FindType(Type type, string name) { while (type != null && RuntimeName(type) != name) type = type.BaseType; return type; }
        private static void ValidateValue(GdoOverlayValue value, Type targetType, string path)
        {
            if (value == null) throw new InvalidOperationException("Missing overlay value at " + path + ".");
            if (value.Kind == GdoOverlayValueKind.GdoReference)
            {
                if (!typeof(GameDataObject).IsAssignableFrom(targetType) || value.ReferenceValue == null || value.ReferenceValue.ExpectedType != RuntimeName(targetType))
                    throw new InvalidOperationException("Incompatible GDO reference at " + path + ".");
                if (value.ReferenceValue.Asset != null && !targetType.IsInstanceOfType(value.ReferenceValue.Asset))
                    throw new InvalidOperationException("Incompatible project asset reference at " + path + ".");
                return;
            }
            if (value.Kind == GdoOverlayValueKind.Null)
            {
                if (targetType.IsValueType) throw new InvalidOperationException("Null cannot populate value type at " + path + ".");
                return;
            }
            if (value.Kind == GdoOverlayValueKind.LocalizationMap)
            {
                if (!targetType.IsGenericType || targetType.GetGenericTypeDefinition().FullName != "KitchenData.LocalisationObject`1")
                    throw new InvalidOperationException("Localization node kind mismatch at " + path + ".");
                return;
            }
            if (value.Kind == GdoOverlayValueKind.List || value.Kind == GdoOverlayValueKind.Set)
            {
                Type element = targetType.IsGenericType ? targetType.GetGenericArguments().Single() : null;
                if (element == null || (value.Kind == GdoOverlayValueKind.List && targetType.GetGenericTypeDefinition() != typeof(List<>)) ||
                    (value.Kind == GdoOverlayValueKind.Set && targetType.GetGenericTypeDefinition() != typeof(HashSet<>)))
                    throw new InvalidOperationException("Collection node kind mismatch at " + path + ".");
                for (int i = 0; i < value.Values.Count; i++) ValidateValue(value.Values[i], element, path + "." + i);
                return;
            }
            if (value.Kind == GdoOverlayValueKind.Struct)
            {
                if (!targetType.IsValueType || targetType.IsPrimitive || targetType.IsEnum)
                    throw new InvalidOperationException("Struct node kind mismatch at " + path + ".");
                FieldInfo[] fields = targetType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (FieldInfo field in fields)
                {
                    GdoOverlayMember member = value.Members.SingleOrDefault(candidate => candidate.Name == field.Name);
                    if (member == null) throw new InvalidOperationException("Missing required nested member " + path + "." + field.Name + ".");
                    ValidateValue(member.Value, field.FieldType, path + "." + field.Name);
                }
                if (value.Members.Any(member => fields.All(field => field.Name != member.Name)))
                    throw new InvalidOperationException("Unknown nested member at " + path + ".");
                return;
            }
            if (value.Kind == GdoOverlayValueKind.String && targetType != typeof(string)) throw new InvalidOperationException("String node kind mismatch at " + path + ".");
            if (value.Kind == GdoOverlayValueKind.Enum && !targetType.IsEnum) throw new InvalidOperationException("Enum node kind mismatch at " + path + ".");
            if (value.Kind == GdoOverlayValueKind.Scalar && (targetType.IsEnum || targetType == typeof(string) || typeof(GameDataObject).IsAssignableFrom(targetType)))
                throw new InvalidOperationException("Scalar node kind mismatch at " + path + ".");
        }

    }
}
