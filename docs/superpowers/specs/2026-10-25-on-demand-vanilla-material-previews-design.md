# On-Demand Vanilla Material Previews Design

## Intent

KitchenLib Studio should let authors find a vanilla PlateUp material and use a faithful approximation while designing GameObjects in Unity. These reconstructed materials are editor previews, not mod-owned content and not runtime payloads. One shared preview cache serves every mod project.

## Goals

- Provide a dedicated, searchable browser for vanilla materials in the generated vanilla asset catalogue.
- Prioritize ordinary object-surface materials used by food, appliances, clothing, and furniture.
- Read a selected material's descriptive parameters on demand from the installed PlateUp files and reconstruct a new approximation without copying authored vanilla assets.
- Persist a reusable Unity `.mat` preview asset so it can be assigned through normal `Material` fields and `Renderer.sharedMaterials` slots.
- Store stable vanilla identity using the catalogue key plus source file/path ID; names are display data, not identity.
- Share and deduplicate preview assets across all mod projects.
- Prevent reconstructed preview materials and generated preview-only resources from being included in built AssetBundles by rejecting builds whose dependency closure contains them.

## Non-Goals

- Bulk-importing every vanilla material.
- Providing faithful first-release previews for fonts, UI atlases, maps, paintings, signage, icons, screens, water, glass, glow, fire, or other image-driven and special-effect materials.
- Reproducing unavailable compiled PlateUp shaders exactly inside the editor.
- Extracting, copying, converting, or persisting vanilla textures, shaders, meshes, animation data, or other authored binary assets.
- Treating KitchenLib's runtime material cache as an editor dependency.
- Creating per-mod copies of vanilla preview materials.
- Exporting reconstructed preview materials or textures as mod content.

## Anti-Goals

- Do not identify materials only by name; PlateUp contains duplicate names.
- Do not write decoded vanilla texture bytes or shader binaries into the Unity project or cache.
- Do not treat access to an installed game as permission to redistribute its content.
- Do not rely on an `Editor` or `Generated` folder name to exclude referenced assets from AssetBundles.
- Do not strip or rewrite preview material references in this release.
- Do not mutate source prefabs while preparing a bundle.

## Existing Capabilities

- `VanillaAssetCatalogData` already describes material records, stable source identities, and renderer-to-material edges.
- `AssetsToolsPlateUpPreviewDecoderV2` already decodes shader names, floats, colors, vectors, texture references, scale, and offset from installed PlateUp files.
- `PlateUpTextureDecoder` already decodes referenced texture payloads.
- `VanillaPreviewReconstructor` already builds temporary preview materials and includes Standard-shader fallback mappings for common PlateUp properties.
- `GdoBundleBuilder` builds explicitly listed assets and Unity recursively includes their dependencies.
- `GdoPackageAudit` currently accepts materials, meshes, textures, and shaders when they are dependencies of bundled prefabs. Therefore a referenced preview material would currently be exported unless the build is rejected before `BuildPipeline` runs.

## KitchenLib Prior Art

KitchenLib discovers already-loaded runtime materials with `Resources.FindObjectsOfTypeAll<Material>()`, indexes them by name, filters them by shader, and can replace editor materials with runtime materials by name. This validates the value of a searchable shared index, but it does not solve offline reconstruction. KitchenLib Studio must keep stable source identity because duplicate names make a name-only index ambiguous.

## Approaches Considered

### Dedicated vanilla material browser — selected

A focused editor window searches material catalogue records, decodes a selected record, previews it, and creates or refreshes its shared proxy. This keeps material authoring discoverable and separate from GameObject snapshots.

### Extend the Snapshot Browser

This would reuse an existing window but mix GameObject rendering with material import/cache management. The controls, selection model, and output semantics differ enough that the combined window would become harder to understand.

### Import from every Material object field

This offers the shortest eventual workflow but requires invasive custom picker integration across inspectors and graph controls. It can be added later on top of the same cache service.

## Authoring Workflow

1. Open **KitchenLib Studio > Vanilla Material Browser**.
2. Search by display name or catalogue key. Results show enough source identity to distinguish duplicate names.
3. Select a result to decode and preview it without writing assets.
4. Choose **Create Preview Asset**. The browser creates or refreshes the shared proxy and selects it in the Project window.
5. Assign the proxy normally to a Renderer or KitchenLib Studio prefab node while designing a GameObject.
6. Reopening or reimporting the same stable identity returns the existing proxy rather than creating another copy.

