# Research and to-do list

Ideas and research, grouped by subject. Every point is also an item in `ROADMAP.md` (topic number
in brackets), where the order of work is set. This file keeps the background: quotes, file names,
what is known so far.

Status: ⬜ to do · 🔍 research (understand first, then write code) · 🐞 reported bug

---

## A. Cleanup and platform (ROADMAP 12)

- [x] ✅ **A1. Remove Console Modding.** Done 2026-10-16. Drops the dependency on `xdevkit.dll` (Xbox 360 SDK, not
      needed). Files: `ConsoleModding.cs` + `.Designer.cs`, `ConsoleFunctions.cs`,
      `ConsoleMethod.cs`, references in `ForzaStudioForm.cs` and `ArchiveEditorForm.cs`, the
      reference in `ForzaEditor.csproj`. Small; do it before A2.
- [ ] 🔍 **A2. Move to a recent .NET Framework.** Today the project targets `v2.0` with XNA, x86.
      The renderer stays XNA (see B1), so the target is the newest .NET XNA allows. To find out
      what else breaks (SQLite, HexBox). Depends on A1.
- [ ] ⬜ **A3. Settings window / button** (game folders, Xenia, DEV folder, viewer options).
- [ ] ⬜ **A4. Conversion tool as MDI** (child windows inside the main window instead of separate
      windows).

## B. 3D viewer and materials (ROADMAP 13)

- **B1. Change the renderer** (the dream): almost impossible, the viewer stays on XNA. Parked in
  ROADMAP "Later". B2 and B3 are done within XNA.
- [ ] 🔍 **B2. Game materials.** Find out whether they are hardcoded in the executable or whether
      shader files exist somewhere (DVD, zips, `shadersettings`).
  - [ ] 🔍 B2.1 If the FM shaders are found: use (or imitate) them in the viewer for a real-time
        preview of the in-game look.
  - [ ] ⬜ B2.2 Better carbin viewer with the real materials (depends on B2).
- [ ] 🔧 **B3. Viewer camera like the game's:** the game's FOV and the viewport's aspect ratio.
      Code done 2026-10-16 (ROADMAP 13.3), user check pending. The FOVs are in
      `Media\Camera\CameraSettings.ini` (race cameras) and `Media\UICarTargetCameraPresets.xml`
      (garage, photo mode), vertical degrees.

## C. Wheels and morph targets (ROADMAP 11, C2 in ROADMAP 7)

- [ ] 🔍 **C1. Wheel morph.** From @Jew: *"it's dependant on the wheel having a morph target in its
      package"*. It looks like a flag travelling with the shader and `IsStock`; a texture is
      mentioned too: larger texture = resizable wheel.
  - [ ] 🔍 C1.1 Comparison: the BMW E30 rim does not scale, the S60 one does. @Jew again: *"You need
        morph targets in the wheel model for Forza to scale them to size"*. Name to look for in the
        files: `Morph_LOD0`.
  - Linked to the tyre width on ported cars (ROADMAP 1, "Tyre width").
- [ ] 🐞 **C2. Replacing a wheel by hand from OBJ** (reported by @delete.diskord on Discord):
      *"i used forza studio to export an fm3 wheel to obj, then opened an fm4 wheel zip in
      forzaeditor and attempted to replace the wheel with the obj i exported, resulting in the
      'LOD 0 and LODs 1+ live in different files' error. however i did try the fm3 conversion (port
      car from another forza) in the new commit a little while ago and the wheel zip it output seems
      to work perfectly in fm4."*
      The error comes from `CarbinModel.cs` (the OBJ holds LOD0 and LOD1+ together, but an FM4 wheel
      keeps them in two different carbins). Fix: split the OBJ between the two files instead of
      stopping. The FM3 → FM4 port already works.

## D. Other models to study (ROADMAP 14)

- [ ] 🔍 **D1. FH1/FH2 traffic models:** open them and see what the format is.
- [ ] 🔍 **D2. FM2 models** (FM2 is out of scope today, export only).

## E. Documentation and community (ROADMAP 15)

- [ ] ⬜ **E1. Proper documentation of the formats found** (carbin, xds, Anark, gamedb, DLC...), in
      `docs/`.
- [ ] ⬜ **E2. Discord feedback.** Still few users: collect the reports (like C2) and add them here.
