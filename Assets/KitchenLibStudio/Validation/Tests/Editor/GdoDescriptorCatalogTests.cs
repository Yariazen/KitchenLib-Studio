using System.Linq;
using KitchenData;
using NUnit.Framework;
using UnityEngine;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class GdoDescriptorCatalogTests
    {
        [Test]
        public void DishLookupReturnsInstalledSchemaFingerprintAndFields()
        {
            Dish dish = ScriptableObject.CreateInstance<Dish>();
            try
            {
                GdoTypeDescriptorView descriptor = GdoDescriptorCatalog.ForTarget(dish);

                Assert.That(descriptor.RuntimeTypeName, Is.EqualTo("KitchenData.Dish"));
                Assert.That(descriptor.DescriptorFingerprint, Does.Match("^[A-F0-9]{64}$"));
                Assert.That(descriptor.IsAvailable, Is.True);
                Assert.That(descriptor.Data.Fields.Select(field => field.Name), Does.Contain("MinimumIngredients"));
                Assert.That(GdoDescriptorCatalog.GlobalFingerprint, Does.Match("^[A-F0-9]{64}$"));
            }
            finally { Object.DestroyImmediate(dish); }
        }

        [Test]
        public void RuntimeTypeLookupReturnsExactUnavailableDiagnostic()
        {
            GdoTypeDescriptorView descriptor = GdoDescriptorCatalog.ForRuntimeType("KitchenData.DictionaryLocalisation");

            Assert.That(descriptor, Is.Not.Null);
            Assert.That(descriptor.IsAvailable, Is.False);
            Assert.That(descriptor.Data.Diagnostics, Is.Not.Empty);
            Assert.That(descriptor.Data.Diagnostics[0].Path, Does.StartWith("KitchenData.DictionaryLocalisation."));
        }

        [Test]
        public void UnknownConcreteSubtypeDoesNotFallBackToBaseDescriptor()
        {
            UnknownDish target = ScriptableObject.CreateInstance<UnknownDish>();
            try
            {
                Assert.Throws<System.InvalidOperationException>(() => GdoDescriptorCatalog.ForTarget(target));
            }
            finally { Object.DestroyImmediate(target); }
        }

        private sealed class UnknownDish : Dish { }
    }
}
