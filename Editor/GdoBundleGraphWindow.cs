using UnityEditor;
using KitchenData;
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

using KitchenLib.Studio.Editor.Bootstrap;
using KitchenLib.Studio.Editor.Catalogs.Mods;

namespace KitchenLib.Studio.Editor
{
    public sealed class GdoBundleGraphWindow : EditorWindow
    {
        private GdoGraphToolbar _toolbar;
        private GdoBundleGraphView _graphView;
        private GdoGraphDiagnosticsPanel _diagnostics;
        private VisualElement _onboarding;
        private VisualElement _emptyGraph;

        public GdoBundleGraphAsset Graph { get; private set; }
        public GdoBundleGraphView GraphView => _graphView;

        [MenuItem("KitchenLib Studio/GDO Bundle Graph")]
        public static void ShowWindow()
        {
            GdoBundleGraphWindow window = GetWindow<GdoBundleGraphWindow>("GDO Bundle Graph");
            window.EnsureUsableWindowBounds();
            window.Show();
        }

        public static GdoBundleGraphWindow Open(GdoBundleGraphAsset graph, string focusElementId = null)
        {
            GdoBundleGraphWindow window = GetWindow<GdoBundleGraphWindow>("GDO Bundle Graph");
            window.EnsureUsableWindowBounds();
            window.EnsureUi();
            window.SelectGraph(graph, focusElementId);
            window.Show();
            return window;
        }

        private void OnEnable()
        {
            minSize = new Vector2(800f, 500f);
            EnsureUsableWindowBounds();
            Undo.undoRedoPerformed += HandleUndoRedo;
        }
        private void OnDisable()
        {
            Undo.undoRedoPerformed -= HandleUndoRedo;
            _graphView?.DisposeSnapshotThumbnails();
        }

        private void EnsureUsableWindowBounds()
        {
            minSize = new Vector2(800f, 500f);
            Rect bounds = position;
            if (bounds.width >= minSize.x && bounds.height >= minSize.y) return;
            bounds.width = Mathf.Max(bounds.width, minSize.x);
            bounds.height = Mathf.Max(bounds.height, minSize.y);
            position = bounds;
        }

        private void HandleUndoRedo()
        {
            if (_graphView == null) return;
            _graphView.Load(Graph);
            RefreshGraphState();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            _graphView = new GdoBundleGraphView(RefreshGraphState, nodeId => OpenRenameGdoDialog(nodeId),
                createGdo: CreateGdo, addSelected: AddSelected, addExternal: ShowExternalReferenceDialog,
                renameProject: () => OpenRenameProjectDialog(), collectOverlays: CollectProjectOverlays);
            _toolbar = new GdoGraphToolbar(SelectGraph, text => _graphView.SearchAndFrame(text),
                BuildGraph, () => _graphView.FrameEverything());
            _diagnostics = new GdoGraphDiagnosticsPanel();
            rootVisualElement.Add(_toolbar);
            var canvas = new VisualElement { name = "gdo-graph-canvas-container" };
            canvas.style.flexGrow = 1;
            canvas.Add(_graphView);
            _onboarding = CreateOnboarding();
            _emptyGraph = CreateEmptyGraphGuidance();
            canvas.Add(_onboarding);
            canvas.Add(_emptyGraph);
            rootVisualElement.Add(canvas);
            rootVisualElement.Add(_diagnostics);
            SelectGraph(Graph);
        }

        public void SelectGraph(GdoBundleGraphAsset graph) => SelectGraph(graph, null);

        private void SelectGraph(GdoBundleGraphAsset graph, string focusElementId)
        {
            EnsureUi();
            Graph = graph;
            _toolbar.SetGraphWithoutNotify(graph);
            _graphView.Load(graph, focusElementId);
            RefreshGraphState();
        }

