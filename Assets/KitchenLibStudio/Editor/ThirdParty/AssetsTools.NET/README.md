# AssetsTools.NET editor dependencies

These files are used only by KitchenLib Studio's Unity Editor assembly to read
installed PlateUp metadata. They are not runtime mod dependencies and must not
be included in player builds or generated AssetBundles.

- `AssetsTools.NET.dll`: NuGet `AssetsTools.NET` 3.0.5, .NET Standard 2.0, MIT.
- `AssetsTools.NET.MonoCecil.dll`: NuGet `AssetsTools.NET.MonoCecil` 3.0.4,
  .NET Standard 2.0, MIT. (There is no 3.0.5 adapter release.)
- `Mono.Cecil.dll`: NuGet `Mono.Cecil` 0.11.6, .NET Standard 2.0, MIT.
- `Mono.Cecil.Rocks.dll`: the matching Mono.Cecil 0.11.6 extension assembly
  required transitively by the MonoCecil adapter, .NET Standard 2.0, MIT.
- `classdata.tpk`: current `lz4.tpk` from the official `AssetRipper/Tpk`
  `type_tree_tpk` workflow's `lz4_file` artifact. It supplies built-in Unity
  player type trees for stripped serialized files; PlateUp's Unity 2020.3
  files have type trees disabled.

Only ordinary Unity serialization fields needed for GDO identity are read.
The importer does not execute installed assemblies or decode Odin payloads.
