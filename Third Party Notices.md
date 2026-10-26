# Third Party Notices

KitchenLib Studio includes the following editor-only dependencies under `Editor/ThirdParty/AssetsTools.NET/`. Their existing binaries, Unity import metadata, README, and license texts are preserved. They must not be included in player builds or generated mod bundles.

| Component | Version / provenance | SHA-256 | License notice |
| --- | --- | --- | --- |
| AssetsTools.NET.dll | NuGet AssetsTools.NET 3.0.5, .NET Standard 2.0 | `6B03D589163623B67620FBBC6F425DCB47F856584BD3AB1A34C896B2666372E4` | MIT; copyright (c) 2020 nesrak1; complete text in `LICENSE-AssetsTools.NET.txt` |
| AssetsTools.NET.MonoCecil.dll | NuGet AssetsTools.NET.MonoCecil 3.0.4, .NET Standard 2.0 | `D9358259FACAF85356ED3BDEC5AEBFD9C026ECA05EF28E6413B85935770ACE67` | MIT; complete text in `LICENSE-AssetsTools.NET.txt` |
| Mono.Cecil.dll | NuGet Mono.Cecil 0.11.6, .NET Standard 2.0 | `831DCA77470D85CB6FFBEA3072DAA7A3DF5B7C9FCFD9C3F43674A9BE99D4BFCF` | MIT; copyright (c) 2008–2015 Jb Evain and (c) 2008–2011 Novell, Inc.; complete text in `LICENSE-Mono.Cecil.txt` |
| Mono.Cecil.Rocks.dll | Matching Mono.Cecil 0.11.6 extension assembly | `BF992F3DCE364EBCC3200FA7832EF07E20B4E2DBC3A8A6213CE44E3D239DB984` | MIT; complete text in `LICENSE-Mono.Cecil.txt` |
| classdata.tpk | Existing `lz4.tpk` from the AssetRipper/Tpk `type_tree_tpk` workflow's `lz4_file` artifact; no immutable upstream revision is recorded | `3FAB18495D13E559E0FB9926575AFBD682A7AE780FE1569E351A6123E0059150` | Redistribution terms are unresolved. The inherited README records provenance but no separate license notice or pinned revision. Public distribution is blocked until the source revision and redistribution license are verified and any required notice is preserved. |

Unity module dependencies are supplied by Unity, under Unity's terms; they are not vendored here. PlateUp assemblies and imported game data are not licensed or distributed by this package.

`Tools~/Verify-Package.ps1` pins these hashes so a binary or data update cannot pass the repository gate without a renewed provenance and license audit.