        private void RefreshGraphState()
        {
            GdoBundleGraphAsset graph = Graph;
            GdoGraphDiagnostic[] issues = graph == null ? new GdoGraphDiagnostic[0] :
                GdoBundleGraphValidator.Validate(graph).ToArray();
            bool hasGraph = graph != null;
            bool hasLocalGdo = hasGraph && graph.Nodes.Any(node => node.Kind == GdoGraphNodeKind.Local && !node.IsBroken);
            bool hasBlockingIssue = issues.Any(issue => issue.Severity == ValidationSeverity.Error || issue.Severity == ValidationSeverity.Fatal);
            bool canBuild = hasLocalGdo && !hasBlockingIssue;
            string buildReason = !hasGraph ? "Create or select a bundle graph first" :
                !hasLocalGdo ? "Add at least one GDO to this bundle" :
                "Resolve " + issues.Count(issue => issue.Severity == ValidationSeverity.Error || issue.Severity == ValidationSeverity.Fatal) +
                " graph issue(s) before building";
            _toolbar.SetGraphState(hasGraph, canBuild, buildReason);
            _onboarding.style.display = hasGraph ? DisplayStyle.None : DisplayStyle.Flex;
            _emptyGraph.style.display = hasGraph && graph.Nodes.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            string readyStatus = !hasGraph ? "Create or select a bundle graph to begin" :
                !hasLocalGdo ? "Bundle graph ready · add your first GDO" :
                "Graph valid · " + graph.Nodes.Count + " GDO(s) · " + graph.Edges.Count + " connection(s)";
            _diagnostics.Bind(issues, _graphView.Focus, readyStatus);
            Repaint();
        }

        public GdoBundleGraphAsset CreateProject(string parentFolder, string authorNamespace, string projectName)
        {
            GdoBundleGraphAsset graph = GdoBundleProjectRepository.Create(
                parentFolder, authorNamespace, projectName);
            ActivateProject(graph);
            return graph;
        }

        private void ActivateProject(GdoBundleGraphAsset graph)
        {
            SelectGraph(graph);
            Selection.activeObject = graph;
            EditorGUIUtility.PingObject(graph);
        }

        private void EnsureUi()
        {
            if (_graphView == null) CreateGUI();
        }

        private void CreateGdo(string runtimeTypeName)
        {
            if (Graph == null) return;
            OpenCreateGdoDialog(runtimeTypeName);
        }

        public GdoNameDialog OpenCreateGdoDialog(string runtimeTypeName)
        {
            GdoBundleGraphAsset target = Graph;
            if (target == null) throw new InvalidOperationException("Open a bundle project first.");
            string shortName = runtimeTypeName.Substring(runtimeTypeName.LastIndexOf('.') + 1);
            return GdoNameDialog.Open("Create " + shortName, "New " + shortName,
                value => GdoBundleProjectRepository.GdoFolder(target) + "/" + value + ".asset", value =>
                {
                    GdoGraphNodeRecord node = GdoGraphAssetCommands.CreateGdo(target, runtimeTypeName, value, new Rect(40, 40, 260, 160));
                    SelectGraph(target, node.Id);
                    Selection.activeObject = node.Asset;
                });
        }

        public GdoNameDialog OpenRenameProjectDialog()
        {
            GdoBundleGraphAsset target = Graph;
            if (target == null) throw new InvalidOperationException("Open a bundle project first.");
            string selectedId = _graphView.selection.OfType<GdoGraphNodeView>().FirstOrDefault()?.RecordId;
            string focusedGuid = target.Nodes.FirstOrDefault(node => node.Id == (selectedId ?? _graphView.FocusedElementId))?.AssetGuid;
            return GdoNameDialog.Open("Rename Project", target.BundleName, null, value =>
            {
                GdoProjectRenamePreview preview = GdoBundleProjectRepository.PreviewProjectRename(target, value);
                GdoRenameConfirmationDialog.Open(new GdoRenameConfirmationModel(preview), () =>
                {
                    GdoBundleProjectRepository.RenameProject(target, preview);
                    ReloadByGuid(preview.GraphGuid, focusedGuid);
                });
            });
        }

