using System.Collections.Generic;
using System.Linq;
using System.IO;
using KitchenData;
using UnityEditor;
using UnityEngine;

namespace KitchenLib.Studio.Editor
{
    public static class FullDishProofAsset
    {
        public const int DishId = -1950922445;
        public const int OnionId = -201067776;
        public const int OnionRingsRawGroupId = 1818895897;
        public const string Root = "Assets/ModContent/FullDishProof";
        public const string DishPath = Root + "/KLStudio Full Dish Proof.asset";
        public const string MaterialPath = Root + "/KLStudio Proof Material.mat";
        public const string IconPath = Root + "/KLStudio Proof Icon.prefab";
        public const string DisplayPath = Root + "/KLStudio Proof Display.prefab";
        public static IReadOnlyList<string> RuntimeAssetPaths => new[] { DishPath, MaterialPath, IconPath, DisplayPath };

        public static Dish CreateOrUpdate()
        {
            EnsureFolder("Assets/ModContent"); EnsureFolder(Root);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null) { material = new Material(Shader.Find("Standard")) { name = "KLStudio Proof Material", color = new Color(0.95f, 0.35f, 0.12f) }; AssetDatabase.CreateAsset(material, MaterialPath); }
            GameObject icon = CreatePrefab(IconPath, "KLStudio Proof Icon", material, PrimitiveType.Sphere);
            GameObject display = CreatePrefab(DisplayPath, "KLStudio Proof Display", material, PrimitiveType.Cube);
            Dish dish = AssetDatabase.LoadAssetAtPath<Dish>(DishPath);
            if (dish == null) { dish = ScriptableObject.CreateInstance<Dish>(); AssetDatabase.CreateAsset(dish, DishPath); }
            dish.name = "KLStudio Full Dish Proof"; dish.ID = DishId; dish.Difficulty = 3; dish.RewardOverride = 17;
            dish.ImageKey = "klstudio_full_dish"; dish.AchievementName = "KLStudio Proof"; dish.IconPrefab = icon; dish.DisplayPrefab = display;
            dish.StartingNameSet = new List<string> { "Proof Plate" }; dish.IsSpeedrunDish = true; dish.HideInfoPanel = true;
            EditorUtility.SetDirty(dish); AssetDatabase.SaveAssets();

            GdoOverlayAuthoringAsset overlay = GdoOverlayAuthoringRepository.GetOrCreate(dish);
            if (overlay.Fields.Count == 0) Populate(overlay, OnionId);
            EditorUtility.SetDirty(overlay); AssetDatabase.SaveAssets();
            return dish;
        }

        [MenuItem("KitchenLib Studio/Validation/Build Full Dish Proof")]
        public static void BuildPackage()
        {
            Dish dish = CreateOrUpdate();
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string packageRoot = Path.Combine(projectRoot, "Build", "KitchenLibStudioFullDishProof");
            string contentRoot = Path.Combine(packageRoot, "content");
            if (Directory.Exists(packageRoot)) Directory.Delete(packageRoot, true);
            BundleBuildResult result = GdoBundleBuilder.Build(new BundleBuildRequest(new[] { AssetDatabase.GetAssetPath(dish) }, contentRoot, "full-dish-proof.assets"));
            if (!result.Succeeded) throw new System.InvalidOperationException(string.Join("\n", result.Issues));
            IReadOnlyList<ValidationIssue> issues = BundleOutputVerifier.Verify(packageRoot, result.BundlePath, ExpectedGdo.FullDishProof);
            if (issues.Any(issue => issue.Severity >= ValidationSeverity.Error)) throw new System.InvalidOperationException(string.Join("\n", issues));
        }
        public static void BuildPackageForBatch() => BuildPackage();

        public static void BuildNegativePackageForBatch()
        {
            Dish dish = CreateOrUpdate();
            ResetOverlay(dish, -987654321);
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string packageRoot = Path.Combine(projectRoot, "Build", "KitchenLibStudioFullDishNegativeProof");
            if (Directory.Exists(packageRoot)) Directory.Delete(packageRoot, true);
            BundleBuildResult result = GdoBundleBuilder.Build(new BundleBuildRequest(new[] { AssetDatabase.GetAssetPath(dish) }, Path.Combine(packageRoot, "content"), "full-dish-proof.assets"));
            dish = AssetDatabase.LoadAssetAtPath<Dish>(DishPath);
            ResetOverlay(dish, OnionId);
            if (!result.Succeeded) throw new System.InvalidOperationException(string.Join("\n", result.Issues));
        }

