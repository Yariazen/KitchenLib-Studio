using System.Collections.Generic;
using KitchenData;
using UnityEditor;
using UnityEngine;

namespace KitchenLib.Studio.Editor
{
    public static class DishLinkProofAsset
    {
        public const string AssetPath = "Assets/ModContent/DishLinkProof/KLStudio Link Proof Dish.asset";
        public static IReadOnlyList<string> RuntimeAssetPaths { get; } = new[] { AssetPath };

        public static Dish CreateOrUpdate()
        {
            EnsureFolder("Assets/ModContent");
            EnsureFolder("Assets/ModContent/DishLinkProof");

            Dish dish = AssetDatabase.LoadAssetAtPath<Dish>(AssetPath);
            if (dish == null)
            {
                dish = ScriptableObject.CreateInstance<Dish>();
                AssetDatabase.CreateAsset(dish, AssetPath);
            }

            dish.name = "KLStudio Link Proof Dish";
            dish.ID = -1950922446;
            EditorUtility.SetDirty(dish);

            GdoOverlayAuthoringAsset companion = GdoOverlayAuthoringRepository.GetOrCreate(dish);
            if (companion.Fields.Count == 0)
                companion.SetField("KitchenData.Dish", "MinimumIngredients", GdoOverlayValue.Set(new[]
                { GdoOverlayValue.Reference(GdoReferenceToken.Create(ExternalGdoCatalogRepository.VanillaProviderKey, -201067776, "KitchenData.Item")) }));
            EditorUtility.SetDirty(companion);
            AssetDatabase.SaveAssets();
            return dish;
        }

        [MenuItem("KitchenLib Studio/Validation/Create Dish Link Proof Asset")]
        private static void CreateFromMenu()
        {
            Selection.activeObject = CreateOrUpdate();
        }

        public static void CreateOrUpdateForBatch()
        {
            CreateOrUpdate();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string name = System.IO.Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
