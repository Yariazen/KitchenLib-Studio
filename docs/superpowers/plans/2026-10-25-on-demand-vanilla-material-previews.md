# On-Demand Vanilla Material Previews Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let authors reconstruct reusable editor-only approximations of PlateUp food, appliance, clothing, and furniture materials without copying vanilla authored assets, and prevent those proxies from entering bundles.

**Architecture:** Add a material-only decoder contract that emits descriptive scalar/vector/color and texture-slot metadata but never texture bytes. A shared editor cache creates normal Unity `.mat` proxies plus provenance metadata, a dedicated browser drives discovery and import, and a dependency validator blocks any export graph that still contains a proxy or generated preview resource.

**Tech Stack:** Unity 2020.3.48f1 Editor C#, AssetsTools.NET, Unity `AssetDatabase`, UIElements/EditorWindow, NUnit EditMode tests

**Spec:** `docs/superpowers/specs/2026-10-25-on-demand-vanilla-material-previews-design.md`

## Global Constraints

- Reconstruct approximations only; never persist PlateUp texture pixels, shader binaries/source, meshes, animation, audio, or converted equivalents.
- Prioritize food, appliance, clothing, and furniture surface materials; font/UI/image/effect fidelity is not an acceptance target.
- Store shared proxies under `Assets/KitchenLibStudio/Generated/VanillaMaterialPreviews/`, never per mod project.
- Stable identity is catalogue key plus source relative path/path ID; display name is not identity.
- Refresh assets in place so Unity GUIDs and authoring references survive.
- Proxies and generated preview resources are editor-only and must cause bundle validation to fail before `BuildPipeline` runs.
- Do not mutate source prefabs or material slots during validation.
- Unity version remains 2020.3.48f1.

## Review Focus

- A malicious or accidental decoder result carrying source texture bytes must be rejected before any asset write (Task 1 payload-boundary test and Task 3 transactional rejection test).
- Duplicate material names from different source identities must never overwrite or reuse each other (Task 2 identity/path tests and Task 3 cache tests).
- Refreshing an existing proxy must preserve its `.meta` GUID even when source fingerprint or properties change (Task 3 refresh test).
- A transitive proxy reference through a nested prefab or GDO dependency must block export, while an unrelated proxy elsewhere in the shared cache must not (Task 5 dependency tests).
- Browser classification is advisory only; unsupported entries cannot bypass importer safety by direct API use (Task 2 classification tests and Task 3 importer rejection tests).

---

### Task 1: Safe material-only decode contract

**Files:**
- Create: `KitchenLib Studio/Assets/KitchenLibStudio/Editor/Materials/VanillaMaterialPreviewData.cs`
- Create: `KitchenLib Studio/Assets/KitchenLibStudio/Editor/Materials/IVanillaMaterialPreviewDecoder.cs`
- Modify: `KitchenLib Studio/Assets/KitchenLibStudio/Editor/Snapshots/AssetsToolsPlateUpPreviewDecoderV2.cs`
- Create: `KitchenLib Studio/Assets/KitchenLibStudio/Validation/Tests/Editor/Materials/VanillaMaterialPreviewDecoderTests.cs`
- Create Unity `.meta` files for the new folder and sources.

**Interfaces:**
- Consumes: `VanillaAssetRecord`, `VanillaAssetCatalogData`, installed PlateUp root, `IProgress<SnapshotProgress>`, and `CancellationToken`.
- Produces: `VanillaMaterialPreviewData IVanillaMaterialPreviewDecoder.DecodeMaterial(string gameRoot, VanillaAssetRecord material, VanillaAssetCatalogData catalog, IProgress<SnapshotProgress> progress, CancellationToken token)`.
- `VanillaMaterialPreviewData` exposes stable source identity, display name, shader-name hint, floats, colors, vectors, `VanillaMaterialTextureSlotData` entries containing slot name/scale/offset/hasSourceTexture, and diagnostics; it exposes no `byte[]`, `Texture2D`, `PreviewTextureData`, or source texture object.

- [ ] **Step 1: Write failing contract tests**

