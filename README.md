# ForzaStudio EVO

(Repository: ForzaStudio-EVO, exe: ForzaStudioEVO.exe.)

Complete utility for Xbox 360 era Forza games modding: Forza Motorsport 4 first, with Forza
Horizon 1 and 2 and Forza Motorsport 3 as sources (Forza Motorsport 2: archives, textures, model
viewing and export, music).

Edits the game's zip archives directly: `.carbin` car models (OBJ export/import,
part transplant and duplication), `.xds` textures (import, export, creation from
images), livery masks, the gamedb, the digital dashes and the cockpit camera,
with the game's music and radio in the status bar.

Ports cars from Forza Horizon, Forza Horizon 2 and Forza Motorsport 3 to FM4, either into the
game's files or as a **DLC package** that leaves the game untouched, for Xenia or a modded
Xbox 360: see [docs/DLC_PACKAGES.md](docs/DLC_PACKAGES.md).

User manual: [docs/MANUAL.md](docs/MANUAL.md). Quick start for the beta zip:
[docs/INSTRUCTIONS.txt](docs/INSTRUCTIONS.txt).

Derived from Forza Studio 4.6 by Mike Davis, with an earlier edit by JYSB59.
Maintained by FMdangaina. The DLC package format was worked out with help from Jew
(@mr.abortion) and XTD (@xtd_ac) on Discord. The engine power calculation (the torque a
given power needs at its rpm, as a scalar of the peak torque) follows BlackBirdOsaka's notes
on Discord.

## License

GNU General Public License v3.0, see [LICENSE](LICENSE). The Forza Studio 4.6 code it is
based on is MIT-licensed (Copyright (C) 2012 Mike Davis); its notice is kept in
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Build

- Visual Studio 2022 (MSBuild), .NET Framework 2.0 target, **x86 only**
- XNA Game Studio 3.1 (Microsoft.Xna.Framework in the GAC)

```
MSBuild ForzaStudioEVO.csproj -p:Configuration=Debug -p:Platform=x86
```

`!ForzaStudio release/` must also contain the non-redistributable files listed in
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
At runtime the editor expects `texconv.exe`, `xds.dll` and the other tools next to the exe.

## Repository policy

No game data and no non-redistributable binaries are ever committed; see
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
