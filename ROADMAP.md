# Forza Studio roadmap

Renumbered from scratch on 2026-10-22 (several topics were running in parallel and the old numbers
had become impossible to follow). One file holds everything: work, issues, research.
**Old numbers are in the table at the very end** (section E), for commits and chats that quote them.

How we work:

- **Numbers are permanent from now on.** Topic `N`, point `N.m`, sub-point `N.m.x`. A finished point keeps its
  number and only moves to section D.
- **A point is done** when it works (checked in game, if it touches the game), is committed, and is ticked.
- **Open work is only in section A.** Everything 100% finished is listed **only at the bottom (section D)**,
  never mixed with open work. Details of finished work live in commits, `docs/` and code comments.
- New ideas go to the right topic as ⬜ (or 💤 if "later"), never into work in progress.
- **Game folders:** tests and installs go only to `FM4_Extracted_DEV`. `FM4_Extracted` is the clean copy, never written.
- Tests live in `E:\DEV\fm4dev\<topic>`.

Status: 🔧 in progress · ⏸ paused · ⬜ not started · 💤 maybe later · ✅ done (section D only)

Sections: **0** Dashboard · **A** Open work · **B** Current issues · **C** Research notes ·
**D** Finished tasks (bottom) · **E** Old → new numbers

---

# 0. Dashboard

## 0.1 In progress together (2026-10-22)

| Where | What | State |
|---|---|---|
| 1.1 + 1.2 | Id rule above `GameIdLimits.json`, ModelId = Id | committed (638a573); in-game check pending |
| 1.4 | SQL console (Tools > Database SQL) | committed, tested on a DB copy |
| 2.2 + 2.3 | Duplicate car (game folder) and its wizard | committed, copy tested; not checked in game |
| 5.1 | FM2 game folder as port source | committed (638a573); in-game check pending |
| 7.1 | String tables on all installed pack layers | coded, in-game check pending |
| 8.1 | New car makes | tested on files, in-game check pending |
| 13.1 | Add a song (FM4) | coded, not tested in game |
| 3.3 | Generic brakes for missing / placeholder calipers | committed (a06a7a7: rotor types, game material kept, atlas patch dropped); in-game check pending |

Every row above is code that is in git and waits for a check in game. Nothing is half-written.

## 0.2 Not committed (end of day 2026-10-22, all work closed by the user)

- Code: nothing. `git status` is clean except this file. 638a573 and a06a7a7 hold all the code of the day (FM2 source, makes, settings,
  thumbnails, Id / duplicate / SQL, generic brakes); 4.9, 6.12 and 15.3 went in with 638a573.
- `BrakeAtlas.cs` no longer exists (the atlas patch was dropped in a06a7a7).

## 0.2b Next session: what to check first (all need the game, no new code)

1. Install one car with the new Id rule in `FM4_Extracted_DEV` and look at it in game (1.1.2, 1.2.1, 5.1.1, 3.3)
2. Pack checks: two packs together (7.1), a pack with a new make (8.1)
3. Smaller: Add song (13.1), duplicate car in game (2.2.3)

## 0.3 Waiting for something outside the code

- 7.2 console FTP (the user sold the Xbox 360; B3 is on hold with it)
- 3.4 FM4 DVD2 (only DVD1 found)
- 9.x materials: separate WIP project (shader decode, in-game-like preview)

---

# A. Open work

## 1. Ids, database checks and SQL 🔧

