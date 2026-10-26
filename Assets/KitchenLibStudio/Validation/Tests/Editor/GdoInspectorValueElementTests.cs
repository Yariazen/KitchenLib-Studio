using System.Collections.Generic;
using System.Linq;
using KitchenLib.Studio.Generated;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class GdoInspectorValueElementTests
    {
        [TestCase("Scalar", typeof(TextField))]
        [TestCase("String", typeof(TextField))]
        [TestCase("Enum", typeof(PopupField<string>))]
        [TestCase("Struct", typeof(Foldout))]
        [TestCase("List", typeof(Foldout))]
        [TestCase("Set", typeof(Foldout))]
        [TestCase("LocalizationMap", typeof(Foldout))]
        public void CreateUsesDescriptorKind(string kind, System.Type expectedControl)
        {
            GdoValueDescriptorData descriptor = Descriptor(kind);
            GdoOverlayValue value = GdoOverlayValueEditor.CreateDefault(descriptor);

            VisualElement element = GdoInspectorValueElement.Create("Value", descriptor, value, _ => { });

            Assert.That(element.Q(className: "kls-gdo-value"), Is.Not.Null);
            Assert.That(element.Query<VisualElement>().ToList().Exists(expectedControl.IsInstanceOfType), Is.True);
            if (element.Q<TextInputBaseField<int>>() is TextInputBaseField<int> integer) Assert.That(integer.isDelayed, Is.True);
            if (element.Q<TextInputBaseField<long>>() is TextInputBaseField<long> longField) Assert.That(longField.isDelayed, Is.True);
            if (element.Q<TextInputBaseField<float>>() is TextInputBaseField<float> floatField) Assert.That(floatField.isDelayed, Is.True);
            if (element.Q<TextInputBaseField<double>>() is TextInputBaseField<double> doubleField) Assert.That(doubleField.isDelayed, Is.True);
        }

        [Test]
        public void ScalarChangeReturnsNewOverlayValue()
        {
            VisualElement element = GdoInspectorValueElement.Create("Value", Descriptor("Scalar"),
                GdoOverlayValue.Scalar("1"), _ => { });

            Assert.That(element.Q<TextField>(), Is.Not.Null);
            Assert.That(GdoInspectorValueElement.ApplyScalar("2").ScalarValue, Is.EqualTo("2"));
        }

        [TestCase("System.Int32", typeof(IntegerField))]
        [TestCase("System.Int64", typeof(LongField))]
        [TestCase("System.Single", typeof(FloatField))]
        [TestCase("System.Double", typeof(DoubleField))]
        [TestCase("System.Boolean", typeof(Toggle))]
        public void ScalarUsesNativeTypedControl(string runtimeType, System.Type expectedControl)
        {
            GdoValueDescriptorData descriptor = new GdoValueDescriptorData("Scalar", runtimeType, null, null,
                new GdoValueMemberDescriptorData[0], new string[0]);

            VisualElement element = GdoInspectorValueElement.Create("Value", descriptor,
                GdoOverlayValue.Scalar("0"), _ => { });

            Assert.That(element.Query<VisualElement>().ToList().Exists(expectedControl.IsInstanceOfType), Is.True);
        }

        [Test]
        public void CollectionsExposeListOperationsButSetsDoNotExposeOrderingOrDuplicate()
        {
            GdoValueDescriptorData child = Descriptor("String");
            GdoValueDescriptorData list = new GdoValueDescriptorData("List", "Fixture.List", null, child,
                new GdoValueMemberDescriptorData[0], new string[0]);
            GdoValueDescriptorData set = new GdoValueDescriptorData("Set", "Fixture.Set", null, child,
                new GdoValueMemberDescriptorData[0], new string[0]);
            GdoOverlayValue populatedList = GdoOverlayValue.List(new[] { GdoOverlayValue.String("a"), GdoOverlayValue.String("b") });
            GdoOverlayValue populatedSet = GdoOverlayValue.Set(new[] { GdoOverlayValue.String("a") });

            VisualElement listElement = GdoInspectorValueElement.Create("List", list, populatedList, _ => { });
            VisualElement setElement = GdoInspectorValueElement.Create("Set", set, populatedSet, _ => { });

            Assert.That(listElement.Q<Button>("move-down"), Is.Not.Null);
            Assert.That(listElement.Q<Button>("duplicate-element"), Is.Not.Null);
            Assert.That(setElement.Q<Button>("move-down"), Is.Null);
            Assert.That(setElement.Q<Button>("duplicate-element"), Is.Null);
        }

        [Test]
        public void LocalizationRowsAreEditableAndRemovable()
        {
            GdoOverlayValue value = GdoOverlayValue.LocalizationMap(new[]
            {
                new GdoLocaleEntry(1, "Name", "Description", "Flavour")
            });

            VisualElement element = GdoInspectorValueElement.Create("Locales", Descriptor("LocalizationMap"), value, _ => { });

            Assert.That(element.Q<IntegerField>("locale-key"), Is.Not.Null);
            Assert.That(element.Q<IntegerField>("locale-key").isDelayed, Is.True);
            Assert.That(element.Q<TextField>("locale-display-name"), Is.Not.Null);
            Assert.That(element.Q<TextField>("locale-description"), Is.Not.Null);
            Assert.That(element.Q<TextField>("locale-flavour-text"), Is.Not.Null);
            Assert.That(element.Q<Button>("remove-locale"), Is.Not.Null);
            Assert.That(element.Q<TextField>("locale-display-name").isDelayed, Is.True);
        }

        [Test]
        public void EnumUsesGeneratedNamesAndPreservesNumericValue()
        {
            GdoValueDescriptorData descriptor = new GdoValueDescriptorData("Enum", "Fixture.Mode", null, null,
                new GdoValueMemberDescriptorData[0], new[] { "First=0", "Second=4" });
            VisualElement element = GdoInspectorValueElement.Create("Mode", descriptor, GdoOverlayValue.Enum(0), _ => { });

            Assert.That(element.Q<PopupField<string>>(), Is.Not.Null);
            Assert.That(GdoInspectorValueElement.ApplyEnum(descriptor, "Second").ScalarValue, Is.EqualTo("4"));
        }

        [Test]
        public void ReferenceShowsExpectedTypeAndVanillaIdControl()
        {
            GdoValueDescriptorData descriptor = new GdoValueDescriptorData("GdoReference", "KitchenData.Item", "KitchenData.Item",
                null, new GdoValueMemberDescriptorData[0], new string[0]);

            VisualElement element = GdoInspectorValueElement.Create("Result", descriptor, GdoOverlayValue.Null(), _ => { });

            GdoReferenceField reference = element.Children().Single().Children()
                .OfType<GdoReferenceField>().SingleOrDefault();
            Assert.That(reference, Is.Not.Null);
            Assert.That(reference.ExpectedTypeName, Is.EqualTo("KitchenData.Item"));
            Assert.That(reference.Picker.ManualContent.Q<IntegerField>("external-gdo-id"), Is.Not.Null);
            Assert.That(reference.Q<Button>("clear-reference"), Is.Not.Null);
        }

        [Test]
        public void ReferenceCanBeClearedAndRejectsZeroVanillaId()
        {
            Assert.That(GdoReferenceField.ClearValue().Kind, Is.EqualTo(GdoOverlayValueKind.Null));
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                GdoReferenceField.ApplyExternal(ExternalGdoCatalogRepository.VanillaProviderKey, 0, "KitchenData.Item"));
            GdoOverlayValue value = GdoReferenceField.ApplyExternal(
                ExternalGdoCatalogRepository.VanillaProviderKey, 42, "KitchenData.Item");
            Assert.That(value.ReferenceValue.Id, Is.EqualTo(42));
            Assert.That(value.ReferenceValue.ExpectedType, Is.EqualTo("KitchenData.Item"));
        }

        [Test]
        public void NestedStructCollectionReferenceBuildsRecursively()
        {
            GdoValueDescriptorData reference = new GdoValueDescriptorData("GdoReference", "KitchenData.Item", "KitchenData.Item", null, new GdoValueMemberDescriptorData[0], new string[0]);
            GdoValueDescriptorData list = new GdoValueDescriptorData("List", "Fixture.List", null, reference, new GdoValueMemberDescriptorData[0], new string[0]);
            GdoValueDescriptorData root = new GdoValueDescriptorData("Struct", "Fixture.Root", null, null,
                new[] { new GdoValueMemberDescriptorData("Items", list) }, new string[0]);

            VisualElement element = GdoInspectorValueElement.Create("Root", root, GdoOverlayValueEditor.CreateDefault(root), _ => { });

            Assert.That(element.Query<Foldout>().ToList(), Has.Count.EqualTo(2));
            Assert.That(element.Q<Button>("add-element"), Is.Not.Null);
        }

        private static GdoValueDescriptorData Descriptor(string kind)
        {
            GdoValueDescriptorData child = new GdoValueDescriptorData("String", "System.String", null, null,
                new GdoValueMemberDescriptorData[0], new string[0]);
            GdoValueMemberDescriptorData[] members = kind == "Struct"
                ? new[] { new GdoValueMemberDescriptorData("Text", child) }
                : new GdoValueMemberDescriptorData[0];
            string[] enums = kind == "Enum" ? new[] { "First=0", "Second=4" } : new string[0];
            return new GdoValueDescriptorData(kind, kind == "Scalar" ? "System.String" : "Fixture." + kind,
                kind == "GdoReference" ? "KitchenData.Item" : null, child, members, enums);
        }

    }
}
