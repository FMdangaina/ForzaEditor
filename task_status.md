# Task status - chat of 2026-10-27 (test queue of TODO.md)

Where we stopped. The full lists are in `TODO.md` (test queue 1a / 1b, work 2a); this file is only this chat's state.

## Committed this chat
- `cd8361a` Engine sound and numbers, ratings at import (D-14), make logos (D-13), Tools > Car Performance From Game (D-15).
- `0effa50` Fixes from the tests: Write of Car Performance / SQL console reaches the file (`OnGameDbEdited`), `DbIds.CarId` skips an Id any car column
  of the source holds (FH2 `CameraOverrides`), FH1 / FH2 crash damage +1 (`[Damage] ScalarBonus`), TODO updated.
- Not pushed (the user pushes).

## Left out of the commits on purpose (other chats' work, still uncommitted)
`AnarkBgf/Bsg/Dash/DashCanvas.cs`, `ArchiveEditorForm.Dash.cs`, `DashReport.cs`, `docs/ANARK_DASH.md`, `UNTESTED.md` (dash, topic 12),
`ArchiveEditorForm.Creators.cs` (event poster `EventBkgd<Id>`), and the `DashReport.cs` line of `ForzaEditor.csproj`.
Until `DashReport.cs` is committed the working tree builds, the committed tree misses that csproj line.

## Test queue result
| Test | State |
|---|---|
| T-G6, T-G13 make logo | ok in game |
| T-G11 material suggestions | ok |
| T-G14 Car Performance From Game | ok in game (Bowler: showroom = garage) |
| T-G8 add a song | ok in game: Blow Me Away in `InRaceLayer1Playlist`, heard on Maple Valley (layer 1); menu playlists, second song, mono / 48 kHz left |
| T-G12 crash damage | +1 on every DamageScalars point deforms well (Bowler); coded for FH1 / FH2 ports only, FM3 unchanged; a new FH port to check the log line |
| T-G15 ratings at import | Raptor ported beside FM4's as `FOR_SVTRaptor_12_FH1` (id 1644) and `_FH2` (id 1657), donor `DOD_RamSRT10_06`, in `FM4_Extracted_DEV`. Bars close to FM4's (max diff 0.4). **SUSPENDED** by the user, see below |
| T-G10 OBJ on a wheel zip | postponed (no 3D editing now) |
| T-G7 generic brakes | todo, next |
| T-ED1 .. T-ED9 | editor checks without the game, explained to the user, not done (ED8 already covered by the game) |

