using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace KitchenLib.Studio.Editor.Tests
{
    public sealed class ExternalGdoPickerTests
    {
        private string _path;

        [SetUp]
        public void SetUp() { _path = Path.Combine(Path.GetTempPath(), "picker-" + Guid.NewGuid().ToString("N") + ".json"); }
        [TearDown]
        public void TearDown() { if (File.Exists(_path)) File.Delete(_path); }

        [Test]
        public void FiltersByExpectedTypeAndCommitsWaterWithoutChangingItsExpectedType()
        {
            ExternalGdoCatalogRepository repository = ReadyRepository(_path);
            ExternalGdoCatalogEntry selected = null;
            var picker = new ExternalGdoPicker("KitchenData.Item", repository, entry => selected = entry);

            Assert.That(picker.Search("wa").Select(entry => entry.DisplayName), Is.EqualTo(new[] { "Water" }));
            picker.Select(repository.Find(ExternalGdoCatalogRepository.VanillaProviderKey, 1657174953, "KitchenData.Item"));

            Assert.That(selected.RuntimeId, Is.EqualTo(1657174953));
            Assert.That(picker.Q<Label>("vanilla-selection").text, Does.Contain("Water"));
            Assert.That(picker.Q<Label>("vanilla-provenance").text, Is.EqualTo("VANILLA · PlateUp"));
        }

        [Test]
        public void CatalogueFirstLayoutUsesCompactRowsAndKeepsRuntimeIdInTooltip()
        {
            ExternalGdoCatalogRepository repository = ReadyRepository(_path);
            var picker = new ExternalGdoPicker("KitchenData.Item", repository, _ => { }, false);

            Assert.That(picker.Q<Label>("vanilla-selection"), Is.Null);
            Assert.That(picker.Q<Label>("vanilla-provenance"), Is.Null);
            ListView results = picker.Q<ListView>("vanilla-results");
            Assert.That(results, Is.Not.Null);
            Assert.That(results.itemsSource.Count, Is.EqualTo(1));
            VisualElement row = results.makeItem();
            results.bindItem(row, 0);
            Assert.That(row.Q<Label>("vanilla-result-name").text, Is.EqualTo("Water"));
            Assert.That(row.Q<Label>("vanilla-result-type").text, Is.EqualTo("PlateUp · Item"));
            Assert.That(row.tooltip, Does.Contain("1657174953"));
            Assert.That(row.Query<Label>().ToList().All(label => !label.text.Contains("1657174953")), Is.True);
            Assert.That(picker.Q<Button>("enter-external-id-manually").text,
                Is.EqualTo("Advanced: Enter ID manually"));
        }

        [Test]
        public void BoundAggregateResultRowSelectsItsExactEntry()
        {
            ExternalGdoCatalogRepository repository = ReadyRepository(_path);
            ExternalGdoCatalogEntry selected = null;
            var picker = new ExternalGdoPicker("KitchenData.Item", repository, entry => selected = entry, false);
            ListView results = picker.Q<ListView>("vanilla-results");
            VisualElement row = results.makeItem();
            results.bindItem(row, 0);

            typeof(ExternalGdoPicker).GetMethod("SelectBoundRow", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(picker, new object[] { row });

            Assert.That(selected, Is.SameAs(row.userData));
            Assert.That(selected.ProviderKey, Is.EqualTo(ExternalGdoCatalogRepository.VanillaProviderKey));
        }

        [Test]
        public void CompleteCatalogueIsVirtualizedAndCombinesExactTypeWithTextSearch()
        {
            var entries = new List<VanillaGdoCatalogEntryData>();
            for (int index = 0; index < 125; index++)
                entries.Add(Entry(1000 + index, "Item " + index, "KitchenData.Item"));
            entries.Add(Entry(2001, "First Sink", "KitchenData.Appliance"));
            entries.Add(Entry(2002, "Second Sink", "KitchenData.Appliance"));
            ExternalGdoCatalogRepository repository = ReadyRepository(_path, entries.ToArray());
            var picker = new ExternalGdoPicker(null, repository, _ => { }, false);

            Assert.That(picker.ResultCount, Is.EqualTo(127));
            Assert.That(picker.Q<ListView>("vanilla-results").itemsSource.Count, Is.EqualTo(127));
            Assert.That(picker.Q<PopupField<string>>("vanilla-type-filter").choices, Is.EqualTo(new[]
                { "All types", "KitchenData.Appliance", "KitchenData.Item" }));

            picker.SetRuntimeType("KitchenData.Appliance");
            picker.ApplyQuery("second");

            Assert.That(picker.SelectedRuntimeType, Is.EqualTo("KitchenData.Appliance"));
            Assert.That(picker.ResultCount, Is.EqualTo(1));
            Assert.That(picker.Search("second").Single().RuntimeId, Is.EqualTo(2002));
            Assert.That(picker.Q<Label>("vanilla-result-count").text, Is.EqualTo("1 result"));
        }

        [Test]
        public void ExpectedTypeBoundsExactTypeChoices()
        {
            ExternalGdoCatalogRepository repository = ReadyRepository(_path);
            var picker = new ExternalGdoPicker("KitchenData.Item", repository, _ => { }, false);

            Assert.That(picker.Q<PopupField<string>>("vanilla-type-filter").choices,
                Is.EqualTo(new[] { "All types", "KitchenData.Item" }));
            Assert.Throws<ArgumentException>(() => picker.SetRuntimeType("KitchenData.Appliance"));
        }

        [Test]
        public void MissingCatalogShowsBoundedStatusAndKeepsManualFallbackAvailable()
        {
            var vanilla = new VanillaGdoCatalogRepository(_path);
            vanilla.Reload();
            var repository = new ExternalGdoCatalogRepository(vanilla, null);
            repository.Reload();
            var picker = new ExternalGdoPicker("KitchenData.Item", repository, _ => { });

            Assert.That(picker.Q<HelpBox>("vanilla-catalog-status").text, Does.Contain("No external GDO catalogues"));
            Assert.That(picker.Q<Button>("enter-external-id-manually"), Is.Not.Null);
            picker.SetManualExpanded(true);
            Assert.That(picker.ManualExpanded, Is.True);
        }

        [Test]
        public void ProviderFilterDefaultsToAllProvidersAndListsPlateUp()
        {
            ExternalGdoCatalogRepository repository = ReadyRepository(_path);
            var picker = new ExternalGdoPicker("KitchenData.Item", repository, _ => { }, false);

            Assert.That(picker.Q<PopupField<string>>("external-provider-filter").choices,
                Is.EqualTo(new[] { "All providers", "PlateUp" }));
            Assert.That(picker.SelectedProviderKey, Is.Null);
        }

        [Test]
        public void ProvidersWithTheSameDisplayNameRemainIndependentlySelectable()
        {
            string root = Path.Combine(Path.GetTempPath(), "picker-providers-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(root);
                WriteImported(root, "a", "workshop:a", "Shared Name", 301, "First");
                WriteImported(root, "b", "workshop:b", "Shared Name", 302, "Second");
                VanillaGdoCatalogRepository vanilla = ReadyVanillaRepository(_path, new[]
                {
                    Entry(1657174953, "Water", "KitchenData.Item"),
                    Entry(200, "Sink", "KitchenData.Appliance")
                });
                var aggregate = new ExternalGdoCatalogRepository(vanilla, root);
                aggregate.Reload();
                var picker = new ExternalGdoPicker("KitchenData.Item", aggregate, _ => { }, false);
                PopupField<string> providers = picker.Q<PopupField<string>>("external-provider-filter");

                Assert.That(providers.choices, Does.Contain("Shared Name · workshop:a"));
                Assert.That(providers.choices, Does.Contain("Shared Name · workshop:b"));

                picker.SetProvider("workshop:b");

                Assert.That(picker.SelectedProviderKey, Is.EqualTo("workshop:b"));
                Assert.That(picker.Search(string.Empty).Select(entry => entry.DisplayName), Is.EqualTo(new[] { "Second" }));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void ReferenceFieldSelectionStoresOnlyNumericTokenAndDeclaredExpectedType()
        {
            ExternalGdoCatalogRepository repository = ReadyRepository(_path);
            GdoOverlayValue changed = null;
            var field = new GdoReferenceField("Result", "KitchenData.Item", GdoOverlayValue.Null(), value => changed = value, repository);

            field.Picker.Select(repository.Find(ExternalGdoCatalogRepository.VanillaProviderKey, 1657174953, "KitchenData.Item"));

            Assert.That(changed.ReferenceValue.Id, Is.EqualTo(1657174953));
            Assert.That(changed.ReferenceValue.ExpectedType, Is.EqualTo("KitchenData.Item"));
            Assert.That(changed.ReferenceValue.Asset, Is.Null);
            Assert.That(field.Q<IntegerField>("external-gdo-id").parent.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        internal static ExternalGdoCatalogRepository ReadyRepository(string path)
            => ReadyRepository(path, new[]
            {
                Entry(1657174953, "Water", "KitchenData.Item"),
                Entry(200, "Sink", "KitchenData.Appliance")
            });

        private static ExternalGdoCatalogRepository ReadyRepository(string path,
            VanillaGdoCatalogEntryData[] entries)
        {
            var repository = new ExternalGdoCatalogRepository(ReadyVanillaRepository(path, entries), null);
            repository.Reload();
            return repository;
        }

        private static VanillaGdoCatalogRepository ReadyVanillaRepository(string path,
            VanillaGdoCatalogEntryData[] entries)
        {
            var data = new VanillaGdoCatalogData
            {
                schemaVersion = VanillaGdoCatalogFile.SchemaVersion, providerId = "plateup", providerName = "PlateUp", steamAppId = 1599600,
                importerVersion = "1", unityVersion = "2020.3.48f1", sourceAssemblyName = "Kitchen.GameData",
                sourceAssemblySha256 = new string('A', 64), schemaFingerprint = GdoDescriptorCatalog.GlobalFingerprint,
                assets = new VanillaGdoCatalogAssetData[0], relationships = new ExternalGdoRelationshipData[0], diagnostics = new VanillaGdoCatalogDiagnosticData[0],
                entries = entries
            };
            File.WriteAllBytes(path, VanillaGdoCatalogFile.Serialize(data));
            var repository = new VanillaGdoCatalogRepository(path);
            Assert.That(repository.Reload(), Is.True);
            return repository;
        }

        private static VanillaGdoCatalogEntryData Entry(int id, string name, string runtimeType) =>
            new VanillaGdoCatalogEntryData { id = id, name = name, runtimeType = runtimeType,
                assemblyName = "Kitchen.GameData", locations = new VanillaGdoCatalogLocationData[0] };

        private static void WriteImported(string root, string folder, string providerKey,
            string providerName, int id, string name)
        {
            string directory = Path.Combine(root, folder);
            Directory.CreateDirectory(directory);
            ModGdoCatalogData data = ModGdoRelationshipValueTests.CreateCatalog();
            data.providers[0].key = providerKey;
            data.providers[0].displayName = providerName;
            data.entries[0].providerKey = providerKey;
            data.entries[0].runtimeId = id;
            data.entries[0].displayName = name;
            File.WriteAllBytes(Path.Combine(directory, "mod-gdo-catalog.json"), ModGdoCatalogFile.Serialize(data));
        }
    }
}
