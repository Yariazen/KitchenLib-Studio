using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace KitchenLib.Studio.Editor
{
    public sealed class GdoGraphOwnerGroup : Group
    {
        private readonly HashSet<string> _derivedElementIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<GraphElement> _orderedElements = new List<GraphElement>();
        private readonly HashSet<GraphElement> _trackedElements = new HashSet<GraphElement>();
        private readonly Action<GdoGraphPresentationMode> _presentationChanged;
        private readonly Action _removeGdo;
        private readonly Action _layoutChanged;
        private readonly Func<IReadOnlyList<GdoRelationshipChoice>> _relationshipChoices;
        private readonly Action<string> _addRelationship;
        private readonly Func<string, bool> _canAddRelationship;
        private readonly Dictionary<GraphElement, string> _elementIds =
            new Dictionary<GraphElement, string>();
        private readonly HashSet<string> _connectedElementIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, HashSet<string>> _connectedInputPaths =
            new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        private readonly HashSet<string> _connectedOutputs = new HashSet<string>(StringComparer.Ordinal);
        private bool _reflowing;
        private bool _reflowScheduled;
        private Vector2? _pendingRestorePosition;
        private bool _initialLayoutPending;
        private bool _initialLayoutGeometryChanged;

        public GdoGraphOwnerGroup(string ownerNodeId, string groupTitle,
            GdoGraphPresentationMode presentationMode = GdoGraphPresentationMode.Nodes,
            Action<GdoGraphPresentationMode> presentationChanged = null,
            Action removeGdo = null, Action layoutChanged = null,
            Func<IReadOnlyList<GdoRelationshipChoice>> relationshipChoices = null,
            Action<string> addRelationship = null, Func<string, bool> canAddRelationship = null)
        {
            if (string.IsNullOrEmpty(ownerNodeId)) throw new ArgumentException("Owner node ID is required.", nameof(ownerNodeId));
            OwnerNodeId = ownerNodeId;
            PresentationMode = presentationMode;
            _presentationChanged = presentationChanged;
            _removeGdo = removeGdo;
            _layoutChanged = layoutChanged;
            _relationshipChoices = relationshipChoices;
            _addRelationship = addRelationship;
            _canAddRelationship = canAddRelationship;
            title = groupTitle ?? string.Empty;
            capabilities &= ~Capabilities.Deletable;
            AddToClassList("kls-gdo-owner-group");

            var contextSurface = new VisualElement
            {
                name = "owner-group-context-surface",
                pickingMode = PickingMode.Position
            };
            contextSurface.style.position = Position.Absolute;
            contextSurface.style.left = 0f;
            contextSurface.style.right = 0f;
            contextSurface.style.top = 0f;
            contextSurface.style.bottom = 0f;
            contextSurface.AddManipulator(new ContextualMenuManipulator(BuildContextualMenu));
            // Keep this above the Group's own full-size visual containers so the otherwise
            // empty painted body is a real event target. Contained graph elements are
            // GraphView siblings rendered above the Group, so their own menus still win.
            hierarchy.Add(contextSurface);

            RegisterCallback<ContextualMenuPopulateEvent>(BuildContextualMenu);
            RefreshCollapseButton();
        }

        public void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            VisualElement target = evt.target as VisualElement;
            if (target == null || containedElements.Any(element =>
                ReferenceEquals(element, target) || element.Contains(target))) return;
            IReadOnlyList<GdoRelationshipChoice> choices = _relationshipChoices?.Invoke() ??
                Array.Empty<GdoRelationshipChoice>();
            if (choices.Count == 0)
                evt.menu.AppendAction("Add Relationship", _ => { }, DropdownMenuAction.Status.Disabled);
            foreach (GdoRelationshipChoice choice in choices)
            {
                GdoRelationshipChoice captured = choice;
                evt.menu.AppendAction("Add Relationship/" + captured.Label,
                    _ => _addRelationship?.Invoke(captured.FieldPath),
                    _ => _addRelationship != null && (_canAddRelationship == null ||
                        _canAddRelationship(captured.FieldPath))
                        ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            }
            evt.menu.AppendSeparator();
            AppendPresentationAction(evt, GdoGraphPresentationMode.Full);
            AppendPresentationAction(evt, GdoGraphPresentationMode.Nodes);
            AppendPresentationAction(evt, GdoGraphPresentationMode.Connected);
            evt.menu.AppendSeparator();
            evt.menu.AppendAction("Remove GDO", _ => RequestRemoval(),
                _removeGdo == null ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
            evt.StopPropagation();
        }

        private void AppendPresentationAction(ContextualMenuPopulateEvent evt, GdoGraphPresentationMode mode)
        {
            evt.menu.AppendAction("Presentation/" + mode, _ => SetPresentation(mode),
                _ => PresentationMode == mode ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
        }

        public void RequestRemoval()
        {
            _removeGdo?.Invoke();
        }

        public string OwnerNodeId { get; }
        public GdoGraphPresentationMode PresentationMode { get; private set; }
        public bool IsExpanded => PresentationMode == GdoGraphPresentationMode.Full;
        internal bool IsPositionRestorePending => _pendingRestorePosition.HasValue;
        internal bool IsInitialLayoutPending => _initialLayoutPending;
        public IReadOnlyCollection<string> DerivedElementIds => _derivedElementIds;
        public void SetExpanded(bool value, bool notify = true)
        {
            SetPresentation(value ? GdoGraphPresentationMode.Full : GdoGraphPresentationMode.Connected, notify);
        }

        public void CyclePresentation()
        {
            GdoGraphPresentationMode next = PresentationMode == GdoGraphPresentationMode.Nodes
                ? GdoGraphPresentationMode.Full
                : PresentationMode == GdoGraphPresentationMode.Full
                    ? GdoGraphPresentationMode.Connected
                    : GdoGraphPresentationMode.Nodes;
            SetPresentation(next);
        }

        public void SetPresentation(GdoGraphPresentationMode mode, bool notify = true)
        {
            CancelInitialLayout();
            PresentationMode = mode;
            ApplyExpandedState();
            RefreshCollapseButton();
            if (notify) _presentationChanged?.Invoke(mode);
        }

        internal void SetConnections(IEnumerable<GdoGraphEdgeRecord> edges)
        {
            _connectedElementIds.Clear();
            _connectedInputPaths.Clear();
            _connectedOutputs.Clear();
            foreach (GdoGraphEdgeRecord edge in edges ?? Enumerable.Empty<GdoGraphEdgeRecord>())
            {
                if (!string.IsNullOrEmpty(edge.OutputNodeId))
                {
                    _connectedElementIds.Add(edge.OutputNodeId);
                    _connectedOutputs.Add(edge.OutputNodeId);
                }
                if (string.IsNullOrEmpty(edge.InputNodeId)) continue;
                _connectedElementIds.Add(edge.InputNodeId);
                if (!_connectedInputPaths.TryGetValue(edge.InputNodeId, out HashSet<string> paths))
                    _connectedInputPaths.Add(edge.InputNodeId, paths = new HashSet<string>(StringComparer.Ordinal));
                if (!string.IsNullOrEmpty(edge.InputPortPath)) paths.Add(edge.InputPortPath);
            }
            ApplyExpandedState();
        }

        internal void AddDerivedElement(string elementId, GraphElement element)
        {
            if (string.IsNullOrEmpty(elementId)) throw new ArgumentException("Element ID is required.", nameof(elementId));
            if (element == null) throw new ArgumentNullException(nameof(element));
            _derivedElementIds.Add(elementId);
            _elementIds[element] = elementId;
            _orderedElements.Add(element);
            AddElement(element);
            TrackGeometry(element);
            ApplyExpandedState(element);
            Reflow();
        }

        internal void SynchronizeDerivedElements(IEnumerable<KeyValuePair<string, GraphElement>> desiredElements)
        {
            if (desiredElements == null) throw new ArgumentNullException(nameof(desiredElements));
            KeyValuePair<string, GraphElement>[] desired = desiredElements.ToArray();
            var expectedViews = new HashSet<GraphElement>(desired.Select(value => value.Value));
            foreach (GraphElement element in containedElements.ToArray())
                if (!expectedViews.Contains(element))
                {
                    UntrackGeometry(element);
                    _elementIds.Remove(element);
                    RemoveElement(element);
                }
            _derivedElementIds.Clear();
            _orderedElements.Clear();
            foreach (KeyValuePair<string, GraphElement> desiredElement in desired)
            {
                _derivedElementIds.Add(desiredElement.Key);
                _elementIds[desiredElement.Value] = desiredElement.Key;
                _orderedElements.Add(desiredElement.Value);
                if (!containedElements.Contains(desiredElement.Value)) AddElement(desiredElement.Value);
                TrackGeometry(desiredElement.Value);
            }
            ApplyExpandedState();
            Reflow();
        }

        internal void RestorePosition(Vector2 position)
        {
            _pendingRestorePosition = position;
            ApplyRestoredPosition();
            schedule.Execute(() =>
            {
                ApplyRestoredPosition();
                _pendingRestorePosition = null;
            });
        }

        internal void ReleasePositionRestore() => _pendingRestorePosition = null;

        internal void PrimeInitialLayout()
        {
            if (IsExpanded || _initialLayoutPending) return;
            _initialLayoutPending = true;
            if (panel != null) BeginInitialLayout();
            else RegisterCallback<AttachToPanelEvent>(OnAttachedForInitialLayout);
        }

        private void OnAttachedForInitialLayout(AttachToPanelEvent evt)
        {
            UnregisterCallback<AttachToPanelEvent>(OnAttachedForInitialLayout);
            BeginInitialLayout();
        }

        private void BeginInitialLayout()
        {
            if (!_initialLayoutPending) return;
            style.visibility = Visibility.Hidden;
            ApplyExpandedState(GdoGraphPresentationMode.Full);
            WaitForStableInitialLayout(() =>
            {
                ApplyExpandedState();
                WaitForStableInitialLayout(() =>
                {
                    _initialLayoutPending = false;
                    style.visibility = Visibility.Visible;
                    _layoutChanged?.Invoke();
                });
            });
        }

        private void WaitForStableInitialLayout(Action completed)
        {
            if (!_initialLayoutPending) return;
            _initialLayoutGeometryChanged = false;
            Reflow();
            schedule.Execute(() =>
            {
                if (!_initialLayoutPending) return;
                if (_initialLayoutGeometryChanged)
                {
                    WaitForStableInitialLayout(completed);
                    return;
                }
                completed();
            });
        }

        private void CancelInitialLayout()
        {
            if (!_initialLayoutPending) return;
            _initialLayoutPending = false;
            UnregisterCallback<AttachToPanelEvent>(OnAttachedForInitialLayout);
            style.visibility = Visibility.Visible;
        }

        public void Reflow()
        {
            if (_orderedElements.Count == 0) return;
            Rect groupPosition = GetPosition();
            const float gap = 12f;
            GraphElement owner = _orderedElements[0];
            Rect ownerPosition = owner.GetPosition();
            float x = ownerPosition.x;
            float y = ownerPosition.y;
            bool restoreAutoUpdate = autoUpdateGeometry;
            autoUpdateGeometry = false;
            _reflowing = true;
            try
            {
                for (int index = 0; index < _orderedElements.Count; index++)
                {
                    GraphElement element = _orderedElements[index];
                    if (element.style.display.value == DisplayStyle.None) continue;
                    Rect position = element.GetPosition();
                    position.x = x;
                    position.y = y;
                    element.SetPosition(position);
                    float height = EffectiveHeight(position, element.layout);
                    y += height + gap;
                }
            }
            finally
            {
                autoUpdateGeometry = restoreAutoUpdate;
                _reflowing = false;
                UpdateGeometryFromContent();
                SetPosition(PreserveGraphPosition(groupPosition, GetPosition()));
            }
        }

        internal static Rect PreserveGraphPosition(Rect anchor, Rect geometry)
        {
            if (!IsFinite(anchor.position)) return geometry;
            geometry.position = anchor.position;
            return geometry;
        }

        internal static float EffectiveHeight(Rect storedPosition, Rect resolvedLayout)
        {
            if (resolvedLayout.height > 0f && !float.IsNaN(resolvedLayout.height) && !float.IsInfinity(resolvedLayout.height))
                return resolvedLayout.height;
            if (storedPosition.height > 0f && !float.IsNaN(storedPosition.height) && !float.IsInfinity(storedPosition.height))
                return storedPosition.height;
            return 80f;
        }

        private void TrackGeometry(GraphElement element)
        {
            if (!_trackedElements.Add(element)) return;
            element.RegisterCallback<GeometryChangedEvent>(OnElementGeometryChanged);
            if (element is GdoGraphNodeView owner)
            {
                owner.BodyVisibilityChanged += RefreshPresentationIndicator;
                owner.PresentationCycleRequested += CyclePresentation;
                owner.SetPresentationIndicator(PresentationMode);
            }
            else if (element is GdoGraphRelationshipNodeView relationship)
                relationship.BodyVisibilityChanged += RefreshPresentationIndicator;
        }

        private void UntrackGeometry(GraphElement element)
        {
            if (!_trackedElements.Remove(element)) return;
            element.UnregisterCallback<GeometryChangedEvent>(OnElementGeometryChanged);
            if (element is GdoGraphNodeView owner)
            {
                owner.BodyVisibilityChanged -= RefreshPresentationIndicator;
                owner.PresentationCycleRequested -= CyclePresentation;
            }
            else if (element is GdoGraphRelationshipNodeView relationship)
                relationship.BodyVisibilityChanged -= RefreshPresentationIndicator;
        }

        private void OnElementGeometryChanged(GeometryChangedEvent evt)
        {
            RefreshPresentationIndicator();
            if (_reflowing || (Mathf.Approximately(evt.oldRect.width, evt.newRect.width) &&
                Mathf.Approximately(evt.oldRect.height, evt.newRect.height))) return;
            if (_initialLayoutPending)
            {
                _initialLayoutGeometryChanged = true;
                return;
            }
            ScheduleReflow();
        }

        private void ScheduleReflow()
        {
            if (_reflowScheduled) return;
            _reflowScheduled = true;
            schedule.Execute(() =>
            {
                _reflowScheduled = false;
                Reflow();
            });
        }

        private void ApplyRestoredPosition()
        {
            if (!_pendingRestorePosition.HasValue) return;
            Rect current = GetPosition();
            Vector2 target = _pendingRestorePosition.Value;
            if (!IsFinite(target) || !IsFinite(current.position)) return;
            if ((current.position - target).sqrMagnitude <= 0.01f) return;
            current.position = target;
            SetPosition(current);
        }

        private static bool IsFinite(Vector2 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y);

        private void ApplyExpandedState()
        {
            foreach (GraphElement element in containedElements)
                ApplyExpandedState(element);
        }

        private void ApplyExpandedState(GdoGraphPresentationMode mode)
        {
            foreach (GraphElement element in containedElements)
                ApplyExpandedState(element, mode);
        }

        private void ApplyExpandedState(GraphElement element)
        {
            ApplyExpandedState(element, PresentationMode);
        }

        private void ApplyExpandedState(GraphElement element, GdoGraphPresentationMode mode)
        {
            bool connected = _elementIds.TryGetValue(element, out string id) && _connectedElementIds.Contains(id);
            bool properties = element is GdoGraphNodeView;
            element.style.display = mode == GdoGraphPresentationMode.Connected && !connected && !properties
                ? DisplayStyle.None : DisplayStyle.Flex;
            bool full = mode == GdoGraphPresentationMode.Full;
            bool connectedOnly = mode == GdoGraphPresentationMode.Connected;
            _elementIds.TryGetValue(element, out string elementId);
            _connectedInputPaths.TryGetValue(elementId ?? string.Empty, out HashSet<string> inputPaths);
            bool outputConnected = elementId != null && _connectedOutputs.Contains(elementId);
            if (element is GdoGraphNodeView owner)
            {
                owner.SetConnectedPorts(inputPaths, outputConnected, connectedOnly);
                owner.SetGroupExpanded(full);
            }
            else if (element is GdoGraphRelationshipNodeView relationship)
            {
                relationship.SetConnectedPorts(inputPaths, connectedOnly);
                relationship.SetGroupExpanded(full);
                relationship.RefreshMoveControls();
            }
        }

        private void RefreshCollapseButton()
        {
            RefreshPresentationIndicator();
            ScheduleReflow();
        }

        public void RefreshPresentationIndicator()
        {
            foreach (GdoGraphNodeView owner in _orderedElements.OfType<GdoGraphNodeView>())
                owner.SetPresentationIndicator(PresentationMode);
        }

    }
}
