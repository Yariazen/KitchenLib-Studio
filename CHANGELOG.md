# Changelog

## [0.1.0-alpha.1]

- Define the editor-only `KitchenLib.Studio.Editor.Api` contracts for declarative graph nodes, scalar authoring values, validation findings, and bounded build projections. Explicit registration currently records definitions; graph rendering, persistence, validation scheduling, and build integration follow separately.
- Establish the supported C# API policy: during `0.x`, incompatible API changes require changelog documentation; strict semantic-version compatibility begins at `1.0.0`, with later breaking changes requiring a major version. Serialized stable IDs and field/port keys are protected independently from the first alpha.
- Establish the `com.kitchenlib.studio` Unity Package Manager package for Unity 2020.3.
- Place production editor tooling and required third-party dependencies under `Editor`.
- Document package installation and disposable generated output.
- Add production-only content gating, ephemeral Unity 2020.3 import verification, and allowlisted release archives without shipping a test project or internal tests.
- Pin and acknowledge the exact AssetRipper type-tree data provenance used by the owner-only alpha.
- Show a non-blocking type-tree attribution notice in KitchenLib Studio Setup.

Internal alpha label: `0.1.0-alpha.1`. No public tag or archive is authorized. Replace the placeholder repository URL and original-code license before public distribution.
