# Third Party Notices

KitchenLib Studio includes the following editor-only dependencies under `Editor/ThirdParty/AssetsTools.NET/`. Their existing binaries, Unity import metadata, README, and license texts are preserved. They must not be included in player builds or generated mod bundles.

| Component | Version / provenance | License notice |
| --- | --- | --- |
| AssetsTools.NET.dll | NuGet AssetsTools.NET 3.0.5, .NET Standard 2.0 | MIT; copyright (c) 2020 nesrak1; complete text in `LICENSE-AssetsTools.NET.txt` |
| AssetsTools.NET.MonoCecil.dll | NuGet AssetsTools.NET.MonoCecil 3.0.4, .NET Standard 2.0 | MIT; complete text in `LICENSE-AssetsTools.NET.txt` |
| Mono.Cecil.dll | NuGet Mono.Cecil 0.11.6, .NET Standard 2.0 | MIT; copyright (c) 2008–2015 Jb Evain and (c) 2008–2011 Novell, Inc.; complete text in `LICENSE-Mono.Cecil.txt` |
| Mono.Cecil.Rocks.dll | Matching Mono.Cecil 0.11.6 extension assembly | MIT; complete text in `LICENSE-Mono.Cecil.txt` |
| classdata.tpk | Existing `lz4.tpk` from the AssetRipper/Tpk `type_tree_tpk` workflow's `lz4_file` artifact | The inherited README records provenance but no separate license notice or pinned revision. Verify redistribution terms and preserve any required notice before public distribution. |

Unity module dependencies are supplied by Unity, under Unity's terms; they are not vendored here. PlateUp assemblies and imported game data are not licensed or distributed by this package.
