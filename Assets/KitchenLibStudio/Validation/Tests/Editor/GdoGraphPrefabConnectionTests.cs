using System.Linq;
using KitchenData;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class GdoGraphPrefabConnectionTests
    {
        private const string Root = "Assets/Prefab Connection Tests";

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(Root);
            AssetDatabase.CreateFolder("Assets", "Prefab Connection Tests");
        }

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(Root);

        [Test]
        public void ItemExposesGameObjectPrefabPorts()
        {
            GdoTypeDescriptorView descriptor = GdoDescriptorCatalog.ForRuntimeType("KitchenData.Item");

            string[] paths = GdoUnityObjectPortCatalog.ForDescriptor(descriptor)
                .Select(port => port.Path).ToArray();

            Assert.That(paths, Has.Some.EndsWith(".Prefab"));
        }

        [Test]
        public void PrefabConnectionAssignsAndDisconnectRestoresManualValue()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "Prefab Project");
            Item item = ScriptableObject.CreateInstance<Item>();
            item.name = "Test Item";
            item.ID = 12345;
            AssetDatabase.CreateAsset(item, GdoBundleProjectRepository.GdoFolder(graph) + "/Test Item.asset");
            GdoGraphNodeRecord owner = GdoBundleGraphRepository.AddMember(graph, item, new Rect());
            GdoGraphPrefabRecord source = GdoBundleGraphRepository.AddPrefab(graph, "Test Cube", new Rect());
            string path = GdoUnityObjectPortCatalog.ForDescriptor(GdoDescriptorCatalog.ForTarget(item))
                .Single(port => port.Path.EndsWith(".Prefab")).Path;
            GameObject previousInstance = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            GameObject previous = PrefabUtility.SaveAsPrefabAsset(previousInstance, Root + "/Previous.prefab");
            Object.DestroyImmediate(previousInstance);
            var serialized = new SerializedObject(item);
            serialized.FindProperty("Prefab").objectReferenceValue = previous;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            GdoGraphEdgeRecord edge = new GdoGraphConnectionController(graph).Connect(
                source.Id, owner.Id, path, 0);

            serialized.Update();
            GameObject generated = serialized.FindProperty("Prefab").objectReferenceValue as GameObject;
            Assert.That(generated, Is.Not.Null.And.Not.SameAs(previous));

            new GdoGraphConnectionController(graph).Disconnect(edge.Id);
            serialized.Update();
            Assert.That(serialized.FindProperty("Prefab").objectReferenceValue, Is.SameAs(previous));
        }

        [Test]
        public void PrimitiveNodeExposesUnifiedPrefabControlsAndOutput()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "Prefab Project");
            GdoGraphPrefabRecord record = GdoBundleGraphRepository.AddPrefab(graph, "Test Cube", new Rect());

            var view = new GdoGraphPrefabNodeView(graph, record, () => { }, () => { });

            Assert.That(view.Q<TextField>("prefab-name"), Is.Not.Null);
            Assert.That(view.Q<EnumField>("prefab-source-mode"), Is.Not.Null);
            Assert.That(view.Q<EnumField>("prefab-primitive-type"), Is.Not.Null);
            Assert.That(view.Q<Vector3Field>("prefab-scale"), Is.Not.Null);
            Assert.That(view.Q<ObjectField>("prefab-material"), Is.Not.Null);
            Assert.That(view.Q<Toggle>("prefab-collider"), Is.Not.Null);
            Assert.That(view.outputContainer.Query<Port>().ToList().Single().userData, Is.EqualTo("Value"));
            Assert.That((view.capabilities & Capabilities.Deletable) == 0, Is.True);
        }

        [Test]
        public void FailedPrefabProjectionRestoresEarlierAssignmentsAndState()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "Prefab Project");
            Item item = ScriptableObject.CreateInstance<Item>();
            item.name = "Test Item"; item.ID = 12345;
            AssetDatabase.CreateAsset(item, GdoBundleProjectRepository.GdoFolder(graph) + "/Test Item.asset");
            GdoGraphNodeRecord owner = GdoBundleGraphRepository.AddMember(graph, item, new Rect());
            GdoGraphPrefabRecord source = GdoBundleGraphRepository.AddPrefab(graph, "Test Cube", new Rect());
            string validPath = GdoUnityObjectPortCatalog.ForDescriptor(GdoDescriptorCatalog.ForTarget(item))
                .Single(port => port.Path.EndsWith(".Prefab")).Path;
            GameObject previousInstance = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            GameObject previous = PrefabUtility.SaveAsPrefabAsset(previousInstance, Root + "/Previous.prefab");
            Object.DestroyImmediate(previousInstance);
            var serialized = new SerializedObject(item);
            serialized.FindProperty("Prefab").objectReferenceValue = previous;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            GdoBundleGraphRepository.AddEdge(graph, source.Id, "Value", owner.Id, validPath, 0);
            GdoBundleGraphRepository.AddEdge(graph, source.Id, "Value", owner.Id,
                "KitchenData.Item.NotAField", 1);

            Assert.That(GdoGraphPrefabProjection.TryApply(graph, out _, false), Is.False);

            serialized.Update();
            Assert.That(serialized.FindProperty("Prefab").objectReferenceValue, Is.SameAs(previous));
            Assert.That(graph.PrefabProjectionStates, Is.Empty);
        }
    }
}