Facts (2026-10-20/22): car Ids are one list across the games (same Id = same car in all 344 shared cases). Max Ids FM4 / FH1 / FH2:
Data_Car 1473 / 1568 / 51558, List_Wheels 11066 / 11153 / 5011150, Events 726 / 246 / 911, Races 3675 / 182 / 716,
EventParticipants 72470 / 1337 / 4754. Engine / drivetrain / wheel Ids copied from the source stay as they are.
Decisions (user): a ported car **always** gets a new Id (not the source's); rows Forza Studio generates come from a
reserved range per table; FM4 packs never collide. The 2026-10-21 "review totally, not stable" reopening was answered by the
limits table (`docs/GameIdLimits.json`, `tools/IdLimits`).

- [ ] 1.1 Id allocator (`DbIds.cs`, `GameIdLimits.cs`, embedded `docs/GameIdLimits.json`; ex Now "C2+C3" and 18.1; committed 638a573).
      Rule: car Id = first free above the last FM4 DLC car (1638) and free in the game DB **and** in every installed
      Forza Studio pack's merge DB; reinstall of the same MediaName keeps its Id; other tables above the maxima of the
      limits file; formula Ids (car × 1000 + n, 100 000 × k + car) unchanged; every Id written to the port log with the
      reason. `Port.ini` `InstallFirstCar > 0` = dev/test override only. Parser tested on the real file (FM4 List_Wheels 11209).
  - [ ] 1.1.2 In game: a car with Id above the new base in showroom, race, upgrades; a 2-car pack
  - [ ] 1.1.3 Editor check of the port window with the new rule
- [ ] 1.2 Car Id = ModelId. FM4 has `ModelId = Id` on all 503 cars; the renumbering missed it (X5 as 4001 did not show,
      ModelId still 1079). Fixed in `RenumberCar`, `CheckCar` refuses a mismatch. Scan of every column of the trial DB:
      no value left on the source Id.
  - [ ] 1.2.1 Reinstall the Aveo IE and the X5 with the current rule and **test in game** (the Aveo IE also carries FM3's
        unicorn flag and IsArcade 0: source data, possibly the real reason it did not show)
- [ ] 1.3 Checks before every insert (ex C3). Coded: `DbIds.CheckCar` (one Data_Car row, Data_CarBody row, no key twice,
      no key built from the source Id; the port stops and writes nothing on error, errors name table and column; test
      `fm4dev\DbIds\tools\c3test.exe`, 4/4). Left: confirm the rest of the old list against the code:
  - [ ] 1.3.1 `ordinal` rule documented (FM4: `Ordinal` = `Data_Car.Id`; do not assume MAX + 1)
  - [ ] 1.3.2 Writes in a transaction with rollback on error (the port already works on an in-memory DB)
  - [ ] 1.3.3 Diagnostic summary of the values about to be inserted
  - Done when: inserts do not run if the checks fail; errors name table and field.
- [ ] 1.4 SQL for every table (ex 18.5 + C1). Coded and committed: `SqlGuard.cs`, `SqlConsoleForm.cs`, `GameDatabase.RunSql`, Tools >
      Database SQL: one statement, SELECT / INSERT / UPDATE / DELETE on any table, nothing else (DROP, ALTER... refused); an
      INSERT / UPDATE runs on a copy first and is refused when it creates a key ≤ FM4's largest (message with the next free
      one); writes are listed with the DB changes, File > Save writes them. Tested on a DB copy (Data_Car Id 5 refused, 1700
      accepted, DROP / two statements refused). Left:
  - [ ] 1.4.2 The full 1.3 checks on SQL writes (today only the key rule)
  - [ ] 1.4.3 Schema documented: primary keys, relations (no `REFERENCES` in the DB; the 340-relation map in `GameDbSchema.cs`
        feeds Go to / Used by), indexes, meaning of the fields import / export / build use
  - [ ] 1.4.4 Dependencies between tables and the right order of operations
  - [ ] 1.4.5 Editor easier for writing, running and checking queries
  - [ ] 1.4.6 Reusable diagnostic queries (data, relations, linked records) that change nothing
- [ ] 1.5 Decode `EventRestrictions.Value`

## 2. Duplicate car, wizard, cars from scratch 🔧 (user 2026-10-22)

All with every FM4 DLC present (Ids free in the game AND in all packs). Code committed in 638a573.

- [ ] 2.2 Duplicate car in the game folder (`CarDuplicate.cs`, `GameStringKeys.cs` + `docs/GameStringKeys.json`: every .str key of FM4 + all
      packs): tested on a copy (`E:\DEV\fm4dev\Duplicate`: NSX '97 → ACU_NSX_97_B, id 1638, 95 rows in 28 tables, new string keys 65534 / 65533
      in 18 languages, 36-file zip renamed, `Thumbnail_1638.xds`, manifest line, original untouched).
  - [ ] 2.2.1 Copy into a **DLC pack** (only the game folder today)
  - [ ] 2.2.2 `Combo_Engines` rows that point at the car are not copied
  - [ ] 2.2.3 Check in game
- [ ] 2.3 Wizard (`DuplicateCarForm.cs`, Tools > Duplicate Car): car + name, names / year / make, Data_Car numbers, summary with the plan,
      Create. Rendered and the error path seen; not checked in game.
  - [ ] 2.3.2 Own engine from scratch (e.g. Bora 1.9 TDI from the V6)
  - [ ] 2.3.3 Colours / textures page
  - [ ] 2.3.4 DLC pack target (with 2.2.1)
- [ ] 2.4 💤 Cars built from scratch (a full wizard, also as a DLC package). The conversions are the road to it: automatic reusable
      rules, source first, else computed (user 2026-10-21)

## 3. Car port: FH1 / FH2 → FM4 🔧

Tools > Import Car From Another Forza… (`CarPort.cs`, `CarPortForm.cs`). All 73 FH2 cars pass the trial; FH1 cars work in game.

