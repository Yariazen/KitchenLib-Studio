using UnityEngine;

namespace KitchenData
{
    [CreateAssetMenu(fileName = "Game Difficulty Settings", menuName = "Kitchen/Difficulty Settings")]
    public class GameDifficultySettings : GameDataObject
    {
        public bool IsActive;
        public float CustomersPerHourBase = 1f;
        public float CustomersPerHourIncreasePerDay = 0.2f;
        public float CustomerSideChance = 1f;
        public float QueuePatienceTime = 100f;
        public float QueuePatienceBoost = 10f;
        public float CustomerStarterChance = 1f;
        public float GroupDessertChance = 1f;

        protected override void InitialiseDefaults()
        {
        }
    }
}