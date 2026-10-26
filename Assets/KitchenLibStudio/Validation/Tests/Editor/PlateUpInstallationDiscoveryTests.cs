using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class PlateUpInstallationDiscoveryTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "KitchenLibStudioDiscoveryTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test]
        public void DiscoverIncludesRootAndAdditionalLibrariesAndDeduplicatesPaths()
        {
            string steam = Path.Combine(_root, "Steam");
            string library = Path.Combine(_root, "Library");
            CreateLibraryFolders(steam, library, library.ToUpperInvariant());
            CreateManifestAndGame(library, "PlateUp", "26000811", nested: false);

            PlateUpDiscoveryResult result = new PlateUpInstallationDiscovery().Discover(new[] { steam, steam.ToUpperInvariant() });

            Assert.That(result.Installations.Count, Is.EqualTo(1));
            Assert.That(result.Installations[0].LibraryRoot, Is.EqualTo(Path.GetFullPath(library)));
            Assert.That(result.Installations[0].BuildId, Is.EqualTo("26000811"));
            Assert.That(result.Installations[0].WorkshopRoot, Is.EqualTo(Path.Combine(library, "steamapps", "workshop", "content", "1599600")));
        }

        [Test]
        public void DiscoverResolvesObservedNestedPlateUpLayout()
        {
            string steam = Path.Combine(_root, "Steam");
            CreateLibraryFolders(steam);
            string expected = CreateManifestAndGame(steam, "PlateUp", "26000811", nested: true);

            PlateUpDiscoveryResult result = new PlateUpInstallationDiscovery().Discover(new[] { steam });

            Assert.That(result.Installations.Single().GameRoot, Is.EqualTo(expected));
        }

        [Test]
        public void DiscoverReturnsDiagnosticAndManualFallbackForMalformedVdf()
        {
            string steam = Path.Combine(_root, "Steam");
            Directory.CreateDirectory(Path.Combine(steam, "steamapps"));
            File.WriteAllText(Path.Combine(steam, "steamapps", "libraryfolders.vdf"), "\"libraryfolders\" {");

            PlateUpDiscoveryResult result = new PlateUpInstallationDiscovery().Discover(new[] { steam });

            Assert.That(result.Installations, Is.Empty);
            Assert.That(result.RequiresManualSelection, Is.True);
            Assert.That(result.Diagnostics, Has.Some.Contains("libraryfolders.vdf"));
        }

        [Test]
        public void DiscoverRejectsMissingAndIncompleteManifests()
        {
            string steam = Path.Combine(_root, "Steam");
            CreateLibraryFolders(steam);
            Directory.CreateDirectory(Path.Combine(steam, "steamapps"));
            File.WriteAllText(Path.Combine(steam, "steamapps", "appmanifest_1599600.acf"),
                "\"AppState\" { \"appid\" \"1599600\" \"StateFlags\" \"1026\" \"installdir\" \"PlateUp\" \"buildid\" \"26000811\" }");

            PlateUpDiscoveryResult incomplete = new PlateUpInstallationDiscovery().Discover(new[] { steam });
            File.Delete(Path.Combine(steam, "steamapps", "appmanifest_1599600.acf"));
            PlateUpDiscoveryResult missing = new PlateUpInstallationDiscovery().Discover(new[] { steam });

            Assert.That(incomplete.Installations, Is.Empty);
            Assert.That(incomplete.Diagnostics, Has.Some.Contains("incomplete"));
            Assert.That(missing.Installations, Is.Empty);
            Assert.That(missing.Diagnostics, Has.Some.Contains("manifest"));
        }

        [Test]
        public void DiscoverReportsAmbiguousValidInstallations()
        {
            string first = Path.Combine(_root, "SteamA");
            string second = Path.Combine(_root, "SteamB");
            CreateLibraryFolders(first);
            CreateLibraryFolders(second);
            CreateManifestAndGame(first, "PlateUp", "1", nested: false);
            CreateManifestAndGame(second, "PlateUp", "2", nested: false);

            PlateUpDiscoveryResult result = new PlateUpInstallationDiscovery().Discover(new[] { first, second });

            Assert.That(result.Installations.Count, Is.EqualTo(2));
            Assert.That(result.RequiresManualSelection, Is.True);
            Assert.That(result.Diagnostics, Has.Some.Contains("Multiple"));
        }

        [Test]
        public void ValidateManualRootAcceptsStandaloneAndLeavesSteamMetadataNull()
        {
            string standalone = CreateValidGame(Path.Combine(_root, "CopiedPlateUp"));

            PlateUpInstallationValidation result = new PlateUpInstallationDiscovery().ValidateManualRoot(standalone);

            Assert.That(result.IsValid, Is.True, string.Join("\n", result.Diagnostics));
            Assert.That(result.Installation.GameRoot, Is.EqualTo(standalone));
            Assert.That(result.Installation.SteamRoot, Is.Null);
            Assert.That(result.Installation.ManifestPath, Is.Null);
            Assert.That(result.Installation.BuildId, Is.Null);
        }

        [Test]
        public void ValidateSavedRootRejectsUpdatingSteamInstallationInsteadOfTreatingItAsStandalone()
        {
            string steam = Path.Combine(_root, "Steam");
            CreateLibraryFolders(steam);
            string gameRoot = CreateManifestAndGame(steam, "PlateUp", "26000811", nested: false);
            File.WriteAllText(Path.Combine(steam, "steamapps", "appmanifest_1599600.acf"),
                "\"AppState\" { \"appid\" \"1599600\" \"StateFlags\" \"1026\" \"installdir\" \"PlateUp\" \"buildid\" \"26000811\" }");

            PlateUpInstallationValidation result = new PlateUpInstallationDiscovery().ValidateSavedRoot(gameRoot);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Diagnostics, Has.Some.Contains("incomplete"));
        }

        [Test]
        public void ValidateSavedRootPreservesSteamMetadataForCompletedInstallation()
        {
            string steam = Path.Combine(_root, "Steam");
            CreateLibraryFolders(steam);
            string gameRoot = CreateManifestAndGame(steam, "PlateUp", "26000811", nested: false);

            PlateUpInstallationValidation result = new PlateUpInstallationDiscovery().ValidateSavedRoot(gameRoot);

            Assert.That(result.IsValid, Is.True, string.Join("\n", result.Diagnostics));
            Assert.That(result.Installation.GameRoot, Is.EqualTo(gameRoot));
            Assert.That(result.Installation.LibraryRoot, Is.EqualTo(Path.GetFullPath(steam)));
            Assert.That(result.Installation.BuildId, Is.EqualTo("26000811"));
        }

        [Test]
        public void ValidateManualRootRejectsMisleadingDirectoryWithSpecificMissingFiles()
        {
            string invalid = Path.Combine(_root, "NotPlateUp");
            Directory.CreateDirectory(invalid);

            PlateUpInstallationValidation result = new PlateUpInstallationDiscovery().ValidateManualRoot(invalid);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Diagnostics, Has.Some.Contains("PlateUp.exe"));
            Assert.That(result.Diagnostics, Has.Some.Contains("Kitchen.GameData.dll"));
            Assert.That(result.Diagnostics, Has.Some.Contains("serialized asset"));
        }

        private void CreateLibraryFolders(string steamRoot, params string[] extraLibraries)
        {
            string steamApps = Path.Combine(steamRoot, "steamapps");
            Directory.CreateDirectory(steamApps);
            string entries = string.Join(" ", extraLibraries.Select((path, index) =>
                "\"" + (index + 1) + "\" { \"path\" \"" + path.Replace("\\", "\\\\") + "\" }"));
            File.WriteAllText(Path.Combine(steamApps, "libraryfolders.vdf"), "\"libraryfolders\" { " + entries + " }");
        }

        private string CreateManifestAndGame(string libraryRoot, string installDir, string buildId, bool nested)
        {
            string steamApps = Path.Combine(libraryRoot, "steamapps");
            Directory.CreateDirectory(steamApps);
            File.WriteAllText(Path.Combine(steamApps, "appmanifest_1599600.acf"),
                "\"AppState\" { \"appid\" \"1599600\" \"StateFlags\" \"4\" \"installdir\" \"" + installDir + "\" \"buildid\" \"" + buildId + "\" }");
            string candidate = Path.Combine(steamApps, "common", installDir);
            if (nested) candidate = Path.Combine(candidate, Path.GetFileName(candidate));
            return CreateValidGame(candidate);
        }

        private static string CreateValidGame(string root)
        {
            Directory.CreateDirectory(Path.Combine(root, "PlateUp_Data", "Managed"));
            File.WriteAllBytes(Path.Combine(root, "PlateUp.exe"), new byte[] { 1 });
            File.WriteAllBytes(Path.Combine(root, "PlateUp_Data", "Managed", "Kitchen.GameData.dll"), new byte[] { 1 });
            File.WriteAllBytes(Path.Combine(root, "PlateUp_Data", "resources.assets"), new byte[] { 1 });
            return Path.GetFullPath(root);
        }
    }
}
