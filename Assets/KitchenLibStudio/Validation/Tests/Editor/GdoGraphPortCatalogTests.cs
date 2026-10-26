using System.Linq;
using NUnit.Framework;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class GdoGraphPortCatalogTests
    {
        [Test]
        public void DishPortsDescribeDirectSetAndStructuredRelationships()
        {
            GdoGraphPortDescriptor[] ports = GdoGraphPortCatalog
                .ForDescriptor(GdoDescriptorCatalog.ForRuntimeType("KitchenData.Dish")).ToArray();

            GdoGraphPortDescriptor minimum = ports.Single(port => port.Path == "KitchenData.Dish.MinimumIngredients[]");
            Assert.That(minimum.ExpectedTypeName, Is.EqualTo("KitchenData.Item"));
            Assert.That(minimum.Cardinality, Is.EqualTo(GdoGraphPortCardinality.Multiple));
            Assert.That(minimum.CollectionKind, Is.EqualTo(GdoGraphCollectionKind.Set));
            Assert.That(minimum.RelationshipPath, Is.EqualTo("KitchenData.Dish.MinimumIngredients"));
            Assert.That(minimum.UsesRelationshipNode, Is.False);

            GdoGraphPortDescriptor menuItem = ports.Single(port =>
                port.Path == "KitchenData.Dish.ResultingMenuItems[].Item");
            Assert.That(menuItem.ExpectedTypeName, Is.EqualTo("KitchenData.Item"));
            Assert.That(menuItem.UsesRelationshipNode, Is.True);
            Assert.That(menuItem.RelationshipPath, Is.EqualTo("KitchenData.Dish.ResultingMenuItems[]"));
            Assert.That(ports.Any(port => port.Path == "KitchenData.Dish.ResultingMenuItems[].DynamicMenuIngredient"), Is.True);
        }

        [Test]
        public void PortOrderingIsStableAndEveryPathIsUnique()
        {
            string[] first = GdoGraphPortCatalog.ForDescriptor(GdoDescriptorCatalog.ForRuntimeType("KitchenData.Appliance"))
                .Select(port => port.Path).ToArray();
            string[] second = GdoGraphPortCatalog.ForDescriptor(GdoDescriptorCatalog.ForRuntimeType("KitchenData.Appliance"))
                .Select(port => port.Path).ToArray();

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first.Distinct().Count(), Is.EqualTo(first.Length));
            Assert.That(first, Is.EqualTo(first.OrderBy(path => path, System.StringComparer.Ordinal)));
        }
    }
}
