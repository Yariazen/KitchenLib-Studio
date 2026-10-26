using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class VanillaAssetCatalogRepositoryTests
    {
        private string _path;
        [SetUp] public void SetUp() { _path = Path.Combine(Path.GetTempPath(), "asset-repository-" + Guid.NewGuid().ToString("N") + ".json"); }
        [TearDown] public void TearDown() { if (File.Exists(_path)) File.Delete(_path); }

        [Test]
        public void SearchHidesStructuralRecordsAndIndexesBothDependencyDirections()
        {
            VanillaAssetCatalogData data = VanillaAssetCatalogTests.FixtureForRepository();
            File.WriteAllBytes(_path, VanillaAssetCatalogFile.Serialize(data));
            var repository = new VanillaAssetCatalogRepository(_path);
            Assert.That(repository.Reload(), Is.True);

            Assert.That(repository.Search("water").Select(record => record.Key),
                Is.EqualTo(new[] { "plateup/game-object/water" }));
            Assert.That(repository.Search("transform", null, true).Single().Structural, Is.True);
            Assert.That(repository.Dependencies("plateup/game-object/water").Single().Key,
                Is.EqualTo("plateup/transform/water"));
            Assert.That(repository.Dependents("plateup/transform/water").Single().Key,
                Is.EqualTo("plateup/game-object/water"));
        }
    }
}
