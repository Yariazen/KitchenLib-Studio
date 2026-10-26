using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KitchenData;
using UnityEditor;
using UnityEngine;

namespace KitchenLib.Studio.Editor
{
    public static class GenericGdoProofAsset
    {
        public const int DishId = -1950922435;
        public const int ItemId = -1950922434;
        public const int ApplianceId = -1950922433;
        public const int OnionRingsRawGroupId = 1818895897;
        public const string Root = "Assets/ModContent/GenericGdoProof";
        public const string DishPath = Root + "/KLStudio Generic Dish.asset";
        public const string ItemPath = Root + "/KLStudio Generic Item.asset";
        public const string AppliancePath = Root + "/KLStudio Generic Appliance.asset";
        public const string MaterialPath = Root + "/KLStudio Generic Material.mat";
        public const string DishPrefabPath = Root + "/KLStudio Generic Dish.prefab";
        public const string AppliancePrefabPath = Root + "/KLStudio Generic Appliance.prefab";
        public const string PositivePackageName = "KitchenLibStudioGenericGdoProof";
        public const string NegativePackageName = "KitchenLibStudioGenericGdoNegativeProof";

        public static IReadOnlyList<GameDataObject> CreateOrUpdate()
        {
            EnsureFolder("Assets/ModContent");
            EnsureFolder(Root);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard"))
                { name = "KLStudio Generic Material", color = new Color(0.2f, 0.65f, 0.9f) };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            GameObject dishPrefab = CreatePrefab(DishPrefabPath, "KLStudio Generic Dish", material, PrimitiveType.Sphere);
            GameObject appliancePrefab = CreatePrefab(AppliancePrefabPath, "KLStudio Generic Appliance", material, PrimitiveType.Cube);

            Item item = LoadOrCreate<Item>(ItemPath, "KLStudio Generic Item", ItemId);
            Dish dish = LoadOrCreate<Dish>(DishPath, "KLStudio Generic Dish", DishId);
            dish.Difficulty = 2;
            dish.RewardOverride = 19;
            dish.ImageKey = "klstudio_generic_dish";
            dish.IconPrefab = dishPrefab;
            dish.DisplayPrefab = dishPrefab;
            dish.StartingNameSet = new List<string> { "Generic Plate" };
            Appliance appliance = LoadOrCreate<Appliance>(AppliancePath, "KLStudio Generic Appliance", ApplianceId);
            appliance.Prefab = appliancePrefab;
            appliance.HeldAppliancePrefab = appliancePrefab;
            appliance.PurchaseCost = 42;
            appliance.IsPurchasable = true;
            appliance.IsNonInteractive = true;
            EditorUtility.SetDirty(dish);
            EditorUtility.SetDirty(appliance);
            AssetDatabase.SaveAssets();

            PopulateIfEmpty(AcceptCurrentDescriptor(GdoOverlayAuthoringRepository.GetOrCreate(dish)), dish, item, ItemId);
            PopulateIfEmpty(AcceptCurrentDescriptor(GdoOverlayAuthoringRepository.GetOrCreate(item)), item, ItemId);
            PopulateIfEmpty(AcceptCurrentDescriptor(GdoOverlayAuthoringRepository.GetOrCreate(appliance)), appliance, item);
            AssetDatabase.SaveAssets();
            return new GameDataObject[] { dish, item, appliance };
        }

        [MenuItem("KitchenLib Studio/Validation/Build Generic GDO Proof")]
        public static void BuildPositivePackageForBatch() => BuildPackage(PositivePackageName, false);

        public static void BuildNegativePackageForBatch() => BuildPackage(NegativePackageName, true);

