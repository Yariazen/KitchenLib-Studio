using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace KitchenLib.Studio.Editor
{
    public sealed class GdoGraphEdgeView : Edge
    {
        public GdoGraphEdgeView(string recordId, string runtimeTypeName)
        {
            RecordId = recordId;
            RuntimeTypeName = runtimeTypeName;
            TypeColor = string.IsNullOrWhiteSpace(runtimeTypeName)
                ? GdoGraphTypePalette.UnresolvedColor : GdoGraphTypePalette.For(runtimeTypeName);
            edgeControl.inputColor = TypeColor;
            edgeControl.outputColor = TypeColor;
        }

        public string RecordId { get; }
        public string RuntimeTypeName { get; }
        public Color TypeColor { get; }
    }
}
