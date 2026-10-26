using System;
using KitchenData;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

using KitchenLib.Studio.Editor.Catalogs.Mods;
using KitchenLib.Studio.Editor.Snapshots;

using KitchenLib.Studio.Editor.Authoring;

namespace KitchenLib.Studio.Editor.Graph
{
    internal sealed class GdoGraphExternalNodeView : Node
    {
        private readonly Action _removeMember;
        private readonly Image _snapshot;
        private readonly Label _snapshotStatus;
        private readonly Func<System.Collections.Generic.IReadOnlyList<ExternalGdoRelatedEntry>> _related;
        private readonly Action<ExternalGdoCatalogEntry> _addRelated;

        internal GdoGraphExternalNodeView(GdoGraphNodeRecord record, Action removeMember = null,
            Action editReference = null, ExternalGdoCatalogRepository repository = null,
            Func<System.Collections.Generic.IReadOnlyList<ExternalGdoRelatedEntry>> related = null,
            Action<ExternalGdoCatalogEntry> addRelated = null)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            RecordId = record.Id;
            _removeMember = removeMember;
            _related = related;
            _addRelated = addRelated;
            capabilities &= ~Capabilities.Deletable;
            ExternalId = record.ExternalId;
            ExpectedType = record.ExpectedType;
            AddToClassList("kls-gdo-node--external");
            viewDataKey = RecordId;
            ExternalGdoCatalogRepository externalRepository = repository ?? ExternalGdoCatalogRepository.Current;
            ExternalGdoCatalogEntry entry = externalRepository.Find(record.ExternalProviderKey, ExternalId, ExpectedType);
            string displayName = entry == null ? record.CachedName : entry.DisplayName;
            title = string.IsNullOrEmpty(displayName) ? "Unknown External" : displayName;
            tooltip = title + "\n" + ExpectedType + "\n" + record.ExternalProviderKey + "\nID " + ExternalId;
            titleContainer.style.backgroundColor = new Color(0.32f, 0.25f, 0.45f, 1f);
            Port output = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(GameDataObject));
            output.portName = ShortName(ExpectedType);
            output.userData = "Value";
            GdoGraphTypePalette.Apply(output, ExpectedType);
            outputContainer.Add(output);
            if (ExternalGdoSnapshotEligibility.IsEligible(ExpectedType))
            {
                _snapshot = new Image { name = "external-snapshot", scaleMode = ScaleMode.ScaleToFit };
                _snapshot.style.width = ExternalGdoSnapshotCacheIdentity.ThumbnailSize;
                _snapshot.style.height = ExternalGdoSnapshotCacheIdentity.ThumbnailSize;
                _snapshot.style.alignSelf = Align.Center;
                _snapshotStatus = new Label("Snapshot pending") { name = "external-snapshot-status" };
                _snapshotStatus.style.unityTextAlign = TextAnchor.MiddleCenter;
                extensionContainer.Add(_snapshot);
                extensionContainer.Add(_snapshotStatus);
            }
            var provenance = new Label(entry == null ? record.ExternalProviderKey : entry.ProviderName) { name = "external-provenance" };
            extensionContainer.Add(provenance);
            extensionContainer.Add(new Label((entry == null ? ShortName(ExpectedType) : ShortName(entry.RuntimeType)) + " · ID " + ExternalId)
                { name = "external-summary" });
            if (entry != null && !GdoDescriptorCatalog.IsAssignable(entry.RuntimeType, ExpectedType))
                extensionContainer.Add(new HelpBox("Catalog type " + entry.RuntimeType + " is not assignable to " + ExpectedType + ".",
                    HelpBoxMessageType.Error) { name = "external-type-mismatch" });
            var edit = new Button(() => editReference?.Invoke()) { name = "edit-reference", text = "Edit Reference" };
            edit.SetEnabled(editReference != null);
            extensionContainer.Add(edit);
            SetPosition(record.Position);
            RefreshExpandedState();
            RefreshPorts();
        }

        public string RecordId { get; }
        public int ExternalId { get; }
        public string ExpectedType { get; }

        internal void SetSnapshotResult(ExternalGdoSnapshotResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (_snapshot == null || _snapshotStatus == null) return;
            _snapshot.image = result.State == ExternalGdoSnapshotState.Ready ? result.Texture : null;
            _snapshot.tooltip = result.Diagnostic;
            _snapshotStatus.tooltip = result.Diagnostic;
            switch (result.State)
            {
                case ExternalGdoSnapshotState.Ready:
                    _snapshot.style.display = DisplayStyle.Flex;
                    _snapshotStatus.style.display = DisplayStyle.None;
                    break;
                case ExternalGdoSnapshotState.Loading:
                    _snapshot.style.display = DisplayStyle.Flex;
                    _snapshotStatus.style.display = DisplayStyle.Flex;
                    _snapshotStatus.text = "Generating snapshot…";
                    break;
                case ExternalGdoSnapshotState.Unsupported:
                    _snapshot.style.display = DisplayStyle.None;
                    _snapshotStatus.style.display = DisplayStyle.Flex;
                    _snapshotStatus.text = "No snapshot for this GDO type";
                    break;
                case ExternalGdoSnapshotState.Unavailable:
                    _snapshot.style.display = DisplayStyle.None;
                    _snapshotStatus.style.display = DisplayStyle.Flex;
                    _snapshotStatus.text = "Snapshot unavailable";
                    break;
                default:
                    _snapshot.style.display = DisplayStyle.None;
                    _snapshotStatus.style.display = DisplayStyle.Flex;
                    _snapshotStatus.text = "Snapshot failed";
                    break;
            }
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            base.BuildContextualMenu(evt);
            System.Collections.Generic.IReadOnlyList<ExternalGdoRelatedEntry> related =
                _related?.Invoke() ?? Array.Empty<ExternalGdoRelatedEntry>();
            if (related.Count == 0)
                evt.menu.AppendAction("Related GDOs/None found", _ => { }, DropdownMenuAction.Status.Disabled);
            foreach (ExternalGdoRelatedEntry item in related)
            {
                ExternalGdoRelatedEntry captured = item;
                string direction = captured.Direction == ExternalGdoRelationshipDirection.Incoming
                    ? "Referenced By/" : string.Empty;
                string path = "Related GDOs/" + direction + MenuText(captured.Label) + "/" +
                    MenuText(captured.Entry.DisplayName);
                evt.menu.AppendAction(path, _ => _addRelated?.Invoke(captured.Entry),
                    _addRelated == null ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
            }
            evt.menu.AppendSeparator();
            evt.menu.AppendAction("Remove from graph", _ => _removeMember?.Invoke(),
                _removeMember == null ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
        }

        private static string MenuText(string value) => (value ?? "Related").Replace("/", "∕");

        private static string ShortName(string value)
        {
            int index = value == null ? -1 : value.LastIndexOf('.');
            return index < 0 ? value ?? "GDO" : value.Substring(index + 1);
        }
    }
}
