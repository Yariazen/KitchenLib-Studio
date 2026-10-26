using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class ValidationAssemblyBoundaryTests
    {
        [Test]
        public void ProofSourcesAndTestsLiveUnderValidationWithoutProductionDependency()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "KitchenLibStudio"));
            string validation = Path.Combine(root, "Validation");
            string editorAsmdef = File.ReadAllText(Path.Combine(root, "Editor", "KitchenLib.Studio.Editor.asmdef"));

            Assert.That(Directory.Exists(Path.Combine(validation, "Editor")), Is.True);
            Assert.That(Directory.Exists(Path.Combine(validation, "Tests", "Editor")), Is.True);
            Assert.That(File.Exists(Path.Combine(validation, "Editor", "KitchenLib.Studio.Validation.Editor.asmdef")), Is.True);
            Assert.That(editorAsmdef, Does.Not.Contain("KitchenLib.Studio.Validation"));
            Assert.That(Directory.GetFiles(Path.Combine(root, "Editor"), "*Proof*.cs", SearchOption.TopDirectoryOnly), Is.Empty);
        }

        [Test]
        public void EveryProofMenuIsUnderValidationSubmenu()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "KitchenLibStudio"));
            string[] sources = Directory.GetFiles(root, "*Proof*.cs", SearchOption.AllDirectories);
            string[] menuLines = sources.SelectMany(File.ReadAllLines).Where(value => value.Contains("[MenuItem(")).ToArray();

            Assert.That(menuLines, Is.Not.Empty);
            Assert.That(menuLines, Has.All.Contains("KitchenLib Studio/Validation/"));
        }
    }
}