The browser defaults to the supported object-surface set: food, appliance, clothing, and furniture materials. Other categories may be shown behind an unsupported/experimental filter, but they are not first-release acceptance targets. A material whose appearance depends primarily on an authored image or custom effect is labelled unavailable or limited rather than encouraging a misleading import.

## Shared Preview Cache

- Root: `Assets/KitchenLibStudio/Generated/VanillaMaterialPreviews/`.
- Each proxy has a collision-safe filename derived from a sanitized display name plus a short stable-key suffix.
- Optional procedural placeholder resources live below the same root and are deduplicated by semantic role, not vanilla asset identity.
- A small editor-only metadata asset records reconstruction schema version, catalogue key, source relative path, source path ID, source fingerprint, original material name, decoded shader-name hint, and the reconstructed proxy/generated-resource paths.
- Refresh compares the recorded source fingerprint and decoder/importer version before rewriting assets.
- Preview assets are recoverable from PlateUp, but referenced assets must retain their Unity GUIDs. Refresh rewrites assets in place; cleanup refuses to delete referenced proxies and offers an explicit repair workflow for missing cache entries.

## Material Reconstruction

- Add a public material-record decode entry point to the V2 decoder; its current catalogue entry point accepts only GameObjects and its material decoder is private.
- Reuse the existing material-property decoding path rather than implementing a second serialized-material parser, but stop at descriptive data such as shader name, floats, colors, vectors, texture-slot names, and scale/offset. Do not return or persist source texture bytes for this workflow.
- Use a Studio- or Unity-provided shader selected from an explicit allowlist. A decoded shader name is a compatibility hint only; the workflow never extracts or copies a PlateUp shader.
- Otherwise use the existing Standard fallback and property translations, including `_Color0` to `_Color`, `_Shininess` to `_Glossiness`, and `_Specular` to `_Metallic`.
- Preserve texture-slot names and scale/offset as reconstruction metadata, but leave slots empty or use clearly synthetic, Studio-generated neutral placeholders. Generated placeholders must be original procedural data, shared by semantic role, and visibly identified as previews.
- The first version approximates transparency only when decoded properties and the fallback shader expose enough information; exact keyword and render-queue reproduction is deferred because the preview model does not currently retain them.
- Surface a diagnostic when properties cannot be represented by the chosen editor shader; importing remains allowed because the asset is explicitly an approximation.

## Copyright and Asset-Safety Boundary

- The feature reconstructs a functional visual approximation; it does not reproduce or export PlateUp's authored assets.
- Permitted inputs are structural identity and descriptive material parameters needed to build that approximation: names, source identifiers, shader-name hints, numeric values, colors, vectors, texture-slot names, and UV scale/offset.
- Prohibited outputs include source texture pixels, shader binaries or source, meshes, animation clips, audio, and converted or losslessly transformed versions of those assets.
- Any texture used by a proxy must already belong to KitchenLib Studio/the user or be newly generated from a simple procedural recipe. It must not be derived from decoded vanilla pixels.
- Cache metadata records provenance and a reconstruction schema version so generated resources remain distinguishable from game content.
- If a material cannot be usefully approximated without copying an authored asset, the browser reports that limitation instead of importing it.

## Vertex-Color Interaction

PlateUp commonly uses mesh vertex colors to shape object coloration and its outline post-process. Reconstructed materials should therefore support multiplying the base color by user-authored vertex color when the selected Studio shader allows it. The browser warns that an uncolored mesh may look plain and that the editor preview does not reproduce PlateUp's final post-process outlines.

The system does not copy vertex-color arrays from vanilla meshes. Custom objects, including the Bao, use newly authored vertex colors. This feature is useful for the prioritized food, appliance, clothing, and furniture surfaces, but exact outline emulation is not required for the first release.

## Build Boundary

Folder location is not an export boundary: Unity recursively includes prefab dependencies. Before invoking `BuildPipeline`, the builder must inspect the complete dependency closure of the assets selected for export. If that closure contains a reconstructed vanilla preview material or one of its generated preview-only resources, the build stops with an actionable error identifying the owning asset and dependency. Source assets remain unchanged.

This release does not stage sanitized prefabs, strip material slots, emit a sidecar, or resolve materials at runtime. Those behaviors require a separately owned runtime contract and are deferred.

## Validation and Failure Behavior

