using System.Linq;
using KitchenData;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

using KitchenLib.Studio.Editor.Overlays;

namespace KitchenLib.Studio.Editor.Authoring
{
    internal static class DishLinkInspector
    {
        public static void Draw(Dish dish)
        {
            GdoLinkAuthoringAsset companion = GdoLinkAuthoringRepository.GetOrCreate(dish);
            var serialized = new SerializedObject(companion);
            serialized.Update();
            SerializedProperty ids = serialized.FindProperty("_minimumIngredientIds");
            var list = new ReorderableList(serialized, ids, true, true, true, true);
            list.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Minimum Ingredients (Item IDs)");
            list.drawElementCallback = (rect, index, active, focused) =>
                EditorGUI.PropertyField(rect, ids.GetArrayElementAtIndex(index), GUIContent.none);
            list.elementHeight = EditorGUIUtility.singleLineHeight;
            list.DoLayoutList();

            if (serialized.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(companion);
            }

            if (companion.MinimumIngredientIds.Any(id => id == 0))
            {
                EditorGUILayout.HelpBox("Minimum Ingredients cannot contain ID 0.", MessageType.Error);
            }
        }
    }
}