        public static BundleBuildResult BuildPackage(string packageName, bool corruptItemReference)
        {
            GameDataObject[] targets = CreateOrUpdate().ToArray();
            Dish dish = targets.OfType<Dish>().Single();
            Item item = targets.OfType<Item>().Single();
            if (corruptItemReference) ResetItemOverlay(item, -987654321);
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string packageRoot = Path.Combine(projectRoot, "Build", packageName);
            if (Directory.Exists(packageRoot)) Directory.Delete(packageRoot, true);
            BundleBuildResult result = GdoBundleBuilder.Build(new BundleBuildRequest(
                targets.Select(AssetDatabase.GetAssetPath).ToArray(), Path.Combine(packageRoot, "content"), "generic-gdo-proof.assets"));
            if (corruptItemReference) ResetItemOverlay(AssetDatabase.LoadAssetAtPath<Item>(ItemPath), ItemId);
            return result;
        }

        private static T LoadOrCreate<T>(string path, string name, int id) where T : GameDataObject
        {
            T value = AssetDatabase.LoadAssetAtPath<T>(path);
            if (value == null)
            {
                value = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(value, path);
            }
            value.name = name;
            value.ID = id;
            EditorUtility.SetDirty(value);
            return value;
        }

        private static void PopulateIfEmpty(GdoOverlayAuthoringAsset overlay, Dish dish, Item item, int nestedItemId)
        {
            if (overlay.Fields.Count != 0) return;
            GdoOverlayValue self = GdoOverlayValue.Reference(GdoReferenceToken.ForAsset(dish, "KitchenData.Unlock"));
            GdoOverlayValue itemReference = GdoOverlayValue.Reference(GdoReferenceToken.ForAsset(item, "KitchenData.Item"));
            overlay.SetField("KitchenData.LocalisedGameDataObject`1", "Info", GdoOverlayValue.LocalizationMap(new[]
            {
                new GdoLocaleEntry(1, "Generic Dish", "Mixed GDO proof dish", "Generated without a content DLL")
            }));
            overlay.SetField("KitchenData.Unlock", "HardcodedRequirements", GdoOverlayValue.List(new[] { self }));
            overlay.SetField("KitchenData.Unlock", "HardcodedBlockers", GdoOverlayValue.List(new[] { self }));
            overlay.SetField("KitchenData.Dish", "UnlockItemOverride", itemReference);
            overlay.SetField("KitchenData.Dish", "ResultingMenuItems", GdoOverlayValue.List(new[]
            {
                GdoOverlayValue.Struct(new[]
                {
                    new GdoOverlayMember("Item", itemReference),
                    new GdoOverlayMember("Phase", GdoOverlayValue.Enum(1)),
                    new GdoOverlayMember("Weight", GdoOverlayValue.Scalar("1")),
                    new GdoOverlayMember("DynamicMenuType", GdoOverlayValue.Enum(0)),
                    new GdoOverlayMember("DynamicMenuIngredient", nestedItemId == ItemId ? itemReference :
                        GdoOverlayValue.Reference(GdoReferenceToken.Create(ExternalGdoCatalogRepository.VanillaProviderKey, nestedItemId, "KitchenData.Item")))
                })
            }));
            overlay.SetField("KitchenData.Dish", "IngredientsUnlocks", GdoOverlayValue.Set(new[]
            {
                GdoOverlayValue.Struct(new[]
                {
                    new GdoOverlayMember("MenuItem", GdoOverlayValue.Reference(GdoReferenceToken.Create(ExternalGdoCatalogRepository.VanillaProviderKey, OnionRingsRawGroupId, "KitchenData.ItemGroup"))),
                    new GdoOverlayMember("Ingredient", itemReference)
                })
            }));
            overlay.SetField("KitchenData.Dish", "MinimumIngredients", GdoOverlayValue.Set(new[] { itemReference }));
            overlay.SetField("KitchenData.Dish", "ExtraOrderUnlocks", GdoOverlayValue.Set(Array.Empty<GdoOverlayValue>()));
            overlay.SetField("KitchenData.Dish", "RequiredProcesses", GdoOverlayValue.Set(Array.Empty<GdoOverlayValue>()));
            overlay.SetField("KitchenData.Dish", "BlockProviders", GdoOverlayValue.Set(Array.Empty<GdoOverlayValue>()));
            overlay.SetField("KitchenData.Dish", "BeneficialIngredients", GdoOverlayValue.Set(Array.Empty<GdoOverlayValue>()));
            EditorUtility.SetDirty(overlay);
        }

