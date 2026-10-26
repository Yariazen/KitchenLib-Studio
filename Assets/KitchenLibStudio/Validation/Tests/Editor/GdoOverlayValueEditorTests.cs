using System;
using System.Linq;
using KitchenLib.Studio.Generated;
using NUnit.Framework;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class GdoOverlayValueEditorTests
    {
        [Test]
        public void CreateDefaultBuildsNestedStructCollectionReferenceWithoutThrowing()
        {
            GdoValueDescriptorData reference = Value("GdoReference", "KitchenData.Item", "KitchenData.Item");
            GdoValueDescriptorData list = Value("List", "System.Collections.Generic.List`1<KitchenData.Item>", element: reference);
            GdoValueDescriptorData root = Value("Struct", "Fixture.Root", members: new[]
            {
                new GdoValueMemberDescriptorData("Items", list)
            });

            GdoOverlayValue value = GdoOverlayValueEditor.CreateDefault(root);

            Assert.That(value.Kind, Is.EqualTo(GdoOverlayValueKind.Struct));
            Assert.That(value.Members.Single().Name, Is.EqualTo("Items"));
            Assert.That(value.Members.Single().Value.Kind, Is.EqualTo(GdoOverlayValueKind.List));
            Assert.That(value.Members.Single().Value.Values, Is.Empty);
        }

        [TestCase("Scalar", GdoOverlayValueKind.Scalar)]
        [TestCase("String", GdoOverlayValueKind.String)]
        [TestCase("Enum", GdoOverlayValueKind.Enum)]
        [TestCase("GdoReference", GdoOverlayValueKind.Null)]
        [TestCase("List", GdoOverlayValueKind.List)]
        [TestCase("Set", GdoOverlayValueKind.Set)]
        [TestCase("LocalizationMap", GdoOverlayValueKind.LocalizationMap)]
        [TestCase("Nullable", GdoOverlayValueKind.Null)]
        [TestCase("UnityObject", GdoOverlayValueKind.Null)]
        public void CreateDefaultUsesDescriptorKind(string kind, GdoOverlayValueKind expected)
        {
            Assert.That(GdoOverlayValueEditor.CreateDefault(Value(kind, "Fixture.Value", element: Value("String", "System.String"))).Kind,
                Is.EqualTo(expected));
        }

        [Test]
        public void ReplaceStructMemberPreservesOtherMembers()
        {
            GdoOverlayValue value = GdoOverlayValue.Struct(new[]
            {
                new GdoOverlayMember("A", GdoOverlayValue.String("old")),
                new GdoOverlayMember("B", GdoOverlayValue.Scalar("2"))
            });

            GdoOverlayValue changed = GdoOverlayValueEditor.ReplaceStructMember(value, "A", GdoOverlayValue.String("new"));

            Assert.That(changed.Members.Single(member => member.Name == "A").Value.ScalarValue, Is.EqualTo("new"));
            Assert.That(changed.Members.Single(member => member.Name == "B").Value.ScalarValue, Is.EqualTo("2"));
            Assert.That(value.Members.Single(member => member.Name == "A").Value.ScalarValue, Is.EqualTo("old"));
        }

        [Test]
        public void CollectionOperationsPreserveListOrderAndRejectSetDuplicates()
        {
            GdoOverlayValue list = GdoOverlayValue.List(new[] { GdoOverlayValue.String("a"), GdoOverlayValue.String("c") });
            GdoOverlayValue inserted = GdoOverlayValueEditor.InsertCollectionElement(list, 1, GdoOverlayValue.String("b"));
            GdoOverlayValue removed = GdoOverlayValueEditor.RemoveCollectionElement(inserted, 0);

            Assert.That(inserted.Values.Select(item => item.ScalarValue), Is.EqualTo(new[] { "a", "b", "c" }));
            Assert.That(removed.Values.Select(item => item.ScalarValue), Is.EqualTo(new[] { "b", "c" }));
            Assert.Throws<ArgumentException>(() => GdoOverlayValueEditor.InsertCollectionElement(
                GdoOverlayValue.Set(new[] { GdoOverlayValue.String("a") }), 1, GdoOverlayValue.String("a")));
        }

        [Test]
        public void CollectionMoveAndDuplicateAreCopyOnWrite()
        {
            GdoOverlayValue value = GdoOverlayValue.List(new[]
            {
                GdoOverlayValue.String("a"), GdoOverlayValue.String("b"), GdoOverlayValue.String("c")
            });

            GdoOverlayValue moved = GdoOverlayValueEditor.MoveCollectionElement(value, 0, 2);
            GdoOverlayValue duplicated = GdoOverlayValueEditor.DuplicateCollectionElement(value, 1);

            Assert.That(moved.Values.Select(item => item.ScalarValue), Is.EqualTo(new[] { "b", "c", "a" }));
            Assert.That(duplicated.Values.Select(item => item.ScalarValue), Is.EqualTo(new[] { "a", "b", "b", "c" }));
            Assert.That(value.Values.Select(item => item.ScalarValue), Is.EqualTo(new[] { "a", "b", "c" }));
            Assert.Throws<ArgumentException>(() => GdoOverlayValueEditor.DuplicateCollectionElement(
                GdoOverlayValue.Set(new[] { GdoOverlayValue.String("a") }), 0));
        }

        [Test]
        public void UpsertLocaleReplacesDuplicateLocaleRatherThanAddingIt()
        {
            GdoOverlayValue value = GdoOverlayValue.LocalizationMap(new[] { new GdoLocaleEntry(1, "Old") });

            GdoOverlayValue changed = GdoOverlayValueEditor.UpsertLocale(value, new GdoLocaleEntry(1, "New"));

            Assert.That(changed.Locales, Has.Count.EqualTo(1));
            Assert.That(changed.Locales[0].DisplayName, Is.EqualTo("New"));
        }

        [Test]
        public void RemoveLocalePreservesOtherRows()
        {
            GdoOverlayValue value = GdoOverlayValue.LocalizationMap(new[]
            {
                new GdoLocaleEntry(1, "One"), new GdoLocaleEntry(2, "Two")
            });

            GdoOverlayValue changed = GdoOverlayValueEditor.RemoveLocale(value, 1);

            Assert.That(changed.Locales.Select(item => item.Locale), Is.EqualTo(new[] { 2 }));
            Assert.That(value.Locales, Has.Count.EqualTo(2));
        }

        [Test]
        public void RenameLocaleRejectsCollisionWithoutChangingSource()
        {
            GdoOverlayValue value = GdoOverlayValue.LocalizationMap(new[]
            {
                new GdoLocaleEntry(1, "One"), new GdoLocaleEntry(2, "Two")
            });

            Assert.Throws<ArgumentException>(() => GdoOverlayValueEditor.RenameLocale(value, 1, 2));
            Assert.That(value.Locales.Select(item => item.DisplayName), Is.EqualTo(new[] { "One", "Two" }));
        }

        [Test]
        public void NormalizeReturnsDefaultForMalformedRootAndPreservesCompatibleValue()
        {
            GdoValueDescriptorData list = Value("List", "Fixture.List", element: Value("String", "System.String"));
            GdoOverlayValue valid = GdoOverlayValue.List(new[] { GdoOverlayValue.String("kept") });

            Assert.That(GdoOverlayValueEditor.Normalize(list, GdoOverlayValue.String("wrong")).Kind,
                Is.EqualTo(GdoOverlayValueKind.List));
            Assert.That(GdoOverlayValueEditor.Normalize(list, valid), Is.SameAs(valid));
        }

        [Test]
        public void NormalizeRepairsMalformedScalarAndMissingStructMemberCanBeAdded()
        {
            GdoValueDescriptorData integer = Value("Scalar", "System.Int32");
            GdoOverlayValue empty = GdoOverlayValue.Struct(new GdoOverlayMember[0]);

            Assert.That(GdoOverlayValueEditor.Normalize(integer, GdoOverlayValue.Scalar("not-a-number")).ScalarValue,
                Is.EqualTo("0"));
            Assert.That(GdoOverlayValueEditor.ReplaceStructMember(empty, "Added", GdoOverlayValue.String("value"))
                .Members.Single().Name, Is.EqualTo("Added"));
        }

        [Test]
        public void UnsupportedDefaultNamesKindAndRuntimeType()
        {
            NotSupportedException exception = Assert.Throws<NotSupportedException>(() =>
                GdoOverlayValueEditor.CreateDefault(Value("Unsupported", "Fixture.Dictionary")));

            Assert.That(exception.Message, Does.Contain("Unsupported"));
            Assert.That(exception.Message, Does.Contain("Fixture.Dictionary"));
        }

        private static GdoValueDescriptorData Value(string kind, string runtimeType, string expected = null,
            GdoValueDescriptorData element = null, GdoValueMemberDescriptorData[] members = null) =>
            new GdoValueDescriptorData(kind, runtimeType, expected, element, members ?? new GdoValueMemberDescriptorData[0], new string[0]);
    }
}
