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
            => DescribeAt(entries, prefix, Vector2.zero, (runtimeType, _) => selected(runtimeType), enabled);

        internal static IReadOnlyList<GdoCreateMenuPresentation> DescribeAt(
            IReadOnlyList<GdoCreateMenuEntry> entries, string prefix, Vector2 position,
            Action<string, Vector2> selected, Func<bool> enabled)
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
                    () => selected(runtimeTypeName, position),
                    enabled);
            }
            return items;
        }

        public static void Populate(GenericMenu menu, IReadOnlyList<GdoCreateMenuEntry> entries,
            Action<string> selected)
            => Populate(menu, entries, new Vector2(40f, 40f), (runtimeType, _) => selected(runtimeType));

        public static void Populate(GenericMenu menu, IReadOnlyList<GdoCreateMenuEntry> entries,
            Vector2 position, Action<string, Vector2> selected)
        {
            if (menu == null) throw new ArgumentNullException(nameof(menu));
            foreach (GdoCreateMenuPresentation item in DescribeAt(entries, string.Empty, position, selected, () => true))
                menu.AddItem(new GUIContent(item.Path), false, () => item.Select());
        }

        public static void Populate(DropdownMenu menu, string prefix, IReadOnlyList<GdoCreateMenuEntry> entries,
            Action<string> selected, Func<bool> enabled)
            => Populate(menu, prefix, entries, Vector2.zero,
                (runtimeType, _) => selected(runtimeType), enabled);

        public static void Populate(DropdownMenu menu, string prefix, IReadOnlyList<GdoCreateMenuEntry> entries,
            Vector2 position, Action<string, Vector2> selected, Func<bool> enabled)
        {
            if (menu == null) throw new ArgumentNullException(nameof(menu));
            foreach (GdoCreateMenuPresentation item in DescribeAt(entries, prefix, position, selected, enabled))
                menu.AppendAction(item.Path, _ => item.Select(),
                    _ => item.Enabled ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
        }
    }
}
