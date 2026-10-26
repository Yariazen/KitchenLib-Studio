# KitchenLib Studio bundle projects

Open **KitchenLib Studio → GDO Bundle Graph**, choose **Create Bundle Project**, select an existing parent folder under `Assets`, and enter a project name. Studio creates `<parent>/<project>/<project>.asset` with owned `GDOs` and `Content` folders. The graph is the bundle descriptor: only its local GDO members are built. Use **Open Bundle Project** to reopen it.

Create GDOs from the graph, choose a generated type, and enter the GDO name. Studio derives the path in `GDOs` and the negative runtime ID from the normalized `project:gdo` identity. Relationships and external references are authored in the graph; expanded GDO nodes expose intrinsic fields and prefab references. Save the project before closing Unity. Build writes `Build/<project>/content/<project-slug>.assets` and a package audit report.

New GDO companions (intrinsic values, localization and relationship projections) are stored in `<project>/Editor/Overlays/<GDO-guid>.asset`. Transfer the complete project folder and its `.meta` files to preserve authored data and GUID references. Referenced prefabs and other content must also be transferred; keep those in `Content`. Existing shared companions under `Assets/ModContent/Editor/KitchenLibStudioOverlays` remain readable, including older YAML. For an existing project, choose **Collect Project Overlays** to move its shared companions into the owned folder while preserving their GUIDs, then transfer the project.

Every local member must remain under the project's `GDOs` folder. Moving it elsewhere produces a member-specific blocking diagnostic and stops a build before any output is created. Creating a GDO preflights calculated-ID collisions with local and known external references.

Use **Remove Member** on local, external or missing nodes to remove graph membership while preserving any source asset and its companion. Connected edges and owned relationship nodes require explicit confirmation; canceling leaves the graph and projections unchanged. Removal is undoable. You can remove broken members even when other invalid members remain; remaining errors keep builds blocked, and projections refresh once those errors are resolved. Relationship nodes have their own removal action.

## Renaming and compatibility

Use **Rename Project** in the toolbar or **Rename GDO** on a local node. Review the old/new canonical identities, runtime IDs, and affected count, then explicitly acknowledge the compatibility warning to confirm. A project rename recalculates every local GDO identity; a GDO rename recalculates that GDO's identity. Changed IDs can break existing saves and external references in other mods. Even case-only renames require confirmation. Canceling changes nothing. Asset GUIDs and internal graph connections survive coordinated renames; avoid renaming owned assets directly in the Project window.

A successful rename clears the Unity editor's entire Undo/Redo history, as stated in the confirmation. Unity 2020.3 cannot reliably undo these filesystem operations or clear all creation records per affected object. Clearing history prevents older graph, GDO and companion snapshots from restoring identities that no longer match paths. Failed renames explicitly restore paths, identities, graph metadata and companion contents. Relationship projections are refreshed in the same transaction.

## Dough Ball in nine steps

1. Create the bundle project `MyBakery`.
2. Create a `KitchenData.ItemGroup` named `DoughBall`; its canonical identity is `mybakery:doughball`.
3. On its node choose **Add Relationship → Sets**.
4. In the Item Set node set `Min = 2`, `Max = 2`, and `IsMandatory = true`.
5. Add two entries to `Items` to expose **Items 0** and **Items 1** ports.
6. Add two external `KitchenData.Item` reference nodes, entering the known runtime IDs for Flour and Water from your target game version.
7. Connect Flour to **Items 0** and Water to **Items 1**.
8. Assign the Dough Ball prefab and any other intrinsic Item/ItemGroup fields on the expanded GDO node.
9. Save, resolve validation issues, and choose **Build**. The bundle contains the ItemGroup and a data-only overlay holding both external IDs; the runtime linker resolves those references in the game.

Use the actual Flour and Water runtime IDs from your target game version. Studio needs no PlateUp assemblies copied into the Unity authoring project.
