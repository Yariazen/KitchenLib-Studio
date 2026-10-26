# Changelog

## [0.1.0-alpha.1]

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
- Pin and acknowledge the exact AssetRipper type-tree data provenance used by the owner-only alpha.
- Show a non-blocking type-tree attribution notice in KitchenLib Studio Setup.

Internal alpha label: `0.1.0-alpha.1`. No public tag or archive is authorized. Replace the placeholder repository URL and original-code license before public distribution.
