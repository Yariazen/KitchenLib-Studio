using System.Linq;
using KitchenData;
using KitchenLib.Studio.Generated;
using NUnit.Framework;
using UnityEngine;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class GdoInspectorModelTests
    {
        [Test]
        public void BuildGroupsFieldsByBehaviorThenDeclaringType()
        {
            Dish dish = ScriptableObject.CreateInstance<Dish>();
            try
            {
                GdoInspectorModel model = GdoInspectorModel.Build(GdoDescriptorCatalog.ForTarget(dish));

                Assert.That(model.Section(GdoInspectorFieldKind.Authored).Fields.Select(field => field.Name), Does.Contain("Difficulty"));
                Assert.That(model.Section(GdoInspectorFieldKind.Relationship).Fields.Select(field => field.Name), Does.Contain("MinimumIngredients"));
                Assert.That(model.Section(GdoInspectorFieldKind.Authored).Groups.Select(group => group.DeclaringTypeName),
                    Does.Contain("KitchenData.LocalisedGameDataObject`1"));
                Assert.That(model.Sections.SelectMany(section => section.Fields).Select(field => field.Key).Distinct().Count(),
                    Is.EqualTo(model.Sections.SelectMany(section => section.Fields).Count()));
            }
            finally { Object.DestroyImmediate(dish); }
        }

        [Test]
        public void BuildOrdersSectionsAndFieldsDeterministically()
        {
            GdoInspectorModel first = GdoInspectorModel.Build(GdoDescriptorCatalog.ForRuntimeType("KitchenData.Dish"));
            GdoInspectorModel second = GdoInspectorModel.Build(GdoDescriptorCatalog.ForRuntimeType("KitchenData.Dish"));

            Assert.That(first.Sections.Select(section => section.Kind), Is.EqualTo(new[]
            {
                GdoInspectorFieldKind.Authored,
                GdoInspectorFieldKind.Relationship,
                GdoInspectorFieldKind.RuntimeDerived,
                GdoInspectorFieldKind.Unsupported
            }));
            Assert.That(first.Sections.SelectMany(section => section.Fields).Select(field => field.Key),
                Is.EqualTo(second.Sections.SelectMany(section => section.Fields).Select(field => field.Key)));
            foreach (GdoInspectorSection section in first.Sections)
                foreach (GdoInspectorGroup group in section.Groups)
                    Assert.That(group.Fields.Select(field => field.Name),
                        Is.EqualTo(group.Fields.Select(field => field.Name).OrderBy(name => name, System.StringComparer.Ordinal)));
        }

        [Test]
        public void ContainsGdoReferenceFindsNestedCollectionStructReferences()
        {
            GdoFieldDescriptorData field = GdoDescriptorCatalog.ForRuntimeType("KitchenData.Item").Data.Fields
                .Single(item => item.Name == "Processes");

            Assert.That(GdoInspectorModel.ContainsGdoReference(field.Value), Is.True);
        }

        [Test]
        public void BuildMarksRuntimeDerivedAndUnsupportedFieldsReadOnly()
        {
            GdoInspectorModel dish = GdoInspectorModel.Build(GdoDescriptorCatalog.ForRuntimeType("KitchenData.Dish"));
            GdoInspectorModel unavailable = GdoInspectorModel.Build(GdoDescriptorCatalog.ForRuntimeType("KitchenData.DictionaryLocalisation"));

            GdoInspectorField runtime = dish.Section(GdoInspectorFieldKind.RuntimeDerived).Fields
                .Single(field => field.Name == "UnlocksMenuItems");
            Assert.That(runtime.IsReadOnly, Is.True);
            Assert.That(runtime.Description, Does.Contain("SetupForGame"));
            Assert.That(unavailable.Section(GdoInspectorFieldKind.Unsupported).Fields, Is.Not.Empty);
            Assert.That(unavailable.Section(GdoInspectorFieldKind.Unsupported).Fields.All(field => field.IsReadOnly), Is.True);
            Assert.That(unavailable.Section(GdoInspectorFieldKind.Unsupported).Fields.First().Description,
                Does.Contain("Unsupported serialized shape").And.Contain("KitchenData.DictionaryLocalisation.Info"));
            GdoInspectorField id = dish.Section(GdoInspectorFieldKind.RuntimeDerived).Fields.Single(field => field.Name == "ID");
            Assert.That(id.IsReadOnly, Is.True);
            Assert.That(dish.Section(GdoInspectorFieldKind.Authored).Fields.Any(field => field.Name == "ID"), Is.False);
        }

        [TestCase("KitchenData.Dish")]
        [TestCase("KitchenData.Item")]
        [TestCase("KitchenData.Appliance")]
        public void DishItemAndApplianceUseTheSameModelPath(string runtimeType)
        {
            GdoInspectorModel model = GdoInspectorModel.Build(GdoDescriptorCatalog.ForRuntimeType(runtimeType));

            Assert.That(model.RuntimeTypeName, Is.EqualTo(runtimeType));
            Assert.That(model.Sections.Count, Is.EqualTo(4));
            Assert.That(model.Sections.SelectMany(section => section.Fields), Is.Not.Empty);
        }
    }
}