- [ ] 3.1 FH1 / FH2 ports do not deform in a crash (issue B1 below; maybe tied to 10.1)
- [ ] 3.2 Big-endian vs little-endian gr2: find the differences before writing a converter
- [ ] 3.3 Generic brakes: a missing / placeholder caliper + rotor becomes the generic `Media\brakes` model (`CarPort.GenericBrake`; committed
      a06a7a7: rotor types slotted / carbon / plain / drilled, the plain and drilled ones exist only as LOD models so their vertices go to LOD0;
      the generic file's own material and UV transform are kept, car textures untouched: the white discs on traffic cars are gone). Check in game
- [ ] 3.4 💤 FM4 DVD2 (user 2026-10-20: files come only from the folder opened as source). Our folder is DVD1 only (353 banks in
      `soundbanks\lod1`); swap engines whose sound is on DVD2 crash the race with an I/O error (BMW M3 GTR V8 `8N1_E92M3_Exh.fsb`;
      M5 / M6 V10 are fine). When DVD2 is found: read it as a second FM4 source; until then maybe hide or flag swap engines with a missing
      bank (ET → HT → `<Soundbank>` check, as `CarPort.ImportHarmonic`).
- [ ] 3.5 Missing sounds across generations (ex C6). One case classified and fixed (FM3 engine tuning format); banks: source game first,
      FM4 car picked in the review for what is missing, `NA.fsb` = no sound. Left:
  - [ ] 3.5.1 Classify more real cases (missing reference / asset / format / audio system)
  - [ ] 3.5.2 Mapping table between the generations; written procedure to find invalid references
  - [ ] 3.5.3 💤 Engine sound listening tool: same-name banks of source and FM4 played at chosen RPM / throttle, then pick one (needs the
        .fsb / .xma decoder, RPM-to-sample mapping, crossfade)
- Tyre width of ported cars: see 10.4.

## 5. FM2 / FM1 → FM4 🔧

2026-10-21 product decision: an FM2 game folder is a normal port source. FM2 reuses the FM1 mesh; FM1 is a little-endian chunk tree,
FM2 big-endian in the FM4 family (docs + tools `fm4dev\Fm1_Fm2`, `fm4dev\Fm1_CAB`).

- [ ] 5.1 FM2 as port source (`Fm2Source.cs` stages the folder, port window lists 310 FM2 cars, thumbnail ported; Veilside = DEV id 4008;
      committed 638a573)
  - [ ] 5.1.1 In-game check of an FM2 port with the new Id rule (1.1)
- [ ] 5.2 FM2 car data: `Fm2CarData.cs` maps FM2 Data_Car / engine / body (Supra RZ compared FM2 / FM3 / FM4) but is not wired in yet
- [ ] 5.3 Same close-up rebuild as the FH traffic cars for FM1 / FM2 cars (ex 14.1 last item, ex C4): only after FH traffic works (6.1);
      a LOD 1 → LOD 0 copy is the first build test
  - Done when: one reproducible test with log and identified source assets, documented whether LOD1 → LOD0 is enough or what is missing,
    cockpit camera off checked, not assumed. Keep results apart by game generation and asset type.
- [ ] 5.4 ⏸ FM1 `.cab` / `.ca2` mesh exporter (`fm4dev\Fm1_CAB`, f4852a6, CAB 243/243, carbin 503/503): paused, order is traffic cars, FM2, then FM1
- [ ] 5.5 💤 FM2 model writer (superseded as a product by 5.1; only if a case needs it)

## 6. FH1 / FH2 traffic cars 🔧

Ported traffic cars load and run (see D, topic 6). What stays open:

- [ ] 6.1 Exterior FH material names still black, fixed by hand (Rename Material): parked under 9 (shader research, an FM4 "mini engine" in the viewer)
- [ ] 6.2 Driver position (ex C5): where the seated body position is stored (known: rig in `_cockpit.carbin` head 0x1C8-0x2FF,
      `DriverHandsOffset`, cockpit camera = `CarPartPositions` part 9 + `CameraOverrides`). Compare a working car with a known-correct one;
      position, rotation and coordinate system separately; a controlled change checked in game; document the field. Done when the position
      maps to a specific field with a reproducible test.

## 7. DLC packages 🔧

Credits: **[Aiden-Piercee](https://github.com/Aiden-Piercee)** (`fm4dlc` toolchain) and **XTD (@xtd_ac)** (Xenia folder DLC, XLAST package,
console tests). Docs: `docs/DLC_PACKAGES.md`. Material: `fm4dev\DEV_DLC`.

- [ ] 7.1 Several Forza Studio packs at once. CODED 2026-10-22: a pack's string tables = the game's + EVERY installed FM4 pack layer (STFS or
      folder, `DlcPackage.StringLayer`) + its own strings; priority above the highest layer; key clashes with an installed pack reported as
      WARNING (merged EN Data_Car 1006 → 1402 keys). In-game check pending. Follows the string audit rules in D, 2.1.
- [ ] 7.2 ⏸ Transfer to the console: FTP (Aurora / FSD) or Xbox 360 Neighborhood (the user's side; no console now)

## 8. New car makes 🔧

A make FM4 lacks (or a brand-new one), local or in a DLC pack. Code: `CarMake.cs`, `NewMakeForm.cs`, `CarPortForm.Makes.cs`.

- [ ] 8.1 In-game check (local and pack): the make's page, logo in the list, Livery editor with no decals
- [ ] 8.2 Open: own decals (`Livery_DecalsSortOrder`), a country not in `List_Country`, a logo with its own look for the `_wtbg` folder,
      edit / remove a created make, per-language names in the dialog (uninstalling the last car of a make already removes the make)

## 9. Materials and game shaders ⏸

Paused (user 2026-10-19): the shader similarity analysis is looked at again with a separate WIP materials project (simple decode of the
game shaders, in-game-like preview; notes `docs/internal_traffic_conversion.md`).

- [ ] 9.1 Material suggestions (`MaterialSuggestions.cs`, committed 039aa06): left
  - [ ] 9.1.1 Check in the editor and in game ("by name" rules and shader scores untested; `lights_amber`, the port table's target for
        indicators, is not in FM4's in-race shader library)
  - [ ] 9.1.2 Instructions and documentation (`docs/MANUAL.md` sections 5 and 11)
- [ ] 9.2 Document the game materials: the shader format and the similarity score (car shaders in `Media\Shaders\Cars\*.fxobj`)
- [ ] 9.3 Imitate the FM shaders in the viewer from their parameters (carbin viewer with real materials)
- [ ] 9.4 💤 Automatic FH → FM4 material mapping instead of the manual fix (9.1 only suggests; the choice stays the user's).
      @BlackBirdOsaka (Discord) is writing down the materials they replace: a starting point
- [ ] 9.5 💤 New renderer in place of XNA (a dream; almost impossible)

## 10. Wheels: morph targets and resizing ⏸

Background C3 (research). Paused by the user 2026-10-19.

- [ ] 10.1 Morph table on OBJ import onto a wheel: regenerate the table instead of keeping a stale one (stale = exploded wheel in game).
      To resume by comparing how Forza Studio and Aiden's script (`fm4dev\Aiden-Piercee\carbin_check.py`, read only) process the carbin binaries.
  - [ ] 10.1.1 Same check for every older source game: FM3 done; FM2 and FM1 when their ports come (5.1, 5.4). FH1 / FH2 keep their own
        tables: only if a case shows up
  - [ ] 10.1.2 Traffic cars' LOD0 wheel rebuilt from LOD 1 copies the table from the nearest vertex: not a problem so far; watch if a traffic
        rim looks small up close
- [ ] 10.4 Tyre width on ported cars whose wheel mesh is wider than usual (ex topic 1 + 11.6): check in game whether D 10.5 / 10.6 already fixed it;
      maybe tied to 10.1 and to 3.1

## 11. Model editing and automatic LODs 🔧

- [ ] 11.1 OBJ import into a wheel zip (@delete.diskord): fixed in code (60f7d57, `CarbinModel.RebuildLods`: cars keep LOD 0 in `_lod0`, wheels keep
      LODs 0-5 in one file); in-game test pending (FM3 wheel OBJ onto an FM4 wheel zip)
- [ ] 11.2 In-game test of a whole model rebuilt from OBJ, of edited parts and of Merge Into
- [ ] 11.3 Effect of the cleared glass A/B arrays on rebuilt parts
- [ ] 11.4 One export / import folder layout for every asset type (ex C7): `<export_folder>/car.carbin/LOD0/body/<object_n>` (the original note ends there)
- [ ] 11.5 ⬜ Automatic LODs: decimation (QEM) from lod0 to lod1-lod5 at the measured ratios (23.7 / 8.8 / 6.7 / 3.4 / 2.5 %)
  - [ ] 11.5.1 Keep UV seams, normals, materials and damage data
  - [ ] 11.5.2 Hook into the OBJ work folder import
- [ ] 11.6 💤 .gr2 animation editor

## 12. Digital dash (Anark) editor 🔧

- [ ] 12.1 Slides (pages shown one at a time)
- [ ] 12.2 Behaviour parameter names
- [ ] 12.3 💤 Simpler edit panel (user: "to simplify, fine for now")

## 13. Music 🔧

- [ ] 13.1 Add a song (FM4), coded 2026-10-21, **not tested in game**: ♪ > "Add song (FM4)..." (`MusicInstaller.cs`, `ArchiveEditorForm.SongAdd.cs`;
      ideas from the Fm4 Sound SDK). Any audio → vgmstream WAV → `xma2encode` (Xbox 360 SDK, copied to `bin\xma2encode\`, NOT redistributed) →
      `Media\audio\Music\<name>.xma` + `<Song>` line (playlist kept once as `.orig`). Tried: Digitalism - Pogo in FM4_Extracted_DEV (race layer 1);
      the user says Forza Studio parses the new .xma like a game file. To test: plays in Xenia in race; menu playlists; the dialog; a second song /
      existing name; mono or 48 kHz input; many songs; loading without the SDK (path prompt). Release note: the user needs the SDK for this feature.

## 14. Cameras 💤

- [ ] 14.1 💤 Exterior cameras (hood, bumper, chase, thumbnails). Known: per car only `CameraOverrides` in the DB (hood height / Z on 501 of 503
      cars, chase / bumper offsets 0 on all but one); global plain text in `Media\Camera` (`CameraSettings.ini`, `CameraPhysics.xml`,
      `CameraPhysicsSansEffects.xml`); replay / menu cameras `CarRelativeCams.xml` (138), `321Cams.xml`, `carrevealcams.xml`, `HomeSpaceCams.xml`,
      `ColorSelectCams.xml`, per track `preracecams.xml` / `tracksegcams.xml`; thumbnails `Media\ThumbnailCamSettings.xml` (2 defaults + 24 per-car).
      A hood camera panel was written and dropped.

## 15. Platform and UI ⏸

- [ ] 15.1 Conversion tool as MDI (child windows inside the main window)
- [ ] 15.2 Recent .NET Framework (today `v2.0` with XNA, x86): newest one XNA allows; check SQLite, HexBox. Last, it touches everything

## 16. Documentation and community 🔧

- [ ] 16.1 Format documentation in `docs/` (carbin, xds, Anark, gamedb, DLC, shaders...)
- [ ] 16.2 Discord feedback: reports become points in the right topic (still few users)

## 17. Side projects (own tools, not Forza Studio features) ⏸

Each has its own folder and notes file.

- [ ] 17.1 PGR4 (`C:\Users\Dan\Desktop\PGR4DEV`, `FORMATO_PGR4.md`): textures + geometry solved 123/123 (2026-10-20); next materials → textures, scale
- [ ] 17.2 NFS ProStreet tracks (`nfs_track_dev`, `NOTES.md`): chunk format verified, solid list decoding next
- [ ] 17.3 FM4 tracks study (`fm4dev\Tracks`): MapleValley smallest circuit; rmb.bin pieces 981/981; next placement + OBJ export
- [ ] 17.4 GT5 / GT6 (`gt_dev`): decoder / viewer largely closed; car data JSON export committed (ea8ccad); Forza Studio import left

---

# B. Current issues

Problems seen in game, cause unknown or check pending. When fixed they move to their topic.

## B1. FH1 / FH2 ports do not deform in a crash (topic 3.1)

- Seen 2026-10-17 in DEV: FH1 / FH2 ports keep their shape after a crash; FM3 ports deform like FM4. Deformable flag OK on the FH body parts.
  User's guess: maybe the morph data (10.1). To note: which cars, ported before or after 80f5104 (FM4 damage values in `carattribs.xml` for soft source cars).
- Per-vertex damage words, TOY_SupraRZ_98 in four games (`fm4dev\Deform\dmgcmp.exe`, outputs `fm3.txt`, `fh1.txt`, `fh2.txt`, `pairs_fh2.tsv`):
  FM3 = FM4 on 93% of matched vertices. FH1 = FH2, **a different format from FM4**: three signed fields 11:11:10, a short model-space vector
  (median length 43); FM4's word is a near-unit direction at full scale (median 614); |FM4 y| follows |FH vector| (corr 0.81). Not decoded.
  Likely cause: FM4 reads FH's short vectors as its own format.
- Next, one of: decode the FM4 word and convert FH words in CarPort; or take each vertex's word from the nearest vertex of the same car in
  FM4 / FM3, else from a similar FM4 car.

## B3. The tyre looks the same with every compound (on hold, uncertain)

- 2026-10-19: no real hardware any more (the Xbox 360 was sold). Not a port bug: FM4's own cars do the same in Xenia. If the tyre changes on a
  real console, look at what picks `Media\wheels\TireA/B/C.zip` (`List_Wheels`, `WheelLayouts`, `Combo_TireBrandCompound`).

---

# C. Research notes

## C1. Platform (topic 15)

- **.NET:** today `v2.0` with XNA, x86. The renderer stays XNA, so the target is the newest .NET XNA allows. To find out what else breaks
  (SQLite, HexBox). `xdevkit.dll` no longer blocks it.

## C2. Viewer and materials (topic 9)

- **Renderer:** a new one is almost impossible, the viewer stays on XNA.
- **Car shaders** (2026-10-17, `fm4dev\Shaders\FINDINGS.md`): `Media\Shaders\Cars\*.fxobj`, compiled Xbox 360 effects. 5 big libraries
  (`shaders_v16`, `_16car`, `_slod`, `_splitscreen`, `_ui`) + ~148 single-material files in `CustomCarShaders.xml`. A carbin material name is a
  technique name (`cockpit_` prefix in the cockpit). An unknown name falls back to `missing`, which is why FH lights render black in FM4.
  - FM4 library: 196 techniques, FH1: 254. FH1-only (57): one technique per light type (fullbeam, hidhead, xenonhead, drl*, indicator_*, fog*,
    lights_gls_*...), numberplate / numplate, traffic body / window, shadow masks. FH1 lights take colour from per-car `shadersettings.xml`;
    FM4 has only generic head_light / tail_light / reverse_light / lights_*.
  - FH1 shaders need engine inputs FM4 lacks (dynamic lights, cascaded shadows, two-tone paint): copying them is not realistic; the viewer can
    only imitate them from the parameters.
  - **Similarity** (2026-10-18): technique → vertex / pixel shader decoded for all four games; every FH1 / FH2 / FM3 material scored against FM4's
    (0-100). FH lights closest to `reflector` / `tail_light` (70-75), FM3 same-name materials 59-89. Used by 9.1.
- **Viewer camera:** the game's FOVs are vertical degrees, in `Media\Camera\CameraSettings.ini` and `Media\UICarTargetCameraPresets.xml`.

## C3. Wheels and morph targets (topics 10, 11)

- **Wheel morph.** [Aiden-Piercee](https://github.com/Aiden-Piercee): *"it's dependant on the wheel having a morph target in its package"*.
  BMW E30 rim does not scale, S60 does. @Jew: *"You need morph targets in the wheel model for Forza to scale them to size"*. Solved 2026-10-18:
  the morph target is the 16-byte per-vertex table of the wheel (rule in D, 10.6).
- **OBJ wheel import** (@delete.diskord): *"i used forza studio to export an fm3 wheel to obj, then opened an fm4 wheel zip in forzaeditor and
  attempted to replace the wheel with the obj i exported, resulting in the 'LOD 0 and LODs 1+ live in different files' error. however i did try the
  fm3 conversion (port car from another forza) in the new commit a little while ago and the wheel zip it output seems to work perfectly in fm4."*
  An FM4 wheel keeps LODs 0-5 in one carbin (cars keep LOD0 in `_lod0`): the import now tells them apart (60f7d57), in-game test pending (11.1).

## C4. Other models (topics 5, 6)

- FH1 / FH2 traffic models: close-up files are placeholders (17-vertex parts), rebuilt by the port.
- **Road holding FH vs FM4** (user report 2026-10-20): the car's own values (tyre compound id, widths, chassis friction scales, springs, ride height,
  anti-roll, aero, CM) are the same as FM4's for the same car (FH1 103 cars, FH2 138; `fm4dev\Traffic\tools\gripcmp`). What differs is game-wide:
  `List_TireCompound` (lateral grip FH1 ~1.8 / FH2 ~2.3 vs FM4 ~1.3, wear ×12) and half of `List_TireAffectCurve`. The port keeps FM4's rows for those
  ids, so a ported car grips like an FM4 car: nothing to convert per car.
- FM1: `.cab` / `.ca2` reader verified on 243 archives.

## C5. Documentation and community (topic 16)

- Format documentation in `docs/`. Discord: still few users; reports go into topics.

---

# D. Finished tasks (100% done)

Only closed work is here. Numbers are the new permanent ones; "Topic N" blocks hold the finished points of topics that still have open work in A.

## Topic 4. Forza Motorsport 3 → FM4 ✅ whole topic

- ✅ 4.1 FM3 carbin converter (3930 / 3930 files)
- ✅ 4.2 FM3 as a port source (74 / 74 trial)
- ✅ 4.3 X5 and Boxster work in game; Porsche make
- ✅ 4.4 FM3 digital dashes in game (cebc0a1)
- ✅ 4.5 Engine banks rule: FM4's bank if same name, else the source's
- ✅ 4.6 Cockpit camera centred on the steering wheel (part 9; e972289)
- ✅ 4.7 Backfire flames: FM3 ports DO show them (user 2026-10-20, no code change); exhaust locators = carbin header records type 2
- ✅ 4.8 Drag tyres (`CarPort.DragTires`, FM4's Level 4 compound 9 row; X5 offers Drag)
- ✅ 4.9 FM3 textures too bright: FM4 = PWL⁻¹(sRGB → linear(FM3)), applied to the DXT colour endpoints (X5 much darker, close to FM4)
- ✅ 4.10 Fully upgraded engine sound (`CarPort.CompleteEngineTuning`, checked by the user 2026-10-20)

## Topic 1 (Ids, database): finished points

- ✅ 1.6 Game database editor: cars, tracks, events, wheels, tables views; creation tools; FM3 / FM4 / FH1 / FH2 relation map, Go to / Used by, reference check;
  database view with a search per section and a row filter per table
- ✅ 1.7 C3 test without the game (`c3test.exe`, in-memory): clean car no errors; Data_CarBody deleted, a key twice, a second Data_Car with the same name: each
  caught (4/4 on FOR_FocusZX4_05 and CHE_Aveo5IE_09); a second car with the same Id refused by SQLite (primary key); port log line "database check: car N (...) OK"
- ✅ 1.8 Structure of the first allocator (2026-10-20, rule superseded by 1.1): one allocator in `DbIds.cs`; CarPort uses the new Id everywhere; C3 checks before the write;
  port window "Ported" by MediaName + build number; creators (swap fallback, events, races, AI participants, restrictions) and Power use the allocator; trial 20 FH1 cars
  (10 standard + 10 traffic) 20/20 OK, 3 FM3 cars OK; DEV install + uninstall of TOY_Camry_07: DB back identical
- ✅ 1.9 Uninstall also removes the aspiration rows CarUpgrades generated for the car's own engine

## Topic 2 (duplicate car): finished points

- ✅ 2.1 Audit of the .str of the 16 FM4 packs + 3 model-year / Autovista packs (`tools/FM4Audit`). FM4 is clean on structure (same keys in all languages, sorted, no
  duplicates, same key = same IDS name). FM4's own problems: 909 of 1088 shared keys are empty in one pack and real in another (the pack loaded last can blank a string);
  179 keys spelled differently between packs; 1 text with broken characters; 26 empty "Subtitles". **Rule for Forza Studio packs:** never ship an empty text for a key another
  pack / base fills, never change the text of a key that exists (new key = new Id above the maximum)

## Topic 3 (port FH1 / FH2): finished points

- ✅ 3.6 Model / wheel / brake conversion, DB rows with reference closure, strings in 18 languages, digital dash, carattribs, shadersettings, LE gr2 animations, engine sound and
  banks from the source game, FM4 tyre rule, PI mapping, port window with trial / review / install, `port_log.txt`
- ✅ 3.7 In-game check of a car installed with the review fixes: sound and swap choices OK (user 2026-10-20); materials left to 9
- ✅ 3.8 Port window split in two (user 2026-10-21): Tools > Import Car From Another Forza and Tools > Create DLC Package; offer id typed by the user (button Free suggests one)

## Topic 6 (FH traffic cars): finished points of old 14.1 (all checked in game by the user 2026-10-18..20)

- ✅ 6.3 List of problems from the 13-car test pack (`docs/internal_traffic_conversion.md`); mesh and cameras OK; donor engine sound accepted
- ✅ 6.4 Rims missing in the showroom (RAV4 / XC70 / F350): wheel head first / last LOD fixed in `CarbinModel.SetWheelLodRange`
- ✅ 6.5 Rename Material on one mesh only ("only this mesh" option); checked in the editor
- ✅ 6.6 Model tree: new part, drag a material onto a part or a LOD (`CarbinModel.MoveSubSection`), empty parts in yellow
- ✅ 6.7 `shadersettings.xml` follows the material edits (`ShaderSettings.cs`; Shader Settings view; 256 / 256 FM4 files read and written back the same; "Settings of car" copy)
- ✅ 6.8 Collision editor + rebuild (COLLISION tab, `ReplacePlaceholderCollision`, Z from the wheelbase centre; checked in game on the Yukon)
- ✅ 6.9 Textures per material (`ShaderTextures.cs`, `textures.xml`; viewer auto-textures with override); number plates removed (`CarPort.DropMaterial`)
- ✅ 6.10 Generic upgrades (`CarUpgrades.cs`, `Upgrades.ini`: Street / Sport / Race levels from FM4's medians), drive-type conversion rule, UPGRADES tab in the port window,
  aspiration rows (no twin turbo under 6 cylinders), engine swaps kept with the review; install warns when the car is also in an installed pack; model view side tabs on a vertical rail
- ✅ 6.11 Steering wheel and hands on cars without a source cockpit (rig rebuilt from the car's own wheel, hands from the pivot, grips on the measured rim; Focus ZX4)
- ✅ 6.12 Interior too dark: `interior_lod0.xds` = copy of `nodamage.xds`, cockpit interior → `leather`, `CarPort.SplitLights` moves light triangles to FM4's corner parts
  (closed by the user 2026-10-20)
- ✅ 6.13 Traffic cars shown in yellow in the port list (`CarPort.HasPlaceholderCloseUp`); all port settings in `.ini` files (`Upgrades.ini`, `Port.ini`); code 7458731

## Topic 7 (DLC packages): finished points

- ✅ 7.3 Pack base: merge DB from a port, Xenia package folder, DLC pack panel (n cars), STFS package for a real Xbox 360 (`StfsPackage.cs`), checked in game and on the console
- ✅ 7.4 Set Generated Thumbnail (Tools > Set Generated Thumbnail..., `SetThumbnailsForm.cs`, `XeniaThumbnails.cs`; Xenia `Thumbnail_N.xdc` = deflate + the car's 768×288 xds);
  user 2026-10-21 "molto bello"
- ✅ 7.5 Port from a car DLC (FH1 / FM3) → FM4, standalone (only the package + FM4), steps a-l: `DlcSource.cs` reader (0a1e9fb), source game from the pack's `ContentOffers.ID`, PI line per
  source game (FH1: FM4 = 1.189 PI - 0.363), swap engine rule (FM4's engine, else ask in the review), aspiration conversions, port window with a DLC folder as source, Add → / ← Remove,
  trial on 48 / 48 FH1 DLC cars, STFS `CON` / `LIVE` read directly (`StfsReader.cs`, 17 packages byte-identical), Open DLC Package + repack (06d4268), FM3 DLC Hyundai Car Pack 3 / 3
  checked in game, Uninstall of ported cars (`CarUninstall.cs`, 96dcf52). Checked in game: Escalade, LAM_AventadorJ_12, FOR_Mustang_95 (swap picked in the review)

## Topic 8 (new makes): finished points

- ✅ 8.3 `CarMake.Create`: List_CarMake row, names in List_CarMake.str of every language, logo in the six logo folders of `UI\Textures.zip`, zipmanifest lines; local in place with `bak\`
  backups, DLC into the pack's trial folder
- ✅ 8.4 Port window "+" next to Make: "Create in the game" or "Add to the DLC pack"
- ✅ 8.5 Tested (`fm4dev\Makes`): trial with two makes, in place on a copy, a full pack build (the pack gets the List_CarMake row, strings and 6 logos)

## Topic 9 (materials): finished points

- ✅ 9.6 Port report lists materials FM4 renders black, model view marks them red, Rename Material… with a list of suggestions (old C8), checked by the user
- ✅ 9.7 Material work inside the port (old 3k) superseded by the separate WIP materials project (user 2026-10-19)

## Topic 10 (wheels): finished points

- ✅ 10.5 Why a rim does not scale (BMW E30) and another does (S60): the 16-byte per-vertex table = LOD0 morph target (copy 1 = rim at max size); the game blends by the car's rim size
- ✅ 10.6 Rule: 15" reference LOD0 ⇒ morph table + header f8 / f10 = target radius; true-size LOD0 ⇒ no table. `CarbinModel.WheelMorphTable` / `Lod0MorphScale`, used by the FM3 port;
  checked in game (20c0fa2)

## Topic 11 (model editing): finished points

- ✅ 11.7 Edit Part (move / rotate / scale, merge, delete), OBJ part import / export, OBJ work folder

## Topic 12 (dash): finished points

- ✅ 12.4 .fbf / .bgf / .bsg parsed (431 / 431 byte-identical), FM3 dashes too (154 / 154); edit in place works in game (text colour, 2026-10-17, 0f69352)

## Topic 13 (music): finished points

- ✅ 13.2 Music and radio for FM2, FM3, FM4, FH1, FH2

## Topic 14 (cameras and viewer): finished points

- ✅ 14.2 Viewer camera and cockpit camera editor (checked 2026-10-17); Go to Camera (cockpit + 27 presets of `UICarTargetCameraPresets.xml`, 2026-10-20)
- ✅ 14.3 Car cameras (old 16): FM3 cockpit camera and COCKPIT CAMERA panel; the rest is parked in 14.1

## Topic 15 (platform, UI): finished points

- ✅ 15.3 Settings window (Tools > Settings…: FOLDERS / PORT / UPGRADES / VIEWER tabs, `Port.ini`, `IniFile.Set`, Restore Defaults with `.bak`); closed by the user 2026-10-20
- ✅ 15.4 Console Modding and `xdevkit.dll` removed; friendlier UI with the name Forza Studio (f141093, 9d08e0c); themed windows (original 4.6 ones kept as a tribute), dark list headers
- ✅ 15.5 Green recommended entries (`ForzaTheme.GreenItems` / `IRecommended`) instead of ★ marks

## Topic 16 (documentation): finished points

- ✅ 16.3 README update

---

# E. Old → new numbers

| Old | New | | Old | New |
|---|---|---|---|---|
| Now: C2 + C3 | 1.1, 1.2, 1.3 | | 9.1 / 9.2 / 9.3 | 4.7 / 4.8 / 4.9 |
| Now: 14.1 interior dark | 6.12 | | 10 / 10.1 | 13.2 / 13.1 |
| Now: 12.3 + 14.1 ini files | 15.3 / 6.13 | | 11.1 / 11.2 | 10.5 / 10.6 |
| 1 Car port FH1/FH2 | 3 (3.1, 3.2) | | 11.3 / 11.4 | 10.1.1 / 10.1.2 |
| 1: tyre width, 11.6 | 10.4 | | 11.5 | 10.1 |
| 2 DLC package | 7 | | 12.1 – 12.5 | 15.4 / 15.2 / 15.3 / 15.1 / 15.4 |
| 2.1 (was 3.6d) / 2.2 / 2.3 | 7.1 / 7.2 / 7.4 | | 13.1 / 13.2 | 9.2 / 9.3 |
| 3 Port from DLC | 7.5 | | 13.3 / 13.4 | 14.2 |
| 4 / 4.1 | 9 / 9.1 | | 14.1 a-h | 6.3 – 6.11, 6.13 |
| 5 Dash | 12 | | 14.1 FM1/FM2 rebuild / 14.2 | 5.3 / 5.1 + 5.5 |
| 6 DB editor | 1.5, 1.6 | | 15.1 – 15.3 | 16.1 – 16.3 |
| 7 Model editing | 11 | | 16 Cameras | 14.3 |
| 8 Auto LODs | 11.5 | | 17 New makes (17.1-17.6) | 8.3 – 8.5, 8.1, 8.2, 3.8 |
| 9 FM3 → FM4 | 4 | | 18.1 / 18.2 / 18.3 | 1.1 / 2.1 / 2.2 |
| B1 / B3 | B1 / B3 | | 18.4 / 18.5 | 2.3 / 1.4 |
| C1 / C2 / C3 | 1.4 / 1.1 / 1.3 | | C4 / C5 / C6 / C7 / C8 | 5.3 / 6.2 / 3.5 / 11.4 / 9.6 |
| Maybe later | 3.4, 3.5.3, 14.1, 12.3, 5.5, 9.4, 9.5, 11.6, 2.4 | | Memory side projects | 17.1 – 17.4 |
