# ForzaEditor roadmap

How we work:

- **One item at a time.** The item under **Now** is the only one being worked on. It is closed before anything else starts.
- **An item is done** when it works (checked in game, if it touches the game), is committed, and is ticked here.
- Each topic below lists what is done (`[x]`) and what is left (`[ ]`). The order inside a topic is the planned order.
- New ideas go to **Later**, not into the current item.
- **Game folders:** tests and installs go only to `FM4_Extracted_DEV`. `FM4_Extracted` is the clean copy and is never written.

Status: ✅ done · 🔧 in progress · ⏸ paused · ⬜ not started

## Now

**Topic 3: ported cars as a DLC package.** The Xenia DLC folder works in game (3.5). Next: the port
window (3.6).

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

## 3. Ported cars as a DLC package 🔧 Now

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
      works (garage, model, dash, sound, upgrades). Xenia needs two things:
      `Headers\00000002\<package>.header` next to the folder (written by the build), and
      `license_mask = -1` in `xenia-canary.config.toml`
- [ ] 3.6 Port window, so nobody needs the test tools:
      - choose n cars from the source game, pack name; offer number picked free automatically
      - trial run that converts and fixes them all (CarPort chained: each car starts from the
        previous one's output, so the pack's ids and string keys do not clash)
      - "Build DLC package": straight into Xenia's content folder (setting), header included;
        rebuilding an existing pack replaces it after a confirmation; "Remove pack"
      - a reminder when Xenia's `license_mask` is 0
      - "Install into game" stays as it is today
- [ ] 3.6b In-game test of a pack with several cars
- [ ] 3.7 STFS package file for a real Xbox 360 (Jew's writer ported to C#), tested by XTD or on
      the user's RGH consoles
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
