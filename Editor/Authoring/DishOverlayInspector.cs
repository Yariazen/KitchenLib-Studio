using System;
using System.Collections.Generic;
using System.Linq;
using KitchenData;
using UnityEditor;
using UnityEngine;

using KitchenLib.Studio.Editor.Overlays;

namespace KitchenLib.Studio.Editor.Authoring
{
    internal sealed class DishOverlayFieldDescriptor
    {
        public string Label { get; }
        public string DeclaringType { get; }
        public string AuthoringField { get; }
        public string PopulationMethod { get; }
        public DishOverlayFieldDescriptor(string label, string declaringType, string authoringField, string populationMethod)
        { Label = label; DeclaringType = declaringType; AuthoringField = authoringField; PopulationMethod = populationMethod; }
    }

    internal static class DishOverlayInspectorModel
    {
        private static readonly DishOverlayFieldDescriptor[] Descriptors =
        {
            new DishOverlayFieldDescriptor("Requires", "KitchenData.Unlock", "HardcodedRequirements", "SetupForGame"),
            new DishOverlayFieldDescriptor("Blocked By", "KitchenData.Unlock", "HardcodedBlockers", "SetupForGame"),
            new DishOverlayFieldDescriptor("Menu Items", "KitchenData.Dish", "ResultingMenuItems", "SetupForGame"),
            new DishOverlayFieldDescriptor("Ingredient Unlocks", "KitchenData.Dish", "IngredientsUnlocks", "SetupForGame"),
            new DishOverlayFieldDescriptor("Localizations", "KitchenData.LocalisedGameDataObject`1", "Info", "Localise")
        };
        public static DishOverlayFieldDescriptor Find(string label) => Descriptors.Single(value => value.Label == label);
        public static IReadOnlyList<DishOverlayFieldDescriptor> All => Descriptors;
    }

    internal static class DishOverlayInspector
    {
        public static void Draw(Dish dish)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("KitchenLib Studio Dish Overlay", EditorStyles.boldLabel);
            GdoOverlayAuthoringAsset companion = GdoOverlayAuthoringRepository.Find(dish);
            if (companion == null)
            {
                EditorGUILayout.HelpBox("Linked and reconstructed Dish fields use a generated overlay companion.", MessageType.Info);
                if (GUILayout.Button("Create Dish Overlay Data")) GdoOverlayAuthoringRepository.GetOrCreate(dish);
                return;
            }
            foreach (DishOverlayFieldDescriptor descriptor in DishOverlayInspectorModel.All)
                EditorGUILayout.LabelField(descriptor.Label, "Stored as " + descriptor.AuthoringField + "; PlateUp populates it during " + descriptor.PopulationMethod + ".");
            if (GUILayout.Button("Open Overlay Data")) Selection.activeObject = companion;
        }
    }
}
