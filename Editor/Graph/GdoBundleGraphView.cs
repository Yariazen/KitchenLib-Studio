using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

using KitchenLib.Studio.Editor.Bootstrap;
using KitchenLib.Studio.Editor.Catalogs.Mods;
using KitchenLib.Studio.Editor.Catalogs.Vanilla;
using KitchenLib.Studio.Editor.Snapshots;

namespace KitchenLib.Studio.Editor.Graph
{
    internal sealed class GdoBundleGraphView : GraphView
    {
        private readonly Dictionary<string, GraphElement> _elements = new Dictionary<string, GraphElement>();
        private readonly Dictionary<string, GdoGraphOwnerGroup> _ownerGroups = new Dictionary<string, GdoGraphOwnerGroup>();
        private GdoBundleGraphAsset _graph;
        private GdoGraphConnectionController _connectionController;
        private bool _rebuilding;
        private readonly Action _graphChanged;
        private readonly Action<string> _renameGdo;
        private readonly Func<string, bool> _confirmMemberRemoval;
        private readonly Func<IExternalGdoSnapshotService> _snapshotServiceFactory;
        private readonly Func<GraphElement, bool> _snapshotVisibility;
        private readonly Dictionary<string, IDisposable> _snapshotSubscriptions =
            new Dictionary<string, IDisposable>(StringComparer.Ordinal);
        private IExternalGdoSnapshotService _snapshotService;
        private bool _snapshotVisibilityScheduled;
        private int _snapshotGeneration;
        private bool _edgeGeometryRefreshScheduled;
        private readonly Action<string> _createGdo;
        private readonly Action _addSelected;
        private readonly Action _addExternal;
        private readonly Action _renameProject;
        private readonly Action _collectOverlays;
        private readonly ExternalGdoCatalogRepository _externalRepository;

        internal GdoBundleGraphView(Action graphChanged = null, Action<string> renameGdo = null,
            Func<string, bool> confirmMemberRemoval = null,
            Func<IExternalGdoSnapshotService> snapshotServiceFactory = null,
            Func<GraphElement, bool> snapshotVisibility = null,
            Action<string> createGdo = null, Action addSelected = null, Action addExternal = null,
            Action renameProject = null, Action collectOverlays = null,
            ExternalGdoCatalogRepository externalRepository = null)
        {
            _graphChanged = graphChanged;
            _renameGdo = renameGdo;
            _confirmMemberRemoval = confirmMemberRemoval ?? (_ => EditorUtility.DisplayDialog("Remove Member",
                "Remove this member, its owned relationships and all connected edges? The source asset is preserved.",
                "Remove Member and Edges", "Cancel"));
            _snapshotServiceFactory = snapshotServiceFactory;
            _snapshotVisibility = snapshotVisibility ?? DefaultSnapshotVisibility;
            _createGdo = createGdo;
            _addSelected = addSelected;
            _addExternal = addExternal;
            _renameProject = renameProject;
            _collectOverlays = collectOverlays;
            _externalRepository = externalRepository ?? ExternalGdoCatalogRepository.Current;
            name = "gdo-bundle-graph-view";
            style.flexGrow = 1;
            StyleSheet controls = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                KitchenLibStudioGeneratedPaths.PackageAssetPath("Editor/Graph/GdoGraphControls.uss"));
            if (controls != null) styleSheets.Add(controls);
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            MiniMap map = new MiniMap { anchored = true };
            map.SetPosition(new Rect(10, 30, 180, 120));
            Add(map);
            HasMiniMap = true;
            graphViewChanged = OnGraphViewChanged;
            elementsAddedToGroup = HandleGroupMembershipChanged;
            elementsRemovedFromGroup = HandleGroupMembershipChanged;
            RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            RegisterCallback<ContextualMenuPopulateEvent>(RouteOwnerGroupContextMenu,
                TrickleDown.TrickleDown);
            RegisterCallback<ContextualMenuPopulateEvent>(BuildCanvasContextMenu);
            RegisterCallback<GeometryChangedEvent>(_ =>
            {
                ScheduleVisibleExternalSnapshots();
                ScheduleEdgeGeometryRefresh();
            });
            viewTransformChanged += _ =>
            {
                ScheduleVisibleExternalSnapshots();
                ScheduleEdgeGeometryRefresh();
            };
        }

