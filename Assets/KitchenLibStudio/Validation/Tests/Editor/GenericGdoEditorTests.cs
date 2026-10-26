using System.Linq;
using KitchenData;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class GenericGdoEditorTests
    {
        [TestCase(typeof(Dish))]
        [TestCase(typeof(Item))]
        [TestCase(typeof(Appliance))]
        public void RepresentativeGdosUseStructuredGenericInspector(System.Type type)
        {
            GameDataObject target = (GameDataObject)ScriptableObject.CreateInstance(type);
            UnityEditor.Editor editor = UnityEditor.Editor.CreateEditor(target, typeof(GenericGdoEditor));
            try
            {
                VisualElement root = ((GenericGdoEditor)editor).CreateInspectorGUI();

                Assert.That(root.Q<GdoInspectorHeader>(), Is.Not.Null);
                Assert.That(root.Q<Label>("runtime-type").text, Is.EqualTo(type.FullName));
                Assert.That(root.Q<Label>("schema-status").text, Does.Contain("Generated descriptor ready"));
                Assert.That(root.Query<GdoInspectorSectionElement>().ToList().Select(section => section.Kind),
                    Is.EqualTo(new[] { GdoInspectorFieldKind.Authored, GdoInspectorFieldKind.Relationship, GdoInspectorFieldKind.RuntimeDerived, GdoInspectorFieldKind.Unsupported }));
                Assert.That(root.Q(className: "kls-relationship-notice"), Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(editor);
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void MultiObjectSelectionShowsReadOnlyDiagnosticAndDoesNotMutateAssets()
        {
            Dish first = ScriptableObject.CreateInstance<Dish>();
            Dish second = ScriptableObject.CreateInstance<Dish>();
            UnityEditor.Editor editor = UnityEditor.Editor.CreateEditor(new Object[] { first, second }, typeof(GenericGdoEditor));
            try
            {
                VisualElement root = ((GenericGdoEditor)editor).CreateInspectorGUI();

                Assert.That(root.Q<HelpBox>("multi-object-diagnostic"), Is.Not.Null);
                Assert.That(root.enabledSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(editor);
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void UnknownConcreteTypeBuildsReadOnlyDiagnosticInspector()
        {
            UnavailableFixture target = ScriptableObject.CreateInstance<UnavailableFixture>();
            UnityEditor.Editor editor = UnityEditor.Editor.CreateEditor(target, typeof(GenericGdoEditor));
            try
            {
                VisualElement root = ((GenericGdoEditor)editor).CreateInspectorGUI();

                Assert.That(root.Q<HelpBox>("unknown-type-diagnostic"), Is.Not.Null);
                Assert.That(root.enabledSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(editor);
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void ZeroIdDiagnosticAppearsOnIdFieldAndInAggregateHeader()
        {
            Dish target = ScriptableObject.CreateInstance<Dish>();
            UnityEditor.Editor editor = UnityEditor.Editor.CreateEditor(target, typeof(GenericGdoEditor));
            try
            {
                VisualElement root = ((GenericGdoEditor)editor).CreateInspectorGUI();

                Assert.That(root.Q<VisualElement>("KitchenData.GameDataObject.ID").Query<HelpBox>().ToList(), Is.Not.Empty);
                Assert.That(root.Q<VisualElement>("diagnostic-summary"), Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(editor);
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void StaleCompanionShowsSchemaDriftAndReviewAction()
        {
            const string folder = "Assets/__KitchenLibStudioInspectorSchemaTest";
            AssetDatabase.CreateFolder("Assets", "__KitchenLibStudioInspectorSchemaTest");
            Dish target = ScriptableObject.CreateInstance<Dish>();
            target.ID = 91;
            AssetDatabase.CreateAsset(target, folder + "/Dish.asset");
            GdoOverlayAuthoringAsset companion = GdoOverlayAuthoringRepository.GetOrCreate(target);
            var serialized = new SerializedObject(companion);
            serialized.FindProperty("_descriptorFingerprint").stringValue = "STALE";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            UnityEditor.Editor editor = UnityEditor.Editor.CreateEditor(target, typeof(GenericGdoEditor));
            try
            {
                VisualElement root = ((GenericGdoEditor)editor).CreateInspectorGUI();
                Assert.That(root.Q<Label>("schema-status").text, Does.Contain("Schema drift"));
                Assert.That(root.Q<Button>("accept-schema"), Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(editor);
                AssetDatabase.DeleteAsset(folder);
            }
        }

        private sealed class UnavailableFixture : DictionaryLocalisation
        {
            protected override void InitialiseDefaults() { }
        }
    }
}
