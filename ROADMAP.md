# Forza Studio roadmap

Clean sheet, 2026-10-18. This one file now holds everything that used to be split across four:
the roadmap, `current_issues.md`, `docs/ideas_italian.md` (translated) and `docs/todo_research.md`.

How we work:

- **One item at a time.** The item under **Now** is the only one being worked on. It is closed before anything else starts.
- **An item is done** when it works (checked in game, if it touches the game), is committed, and is ticked here.
- Open items (`[ ]`) come first; what is done is one short line per topic. Details of finished work
  live in the commits, `docs/` and the code comments, not here.
- New ideas go to **C. Ideas** or **Maybe later**, never into the current item.
- **Game folders:** tests and installs go only to `FM4_Extracted_DEV`. `FM4_Extracted` is the clean copy and is never written.

Status: ✅ done · 🔧 in progress · ⏸ paused · ⬜ not started

Sections:

- **Now** — the single item being worked on
- **A. Topics** — the work, by area
- **B. Current issues** — problems seen in game, cause unknown or check pending
- **C. Ideas** — the user's requests list (from `ideas_italian.md`, translated)
- **D. Research notes** — background, quotes, what is known
- **Maybe later**

## Now

(empty — the user picks the next item)

---

# A. Topics

## 1. Car port: FH1 / FH2 → FM4 ✅ working, open points

Tools > Port Car From Another Forza… (`CarPort.cs`, `CarPortForm.cs`).

- [ ] Tyre width on ported cars whose wheel mesh is wider than usual (linked to 11)
- [ ] In-game check of a car installed with review fixes
- [ ] FH1/FH2 ports do not deform in a crash (issue B1)
- [ ] Big-endian vs little-endian gr2: find the differences before writing a converter
- Done: model/wheel/brake conversion, DB rows with reference closure, strings in 18 languages, digital
  dash, carattribs, shadersettings, LE gr2 animations, engine sound and banks from the source game, FM4
  tyre rule, PI mapping, port window with trial/review/install, `port_log.txt`. All 73 FH2 cars pass
  the trial; FH1 cars work in game (458 Spider, S65 AMG, R8 GT, R8 GT Spyder).

## 2. Ported cars as a DLC package ✅ done, open points

Credits: **Jew (@mr.abortion)** (`fm4dlc` toolchain) and **XTD (@xtd_ac)** (Xenia folder DLC, XLAST
package, console tests). Documentation: `docs/DLC_PACKAGES.md`. Material: `fm4dev\DEV_DLC`.

- [ ] 2.1 (was 3.6d) Several Forza Studio packs at once: each pack's string tables built on top of the
      other installed packs' (today only the highest priority pack shows its car names)
