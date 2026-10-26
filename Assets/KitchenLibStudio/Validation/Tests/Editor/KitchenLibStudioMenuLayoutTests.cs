using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class KitchenLibStudioMenuLayoutTests
    {
        [Test]
        public void MenuCommandsUseApprovedSubmenus()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "KitchenLibStudio"));
            string[] lines = Directory.GetFiles(Path.Combine(root, "Editor"), "*.cs", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(Path.Combine(root, "Validation", "Editor"), "*.cs", SearchOption.AllDirectories))
                .SelectMany(File.ReadAllLines).Where(value => value.Contains("[MenuItem(\"KitchenLib Studio/")).ToArray();
            string joined = string.Join("\n", lines);

            Assert.That(joined, Does.Contain("KitchenLib Studio/GDO Bundle Graph"));
            Assert.That(joined, Does.Contain("KitchenLib Studio/Snapshot Browser"));
            Assert.That(joined, Does.Contain("KitchenLib Studio/Catalogues/Refresh All"));
            Assert.That(joined, Does.Contain("KitchenLib Studio/Catalogues/Refresh Vanilla GDO Catalogue"));
            Assert.That(joined, Does.Contain("KitchenLib Studio/Catalogues/Refresh Snapshot Catalogue"));
            Assert.That(joined, Does.Contain("KitchenLib Studio/Catalogues/Change PlateUp Installation…"));
            Assert.That(joined, Does.Contain("KitchenLib Studio/Maintenance/Regenerate All"));
            Assert.That(joined, Does.Contain("KitchenLib Studio/Maintenance/Clean Generated Environment…"));
            Assert.That(lines.Where(value => value.Contains("Proof")), Has.All.Contains("KitchenLib Studio/Validation/"));
            Assert.That(joined, Does.Not.Contain("KitchenLib Studio/Import PlateUp Vanilla Catalog"));
            Assert.That(joined, Does.Not.Contain("KitchenLib Studio/Refresh Vanilla Catalog\""));
            Assert.That(joined, Does.Not.Contain("KitchenLib Studio/Regenerate Mirrors"));
            Assert.That(joined, Does.Not.Contain("KitchenLib Studio/Clear PlateUp Installation"));
        }
    }
}
