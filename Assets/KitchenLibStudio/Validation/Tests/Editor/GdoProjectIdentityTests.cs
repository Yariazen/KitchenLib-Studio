using System;
using NUnit.Framework;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class GdoProjectIdentityTests
    {
        [Test]
        public void CanonicalUsesTrimmedInvariantLowercaseNamespaceAndName()
        {
            Assert.That(GdoProjectIdentity.Canonical("  My Project  ", "  Breakfast Menu  "),
                Is.EqualTo("my project:breakfast menu"));
            Assert.That(GdoProjectIdentity.Canonical("CAFÉ", "Crème Brûlée"),
                Is.EqualTo("café:crème brûlée"));
        }

        [Test]
        public void RuntimeIdUsesFrozenFnv1aVectorsAndNeverReturnsZero()
        {
            Assert.That(GdoProjectIdentity.RuntimeId("alpha", "breakfast"), Is.EqualTo(-357300652));
            Assert.That(GdoProjectIdentity.RuntimeId("my project", "crème brûlée"), Is.EqualTo(-242332529));
            Assert.That(GdoProjectIdentity.RuntimeId("studio", "gdo"), Is.EqualTo(-286145645));
            Assert.That(GdoProjectIdentity.RuntimeId("project", "item"), Is.Not.EqualTo(0));
        }

        [Test]
        public void AuthorScopedIdentityIsCanonicalAndDistinctFromLegacyIdentity()
        {
            Assert.That(GdoProjectIdentity.Canonical("  My Studio ", " My Mod ", " My GDO "),
                Is.EqualTo("my studio:my mod:my gdo"));
            Assert.That(GdoProjectIdentity.RuntimeId(" My Studio ", " My Mod ", " My GDO "),
                Is.EqualTo(GdoProjectIdentity.RuntimeId("my studio", "my mod", "my gdo")));
            Assert.That(GdoProjectIdentity.RuntimeId("my studio", "my mod", "my gdo"),
                Is.Not.EqualTo(GdoProjectIdentity.RuntimeId("my mod", "my gdo")));
        }

        [Test]
        public void NameValidationRejectsWindowsAndUnityInvalidNames()
        {
            string[] invalidNames =
            {
                null, "", "   ", "nested/name", "nested\\name", "line\nbreak", "tab\tname",
                "trailing.", "trailing ", "CON", "con.txt", "PRN.log", "AUX", "nul.anything",
                "COM1", "com9.txt", "LPT1", "lpt9.data", "bad<name", "bad>name", "bad:name",
                "bad\"name", "bad|name", "bad?name", "bad*name"
            };

            foreach (string name in invalidNames)
                Assert.Throws<ArgumentException>(() => GdoProjectIdentity.ValidateName(name, "projectName"), name);
        }

        [Test]
        public void BundleFileNameUsesStableSlug()
        {
            Assert.That(GdoProjectIdentity.BundleFileName("  My Breakfast Menu!  "), Is.EqualTo("my-breakfast-menu.assets"));
            Assert.That(GdoProjectIdentity.BundleFileName("Crème & Soup"), Is.EqualTo("cr-me-soup.assets"));
            Assert.That(GdoProjectIdentity.BundleFileName("!!!"), Is.EqualTo("gdo-bundle.assets"));
        }
    }
}
