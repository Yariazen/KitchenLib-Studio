using System;
using System.Collections.Generic;

namespace KitchenLib.Studio.Editor.Overlays
{
    internal static class GdoOverlayLimits
    {
        public const int MaximumDepth = 64;
        public const int MaximumNodeCount = 10000;

        public static bool TryValidate(IEnumerable<GdoOverlayValue> roots, out GdoOverlayPersistenceDiagnostic diagnostic)
        {
            if (roots == null) throw new ArgumentNullException(nameof(roots));
            var stack = new Stack<Entry>();
            int rootIndex = 0;
            foreach (GdoOverlayValue root in roots) stack.Push(new Entry(root, 0, "root[" + rootIndex++ + "]"));
            int count = 0;
            while (stack.Count > 0)
            {
                Entry current = stack.Pop();
                if (current.Value == null)
                {
                    diagnostic = new GdoOverlayPersistenceDiagnostic("InvalidNodePayload", current.Path, "Overlay value is missing.");
                    return false;
                }
                if (current.Depth > MaximumDepth)
                {
                    diagnostic = new GdoOverlayPersistenceDiagnostic("MaximumDepthExceeded", current.Path,
                        "Overlay depth exceeds the maximum of " + MaximumDepth + ".");
                    return false;
                }
                if (++count > MaximumNodeCount)
                {
                    diagnostic = new GdoOverlayPersistenceDiagnostic("MaximumNodeCountExceeded", current.Path,
                        "Overlay node count exceeds the maximum of " + MaximumNodeCount + ".");
                    return false;
                }
                for (int index = current.Value.Values.Count - 1; index >= 0; index--)
                    stack.Push(new Entry(current.Value.Values[index], current.Depth + 1, current.Path + "[" + index + "]"));
                for (int index = current.Value.Members.Count - 1; index >= 0; index--)
                {
                    GdoOverlayMember member = current.Value.Members[index];
                    stack.Push(new Entry(member?.Value, current.Depth + 1, current.Path + "." + (member?.Name ?? "<missing>")));
                }
            }
            diagnostic = null;
            return true;
        }

        private readonly struct Entry
        {
            public Entry(GdoOverlayValue value, int depth, string path) { Value = value; Depth = depth; Path = path; }
            public GdoOverlayValue Value { get; }
            public int Depth { get; }
            public string Path { get; }
        }
    }
}
