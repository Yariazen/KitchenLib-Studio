using KitchenLib.Studio.Editor;
using NUnit.Framework;

namespace KitchenLib.Studio.Tests.Editor
{
    public sealed class DishOverlayInspectorModelTests
    {
        [TestCase("Requires", "KitchenData.Unlock", "HardcodedRequirements", "SetupForGame")]
        [TestCase("Blocked By", "KitchenData.Unlock", "HardcodedBlockers", "SetupForGame")]
        [TestCase("Menu Items", "KitchenData.Dish", "ResultingMenuItems", "SetupForGame")]
        [TestCase("Ingredient Unlocks", "KitchenData.Dish", "IngredientsUnlocks", "SetupForGame")]
        [TestCase("Localizations", "KitchenData.LocalisedGameDataObject`1", "Info", "Localise")]
        public void RuntimeFriendlyLabelMapsToSerializedBackingField(
            string label,
            string declaringType,
            string authoringField,
            string populationMethod)
        {
            DishOverlayFieldDescriptor descriptor = DishOverlayInspectorModel.Find(label);

            Assert.That(descriptor.DeclaringType, Is.EqualTo(declaringType));
            Assert.That(descriptor.AuthoringField, Is.EqualTo(authoringField));
            Assert.That(descriptor.PopulationMethod, Is.EqualTo(populationMethod));
        }
    }
}
