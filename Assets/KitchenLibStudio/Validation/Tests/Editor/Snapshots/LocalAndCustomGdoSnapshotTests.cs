using System;
using System.Linq;
using System.Threading;
using KitchenData;
using NUnit.Framework;
using UnityEngine;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class LocalAndCustomGdoSnapshotTests
    {
        [Test]
        public void LocalRenderingDoesNotMutateTheSourceAndUsesSharedRenderer()
        {
            GameObject source = GameObject.CreatePrimitive(PrimitiveType.Cube);
            source.name = "Local Source"; source.transform.position = new Vector3(12, 4, -3);
            int sourceId = source.GetInstanceID(); Vector3 position = source.transform.position;
            try
            {
                SnapshotSubject subject = SnapshotSubject.LocalGameObject(source);
                SnapshotRenderResult result = new GameObjectSnapshotService().RenderAsync(subject,
                    new SnapshotRenderOptions(96, 96), CancellationToken.None).GetAwaiter().GetResult();
                Assert.That(result.PngBytes, Is.Not.Empty);
                Assert.That(result.Width, Is.EqualTo(96));
                Assert.That(source.GetInstanceID(), Is.EqualTo(sourceId));
                Assert.That(source.transform.position, Is.EqualTo(position));
                Assert.That(source.name, Is.EqualTo("Local Source"));
                Assert.That(subject.CanUsePersistentCache, Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }

        [Test]
        public void SinglePrefabCandidateResolvesAutomatically()
        {
            Item item = ScriptableObject.CreateInstance<Item>();
            GameObject prefab = GameObject.CreatePrimitive(PrimitiveType.Sphere); item.Prefab = prefab;
            try
            {
                var resolver = new CustomGdoSnapshotResolver();
                Assert.That(resolver.Candidates(item).Select(value => value.FieldPath), Is.EqualTo(new[] { "Prefab" }));
                SnapshotResolution resolution = resolver.Resolve(item, null);
                Assert.That(resolution.Succeeded, Is.True);
                Assert.That(resolution.GameObject, Is.SameAs(prefab));
                Assert.That(resolution.FieldPath, Is.EqualTo("Prefab"));
            }
            finally { UnityEngine.Object.DestroyImmediate(item); UnityEngine.Object.DestroyImmediate(prefab); }
        }

        [Test]
        public void MultipleCandidatesRequireSelectionAndNamedFieldsAreDiscoverable()
        {
            Dish dish = ScriptableObject.CreateInstance<Dish>();
            GameObject icon = new GameObject("Icon"); GameObject display = new GameObject("Display");
            dish.IconPrefab = icon; dish.DisplayPrefab = display;
            try
            {
                var resolver = new CustomGdoSnapshotResolver();
                Assert.That(resolver.Candidates(dish).Select(value => value.FieldPath),
                    Is.EqualTo(new[] { "DisplayPrefab", "IconPrefab" }));
                SnapshotResolution ambiguous = resolver.Resolve(dish, null);
                Assert.That(ambiguous.Succeeded, Is.False);
                Assert.That(ambiguous.RequiresSelection, Is.True);
                Assert.That(ambiguous.Diagnostic, Does.Contain("Multiple"));
                Assert.That(resolver.Resolve(dish, "IconPrefab").GameObject, Is.SameAs(icon));
            }
            finally { UnityEngine.Object.DestroyImmediate(dish); UnityEngine.Object.DestroyImmediate(icon); UnityEngine.Object.DestroyImmediate(display); }
        }

        [Test]
        public void MissingCandidateReturnsDiagnosticAndCustomSubjectRendersSelectedPrefab()
        {
            Appliance appliance = ScriptableObject.CreateInstance<Appliance>();
            try
            {
                var resolver = new CustomGdoSnapshotResolver();
                SnapshotResolution missing = resolver.Resolve(appliance, null);
                Assert.That(missing.Succeeded, Is.False);
                Assert.That(missing.RequiresSelection, Is.False);
                Assert.That(missing.Diagnostic, Does.Contain("no populated").IgnoreCase);

                appliance.Prefab = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                SnapshotRenderResult result = new GameObjectSnapshotService().RenderAsync(
                    SnapshotSubject.CustomGdo(appliance, "Prefab"), new SnapshotRenderOptions(96, 96),
                    CancellationToken.None).GetAwaiter().GetResult();
                Assert.That(result.VisiblePixelCount, Is.GreaterThan(20));
            }
            finally
            {
                if (appliance.Prefab != null) UnityEngine.Object.DestroyImmediate(appliance.Prefab);
                UnityEngine.Object.DestroyImmediate(appliance);
            }
        }

        [Test]
        public void FailedLocalRenderDoesNotLeakTheClone()
        {
            var source = new GameObject("No Renderer");
            int before = Resources.FindObjectsOfTypeAll<GameObject>().Count(value => value.name == "No Renderer");
            try
            {
                Assert.Throws<InvalidOperationException>(() => new GameObjectSnapshotService().RenderAsync(
                    SnapshotSubject.LocalGameObject(source), new SnapshotRenderOptions(64, 64), CancellationToken.None));
                Assert.That(Resources.FindObjectsOfTypeAll<GameObject>().Count(value => value.name == "No Renderer"), Is.EqualTo(before));
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }
    }
}
