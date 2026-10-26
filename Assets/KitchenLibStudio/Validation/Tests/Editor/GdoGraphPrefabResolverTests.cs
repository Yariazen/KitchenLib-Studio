using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class GdoGraphPrefabResolverTests
    {
        private const string Root = "Assets/Prefab Resolver Tests";

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(Root);
            AssetDatabase.CreateFolder("Assets", "Prefab Resolver Tests");
        }

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(Root);

        [Test]
        public void PrimitiveResolutionCreatesAndUpdatesOneStablePrefab()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "Prefab Project");
            GdoGraphPrefabRecord record = GdoBundleGraphRepository.AddPrefab(graph, "Test Cube", new Rect());
            GdoBundleGraphRepository.ConfigurePrimitive(graph, record.Id, PrimitiveType.Cube,
                new Vector3(2, 3, 4), null, false);

            GameObject first = GdoGraphPrefabResolver.Resolve(graph, record);
            string path = AssetDatabase.GetAssetPath(first);
            GdoBundleGraphRepository.ConfigurePrimitive(graph, record.Id, PrimitiveType.Cube,
                new Vector3(5, 6, 7), null, true);
            GameObject second = GdoGraphPrefabResolver.Resolve(graph, record);

            Assert.That(path, Is.EqualTo(GdoGraphPrefabResolver.PrimitiveAssetPath(graph, record)));
            Assert.That(AssetDatabase.GetAssetPath(second), Is.EqualTo(path));
            Assert.That(second.transform.localScale, Is.EqualTo(new Vector3(5, 6, 7)));
            Assert.That(second.GetComponent<Collider>(), Is.Not.Null);
        }

        [Test]
        public void WorkspaceResolutionUsesGuidAfterAssetMoves()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "Prefab Project");
            GameObject temporary = GameObject.CreatePrimitive(PrimitiveType.Cube);
            string original = Root + "/Workspace.prefab";
            GameObject source = PrefabUtility.SaveAsPrefabAsset(temporary, original);
            Object.DestroyImmediate(temporary);
            GdoGraphPrefabRecord record = GdoBundleGraphRepository.AddPrefab(graph, "Workspace", new Rect());
            string guid = AssetDatabase.AssetPathToGUID(original);
            GdoBundleGraphRepository.ConfigureWorkspacePrefab(graph, record.Id, source, guid);
            string moved = Root + "/Moved.prefab";
            Assert.That(AssetDatabase.MoveAsset(original, moved), Is.Empty);

            GameObject resolved = GdoGraphPrefabResolver.Resolve(graph, record);

            Assert.That(AssetDatabase.GetAssetPath(resolved), Is.EqualTo(moved));
        }

        [Test]
        public void WorkspaceValidationRejectsEditorAndGeneratedSources()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "Prefab Project");
            AssetDatabase.CreateFolder(Root, "Editor");
            GameObject temporary = GameObject.CreatePrimitive(PrimitiveType.Cube);
            string path = Root + "/Editor/Bad.prefab";
            GameObject source = PrefabUtility.SaveAsPrefabAsset(temporary, path);
            Object.DestroyImmediate(temporary);
            GdoGraphPrefabRecord record = GdoBundleGraphRepository.AddPrefab(graph, "Bad", new Rect());
            GdoBundleGraphRepository.ConfigureWorkspacePrefab(graph, record.Id, source,
                AssetDatabase.AssetPathToGUID(path));

            Assert.That(GdoGraphPrefabResolver.Validate(graph, record)
                .Any(issue => issue.Severity >= ValidationSeverity.Error), Is.True);
        }
    }
}