- [ ] 2.2 (was 3.8) Transfer to the console: FTP (Aurora/FSD) or Xbox 360 Neighborhood (the user's side)
- Done: merge DB from a port, Xenia package folder, DLC pack panel (n cars), STFS package for a real
  Xbox 360 (RGH/JTAG, `StfsPackage.cs`), checked in game and on the console.

## 3. Port from a car DLC (FH1 / FM3) → FM4 🔧 very important

(Was 3.9.) Port → FM4 from an FH1 or FM3 car DLC (FH2 on 360 has no DLC), STFS `CON`/`LIVE` or its
extracted folder: base game for shared data, the package's `<offer>000_merge.slt`, string tables,
car/wheel zips and `_pri_N` folders, audio on top. DLC cars appear in the car list and go into an FM4
pack. **Standalone**: only the DLC package + FM4, never the base source game.
Material: `fm4dev\Forza Horizon 1 DLC` (20 official packages); census `fm4dev\FH1DLC` (2026-10-17):
46 new FH1 DLC cars, 16 complete, 16 lose engine swaps, 14 part of the stock sound.

Decisions (user, 2026-10-17):

- [ ] Swap engine not in the pack nor in FM4: ask the user which swap to put instead (coded, step f)
- [ ] PI: fixed line per source game (FH1: FM4 = 1.189 PI - 0.363, error 0.0145) (coded, step e)
- [ ] FM3 DLC: no material yet
- [x] Incomplete engine sound: source game first, what it lacks from an FM4 car picked in the review
      (`soundfull:`); `NA.fsb` = "no sound" placeholder
- [x] Sound with the engine upgraded: FM3 engine tunings completed to the FM4 layout
      (`CarPort.CompleteEngineTuning`)

Steps, in order:

- [x] a. Traffic close-up work committed first (aedfdaf), so the two topics do not mix
- [x] b. DLC reader on the extracted folder: `DlcSource.cs` lays a package out as a source game folder
      in `%TEMP%\ForzaStudio\DlcSource\<package>` (merge DB as gamedb, car / wheel / string table /
      tuning zips, loose banks and thumbnails) (0a1e9fb)
- [x] c. Source game from the pack itself: the merge's ContentOffers.ID (FH1 4D5309C9)
- [x] d. CarPort standalone: only the package + FM4 (Escalade played in game, 2026-10-18)
- [ ] e. PI fixed line per source game (FH1 line coded): check the class / PI of DLC cars in game
- [ ] f. Swap engine: FM4's engine for an id the pack lacks, else the review asks
      (`List_UpgradeEngine.EngineID`, choices by MediaName). Coded; check in game a car whose swap
      is missing (e.g. SUB_BRZ_13), with an engine picked in the review
- [x] g. Port window: a DLC folder as source (cars listed once, damaged / rims-only packages in the
      report); Add → / ← Remove between the two lists (user's choice B), double-click adds
- [x] h. Trial on all FH1 DLC cars FM4 lacks: 48 of 48 pass (`fm4dev\FH1DLC\tools\dlcall.exe`, 45
      review items: swap engines, incomplete sound, materials); pack built and played (offer 93)
- [ ] i. In-game check by the user: one complete car (Escalade, done 2026-10-18), one without swaps,
      one with partial sound (e.g. LAM_AventadorJ_12)
- [ ] j. Open the STFS package directly (`CON`/`LIVE`), not only the extracted folder
- [ ] k. Rear-light materials in front parts: FH1 `brinr` in the Escalade's front bumper became
      `tail_light` and lit red under braking (fixed by hand with Rename Material); also
      MER_CLKGTR_HZN_98 `fogred` in the hood. Automatic rule for `brinl`/`brinr`/`fogred`/`taillight*`
      in `bumperF*`/`headlight*`/`hood*`: waits for the material to use (file from BlackBirdOsaka)
- [ ] l. Open DLC Package in the editor for small fixes (tree of the pack: cars, wheels, merge DB,
      strings; edit in place; rebuild the Xbox package). Same work as 14.1a g

## 4. Materials FM4 renders black 🔧

- [ ] 4.1 Material suggestions from the car shaders (`MaterialSuggestions.cs`, data from
      `fm4dev\Shaders\tools\similarity`): ★ recommended, common FM4, ~ unusual. Committed (039aa06); left:
  - [ ] check in the editor and in game (the "by name" rules and shader scores are untested in game;
        `lights_amber`, the port table's target for indicators, is not in FM4's in-race shader library)
  - [ ] instructions and documentation (`docs/MANUAL.md` sections 5 and 11)
- Done: port report lists them, model view marks them red, Rename Material… (checked by the user).

## 5. Digital dash (Anark) editor 🔧

- [ ] Slides (pages shown one at a time)
- [ ] Behaviour parameter names
- Done: .fbf/.bgf/.bsg parsed (431/431 byte-identical), FM3 dashes too (154/154); edit in place
  (5.1-5.3) works in game (text colour, 2026-10-17, 0f69352).

## 6. Game database editor ✅ working

- [ ] Decode `EventRestrictions.Value`
- [ ] See ideas C1-C3 (SQL editor, Id rule, checks before INSERT)
- Done: cars, tracks, events, wheels, tables views; creation tools; FM3/FM4/FH1/FH2 relation map,
  Go to / Used by, reference check.

## 7. Model editing (FM4) ✅ working, tests pending

- [ ] OBJ import into a wheel zip stops with "LOD 0 and LODs 1+ live in different files"
      (`CarbinModel.cs`): split the OBJ between the two carbins (@delete.diskord, D3)
- [ ] In-game test of a whole model rebuilt from OBJ, of edited parts and of Merge Into
- [ ] Effect of the cleared glass A/B arrays on rebuilt parts
- [ ] See idea C7 (one export/import folder layout)
- Done: Edit Part (move/rotate/scale, merge, delete), OBJ part import/export, OBJ work folder.

## 8. Automatic LODs ⬜

- [ ] Decimation (QEM) from lod0 to lod1-lod5 at the measured ratios (23.7 / 8.8 / 6.7 / 3.4 / 2.5 %)
- [ ] Keep UV seams, normals, materials and damage data; hook into the OBJ work folder import

## 9. Forza Motorsport 3 → FM4 🔧

- [ ] 9.1 (was 3i) No backfire flames on FM3 ports. Sound side identical (`BackfireLevels` from exhaust
      + cam upgrade); the flame is FM4 only (`Media\Effects\Backfire.xml`, `Flame.tga`,
      `FireDistortion.tga`). To find: where FM4 places the flame (exhaust point in the carbin, DB or
      fixed) and what FM3 cars lack. Compare an FM4 car with flames and the same car from FM3
- [ ] 9.2 Drag tyres added by the port: in-game check pending (issue B2)
- Done: FM3 carbin converter (3930/3930), FM3 as port source (74/74 trial), X5 and Boxster in game,
  Porsche make, FM3 digital dashes, engine banks rule (FM4's if same name), cockpit camera centred on
  the steering wheel.

## 10. Music ✅ done

- Done: music and radio for FM2, FM3, FM4, FH1, FH2.

## 11. Wheels: morph targets and resizing ⬜

Background: D3.

- [ ] 11.1 Compare a rim that does not scale (BMW E30) with one that does (S60): `Morph_LOD0`, shader,
      `IsStock`, texture size
- [ ] 11.2 Write down the rule, then add morph targets to wheels that lack them (linked to tyre width, 1)

## 12. Cleanup, platform and a friendlier UI ⏸

- [ ] 12.3 Settings window (game folders, Xenia, DEV folder, viewer options)
- [ ] 12.4 Conversion tool as MDI (child windows inside the main window)
- [ ] 12.2 Recent .NET Framework (today `v2.0` with XNA, x86): newest one XNA allows; check SQLite,
      HexBox (D1). Last, it touches everything
- Done: 12.1 Console Modding and `xdevkit.dll` removed; 12.5 friendlier UI, name Forza Studio
  (f141093, 9d08e0c).

## 13. 3D viewer and game materials 🔧

Background: D2.

- [ ] 13.1 Game materials: car shaders found (`Media\Shaders\Cars\*.fxobj`); document the format and the score
- [ ] 13.2 Imitate the FM shaders in the viewer from their parameters (carbin viewer with real materials)
- [ ] 13.4 User check of Go to Camera (cockpit + the 27 presets of `UICarTargetCameraPresets.xml`)
- Done: 13.3 viewer camera and cockpit camera editor (checked 2026-10-17).

## 14. Other model formats 🔧

- [ ] 14.1 FH1/FH2 traffic cars: **to check from scratch** (user, 2026-10-18). Ported cars load and
      their meshes show in game, but they are full of problems. Code committed (8549e97, 5e9690d,
      aedfdaf): close-up models (`_lod0`, `_cockpit`) rebuilt from LOD 1, wheel LOD0 and tyre header,
      cockpit driver rig, seat, camera part 9, hood, scaled donor brakes, collision, empty parts, wheel
      LODs, material table. Nothing of it counts as done until checked again.
  - [ ] List the problems seen in game (car, view, part, screenshot), one line each
  - [ ] Fix them one at a time, each checked in game
  - [ ] Traffic cars shown in yellow in the port list (placeholder close-up files)
  - [ ] Interior very dark; exterior FH material names still black (fixed by hand)
  - [ ] Same rebuild for FM1/FM2 cars, only after FH traffic works (see C4)
- [ ] 14.2 FM2 models (today FM2 is export only)

## 15. Documentation and community 🔧

- [ ] 15.1 Format documentation in `docs/` (carbin, xds, Anark, gamedb, DLC, shaders...)
- [ ] 15.2 Discord feedback: reports become items in the right topic
- Done: 15.3 README update.

## 16. Car cameras ✅ done (reduced)

- Done: FM3 cockpit camera and the COCKPIT CAMERA panel. Exterior cameras in Maybe later.

---

# B. Current issues

Problems seen in game, not explained or not checked yet. When the cause is found and fixed, the
issue moves to its topic and is removed from here.

## B1. FH1/FH2 ports do not deform in a crash (open, topic 1)

- Seen 2026-10-17 in DEV: FH1/FH2 ports keep their shape after a crash; FM3 ports deform like FM4
  cars. Deformable flag OK on the FH body parts.
- Still to note: which cars, and whether they were ported before or after 80f5104 (FM4 damage values
  in `carattribs.xml` for soft source cars).
- Per-vertex damage words, TOY_SupraRZ_98 in four games (`fm4dev\Deform\dmgcmp.exe`, outputs
  `fm3.txt`, `fh1.txt`, `fh2.txt`, `pairs_fh2.tsv`):
  - FM3 = FM4 on 93% of matched vertices.
  - FH1 = FH2, **a different format from FM4**: three signed fields 11:11:10, a short model-space
    vector (median length 43). FM4's word, same split, is a near-unit direction at full scale (median
    length 614); |FM4 y| follows |FH vector| (corr 0.81). Not decoded yet.
  - Likely cause: FM4 reads FH's short vectors as its own format.
- Next, one of: decode the FM4 word and convert FH words in CarPort; or take each vertex's word from
  the nearest vertex of the same car in FM4/FM3, else from a similar FM4 car.

## B2. FM3 ports: drag tyres (fixed 2026-10-16, in-game check pending, topic 9)

- FM3's `List_TireCompound` stops at 8; FM4 adds 9 (`9_DragTire`). `CarPort.DragTires()` adds FM4's
  Level 4 row (`TireCompoundID 9, Price 10000`) to every ported car without compound 9.
- X5 row inserted in DEV (backup `bin\Debug\bak\2026-10-16_drag_bak_DEV_gamedb.slt`).
- To close: check in game that the X5 offers the Drag compound.

## B3. The tyre looks the same with every compound (on hold)

- Not a port bug: FM4's own cars do the same in Xenia. The user checks on a real Xbox 360 later; if
  the tyre changes there, look at what picks `Media\wheels\TireA/B/C.zip` (`List_Wheels`,
  `WheelLayouts`, `Combo_TireBrandCompound`).

## B4. FM3 port fully upgraded: no engine sound (fixed, last check pending, topic 3)

- Cause: FM3's engine tuning is an older, shorter format (no `HarmonicTunings`, `FocusPEQ`,
  `Distortion`, `Compressor`, `DSPScalar`... and no `num_cylinders`), copied as is.
- Fixed in `CarPort.CompleteEngineTuning`; patched X5 confirmed in game 2026-10-17.
- To close: an in-game check of a car ported with the new code. Then remove this entry.

## B5. Strange ambient occlusion on some parts (probably closed)

- Seen 2026-10-16 on BMW_X5_09 from FM3: most likely FM3's own vertex bake (the user saw the same
  patches in FM3). Remove unless it shows up on another port; then note car, parts and a screenshot.

