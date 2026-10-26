using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class GdoGraphPrefabRepositoryTests
    {
        private GdoBundleGraphAsset _graph;

        [SetUp]
        public void SetUp() => _graph = ScriptableObject.CreateInstance<GdoBundleGraphAsset>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_graph);

        [Test]
        public void PrefabRecordsRoundTripBothSourceModes()
        {
            GdoGraphPrefabRecord record = GdoBundleGraphRepository.AddPrefab(
                _graph, "Test Cube", new Rect(10, 20, 240, 180));

            Assert.That(record.SourceMode, Is.EqualTo(GdoGraphPrefabSourceMode.Primitive));
            Assert.That(record.PrimitiveType, Is.EqualTo(PrimitiveType.Cube));
            Assert.That(record.Scale, Is.EqualTo(Vector3.one));
            Assert.That(record.IncludeCollider, Is.True);

            GdoBundleGraphRepository.ConfigurePrimitive(_graph, record.Id, PrimitiveType.Sphere,
                new Vector3(2, 3, 4), null, false);

            Assert.That(_graph.Prefabs.Single().PrimitiveType, Is.EqualTo(PrimitiveType.Sphere));
            Assert.That(_graph.Prefabs.Single().Scale, Is.EqualTo(new Vector3(2, 3, 4)));
            Assert.That(_graph.Prefabs.Single().IncludeCollider, Is.False);
        }

        [Test]
        public void PrefabIsIncludedInElementMovementAndConnectionLookup()
        {
            GdoGraphPrefabRecord record = GdoBundleGraphRepository.AddPrefab(
                _graph, "Test Cube", new Rect(10, 20, 240, 180));

            GdoBundleGraphRepository.SetElementPositions(_graph,
                new Dictionary<string, Rect> { [record.Id] = new Rect(30, 40, 240, 180) });

            Assert.That(_graph.Prefabs.Single(value => value.Id == record.Id).Position.position,
                Is.EqualTo(new Vector2(30, 40)));
            Assert.DoesNotThrow(() => GdoBundleGraphRepository.AddEdge(
                _graph, record.Id, "Value", record.Id, "Test", 0));
            Assert.That(_graph.Edges.Any(edge => edge.OutputNodeId == record.Id), Is.True);
        }

        [Test]
        public void DeletingPrefabRemovesEdgesButPreservesSourceAssetReference()
        {
            GameObject source = new GameObject("Workspace Prefab");
            try
            {
                GdoGraphPrefabRecord record = GdoBundleGraphRepository.AddPrefab(
                    _graph, "Workspace", new Rect());
                GdoBundleGraphRepository.ConfigureWorkspacePrefab(_graph, record.Id, source, "guid");
                GdoBundleGraphRepository.AddEdge(_graph, record.Id, "Value", record.Id, "Test", 0);

                GdoBundleGraphRepository.RemovePrefab(_graph, record.Id, true);

                Assert.That(_graph.Prefabs, Is.Empty);
                Assert.That(_graph.Edges, Is.Empty);
                Assert.That(source, Is.Not.Null);
            }
            finally { Object.DestroyImmediate(source); }
        }

        [Test]
        public void SwitchingModesPreservesBothSourceConfigurations()
        {
            GameObject source = new GameObject("Workspace Prefab");
            try
            {
                GdoGraphPrefabRecord record = GdoBundleGraphRepository.AddPrefab(_graph, "Choice", new Rect());
                GdoBundleGraphRepository.ConfigurePrimitive(_graph, record.Id, PrimitiveType.Capsule,
                    new Vector3(2, 2, 2), null, false);
                GdoBundleGraphRepository.ConfigureWorkspacePrefab(_graph, record.Id, source, "guid");
                GdoBundleGraphRepository.SetPrefabSourceMode(_graph, record.Id, GdoGraphPrefabSourceMode.Primitive);

                Assert.That(record.PrimitiveType, Is.EqualTo(PrimitiveType.Capsule));
                Assert.That(record.WorkspacePrefab, Is.SameAs(source));
                Assert.That(record.WorkspacePrefabGuid, Is.EqualTo("guid"));
            }
            finally { Object.DestroyImmediate(source); }
        }
    }
}
