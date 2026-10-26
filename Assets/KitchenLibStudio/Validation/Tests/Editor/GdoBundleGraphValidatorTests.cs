using System.Linq;
using KitchenData;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class GdoBundleGraphValidatorTests
    {
        private const string Root = "Assets/GraphValidatorTests";

        [SetUp]
        public void SetUp() { AssetDatabase.DeleteAsset(Root); AssetDatabase.CreateFolder("Assets", "GraphValidatorTests"); }
        [TearDown]
        public void TearDown() { AssetDatabase.DeleteAsset(Root); Undo.ClearAll(); }

        [Test]
        public void ReportsInvalidProjectLayoutWithStableGraphElementAndBlockingSeverity()
        {
            GdoBundleGraphAsset graph = GdoBundleGraphRepository.Create(Root + "/Wrong.asset", "Test Author", "First", "wrong.assets");

            GdoGraphDiagnostic[] issues = GdoBundleGraphValidator.Validate(graph).ToArray();

            Assert.That(issues.Count(issue => issue.Category == "InvalidProjectLayout"), Is.EqualTo(4));
            Assert.That(issues.Where(issue => issue.Category == "InvalidProjectLayout").Select(issue => issue.AssetPath),
                Is.EquivalentTo(new[] { Root + "/Wrong.asset", Root + "/Wrong.asset", Root + "/GDOs", Root + "/Content" }));
            Assert.That(issues.Where(issue => issue.Category == "InvalidProjectLayout")
                .All(issue => issue.ElementId == string.Empty && issue.Severity == ValidationSeverity.Error), Is.True);
            Assert.That(GdoGraphProjectionApplier.TryApply(graph, out _), Is.False);
            AssertDeterministic(graph, issues);
        }

        [Test]
        public void ReportsDuplicateLocalObjectNamesIgnoringCaseAndFileNames()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "First");
            Item first = Create<Item>("FirstFile", 11);
            Item second = Create<Item>("SecondFile", 12);
            first.name = "Bread";
            second.name = "bReAd";
            GdoGraphNodeRecord firstNode = GdoBundleGraphRepository.AddMember(graph, first, new Rect());
            GdoGraphNodeRecord secondNode = GdoBundleGraphRepository.AddMember(graph, second, new Rect());

            GdoGraphDiagnostic[] issues = GdoBundleGraphValidator.Validate(graph).ToArray();

            AssertBlockingNodes(issues, "DuplicateLocalName", firstNode, secondNode);
            AssertDeterministic(graph, issues);
        }

        [Test]
        public void RenamedObjectDoesNotInvalidateItsStoredId()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "First");
            Item asset = Create<Item>("OldName", -1878647625);
            GdoBundleGraphRepository.AddMember(graph, asset, new Rect());
            asset.name = "CurrentName";

            GdoGraphDiagnostic[] issues = GdoBundleGraphValidator.Validate(graph).ToArray();

            Assert.That(issues.Any(issue => issue.Category == "StaleCalculatedId"), Is.False);
            Assert.That(asset.ID, Is.EqualTo(-1878647625), "Validation must not repair IDs implicitly.");
        }

        [Test]
        public void HashCollisionDoesNotMatterWhenStoredIdsDiffer()
        {
            // Precomputed v1 FNV-1a fixtures: first:collision 8etgw9 and first:collision d972fz.
            Assert.That(GdoProjectIdentity.RuntimeId("First", "collision 8etgw9"), Is.EqualTo(-1757914603));
            Assert.That(GdoProjectIdentity.RuntimeId("First", "collision d972fz"), Is.EqualTo(-1757914603));
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "First");
            GdoGraphNodeRecord first = GdoBundleGraphRepository.AddMember(graph, Create<Item>("collision 8etgw9", 11), new Rect());
            GdoGraphNodeRecord second = GdoBundleGraphRepository.AddMember(graph, Create<Item>("collision d972fz", 12), new Rect());

            GdoGraphDiagnostic[] issues = GdoBundleGraphValidator.Validate(graph).ToArray();

            Assert.That(issues.Any(issue => issue.Category == "CalculatedIdCollision"), Is.False);
            Assert.That(issues.Any(issue => issue.Category == "DuplicateLocalName"), Is.False);
            AssertDeterministic(graph, issues);
        }

        [Test]
        public void ReportsCalculatedLocalCollisionWithKnownExternalId()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "First");
            GdoGraphNodeRecord local = GdoBundleGraphRepository.AddMember(graph, Create<Item>("collision 8etgw9", 11), new Rect());
            GdoGraphNodeRecord external = GdoBundleGraphRepository.AddExternal(graph, ExternalGdoCatalogRepository.VanillaProviderKey, 11, "KitchenData.Item", new Rect());

            GdoGraphDiagnostic[] issues = GdoBundleGraphValidator.Validate(graph).ToArray();

            AssertBlockingNodes(issues, "CalculatedIdCollision", local, external);
            AssertDeterministic(graph, issues);
        }

        [Test]
        public void InvalidNamesProduceBlockingDiagnosticsInsteadOfThrowing()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "First");
            Item asset = Create<Item>("ValidFile", 11);
            GdoGraphNodeRecord node = GdoBundleGraphRepository.AddMember(graph, asset, new Rect());
            asset.name = "Bad:Name";

            GdoGraphDiagnostic[] issues = GdoBundleGraphValidator.Validate(graph).ToArray();

            AssertBlockingNodes(issues, "InvalidProjectLayout", node);
        }

        [Test]
        public void ValidCalculatedIdentitiesIgnoreUnrelatedAssetsAndExternalOnlyAliases()
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(Root, "Test Author", "First");
            graph.name = "A display name is not the namespace";
            GdoBundleGraphRepository.AddMember(graph, Create<Item>("collision 8etgw9", -1757914603), new Rect());
            Create<Item>("Unrelated", -1757914603);
            GdoBundleGraphRepository.AddExternal(graph, ExternalGdoCatalogRepository.VanillaProviderKey, 33, "KitchenData.Item", new Rect());
            GdoBundleGraphRepository.AddExternal(graph, ExternalGdoCatalogRepository.VanillaProviderKey, 33, "KitchenData.Appliance", new Rect());

            GdoGraphDiagnostic[] issues = GdoBundleGraphValidator.Validate(graph).ToArray();

            Assert.That(issues.Where(issue => new[] { "InvalidProjectLayout", "DuplicateLocalName", "StaleCalculatedId",
                "CalculatedIdCollision" }.Contains(issue.Category)), Is.Empty);
        }

        [Test]
        public void InvalidNamespaceStillReportsBrokenMembers()
        {
            GdoBundleGraphAsset graph = GdoBundleGraphRepository.Create(Root + "/Wrong.asset", "Test Author", "Bad:Namespace", "wrong.assets");
            GdoBundleGraphRepository.AddMember(graph, Create<Item>("Resolved", 12), new Rect());
            Item asset = Create<Item>("Broken", 11);
            GdoGraphNodeRecord broken = GdoBundleGraphRepository.AddMember(graph, asset, new Rect());
            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(asset));

            GdoGraphDiagnostic[] issues = GdoBundleGraphValidator.Validate(graph).ToArray();

            Assert.That(issues.Any(issue => issue.Category == "InvalidProjectLayout" && issue.Severity == ValidationSeverity.Error), Is.True);
            Assert.That(issues.Any(issue => issue.Category == "BrokenMember" && issue.ElementId == broken.Id), Is.True);
        }

        private static void AssertBlockingNodes(GdoGraphDiagnostic[] issues, string category, params GdoGraphNodeRecord[] nodes)
        {
            GdoGraphDiagnostic[] matches = issues.Where(issue => issue.Category == category).ToArray();
            Assert.That(matches.Select(issue => issue.ElementId), Is.EquivalentTo(nodes.Select(node => node.Id)));
            Assert.That(matches.All(issue => issue.Severity == ValidationSeverity.Error), Is.True);
        }

        private static void AssertDeterministic(GdoBundleGraphAsset graph, GdoGraphDiagnostic[] issues)
        {
            Assert.That(issues.Select(issue => issue.SortKey), Is.Ordered.Using<string>(System.StringComparer.Ordinal));
            Assert.That(GdoBundleGraphValidator.Validate(graph).Select(issue => issue.SortKey + "|" + issue.AssetPath + "|" + issue.Message),
                Is.EqualTo(issues.Select(issue => issue.SortKey + "|" + issue.AssetPath + "|" + issue.Message)));
        }

        [Test]
        public void ReportsZeroDuplicateAndBrokenMembersWithStableElementIds()
        {
            Item zero = Create<Item>("Zero", 0);
            Item duplicate = Create<Item>("Duplicate", 22);
            Item duplicate2 = Create<Item>("Duplicate2", 22);
            GdoBundleGraphAsset graph = Graph("First");
            GdoGraphNodeRecord zeroNode = GdoBundleGraphRepository.AddMember(graph, zero, new Rect());
            GdoBundleGraphRepository.AddMember(graph, duplicate, new Rect());
            GdoGraphNodeRecord brokenNode = GdoBundleGraphRepository.AddMember(graph, duplicate2, new Rect());
            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(duplicate2));

            GdoGraphDiagnostic[] issues = GdoBundleGraphValidator.Validate(graph).ToArray();

            Assert.That(issues.Any(issue => issue.Category == "ZeroId" && issue.ElementId == zeroNode.Id), Is.True);
            Assert.That(issues.Count(issue => issue.Category == "DuplicateId"), Is.EqualTo(2));
            Assert.That(issues.Any(issue => issue.Category == "BrokenMember" && issue.ElementId == brokenNode.Id), Is.True);
            AssertDeterministic(graph, issues);
        }

        [Test]
        public void ReportsConflictingMembershipAndExcessSingleConnections()
        {
            Item owner = Create<Item>("Owner", 30);
            Item first = Create<Item>("FirstTarget", 31);
            Item second = Create<Item>("SecondTarget", 32);
            GdoBundleGraphAsset graph = Graph("First");
            GdoBundleGraphAsset other = GdoBundleGraphRepository.Create(Root + "/Other.asset", "Test Author", "Other", "other.assets");
            GdoGraphNodeRecord ownerNode = GdoBundleGraphRepository.AddMember(graph, owner, new Rect());
            GdoBundleGraphRepository.AddMember(other, owner, new Rect());
            GdoGraphNodeRecord firstNode = GdoBundleGraphRepository.AddMember(graph, first, new Rect());
            GdoGraphNodeRecord secondNode = GdoBundleGraphRepository.AddMember(graph, second, new Rect());
            GdoBundleGraphRepository.AddEdge(graph, firstNode.Id, "Value", ownerNode.Id, "KitchenData.Item.DirtiesTo", 0);
            GdoBundleGraphRepository.AddEdge(graph, secondNode.Id, "Value", ownerNode.Id, "KitchenData.Item.DirtiesTo", 1);

            string[] categories = GdoBundleGraphValidator.Validate(graph).Select(issue => issue.Category).ToArray();

            Assert.That(categories, Does.Contain("ConflictingMembership"));
            Assert.That(categories, Does.Contain("ExcessConnection"));
        }

        [Test]
        public void ReportsIncompatibleReferenceAndProjectionDriftWithoutDeletingData()
        {
            Item owner = Create<Item>("Owner", 40);
            Appliance incompatible = Create<Appliance>("Appliance", 41);
            GdoBundleGraphAsset graph = Graph("First");
            GdoGraphNodeRecord ownerNode = GdoBundleGraphRepository.AddMember(graph, owner, new Rect());
            GdoGraphNodeRecord targetNode = GdoBundleGraphRepository.AddMember(graph, incompatible, new Rect());
            GdoBundleGraphRepository.AddEdge(graph, targetNode.Id, "Value", ownerNode.Id, "KitchenData.Item.DirtiesTo", 0);
            GdoOverlayAuthoringAsset overlay = GdoOverlayAuthoringRepository.GetOrCreate(owner);
            overlay.UpsertField("KitchenData.Item", "DirtiesTo",
                GdoOverlayValue.Reference(GdoReferenceToken.Create(ExternalGdoCatalogRepository.VanillaProviderKey, 999, "KitchenData.Item")));

            GdoGraphDiagnostic[] issues = GdoBundleGraphValidator.Validate(graph).ToArray();

            Assert.That(issues.Any(issue => issue.Category == "IncompatibleReference" && issue.ElementId == ownerNode.Id), Is.True);
            Assert.That(overlay.TryGetField("KitchenData.Item", "DirtiesTo", out GdoOverlayField existing), Is.True);
            Assert.That(existing.Value.ReferenceValue.Id, Is.EqualTo(999));
        }

        private static GdoBundleGraphAsset Graph(string name) =>
            GdoBundleGraphRepository.Create(Root + "/" + name + ".asset", "Test Author", name, name.ToLowerInvariant() + ".assets");

        private static T Create<T>(string name, int id) where T : GameDataObject
        {
            T value = ScriptableObject.CreateInstance<T>(); value.name = name; value.ID = id;
            AssetDatabase.CreateAsset(value, Root + "/" + name + ".asset"); return value;
        }
    }
}