        public GdoNameDialog OpenRenameGdoDialog(string nodeId)
        {
            GdoBundleGraphAsset target = Graph;
            GdoGraphNodeRecord node = target?.Nodes.SingleOrDefault(value => value.Id == nodeId);
            if (node == null || node.Kind != GdoGraphNodeKind.Local || node.IsBroken)
                throw new ArgumentException("Select a resolved local GDO.", nameof(nodeId));
            return GdoNameDialog.Open("Rename GDO", node.Asset.name, null, value =>
            {
                GdoRenamePreview preview = GdoBundleProjectRepository.PreviewGdoRename(target, nodeId, value);
                GdoRenameConfirmationDialog.Open(new GdoRenameConfirmationModel(preview), () =>
                {
                    GdoBundleProjectRepository.RenameGdo(preview);
                    ReloadByGuid(preview.GraphGuid, preview.AssetGuid);
                });
            });
        }

        private void ReloadByGuid(string graphGuid, string selectedGdoGuid)
        {
            string path = AssetDatabase.GUIDToAssetPath(graphGuid);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            GdoBundleGraphAsset graph = AssetDatabase.LoadAssetAtPath<GdoBundleGraphAsset>(path);
            GdoGraphNodeRecord node = graph.Nodes.FirstOrDefault(value => value.AssetGuid == selectedGdoGuid);
            SelectGraph(graph, node?.Id);
            Selection.activeObject = node == null ? (UnityEngine.Object)graph : node.Asset;
            EditorGUIUtility.PingObject(Selection.activeObject);
        }

        private void AddSelected()
        {
            if (Graph == null || !(Selection.activeObject is GameDataObject gdo)) return;
            GdoGraphAssetCommands.AddExistingGdo(Graph, gdo, new Rect(40, 40, 260, 160));
            SelectGraph(Graph);
        }

        private void AddExternal(GdoBundleGraphAsset targetGraph, int id, string expectedType)
            => AddExternal(targetGraph, ExternalGdoCatalogRepository.VanillaProviderKey, id, expectedType);

        private void AddExternal(GdoBundleGraphAsset targetGraph, string providerKey, int id, string expectedType)
        {
            if (targetGraph == null) return;
            ExternalGdoCatalogEntry entry = ExternalGdoCatalogRepository.Current.Find(providerKey, id, expectedType);
            GdoBundleGraphRepository.AddExternal(targetGraph, providerKey, id, expectedType,
                new Rect(40, 40, 220, 120), entry?.DisplayName);
            SelectGraph(targetGraph);
        }

        private VisualElement CreateOnboarding()
        {
            VisualElement panel = GuidancePanel("gdo-graph-onboarding",
                "Create or open a bundle project to begin",
                "A bundle graph defines which GDOs ship together and how their relationships connect.");
            VisualElement actions = panel.Q("guidance-actions");
            actions.Add(new Button(PromptCreateGraph) { name = "create-bundle-project", text = "Create Bundle Project" });
            actions.Add(new Button(PromptSelectGraph) { name = "open-bundle-project", text = "Open Bundle Project" });
            return panel;
        }

        private VisualElement CreateEmptyGraphGuidance()
        {
            VisualElement panel = GuidancePanel("gdo-empty-graph", "This bundle has no GDOs yet",
                "Create a new GDO asset here, or select an existing GDO in the Project window and add it.");
            VisualElement actions = panel.Q("guidance-actions");
            actions.Add(new Button(ShowCreateGdoMenu) { name = "empty-create-gdo", text = "Create GDO" });
            actions.Add(new Button(AddSelected) { name = "empty-add-selected-gdo", text = "Add Selected GDO" });
            return panel;
        }

