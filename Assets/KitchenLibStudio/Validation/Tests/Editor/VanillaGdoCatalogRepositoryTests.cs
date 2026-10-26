using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class VanillaGdoCatalogRepositoryTests
    {
        private string _path;
        [SetUp] public void SetUp() { _path = Path.Combine(Path.GetTempPath(), "vanilla-catalog-" + Guid.NewGuid().ToString("N") + ".json"); }
        [TearDown] public void TearDown() { if (File.Exists(_path)) File.Delete(_path); }

        [Test]
        public void ReloadBuildsIdAndSearchIndexesAndRaisesChangedOnce()
        {
            var data = new VanillaGdoCatalogData
            {
                schemaVersion = VanillaGdoCatalogFile.SchemaVersion, providerId = "plateup", providerName = "PlateUp", steamAppId = 1599600,
                importerVersion = "1", unityVersion = "2020.3.48f1", sourceAssemblyName = "Kitchen.GameData", sourceAssemblySha256 = new string('A', 64), schemaFingerprint = GdoDescriptorCatalog.GlobalFingerprint, steamBuildId = "old-build",
                assets = new VanillaGdoCatalogAssetData[0], relationships = new ExternalGdoRelationshipData[0], diagnostics = new VanillaGdoCatalogDiagnosticData[0],
                entries = new[] { new VanillaGdoCatalogEntryData { id = 1657174953, name = "Water", runtimeType = "KitchenData.Item", assemblyName = "Kitchen.GameData", locations = new VanillaGdoCatalogLocationData[0] } }
            };
            File.WriteAllBytes(_path, VanillaGdoCatalogFile.Serialize(data));
            var repository = new VanillaGdoCatalogRepository(_path);
            int changed = 0; repository.Changed += () => changed++;

            Assert.That(repository.Reload(), Is.True);
            Assert.That(repository.Status.State, Is.EqualTo(VanillaGdoCatalogState.Ready));
            Assert.That(repository.FindById(1657174953).Name, Is.EqualTo("Water"));
            Assert.That(repository.Search("1657174953").Count, Is.EqualTo(1));
            Assert.That(repository.Search("item", "KitchenData.Item").Count, Is.EqualTo(1));
            Assert.That(changed, Is.EqualTo(1));

            repository.AssessInstallation(new PlateUpInstallation("root", null, null, null, null, "new-build"), true);
            Assert.That(repository.Status.State, Is.EqualTo(VanillaGdoCatalogState.StaleBuild));
            repository.AssessInstallation(null, true);
            Assert.That(repository.Status.State, Is.EqualTo(VanillaGdoCatalogState.SavedInstallUnavailable));
            repository.AssessInstallation(null, false);
            Assert.That(repository.Status.State, Is.EqualTo(VanillaGdoCatalogState.Ready));
        }

        [Test]
        public void MissingAndMalformedCatalogsHaveBoundedStates()
        {
            var repository = new VanillaGdoCatalogRepository(_path);
            Assert.That(repository.Reload(), Is.False);
            Assert.That(repository.Status.State, Is.EqualTo(VanillaGdoCatalogState.Missing));
            File.WriteAllText(_path, "broken");
            Assert.That(repository.Reload(), Is.False);
            Assert.That(repository.Status.State, Is.EqualTo(VanillaGdoCatalogState.Malformed));
        }

        [Test]
        public void BrowsingRefreshesCatalogWhenFileIsReplacedOrDeleted()
        {
            WriteCatalog(1, "First");
            var repository = new VanillaGdoCatalogRepository(_path);
            int changed = 0;
            repository.Changed += () => changed++;
            Assert.That(repository.Reload(), Is.True);

            WriteCatalog(2, "Second");
            File.SetLastWriteTimeUtc(_path, DateTime.UtcNow.AddSeconds(2));

            Assert.That(repository.FindById(1), Is.Null);
            Assert.That(repository.FindById(2).Name, Is.EqualTo("Second"));
            Assert.That(changed, Is.EqualTo(2));

            File.Delete(_path);
            Assert.That(repository.Search(string.Empty), Is.Empty);
            Assert.That(repository.Status.State, Is.EqualTo(VanillaGdoCatalogState.Missing));
            Assert.That(changed, Is.EqualTo(3));
        }

        [Test]
        public void ThrowingChangedSubscriberDoesNotReclassifySuccessfulReload()
        {
            var data = new VanillaGdoCatalogData
            {
                schemaVersion = VanillaGdoCatalogFile.SchemaVersion, providerId = "plateup", providerName = "PlateUp", steamAppId = 1599600,
                importerVersion = "1", unityVersion = "2020.3.48f1", sourceAssemblyName = "Kitchen.GameData",
                sourceAssemblySha256 = new string('A', 64), schemaFingerprint = GdoDescriptorCatalog.GlobalFingerprint,
                assets = new VanillaGdoCatalogAssetData[0], relationships = new ExternalGdoRelationshipData[0], diagnostics = new VanillaGdoCatalogDiagnosticData[0],
                entries = new[] { new VanillaGdoCatalogEntryData { id = 1657174953, name = "Water", runtimeType = "KitchenData.Item", assemblyName = "Kitchen.GameData", locations = new VanillaGdoCatalogLocationData[0] } }
            };
            File.WriteAllBytes(_path, VanillaGdoCatalogFile.Serialize(data));
            var repository = new VanillaGdoCatalogRepository(_path);
            int changed = 0;
            repository.Changed += () => { changed++; throw new ArgumentException("observer failure"); };

            Assert.Throws<ArgumentException>(() => repository.Reload());

            Assert.That(changed, Is.EqualTo(1));
            Assert.That(repository.Status.State, Is.EqualTo(VanillaGdoCatalogState.Ready));
            Assert.That(repository.FindById(1657174953).Name, Is.EqualTo("Water"));
        }

        [Test]
        public void DescriptorAssignabilityHandlesKnownInheritanceAndUnknownBases()
        {
            Assert.That(GdoDescriptorCatalog.IsAssignable("KitchenData.Item", "KitchenData.Item"), Is.True);
            Assert.That(GdoDescriptorCatalog.IsAssignable("Missing.Type", "KitchenData.Item"), Is.False);
        }

        [Test]
        public void SearchCombinesExpectedTypeExactRuntimeTypeAndText()
        {
            WriteCatalog(new[]
            {
                Entry(1, "Apple", "KitchenData.Item"),
                Entry(2, "Apple Provider", "KitchenData.Appliance"),
                Entry(3, "Burger", "KitchenData.Dish")
            });
            var repository = new VanillaGdoCatalogRepository(_path);
            Assert.That(repository.Reload(), Is.True);

            Assert.That(repository.Search("apple", null, "KitchenData.Item").Select(entry => entry.Id),
                Is.EqualTo(new[] { 1 }));
            Assert.That(repository.Search(string.Empty, "KitchenData.Item", "KitchenData.Appliance"), Is.Empty);
        }

        [Test]
        public void RuntimeTypesAreDistinctSortedAndBoundedByExpectedType()
        {
            WriteCatalog(new[]
            {
                Entry(1, "Sink", "KitchenData.Appliance"),
                Entry(2, "Water", "KitchenData.Item"),
                Entry(3, "Flour", "KitchenData.Item"),
                Entry(4, "Burger", "KitchenData.Dish")
            });
            var repository = new VanillaGdoCatalogRepository(_path);
            Assert.That(repository.Reload(), Is.True);

            Assert.That(repository.RuntimeTypes(), Is.EqualTo(new[]
                { "KitchenData.Appliance", "KitchenData.Dish", "KitchenData.Item" }));
            Assert.That(repository.RuntimeTypes("KitchenData.Item"), Is.EqualTo(new[] { "KitchenData.Item" }));
        }

        [Test]
        public void DescriptorAndInstalledBuildCompatibilityHaveBoundedStates()
        {
            var incompatible = new VanillaGdoCatalogData
            {
                schemaVersion = VanillaGdoCatalogFile.SchemaVersion, providerId = "plateup", providerName = "PlateUp", steamAppId = 1599600,
                importerVersion = "1", unityVersion = "2020.3.48f1", sourceAssemblyName = "Kitchen.GameData",
                sourceAssemblySha256 = new string('A', 64), schemaFingerprint = "wrong",
                assets = new VanillaGdoCatalogAssetData[0], relationships = new ExternalGdoRelationshipData[0], diagnostics = new VanillaGdoCatalogDiagnosticData[0],
                entries = new VanillaGdoCatalogEntryData[0]
            };
            File.WriteAllBytes(_path, VanillaGdoCatalogFile.Serialize(incompatible));
            var repository = new VanillaGdoCatalogRepository(_path);
            Assert.That(repository.Reload(), Is.False);
            Assert.That(repository.Status.State, Is.EqualTo(VanillaGdoCatalogState.DescriptorMismatch));
        }


        private void WriteCatalog(int id, string name)
            => WriteCatalog(new[] { Entry(id, name, "KitchenData.Item") });

        private void WriteCatalog(VanillaGdoCatalogEntryData[] entries)
        {
            var data = new VanillaGdoCatalogData
            {
                schemaVersion = VanillaGdoCatalogFile.SchemaVersion, providerId = "plateup", providerName = "PlateUp", steamAppId = 1599600,
                importerVersion = "1", unityVersion = "2020.3.48f1", sourceAssemblyName = "Kitchen.GameData",
                sourceAssemblySha256 = new string('A', 64), schemaFingerprint = GdoDescriptorCatalog.GlobalFingerprint,
                assets = new VanillaGdoCatalogAssetData[0], relationships = new ExternalGdoRelationshipData[0], diagnostics = new VanillaGdoCatalogDiagnosticData[0],
                entries = entries
            };
            File.WriteAllBytes(_path, VanillaGdoCatalogFile.Serialize(data));
        }

        private static VanillaGdoCatalogEntryData Entry(int id, string name, string runtimeType) =>
            new VanillaGdoCatalogEntryData { id = id, name = name, runtimeType = runtimeType,
                assemblyName = "Kitchen.GameData", locations = new VanillaGdoCatalogLocationData[0] };
    }
}
