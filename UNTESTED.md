# Changes made and NOT tested (state of 2026-10-23)

Everything below is coded, compiles and is committed, but nobody has used it yet. Use this file to resume:
test each point, then tick it here and update `TODO.md` (state and date). Roadmap numbers in brackets.

How a test build was made on this machine (the project targets .NET 2.0, which this PC cannot build):
`MSBuild v4.0` with `-p:Platform=x86 -p:PlatformTarget=x86 -p:AllowUnsafeBlocks=true -p:TargetFrameworkVersion=v4.0`,
output in the session's scratchpad `out\`, `mscorlib.dll` / `norm*.nlp` deleted, `ForzaEditor.exe.config` with
`<startup useLegacyV2RuntimeActivationPolicy="true"><supportedRuntime version="v4.0"/></startup>`. Your own Visual Studio
build is unchanged and is the real one to test.

Game data used for checks: `Z:\Lavoro\Personale\extracted_dvd1` (FM4 DVD1, `Media\db\gamedb.slt`).

---

## A. Game database view (session 2026-10-23) [topic 18]

Code: `ForzaStudio/ArchiveEditorForm.GameDb.cs`, `ArchiveEditorForm.Creators.cs`, `ArchiveEditorForm.Navigation.cs`, `GameDatabase.cs`.
The toolbar now has groups: **Cars Wheels Tracks Events | Engines Makes Countries Manufacturers Lists | Tables**.
A section appears only if the database has its table.

- [ ] A1. **Open the database** (a crash here was fixed: `SectionTables` lacked the Lists entry; commit 5611985). Check it opens and every button works.
- [ ] A2. **Engines** (`Data_Engine`): columns Id / Engine / Media name / Config / Cars (cars using it); tab "Engine" (record), tab "Cars and curves"
      (rows of `List_UpgradeEngine`, `Combo_Engines`, `List_TorqueCurve`). Go to on a car's engine opens this section.
- [ ] A3. **Makes** (`List_CarMake`): logos (`IconPath` + `IconPathLight`; Chrysler, Infiniti, Jaguar, Mitsubishi have no light file),
      columns Id / Make / Code / Country / Cars, tab "Cars and parts" (cars, engine and drivetrain upgrades, decal order, part discounts).
- [ ] A4. **Countries** (`List_Country`): flag (10 of 22 have none), Makes count, tab "Makes and cars" (makes, cars, environments, AI players).
- [ ] A5. **Manufacturers** (`List_PartManufacturer`, NOT the same as car makes): logo built as
      `GAME:\Media\UI\Textures\UI4\Logos\Car_Parts_Logos\<LogoPath>.xds` (only ~232 of 569 files exist in FM4; empty box is normal);
      tab "Parts" lists every `List_Upgrade*` table with `ManufacturerID` except `List_UpgradeEngine` / `List_UpgradeDrivetrain`
      (those point at car makes), plus wheels (`PartManufacturerID`), tire brands (`TireBrandId`), part discounts (`BrandId`).
- [ ] A6. **Lists**: a picker (button "List: <name> (rows)" next to the search) over 15 id lists: Drivetrains, Motors, Car bodies, Aspirations,
      Cylinders, Engine layouts, Engine placements, Drive types, Variable timing, Powertrains, Car types, Car classes, Tire compounds,
      Regions, Upgrade types. Columns: the key + up to 5 useful ones (ids shown as the name of the row they point to, localized texts as text;
      paths, long texts and empty columns left out). Tabs "Row" (record) and "Used by" (what points at it). Go to / Used by to these tables opens here.
- [ ] A7. **Events header picture**: poster of the career type (`CareerEventTypes.PosterPath`; 15 of 32 types have one), else the first race's
      track logo. The header text and buttons moved right to make room.
- [ ] A8. Related-row counts no longer raise an error for a table that lacks the column (another game version).

Ideas not done: "Used by" column in the lists, filter "only used", button "new row (copy of this)", light make logo falling back to the dark one.

## B. Engine designer: paste from Excel (2026-10-23) [2.3.2]

Code: `ForzaStudio/EngineDialog.cs`, `PasteTorque`.

- [ ] B1. Ctrl+V in the torque table with **two columns** (rpm, N.m at any rpm, tab / `;` / space separated, comma decimals accepted, a header
      line skipped): the curve is interpolated onto the table's rows (every 250 rpm); rows outside the pasted rpm range stay as they were.
- [ ] B2. One column of N.m still pastes from the selected row down (old behaviour).

## C. Duplicate Car wizard (2026-10-23) [2.3]

Code: `ForzaStudio/DuplicateCarForm.cs`.

- [ ] C1. "in a new DLC pack" can always be chosen. If Xenia's folder is not in Settings, a folder dialog asks for it, remembers it
      (`CarPort.Xenia`) and finds the next free offer. Cancel or a bad folder returns to "in the game folder".
- [ ] C2. **The whole wizard still needs a usability pass** (user 2026-10-23: "tutto il wizard sarà da migliorare nell'utilizzo"):
      collect what is awkward while testing, then change it (TODO D-1).

## D. Work already in the tree before today, still untested [topics 1, 2, 6]

From the sessions of 2026-10-22 (see `TODO.md`, section 1 and 2 for the full notes):

- [ ] D1. Duplicate car in the game folder and into a DLC pack (`CarDuplicate.cs`) [2.2]; checked on copies only.
- [ ] D2. Own engine for the copy (`CarEngine.cs`, `EngineDialog.cs`, `PowerGraph`): unique `Data_Engine.MediaName`, Sim* values, upgrade levels [2.3.2].
- [ ] D3. Id rule above `GameIdLimits.json`, ModelId = Id (`DbIds.cs`) [1.1, 1.2]; integrity rules (`DbIntegrity.cs`) used by the SQL console, duplicate and port [1.3].
- [ ] D4. SQL console and diagnostics (`SqlConsoleForm.cs`, `SqlGuard.cs`, `SqlDiagnostics.cs`) [1.4]; EventRestrictions decode (`EventRestriction.cs`) [1.5].
- [ ] D5. DRIVER tab in the model view (`ArchiveEditorForm.Driver.cs`) [6.2].
- [ ] D6. Crash deformation options in `Port.ini [Damage]` (`Words`, `Tables`, `FlipSections`) [3.1, B1 T2 / T3 / T5].
- [ ] D7. Several packs at once (string layers) [7.1]; new makes [8.1]; Add a song [13.1]; generic brakes [3.3]; FM2 port [5.1].

## E. Digital dash editor (made in a parallel session, 2026-10-23; committed together with this file) [12]

Code: `AnarkBgf.cs`, `AnarkBsg.cs`, `AnarkDash.cs`, `AnarkDashCanvas.cs`, `AnarkFbf.cs`, `ArchiveEditorForm.Dash.cs`, new `DashInspector.cs`;
notes in `docs/MANUAL.md` and `TODO.md` (12.3, 12.5, 12.6, 12.7). Not written by the database session; read those notes before testing.

- [ ] E1. Property inspector + drag on the preview (move, 8 resize handles, arrows) [12.3].
- [ ] E2. Multi-selection (Ctrl+click, rubber band, Ctrl+A, align / spread) [12.5].
- [ ] E3. Duplicate node (Ctrl+D): re-read tests passed offline; in game: do slides / animations ignore the extra elements? [12.6].

---

## Where things are

- `ROADMAP.md`: goals. `TODO.md`: work, test queue, states and dates. `docs/RESEARCH_NOTES.md`: research notes.
- This file lists only what is coded and not yet seen working; when a point is tested, tick it here and change its state in `TODO.md`.
