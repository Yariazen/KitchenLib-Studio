using KitchenLib.Studio.Generated;

namespace KitchenLib.Studio.Editor
{
    public enum GdoGraphPortCardinality { Single, Multiple }
    public enum GdoGraphCollectionKind { None, List, Array, Set }

    public sealed class GdoGraphPortDescriptor
    {
        internal GdoGraphPortDescriptor(string fieldKey, string path, string relationshipPath,
            string expectedTypeName, bool required, bool usesRelationshipNode,
            GdoGraphPortCardinality cardinality, GdoGraphCollectionKind collectionKind,
            GdoValueDescriptorData rootValue, GdoValueDescriptorData value)
        {
            FieldKey = fieldKey;
            Path = path;
            RelationshipPath = relationshipPath;
            ExpectedTypeName = expectedTypeName;
            IsRequired = required;
            UsesRelationshipNode = usesRelationshipNode;
            Cardinality = cardinality;
            CollectionKind = collectionKind;
            RootValue = rootValue;
            Value = value;
        }

        public string FieldKey { get; }
        public string Path { get; }
        public string RelationshipPath { get; }
        public string ExpectedTypeName { get; }
        public bool IsRequired { get; }
        public bool UsesRelationshipNode { get; }
        public GdoGraphPortCardinality Cardinality { get; }
        public GdoGraphCollectionKind CollectionKind { get; }
        internal GdoValueDescriptorData RootValue { get; }
        internal GdoValueDescriptorData Value { get; }
    }
}
