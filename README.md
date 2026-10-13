# ForzaEditor

Complete utility for Xbox 360 era Forza games modding (Forza Motorsport 4, Forza Horizon).

Edits the game's zip archives directly: `.carbin` car models (OBJ export/import,
part transplant and duplication), `.xds` textures (import, export, creation from
images), livery masks and the gamedb.

## Build

- Visual Studio 2022 (MSBuild), .NET Framework 2.0 target, **x86 only**
- XNA Game Studio 3.1 (Microsoft.Xna.Framework in the GAC)

```
MSBuild ForzaStudio.csproj -p:Configuration=Debug -p:Platform=x86
```

`!ForzaStudio release/` must also contain the non-redistributable files listed in
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) (`xdevkit.dll` is needed to compile).
At runtime the editor expects `texconv.exe`, `xds.dll` and the other tools next to the exe.

## Repository policy

No game data and no non-redistributable binaries are ever committed; see
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
