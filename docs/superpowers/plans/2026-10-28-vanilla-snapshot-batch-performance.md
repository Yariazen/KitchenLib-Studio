# Vanilla Snapshot Batch Performance Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make alpha.7 vanilla snapshot generation reuse one validated, lazy decoding session while exposing meaningful progress and phase timings.

**Architecture:** The import service owns one disposable AssetsTools decode session for a batch. The session validates once, retains loaded containers and shared decoded resources, while Unity rendering remains sequential and atomic publication remains unchanged.

**Tech Stack:** C# 8-compatible Unity 2020.3 editor code, AssetsTools.NET, an external Roslyn/stub test harness that does not launch Unity, PowerShell package verification.

**Spec:** `docs/superpowers/specs/2026-10-28-vanilla-snapshot-batch-performance-design.md`

## Global Constraints

- Do not run Unity locally; exercise orchestration through the external Roslyn harness and defer real Unity integration verification until a self-hosted runner exists.
- Preserve 128x128 PNG output, snapshot eligibility/source resolution, cancellation, and atomic publication.
- Do not add a persistent decoded cache or parallel Unity rendering.
- Generated mirrors from older package revisions remain unsupported.
- The distributable package root must contain no internal tests or planning files at release time.

## Review Focus

- Cancellation during decode must dispose the session and leave published output untouched; covered in Task 2.
- A decode exception after containers load must dispose all AssetsTools resources; covered in Task 1.
- Two snapshots sharing a mesh or texture must decode the resource once without corrupting model indices; covered in Task 1.
- Zero eligible snapshots must still complete progress and publish valid empty snapshot output; covered in Task 2.
- Progress callbacks and timing logs must not execute from an unsafe worker context; covered in Task 3.

---

### Task 1: Reusable preview decode session

**Files:**
- Modify: `Editor/Snapshots/IPlateUpPreviewDecoder.cs`
- Modify: `Editor/Snapshots/AssetsToolsPlateUpPreviewDecoderV2.cs`
- Modify: `Editor/Snapshots/GameObjectSnapshotService.cs`
- Test: `C:/Users/Yariazen/Documents/ChatGPT/Plateup/verification/alpha7-snapshot-performance/PreviewDecodeSessionTests.cs`

**Interfaces:**
- Produces: `IPlateUpPreviewDecodeSession : IDisposable` with `VanillaPreviewModel Decode(VanillaAssetRecord root, IProgress<SnapshotProgress> progress, CancellationToken token)`.
- Produces: `IPlateUpPreviewDecoder.OpenSession(string gameRoot, VanillaAssetCatalogData catalog)`.
- Produces: `GameObjectSnapshotService.RenderVanillaBlocking(IPlateUpPreviewDecodeSession session, string key, SnapshotRenderOptions options, CancellationToken token, Action<SnapshotRenderTiming> timing)`.

- [ ] **Step 1: Write failing session lifecycle and reuse tests**

Assert that opening a session validates sources once, two snapshot decodes use one session, shared resource identities decode once, and disposal occurs after success or decode failure.

- [ ] **Step 2: Run the external Roslyn harness and verify RED**

Run `pwsh -File C:/Users/Yariazen/Documents/ChatGPT/Plateup/verification/alpha7-snapshot-performance/Invoke-Tests.ps1 -PackageRoot . -Case PreviewDecodeSession`. Expected: compilation/test failure because the session interfaces do not exist.

- [ ] **Step 3: Implement the session API and one-shot adapter**

Move manager/class-package/container ownership and source-identity resource dictionaries into the session. Make the existing one-shot `Decode` open, use, and dispose a session so interactive callers retain behavior.

- [ ] **Step 4: Implement batch-aware rendering and decode/render timing**

Add internal `SnapshotRenderTiming` with `TimeSpan Decode` and `TimeSpan Render`; reconstruct/render only after session decode returns.

- [ ] **Step 5: Run focused tests and verify GREEN**

Expected: session lifecycle, source validation count, resource reuse, failure disposal, and timing assertions pass.

- [ ] **Step 6: Commit**

`git commit -m "perf: reuse vanilla preview decode sessions"`

### Task 2: Snapshot batch progress, cancellation, and ownership

**Files:**
- Modify: `Editor/Snapshots/VanillaSnapshotBatchRenderer.cs`
- Modify: `Editor/Catalogs/Vanilla/PlateUpCatalogImportService.cs`
- Modify: `Editor/Catalogs/Vanilla/VanillaCatalogGeneration.cs`
- Test: `C:/Users/Yariazen/Documents/ChatGPT/Plateup/verification/alpha7-snapshot-performance/VanillaSnapshotBatchPerformanceTests.cs`

