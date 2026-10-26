using System;

namespace KitchenLib.Studio.Editor
{
    public static class ExternalGdoSnapshotEligibility
    {
        private static readonly string[] SupportedRoots =
        {
            "KitchenData.Item",
            "KitchenData.Dish",
            "KitchenData.Appliance",
            "KitchenData.PlayerCosmetic"
        };

        public static bool IsEligible(string runtimeType)
        {
            if (string.IsNullOrWhiteSpace(runtimeType)) return false;
            foreach (string root in SupportedRoots)
                if (GdoDescriptorCatalog.IsAssignable(runtimeType, root)) return true;
            return false;
        }
    }
}