        private static void PopulateIfEmpty(GdoOverlayAuthoringAsset overlay, Appliance appliance, Item item)
        {
            if (overlay.Fields.Count != 0) return;
            GdoOverlayValue itemReference = GdoOverlayValue.Reference(GdoReferenceToken.ForAsset(item, "KitchenData.Item"));
            overlay.SetField("KitchenData.LocalisedGameDataObject`1", "Info", GdoOverlayValue.LocalizationMap(new[]
            {
                new GdoLocaleEntry(1, "Generic Appliance", "Mixed GDO proof appliance", string.Empty)
            }));
            overlay.SetField("KitchenData.Appliance", "CrateItem", itemReference);
            overlay.SetField("KitchenData.Appliance", "RequiresIngredientForShop", GdoOverlayValue.List(new[] { itemReference }));
            EditorUtility.SetDirty(overlay);
        }

        private static void PopulateIfEmpty(GdoOverlayAuthoringAsset overlay, Item item, int dirtiesToId)
        {
            GdoOverlayValue reference = dirtiesToId == ItemId
                ? GdoOverlayValue.Reference(GdoReferenceToken.ForAsset(item, "KitchenData.Item"))
                : GdoOverlayValue.Reference(GdoReferenceToken.Create(ExternalGdoCatalogRepository.VanillaProviderKey, dirtiesToId, "KitchenData.Item"));
            if (!overlay.Fields.Any(field => field.DeclaringType == "KitchenData.Item" && field.FieldName == "AutomaticItemProcess"))
                overlay.SetField("KitchenData.Item", "AutomaticItemProcess", GdoOverlayValue.Struct(new[]
                {
                    new GdoOverlayMember("Duration", GdoOverlayValue.Scalar("0")),
                    new GdoOverlayMember("IsBad", GdoOverlayValue.Scalar("False")),
                    new GdoOverlayMember("Process", GdoOverlayValue.Null()),
                    new GdoOverlayMember("RequiresWrapper", GdoOverlayValue.Scalar("False")),
                    new GdoOverlayMember("Result", GdoOverlayValue.Reference(GdoReferenceToken.ForAsset(item, "KitchenData.Item")))
                }));
            if (!overlay.Fields.Any(field => field.DeclaringType == "KitchenData.Item" && field.FieldName == "DirtiesTo"))
                overlay.SetField("KitchenData.Item", "DirtiesTo", reference);
            EditorUtility.SetDirty(overlay);
        }

        private static GdoOverlayAuthoringAsset AcceptCurrentDescriptor(GdoOverlayAuthoringAsset overlay)
        {
            overlay.AcceptCurrentDescriptor();
            EditorUtility.SetDirty(overlay);
            return overlay;
        }

        private static void ResetDishOverlay(Dish dish, Item item, int nestedItemId)
        {
            GdoOverlayAuthoringAsset existing = GdoOverlayAuthoringRepository.Find(dish);
            if (existing != null) AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(existing));
            PopulateIfEmpty(AcceptCurrentDescriptor(GdoOverlayAuthoringRepository.GetOrCreate(dish)), dish, item, nestedItemId);
            AssetDatabase.SaveAssets();
        }

        private static void ResetItemOverlay(Item item, int dirtiesToId)
        {
            GdoOverlayAuthoringAsset existing = GdoOverlayAuthoringRepository.Find(item);
            if (existing != null) AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(existing));
            PopulateIfEmpty(AcceptCurrentDescriptor(GdoOverlayAuthoringRepository.GetOrCreate(item)), item, dirtiesToId);
            AssetDatabase.SaveAssets();
        }

        private static GameObject CreatePrefab(string path, string name, Material material, PrimitiveType primitive)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            GameObject source = GameObject.CreatePrimitive(primitive);
            source.name = name;
            source.GetComponent<Renderer>().sharedMaterial = material;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(source, path);
            UnityEngine.Object.DestroyImmediate(source);
            return prefab;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
