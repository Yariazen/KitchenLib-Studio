using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class VanillaGdoPointerCollectorTests
    {
        [Test]
        public void CollectsNestedLocalAndExternalPointersWithCollectionPaths()
        {
            object array = Field("Array",
                Field("data", Pointer("Process", 0, 20)),
                Field("data", Pointer("Result", 1, 30)));
            object root = Field("Base", Field("Processes", array));

            PlateUpAssetPointer[] values = Collect(root,
                fileId => fileId == 1 ? "sharedassets0.assets" : null);

            Assert.That(values.Select(value => value.FieldPath),
                Is.EqualTo(new[] { "Processes[0].Process", "Processes[1].Result" }));
            Assert.That(values[0].TargetRelativeAssetPath, Is.EqualTo("sharedassets1.assets"));
            Assert.That(values[1].TargetRelativeAssetPath, Is.EqualTo("sharedassets0.assets"));
        }

        [Test]
        public void ExcludesMetadataPrefabIdentityAndUnresolvedPointers()
        {
            object root = Field("Base",
                Pointer("m_Script", 0, 1),
                Pointer("m_GameObject", 0, 2),
                Pointer("Prefab", 0, 3),
                Pointer("IconPrefab", 0, 4),
                Pointer("DisplayPrefab", 0, 5),
                Pointer("Visual", 0, 6),
                Pointer("_IconPrefab", 0, 7),
                Value("ID", 8),
                Pointer("Related", 2, 9));

            Assert.That(Collect(root, _ => null), Is.Empty);
        }

        private static PlateUpAssetPointer[] Collect(object root, Func<int, string> resolver)
        {
            Type collector = typeof(PlateUpAssetPointer).Assembly.GetType(
                "KitchenLib.Studio.Editor.VanillaGdoPointerCollector");
            Assert.That(collector, Is.Not.Null, "The serialized pointer collector is missing.");
            MethodInfo method = collector.GetMethod("Collect", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            return ((IEnumerable)method.Invoke(null, new object[]
                { root, "sharedassets1.assets", 10L, resolver })).Cast<PlateUpAssetPointer>().ToArray();
        }

        private static object Pointer(string name, int fileId, long pathId) =>
            Field(name, Value("m_FileID", fileId), Value("m_PathID", pathId));

        private static object Value(string name, int value) => Value(name, value, typeof(int));
        private static object Value(string name, long value) => Value(name, value, typeof(long));

        private static object Value(string name, object value, Type valueType)
        {
            object field = Field(name);
            Type assetValue = AssetsToolsType("AssetTypeValue");
            field.GetType().GetProperty("Value").SetValue(field,
                Activator.CreateInstance(assetValue, new[] { value }));
            return field;
        }

        private static object Field(string name, params object[] children)
        {
            Type fieldType = AssetsToolsType("AssetTypeValueField");
            Type templateType = AssetsToolsType("AssetTypeTemplateField");
            object template = Activator.CreateInstance(templateType);
            templateType.GetProperty("Name").SetValue(template, name);
            templateType.GetProperty("Type").SetValue(template, "struct");
            object field = Activator.CreateInstance(fieldType);
            fieldType.GetProperty("TemplateField").SetValue(field, template);
            Type listType = typeof(List<>).MakeGenericType(fieldType);
            IList list = (IList)Activator.CreateInstance(listType);
            foreach (object child in children) list.Add(child);
            fieldType.GetProperty("Children").SetValue(field, list);
            return field;
        }

        private static Type AssetsToolsType(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("AssetsTools.NET." + name, false))
            .First(type => type != null);
    }
}
