# ForzaEditor roadmap

How we work:

- **One item at a time.** The item under **Now** is the only one being worked on. It is closed before anything else starts.
- **An item is done** when it works (checked in game, if it touches the game), is committed, and is ticked here.
- Each topic below lists what is done (`[x]`) and what is left (`[ ]`). The order inside a topic is the planned order.
- New ideas go to **Later**, not into the current item.
- **Game folders:** tests and installs go only to `FM4_Extracted_DEV`. `FM4_Extracted` is the clean copy and is never written.

Status: ✅ done · 🔧 in progress · ⏸ paused · ⬜ not started

## Now

_to decide._ Topic 3 (ported cars as a DLC package) is done: packs of n cars work in Xenia and on
a real Xbox 360. Its open points (3.6d, 3.8) are listed there.

## Next (in order)

1. _to decide_

---

## 1. Car port: Forza Horizon 2 → FM4 ✅ working, open points

Tools > Port Car From Forza Horizon… (`CarPort.cs`, `CarPortForm.cs`).

- [x] Model, wheel and brake conversion (carbin type 5/3/1 → FM4)
- [x] Database rows, reference closure, `Data_CarBody`, key clashes
- [x] Strings in all 18 languages, with key remapping
- [x] Digital dash converted to the FM4 Anark layout
- [x] carattribs, shadersettings and animations (little-endian gr2)
- [x] Engine sound, harmonic tunings and missing sound banks imported
- [x] FM4 stock tyre rule, local PI mapping, class from PI
- [x] Port window: car list, donor ranking, trial run, install
- [x] Ported cars work in game; the trial run passes on every new FH2 car
- [x] Small texture glitches on ported cars (P1 and LaFerrari checked: fine)
- [ ] Tyre width on ported cars whose wheel mesh is wider than usual (to look at calmly)
- [x] Data review step in the port window: after a trial run, a table lists what is not regular
      (values pointing at rows FM4 lacks, e.g. FH-only `FamilyBodyID`; materials no FM4 car uses),
      each with a fix to choose; the next trial run and the install apply the fixes
- [ ] In-game check of a car installed with review fixes
- [x] Sound banks: every bank comes from the source game, except one identical to the target's
      (same name and same bytes: not copied); a same-name bank with other bytes stays FM4's and is
      reported; the donor-fallback tunings' banks are imported too
- [x] Conversion log: `port_log.txt` (the full report) inside the converted car zip
- [x] Rule: an FH car without its own harmonic tuning uses the donor's
- [ ] Big-endian vs little-endian gr2: find the differences, and whether a game mixes both, before
      writing a converter (own topic)

## 2. Car port: Forza Horizon 1 → FM4 ✅ done

Same tool and code as topic 1; its open points are listed there.

- [x] FH1 accepted as source; the trial run passes on every new FH1 car
- [x] Ported cars work in game (458 Spider, S65 AMG, R8 GT, R8 GT Spyder)
- [x] Fixes: donor animations on cars without gr2 files, missing engine sound banks
- [x] In-game check of the cars without gr2 files after the animation fix (both R8s OK)
- [x] FH1 cars without a harmonic tuning use the donor's (fixed rule, see topic 1)

## 3. Ported cars as a DLC package ✅ done, open points

Today the port writes into the game's own files (vanilla `gamedb.slt`, string tables, zips, manifest).
The goal is a separate DLC package instead, so the base game stays untouched and the package can be
installed or removed on its own.

Everything is written in C# inside ForzaEditor. No Python and no Xbox SDK. Comments credit the help
from Discord: **Jew (@mr.abortion)**, whose `fm4dlc` toolchain covers layout, merge, strings and the
STFS writer, and **XTD (@xtd_ac)**, who supplied the folder DLC for Xenia, the XLAST package and console
tests. Material: `fm4dev\DEV_DLC`.

One package holds **n cars** chosen by the user, with its own name (e.g. a pack of FH1 cars). The
reason: a DLC string table replaces the game's whole table, so separate one-car packages would hide
each other's names (still to be confirmed in game).

- [x] 3.1 Study a real FM4 car DLC (official offer 31, 10 cars) and XTD's package: layout, `zipmount.xml`,
      `stringtables.xml`, `<offer>_pri_N` loose folder, `<offer>000_merge.slt`, full string tables
- [x] 3.2 DEV DB restored to the clean FM4 one (FM4_Extracted, 503 cars); DEV's ported one backed up in
      `bin\Debug\bak\2026-10-15_21-00-00_bak_DEV_before_DLC_gamedb.slt`
- [x] 3.3 Merge DB: run the port on a temporary copy of the clean DB; the new rows go into
      `<offer>000_merge.slt`, plus `ContentOffers` and `ContentOffersMapping`
      (`DlcPackage.BuildMerge`; 458 Spider: 133 rows in 49 tables; clean DB + merge = ported DB.
      Offer names are filled in 3.4 with the strings; the in-game check is 3.5)
- [x] 3.4 Package folder for Xenia (`content\0000000000000000\4D530910\00000002\<package>`):
      car/wheel files, audio tunings and banks, thumbnails, string tables, `zipmount.xml`, `.puboffer`
      (`DlcPackage.BuildFolder` from a trial run; Gallardo LP570-4 Superleggera (FH1), offer 91,
      folder `4D530910FF005B00`, installed in Xenia for the 3.5 test)
- [x] 3.5 In-game test in Xenia with the base game untouched: Gallardo LP570-4 (FH1) as a DLC
      works (garage, model, dash, sound, upgrades). Xenia lists the folder only with
      `Headers\00000002\<package>.header` next to it (written by the build). `license_mask` is not
      needed: the test ran with 0