        private static VisualElement GuidancePanel(string name, string heading, string description)
        {
            var panel = new VisualElement { name = name };
            panel.style.position = Position.Absolute;
            panel.style.left = 0; panel.style.right = 0; panel.style.top = 0; panel.style.bottom = 0;
            panel.style.alignItems = Align.Center;
            panel.style.justifyContent = Justify.Center;
            panel.style.backgroundColor = new Color(0.12f, 0.12f, 0.12f, 0.92f);
            var card = new VisualElement();
            card.style.width = 460;
            card.style.paddingLeft = 24; card.style.paddingRight = 24;
            card.style.paddingTop = 20; card.style.paddingBottom = 20;
            card.style.backgroundColor = new Color(0.20f, 0.20f, 0.20f, 1f);
            var title = new Label(heading); title.style.fontSize = 18; title.style.unityFontStyleAndWeight = FontStyle.Bold;
            card.Add(title);
            var body = new Label(description); body.style.whiteSpace = WhiteSpace.Normal; body.style.marginTop = 8; body.style.marginBottom = 14;
            card.Add(body);
            var actions = new VisualElement { name = "guidance-actions" }; actions.style.flexDirection = FlexDirection.Row;
            card.Add(actions);
            panel.Add(card);
            return panel;
        }

        private void PromptCreateGraph()
        {
            GdoProjectIdentityDialog.OpenCreate(SuggestedAuthor(), "New Bundle Project",
                (author, project) =>
                {
                    GdoBundleProjectRepository.EnsureDefaultRoot();
                    CreateProject(GdoBundleProjectRepository.DefaultRoot, author, project);
                });
        }

        private static string SuggestedAuthor()
        {
            string value = PlayerSettings.companyName;
            return string.IsNullOrWhiteSpace(value) || string.Equals(value, "DefaultCompany", StringComparison.OrdinalIgnoreCase)
                ? string.Empty : value.Trim();
        }

        private void PromptSelectGraph()
        {
            string absolute = EditorUtility.OpenFilePanel("Open Bundle Project", Application.dataPath, "asset");
            if (string.IsNullOrEmpty(absolute)) return;
            string projectRoot = KitchenLibStudioGeneratedPaths.ProjectRoot;
            string full = Path.GetFullPath(absolute);
            if (!full.StartsWith(projectRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog("Open Bundle Project", "Choose the project's graph asset inside this Unity project.", "OK");
                return;
            }
            string assetPath = full.Substring(projectRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Replace('\\', '/');
            GdoBundleGraphAsset graph = AssetDatabase.LoadAssetAtPath<GdoBundleGraphAsset>(assetPath);
            if (graph == null || GdoBundleProjectRepository.ValidateLayout(graph).Count != 0)
                EditorUtility.DisplayDialog("Open Bundle Project", "Choose a valid bundle project's graph asset.", "OK");
            else SelectGraph(graph);
        }

        private void ShowCreateGdoMenu()
        {
            var menu = new GenericMenu();
            foreach (string runtimeType in GdoGraphAssetCommands.SupportedRuntimeTypes)
            {
                string captured = runtimeType;
                menu.AddItem(new GUIContent(captured.Substring(captured.LastIndexOf('.') + 1)), false, () => CreateGdo(captured));
            }
            menu.ShowAsContext();
        }

        private void ShowExternalReferenceDialog()
        {
            if (Graph != null) GdoExternalReferenceDialog.Open(Graph,
                (GdoBundleGraphAsset graph, string provider, int id, string type) => AddExternal(graph, provider, id, type));
        }

        private void CollectProjectOverlays()
        {
            try { GdoOverlayAuthoringRepository.MoveCompanionsIntoProject(Graph); RefreshGraphState(); }
            catch (Exception exception) { EditorUtility.DisplayDialog("Collect Project Overlays", exception.Message, "OK"); }
        }

        private void BuildGraph()
        {
            if (Graph == null) return;
            BundleBuildResult result = GdoBundleGraphBuilder.Build(Graph);
            if (result.Succeeded) Debug.Log("Built graph-authored GDO bundle: " + result.BundlePath);
            else Debug.LogError(string.Join(System.Environment.NewLine, result.Issues));
            SelectGraph(Graph);
        }
    }
}