Add `MaterialDecoderRejectsNonMaterialRecord`, `MaterialDecoderRejectsRecordOutsideValidatedCatalog`, and `MaterialPreviewContractContainsNoAuthoredAssetPayloadType`. The last test reflects over the returned model and fails if it exposes byte arrays, Unity texture/mesh/audio/animation objects, or existing snapshot texture payload models.

- [ ] **Step 2: Run the decoder fixture and verify red**

Run the Unity EditMode fixture `VanillaMaterialPreviewDecoderTests`.

Expected: compilation fails because the material decoder contract and data types do not exist.

- [ ] **Step 3: Implement the public material decode boundary**

Add the exact interface and immutable data model above. Refactor the existing private material reader so the new entry point validates fingerprint/catalog membership, reads only the selected `Material`, records whether texture slots reference source content, and never calls `DecodeTexture`.

- [ ] **Step 4: Run focused decoder and snapshot decoder tests**

Expected: the new contract tests pass; existing `PlateUpRenderDataDecoderTests` remain green because GameObject snapshots retain their separate internal texture decoding path.

- [ ] **Step 5: Commit or record the repository's no-commit ruling**

Limit changes to the decoder boundary, immutable models, and owned tests.

### Task 2: Support classification and stable cache identity

**Files:**
- Create: `KitchenLib Studio/Assets/KitchenLibStudio/Editor/Materials/VanillaMaterialSupportClassifier.cs`
- Create: `KitchenLib Studio/Assets/KitchenLibStudio/Editor/Materials/VanillaMaterialCachePaths.cs`
- Create: `KitchenLib Studio/Assets/KitchenLibStudio/Validation/Tests/Editor/Materials/VanillaMaterialSupportClassifierTests.cs`
- Create: `KitchenLib Studio/Assets/KitchenLibStudio/Validation/Tests/Editor/Materials/VanillaMaterialCachePathsTests.cs`
- Create Unity `.meta` files for new sources.

**Interfaces:**
- Consumes: Task 1 `VanillaMaterialPreviewData` and `VanillaAssetRecord`.
- Produces: `VanillaMaterialSupportAssessment VanillaMaterialSupportClassifier.Classify(VanillaMaterialPreviewData data)` with `Good`, `Limited`, or `Unavailable`, reason text, and an advisory priority; `string VanillaMaterialCachePaths.MaterialPath(VanillaAssetRecord record)` derived from stable identity.

- [ ] **Step 1: Write failing classification tests**

Add literal fixtures proving an ordinary color-only food material is `Good`, a source-texture-dependent atlas/image material is `Unavailable`, and transparent/emissive/custom-effect hints are `Limited`. Prove classification never changes whether the importer enforces payload safety.

- [ ] **Step 2: Write failing identity tests**

Prove repeated calls for one source identity return one deterministic `.mat` path, two identical display names with different path IDs produce different paths, invalid filename characters are sanitized, and paths remain beneath the shared cache root.

- [ ] **Step 3: Run both fixtures and verify red**

Expected: compilation fails because classifier and path service do not exist.

- [ ] **Step 4: Implement minimal classifier and path service**

Classify from decoded properties/slot dependence, using names only as a secondary explanation hint. Build a readable slug plus a stable hash of catalogue key, relative path, and path ID; reject empty/malformed source identity.

- [ ] **Step 5: Run focused tests and verify green**

Expected: all Task 2 cases pass without accessing PlateUp files or `AssetDatabase`.

- [ ] **Step 6: Commit or record the repository's no-commit ruling**

Limit changes to pure classification/path code and tests.

### Task 3: Shared proxy asset cache and provenance

**Files:**
- Create: `KitchenLib Studio/Assets/KitchenLibStudio/Editor/Materials/VanillaMaterialPreviewMetadata.cs`
- Create: `KitchenLib Studio/Assets/KitchenLibStudio/Editor/Materials/VanillaMaterialPreviewImporter.cs`
- Create: `KitchenLib Studio/Assets/KitchenLibStudio/Editor/Materials/VanillaMaterialPreviewRegistry.cs`
- Create: `KitchenLib Studio/Assets/KitchenLibStudio/Validation/Tests/Editor/Materials/VanillaMaterialPreviewImporterTests.cs`
- Create Unity `.meta` files for new sources.

