using NUnit.Framework;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class PlateUpCatalogImportMenuTests
    {
        [Test]
        public void SettingsSaveOnlyValidatedRootsAndClearOnlyLocalPreference()
        {
            var store = new MemoryStore();
            var settings = new PlateUpCatalogInstallationSettings(store, path =>
                path == "valid" ? new PlateUpInstallationValidation(new PlateUpInstallation(path, null, null, null, null, null), new string[0]) :
                                  new PlateUpInstallationValidation(null, new[] { "invalid" }));

            Assert.That(settings.TrySave("invalid"), Is.False);
            Assert.That(store.Value, Is.Null);
            Assert.That(settings.TrySave("valid"), Is.True);
            Assert.That(settings.SavedRoot, Is.EqualTo("valid"));
            settings.Clear();
            Assert.That(settings.SavedRoot, Is.Null);
        }

        private sealed class MemoryStore : IPlateUpCatalogPreferenceStore
        {
            public string Value;
            public string Get() => Value;
            public void Set(string value) => Value = value;
            public void Delete() => Value = null;
        }
    }
}
