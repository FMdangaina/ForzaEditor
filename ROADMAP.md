# Forza Studio roadmap

How we work:

- **One item at a time.** The item under **Now** is the only one being worked on. It is closed before anything else starts.
- **An item is done** when it works (checked in game, if it touches the game), is committed, and is ticked here.
- Each topic lists what is left (`[ ]`) first, then what is done (`[x]`), one line each. Details of
  finished work live in the commits, `docs/` and the code comments, not here.
- New ideas go to **Maybe later**, not into the current item.
- **Game folders:** tests and installs go only to `FM4_Extracted_DEV`. `FM4_Extracted` is the clean copy and is never written.
- Other lists: `current_issues.md` (problems seen in game, cause unknown), `docs/todo_research.md`
  (background of topics 11-15).

Status: ✅ done · 🔧 in progress · ⏸ paused · ⬜ not started

## Now

**12: Cleanup and platform, with a friendlier and simpler UI** (user's choice 2026-10-17: "cerca di
renderla più amichevole e semplice"). Steps in topic 12.

## Next (in order)

Fastest first.

Quick (hours to a day):

1. 9 3i: backfire flames missing on FM3 ports (study first: where FM4 places the flame)
2. 7: OBJ wheel import, LOD 0 and LODs 1+ split between the two carbins (bug from Discord)

Medium (a few days):

3. 11: Wheel morph targets (why the E30 rim does not scale and the S60 one does)
4. 14.1: FH1/FH2 traffic models
5. 15.1: Format documentation (grows with each topic)

Long (research first, size unknown):

6. 3.9: Port FH → FM4 from a FH1/FH2 car DLC package (**very important**; was Now, paused 2026-10-17 for 12)
7. 13.1: Game materials and shaders (hardcoded or shader files)
8. 14.2: FM2 models

Always open: 15.2 Discord feedback, added here as items when reported.

---

## 1. Car port: Forza Horizon 2 → FM4 ✅ working, open points

Tools > Port Car From Another Forza… (`CarPort.cs`, `CarPortForm.cs`).

- [ ] Tyre width on ported cars whose wheel mesh is wider than usual
- [ ] In-game check of a car installed with review fixes
- [ ] FH1/FH2 ports do not deform in a crash: FH damage words differ from FM4's (`current_issues.md` 4)
- [ ] Big-endian vs little-endian gr2: find the differences before writing a converter (own topic)
- [x] Model, wheel, brake conversion; DB rows with reference closure; strings in 18 languages
- [x] Digital dash to the FM4 Anark layout; carattribs, shadersettings, little-endian gr2 animations
- [x] Engine sound, harmonic tunings (donor's when the FH car has none), sound banks from the source game
- [x] FM4 stock tyre rule, local PI mapping, class from PI
- [x] Port window: car list, donor ranking, trial run, data review step with fixes, install
- [x] `port_log.txt` inside the converted car zip
- [x] Ported cars work in game; trial run passes on all 73 FH2 cars

## 2. Car port: Forza Horizon 1 → FM4 ✅ done

Same tool and code as topic 1; its open points are listed there.

- [x] FH1 as source; ported cars work in game (458 Spider, S65 AMG, R8 GT, R8 GT Spyder)
- [x] Fixes: donor animations on cars without gr2, missing engine sound banks

## 3. Ported cars as a DLC package ✅ done, open points

A separate DLC package instead of writing into the game's files. Credits: **Jew (@mr.abortion)**
(`fm4dlc` toolchain) and **XTD (@xtd_ac)** (Xenia folder DLC, XLAST package, console tests).
Documentation: `docs/DLC_PACKAGES.md`. Material: `fm4dev\DEV_DLC`.

- [ ] 3.6d Several Forza Studio packs at once: each pack's string tables built on top of the other
      installed packs' (today only the highest priority pack shows its car names)
- [ ] 3.8 Transfer to the console: FTP (Aurora/FSD) or Xbox 360 Neighborhood (the user's side)
- [ ] 3.9 **Very important** (paused 2026-10-17 for topic 12). Port FH → FM4 from a FH1/FH2 car DLC (STFS `CON`/`LIVE` or its
      extracted folder): base game for shared data, the package's `<offer>000_merge.slt`, string
      tables, car/wheel zips and `_pri_N` folders, audio on top. DLC cars appear in the car list
      and go into an FM4 pack. Material: `fm4dev\Forza Horizon 1 DLC` (20 official FH1 packages)
- [x] 3.1-3.3 Real DLC studied; merge DB `<offer>000_merge.slt` built from a port on a clean DB copy
- [x] 3.4-3.5 Xenia package folder with header; works in game with the base game untouched
- [x] 3.6 Port window DLC pack panel (n cars, trial, build into Xenia); multi-car pack checked in game
- [x] 3.6e/f Port window restyled; FM4 material list measured again on the original cars
- [x] 3.7 STFS package for a real Xbox 360 (RGH/JTAG, `StfsPackage.cs`), works on the console

## 4. Materials FM4 renders black ✅ done

- [x] Port report lists them, model view marks them red, Rename Material… (checked by the user)
- [x] No fixed FH → FM4 table for now (automatic mapping: see Maybe later)

## 5. Digital dash (Anark) editor 🔧

- [ ] Edit in place: position, scale, colour, text, texture, then a test in game (works, to commit)
  - [x] 5.1 Edit model (`AnarkDash`): a value is written where it lives (graph property, else .bsg base or
        .fbf record), nothing is added (the .bgf header counts strings and properties); 585 dashes, 0 failures
  - [x] 5.2 Edit panel above the property list in the Dash view, live preview, edits are pending entries
        saved with File > Save (save path checked by `fm4dev\anark\tools\dashsave.exe`); UI accepted by the user
  - [x] 5.3 Test in game (DEV): text colour change works (2026-10-17)
- [ ] Slides (pages shown one at a time)
- [ ] Behaviour parameter names
- [x] .fbf / .bgf / .bsg parsed (431/431 byte-identical), read-only Dash view
- [x] FM3 dashes (0x3EE) read too: 154/154 byte-identical in all 3 formats (with 9 3f)

## 6. Game database editor ✅ working

- [ ] Decode `EventRestrictions.Value`
- [x] Cars, tracks, events, wheels, tables views; creation tools
- [x] Relation map for FM3/FM4/FH1/FH2, Go to / Used by, reference check

## 7. Model editing (FM4) ✅ working, tests pending

- [ ] OBJ import into a wheel zip: an OBJ with LOD 0 and LODs 1+ stops with "LOD 0 and LODs 1+ live
      in different files" (`CarbinModel.cs`); split it between the two carbins (@delete.diskord)
- [ ] In-game test of a whole model rebuilt from OBJ, of edited parts and of Merge Into
- [ ] Effect of the cleared glass A/B arrays on rebuilt parts
- [x] Edit Part (move/rotate/scale, merge, delete), OBJ part import/export, OBJ work folder

## 8. Automatic LODs ⬜

- [ ] Decimation (QEM) from lod0 to lod1-lod5 at the measured ratios (23.7 / 8.8 / 6.7 / 3.4 / 2.5 %)
- [ ] Keep UV seams, normals, materials and damage data; hook into the OBJ work folder import

## 9. Forza Motorsport 3 → FM4 🔧

- [ ] **3i** No backfire flames on FM3 ports in FM4 (user, 2026-10-17). Sound side is the same in both
      games (`BackfireLevels` from exhaust + cam upgrade, identical audio configs); the flame is FM4 only
      (`Media\Effects\Backfire.xml`, `Flame.tga`, `FireDistortion.tga`). To find: where FM4 places the
      flame (exhaust point in the carbin, DB or fixed) and what FM3 cars lack. Compare an FM4 car with
      flames and the same car from FM3. Notes in memory `backfire-findings`
- [x] 3h Automatic cockpit camera on FM3 cars centred on the steering wheel, checked by the user
      2026-10-17: the port kept FM3's `CamCockpitOffsetX` (about 0.020; FM4's is 0 on 495/503 cars);
      port and the panel's rebuild now set offset x to 0 (`CarCamera.SetSteeringWheelFromModel`)
- [x] 3g Engine sound banks of the same name: rule for now (user, 2026-10-17): FM4's bank when the
      names match, the source game's when FM4 lacks it. Listening tool moved to Maybe later
- [x] 3f FM3 digital dashes, checked in game 2026-10-17 (AUD_1_RS6_03 in DEV):
      FM3 dashes are Anark too (.fbf 0x3EE: text tail 33 bytes instead of 34; .bgf/.bsg as FM4's), kept
      outside the car (`Data_Car.GaugeID` -> `List_DigitalGauges` -> `Media\DigitalGauges\<name>.zip`,
      textures in `UI\Textures.zip` ui4\dashboards, linear DXT). The port writes them into the car zip
      (`digitalgauge/dash_<car>.*` + textures in FM4's gamma form), sets GaugeID -1 (FM4's value on
      every car; the old port left FM3's id). All 154 FM3 dashes convert, 2252/2252 images found,
      46/46 FM3-only DVD1 cars pass the trial. The editor's Dash view (topic 5) reads FM3 files too
- [ ] FM3 ports: drag tyres added by the port, in-game check pending (`current_issues.md` 2)
- [x] FM3 detected; FM3 carbins read byte-identical; FM3 vertex layout fully decoded (Supra vs FM4)
- [x] 3a Converter FM3 → FM4 (`CarbinModelMotorsport3.cs`): all 3930 FM3 carbins convert
- [x] 3b-3c FM3 as a port source; trial run passes on all 74 FM3-only cars
- [x] 3d FM3-only cars work in game (X5, Boxster):
  - 3d.1 textures too bright: FM3 marks colour textures linear, the port gives FM4's gamma header;
    the X5's dark patches are in FM3 too (FM3's own bake, not a port bug)
  - 3d.2 **cockpit camera at 0,0,0**: FM3 lacks `CarPartPositions` part 9 (steering wheel); the port
    adds it from the cockpit model; COCKPIT CAMERA panel in the model view (`CarCamera.cs`)
- [x] 3e Porsche make (FM4 has none): the port adds it (`CarPort.PortMake`), checked in game
- [x] Port step 4: database column diff for FM3-only cars (ids, wheels, FM4-only columns, torque curves)

## 10. Music ✅ done

- [x] Music and radio for FM2, FM3, FM4, FH1, FH2

## 11. Wheels: morph targets and resizing ⬜

Background and Jew's notes: `docs/todo_research.md`, C1.

- [ ] 11.1 Compare a rim that does not scale (BMW E30) with one that does (S60): `Morph_LOD0`, shader,
      `IsStock`, texture size
- [ ] 11.2 Write down the rule, then add morph targets to wheels that lack them (linked to the tyre
      width point of topic 1)

## 12. Cleanup, platform and a friendlier UI 🔧

Now (2026-10-17). Order: UI first (visible, low risk), the .NET change last (it touches everything).

- [ ] 12.5 Friendlier, simpler UI. List agreed with the user 2026-10-17; name back to **Forza Studio**
      (exe and repo stay ForzaEditor), the Pay Respects window stays as it is. Steps, each checked by
      the user:
  - [x] a. Dead items out: Tools > Hex View (crashed) and Save Modding (did nothing), the unused
        XboxTool form, "Chase, hood, bumper: not decoded yet"; name Forza Studio in title, dialogs,
        About, docs (checked by the user 2026-10-17)
  - [ ] b. Menus without duplicates: Model and Texture menus, File only for open/save; one Back; one
        set of export names
  - [x] c. Model view side panel as tabs PART / TEXTURE / CAMERA (Camera only for car models), labels
        sized to their text, buttons in even rows, Save to Game Database Now (checked by the user
        2026-10-17; asked first by the user, before b)
  - [ ] d. Plain wording (technical name in the tooltip), one spelling, port tool title covers FM3,
        database text language follows the UI; say clearly that Save to Database writes at once
  - [ ] e. Right-click-only actions (Rename Material, Extract/Replace/Revert, Go to / Used by) visible
- [ ] 12.3 Settings window (game folders, Xenia, DEV folder, viewer options)
- [ ] 12.4 Conversion tool as MDI (child windows inside the main window)
- [ ] 12.2 Recent .NET Framework (today `v2.0` with XNA, x86): newest one XNA allows; check SQLite, HexBox
- [x] 12.1 Console Modding and the `xdevkit.dll` reference removed

## 13. 3D viewer and game materials 🔧

- [ ] 13.1 Game materials: hardcoded in the executable or shader files somewhere
- [ ] 13.2 If the FM shaders are found: in-game look in the viewer, carbin viewer with real materials
- [x] 13.3 Viewer camera and cockpit camera editor checked by the user (2026-10-17)
- [ ] 13.4 User check of Go to Camera (cockpit + the 27 presets of `UICarTargetCameraPresets.xml`)

## 14. Other model formats ⬜

- [ ] 14.1 FH1/FH2 traffic models: open them and describe the format
- [ ] 14.2 FM2 models (today FM2 is export only)

## 15. Documentation and community 🔧

- [ ] 15.1 Format documentation in `docs/` (carbin, xds, Anark, gamedb, DLC...)
- [ ] 15.2 Discord feedback: reports become items in the right topic
- [x] 15.3 README update (FM3/FH1, port tool, console package)

## 16. Car cameras ✅ done (reduced)

Decision 2026-10-17: the only camera work needed is the FM3 cockpit fix, done in 9 3d.2. The other
cameras moved to Maybe later.

- [x] FM3 cockpit camera (9 3d.2) and the COCKPIT CAMERA panel

## Maybe later

- **Exterior cameras** (hood, bumper, chase, thumbnails). What is known (2026-10-17):
  - Per car: only `CameraOverrides` in the DB. Hood height/Z set on 501 of 503 cars (height ≈
    steering wheel y - 0.195, Z ≈ wheel z), pitch 0; chase/bumper offsets 0 on all cars but one.
  - Global, plain text in `Media\Camera`: `CameraSettings.ini` (FollowLow/FollowHigh height,
    radius, target, FOV, springs; HoodCam offsets and FOV 66; BumperHigh height and FOV 53;
    WheelCam; Driver default cockpit offset), `CameraPhysics.xml` (shake, tilt, FOV effects for
    Follow, Driver, Hood, BumperHigh; each layer can be turned off), `CameraPhysicsSansEffects.xml`.
  - Replay and menu cameras: `CarRelativeCams.xml` (138), `321Cams.xml`, `carrevealcams.xml`,
    `HomeSpaceCams.xml`, `ColorSelectCams.xml`, per track `preracecams.xml` / `tracksegcams.xml`.
  - Thumbnails: `Media\ThumbnailCamSettings.xml` (2 defaults + 24 per-car cameras).
  - A hood camera panel was written and dropped (not committed).
- **Engine sound listening tool** (from 9 3g): during a port, when the source game and FM4 have an
  engine sound bank of the same name, play both at a chosen RPM (slider, e.g. idle to redline) and on/off
  throttle, side by side, then pick which one goes into the car. Needs: the .fsb/.xma sample decoder
  (the music player has XMA), the engine's RPM-to-sample mapping from the bank or the harmonic tunings,
  and a crossfade between the samples around the chosen RPM. Today: FM4's bank when the names match
- Dash editor (topic 5): simpler edit panel (5.4; the user: "to simplify, fine for now")
- FM2 model writer (FM2 is out of scope for now)
- Automatic FH → FM4 material mapping instead of the manual fix (topic 4). @BlackBirdOsaka
  (Discord) is writing down the materials they replace: a starting point
- .gr2 animation editor
- Cars built from scratch (a full wizard, also as a DLC package)
- New renderer in place of XNA (a dream; almost impossible for now)
