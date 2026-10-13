# ForzaEditor

Complete utility for Xbox 360 era Forza games modding: Forza Motorsport 4 and Forza Horizon 2
(Forza Motorsport 2: archives, textures, model viewing and music; model editing to come).

Edits the game's zip archives directly: `.carbin` car models (OBJ export/import,
part transplant and duplication), `.xds` textures (import, export, creation from
images), livery masks and the gamedb, with the game's music and FH2 radio in the status bar.

Derived from Forza Studio 4.6 by Mike Davis, with an earlier edit by JYSB59.
Maintained by FMdangaina.

## License

GNU General Public License v3.0, see [LICENSE](LICENSE). The Forza Studio 4.6 code it is
based on is MIT-licensed (Copyright (C) 2012 Mike Davis); its notice is kept in
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Build

- Visual Studio 2022 (MSBuild), .NET Framework 2.0 target, **x86 only**
- XNA Game Studio 3.1 (Microsoft.Xna.Framework in the GAC)

```
MSBuild ForzaEditor.csproj -p:Configuration=Debug -p:Platform=x86
```

`!ForzaStudio release/` must also contain the non-redistributable files listed in
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) (`xdevkit.dll` is needed to compile).
At runtime the editor expects `texconv.exe`, `xds.dll` and the other tools next to the exe.

## Repository policy

No game data and no non-redistributable binaries are ever committed; see
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
