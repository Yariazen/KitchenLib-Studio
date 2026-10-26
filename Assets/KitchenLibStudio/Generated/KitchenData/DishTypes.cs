using System;

namespace KitchenData
{
    public enum DishCustomerChange { None, SmallIncrease, LargeIncrease, SmallDecrease, LargeDecrease, FranchiseTier }
    public enum UnlockGroup { Generic = 0, Dish = 1, PrimaryTheme = 3, SecondaryTheme = 4, Special = 6 }
    public enum CardType { Default, FranchiseTier, Contract, ThemeUnlock, HalloweenTreat, HalloweenTrick, Setting, Heat, Chill }
    public enum DishType { Base, Main, Extra, Side, Starter, Dessert, Null }
    public enum MenuPhase { Starter, Main, Dessert, Side, Complete }
    public enum DynamicMenuType { Static, Fish }
    public enum RestaurantStatus { None = 0 }

    [Serializable] public struct Factor { public float Value; }

}