**Interfaces:**
- Consumes: Task 1 session and `SnapshotRenderTiming`.
- Produces: `VanillaSnapshotBatchProgress` with current key, completed count, total count, decode elapsed, and render elapsed.
- Produces: `RenderAsync(VanillaAssetCatalogData catalog, IPlateUpPreviewDecodeSession session, IProgress<VanillaSnapshotBatchProgress> progress, CancellationToken token)`.

- [ ] **Step 1: Write failing batch tests**

Assert a single session serves all resolved unique GameObjects, progress is ordered from `0/N` through `N/N`, zero-work reports completion, cancellation stops before the next decode, and the import service disposes the session without calling publication.

- [ ] **Step 2: Run focused tests and verify RED**

Expected: failure because item progress and session ownership are absent.

- [ ] **Step 3: Implement sequential item progress and cancellation**

Resolve all keys first to establish `N`, report before and after each item, and check cancellation at both boundaries.

- [ ] **Step 4: Make the import service own exactly one session**

Open after catalogue decoding, use a `using` boundary spanning batch rendering, aggregate timings, then dispose before atomic publication.

- [ ] **Step 5: Run focused tests and verify GREEN**

Expected: progress, zero-work, cancellation, disposal, and no-publication-on-failure tests pass.

- [ ] **Step 6: Commit**

`git commit -m "perf: batch vanilla snapshot generation"`

### Task 3: Editor progress and diagnostic timings

**Files:**
- Modify: `Editor/Catalogs/Vanilla/PlateUpCatalogImportController.cs`
- Modify: `Editor/Catalogs/Vanilla/PlateUpCatalogImportService.cs`
- Modify: `Tools~/Verify-Package.ps1`
- Test: `C:/Users/Yariazen/Documents/ChatGPT/Plateup/verification/alpha7-snapshot-performance/VanillaCatalogProgressTests.cs`

**Interfaces:**
- Consumes: Task 2 progress events and aggregate timings.
- Produces: progress text `Rendering snapshot X of Y: <name>` and one completion log containing validation, decode, render, publication, and total elapsed milliseconds.

- [ ] **Step 1: Write failing formatting and synchronization-context tests**

Assert exact progress text/counts, cancellation forwarding, zero-work formatting, and that editor UI callbacks are posted through `Progress<T>` rather than invoked from the worker.

- [ ] **Step 2: Run focused tests and verify RED**

Expected: failure because the controller only reports four broad phases.

- [ ] **Step 3: Wire item progress into the cancelable progress bar**

Retain broad scan/decode/publish phases and replace the render plateau with exact item counts and the current GameObject key.

- [ ] **Step 4: Emit one bounded timing summary**

Log milliseconds for validation, decode, Unity render, publication, and total only after successful publication; do not log per-resource noise.

- [ ] **Step 5: Extend the package gate**

Require the versioned session API and item-progress formatting while continuing to forbid test/planning content from the distributable tree.

- [ ] **Step 6: Run focused tests and package verification**

Expected: the external non-Unity harness passes and `Tools~/Verify-Package.ps1` reports package success before release metadata changes.

- [ ] **Step 7: Commit**

`git commit -m "feat: report vanilla snapshot batch progress"`

### Task 4: Whole-branch verification and plan cleanup

**Files:**
- Delete before release: `docs/superpowers/specs/2026-10-28-vanilla-snapshot-batch-performance-design.md`
- Delete before release: `docs/superpowers/plans/2026-10-28-vanilla-snapshot-batch-performance.md`

**Interfaces:**
- Consumes: Tasks 1-3.
- Produces: an alpha.7 branch whose distributable tree passes the production-only gate.

- [ ] **Step 1: Run the complete external non-Unity harness and GitHub package workflow**

Expected: orchestration tests and package verification pass; no local Unity execution. Record real Unity integration as deferred until the self-hosted runner is available.

- [ ] **Step 2: Run static release checks**

Run `Tools~/Verify-Package.ps1`, `git diff --check`, and build/inspect the allowlisted archive outside the checkout.

- [ ] **Step 3: Remove internal planning files from the distributable tree**

Delete the spec and plan because repository policy forbids `docs/` in the package root; preserve their committed history.

- [ ] **Step 4: Re-run package verification**

Expected: production-only package gate passes with no `docs/`, tests, generated game data, or agent files.

- [ ] **Step 5: Commit**

`git commit -m "chore: finalize alpha 7 snapshot performance work"`
