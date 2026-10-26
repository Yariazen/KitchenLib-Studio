using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class VanillaGdoCatalogBuilderTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "KitchenLibStudio-CatalogBuilder-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(_root, "PlateUp_Data", "Managed"));
            File.WriteAllBytes(Path.Combine(_root, "PlateUp_Data", "Managed", "Kitchen.GameData.dll"), new byte[] { 1, 2, 3 });
            File.WriteAllBytes(Path.Combine(_root, "PlateUp_Data", "sharedassets1.assets"), new byte[] { 4, 5, 6 });
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test]
        public void BuildProducesSortedPortableSchemaV1CatalogAndCollapsesEquivalentDuplicates()
        {
            var installation = new PlateUpInstallation(_root, null, null, null, null, "12345");
            var scan = new VanillaGdoScanResult(new[]
            {
                new VanillaGdoScanEntry(20, "Zed", "KitchenData.Item", "Kitchen.GameData", "sharedassets1.assets", 9),
                new VanillaGdoScanEntry(10, "Water", "KitchenData.Item", "Kitchen.GameData", "sharedassets1.assets", 73340),
                new VanillaGdoScanEntry(10, "Water", "KitchenData.Item", "Kitchen.GameData", "sharedassets1.assets", 7)
            }, new[]
            {
                new VanillaGdoScanDiagnostic(VanillaGdoDiagnosticSeverity.Warning, "z", "later", "sharedassets1.assets", 9),
                new VanillaGdoScanDiagnostic(VanillaGdoDiagnosticSeverity.Warning, "a", "first", "sharedassets1.assets", 7)
            });
            PlateUpSchemaFingerprint schema = PlateUpSchemaFingerprint.FromInstalledAssembly(_root, "2020.3.48f1", "schema-fingerprint");

            VanillaGdoCatalogData result = VanillaGdoCatalogBuilder.Build(installation, scan, schema);

            Assert.That(result.schemaVersion, Is.EqualTo(VanillaGdoCatalogFile.SchemaVersion));
            Assert.That(result.providerId, Is.EqualTo("plateup"));
            Assert.That(result.providerName, Is.EqualTo("PlateUp"));
            Assert.That(result.steamAppId, Is.EqualTo(1599600));
            Assert.That(result.steamBuildId, Is.EqualTo("12345"));
            Assert.That(result.unityVersion, Is.EqualTo("2020.3.48f1"));
            Assert.That(result.entries.Select(item => item.id), Is.EqualTo(new[] { 10, 20 }));
            Assert.That(result.entries[0].locations.Select(item => item.pathId), Is.EqualTo(new long[] { 7, 73340 }));
            Assert.That(result.assets.Single().relativePath, Is.EqualTo("sharedassets1.assets"));
            Assert.That(result.diagnostics.Select(item => item.code), Is.EqualTo(new[] { "a", "z" }));
        }

        [Test]
        public void BuildRejectsConflictingDuplicateIdsAndFatalScanDiagnostics()
        {
            PlateUpInstallation installation = new PlateUpInstallation(_root, null, null, null, null, null);
            PlateUpSchemaFingerprint schema = PlateUpSchemaFingerprint.FromInstalledAssembly(_root, "2020.3.48f1", "schema");
            var conflict = new VanillaGdoScanResult(new[]
            {
                new VanillaGdoScanEntry(10, "Water", "KitchenData.Item", "Kitchen.GameData", "sharedassets1.assets", 1),
                new VanillaGdoScanEntry(10, "Not Water", "KitchenData.Item", "Kitchen.GameData", "sharedassets1.assets", 2)
            }, null);
            var fatal = new VanillaGdoScanResult(null, new[]
            {
                new VanillaGdoScanDiagnostic(VanillaGdoDiagnosticSeverity.Fatal, "broken", "broken")
            });

            Assert.Throws<InvalidDataException>(() => VanillaGdoCatalogBuilder.Build(installation, conflict, schema));
            InvalidDataException fatalError = Assert.Throws<InvalidDataException>(() => VanillaGdoCatalogBuilder.Build(installation, fatal, schema));
            Assert.That(fatalError.Message, Does.Contain("broken"));
            Assert.Throws<InvalidDataException>(() => VanillaGdoCatalogBuilder.Build(
                installation, new VanillaGdoScanResult(null, null), schema));
        }

        [Test]
        public void BuildPublishesProviderQualifiedRelationshipsInStableOrder()
        {
            var installation = new PlateUpInstallation(_root, null, null, null, null, "12345");
            var scan = new VanillaGdoScanResult(new[]
            {
                new VanillaGdoScanEntry(10, "Dough", "KitchenData.ItemGroup", "Kitchen.GameData", "sharedassets1.assets", 1),
                new VanillaGdoScanEntry(20, "Knead", "KitchenData.Process", "Kitchen.GameData", "sharedassets1.assets", 2),
                new VanillaGdoScanEntry(30, "Bread", "KitchenData.Item", "Kitchen.GameData", "sharedassets1.assets", 3)
            }, null, new[]
            {
                new VanillaGdoScanRelationship(10, "KitchenData.ItemGroup", 30, "KitchenData.Item", "Processes[1].Result", "Result"),
                new VanillaGdoScanRelationship(10, "KitchenData.ItemGroup", 20, "KitchenData.Process", "Processes[0].Process", "Process"),
                new VanillaGdoScanRelationship(10, "KitchenData.ItemGroup", 20, "KitchenData.Process", "Processes[0].Process", "Process")
            });
            PlateUpSchemaFingerprint schema = PlateUpSchemaFingerprint.FromInstalledAssembly(
                _root, "2020.3.48f1", "schema-fingerprint");

            VanillaGdoCatalogData result = VanillaGdoCatalogBuilder.Build(installation, scan, schema);

            Assert.That(result.relationships.Select(value => value.fieldPath),
                Is.EqualTo(new[] { "Processes[0].Process", "Processes[1].Result" }));
            Assert.That(result.relationships.All(value => value.source.providerKey == "vanilla:plateup" &&
                value.target.providerKey == "vanilla:plateup"), Is.True);
            Assert.That(result.relationships[0].source.runtimeId, Is.EqualTo(10));
            Assert.That(result.relationships[0].target.runtimeId, Is.EqualTo(20));
            Assert.That(result.relationships[0].label, Is.EqualTo("Process"));
        }
    }
}