---

# C. Ideas

The user's request list (`docs/ideas_italian.md`, 2026-10-17), translated. Goal: make the editing tools
and automatic procedures more reliable, check asset compatibility across Forza generations, and
standardise model export/import. Each idea notes what is already known or done today.

| Id | Idea | Priority | Linked to |
|---|---|---|---|
| C1 | Better SQL editor and DB schema map | High | 6 |
| C2 | Fix the automatic Id generation | High | 1, 3, 6 |
| C3 | Check queries before every `INSERT` | High | 1, 3, 6 |
| C4 | Build test with traffic cars and FM1/FM2 assets | High | 14 |
| C5 | Find the driver position | Medium | 14, 16 |
| C6 | Missing sounds across generations | Medium | 3, 9 |
| C7 | One export/import folder layout | High | 7 |
| C8 | Change a mesh's material from a list | Medium | 3, 4 |

## C1. Better SQL editor and understanding of the DB schema

Goal: a more effective SQL editor and reliable knowledge of the tables involved.

- [ ] Examine the full schema of the tables involved
- [ ] Identify primary keys, foreign keys, indexes, constraints and relations
- [ ] Document the meaning of the fields used by import, export and build
- [ ] Check dependencies between tables and the right order of operations
- [ ] Make the SQL editor easier for writing, running and checking queries
- [ ] Reusable diagnostic queries to look at data, relations and linked records

