using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class ModGdoCatalogRepositoryTests
    {
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _path = Path.Combine(Path.GetTempPath(), "mod-gdo-catalog-" + Guid.NewGuid().ToString("N") + ".json");
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_path)) File.Delete(_path);
        }

        [Test]
        public void RoundTripPreservesProviderScopedEntriesAndMissingPreviews()
        {
            ModGdoCatalogData expected = Sample();
            File.WriteAllBytes(_path, ModGdoCatalogFile.Serialize(expected));

            ModGdoCatalogData actual = ModGdoCatalogFile.Read(_path);
            var repository = new ModGdoCatalogRepository(_path, "capture-1", "build-1");

            Assert.That(actual.providers.Single().workshopId, Is.EqualTo("2913877103"));
            Assert.That(repository.Reload(), Is.True);
            ModGdoCatalogEntry entry = repository.Find("workshop:2913877103", 42, "KitchenData.Item");
            Assert.That(entry.DisplayName, Is.EqualTo("Pork"));
            Assert.That(entry.Provider.DisplayName, Is.EqualTo("IngredientLib"));
            Assert.That(entry.PreviewState, Is.EqualTo(ModGdoPreviewState.Missing));
            Assert.That(repository.Search("ingredient", "KitchenData.Item").Single().RuntimeId, Is.EqualTo(42));
            Assert.That(actual.entries.Single().references.Single().path, Is.EqualTo("DerivedProcesses[0].Result"));
            Assert.That(actual.relationships.Single().target.providerKey, Is.EqualTo("plateup"));
        }

        [Test]
        public void ValidationRejectsAmbiguousOrDuplicateRelationshipRows()
        {
            ModGdoCatalogData data = Sample();
            data.relationships[0].target.providerKey = null;
            Assert.Throws<InvalidDataException>(() => ModGdoCatalogFile.Serialize(data));

            data = Sample();
            data.relationships = new[] { data.relationships[0], data.relationships[0] };
            Assert.Throws<InvalidDataException>(() => ModGdoCatalogFile.Serialize(data));
        }

        [Test]
        public void SerializeIsDeterministicUtf8WithoutBomAndEndsWithNewline()
        {
            byte[] first = ModGdoCatalogFile.Serialize(Sample());
            byte[] second = ModGdoCatalogFile.Serialize(Sample());
            string json = Encoding.UTF8.GetString(first);

            Assert.That(second, Is.EqualTo(first));
            Assert.That(first.Take(3).ToArray(), Is.Not.EqualTo(Encoding.UTF8.GetPreamble()));
            Assert.That(json.EndsWith("\n", StringComparison.Ordinal), Is.True);
        }

        [Test]
        public void AtomicPublishPreservesPreviousCatalogWhenReplacementFails()
        {
            byte[] previous = ModGdoCatalogFile.Serialize(Sample());
            File.WriteAllBytes(_path, previous);

            Assert.Throws<IOException>(() => ModGdoCatalogFile.PublishAtomic(_path,
                ModGdoCatalogFile.Serialize(Sample("capture-2")),
                (source, destination) => throw new IOException("injected")));

            Assert.That(File.ReadAllBytes(_path), Is.EqualTo(previous));
            Assert.That(Directory.GetFiles(Path.GetDirectoryName(_path), Path.GetFileName(_path) + ".*.tmp"), Is.Empty);
        }

        [Test]
        public void RepositoryRejectsWrongSchemaStaleCaptureAndStaleBuild()
        {
            ModGdoCatalogData data = Sample();
            data.schemaVersion = 99;
            Assert.Throws<InvalidDataException>(() => ModGdoCatalogFile.Serialize(data));

            File.WriteAllBytes(_path, ModGdoCatalogFile.Serialize(Sample()));
            Assert.That(new ModGdoCatalogRepository(_path, "wrong", "build-1").Reload(), Is.False);
            Assert.That(new ModGdoCatalogRepository(_path, "capture-1", "wrong").Reload(), Is.False);
        }

        [Test]
        public void RepositoryRejectsDuplicateRuntimeIdsAcrossProviders()
        {
            ModGdoCatalogData data = Sample();
            data.providers = new[]
            {
                data.providers[0],
                new ModGdoProviderData { key = "assembly:Other", displayName = "Other", sourceAssembly = "Other" }
            };
            data.entries = new[]
            {
                data.entries[0],
                new ModGdoEntryData { providerKey = "assembly:Other", runtimeId = 42, displayName = "Conflict",
                    runtimeType = "KitchenData.Item", sourceAssembly = "Other", previewState = "missing" }
            };
            File.WriteAllBytes(_path, ModGdoCatalogFile.Serialize(data));

            var repository = new ModGdoCatalogRepository(_path, "capture-1", "build-1");

            Assert.That(repository.Reload(), Is.False);
            Assert.That(repository.Status, Is.EqualTo(ModGdoCatalogState.DuplicateRuntimeId));
        }

        [Test]
        public void MissingProviderIdentityIsRejectedAsAmbiguous()
        {
            ModGdoCatalogData data = Sample();
            data.providers = new ModGdoProviderData[0];
            data.entries[0].providerKey = null;

            Assert.Throws<InvalidDataException>(() => ModGdoCatalogFile.Serialize(data));
        }

        private static ModGdoCatalogData Sample(string captureToken = "capture-1")
        {
            return new ModGdoCatalogData
            {
                schemaVersion = ModGdoCatalogFile.SchemaVersion,
                captureToken = captureToken,
                steamAppId = 1599600,
                steamBuildId = "build-1",
                bridgeVersion = "1",
                providers = new[]
                {
                    new ModGdoProviderData
                    {
                        key = "workshop:2913877103", displayName = "IngredientLib", version = "1.0",
                        workshopId = "2913877103", sourceAssembly = "IngredientLib", provenance = "kitchenlib"
                    }
                },
                entries = new[]
                {
                    new ModGdoEntryData
                    {
                        providerKey = "workshop:2913877103", runtimeId = 42, displayName = "Pork",
                        runtimeType = "KitchenData.Item", sourceAssembly = "IngredientLib",
                        customRegistrationType = "IngredientLib.Ingredient.Items.Pork", hasPrefab = true,
                        previewState = "missing",
                        references = new[] { new ModGdoReferenceData { path = "DerivedProcesses[0].Result",
                            providerKey = "plateup", runtimeId = 43, runtimeType = "KitchenData.Item" } }
                    }
                },
                relationships = new[]
                {
                    new ExternalGdoRelationshipData
                    {
                        source = new ExternalGdoIdentityData { providerKey = "workshop:2913877103", runtimeId = 42, runtimeType = "KitchenData.Item" },
                        target = new ExternalGdoIdentityData { providerKey = "plateup", runtimeId = 43, runtimeType = "KitchenData.Item" },
                        fieldPath = "DerivedProcesses[0].Result", label = "Result"
                    }
                },
                diagnostics = new ModGdoCatalogDiagnosticData[0]
            };
        }
    }
}
