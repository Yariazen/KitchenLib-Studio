using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class ModGdoRelationshipValueTests
    {
        [Test]
        public void RoundTripPreservesNestedRelationshipStructureAndScalarSiblings()
        {
            ModGdoCatalogData data = CreateCatalog();
            string path = Path.Combine(Path.GetTempPath(), "relationship-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                File.WriteAllBytes(path, ModGdoCatalogFile.Serialize(data));
                ModGdoRelationshipValueData process = ModGdoCatalogFile.Read(path).entries.Single().relationshipValues.Single();

                Assert.That(process.runtimeType, Is.EqualTo("KitchenData.Item+ItemProcess"));
                Assert.That(process.children.Single(child => child.name == "Duration").rawValue, Is.EqualTo("3.5"));
                Assert.That(process.children.Single(child => child.name == "IsBad").rawValue, Is.EqualTo("false"));
                Assert.That(process.children.Single(child => child.name == "RequiresWrapper").rawValue, Is.EqualTo("true"));
                Assert.That(process.children.Single(child => child.name == "Result").resolvedGdo.runtimeId, Is.EqualTo(43));
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        [Test]
        public void RoundTripSupportsRelationshipTreesBeyondUnitySerializationDepth()
        {
            ModGdoCatalogData data = CreateCatalog();
            ModGdoRelationshipValueData root = new ModGdoRelationshipValueData
            {
                name = "Root", path = "Root", kind = "object", children = new ModGdoRelationshipValueData[0]
            };
            ModGdoRelationshipValueData cursor = root;
            for (int depth = 1; depth <= 12; depth++)
            {
                var child = new ModGdoRelationshipValueData
                {
                    name = "Level" + depth, path = cursor.path + ".Level" + depth,
                    kind = depth == 12 ? "scalar" : "object", rawValue = depth == 12 ? "leaf" : null,
                    children = new ModGdoRelationshipValueData[0]
                };
                cursor.children = new[] { child };
                cursor = child;
            }
            data.entries[0].relationshipValues = new[] { root };

            byte[] serialized = ModGdoCatalogFile.Serialize(data);
            string path = Path.Combine(Path.GetTempPath(), "deep-relationship-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                File.WriteAllBytes(path, serialized);
                cursor = ModGdoCatalogFile.Read(path).entries.Single().relationshipValues.Single();
                for (int depth = 1; depth <= 12; depth++) cursor = cursor.children.Single();
                Assert.That(cursor.rawValue, Is.EqualTo("leaf"));
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        public static ModGdoCatalogData CreateCatalog() => new ModGdoCatalogData
        {
            schemaVersion = ModGdoCatalogFile.SchemaVersion, captureToken = "capture", steamAppId = 1599600, steamBuildId = "build", bridgeVersion = "1",
            providers = new[] { new ModGdoProviderData { key = "workshop:2913877103", displayName = "IngredientLib" } },
            entries = new[]
            {
                new ModGdoEntryData
                {
                    providerKey = "workshop:2913877103", runtimeId = 42, displayName = "Pork", runtimeType = "KitchenData.Item", sourceAssembly = "IngredientLib",
                    relationshipValues = new[]
                    {
                        new ModGdoRelationshipValueData
                        {
                            name = "Processes", path = "Processes[0]", declaredType = "KitchenData.Item+ItemProcess", runtimeType = "KitchenData.Item+ItemProcess", kind = "object", collectionIndex = 0,
                            children = new[]
                            {
                                new ModGdoRelationshipValueData { name = "Process", path = "Processes[0].Process", kind = "gdo-reference", resolvedGdo = new ModGdoResolvedIdentityData { runtimeId = 1, runtimeType = "KitchenData.Process" }, children = new ModGdoRelationshipValueData[0] },
                                new ModGdoRelationshipValueData { name = "Result", path = "Processes[0].Result", kind = "gdo-reference", resolvedGdo = new ModGdoResolvedIdentityData { providerKey = "workshop:2913877103", runtimeId = 43, runtimeType = "KitchenData.Item" }, children = new ModGdoRelationshipValueData[0] },
                                new ModGdoRelationshipValueData { name = "Duration", path = "Processes[0].Duration", kind = "scalar", rawValue = "3.5", children = new ModGdoRelationshipValueData[0] },
                                new ModGdoRelationshipValueData { name = "IsBad", path = "Processes[0].IsBad", kind = "scalar", rawValue = "false", children = new ModGdoRelationshipValueData[0] },
                                new ModGdoRelationshipValueData { name = "RequiresWrapper", path = "Processes[0].RequiresWrapper", kind = "scalar", rawValue = "true", children = new ModGdoRelationshipValueData[0] }
                            }
                        }
                    }
                }
            },
            relationships = new ExternalGdoRelationshipData[0],
            diagnostics = new ModGdoCatalogDiagnosticData[0]
        };
    }
}
