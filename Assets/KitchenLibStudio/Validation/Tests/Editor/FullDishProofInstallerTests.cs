using System;
using System.IO;
using KitchenLib.Studio.Editor;
using NUnit.Framework;

namespace KitchenLib.Studio.Tests.Editor
{
    public sealed class FullDishProofInstallerTests
    {
        [Test]
        public void MissingSharedLinkerFailsBeforeCreatingModsDirectory()
        {
            string root = Path.Combine(Path.GetTempPath(), "KLStudio Full Dish Installer", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                Assert.Throws<FileNotFoundException>(() => FullDishProofInstaller.Install("missing-package", "missing-bundle", "missing-linker", "missing-validator", root));
                Assert.That(Directory.Exists(Path.Combine(root, "Mods")), Is.False);
            }
            finally { Directory.Delete(root, true); }
        }
    }
}
