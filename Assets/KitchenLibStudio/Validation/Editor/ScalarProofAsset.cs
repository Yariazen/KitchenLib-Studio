using KitchenData;
using UnityEditor;
using UnityEngine;

namespace KitchenLib.Studio.Editor
{
    public static class ScalarProofAsset
    {
        public const string AssetPath = "Assets/ModContent/ScalarProof/KLStudio Scalar Proof.asset";

        public static GameDifficultySettings CreateOrUpdate()
        {
            EnsureFolder("Assets/ModContent");
            EnsureFolder("Assets/ModContent/ScalarProof");

            GameDifficultySettings asset = AssetDatabase.LoadAssetAtPath<GameDifficultySettings>(AssetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<GameDifficultySettings>();
                AssetDatabase.CreateAsset(asset, AssetPath);
            }

            asset.name = "KLStudio Scalar Proof";
            asset.ID = -1950922447;
            asset.IsActive = false;
            asset.CustomersPerHourBase = 1f;
            asset.CustomersPerHourIncreasePerDay = 0.2f;
            asset.CustomerSideChance = 1f;
            asset.QueuePatienceTime = 100f;
            asset.QueuePatienceBoost = 10f;
            asset.CustomerStarterChance = 1f;
            asset.GroupDessertChance = 1f;
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        [MenuItem("KitchenLib Studio/Validation/Create Scalar Proof Asset")]
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
