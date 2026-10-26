using System.Linq;
using KitchenData;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class GdoGraphNodeEditorElementTests
    {
        private const string Root = "Assets/GraphNodeEditorTests";
        [SetUp] public void SetUp() { AssetDatabase.DeleteAsset(Root); AssetDatabase.CreateFolder("Assets", "GraphNodeEditorTests"); }
        [TearDown] public void TearDown() { AssetDatabase.DeleteAsset(Root); Undo.ClearAll(); }

        [Test]
        public void ExpandedNodeShowsIntrinsicFieldsAndPortsWithoutRelationshipSummaries()
        {
            Item item = ScriptableObject.CreateInstance<Item>(); item.name = "Editable"; item.ID = 401;
            AssetDatabase.CreateAsset(item, Root + "/Item.asset");
            GdoBundleGraphAsset graph = GdoBundleGraphRepository.Create(Root + "/Graph.asset", "Test Author", "Graph", "graph.assets");
            GdoGraphNodeRecord record = GdoBundleGraphRepository.AddMember(graph, item, new Rect());
            var node = new GdoGraphNodeView(record);

            Assert.That(node.EditorBody, Is.Null);
            node.SetExpanded(true);

            Assert.That(node.EditorBody, Is.Not.Null);
            Assert.That(node.EditorBody.Query<GdoIntrinsicFieldElement>().ToList().Count, Is.GreaterThan(0));
            Assert.That(node.EditorBody.Query<Label>(className: "kls-relationship-summary").ToList(), Is.Empty);
            Assert.That(node.EditorBody.Q<IntegerField>("stable-gdo-id").value, Is.EqualTo(401));
            Assert.That(node.EditorBody.Q<IntegerField>("stable-gdo-id").enabledSelf, Is.False);
            Assert.That(node.outputContainer.childCount, Is.GreaterThan(0));
            Assert.That(node.inputContainer.childCount, Is.GreaterThan(0));
        }

        [Test]
        public void SharedIntrinsicElementPersistsOverlayFieldsThroughCompanion()
        {
            Item item = ScriptableObject.CreateInstance<Item>(); item.name = "Overlay"; item.ID = 402;
            AssetDatabase.CreateAsset(item, Root + "/Item.asset");
            GdoInspectorField field = GdoInspectorModel.Build(GdoDescriptorCatalog.ForTarget(item))
                .Section(GdoInspectorFieldKind.Relationship).Fields.First(value => value.Descriptor.Classification != "Native");
            var element = new GdoIntrinsicFieldElement(field, new SerializedObject(item), item,
                () => GdoOverlayAuthoringRepository.GetOrCreate(item), null);

            element.CommitOverlay(GdoOverlayValueEditor.CreateDefault(field.Descriptor.Value));

            GdoOverlayAuthoringAsset companion = GdoOverlayAuthoringRepository.Find(item);
            Assert.That(companion, Is.Not.Null);
            Assert.That(companion.TryGetField(field.DeclaringTypeName, field.Name, out _), Is.True);
        }

        [Test]
        public void EditorBodyStopsAtConfiguredFieldLimit()
        {
            Item item = ScriptableObject.CreateInstance<Item>(); item.name = "Limited"; item.ID = 403;
            AssetDatabase.CreateAsset(item, Root + "/Item.asset");
            var body = new GdoGraphNodeEditorElement(item, 1);

            Assert.That(body.Query<GdoIntrinsicFieldElement>().ToList().Count, Is.EqualTo(1));
            Assert.That(body.Q<HelpBox>("field-limit"), Is.Not.Null);
        }
    }
}
