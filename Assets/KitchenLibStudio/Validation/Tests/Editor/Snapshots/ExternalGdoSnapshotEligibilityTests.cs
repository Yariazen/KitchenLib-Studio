using NUnit.Framework;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class ExternalGdoSnapshotEligibilityTests
    {
        [TestCase("KitchenData.Item")]
        [TestCase("KitchenData.Dish")]
        [TestCase("KitchenData.Appliance")]
        [TestCase("KitchenData.PlayerCosmetic")]
        public void ApprovedRootTypesAreEligible(string runtimeType)
        {
            Assert.That(ExternalGdoSnapshotEligibility.IsEligible(runtimeType), Is.True);
        }

        [Test]
        public void ItemGroupIsEligibleThroughItemInheritance()
        {
            Assert.That(GdoDescriptorCatalog.IsAssignable("KitchenData.ItemGroup", "KitchenData.Item"), Is.True,
                "The generated descriptor fixture must prove the derived-type branch.");
            Assert.That(ExternalGdoSnapshotEligibility.IsEligible("KitchenData.ItemGroup"), Is.True);
        }

        [TestCase("KitchenData.Process")]
        [TestCase("KitchenData.GameDataObject")]
        [TestCase("Missing.Type")]
        [TestCase("")]
        [TestCase(null)]
        public void TypesOutsideApprovedFamiliesAreNotEligible(string runtimeType)
        {
            Assert.That(ExternalGdoSnapshotEligibility.IsEligible(runtimeType), Is.False);
        }
    }
}