        private static void ResetOverlay(Dish dish, int nestedItemId)
        {
            GdoOverlayAuthoringAsset existing = GdoOverlayAuthoringRepository.Find(dish);
            if (existing != null) AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(existing));
            GdoOverlayAuthoringAsset replacement = GdoOverlayAuthoringRepository.GetOrCreate(dish);
            Populate(replacement, nestedItemId); EditorUtility.SetDirty(replacement); AssetDatabase.SaveAssets();
        }

        private static void Populate(GdoOverlayAuthoringAsset overlay, int nestedItemId)
        {
            GdoOverlayValue self = GdoOverlayValue.Reference(GdoReferenceToken.Create(ExternalGdoCatalogRepository.VanillaProviderKey, DishId, "KitchenData.Unlock"));
            GdoOverlayValue onion = GdoOverlayValue.Reference(GdoReferenceToken.Create(ExternalGdoCatalogRepository.VanillaProviderKey, OnionId, "KitchenData.Item"));
            overlay.SetField("KitchenData.LocalisedGameDataObject`1", "Info", GdoOverlayValue.LocalizationMap(new[]
            {
                new GdoLocaleEntry(1, "Proof Dish", "English proof description", "English proof flavour"),
                new GdoLocaleEntry(2, "Plat Preuve", "Description française", "Saveur française")
            }));
            overlay.SetField("KitchenData.Unlock", "HardcodedRequirements", GdoOverlayValue.List(new[] { self }));
            overlay.SetField("KitchenData.Unlock", "HardcodedBlockers", GdoOverlayValue.List(new[] { self }));
            overlay.SetField("KitchenData.Dish", "UnlockItemOverride", onion);
            overlay.SetField("KitchenData.Dish", "ResultingMenuItems", GdoOverlayValue.List(new[] { GdoOverlayValue.Struct(new[]
            {
                new GdoOverlayMember("Item", onion), new GdoOverlayMember("Phase", GdoOverlayValue.Enum(0)),
                new GdoOverlayMember("Weight", GdoOverlayValue.Scalar("1")), new GdoOverlayMember("DynamicMenuType", GdoOverlayValue.Enum(0)),
                new GdoOverlayMember("DynamicMenuIngredient", GdoOverlayValue.Reference(GdoReferenceToken.Create(ExternalGdoCatalogRepository.VanillaProviderKey, nestedItemId, "KitchenData.Item")))
            }) }));
            overlay.SetField("KitchenData.Dish", "IngredientsUnlocks", GdoOverlayValue.Set(new[] { GdoOverlayValue.Struct(new[]
            {
                new GdoOverlayMember("MenuItem", GdoOverlayValue.Reference(GdoReferenceToken.Create(ExternalGdoCatalogRepository.VanillaProviderKey, OnionRingsRawGroupId, "KitchenData.ItemGroup"))),
                new GdoOverlayMember("Ingredient", onion)
            }) }));
            overlay.SetField("KitchenData.Dish", "MinimumIngredients", GdoOverlayValue.Set(new[] { onion }));
            overlay.SetField("KitchenData.Dish", "ExtraOrderUnlocks", GdoOverlayValue.Set(new GdoOverlayValue[0]));
            overlay.SetField("KitchenData.Dish", "RequiredProcesses", GdoOverlayValue.Set(new GdoOverlayValue[0]));
            overlay.SetField("KitchenData.Dish", "BlockProviders", GdoOverlayValue.Set(new GdoOverlayValue[0]));
            overlay.SetField("KitchenData.Dish", "BeneficialIngredients", GdoOverlayValue.Set(new GdoOverlayValue[0]));
        }

        private static GameObject CreatePrefab(string path, string name, Material material, PrimitiveType type)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (existing != null) return existing;
            GameObject source = GameObject.CreatePrimitive(type); source.name = name; source.GetComponent<Renderer>().sharedMaterial = material;
            GameObject result = PrefabUtility.SaveAsPrefabAsset(source, path); Object.DestroyImmediate(source); return result;
        }
        private static void EnsureFolder(string path) { if (AssetDatabase.IsValidFolder(path)) return; string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent); AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path)); }
    }
}
