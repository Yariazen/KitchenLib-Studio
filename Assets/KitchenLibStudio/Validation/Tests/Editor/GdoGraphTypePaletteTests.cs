using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class GdoGraphTypePaletteTests
    {
        [Test]
        public void CommonExactTypesReceiveStableDistinctColors()
        {
            string[] types =
            {
                "KitchenData.Item", "KitchenData.ItemGroup", "KitchenData.Process",
                "KitchenData.Dish", "KitchenData.Appliance", "UnityEngine.GameObject",
                "KitchenData.PlayerCosmetic"
            };

            IReadOnlyDictionary<string, Color> first = GdoGraphTypePalette.Build(types);
            IReadOnlyDictionary<string, Color> reversed = GdoGraphTypePalette.Build(types.Reverse());

            Assert.That(first.Values.Distinct().Count(), Is.EqualTo(types.Length));
            foreach (string type in types) Assert.That(reversed[type], Is.EqualTo(first[type]));
            Assert.That(first["KitchenData.ItemGroup"], Is.Not.EqualTo(first["KitchenData.Item"]));
        }

        [Test]
        public void UnknownKnownTypesUseDeterministicCollisionFreeAssignments()
        {
            string[] types = Enumerable.Range(0, 40)
                .Select(index => "Example.Type" + index).ToArray();

            IReadOnlyDictionary<string, Color> colors = GdoGraphTypePalette.Build(types);

            Assert.That(colors.Count, Is.EqualTo(types.Length));
            Assert.That(colors.Values.Distinct().Count(), Is.EqualTo(types.Length));
            Assert.That(GdoGraphTypePalette.Build(types)[types[17]], Is.EqualTo(colors[types[17]]));
        }

        [Test]
        public void BlankAndInvalidTypesUseDedicatedNeutralColors()
        {
            Assert.That(GdoGraphTypePalette.For(null), Is.EqualTo(GdoGraphTypePalette.UnresolvedColor));
            Assert.That(GdoGraphTypePalette.InvalidColor, Is.Not.EqualTo(GdoGraphTypePalette.UnresolvedColor));
        }
    }
}