Done when: the tables of the workflow are documented; required fields and constraints are clear;
diagnostic queries run without changing data.
Known today: gamedb has no `REFERENCES` constraints; the relations are in the 340-relation map
(`GameDbSchema.cs`) used by Go to / Used by.

## C2. Automatic Id assignment

Goal: no arbitrary Ids or collisions; new records created in a predictable way.

Rules:

- The first car created by the new workflow starts at **Id 4000**.
- Later Ids follow one deterministic, checkable rule.
- No Ids picked at random just because they seem to work.
- The RS6 Champion's Id works but does not follow the rule: not a model for new cars.

- [ ] Find every place where Ids are generated, assigned or copied
- [ ] Define a single Id allocation rule
- [ ] Check that the chosen Id is not already in the relevant tables
- [ ] Handle errors and duplicate insert attempts
- [ ] Check whether the Id must match across several tables or references
- [ ] Diagnostic log: assigned Id and why

Done when: the first new car uses an Id from 4000; Ids are unique by the DB's real constraints; no
hardcoded values scattered in the code.
Known today: CarPort keeps the source game's car Id; FM4 base Ids are 247-1473, FH1 DLC ports use
1519-1556. The RS6 Champion is AUD_1_RS6_03, **Id 421** (the original note says 412, which is
HON_NSXR_92 in FM4).

