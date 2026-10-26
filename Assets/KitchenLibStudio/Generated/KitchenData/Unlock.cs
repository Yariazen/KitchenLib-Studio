using System;
using System.Collections.Generic;
using UnityEngine;

namespace KitchenData
{
    public abstract class Unlock : LocalisedGameDataObject<UnlockInfo>
    {
        public enum RewardLevel { None = 0, Small = 50, Medium = 100, Large = 200 }

        [Serializable]
        public struct ItemReward
        {
            public Item Item;
            public int RewardAmount;
        }

        #region Serialized Authoring State
        public RewardLevel ExpReward = RewardLevel.Medium;
        public Factor ExpMult;
        public DishCustomerChange CustomerMultiplier;
        public bool IsUnlockable = true;
        public bool HasSubOptions;
        public Unlock OptionCard1;
        public Unlock OptionCard2;
        public Unlock ParentOption;
        public UnlockGroup UnlockGroup;
        public CardType CardType;
        public int MinimumFranchiseTier;
        public bool IsSpecificFranchiseTier;
        public float SelectionBias;
        [SerializeField] private List<Unlock> HardcodedRequirements;
        [SerializeField] private List<Unlock> HardcodedBlockers;
        public bool BlocksAllOtherFood;
        public List<Unlock> AllowedFoods = new List<Unlock>();
        public RestaurantSetting ForceFranchiseSetting;
        public List<ItemReward> ItemRewards = new List<ItemReward>();
        public int HeatCardDisplayedNumber;
        #endregion

        #region Runtime-Derived State
        // PlateUp copies HardcodedRequirements into Requires during SetupForGame().
        [NonSerialized] public List<Unlock> Requires;
        // PlateUp copies HardcodedBlockers into BlockedBy during SetupForGame().
        [NonSerialized] public List<Unlock> BlockedBy;
        #endregion

        protected override void InitialiseDefaults() { }
    }
}