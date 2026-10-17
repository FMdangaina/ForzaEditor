# Research notes

Background of the ROADMAP topics 11-15: quotes, file names, what is known so far. The order of
work and the status are in `ROADMAP.md` only.

## Cleanup and platform (ROADMAP 12)

- **.NET:** today `v2.0` with XNA, x86. The renderer stays XNA, so the target is the newest .NET XNA
  allows. To find out what else breaks (SQLite, HexBox). Console Modding is already gone (12.1),
  so `xdevkit.dll` no longer blocks it.

## 3D viewer and materials (ROADMAP 13)

- **Renderer:** a new one is almost impossible, the viewer stays on XNA (ROADMAP "Maybe later").
- **Game materials:** hardcoded in the executable, or shader files somewhere (DVD, zips,
  `shadersettings`)? If the FM shaders are found, use or imitate them for a real-time preview.
- **Viewer camera:** the game's FOVs are vertical degrees, in `Media\Camera\CameraSettings.ini`
  (race cameras) and `Media\UICarTargetCameraPresets.xml` (garage, photo mode).

## Wheels and morph targets (ROADMAP 11, and 7)

- **C1. Wheel morph.** From @Jew: *"it's dependant on the wheel having a morph target in its
  package"*. It looks like a flag travelling with the shader and `IsStock`; a texture is mentioned
  too: larger texture = resizable wheel.
  - Comparison: the BMW E30 rim does not scale, the S60 one does. @Jew: *"You need morph targets in
    the wheel model for Forza to scale them to size"*. Name to look for: `Morph_LOD0`.
  - Linked to the tyre width on ported cars (ROADMAP 1).
- **C2. OBJ wheel import** (@delete.diskord on Discord): *"i used forza studio to export an fm3
  wheel to obj, then opened an fm4 wheel zip in forzaeditor and attempted to replace the wheel with
  the obj i exported, resulting in the 'LOD 0 and LODs 1+ live in different files' error. however i
  did try the fm3 conversion (port car from another forza) in the new commit a little while ago and
  the wheel zip it output seems to work perfectly in fm4."* The error comes from `CarbinModel.cs`:
  an FM4 wheel keeps LOD0 and LOD1+ in two carbins. Fix: split the OBJ instead of stopping.

## Other models (ROADMAP 14)

- FH1/FH2 traffic models: format unknown, to open.
- FM2 models: out of scope today, export only.

## Documentation and community (ROADMAP 15)

- Format documentation in `docs/` (carbin, xds, Anark, gamedb, DLC...).
- Discord feedback: still few users; reports go into ROADMAP topics, like C2.