## C3. Check queries before `INSERT`

Goal: validate data and derived values before writing to the database.

- [ ] A check query before every automatic insert
- [ ] Check that the Id and any linked records exist
- [ ] Check that required fields have a value
- [ ] Compute and check `ordinal` before the insert; find the right rule from real data and the schema,
      without assuming `MAX(ordinal) + 1`
- [ ] Check for duplicates and invalid references
- [ ] Checks and inserts in a transaction where possible, rollback on error
- [ ] Diagnostic summary of the values about to be inserted

Done when: `ordinal` follows a documented rule; inserts do not run if the checks fail; errors name
the table and field.
Known today: in FM4 `Ordinal` = `Data_Car.Id`.

## C4. Build test with traffic cars and FM1/FM2 assets

Goal: check whether models from other categories or generations can be converted and built so they
work.

- [ ] One or more **traffic cars** as the first test case
- [ ] Or a car / asset from **Forza Motorsport 1/2**
- [ ] Temporarily copy `LOD1` into `LOD0` to see whether the build accepts a simplified structure
- [ ] Build the asset and check it in game or in the viewer
- [ ] Record errors, warnings and differences from a working native asset
- [ ] If it builds: check geometry, materials, UVs, hierarchy and in-game behaviour
- [ ] For compatible Ids, see whether the **cockpit camera** can be turned off
- [ ] Keep results apart by game generation and asset type

| Case | Change | Check |
|---|---|---|
| Traffic car | Temporary copy of `LOD1` into `LOD0` | Build and load |
| FM1/FM2 asset | Minimal structure change | Format compatibility |
| Id with cockpit camera | Turn the camera off, if supported | Load and in-game behaviour |

Done when: at least one reproducible test with log and identified source assets; it is documented
whether copying `LOD1` into `LOD0` is enough or what is missing; turning off the cockpit camera is
checked, not assumed.
Known today: traffic cars are built and load in game (topic 14.1: close-up models rebuilt from
LOD 1), but they are full of problems and must be checked from scratch. FM1/FM2 still open (FM1 `.cab` reader in `fm4dev\Fm1_CAB`, FM2 export only).