**Interfaces:**
- Consumes: Task 1 preview data and Task 2 support/path services.
- Produces: `VanillaMaterialImportResult VanillaMaterialPreviewImporter.Import(VanillaAssetRecord record, VanillaMaterialPreviewData data, string sourceFingerprint)` and registry methods `IsProxy(Material)`, `IsPreviewResource(UnityEngine.Object)`, and `TryGetMetadata(Material, out VanillaMaterialPreviewMetadata)`.
- Metadata stores schema version, stable source identity, source fingerprint, shader-name hint, support level, and generated resource paths.

- [ ] **Step 1: Write failing create/reuse/collision tests**

In a temporary `Assets` folder, assert import creates a normal `.mat` and metadata asset, reimporting the same identity reuses them, and same-name/different-identity records remain distinct.

- [ ] **Step 2: Run those cases and verify red**

Expected: compilation fails because importer, metadata, and registry do not exist.

- [ ] **Step 3: Implement minimal proxy creation**

Use an allowlisted Studio/Unity shader with Standard fallback; copy representable floats/colors/vectors and common mappings (`_Color0`→`_Color`, `_Shininess`→`_Glossiness`, `_Specular`→`_Metallic`). Do not assign decoded source textures. Mark proxy and generated resources through metadata/labels recognized by the registry.

- [ ] **Step 4: Write failing refresh and safety tests**

Add `RefreshPreservesMaterialGuid`, `ImportRejectsAuthoredPayloadBeforeAssetWrite`, `FailedRefreshPreservesPriorProxy`, and `UnsupportedTextureDependentMaterialCreatesNoAsset`. Use literal prior/new colors and compare GUID strings through `AssetDatabase.AssetPathToGUID`.

- [ ] **Step 5: Implement transactional in-place refresh**

Validate all inputs before writes; update existing material/metadata objects in place, use rollback snapshots on failure, and save/import only after a complete valid reconstruction. Do not add a cleanup API in this task.

- [ ] **Step 6: Run importer, classifier, and cache tests**

Expected: all Task 3 tests pass and test teardown removes only its exact temporary asset folder.

- [ ] **Step 7: Commit or record the repository's no-commit ruling**

Limit changes to shared cache/provenance code and tests.

### Task 4: Dedicated Vanilla Material Browser

**Files:**
- Create: `KitchenLib Studio/Assets/KitchenLibStudio/Editor/Materials/VanillaMaterialBrowserWindow.cs`
- Create: `KitchenLib Studio/Assets/KitchenLibStudio/Editor/Materials/VanillaMaterialBrowserModel.cs`
- Create: `KitchenLib Studio/Assets/KitchenLibStudio/Validation/Tests/Editor/Materials/VanillaMaterialBrowserTests.cs`
- Modify: `KitchenLib Studio/Assets/KitchenLibStudio/Validation/Tests/Editor/KitchenLibStudioMenuLayoutTests.cs`
- Create Unity `.meta` files for new sources.

**Interfaces:**
- Consumes: catalogue repository, Task 1 decoder, Task 2 classifier, and Task 3 importer/registry.
- Produces: menu `KitchenLib Studio/Vanilla Material Browser`; model operations for query/filter/select/decode/import/refresh and selecting an existing proxy in the Project window.

- [ ] **Step 1: Write failing model tests**

Prove results contain only material records, default ordering prioritizes `Good` food/appliance/clothing/furniture candidates, duplicate names show stable source disambiguators, unsupported entries remain labelled, and search matches display name or catalogue key.

- [ ] **Step 2: Write failing command-state tests**

With injected decoder/importer seams, prove selection performs no write, `Create Preview` is enabled only for a decoded supported selection, an existing identity shows `Refresh Preview`, and unavailable entries cannot invoke import.

- [ ] **Step 3: Run browser fixtures and verify red**

Expected: compilation fails because browser model/window do not exist.

- [ ] **Step 4: Implement the browser model and EditorWindow**

Build a searchable list, support badge/reason, source identity, reconstruction warning, vertex-color/post-process warning, and explicit Create/Refresh/Select actions. Keep decoding/import synchronous for this first on-demand workflow and show actionable errors without partial assets.

- [ ] **Step 5: Run browser and menu-layout tests**