- [x] 3.6 Port window, so nobody needs the test tools (right panel "DLC pack (Xenia)"):
      - Add Selected Car / Remove / New Pack, pack name; the pack is kept in the settings; the
        offer number is picked free (game DB and packages in Xenia) at the first build, then kept
      - Trial Pack: every car in turn into one trial folder (CarPort reads what the car before
        wrote there), each car's review shown when it is selected in the pack (its name in the
        review title); fixes changed after the trial are named in the status, and Build asks
        whether to run the trial again first; older pack trials (436 MB each) are deleted
      - Build DLC Package straight into Xenia's content folder (its `content_root` honoured),
        header included; replacing a built pack asks first; Remove Pack; Xenia must be closed
      - "Install In FM4" stays as it is
      - checked with the real window (tools\packform.exe): Gallardo + M600, 270 merge rows
- [x] 3.6b In-game test of a pack with several cars, built from the window. First try (5 FH1 cars):
      all load; material fixes looked "not applied" on 458 Spider, S65 AMG and R8 GT Spyder. Cause:
      DEV still held their zips from earlier installs, mounted by `zipmanifest.xml` at priority 80,
      above the package's loose files (65). The trial did apply the fixes (`port_log.txt`).
      DEV cleaned (24 car/wheel zips of 12 old ports moved to `fm4dev\DEV_DLC\dev_leftovers`, their
      manifest lines removed); Build now warns when the FM4 folder has a pack car's own zips.
      Checked again in game: fixes applied
- [x] 3.6f Material lists measured again on the 317 original FM4 cars (943 models, 150 names).
      The old "FM4 materials" list had been measured on a game holding ported cars: 25 FH names
      (`lights_gls_*`, `fogred`, `fogwhite`, `oldhead`, `numplate`...) counted as FM4 ones, were never
      reported and stayed black. Now they go to the review. The fix list offers only the 67
      materials used by at least 50 cars (was 175). Checked by the user
- [x] 3.6e Port window restyled like the main window: program icon, menu bar with the brand and
      the red rule, CARS / DLC PACK sections, splitters, REPORT / REVIEW tabs (review count, opens
      by itself), status bar, lists drawn like the database view (class badges with in-game PI).
      Checked by the user; the error on closing the window is fixed
- [x] 3.6c Documentation: `docs/DLC_PACKAGES.md` (use, format, code, test tools, credits)
- [ ] 3.6d Several ForzaEditor packs at once: each pack's string tables built on top of the other
      installed packs' (today only the highest priority pack shows its car names)
- [x] 3.7 STFS package file for a real Xbox 360 (Jew's writer ported to C#, `StfsPackage.cs`):
      LIVE, licence open to everyone, header fields as XTD's console package, empty signature
      (RGH/JTAG). Names in STFS hold at most 40 characters and brake carbins exceed it, so the
      `<offer>_pri_65` folder goes in as `<offer>_pri_65.zip` (stored, Forza layout) as in the
      official packs. Demo built with Jew's Python writer works on the user's RGH console; the C#
      writer and zip are byte-identical to it (1 car and 5 cars, all hash levels).
      Port window: Build for Xbox 360... writes `<folder>\Content\0000000000000000\4D530910\00000002\<id>`
- [x] 3.7b In-game test on the console of a package built by the window (works)
- [ ] 3.8 Transfer to the console: FTP (Aurora/FSD) or Xbox 360 Neighborhood (later, the user's side)

## 4. Materials FM4 renders black ✅ done

- [x] Port report lists materials no FM4 car uses
- [x] Model view marks them in red, with Rename Material…
- [x] User test of the rename in the editor (works)
- [x] Decision: no fixed FH → FM4 table; the user fixes the red nodes in the carbin editor
      (they can also go into the data review step, topic 1)

## 5. Digital dash (Anark) editor 🔧

- [x] .fbf / .bgf / .bsg parsed, byte-identical on 431 files
- [x] Read-only Dash view (tree, preview, properties)
- [ ] Edit in place: position, scale, colour, text, texture, then a test in game
- [ ] Slides (pages shown one at a time)
- [ ] Behaviour parameter names

## 6. Game database editor ✅ working

- [x] Cars, tracks, events, wheels, tables views; creation tools
- [x] Relation map for FM3/FM4/FH1/FH2, Go to / Used by, reference check
- [ ] Decode `EventRestrictions.Value`

## 7. Model editing (FM4) ✅ working, tests pending

- [x] Edit Part (move/rotate/scale, merge, delete), OBJ part import/export
- [x] Whole-model OBJ work folder (`<carbin>\lodN\<part>.obj`)
- [ ] In-game test of a whole model rebuilt from OBJ, and of edited parts
- [ ] Effect of the cleared glass A/B arrays on rebuilt parts
- [ ] In-game test of Merge Into

## 8. Automatic LODs ⬜

- [ ] Decimation (QEM) from lod0 to lod1-lod5 at the measured ratios (23.7 / 8.8 / 6.7 / 3.4 / 2.5 %)
- [ ] Keep UV seams, normals, materials and damage data
- [ ] Hook into the OBJ work folder import

## 9. Forza Motorsport 3 ⏸ study done, port not started

- [x] FM3 detected; FM3 carbins read byte-identical (study only)
- [ ] Remaining FM3 vertex bytes; FM3 DigitalGauges format
- [ ] Port step 3: FM3 → FM4 model conversion
- [ ] Port step 4: database column diff for FM3-only cars

## 10. Music ✅ done

- [x] Music and radio for FM2, FM3, FM4, FH1, FH2

## Later

- FM2 model writer (FM2 is out of scope for now)
- .gr2 animation editor
- Cars built from scratch (a full wizard, also as a DLC package)
- README update (FM3/FH1, port tool)
