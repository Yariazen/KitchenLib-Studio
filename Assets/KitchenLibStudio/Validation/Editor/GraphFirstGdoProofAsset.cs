using System.Collections.Generic;
using KitchenData;
using UnityEditor;
using UnityEngine;

namespace KitchenLib.Studio.Editor
{
    public static class GraphFirstGdoProofAsset
    {
        public const string Root = "Assets/ModContent/Graph First GDO Proof";
        public const string GraphPath = Root + "/Graph First GDO Proof.asset";
        public const int DishId = -1034320677;
        public const int ItemId = -1814903598;
        public const int ApplianceId = -45997132;

        public static GdoBundleGraphAsset CreateOrUpdate()
        {
            Delete();
            EnsureFolder("Assets/ModContent");
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create("Assets/ModContent", "Test Author", "Graph First GDO Proof");
            GdoGraphNodeRecord itemNode = GdoGraphAssetCommands.CreateGdo(graph, "KitchenData.Item",
                "Graph Proof Item", new Rect(20, 260, 260, 160));
            GdoGraphNodeRecord dishNode = GdoGraphAssetCommands.CreateGdo(graph, "KitchenData.Dish",
                "Graph Proof Dish", new Rect(380, 40, 280, 200));
            GdoGraphNodeRecord applianceNode = GdoGraphAssetCommands.CreateGdo(graph, "KitchenData.Appliance",
                "Graph Proof Appliance", new Rect(380, 330, 280, 180));
            Item item = (Item)itemNode.Asset; Dish dish = (Dish)dishNode.Asset; Appliance appliance = (Appliance)applianceNode.Asset;
            dish.Difficulty = 2; dish.RewardOverride = 19; dish.ImageKey = "klstudio_graph_first";
            appliance.PurchaseCost = 42; appliance.IsPurchasable = true; appliance.IsNonInteractive = true;
            EditorUtility.SetDirty(dish); EditorUtility.SetDirty(appliance);

            Localise(item, "Graph Item"); Localise(dish, "Graph Dish"); Localise(appliance, "Graph Appliance");
            var connections = new GdoGraphConnectionController(graph);
            connections.Connect(itemNode.Id, itemNode.Id, "KitchenData.Item.DirtiesTo", 0);
            connections.Connect(itemNode.Id, applianceNode.Id, "KitchenData.Appliance.CrateItem", 0);
            connections.Connect(itemNode.Id, applianceNode.Id, "KitchenData.Appliance.RequiresIngredientForShop[]", 0);
            connections.Connect(itemNode.Id, dishNode.Id, "KitchenData.Dish.UnlockItemOverride", 0);
            connections.Connect(itemNode.Id, dishNode.Id, "KitchenData.Dish.MinimumIngredients[]", 0);

            GdoGraphRelationshipRecord menu = connections.CreateRelationship(dishNode.Id,
                "KitchenData.Dish.ResultingMenuItems[]", 0, new Rect(700, 50, 240, 180));
            connections.Connect(itemNode.Id, menu.Id, "KitchenData.Dish.ResultingMenuItems[].Item", 0);
            connections.Connect(itemNode.Id, menu.Id, "KitchenData.Dish.ResultingMenuItems[].DynamicMenuIngredient", 0);
            GdoGraphRelationshipRecord ingredient = connections.CreateRelationship(dishNode.Id,
                "KitchenData.Dish.IngredientsUnlocks[]", 0, new Rect(700, 260, 240, 160));
            connections.Connect(itemNode.Id, ingredient.Id, "KitchenData.Dish.IngredientsUnlocks[].Ingredient", 0);
            GdoGraphNodeRecord group = GdoBundleGraphRepository.AddExternal(graph,
                ExternalGdoCatalogRepository.VanillaProviderKey, GenericGdoProofAsset.OnionRingsRawGroupId,
                "KitchenData.ItemGroup", new Rect(20, 500, 220, 120));
            connections.Connect(group.Id, ingredient.Id, "KitchenData.Dish.IngredientsUnlocks[].MenuItem", 0);
            GdoGraphProjectionApplier.TryApply(graph, out _);
            AssetDatabase.SaveAssets();
            return graph;
        }

        public static void Delete()
        {
            GdoBundleGraphAsset graph = AssetDatabase.LoadAssetAtPath<GdoBundleGraphAsset>(GraphPath);
            if (graph != null)
                foreach (GdoGraphNodeRecord node in graph.Nodes)
                    if (node.Kind == GdoGraphNodeKind.Local && !node.IsBroken)
                    {
                        GdoOverlayAuthoringAsset companion = GdoOverlayAuthoringRepository.Find(node.Asset);
                        if (companion != null) AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(companion));
                    }
            AssetDatabase.DeleteAsset(Root);
        }

        private static void Localise(GameDataObject target, string name) => GdoGraphAssetCommands.SetIntrinsicValue(target,
            "KitchenData.LocalisedGameDataObject`1", "Info", GdoOverlayValue.LocalizationMap(new[] { new GdoLocaleEntry(1, name) }));

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent); AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
