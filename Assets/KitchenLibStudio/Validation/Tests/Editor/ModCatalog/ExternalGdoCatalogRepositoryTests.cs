using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class ExternalGdoCatalogRepositoryTests
    {
        private string _root;
        private string _vanillaPath;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "external-catalog-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            _vanillaPath = Path.Combine(_root, "vanilla.json");
            WriteVanilla();
            WriteImported("workshop_2913877103", "workshop:2913877103", "IngredientLib", 42, "Pork");
        }

        [TearDown] public void TearDown() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

        [Test]
        public void CombinesProvidersAndComposesProviderTypeAndTextFilters()
        {
            var repository = new ExternalGdoCatalogRepository(new VanillaGdoCatalogRepository(_vanillaPath), _root);
            repository.Reload();

            Assert.That(repository.Providers.Select(item => item.Key),
                Is.EqualTo(new[] { "vanilla:plateup", "workshop:2913877103" }));
            Assert.That(repository.Search(string.Empty).Count, Is.EqualTo(2));
            Assert.That(repository.Search("pork", "KitchenData.Item", "KitchenData.Item", "workshop:2913877103").Single().ProviderName,
                Is.EqualTo("IngredientLib"));
            Assert.That(repository.Search("water", null, null, "workshop:2913877103"), Is.Empty);
        }

        [Test]
        public void KeepsSameRuntimeIdDistinctAcrossProvidersAndSkipsMalformedCatalogues()
        {
            WriteImported("same_id", "workshop:other", "Other", 1, "Other Water");
            string malformed = Path.Combine(_root, "broken");
            Directory.CreateDirectory(malformed);
            File.WriteAllText(Path.Combine(malformed, "mod-gdo-catalog.json"), "broken");
            var repository = new ExternalGdoCatalogRepository(new VanillaGdoCatalogRepository(_vanillaPath), _root);

            Assert.That(repository.Reload(), Is.True);
            Assert.That(repository.Find("vanilla:plateup", 1, "KitchenData.Item").DisplayName, Is.EqualTo("Water"));
            Assert.That(repository.Find("workshop:other", 1, "KitchenData.Item").DisplayName, Is.EqualTo("Other Water"));
            Assert.That(repository.Diagnostics.Count, Is.EqualTo(1));
        }

        [Test]
        public void DuplicateProviderQualifiedIdentityIsDeterministicAndDiagnosed()
        {
            WriteImported("duplicate", "workshop:2913877103", "IngredientLib", 42, "Duplicate Pork");
            var repository = new ExternalGdoCatalogRepository(new VanillaGdoCatalogRepository(_vanillaPath), _root);

            Assert.That(repository.Reload(), Is.True);

            ExternalGdoCatalogEntry[] matches = repository.Search(string.Empty, null, null, "workshop:2913877103")
                .Where(item => item.RuntimeId == 42 && item.RuntimeType == "KitchenData.Item").ToArray();
            Assert.That(matches.Length, Is.EqualTo(1));
            Assert.That(matches[0].DisplayName, Is.EqualTo("Duplicate Pork"));
            Assert.That(repository.Diagnostics.Any(value => value.Contains("Duplicate external GDO identity")), Is.True);
        }

        [Test]
        public void RelatedReturnsDirectOutgoingAndIncomingNeighbours()
        {
            string root = Path.Combine(_root, "related");
            Directory.CreateDirectory(root);
            ModGdoCatalogData data = ModGdoRelationshipValueTests.CreateCatalog();
            data.entries = new[]
            {
                data.entries[0],
                new ModGdoEntryData { providerKey = "workshop:2913877103", runtimeId = 43,
                    displayName = "Chopped Pork", runtimeType = "KitchenData.Item", sourceAssembly = "IngredientLib" }
            };
            data.relationships = new[]
            {
                new ExternalGdoRelationshipData
                {
                    source = new ExternalGdoIdentityData { providerKey = "workshop:2913877103", runtimeId = 42, runtimeType = "KitchenData.Item" },
                    target = new ExternalGdoIdentityData { providerKey = "workshop:2913877103", runtimeId = 43, runtimeType = "KitchenData.Item" },
                    fieldPath = "Processes[0].Result", label = "Result"
                }
            };
            File.WriteAllBytes(Path.Combine(root, "mod-gdo-catalog.json"), ModGdoCatalogFile.Serialize(data));
            var repository = new ExternalGdoCatalogRepository(new VanillaGdoCatalogRepository(_vanillaPath), _root);
            repository.Reload();

            ExternalGdoRelatedEntry outgoing = repository.Related(
                "workshop:2913877103", 42, "KitchenData.Item").Single();
            Assert.That(outgoing.Direction, Is.EqualTo(ExternalGdoRelationshipDirection.Outgoing));
            Assert.That(outgoing.Label, Is.EqualTo("Result"));
            Assert.That(outgoing.Entry.DisplayName, Is.EqualTo("Chopped Pork"));

            ExternalGdoRelatedEntry incoming = repository.Related(
                "workshop:2913877103", 43, "KitchenData.Item").Single();
            Assert.That(incoming.Direction, Is.EqualTo(ExternalGdoRelationshipDirection.Incoming));
            Assert.That(incoming.Entry.DisplayName, Is.EqualTo("Pork"));
        }

        [Test]
        public void RelatedIncludesVanillaCatalogueRelationships()
        {
            VanillaGdoCatalogData data = VanillaGdoCatalogFile.Read(_vanillaPath);
            data.entries = new[]
            {
                new VanillaGdoCatalogEntryData { id = 10, name = "Dough", runtimeType = "KitchenData.ItemGroup", assemblyName = "Kitchen.GameData", locations = new VanillaGdoCatalogLocationData[0] },
                new VanillaGdoCatalogEntryData { id = 20, name = "Knead", runtimeType = "KitchenData.Process", assemblyName = "Kitchen.GameData", locations = new VanillaGdoCatalogLocationData[0] }
            };
            data.relationships = new[]
            {
                new ExternalGdoRelationshipData
                {
                    source = new ExternalGdoIdentityData { providerKey = "vanilla:plateup", runtimeId = 10, runtimeType = "KitchenData.ItemGroup" },
                    target = new ExternalGdoIdentityData { providerKey = "vanilla:plateup", runtimeId = 20, runtimeType = "KitchenData.Process" },
                    fieldPath = "Processes[0].Process", label = "Process"
                }
            };
            File.WriteAllBytes(_vanillaPath, VanillaGdoCatalogFile.Serialize(data));
            var repository = new ExternalGdoCatalogRepository(new VanillaGdoCatalogRepository(_vanillaPath), _root);

            repository.Reload();

            Assert.That(repository.Related("vanilla:plateup", 10, "KitchenData.ItemGroup").Single().Entry.DisplayName,
                Is.EqualTo("Knead"));
            Assert.That(repository.Related("vanilla:plateup", 20, "KitchenData.Process").Single().Direction,
                Is.EqualTo(ExternalGdoRelationshipDirection.Incoming));
        }

        [Test]
        public void ReloadsCombinedIndexWhenVanillaRepositoryChanges()
        {
            var vanilla = new VanillaGdoCatalogRepository(_vanillaPath);
            var repository = new ExternalGdoCatalogRepository(vanilla, _root);
            repository.Reload();
            Assert.That(repository.Related("vanilla:plateup", 1, "KitchenData.Item"), Is.Empty);

            VanillaGdoCatalogData data = VanillaGdoCatalogFile.Read(_vanillaPath);
            data.entries = new[]
            {
                data.entries[0],
                new VanillaGdoCatalogEntryData { id = 20, name = "Knead", runtimeType = "KitchenData.Process", assemblyName = "Kitchen.GameData", locations = new VanillaGdoCatalogLocationData[0] }
            };
            data.relationships = new[]
            {
                new ExternalGdoRelationshipData
                {
                    source = new ExternalGdoIdentityData { providerKey = "vanilla:plateup", runtimeId = 1, runtimeType = "KitchenData.Item" },
                    target = new ExternalGdoIdentityData { providerKey = "vanilla:plateup", runtimeId = 20, runtimeType = "KitchenData.Process" },
                    fieldPath = "Process", label = "Process"
                }
            };
            File.WriteAllBytes(_vanillaPath, VanillaGdoCatalogFile.Serialize(data));

            vanilla.Reload();

            Assert.That(repository.Related("vanilla:plateup", 1, "KitchenData.Item").Single().Entry.DisplayName,
                Is.EqualTo("Knead"));
        }

        private void WriteVanilla()
        {
            var data = new VanillaGdoCatalogData
            {
                schemaVersion = VanillaGdoCatalogFile.SchemaVersion, providerId = "plateup", providerName = "PlateUp", steamAppId = 1599600,
                importerVersion = "1", unityVersion = "2020.3.48f1", sourceAssemblyName = "Kitchen.GameData",
                sourceAssemblySha256 = new string('A', 64), schemaFingerprint = GdoDescriptorCatalog.GlobalFingerprint,
                assets = new VanillaGdoCatalogAssetData[0], relationships = new ExternalGdoRelationshipData[0], diagnostics = new VanillaGdoCatalogDiagnosticData[0],
                entries = new[] { new VanillaGdoCatalogEntryData { id = 1, name = "Water", runtimeType = "KitchenData.Item", assemblyName = "Kitchen.GameData", locations = new VanillaGdoCatalogLocationData[0] } }
            };
            File.WriteAllBytes(_vanillaPath, VanillaGdoCatalogFile.Serialize(data));
        }

        private void WriteImported(string folder, string providerKey, string providerName, int id, string name)
        {
            string root = Path.Combine(_root, folder);
            Directory.CreateDirectory(root);
            ModGdoCatalogData data = ModGdoRelationshipValueTests.CreateCatalog();
            data.providers[0].key = providerKey; data.providers[0].displayName = providerName;
            data.entries[0].providerKey = providerKey; data.entries[0].runtimeId = id; data.entries[0].displayName = name;
            File.WriteAllBytes(Path.Combine(root, "mod-gdo-catalog.json"), ModGdoCatalogFile.Serialize(data));
        }
    }
}