## C5. Find the driver position

Goal: where the driver position is stored and how it is read when the car loads.

- [ ] Compare the files of a working car with one whose position is known or correct
- [ ] Look at hierarchies, nodes, transforms and references in the model files
- [ ] Is it a dedicated node, a transform matrix, a link to the cockpit camera, or data outside the geometry?
- [ ] Check position, rotation and coordinate system separately
- [ ] A controlled change, checked in the viewer or in game
- [ ] Document the file, structure and fields responsible

Done when: the driver position maps to a specific field or transform; the change is checked with a
reproducible test.
Known today: the cockpit driver rig is in the `_cockpit.carbin` head (0x1C8-0x2FF: hand grips, wheel
pivot and axis, shifter...), hands in `DriverHandsOffset`, cockpit camera = `CarPartPositions` part 9
+ `CameraOverrides` (topic 14.1, 16). The seated body position itself is not located yet.

## C6. Missing sounds across game generations

Goal: find why sounds go missing when cars or data move between Forza generations.

- [ ] Compare a car with working sound and one without
- [ ] List the audio references in both
- [ ] Check differences in naming, Ids, events, banks or dependencies
- [ ] Check that the needed audio assets exist in the target game
- [ ] Tell apart: missing reference, missing asset, incompatible format, incompatible audio system
- [ ] A mapping table between the generations
- [ ] Never copy audio references without checking they exist and fit

Done when: the cause is classified for at least one real case; a procedure to find and fix invalid
references is written down.
Known today: one case classified and fixed (B4: FM3 engine tuning format). Banks: source game first,
FM4 car picked in the review for what is missing, `NA.fsb` = no sound.

## C7. One export/import folder layout

Goal: the same exported folder structure for **every supported asset type**, to make editing,
comparing and re-importing simpler.

The `car.carbin` folder holds the model structure, with `LOD0` and `body` as folders and every
relevant object inside:

```text
<export_folder>/
└── car.carbin/
    └── LOD0/
        └── body/
            ├── <object_1>
            ├── <object_2>
            └── ...
```

(The original note ends here.)

## C8. Change a mesh's material from a list

In a `.carbin` the mesh name in the model tree is its material (the shader FM4 draws it with), so
fixing a part means renaming it (Escalade front bumper, 2026-10-18). Instead of typing the name, pick
it from a list of FM4 materials (★ recommended / common, from `MaterialSuggestions`), one click.

---

# D. Research notes

Background of topics 11-15: quotes, file names, what is known so far.

## D1. Cleanup and platform (topic 12)

- **.NET:** today `v2.0` with XNA, x86. The renderer stays XNA, so the target is the newest .NET XNA
  allows. To find out what else breaks (SQLite, HexBox). `xdevkit.dll` no longer blocks it (12.1).

## D2. 3D viewer and materials (topic 13, 4)

- **Renderer:** a new one is almost impossible, the viewer stays on XNA (Maybe later).
- **Car shaders found** (2026-10-17, details in `fm4dev\Shaders\FINDINGS.md`):
  `Media\Shaders\Cars\*.fxobj`, compiled Xbox 360 effects. 5 big libraries (`shaders_v16`, `_16car`,
  `_slod`, `_splitscreen`, `_ui`) + ~148 single-material files listed in `CustomCarShaders.xml`. A
  carbin material name is a technique name (`cockpit_` prefix in the cockpit). An unknown name falls
  back to the `missing` technique, which is why FH lights render black in FM4.
  - FM4 library: 196 techniques, FH1: 254. Single files are the same set in both games. FH1-only (57):
    one technique per light type (fullbeam, hidhead, xenonhead, drl*, indicator_*, fog*,
    lights_gls_*...), numberplate/numplate, traffic body/window, shadow masks. FH1 lights take their
    colour from per-car `shadersettings.xml` (`FullbeamColor`, `DRLWhiteColor`...); FM4 has only
    generic head_light/tail_light/reverse_light/lights_*.
  - FH1 shaders need engine inputs FM4 lacks (dynamic lights, cascaded shadows, two-tone paint):
    copying them into FM4 is not realistic. The viewer can only imitate them from the parameters.
  - **Similarity** (2026-10-18): technique -> vertex/pixel shader decoded for all four games; every
    FH1/FH2/FM3 material scored against FM4's (shared constants and size, 0-100). FH lights closest to
    FM4 `reflector` / `tail_light` (70-75), FM3 same-name materials 59-89. Used by 4.1.
