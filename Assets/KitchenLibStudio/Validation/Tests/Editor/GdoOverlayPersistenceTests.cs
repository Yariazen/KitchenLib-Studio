using System;
using System.Collections.Generic;
using System.Linq;
using KitchenLib.Studio.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace KitchenLib.Studio.Tests.Editor
{
    public sealed class GdoOverlayPersistenceTests
    {
        private const string Root = "Assets/__KitchenLibStudioPersistenceTests";

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(Root);
            AssetDatabase.CreateFolder("Assets", "__KitchenLibStudioPersistenceTests");
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(Root);
        }

        [Test]
        public void FlatGraphRoundTripsDepthSixtyFourDeterministically()
        {
            GdoOverlayValue source = DeepValue(64);

            GdoOverlaySerializedGraph first = GdoOverlaySerializedGraph.Encode(source);
            GdoOverlaySerializedGraph second = GdoOverlaySerializedGraph.Encode(source);

            Assert.That(first.TryDecode(out GdoOverlayValue restored, out IReadOnlyList<GdoOverlayPersistenceDiagnostic> issues), Is.True,
                string.Join("\n", issues.Select(issue => issue.Message)));
            Assert.That(Depth(restored), Is.EqualTo(64));
            Assert.That(JsonUtility.ToJson(first), Is.EqualTo(JsonUtility.ToJson(second)));
            Assert.That(first.Nodes, Has.Count.EqualTo(65));
        }

        [Test]
        public void FlatGraphPreservesReferencesAndCompleteLocales()
        {
            var target = ScriptableObject.CreateInstance<KitchenData.Item>();
            target.ID = -71001;
            try
            {
                GdoOverlayValue source = GdoOverlayValue.Struct(new[]
                {
                    new GdoOverlayMember("Reference", GdoOverlayValue.Reference(GdoReferenceToken.ForAsset(target, "KitchenData.Item"))),
                    new GdoOverlayMember("Locales", GdoOverlayValue.LocalizationMap(new[]
                    {
                        new GdoLocaleEntry(2, "Nom", "Description", "Saveur")
                    }))
                });

                GdoOverlaySerializedGraph graph = GdoOverlaySerializedGraph.Encode(source);
                Assert.That(graph.TryDecode(out GdoOverlayValue restored, out IReadOnlyList<GdoOverlayPersistenceDiagnostic> issues), Is.True,
                    string.Join("\n", issues.Select(issue => issue.Message)));
                Assert.That(restored.Members.Single(member => member.Name == "Reference").Value.ReferenceValue.Asset, Is.SameAs(target));
                GdoLocaleEntry locale = restored.Members.Single(member => member.Name == "Locales").Value.Locales.Single();
                Assert.That(new[] { locale.Locale.ToString(), locale.DisplayName, locale.Description, locale.FlavourText },
                    Is.EqualTo(new[] { "2", "Nom", "Description", "Saveur" }));
            }
            finally { UnityEngine.Object.DestroyImmediate(target); }
        }

        [Test]
        public void MissingChildDuplicateIdAndCycleReturnPreciseDiagnostics()
        {
            AssertMalformed(
                GdoOverlaySerializedGraph.CreateForTesting(0, new[] { GdoOverlaySerializedNode.Null(0) }),
                "InvalidNodeId", "positive");

            AssertMalformed(
                GdoOverlaySerializedGraph.CreateForTesting(1, new[]
                {
                    GdoOverlaySerializedNode.Collection(1, GdoOverlayValueKind.List, new[] { 99 })
                }),
                "MissingNode", "99");

            AssertMalformed(
                GdoOverlaySerializedGraph.CreateForTesting(1, new[]
                {
                    GdoOverlaySerializedNode.Null(1),
                    GdoOverlaySerializedNode.Null(1)
                }),
                "DuplicateNodeId", "1");

            AssertMalformed(
                GdoOverlaySerializedGraph.CreateForTesting(1, new[]
                {
                    GdoOverlaySerializedNode.Collection(1, GdoOverlayValueKind.List, new[] { 2 }),
                    GdoOverlaySerializedNode.Collection(2, GdoOverlayValueKind.List, new[] { 1 })
                }),
                "Cycle", "1");
        }

        [Test]
        public void MissingCurrentFormatPayloadFlagsAreRejected()
        {
            GdoOverlaySerializedNode obsolete = JsonUtility.FromJson<GdoOverlaySerializedNode>(
                "{\"_id\":1,\"_kind\":3,\"_scalar\":\"obsolete\"}");

            AssertMalformed(GdoOverlaySerializedGraph.CreateForTesting(1, new[] { obsolete }),
                "InvalidNodePayload", "does not match");
        }

        [Test]
        public void CompanionSurvivesUnityAssetSaveAndReloadWithoutRecursivePayload()
        {
            var target = ScriptableObject.CreateInstance<KitchenData.GameDifficultySettings>();
            target.ID = -71002;
            AssetDatabase.CreateAsset(target, Root + "/Target.asset");
            var companion = ScriptableObject.CreateInstance<GdoOverlayAuthoringAsset>();
            companion.Initialise(target);
            companion.SetField("KitchenData.GameDifficultySettings", "Deep", DeepValue(64));
            string path = Root + "/Companion.asset";
            AssetDatabase.CreateAsset(companion, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            GdoOverlayAuthoringAsset restored = AssetDatabase.LoadAssetAtPath<GdoOverlayAuthoringAsset>(path);

            Assert.That(restored.TryGetField("KitchenData.GameDifficultySettings", "Deep", out GdoOverlayField field), Is.True);
            Assert.That(Depth(field.Value), Is.EqualTo(64));
            string yaml = System.IO.File.ReadAllText(path);
            Assert.That(yaml, Does.Contain("_serializedFields:"));
            Assert.That(yaml, Does.Not.Contain("_value:\n      _kind:"));
        }

        [Test]
        public void RepositoryDoesNotInterpretObsoleteRecursiveYaml()
        {
            var target = ScriptableObject.CreateInstance<KitchenData.GameDifficultySettings>();
            target.ID = -71003;
            AssetDatabase.CreateAsset(target, Root + "/LegacyTarget.asset");
            GdoOverlayAuthoringAsset initial = GdoOverlayAuthoringRepository.GetOrCreate(target);
            string companionPath = AssetDatabase.GetAssetPath(initial);
            string targetGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(target));
            string scriptGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(initial)));
            string yaml = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n" +
                "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n" +
                "  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n" +
                "  m_Script: {fileID: 11500000, guid: " + scriptGuid + ", type: 3}\n  m_Name: Legacy\n  m_EditorClassIdentifier: \n" +
                "  _target: {fileID: 11400000, guid: " + targetGuid + ", type: 2}\n  _descriptorFingerprint: legacy\n" +
                "  _fields:\n  - _declaringType: KitchenData.GameDifficultySettings\n    _fieldName: LegacyValue\n    _value:\n" +
                "      _kind: 6\n      _scalar: \n      _reference:\n        _asset: {fileID: 0}\n        _id: 0\n        _expectedType: \n" +
                "      _members: []\n      _values:\n      - _kind: 3\n        _scalar: migrated\n        _reference:\n" +
                "          _asset: {fileID: 0}\n          _id: 0\n          _expectedType: \n        _members: []\n        _values: []\n        _locales: []\n" +
                "      _locales: []\n";
            System.IO.File.WriteAllText(companionPath, yaml);
            AssetDatabase.ImportAsset(companionPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            GdoOverlayAuthoringAsset loaded = GdoOverlayAuthoringRepository.GetOrCreate(target);

            Assert.That(loaded.TryGetField("KitchenData.GameDifficultySettings", "LegacyValue", out _), Is.False);
            Assert.That(System.IO.File.ReadAllText(companionPath), Does.Contain("\n  _fields:"));
            AssetDatabase.DeleteAsset(companionPath);
        }

        private static void AssertMalformed(GdoOverlaySerializedGraph graph, string category, string messagePart)
        {
            Assert.That(graph.TryDecode(out _, out IReadOnlyList<GdoOverlayPersistenceDiagnostic> issues), Is.False);
            Assert.That(issues, Has.Some.Matches<GdoOverlayPersistenceDiagnostic>(issue =>
                issue.Category == category && issue.Message.Contains(messagePart)));
            Assert.That(graph.Nodes, Is.Not.Empty, "Malformed source nodes must remain available for recovery.");
        }

        private static GdoOverlayValue DeepValue(int depth)
        {
            GdoOverlayValue value = GdoOverlayValue.String("leaf");
            for (int index = 0; index < depth; index++) value = GdoOverlayValue.List(new[] { value });
            return value;
        }

        private static int Depth(GdoOverlayValue value)
        {
            int depth = 0;
            while (value.Kind == GdoOverlayValueKind.List)
            {
                depth++;
                value = value.Values.Single();
            }
            Assert.That(value.ScalarValue, Is.EqualTo("leaf"));
            return depth;
        }
    }
}