- Block creation when the catalogue record is not a material or the PlateUp source files are unavailable.
- Report decoder failures without creating partial assets.
- Create/refresh reconstructed materials, generated placeholders, and metadata transactionally; a failure restores the prior usable cache entry.
- Reject any decoder result that contains source texture payloads or another prohibited asset payload; do not write it to disk.
- Detect and distinguish duplicate display names in search results.
- Build validation fails if any preview proxy or generated preview-only resource appears anywhere in the AssetBundle dependency closure, including through nested prefabs, variants, GDO references, or other serialized fields.
- The diagnostic identifies the export root and offending dependency so the author can replace or clear it deliberately.
- A proxy elsewhere in the shared cache does not block an unrelated build.
- Source prefabs and their preview assignments remain unchanged after validation.

## Testing Strategy

Tests are split by cost and dependency so the default Editor suite remains fast and deterministic.

### Fast unit and editor-asset tests

- Test material-only catalogue filtering, duplicate-name disambiguation, and deterministic cache paths derived from stable source identity rather than display name.
- Test that the default supported view prioritizes food, appliance, clothing, and furniture surface materials and clearly labels image-driven or special-effect entries as limited/unavailable.
- Test import planning for create, reuse, refresh, stale-source, and unsupported-source cases using injected catalogue and decoder fakes.
- Persist decoded floats, colors, vectors, texture-slot metadata, scale, and offset into ordinary Unity assets in a temporary `Assets` test folder.
- Verify repeated import of the same identity reuses one proxy, while two source identities with the same display name create distinct proxies.
- Verify generated neutral placeholders deduplicate by semantic role and contain no decoded vanilla pixel payload.
- Refresh material and generated placeholder assets in place and assert their Unity GUIDs are preserved, so existing prefab references survive.
- Verify decode/import failure is transactional: no partial assets or temporary files remain and an older valid cache entry is preserved.
- Verify cleanup refuses to remove a proxy that is referenced by an authoring asset.
- Exercise browser presentation and commands through injected services: searching, duplicate labels, read-only preview, import/refresh state, and selecting an existing proxy. UI tests do not read the installed game.
- Verify the reconstructed surface shader uses user-authored vertex color when available and remains usable, with a warning, on meshes that have none.

### Decoder contract tests

- Test the new public material-record decode entry point independently of GameObject reconstruction.
- Verify stable material identity, shader-name hint, properties, texture-slot names, scale, and offset propagate into reconstruction data without texture bytes or other authored asset payloads.
- Add a negative contract test that supplies a source texture payload and verifies the importer rejects it before any asset is written.
- Test missing source files, unsupported records, and unavailable shader fallbacks as structured diagnostics.
- Keep one opt-in integration test against an installed PlateUp copy and a known catalogue material. It is skipped with a clear reason when local game files are unavailable and is not a CI prerequisite.
- Use representative food, appliance, clothing, and furniture records as integration fixtures; do not make atlas/UI/effect fidelity an acceptance requirement.

### Export-boundary tests

- Unit-test dependency validation directly: a direct proxy, generated preview resource, nested-prefab reference, and transitive GDO dependency each block the build; an unrelated cached proxy and an ordinary custom material do not.
- Assert rejection occurs before `BuildPipeline` is invoked and reports both the export root and offending preview dependency.
- Assert validation never changes the source prefab or its material slots.
- Keep one end-to-end bundle-builder test proving a proxy-bearing project emits no bundle, plus one ordinary-material control proving normal bundle output still succeeds. Avoid a real AssetBundle build for every validator case.

### Regression coverage

- Run the focused material-browser, decoder-contract, cache, and build-boundary tests on each change.
- Run the existing snapshot decoder, bundle builder, package audit, and deployment-readiness Editor suites before completion.
- There are deliberately no runtime resolver, sidecar, or stripping tests in this release because those features are outside the approved scope.

## Constraints and Remaining Builder Choices

- Unity version remains 2020.3.48f1.
- The generated cache is shared and editor-only; it is not owned by any mod project.
- The first UI is a dedicated browser. Object-field picker integration is deferred.
- The builder may choose the exact proxy metadata representation, following existing manifest and cleanup conventions.
- Runtime resolution cannot currently integrate with this repository: KitchenLib Studio's assembly is Editor-only and no runtime bundle loader or sidecar consumer exists in this checkout.

## Approved Product Decision

The first release is editor-only: implement the browser and shared persistent proxies, but fail bundle validation whenever an exported dependency still references one. This is complete and safe without a runtime component.

End-to-end runtime support is deferred until an external runtime loader repository and contract are identified, or a separately deployed runtime module is explicitly added. Only then may a future design consider stripping proxies and emitting bindings.

Emitting an unconsumed sidecar or clearing material slots without a resolver is forbidden.
