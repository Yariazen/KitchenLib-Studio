using System.Linq;
using KitchenData;
using NUnit.Framework;
using UnityEditor;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class GraphFirstGdoProofAssetTests
    {
        [SetUp] public void SetUp() => GraphFirstGdoProofAsset.Delete();
        [TearDown] public void TearDown() => GraphFirstGdoProofAsset.Delete();

        [Test]
        public void CreatesRepresentativeDishItemApplianceThroughGraphCommands()
        {
            GdoBundleGraphAsset graph = GraphFirstGdoProofAsset.CreateOrUpdate();

            Assert.That(graph.Nodes.Where(node => node.Kind == GdoGraphNodeKind.Local).Select(node => node.RuntimeTypeName),
                Is.EquivalentTo(new[] { "KitchenData.Dish", "KitchenData.Item", "KitchenData.Appliance" }));
            Assert.That(graph.Relationships, Is.Not.Empty);
            Assert.That(graph.Edges, Is.Not.Empty);
            Assert.That(GdoBundleGraphValidator.Validate(graph).Where(issue => issue.Severity >= ValidationSeverity.Error), Is.Empty);
        }
    }
}
