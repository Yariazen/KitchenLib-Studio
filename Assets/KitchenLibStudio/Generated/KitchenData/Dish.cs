using System;
using System.Collections.Generic;
using UnityEngine;

namespace KitchenData
{
    [CreateAssetMenu(fileName = "Dish", menuName = "Kitchen/Dish")]
    public class Dish : Unlock
    {
        [Serializable]
        public struct MenuItem
        {
            public Item Item;
            public MenuPhase Phase;
            public float Weight;
            public DynamicMenuType DynamicMenuType;
            public Item DynamicMenuIngredient;
        }

        [Serializable]
        public struct IngredientUnlock
        {
            public ItemGroup MenuItem;
            public Item Ingredient;
        }

        #region Serialized Authoring State
        public DishType Type;
        public int Difficulty;
        public int RewardOverride;
        public Item UnlockItemOverride;
        public bool HideInfoPanel;
        public bool SkipOwnRecipe;
        public List<Dish> AlsoAddRecipes = new List<Dish>();
        public GameObject IconPrefab;
        public GameObject DisplayPrefab;
        public string ImageKey;
        [SerializeField] private List<MenuItem> ResultingMenuItems;
        [SerializeField] private HashSet<IngredientUnlock> IngredientsUnlocks;
        public HashSet<IngredientUnlock> ExtraOrderUnlocks;
        public bool IsMainThatDoesNotNeedPlates;
        public List<RestaurantStatus> AddsStatuses;
        public string AchievementName;
        public List<string> StartingNameSet = new List<string>();
        public bool IsSpeedrunDish;
        public HeatLevels HeatOverride;
        public HashSet<Item> MinimumIngredients;
        public HashSet<Process> RequiredProcesses;
        public HashSet<Item> BlockProviders;
        public HashSet<Item> BeneficialIngredients = new HashSet<Item>();
        #endregion

        #region Runtime-Derived State
        // PlateUp copies ResultingMenuItems into UnlocksMenuItems during SetupForGame().
        [NonSerialized] public List<MenuItem> UnlocksMenuItems;
        // PlateUp copies IngredientsUnlocks into UnlocksIngredients during SetupForGame().
        [NonSerialized] public HashSet<IngredientUnlock> UnlocksIngredients;
        #endregion

        protected override void InitialiseDefaults() { }
    }
}