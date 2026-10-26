using System;
using System.Collections.Generic;
using System.Linq;
using KitchenData;
using KitchenLib.Studio.Generated;
using UnityEditor;

using KitchenLib.Studio.Editor.Authoring;
using KitchenLib.Studio.Editor.Overlays;

namespace KitchenLib.Studio.Editor.Validation
{
    internal static class GdoDescriptorValueValidator
    {
        public static IReadOnlyList<ValidationIssue> Validate(GameDataObject target, GdoOverlayAuthoringAsset companion, GdoTypeDescriptorView descriptor)
        {
            var issues = new List<ValidationIssue>();
            string assetPath = AssetDatabase.GetAssetPath(target);
            if (!descriptor.IsAvailable)
            {
                foreach (GdoDiagnosticData diagnostic in descriptor.Data.Diagnostics)
                    issues.Add(new ValidationIssue(ValidationSeverity.Fatal, assetPath, diagnostic.Path, diagnostic.Message));
                return issues;
            }
            if (companion != null && !string.Equals(companion.DescriptorFingerprint, descriptor.DescriptorFingerprint, StringComparison.Ordinal))
                issues.Add(new ValidationIssue(ValidationSeverity.Fatal, assetPath, "SchemaFingerprint",
                    "Authored overlay descriptor fingerprint does not match the generated PlateUp schema. Review the data and accept the current descriptor before building."));
            foreach (GdoFieldDescriptorData required in RequiredFields(target.GetType()))
            {
                if (companion == null || !companion.TryGetField(required.DeclaringTypeName, required.Name, out GdoOverlayField authored))
                    issues.Add(new ValidationIssue(ValidationSeverity.Fatal, assetPath,
                        required.DeclaringTypeName + "." + required.Name, "Required overlay field is missing."));
                else if (authored.Value == null || authored.Value.Kind == GdoOverlayValueKind.Null)
                    issues.Add(new ValidationIssue(ValidationSeverity.Fatal, assetPath,
                        required.DeclaringTypeName + "." + required.Name, "Required overlay field cannot be null."));
            }
            if (companion == null) return issues;
            if (!GdoOverlayLimits.TryValidate(companion.Fields.Select(field => field.Value), out GdoOverlayPersistenceDiagnostic limitIssue))
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Fatal, assetPath, limitIssue.Path, limitIssue.Category + ": " + limitIssue.Message));
                return issues;
            }
            foreach (GdoOverlayField authored in companion.Fields)
            {
                GdoTypeDescriptorView declaringDescriptor = GdoDescriptorCatalog.ForRuntimeType(authored.DeclaringType);
                GdoFieldDescriptorData field = declaringDescriptor == null ? null : declaringDescriptor.Data.Fields.FirstOrDefault(item => item.Name == authored.FieldName);
                if (field == null)
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Fatal, assetPath, authored.DeclaringType + "." + authored.FieldName, "Field is absent from the generated descriptor."));
                    continue;
                }
                ValidateValue(authored.Value, field.Value, authored.DeclaringType + "." + authored.FieldName, assetPath, issues);
            }
            return issues;
        }

        private static void ValidateValue(GdoOverlayValue value, GdoValueDescriptorData descriptor, string path, string assetPath, ICollection<ValidationIssue> issues)
        {
            if (value == null)
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Fatal, assetPath, path, "Overlay value is missing."));
                return;
            }
            string expected = descriptor.Kind == "GdoReference" ? "GdoReference" : descriptor.Kind;
            if (descriptor.Kind == "Array") expected = "List";
            if (!string.Equals(value.Kind.ToString(), expected, StringComparison.Ordinal) && value.Kind != GdoOverlayValueKind.Null)
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Fatal, assetPath, path, "Expected " + descriptor.Kind + " node, found " + value.Kind + "."));
                return;
            }
            if (value.Kind == GdoOverlayValueKind.GdoReference && (value.ReferenceValue == null || value.ReferenceValue.Id == 0))
                issues.Add(new ValidationIssue(ValidationSeverity.Fatal, assetPath, path, "GDO reference ID must be non-zero."));
            else if (value.Kind == GdoOverlayValueKind.GdoReference &&
                !string.Equals(value.ReferenceValue.ExpectedType, descriptor.ExpectedTypeName, StringComparison.Ordinal))
                issues.Add(new ValidationIssue(ValidationSeverity.Fatal, assetPath, path,
                    "GDO reference expected type '" + value.ReferenceValue.ExpectedType + "' does not match descriptor type '" + descriptor.ExpectedTypeName + "'."));
            if (value.Kind == GdoOverlayValueKind.Scalar && !GdoOverlayValueEditor.IsValidScalar(descriptor.RuntimeTypeName, value.ScalarValue))
                issues.Add(new ValidationIssue(ValidationSeverity.Fatal, assetPath, path,
                    "Scalar value '" + value.ScalarValue + "' is invalid for " + descriptor.RuntimeTypeName + "."));
            if (value.Kind == GdoOverlayValueKind.Set && ContainsStructuralDuplicates(value.Values))
                issues.Add(new ValidationIssue(ValidationSeverity.Fatal, assetPath, path, "Set contains duplicate values."));
            if (value.Kind == GdoOverlayValueKind.LocalizationMap && value.Locales.GroupBy(item => item.Locale).Any(group => group.Count() > 1))
                issues.Add(new ValidationIssue(ValidationSeverity.Fatal, assetPath, path, "Localization map contains duplicate locales."));
            if ((value.Kind == GdoOverlayValueKind.List || value.Kind == GdoOverlayValueKind.Set) && descriptor.Element != null)
                for (int index = 0; index < value.Values.Count; index++) ValidateValue(value.Values[index], descriptor.Element, path + "[" + index + "]", assetPath, issues);
            if (value.Kind == GdoOverlayValueKind.Struct)
            {
                foreach (GdoValueMemberDescriptorData member in descriptor.Members)
                {
                    GdoOverlayMember authored = value.Members.FirstOrDefault(item => item.Name == member.Name);
                    if (authored == null) issues.Add(new ValidationIssue(ValidationSeverity.Fatal, assetPath, path + "." + member.Name, "Required struct member is missing."));
                    else ValidateValue(authored.Value, member.Value, path + "." + member.Name, assetPath, issues);
                }
            }
        }

        public static bool ContainsStructuralDuplicates(IEnumerable<GdoOverlayValue> values) => values
            .Select(ValueKey).GroupBy(key => key, StringComparer.Ordinal).Any(group => group.Count() > 1);

        private static string ValueKey(GdoOverlayValue value)
        {
            if (value == null) return "<null>";
            if (value.Kind == GdoOverlayValueKind.GdoReference)
                return "ref:" + value.ReferenceValue?.ExpectedType + ":" + value.ReferenceValue?.Id;
            if (value.Kind == GdoOverlayValueKind.Struct)
                return "struct:" + string.Join("|", value.Members.OrderBy(item => item.Name).Select(item => item.Name + "=" + ValueKey(item.Value)));
            if (value.Kind == GdoOverlayValueKind.List)
                return "List:" + string.Join("|", value.Values.Select(ValueKey));
            if (value.Kind == GdoOverlayValueKind.Set)
                return "Set:" + string.Join("|", value.Values.Select(ValueKey).OrderBy(item => item, StringComparer.Ordinal));
            if (value.Kind == GdoOverlayValueKind.LocalizationMap)
                return "LocalizationMap:" + string.Join("|", value.Locales.OrderBy(item => item.Locale)
                    .Select(item => item.Locale + "=" + item.DisplayName + "=" + item.Description + "=" + item.FlavourText));
            return value.Kind + ":" + value.ScalarValue;
        }

        private static IEnumerable<GdoFieldDescriptorData> RequiredFields(Type type)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                string runtimeName = current.IsGenericType ? current.GetGenericTypeDefinition().FullName : current.FullName;
                GdoTypeDescriptorView view = GdoDescriptorCatalog.ForRuntimeType(runtimeName);
                if (view == null) continue;
                foreach (GdoFieldDescriptorData field in view.Data.Fields.Where(item => item.IsRequired)) yield return field;
            }
        }
    }
}
