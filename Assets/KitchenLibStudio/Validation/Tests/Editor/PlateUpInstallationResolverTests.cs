using System;
using NUnit.Framework;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class PlateUpInstallationResolverTests
    {
        [Test]
        public void ValidSavedRootWinsWithoutDiscoveryOrPicker()
        {
            var store = new Store { Value = "C:\\Saved\\PlateUp" }; int discovered = 0; int picked = 0;
            var resolver = Resolver(store, path => Valid(path), () => { discovered++; return EmptyDiscovery(); }, () => { picked++; return null; });

            PlateUpInstallationResolution result = resolver.Resolve(true);

            Assert.That(result.Installation.GameRoot, Is.EqualTo("C:\\Saved\\PlateUp"));
            Assert.That(result.Source, Is.EqualTo(PlateUpInstallationSource.Saved));
            Assert.That(discovered, Is.Zero); Assert.That(picked, Is.Zero);
        }

        [Test]
        public void StaleSavedRootFallsBackToStandardRootAndPersistsIt()
        {
            var store = new Store { Value = "C:\\Gone" }; int picked = 0;
            var resolver = Resolver(store, path => path == PlateUpInstallationResolver.StandardWindowsRoot ? Valid(path) : Invalid("stale"),
                EmptyDiscovery, () => { picked++; return null; });

            PlateUpInstallationResolution result = resolver.Resolve(true);

            Assert.That(result.Installation.GameRoot, Is.EqualTo(PlateUpInstallationResolver.StandardWindowsRoot));
            Assert.That(result.Source, Is.EqualTo(PlateUpInstallationSource.Standard));
            Assert.That(store.Value, Is.EqualTo(PlateUpInstallationResolver.StandardWindowsRoot));
            Assert.That(picked, Is.Zero);
        }

        [Test]
        public void SingleDiscoveredInstallIsPersistedAfterSavedAndStandardFail()
        {
            var store = new Store(); var found = new PlateUpInstallation("D:\\PlateUp", null, null, null, null, "1");
            var resolver = Resolver(store, _ => Invalid("missing"),
                () => new PlateUpDiscoveryResult(new[] { found }, new string[0]), () => null);

            PlateUpInstallationResolution result = resolver.Resolve(false);

            Assert.That(result.Source, Is.EqualTo(PlateUpInstallationSource.Discovered));
            Assert.That(store.Value, Is.EqualTo("D:\\PlateUp"));
        }

        [Test]
        public void PickerRunsOnlyWhenAllowedAndAutomaticResolutionFails()
        {
            var store = new Store(); int picked = 0;
            var resolver = Resolver(store, path => path == "E:\\Chosen" ? Valid(path) : Invalid("missing"),
                EmptyDiscovery, () => { picked++; return "E:\\Chosen"; });

            Assert.That(resolver.Resolve(false).Installation, Is.Null); Assert.That(picked, Is.Zero);
            PlateUpInstallationResolution result = resolver.Resolve(true);
            Assert.That(result.Source, Is.EqualTo(PlateUpInstallationSource.Picker)); Assert.That(picked, Is.EqualTo(1));
            Assert.That(store.Value, Is.EqualTo("E:\\Chosen"));
        }

        private static PlateUpInstallationResolver Resolver(Store store,
            Func<string, PlateUpInstallationValidation> validate, Func<PlateUpDiscoveryResult> discover, Func<string> picker) =>
            new PlateUpInstallationResolver(store, validate, discover, PlateUpInstallationResolver.StandardWindowsRoot, picker);
        private static PlateUpInstallationValidation Valid(string path) => new PlateUpInstallationValidation(new PlateUpInstallation(path, null, null, null, null, null), new string[0]);
        private static PlateUpInstallationValidation Invalid(string message) => new PlateUpInstallationValidation(null, new[] { message });
        private static PlateUpDiscoveryResult EmptyDiscovery() => new PlateUpDiscoveryResult(new PlateUpInstallation[0], new string[0]);
        private sealed class Store : IPlateUpCatalogPreferenceStore
        { public string Value; public string Get() => Value; public void Set(string value) => Value = value; public void Delete() => Value = null; }
    }
}
