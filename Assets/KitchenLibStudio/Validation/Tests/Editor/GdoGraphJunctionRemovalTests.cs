using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class GdoGraphJunctionRemovalTests
    {
        private const string Root = "Assets/JunctionRemovalTests";

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(Root);
            AssetDatabase.CreateFolder("Assets", "JunctionRemovalTests");
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(Root);
            Undo.ClearAll();
        }

        [Test]
        public void SchemaThreeMigrationRemovesUnknownEndpointEdgesAndPreservesDirectEdges()
        {
            GdoBundleGraphAsset graph = GdoBundleGraphRepository.Create(
                Root + "/Graph.asset", "Test Author", "Direct Edges", "direct-edges.assets");
            GdoGraphNodeRecord source = GdoBundleGraphRepository.AddExternal(graph,
                ExternalGdoCatalogRepository.VanillaProviderKey, 1, "KitchenData.Item", new Rect());
            GdoGraphNodeRecord destination = GdoBundleGraphRepository.AddExternal(graph,
                ExternalGdoCatalogRepository.VanillaProviderKey, 2, "KitchenData.Item", new Rect());
            GdoGraphEdgeRecord direct = GdoBundleGraphRepository.AddEdge(
                graph, source.Id, "Value", destination.Id, "Value", 0);
            GdoBundleGraphRepository.AddEdge(graph, source.Id, "Value", destination.Id, "Other", 1);
            var serialized = new SerializedObject(graph);
            SerializedProperty edges = serialized.FindProperty("_edges");
            edges.GetArrayElementAtIndex(1).FindPropertyRelative("_inputNodeId").stringValue =
                "removed-junction";
            serialized.FindProperty("_schemaVersion").intValue = 3;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(GdoBundleGraphRepository.EnsureCurrentSchema(graph), Is.True);

            Assert.That(graph.SchemaVersion, Is.EqualTo(4));
            Assert.That(graph.Edges.Count, Is.EqualTo(1));
            Assert.That(graph.Edges[0].Id, Is.EqualTo(direct.Id));
        }
    }
}
