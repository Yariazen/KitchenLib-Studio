using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace KitchenLib.Studio.Editor.Graph
{
    internal sealed class GdoCreateMenuPresentation
    {
        private readonly Func<bool> _enabled;

        public GdoCreateMenuPresentation(string path, string runtimeTypeName, Action select, Func<bool> enabled)
        {
            Path = path;
            RuntimeTypeName = runtimeTypeName;
            Select = select;
            _enabled = enabled;
        }

        public string Path { get; }
        public string RuntimeTypeName { get; }
        public Action Select { get; }
        public bool Enabled => _enabled();
    }

    internal static class GdoCreateMenuPresenter
    {
        internal static IReadOnlyList<GdoCreateMenuPresentation> Describe(
            IReadOnlyList<GdoCreateMenuEntry> entries, string prefix, Action<string> selected, Func<bool> enabled)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            if (selected == null) throw new ArgumentNullException(nameof(selected));
            if (enabled == null) throw new ArgumentNullException(nameof(enabled));

            var items = new GdoCreateMenuPresentation[entries.Count];
            for (int index = 0; index < entries.Count; index++)
            {
                GdoCreateMenuEntry entry = entries[index];
                string runtimeTypeName = entry.RuntimeTypeName;
                items[index] = new GdoCreateMenuPresentation(
                    (prefix ?? string.Empty) + entry.MenuPath,
                    runtimeTypeName,
                    () => selected(runtimeTypeName),
                    enabled);
            }
            return items;
        }

        public static void Populate(GenericMenu menu, IReadOnlyList<GdoCreateMenuEntry> entries,
            Action<string> selected)
        {
            if (menu == null) throw new ArgumentNullException(nameof(menu));
            foreach (GdoCreateMenuPresentation item in Describe(entries, string.Empty, selected, () => true))
                menu.AddItem(new GUIContent(item.Path), false, () => item.Select());
        }

        public static void Populate(DropdownMenu menu, string prefix, IReadOnlyList<GdoCreateMenuEntry> entries,
            Action<string> selected, Func<bool> enabled)
        {
            if (menu == null) throw new ArgumentNullException(nameof(menu));
            foreach (GdoCreateMenuPresentation item in Describe(entries, prefix, selected, enabled))
                menu.AppendAction(item.Path, _ => item.Select(),
                    _ => item.Enabled ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
        }
    }
}
