using System;
using System.Reflection;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

namespace KitchenLib.Studio.Editor.Graph
{
    internal static class GdoGraphHeaderButton
    {
        private static readonly FieldInfo NativeCollapseField = typeof(Node).GetField("m_CollapseButton",
            BindingFlags.Instance | BindingFlags.NonPublic);

        public static Button Create(string name, string glyph, string tooltip, string roleClass, Action clicked)
        {
            var button = new Button(clicked) { name = name };
            button.AddToClassList("kls-graph-icon-button");
            if (!string.IsNullOrEmpty(roleClass)) button.AddToClassList(roleClass);
            button.style.width = 24f;
            button.style.height = 24f;
            button.style.minWidth = 24f;
            button.style.minHeight = 24f;
            button.style.paddingLeft = 0f;
            button.style.paddingRight = 0f;
            button.style.paddingTop = 0f;
            button.style.paddingBottom = 0f;
            SetGlyph(button, glyph, tooltip);
            return button;
        }

        public static void SetGlyph(Button button, string glyph, string tooltip)
        {
            if (button == null) throw new ArgumentNullException(nameof(button));
            button.text = glyph ?? string.Empty;
            button.tooltip = tooltip ?? string.Empty;
        }

        public static void HideNativeCollapse(Node node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            VisualElement collapse = NativeCollapseField?.GetValue(node) as VisualElement ??
                node.Q<VisualElement>("collapse-button");
            if (collapse != null) collapse.style.display = DisplayStyle.None;
        }

        public static bool IsNativeCollapseVisible(Node node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            VisualElement collapse = NativeCollapseField?.GetValue(node) as VisualElement ??
                node.Q<VisualElement>("collapse-button");
            return collapse != null && collapse.style.display.value != DisplayStyle.None;
        }
    }
}
