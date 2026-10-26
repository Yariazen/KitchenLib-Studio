using System;
using System.Collections.Generic;
using System.Linq;
using KitchenLib.Studio.Editor;
using NUnit.Framework;
using UnityEngine;

namespace KitchenLib.Studio.Tests.Editor
{
    public sealed class GdoOverlayStressTests
    {
        [Test]
        public void LimitsArePinnedAndDepthBoundaryIsPrecise()
        {
            Assert.That(GdoOverlayLimits.MaximumDepth, Is.EqualTo(64));
            Assert.That(GdoOverlayLimits.MaximumNodeCount, Is.EqualTo(10000));
            Assert.That(GdoOverlayLimits.TryValidate(new[] { DeepValue(64) }, out _), Is.True);

            Assert.That(GdoOverlayLimits.TryValidate(new[] { DeepValue(65) }, out GdoOverlayPersistenceDiagnostic issue), Is.False);
            Assert.That(issue.Category, Is.EqualTo("MaximumDepthExceeded"));
            Assert.That(issue.Path, Does.Contain("[0]"));
        }

        [Test]
        public void NodeCountBoundaryIsPrecise()
        {
            GdoOverlayValue accepted = GdoOverlayValue.List(
                Enumerable.Range(0, 9999).Select(index => GdoOverlayValue.String(index.ToString())));
            GdoOverlayValue rejected = GdoOverlayValue.List(
                Enumerable.Range(0, 10000).Select(index => GdoOverlayValue.String(index.ToString())));

            Assert.That(GdoOverlayLimits.TryValidate(new[] { accepted }, out _), Is.True);
            Assert.That(GdoOverlayLimits.TryValidate(new[] { rejected }, out GdoOverlayPersistenceDiagnostic issue), Is.False);
            Assert.That(issue.Category, Is.EqualTo("MaximumNodeCountExceeded"));
        }

        [Test]
        public void FlatGraphRejectsDepthBeforeRecursiveDecodeAndMalformedPayload()
        {
            GdoOverlaySerializedGraph tooDeep = GdoOverlaySerializedGraph.Encode(DeepValue(65));
            Assert.That(tooDeep.TryDecode(out _, out IReadOnlyList<GdoOverlayPersistenceDiagnostic> depthIssues), Is.False);
            Assert.That(depthIssues, Has.Some.Property("Category").EqualTo("MaximumDepthExceeded"));

            string corruptJson = JsonUtility.ToJson(GdoOverlaySerializedGraph.Encode(GdoOverlayValue.String("unexpected")))
                .Replace("\"_kind\":3", "\"_kind\":0");
            GdoOverlaySerializedGraph malformed = JsonUtility.FromJson<GdoOverlaySerializedGraph>(corruptJson);
            Assert.That(malformed.TryDecode(out _, out IReadOnlyList<GdoOverlayPersistenceDiagnostic> payloadIssues), Is.False);
            Assert.That(payloadIssues, Has.Some.Property("Category").EqualTo("InvalidNodePayload"));
        }

        [Test]
        public void NestedSetsAndCompleteLocalizationRemainValid()
        {
            GdoOverlayValue value = GdoOverlayValue.Struct(new[]
            {
                new GdoOverlayMember("Set", GdoOverlayValue.Set(new[]
                {
                    GdoOverlayValue.Set(new[] { GdoOverlayValue.String("one"), GdoOverlayValue.String("two") })
                })),
                new GdoOverlayMember("Locales", GdoOverlayValue.LocalizationMap(new[]
                {
                    new GdoLocaleEntry(1, "Name", "Description", "Flavour")
                }))
            });

            Assert.That(GdoOverlayLimits.TryValidate(new[] { value }, out _), Is.True);
            Assert.That(GdoOverlaySerializedGraph.Encode(value).TryDecode(out GdoOverlayValue restored,
                out IReadOnlyList<GdoOverlayPersistenceDiagnostic> issues), Is.True, string.Join("\n", issues.Select(item => item.Message)));
            GdoLocaleEntry locale = restored.Members.Single(member => member.Name == "Locales").Value.Locales.Single();
            Assert.That(locale.Description, Is.EqualTo("Description"));
            Assert.That(locale.FlavourText, Is.EqualTo("Flavour"));
        }

        [Test]
        public void CompanionRejectsLimitViolationsBeforeTheyReachPersistence()
        {
            GdoOverlayAuthoringAsset companion = ScriptableObject.CreateInstance<GdoOverlayAuthoringAsset>();
            try
            {
                InvalidOperationException depth = Assert.Throws<InvalidOperationException>(() =>
                    companion.UpsertField("KitchenData.Test", "Deep", DeepValue(65)));
                Assert.That(depth.Message, Does.Contain("MaximumDepthExceeded"));

                GdoOverlayValue wide = GdoOverlayValue.List(
                    Enumerable.Range(0, 10000).Select(index => GdoOverlayValue.String(index.ToString())));
                InvalidOperationException count = Assert.Throws<InvalidOperationException>(() =>
                    companion.UpsertField("KitchenData.Test", "Wide", wide));
                Assert.That(count.Message, Does.Contain("MaximumNodeCountExceeded"));
                Assert.That(companion.Fields, Is.Empty);
            }
            finally { UnityEngine.Object.DestroyImmediate(companion); }
        }

        private static GdoOverlayValue DeepValue(int depth)
        {
            GdoOverlayValue value = GdoOverlayValue.String("leaf");
            for (int index = 0; index < depth; index++) value = GdoOverlayValue.List(new[] { value });
            return value;
        }
    }
}