        public bool HasMiniMap { get; }
        internal bool IsEdgeGeometryRefreshPending => _edgeGeometryRefreshScheduled;
        public string FocusedElementId { get; private set; }
        public GdoBundleGraphAsset Graph => _graph;

        public void BuildCanvasContextMenu(ContextualMenuPopulateEvent evt)
        {
            for (VisualElement target = evt.target as VisualElement; target != null && target != this; target = target.parent)
                if (target is GraphElement) return;

            bool hasGraph = _graph != null;
            bool canCreate = hasGraph && _graph.HasAuthorIdentity;
            GdoCreateMenuPresenter.Populate(evt.menu, "Create GDO/",
                GdoCreateMenuCatalog.Build(GdoGraphAssetCommands.SupportedRuntimeTypes),
                value => _createGdo?.Invoke(value), () => canCreate && _createGdo != null);
            evt.menu.AppendAction("Add Selected GDO", _ => _addSelected?.Invoke(),
                _ => hasGraph && _addSelected != null ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            evt.menu.AppendAction("External Reference…", _ => _addExternal?.Invoke(),
                _ => hasGraph && _addExternal != null ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            foreach (var definition in StudioGraphNodeRegistry.All)
            {
                string typeId = definition.TypeId;
                evt.menu.AppendAction("Create Custom/" + definition.DisplayName + " (" + typeId + ")", _ =>
                {
                    Vector2 location = this.ChangeCoordinatesTo(contentViewContainer, evt.localMousePosition);
                    var record = GdoBundleGraphRepository.AddCustomNode(_graph, typeId, new Rect(location, new Vector2(280, 180)));
                    Load(_graph, record.Id);
                    _graphChanged?.Invoke();
                }, _ => hasGraph ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            }
            evt.menu.AppendAction("Create/Prefab", _ => CreatePrefab(evt.localMousePosition),
                _ => hasGraph ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            evt.menu.AppendSeparator();
            evt.menu.AppendAction("Rename Project…", _ => _renameProject?.Invoke(),
                _ => hasGraph && _renameProject != null ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            evt.menu.AppendAction("Collect Project Overlays", _ => _collectOverlays?.Invoke(),
                _ => hasGraph && _collectOverlays != null ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
        }

        private void RouteOwnerGroupContextMenu(ContextualMenuPopulateEvent evt)
        {
            Vector2 graphPosition = this.ChangeCoordinatesTo(
                contentViewContainer, evt.localMousePosition);
            TryBuildOwnerGroupContextMenu(evt, graphPosition);
        }

        private bool TryBuildOwnerGroupContextMenu(
            ContextualMenuPopulateEvent evt, Vector2 graphPosition)
        {
            GdoGraphOwnerGroup group = _ownerGroups.Values.Reverse()
                .FirstOrDefault(candidate => candidate.GetPosition().Contains(graphPosition));
            if (group == null) return false;

            VisualElement target = evt.target as VisualElement;
            if (target != null && group.containedElements.Any(element =>
                ReferenceEquals(element, target) || element.Contains(target))) return false;

            group.BuildContextualMenu(evt);
            return true;
        }

        public void Load(GdoBundleGraphAsset graph, string focusElementId = null)
        {
            DisposeSnapshotThumbnails();
            if (graph != null) GdoBundleGraphRepository.EnsureCurrentSchema(graph);
            _rebuilding = true;
            _graph = graph;
            _connectionController = graph == null ? null : new GdoGraphConnectionController(graph);
            DeleteElements(graphElements.ToList());
            _elements.Clear();
            _ownerGroups.Clear();
            if (graph == null) { _rebuilding = false; return; }
            foreach (GdoGraphNodeRecord record in graph.Nodes)
            {
                GraphElement element = record.Kind == GdoGraphNodeKind.Local
                    ? (GraphElement)new GdoGraphNodeView(record,
                        null, _graphChanged,
                        _renameGdo == null ? (Action)null : () => _renameGdo(record.Id),
                        () => _connectionController.RelationshipChoices(record.Id),
                        path => CreateRelationship(record.Id, path),
                        path => _connectionController.RelationshipChoices(record.Id)
                            .Any(choice => choice.FieldPath == path && (choice.IsRepeated || !graph.Relationships
                                .Any(relationship => relationship.OwnerNodeId == record.Id && relationship.FieldPath == path))),
                        () => RemoveMemberFromAction(record.Id), true, value =>
                        {
                            GdoBundleGraphRepository.SetPropertiesExpanded(graph, record.Id, value);
                            _graphChanged?.Invoke();
                        })
                    : new GdoGraphExternalNodeView(record, () => RemoveMemberFromAction(record.Id), null,
                        _externalRepository,
                        () => _externalRepository.Related(record.ExternalProviderKey, record.ExternalId, record.ExpectedType),
                        entry => AddRelatedExternal(record.Id, entry));
                _elements.Add(record.Id, element);
                AddElement(element);
            }
            foreach (GdoGraphRelationshipRecord record in graph.Relationships)
            {
                var element = new GdoGraphRelationshipNodeView(record, graph, () =>
                {
                    ReconcileEdges();
                    _graphChanged?.Invoke();
                }, () => RemoveRelationshipFromAction(record.Id),
                    delta => MoveRelationshipFromAction(record.Id, delta),
                    delta => CanMoveRelationship(record.Id, delta), value =>
                    {
                        GdoBundleGraphRepository.SetRelationshipExpanded(graph, record.Id, value);
                        _graphChanged?.Invoke();
                    });
                _elements.Add(record.Id, element);
                AddElement(element);
            }
            foreach (GdoGraphPrefabRecord record in graph.Prefabs)
            {
                var element = new GdoGraphPrefabNodeView(graph, record, () =>
                {
                    Load(graph, record.Id);
                    _graphChanged?.Invoke();
                }, () => RemovePrefabFromAction(record.Id));
                _elements.Add(record.Id, element);
                AddElement(element);
            }
            foreach (StudioCustomGraphNodeRecord record in graph.CustomNodes)
            {
                var element = new StudioCustomGraphNodeView(graph, record, () =>
                {
                    schedule.Execute(() => Load(graph, record.Id));
                    _graphChanged?.Invoke();
                }, () => RemoveCustomNodeFromAction(record.Id));
                _elements.Add(record.Id, element);
                AddElement(element);
            }
            foreach (GdoGraphNodeRecord owner in graph.Nodes.Where(node => node.Kind == GdoGraphNodeKind.Local))
            {
                string groupTitle = owner.IsBroken ? owner.CachedName : owner.Asset.name;
                var group = new GdoGraphOwnerGroup(owner.Id, groupTitle, owner.PresentationMode,
                    mode =>
                    {
                        GdoBundleGraphRepository.SetPresentationMode(graph, owner.Id, mode);
                        _graphChanged?.Invoke();
                    }, () => RemoveMemberFromAction(owner.Id), ScheduleEdgeGeometryRefresh,
                    () => _connectionController.RelationshipChoices(owner.Id),
                    path => CreateRelationship(owner.Id, path),
                    path => _connectionController.RelationshipChoices(owner.Id)
                        .Any(choice => choice.FieldPath == path && (choice.IsRepeated || !graph.Relationships
                            .Any(relationship => relationship.OwnerNodeId == owner.Id && relationship.FieldPath == path))));
                _ownerGroups.Add(owner.Id, group);
                AddElement(group);
                group.AddDerivedElement(owner.Id, _elements[owner.Id]);
                foreach (GdoGraphRelationshipRecord relationship in
                    GdoBundleGraphRepository.OrderedRelationships(graph, owner.Id))
                    group.AddDerivedElement(relationship.Id, _elements[relationship.Id]);
                group.SetConnections(graph.Edges);
                group.PrimeInitialLayout();
                group.RestorePosition(owner.GroupPosition);
            }
            ReconcileEdges();
            ScheduleEdgeGeometryRefresh();
            _rebuilding = false;
            ScheduleVisibleExternalSnapshots();
            if (!string.IsNullOrEmpty(focusElementId)) Focus(focusElementId);
        }

        internal void RefreshVisibleExternalSnapshots()
        {
            if (_graph == null) return;
            foreach (KeyValuePair<string, GraphElement> pair in _elements.Where(value =>
                value.Value is GdoGraphExternalNodeView).ToArray())
            {
                bool visible = _snapshotVisibility(pair.Value);
                if (!visible)
                {
                    if (_snapshotSubscriptions.TryGetValue(pair.Key, out IDisposable subscription))
                    {
                        subscription.Dispose();
                        _snapshotSubscriptions.Remove(pair.Key);
                    }
                    continue;
                }
                var node = (GdoGraphExternalNodeView)pair.Value;
                GdoGraphNodeRecord record = _graph.FindNode(pair.Key);
                if (!ExternalGdoSnapshotEligibility.IsEligible(record.ExpectedType))
                {
                    if (_snapshotSubscriptions.TryGetValue(pair.Key, out IDisposable subscription))
                    {
                        subscription.Dispose();
                        _snapshotSubscriptions.Remove(pair.Key);
                    }
                    node.SetSnapshotResult(ExternalGdoSnapshotResults.Unsupported());
                    continue;
                }
                if (_snapshotSubscriptions.ContainsKey(pair.Key)) continue;
                if (_snapshotService == null)
                    _snapshotService = _snapshotServiceFactory?.Invoke() ?? CreateDefaultSnapshotService();
                int generation = _snapshotGeneration;
                Action<ExternalGdoSnapshotResult> completed = result =>
                {
                    if (generation != _snapshotGeneration || !_elements.TryGetValue(pair.Key, out GraphElement current) ||
                        !ReferenceEquals(current, node)) return;
                    node.SetSnapshotResult(result);
                };
                IDisposable handle = _snapshotService.Request(record.ExternalProviderKey, node.ExternalId,
                    record.ExpectedType, completed);
                _snapshotSubscriptions[pair.Key] = handle;
            }
        }

        public void DisposeSnapshotThumbnails()
        {
            _snapshotGeneration++;
            foreach (IDisposable subscription in _snapshotSubscriptions.Values) subscription?.Dispose();
            _snapshotSubscriptions.Clear();
            _snapshotService?.Dispose();
            _snapshotService = null;
            _snapshotVisibilityScheduled = false;
        }

        private void ScheduleVisibleExternalSnapshots()
        {
            if (_snapshotVisibilityScheduled) return;
            _snapshotVisibilityScheduled = true;
            schedule.Execute(() =>
            {
                _snapshotVisibilityScheduled = false;
                RefreshVisibleExternalSnapshots();
            });
        }

        private bool DefaultSnapshotVisibility(GraphElement element) => IsSnapshotVisible(
            element.worldBound, worldBound, element.panel != null, element.resolvedStyle.display);

        internal static bool IsSnapshotVisible(Rect nodeBounds, Rect viewportBounds, bool attached,
            DisplayStyle display)
        {
            return attached && display != DisplayStyle.None && nodeBounds.width > 0f && nodeBounds.height > 0f &&
                viewportBounds.width > 0f && viewportBounds.height > 0f && nodeBounds.Overlaps(viewportBounds);
        }

        private static IExternalGdoSnapshotService CreateDefaultSnapshotService()
        {
            string path = KitchenLibStudioGeneratedPaths.VanillaSnapshotCatalogPath;
            string indexPath = KitchenLibStudioGeneratedPaths.VanillaSnapshotIndexPath;
            if (!File.Exists(path)) return Wrap(new UnavailableSnapshotService(
                "Refresh the snapshot catalogue to generate external previews."));
            if (File.Exists(indexPath))
                return Wrap(CreateIndexedSnapshotService(indexPath,
                    () => CreateFullSnapshotServiceAsync(path, new SnapshotCache())));
            return Wrap(CreateFullSnapshotService(path, new SnapshotCache()));
        }

        private static IExternalGdoSnapshotService CreateIndexedSnapshotService(string indexPath,
            Func<System.Threading.Tasks.Task<IExternalGdoSnapshotService>> fallbackFactory)
        {
            try
            {
                ExternalGdoSnapshotIndexData index = ExternalGdoSnapshotIndexFile.Read(indexPath);
                return new IndexedExternalGdoSnapshotService(index, new SnapshotCache(), fallbackFactory);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is IOException ||
                exception is UnauthorizedAccessException || exception is InvalidDataException)
            {
                return new UnavailableSnapshotService("Snapshot index could not be loaded: " + exception.Message);
            }
        }

        private static IExternalGdoSnapshotService Wrap(IExternalGdoSnapshotService vanilla) =>
            new ImportedGdoSnapshotService(vanilla, ExternalGdoCatalogRepository.Current);

        private static IExternalGdoSnapshotService CreateFullSnapshotService(string path, SnapshotCache cache)
        {
            try
            {
                VanillaAssetCatalogData catalog = VanillaAssetCatalogFile.Read(path);
                var repository = new VanillaAssetCatalogRepository(path);
                if (!repository.Reload()) return new UnavailableSnapshotService(
                    "The snapshot catalogue could not be loaded.");
                PlateUpInstallation installation = PlateUpCatalogImportMenu.ResolveInstallation(false);
                return ExternalGdoSnapshotService.CreateDefault(catalog, repository, installation?.GameRoot, cache);
            }
            catch (Exception exception) { return new UnavailableSnapshotService(exception.Message); }
        }

        private static async System.Threading.Tasks.Task<IExternalGdoSnapshotService> CreateFullSnapshotServiceAsync(
            string path, SnapshotCache cache)
        {
            VanillaAssetCatalogData catalog = await System.Threading.Tasks.Task.Run(() => VanillaAssetCatalogFile.Read(path));
            var repository = new VanillaAssetCatalogRepository(catalog);
            PlateUpInstallation installation = PlateUpCatalogImportMenu.ResolveInstallation(false);
            return ExternalGdoSnapshotService.CreateDefault(catalog, repository, installation?.GameRoot, cache);
        }

        private sealed class UnavailableSnapshotService : IExternalGdoSnapshotService
        {
            private readonly string _diagnostic;
            public UnavailableSnapshotService(string diagnostic) { _diagnostic = diagnostic; }
            public IDisposable Request(string providerKey, int gdoId, string runtimeType,
                Action<ExternalGdoSnapshotResult> completed)
            {
                completed(new ExternalGdoSnapshotResult(ExternalGdoSnapshotState.Unavailable,
                    diagnostic: _diagnostic));
                return CompletedSnapshotSubscription.Instance;
            }
            public void Dispose() { }
        }

        public void ReconcileEdges()
        {
            if (_graph == null) return;
            bool wasRebuilding = _rebuilding;
            _rebuilding = true;
            DeleteElements(edges.ToList());
            foreach (GdoGraphEdgeRecord record in _graph.Edges)
            {
                Port output = PortFor(record.OutputNodeId, record.OutputPortPath, Direction.Output);
                Port input = PortFor(record.InputNodeId, record.InputPortPath, Direction.Input);
                if (output == null || input == null) continue;
                string runtimeType = GdoGraphConnectionTypeResolver.ExactOutputType(
                    _graph, record.OutputNodeId, record.OutputPortPath);
                var edge = new GdoGraphEdgeView(record.Id, runtimeType)
                    { output = output, input = input, userData = record.Id };
                output.Connect(edge); input.Connect(edge); AddElement(edge);
            }
            foreach (GdoGraphOwnerGroup group in _ownerGroups.Values)
                group.SetConnections(_graph.Edges);
            _rebuilding = wasRebuilding;
        }

        public GraphElement FindElement(string elementId) =>
            elementId != null && _elements.TryGetValue(elementId, out GraphElement element) ? element : null;

        public GdoGraphOwnerGroup FindOwnerGroup(string ownerNodeId) =>
            ownerNodeId != null && _ownerGroups.TryGetValue(ownerNodeId, out GdoGraphOwnerGroup group) ? group : null;

        public void ReconcileOwnerGroups()
        {
            if (_graph == null) return;
            bool wasRebuilding = _rebuilding;
            _rebuilding = true;
            foreach (KeyValuePair<string, GdoGraphOwnerGroup> pair in _ownerGroups)
            {
                var desired = new List<KeyValuePair<string, GraphElement>>
                {
                    new KeyValuePair<string, GraphElement>(pair.Key, _elements[pair.Key])
                };
                desired.AddRange(GdoBundleGraphRepository.OrderedRelationships(_graph, pair.Key)
                    .Select(value => new KeyValuePair<string, GraphElement>(value.Id, _elements[value.Id])));
                pair.Value.SynchronizeDerivedElements(desired);
            }
            _rebuilding = wasRebuilding;
        }

        public void PersistMovedElements(IEnumerable<GraphElement> movedElements, Vector2 moveDelta = default)
        {
            if (_graph == null || movedElements == null) return;
            var positions = new Dictionary<string, Rect>();
            var groupPositions = new Dictionary<string, Vector2>();
            foreach (GraphElement element in movedElements)
            {
                if (element is GdoGraphOwnerGroup group)
                {
                    AddGroupPosition(group, moveDelta, groupPositions);
                    continue;
                }
                if (_ownerGroups.Values.Any(group => group.containedElements.Contains(element))) continue;
                AddPosition(element, positions);
            }
            if (positions.Count > 0)
            {
                GdoBundleGraphRepository.SetElementPositions(_graph, positions);
                ScheduleEdgeGeometryRefresh();
            }
            foreach (KeyValuePair<string, Vector2> value in groupPositions)
                GdoBundleGraphRepository.SetGroupPosition(_graph, value.Key, value.Value);
            if (groupPositions.Count > 0) ScheduleEdgeGeometryRefresh();
        }

        private void ScheduleEdgeGeometryRefresh()
        {
            if (_edgeGeometryRefreshScheduled) return;
            _edgeGeometryRefreshScheduled = true;
            schedule.Execute(() =>
            {
                _edgeGeometryRefreshScheduled = false;
                foreach (Edge edge in edges.ToList())
                {
                    edge.UpdateEdgeControl();
                    edge.MarkDirtyRepaint();
                }
            });
        }

        private void AddGroupPosition(GdoGraphOwnerGroup group, Vector2 moveDelta,
            IDictionary<string, Vector2> positions)
        {
            GdoGraphNodeRecord owner = _graph.FindNode(group.OwnerNodeId);
            if (owner == null) return;
            Vector2 position = owner.GroupPosition + moveDelta;
            if (moveDelta == Vector2.zero && IsFinitePosition(group.GetPosition().position))
                position = group.GetPosition().position;
            if (!IsFinitePosition(position)) return;
            group.ReleasePositionRestore();
            positions[owner.Id] = position;
        }

        private void AddPosition(GraphElement element, IDictionary<string, Rect> positions)
        {
            string id = _elements.FirstOrDefault(pair => ReferenceEquals(pair.Value, element)).Key;
            Rect position = PositionInGraph(element);
            if (!string.IsNullOrEmpty(id) && IsFinite(position)) positions[id] = position;
        }

        private Rect PositionInGraph(GraphElement element)
        {
            return element.GetPosition();
        }

        private static bool IsFinite(Rect position) =>
            !float.IsNaN(position.x) && !float.IsInfinity(position.x) &&
            !float.IsNaN(position.y) && !float.IsInfinity(position.y) &&
            !float.IsNaN(position.width) && !float.IsInfinity(position.width) &&
            !float.IsNaN(position.height) && !float.IsInfinity(position.height);

        private static bool IsFinitePosition(Vector2 position) =>
            !float.IsNaN(position.x) && !float.IsInfinity(position.x) &&
            !float.IsNaN(position.y) && !float.IsInfinity(position.y);

        public bool SearchAndFrame(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            string query = text.Trim();
            GdoGraphOwnerGroup group = _ownerGroups.Values.FirstOrDefault(value =>
                (value.title ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
            if (group != null)
            {
                ClearSelection(); AddToSelection(group); FocusedElementId = group.OwnerNodeId; FrameSelection(); return true;
            }
            KeyValuePair<string, GraphElement> match = _elements.FirstOrDefault(pair => Matches(pair.Value, query));
            if (match.Value == null) return false;
            Focus(match.Key);
            return true;
        }

        public void Focus(string elementId)
        {
            GraphElement element = FindElement(elementId);
            if (element == null) return;
            ClearSelection();
            AddToSelection(element);
            FocusedElementId = elementId;
            FrameSelection();
        }

        public void PersistPosition(string elementId, Rect position)
        {
            if (_graph == null || FindElement(elementId) == null) return;
            GdoBundleGraphRepository.SetElementPositions(_graph, new Dictionary<string, Rect> { { elementId, position } });
            FindElement(elementId).SetPosition(position);
        }

        private bool CanMoveRelationship(string relationshipId, int delta)
        {
            GdoGraphRelationshipRecord relationship = _graph?.FindRelationship(relationshipId);
            if (relationship == null || (delta != -1 && delta != 1)) return false;
            GdoGraphRelationshipRecord[] siblings = GdoBundleGraphRepository
                .OrderedRelationships(_graph, relationship.OwnerNodeId).ToArray();
            int index = Array.IndexOf(siblings, relationship);
            int destination = index + delta;
            return index >= 0 && destination >= 0 && destination < siblings.Length;
        }

        private void MoveRelationshipFromAction(string relationshipId, int delta)
        {
            if (!_connectionController.MoveRelationship(relationshipId, delta)) return;
            ReconcileOwnerGroups();
            _graphChanged?.Invoke();
        }

        public void FrameEverything()
        {
            if (_graph == null || !_elements.Any()) return;
            FrameAll();
        }

        public GdoGraphEdgeRecord Connect(string outputNodeId, string inputNodeId, string inputPortPath, int order)
        {
            if (_graph == null) throw new InvalidOperationException("Select a bundle graph before connecting GDOs.");
            GdoGraphEdgeRecord record = _connectionController.Connect(outputNodeId, inputNodeId, inputPortPath, order);
            ReconcileEdges();
            _graphChanged?.Invoke();
            return record;
        }

        public GdoGraphRelationshipRecord CreateRelationship(string ownerNodeId, string fieldPath)
        {
            if (_graph == null) throw new InvalidOperationException("Select a bundle graph before creating relationships.");
            GdoGraphNodeRecord owner = _graph.Nodes.Single(node => node.Id == ownerNodeId);
            Rect position = new Rect(owner.Position.xMax + 40, owner.Position.y, 260, 180);
            GdoGraphRelationshipRecord record = _connectionController.CreateNextRelationship(ownerNodeId, fieldPath, position);
            Load(_graph, record.Id);
            _graphChanged?.Invoke();
            return record;
        }

        public void RemoveRelationship(string relationshipId, bool removeConnectedEdges)
        {
            if (_graph == null) throw new InvalidOperationException("Select a bundle graph before removing relationships.");
            string ownerId = _graph.Relationships.Single(value => value.Id == relationshipId).OwnerNodeId;
            _connectionController.RemoveRelationship(relationshipId, removeConnectedEdges);
            Load(_graph, ownerId);
            _graphChanged?.Invoke();
        }

        private void RemoveRelationshipFromAction(string relationshipId)
        {
            bool connected = _graph.Edges.Any(edge => edge.InputNodeId == relationshipId || edge.OutputNodeId == relationshipId);
            if (connected && !EditorUtility.DisplayDialog("Remove Relationship",
                "Remove this relationship and all its connected edges?", "Remove Relationship and Edges", "Cancel")) return;
            RemoveRelationship(relationshipId, connected);
        }

        public void RemoveMember(string nodeId, bool removeConnectedEdges)
        {
            if (_graph == null) throw new InvalidOperationException("Select a bundle graph before removing members.");
            _connectionController.RemoveMember(nodeId, removeConnectedEdges);
            Load(_graph);
            _graphChanged?.Invoke();
        }

        private void CreatePrefab(Vector2 mousePosition)
        {
            if (_graph == null) return;
            Vector2 graphPosition = this.ChangeCoordinatesTo(contentViewContainer, mousePosition);
            GdoGraphPrefabRecord record = GdoBundleGraphRepository.AddPrefab(_graph, "New Prefab",
                new Rect(graphPosition, new Vector2(260, 220)));
            Load(_graph, record.Id);
            _graphChanged?.Invoke();
        }

        private void RemoveCustomNodeFromAction(string nodeId)
        {
            bool connected = _graph.HasConnections(nodeId);
            if (connected && !_confirmMemberRemoval(nodeId)) return;
            _connectionController.RemoveCustomNode(nodeId, connected);
            Load(_graph);
            _graphChanged?.Invoke();
        }

        private void RemovePrefabFromAction(string prefabId)
        {
            bool connected = _graph.HasConnections(prefabId);
            if (connected && !_confirmMemberRemoval(prefabId)) return;
            GdoBundleGraphRepository.RemovePrefab(_graph, prefabId, connected);
            GdoGraphPrefabProjection.TryApply(_graph, out _, true);
            Load(_graph);
            _graphChanged?.Invoke();
        }

        private void RemoveMemberFromAction(string nodeId)
        {
            GdoGraphNodeRecord node = _graph.FindNode(nodeId);
            if (node != null && node.Kind == GdoGraphNodeKind.Local && !node.IsBroken)
            {
                if (!_confirmMemberRemoval(nodeId)) return;
                GdoGraphAssetDeletion.Delete(_graph, nodeId);
                Load(_graph);
                _graphChanged?.Invoke();
                return;
            }
            bool connected = _graph.HasConnections(nodeId);
            if (connected && !_confirmMemberRemoval(nodeId)) return;
            RemoveMember(nodeId, connected);
        }

        private void AddRelatedExternal(string sourceNodeId, ExternalGdoCatalogEntry entry)
        {
            GdoGraphNodeRecord source = _graph?.FindNode(sourceNodeId);
            if (source == null || entry == null) return;
            int duplicateCount = _graph.Nodes.Count(node => node.Kind == GdoGraphNodeKind.External &&
                node.ExternalProviderKey == entry.ProviderKey && node.ExternalId == entry.RuntimeId &&
                node.ExpectedType == entry.RuntimeType);
            Vector2 offset = new Vector2(40f * (duplicateCount + 1), 40f * (duplicateCount + 1));
            Rect position = source.Position;
            position.position += offset;
            GdoGraphNodeRecord added = GdoBundleGraphRepository.AddExternal(_graph, entry.ProviderKey,
                entry.RuntimeId, entry.RuntimeType, position, entry.DisplayName);
            Load(_graph, added.Id);
            _graphChanged?.Invoke();
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Delete && evt.keyCode != KeyCode.Backspace) return;
            if (!RemoveSelectedOwnerGroups()) return;
            evt.StopImmediatePropagation();
        }

        private bool RemoveSelectedOwnerGroups()
        {
            string[] ownerIds = selection.OfType<GdoGraphOwnerGroup>()
                .Select(group => group.OwnerNodeId).Distinct(StringComparer.Ordinal).ToArray();
            if (ownerIds.Length == 0) return false;
            foreach (string ownerId in ownerIds)
                if (_graph?.FindNode(ownerId) != null)
                    RemoveMemberFromAction(ownerId);
            return true;
        }

        public void Disconnect(string edgeId)
        {
            if (_graph == null || !_graph.Edges.Any(edge => edge.Id == edgeId)) return;
            _connectionController.Disconnect(edgeId);
            ReconcileEdges();
            _graphChanged?.Invoke();
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            if (_graph == null) return new List<Port>();
            return ports.ToList().Where(candidate =>
            {
                if (candidate == startPort || candidate.direction == startPort.direction || candidate.node == startPort.node) return false;
                Port output = startPort.direction == Direction.Output ? startPort : candidate;
                Port input = startPort.direction == Direction.Input ? startPort : candidate;
                string outputId = ElementId(output.node);
                string inputId = ElementId(input.node);
                return _connectionController.CanConnect(outputId, output.userData as string,
                    inputId, input.userData as string);
            }).ToList();
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (_graph == null || _rebuilding) return change;
            if (change.movedElements != null)
                PersistMovedElements(change.movedElements, change.moveDelta);
            bool changed = false;
            if (change.edgesToCreate != null) foreach (Edge edge in change.edgesToCreate)
            {
                GdoGraphEdgeRecord record = _connectionController.Connect(ElementId(edge.output.node),
                    edge.output.userData as string, ElementId(edge.input.node),
                    edge.input.userData as string, _graph.Edges.Count);
                edge.userData = record.Id;
                changed = true;
            }
            if (change.elementsToRemove != null)
            {
                foreach (Edge edge in change.elementsToRemove.OfType<Edge>())
                    if (edge.userData is string edgeId && _graph.Edges.Any(item => item.Id == edgeId))
                    {
                        _connectionController.Disconnect(edgeId);
                        changed = true;
                    }
            }
            if (changed) _graphChanged?.Invoke();
            if (changed) schedule.Execute(ReconcileEdges);
            return change;
        }

        private void HandleGroupMembershipChanged(Group group, IEnumerable<GraphElement> elements)
        {
            if (_rebuilding || !(group is GdoGraphOwnerGroup)) return;
            ReconcileOwnerGroups();
        }

        private string ElementId(Node node) => _elements.First(pair => ReferenceEquals(pair.Value, node)).Key;

        private Port PortFor(string elementId, string path, Direction direction)
        {
            Node node = FindElement(elementId) as Node;
            if (node == null) return null;
            return node.Query<Port>().ToList().FirstOrDefault(port => port.direction == direction &&
                string.Equals(port.userData as string, path, StringComparison.Ordinal));
        }

        private static bool Matches(GraphElement element, string query)
        {
            Node node = element as Node;
            return node != null && ((node.title ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                (node.tooltip ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
}
