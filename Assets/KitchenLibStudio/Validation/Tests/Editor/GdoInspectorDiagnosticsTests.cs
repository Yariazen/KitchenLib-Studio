using System.Linq;
using NUnit.Framework;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class GdoInspectorDiagnosticsTests
    {
        [Test]
        public void MapUsesLongestOwningFieldPathForNestedIssue()
        {
            GdoInspectorModel model = GdoInspectorModel.Build(GdoDescriptorCatalog.ForRuntimeType("KitchenData.Item"));
            var issue = new ValidationIssue(ValidationSeverity.Fatal, "Assets/Item.asset",
                "KitchenData.Item.Processes[0].Result", "Reference is missing.");

            GdoInspectorDiagnosticMap map = GdoInspectorDiagnostics.Map(new[] { issue }, model);

            Assert.That(map.ForField("KitchenData.Item.Processes"), Is.EqualTo(new[] { issue }));
            Assert.That(map.Unmapped, Is.Empty);
            Assert.That(map.HighestSeverity, Is.EqualTo(ValidationSeverity.Fatal));
        }

        [Test]
        public void MapMatchesShortNativePropertyNameAndKeepsUnknownIssueInSummary()
        {
            GdoInspectorModel model = GdoInspectorModel.Build(GdoDescriptorCatalog.ForRuntimeType("KitchenData.Dish"));
            var id = new ValidationIssue(ValidationSeverity.Error, "Assets/Dish.asset", "ID", "GDO ID must be non-zero.");
            var drift = new ValidationIssue(ValidationSeverity.Fatal, "Assets/Dish.asset", "SchemaFingerprint", "Generated schema fingerprint does not match.");

            GdoInspectorDiagnosticMap map = GdoInspectorDiagnostics.Map(new[] { id, drift }, model);

            Assert.That(map.ForField("KitchenData.GameDataObject.ID").Single(), Is.SameAs(id));
            Assert.That(map.Unmapped.Single(), Is.SameAs(drift));
            Assert.That(map.Count, Is.EqualTo(2));
        }

        [Test]
        public void MapOrdersIssuesBySeverityThenPath()
        {
            GdoInspectorModel model = GdoInspectorModel.Build(GdoDescriptorCatalog.ForRuntimeType("KitchenData.Dish"));
            ValidationIssue warning = new ValidationIssue(ValidationSeverity.Warning, string.Empty, "ID", "Warning");
            ValidationIssue fatal = new ValidationIssue(ValidationSeverity.Fatal, string.Empty, "ID", "Fatal");

            GdoInspectorDiagnosticMap map = GdoInspectorDiagnostics.Map(new[] { warning, fatal }, model);

            Assert.That(map.ForField("KitchenData.GameDataObject.ID").Select(issue => issue.Severity),
                Is.EqualTo(new[] { ValidationSeverity.Fatal, ValidationSeverity.Warning }));
        }
    }
}