## Done: Bowler (FH2, DSC) blow-off instead of supercharger whine, closed by the user 2026-10-27
- Symptom in game: blow-off on throttle lift, no supercharger whine (native Ford GT: whine, no blow-off). Cause: the `blowoffL1..L3Level/Choice` attributes of the FH2 `_ET.xml` (Bowler L1 1/2, L2 3/5, L3 3/5); DB, CMT, volumes and harmonic tunings were not the cause.
- Bisect in the installed pack (original kept in the session's scratchpad `backup\bowler_pack_original.stfs`): Ford GT's whole ET = whine back; Bowler's ET with only the slots of the Ford GT (1/1, 2/1, 3/1) = fine, confirmed in game.
- Fix in the port, uncommitted: `CarPort.DscBlowOff` (only engines with `AspirationID_Stock` 5, called in `PortSound`), logged in `port_log.txt`; built, trial port of the Bowler OK (`E:\DEV\fm4dev\FH2toFM4\trialBowlerDsc`). Not checked in game on a fresh port; other FH supercharged cars need a new port.
- Memory note: `bowler-dsc-blowoff.md`.

## Suspended: Raptor FH1 / FH2 grip
Showroom PI 265 (FH1) / 269 (FH2), garage 314 for both; the original FM4 Raptor has LESS grip than the ports. In the DB the body, tyre compounds, springs
and anti-sway ids are identical; they differ in ABS release rear, the upgrade engine (643 -28 kg vs 1020 +20 kg), stored PI (orig 0.2258, ports 0.2128 / 0.2165)
and class (D vs E). Next if resumed: the original's showroom + garage PI from the user, then `zipdiff.exe` of the original car zip vs the port
(grip files inside the zip), car-specific tyre model (D-16).

## Open, decided
- D-18: donors from the DLC too (user asked): list the installed packs' cars, read their zips from the STFS, merge their DB rows. Not started.
- D-16 / 3.9: copy `TireA/B/C<car>` models. D-2 light make logo fallback. D-8 installed DLC check.

## Harness of this chat (`E:\DEV\fm4dev\Ratings`, outside the repo)
`pr.exe` (port + install, env `TAG`, args: source, target, car, donor, `-`), `dump.exe` (merge DBs of Xenia's packs -> `merge_<offer>.slt`),
`cmpcar.exe` (car rows of two DBs, differing columns), `zipdiff.exe` (two car zips, entry by entry). Build: `csc /platform:x86 /r:...\ForzaEditor.exe X.cs`.
The FM4 pack of the original Raptor is offer 26 (December IGN Pack), car 1460.

## Digital dash editor: SUSPENDED by the user 2026-10-27 (too much time, fine as it is; changes later)
In game: Add > Gauge crashed (Gauge_Clock2), Add > Texts from another car's dash (12.9, AnarkDash.Import) did not work either; cause unknown, hypothesis = blocks of the .bgf tail not decoded (12.8). Next time start with ONE duplicated text in a clean ALF_BreraTi_09.

## Digital dash editor (topic 12, chat of 2026-10-26; all uncommitted, details in `TODO.md` 12.6-12.8, `UNTESTED.md` E3 / E4, `docs/ANARK_DASH.md`)
- **Crash on load of the first copies** (duplicated gear text, then an added text; the game hung loading the car): cause found = .bgf header word 0x1C is the **highest element id << 8** (431 of 431 dashes), copies took id 248 over 247. `UpdateHeader` now sets it. **In game: not yet confirmed** (needs a clean `ALF_BreraTi_09` dash, Xenia closed, then Add > Gauge and load the car).
- Format found: elements are depth first in all 431 dashes; .bgf tail = 5 size-prefixed blocks (behaviours, animation tracks, events, empty, **slides**); slide = 25-byte name, duration, count, entries (element index, flag, A items, N tracks); header 0x5C / 0x60 / 0x64 / 0x68 = slides / entries / A items / track refs; a slide lists the elements its component owns (3640 of 3684). .bsg tail = u32 size + (id, kind 6 material / 7 image) per material and image, count at 0x28.
- **Data contract**: the scene's first behaviour (94 parameters, identical in FM4 / FH2 / FH1) = "on" bool + "target" path per game value (rpm, boost, gear, speed, fuel, laps, temperatures...). Names are deductions (`AnarkBgf.ContractLabels`). Binding a text = point its target at the element path and switch "on".
- Done in the editor: Duplicate rebuilt (appended under the backdrop / layer, slides, bsg table, max id), Delete (real for the last elements, else hide), **Add > Gauge** (copies a whole text gauge, component + group + text + its slides, optional feed with a game value), Structure... report window, contract names + target drop-down in the inspector. Offline checks `E:\DEV\fm4dev\anark\dashcrash\adddel.exe`: copies of text / rectangle / image / group / gauge on all 431 dashes, 0 bad, remove gives the files back byte for byte.
- Limits / open: only gauges without behaviour, tracks or events can be copied (no bars, METER_*); real insertion in the middle and delete of any element need the events block and the behaviour slot lists decoded (12.8); a value feeds one text only; channel names unverified in game.
- Next: in-game test of Add > Gauge (Clock or Gear copy, fed with rpm); then a library of gauges from other dashes (cross-dash import).

## Notes
- The auto-mode permission check failed often (no verdict) today; retrying the same command worked each time.
- `FM4_Extracted_DEV` has car zips for only part of the database (the Mustang GT '05 is in the DB without a zip): pick a donor with a zip.
- Forza Studio and Xenia: `xenia_edge` was running at the end of the chat; close it before any write to `FM4_Extracted_DEV`.
