using System.Linq;
using KitchenData;
using KitchenLib.Studio.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace KitchenLib.Studio.Tests.Editor
{
    public sealed class GdoValidationTests
    {
        private const string Root = "Assets/__KitchenLibStudioValidationTests";

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(Root);
            AssetDatabase.CreateFolder("Assets", "__KitchenLibStudioValidationTests");
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(Root);
        }

        [Test]
        public void ValidateReportsZeroIdWithAssetAndPropertyPath()
        {
            GameDifficultySettings asset = Create("Zero.asset", 0);

            var issues = GdoValidator.Validate(new GameDataObject[] { asset });

            Assert.That(issues, Has.Count.EqualTo(1));
            Assert.That(issues[0].Severity, Is.EqualTo(ValidationSeverity.Error));
            Assert.That(issues[0].AssetPath, Is.EqualTo(Root + "/Zero.asset"));
            Assert.That(issues[0].PropertyPath, Is.EqualTo("ID"));
            Assert.That(issues[0].Message, Does.Contain("non-zero"));
        }

        [Test]
        public void ValidateReportsBothAssetsWithDuplicateId()
        {
            GameDifficultySettings first = Create("First.asset", 42);
            GameDifficultySettings second = Create("Second.asset", 42);

            var issues = GdoValidator.Validate(new GameDataObject[] { first, second });

            Assert.That(issues.Select(issue => issue.AssetPath), Is.EquivalentTo(new[]
            {
                Root + "/First.asset",
                Root + "/Second.asset"
            }));
            Assert.That(issues.All(issue => issue.PropertyPath == "ID" && issue.Message.Contains("42")), Is.True);
        }

        [Test]
        public void ValidateAcceptsDistinctNonzeroIds()
        {
            GameDifficultySettings first = Create("First.asset", 41);
            GameDifficultySettings second = Create("Second.asset", 42);

            Assert.That(GdoValidator.Validate(new GameDataObject[] { first, second }), Is.Empty);
        }

        private static GameDifficultySettings Create(string fileName, int id)
        {
            var asset = ScriptableObject.CreateInstance<GameDifficultySettings>();
            asset.ID = id;
            AssetDatabase.CreateAsset(asset, Root + "/" + fileName);
            return asset;
        }
    }
}
