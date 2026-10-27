# Changelog

## [0.1.0-alpha.4]

This release supersedes the blocked `0.1.0-alpha.3` automation candidate and includes its user-facing changes.

- Automatically discover a standard PlateUp installation when the studio has already detected it.
- Group related GDO types into submenus in the create menu.
- Always confirm graph-first GDO deletion and delete its generated asset with the node.
- Add intuitive material authoring for generated prefabs, including vanilla and custom shaders/materials.
- Run tag-driven packaging and draft-release creation entirely on GitHub-hosted infrastructure; Unity clean-import remains a required maintainer pre-tag check until hosted Unity licensing is configured.

## [0.1.0-alpha.3]

This release supersedes the failed `0.1.0-alpha.2` automation candidate and includes its user-facing changes.

- Automatically discover a standard PlateUp installation when the studio has already detected it.
- Group related GDO types into submenus in the create menu.
- Always confirm graph-first GDO deletion and delete its generated asset with the node.
- Add intuitive material authoring for generated prefabs, including vanilla and custom shaders/materials.
- Accept conventional lightweight or annotated release tags while still requiring the tagged commit to belong to `main`.

## [0.1.0-alpha.2]

- Add graph material authoring for primitive prefabs with lit and flat/unlit presets, custom Material passthrough, custom Shader support, color-property validation, and safely owned generated `.mat` assets.
- Group Create GDO entries into deterministic inheritance-based submenus while keeping every creatable generated type reachable.
- Make the graph authoritative for local GDO deletion: always confirm, then atomically remove the local graph record, relationships, connected edges, owned GDO asset, and companion while keeping external references graph-only.
- Automatically select and remember a valid standard or uniquely discovered PlateUp Steam installation before offering manual folder selection.

## [0.1.0-alpha.1]

- Add root onboarding, manual zip installation guidance, and a maintainer release process for the alpha candidate. Require the README in the package gate and release archive, and check its pinned Git installation URL against package metadata.
- Harden public-surface auditing across conditional configurations, case-sensitive qualified names and all non-template C# source locations. Scope build saves to product assets so unrelated dirty work remains unsaved, and preserve accepted scalar semantic kinds between pre-build processors.
- Finalize the editor-only API with explicit `RegisterBuildProcessor` registration, captured namespaced IDs/orders, ordinal duplicate rejection and deterministic execution. Add immutable `IStudioBuildProcessor`, `StudioBuildContext`, `StudioBuildFieldChange` and `StudioBuildProcessorResult` contracts for build-only replacement of existing scalar companion fields. Contexts/results are bounded, processor failures are isolated, authored state is restored, and original/transformed manifest validation, package audit and final bundle verification remain mandatory. Exact supported shapes, limits and lifecycle behavior are documented in the authoring reference under the existing `0.x` compatibility policy.
- Enforce an exact 22-type public API/signature allowlist in the package gate, retaining internal serialized roots and implementation types. Scope the root build-output ignore rule so `Editor/Api/Build` contracts and future additions remain tracked on every platform.
- Support declarative custom graph nodes through the editor-only `KitchenLib.Studio.Editor.Api`: atomic registration, deferred palette refresh, Studio-owned controls, undoable persistence, missing-definition placeholders, isolated validation and bounded build projection.
- Clarify the `0.x` API: registration requires ordinal `namespace:name` type IDs, non-blank field/port keys without control characters, and the Unity editor thread. `StudioBuildProjection.Values` maps declared output-port keys to authored values (maximum 256 outputs and 65536 characters per text/reference payload). The new `KitchenLibStudioApi.GdoReferenceSemanticTypeId` constant identifies outputs whose Reference values name existing authored GDO nodes for Studio relationship projection; unknown output keys and unresolved GDO references fail validation. Extension findings are limited to 256 per result and cannot impersonate built-in recoverable errors. No extension file-writing or runtime-manifest surface is introduced.
- Persist custom-node instance/type IDs, positions and keyed scalar records with raw payloads; reuse keyed graph edges. Unknown definitions, keys and value discriminants retain their original storage. Existing graph/schema fields and source GUIDs remain unchanged.
- Establish the supported C# API policy: during `0.x`, incompatible API changes require changelog documentation; strict semantic-version compatibility begins at `1.0.0`, with later breaking changes requiring a major version. Serialized stable IDs and field/port keys are protected independently from the first alpha.
- Establish the `com.kitchenlib.studio` Unity Package Manager package for Unity 2020.3.
- Place production editor tooling and required third-party dependencies under `Editor`.
- Document package installation and disposable generated output.
- Add production-only content gating, ephemeral Unity 2020.3 import verification, and allowlisted release archives without shipping a test project or internal tests.
- Pin and acknowledge the exact AssetRipper type-tree data provenance used by the internal alpha.
- Show a non-blocking type-tree attribution notice in KitchenLib Studio Setup.

Current alpha label: `0.1.0-alpha.4`. Original KitchenLib Studio code and documentation are licensed under MIT; bundled third-party material retains its separate status. Release publication remains a separate human decision.