- **Viewer camera:** the game's FOVs are vertical degrees, in `Media\Camera\CameraSettings.ini` (race
  cameras) and `Media\UICarTargetCameraPresets.xml` (garage, photo mode).

## D3. Wheels and morph targets (topic 11, 7)

- **Wheel morph.** @Jew: *"it's dependant on the wheel having a morph target in its package"*. Looks
  like a flag travelling with the shader and `IsStock`; a larger texture is mentioned too.
  - BMW E30 rim does not scale, S60 does. @Jew: *"You need morph targets in the wheel model for Forza
    to scale them to size"*. Name to look for: `Morph_LOD0`. Linked to tyre width (topic 1).
- **OBJ wheel import** (@delete.diskord): *"i used forza studio to export an fm3 wheel to obj, then
  opened an fm4 wheel zip in forzaeditor and attempted to replace the wheel with the obj i exported,
  resulting in the 'LOD 0 and LODs 1+ live in different files' error. however i did try the fm3
  conversion (port car from another forza) in the new commit a little while ago and the wheel zip it
  output seems to work perfectly in fm4."* An FM4 wheel keeps LOD0 and LOD1+ in two carbins: split
  the OBJ instead of stopping (topic 7).

## D4. Other models (topic 14)

- FH1/FH2 traffic models: close-up files are placeholders (17-vertex parts), rebuilt by the port;
  the result loads in game but has many problems (14.1).
- FM2 models: out of scope today, export only. FM1: `.cab`/`.ca2` reader verified on 243 archives.

## D5. Documentation and community (topic 15)

- Format documentation in `docs/` (carbin, xds, Anark, gamedb, DLC, shaders...).
- Discord feedback: still few users; reports go into topics, like D3.

---

## Maybe later

- **Exterior cameras** (hood, bumper, chase, thumbnails). Known (2026-10-17):
  - Per car: only `CameraOverrides` in the DB. Hood height/Z set on 501 of 503 cars (height ≈
    steering wheel y - 0.195, Z ≈ wheel z), pitch 0; chase/bumper offsets 0 on all cars but one.
  - Global, plain text in `Media\Camera`: `CameraSettings.ini` (Follow, HoodCam, BumperHigh, WheelCam,
    Driver default offset), `CameraPhysics.xml` (shake, tilt, FOV effects), `CameraPhysicsSansEffects.xml`.
  - Replay and menu cameras: `CarRelativeCams.xml` (138), `321Cams.xml`, `carrevealcams.xml`,
    `HomeSpaceCams.xml`, `ColorSelectCams.xml`, per track `preracecams.xml` / `tracksegcams.xml`.
  - Thumbnails: `Media\ThumbnailCamSettings.xml` (2 defaults + 24 per-car cameras).
  - A hood camera panel was written and dropped (not committed).
- **Engine sound listening tool**: when the source game and FM4 have an engine bank of the same name,
  play both at a chosen RPM and on/off throttle, then pick one. Needs the .fsb/.xma decoder (the music
  player has XMA), the RPM-to-sample mapping, and a crossfade. Today: FM4's bank when names match.
- Dash editor: simpler edit panel (5.4; the user: "to simplify, fine for now")
- FM2 model writer (FM2 out of scope for now)
- Automatic FH → FM4 material mapping instead of the manual fix (4.1 gives suggestions, the choice stays
  the user's). @BlackBirdOsaka (Discord) is writing down the materials they replace: a starting point
- .gr2 animation editor
- Cars built from scratch (a full wizard, also as a DLC package)
- New renderer in place of XNA (a dream; almost impossible for now)
