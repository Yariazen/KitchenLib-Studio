using System;
using System.IO;
using KitchenLib.Studio.Editor;
using NUnit.Framework;

namespace KitchenLib.Studio.Tests.Editor
{
    public sealed class MirrorGenerationMenuTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "KitchenLib Studio Menu Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(_root, "PlateUp_Data", "Managed"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        [Test]
        public void ResolveGameDataAssemblyReturnsManagedDllFromRootWithSpaces()
        {
            string expected = Path.Combine(_root, "PlateUp_Data", "Managed", "Kitchen.GameData.dll");
            File.WriteAllBytes(expected, new byte[] { 1 });

            Assert.That(MirrorGenerationMenu.ResolveGameDataAssembly(_root), Is.EqualTo(Path.GetFullPath(expected)));
        }

        [Test]
        public void ResolveGameDataAssemblyRejectsRootWithoutDll()
        {
            var exception = Assert.Throws<FileNotFoundException>(() => MirrorGenerationMenu.ResolveGameDataAssembly(_root));

            Assert.That(exception.FileName, Does.EndWith(Path.Combine("PlateUp_Data", "Managed", "Kitchen.GameData.dll")));
        }

        [Test]
        public void MirrorSeedCompatibilityRequiresTheInstalledAssemblyHash()
        {
            const string manifest = "{\"sourceSha256\":\"ABC123\"}";

            Assert.That(MirrorGenerationMenu.IsMirrorSeedCompatible(manifest, "abc123"), Is.True);
            Assert.That(MirrorGenerationMenu.IsMirrorSeedCompatible(manifest, "different"), Is.False);
        }

        [Test]
        public void CatalogueFilesAreTheOnlyGeneratedFilesPreservedDuringMirrorSync()
        {
            Assert.That(MirrorGenerationMenu.ShouldPreserveDuringMirrorSync("vanilla-gdo-catalog.json"), Is.True);
            Assert.That(MirrorGenerationMenu.ShouldPreserveDuringMirrorSync("vanilla-snapshot-catalog.json.meta"), Is.True);
            Assert.That(MirrorGenerationMenu.ShouldPreserveDuringMirrorSync("vanilla-snapshot-index.json"), Is.True);
            Assert.That(MirrorGenerationMenu.ShouldPreserveDuringMirrorSync("ObsoleteMirror.cs"), Is.False);
        }

        [TestCase("plain", "plain")]
        [TestCase("", "\"\"")]
        [TestCase("path with spaces", "\"path with spaces\"")]
        [TestCase("a\\\"b", "\"a\\\\\\\"b\"")]
        [TestCase("trailing slash\\", "\"trailing slash\\\\\"")]
        public void QuoteWindowsArgumentHandlesSpacesQuotesAndTrailingSlashes(string input, string expected)
        {
            Assert.That(MirrorGenerationMenu.QuoteWindowsArgument(input), Is.EqualTo(expected));
        }
    }
}