Expected: browser behavior passes; the menu test finds exactly one new browser entry without disturbing established KitchenLib Studio menus.

- [ ] **Step 6: Commit or record the repository's no-commit ruling**

Limit changes to browser/model, menu registration, and tests.

### Task 5: Reject preview dependencies before bundle construction

**Files:**
- Create: `KitchenLib Studio/Assets/KitchenLibStudio/Editor/Materials/VanillaMaterialExportValidator.cs`
- Modify: `KitchenLib Studio/Assets/KitchenLibStudio/Editor/GdoBundleBuilder.cs`
- Create: `KitchenLib Studio/Assets/KitchenLibStudio/Validation/Tests/Editor/Materials/VanillaMaterialExportValidatorTests.cs`
- Modify: `KitchenLib Studio/Assets/KitchenLibStudio/Validation/Tests/Editor/GdoBundleBuilderTests.cs`
- Create Unity `.meta` files for new sources.

**Interfaces:**
- Consumes: Task 3 registry/provenance and build request root asset paths.
- Produces: `IReadOnlyList<ValidationIssue> VanillaMaterialExportValidator.Validate(IEnumerable<string> rootAssetPaths)`; `GdoBundleBuilder.Build` invokes it after resolving request assets and before staging/output directory creation or `BuildPipeline.BuildAssetBundles`.

- [ ] **Step 1: Write failing dependency-validator tests**

Create real temporary Unity assets proving direct proxy, nested-prefab proxy, generated preview resource, and transitive serialized GDO reference each produce an error naming root and offending dependency. Prove an unrelated cached proxy and an ordinary custom material produce no error.

- [ ] **Step 2: Run validator tests and verify red**

Expected: compilation fails because the validator does not exist.

- [ ] **Step 3: Implement dependency validation**

Use `AssetDatabase.GetDependencies(root, true)` per export root, inspect loaded main/sub-assets through the registry, deduplicate findings, and return actionable errors. Never rewrite or clone source assets.

- [ ] **Step 4: Write failing builder-boundary test**

Add a test whose valid GDO transitively references a proxy and whose output directory does not yet exist; assert `Build` returns an error, emits no bundle, creates no output/staging artifacts, and leaves the source prefab material reference unchanged.

- [ ] **Step 5: Integrate the validator before side effects**

Append validator issues before the existing early error return. Preserve ordinary-material builds and current package-audit behavior.

- [ ] **Step 6: Run validator, builder, audit, and deployment-readiness tests**

Expected: proxy-bearing builds stop before bundle construction; existing normal bundle tests remain green.

- [ ] **Step 7: Commit or record the repository's no-commit ruling**

Limit changes to validator integration and owned tests.

### Task 6: Integrated verification and authoring smoke test

**Files:**
- Modify only if verification exposes a defect: files owned by Tasks 1–5.

**Interfaces:**
- Consumes: all preceding tasks.
- Produces: verified editor workflow and regression evidence; no new public API.

- [ ] **Step 1: Run focused material fixtures**

Run all EditMode tests under `Validation/Tests/Editor/Materials`.

Expected: all focused tests pass with zero unexpected warnings/errors.

- [ ] **Step 2: Run existing regression suites**

Run snapshot decoder/browser/cache, bundle builder/output verifier/package audit, menu layout, and deployment-readiness fixtures.

Expected: all tests pass; any unrelated pre-existing failure is reported by exact test name and kept separate.

- [ ] **Step 3: Exercise the live browser**

Open the browser, reconstruct `Dumplings Cooked` or `Bread - Bun`, assign it to the Bao authoring object, verify a shared `.mat` is created and reusable, and confirm the UI describes it as an approximation dependent on user-authored vertex colors/PlateUp post-processing.

- [ ] **Step 4: Verify export rejection live**

Reference the proxy from a temporary export candidate, confirm the build stops with the root/dependency diagnostic before output, remove the temporary reference, and confirm an ordinary-material control can build.

- [ ] **Step 5: Run repository hygiene checks**

Run `git diff --check` and inspect `git status --short`; do not modify, delete, or stage unrelated existing work.

- [ ] **Step 6: Commit verification fixes or record the repository's no-commit ruling**

Stage only files owned by this plan if commits are appropriate in the dirty shared checkout.
