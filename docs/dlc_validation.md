# DLC validation (Tools > Check and Validate DLC)

Checks a DLC package and lists every problem it finds, with the table, row, key, car or file it is about. The check never writes anything. **Fix...** changes the package only after you tick what to change.

Code: `ForzaStudio/DlcValidator.cs` (checks), `DlcValidatorForm.cs` (window), `DlcFixer.cs` and `DlcFixForm.cs` (Fix).

## Using it

1. Tools > Check and Validate DLC.
2. Pick one or more packages from the list (the packages installed in Xenia's content folder, all games), or use *Package file...* / *Folder...* for a file or an extracted package.
3. *Validate*. The report lists errors first, then warnings, then information. The text can be selected and copied.
4. *Deep check* also unpacks every file of the package, to find damaged data. It is slower.
5. *Fix...* turns on after you validate **one** package (see below).

A file is compared with the game folder set in Tools > Settings > Folders (FM4: the port target). Without it the checks that need the game are skipped and the report says so. A file that is in neither the package nor the game is also searched in the other packages of the same folder.

## What is checked

| Area | Checks |
|---|---|
| structure | the package opens; `Media\<offer>.puboffer` matches the offer; `zipmount.xml` and `stringtables.xml` are valid XML and name only zips and tables that exist; empty files |
| database | every table and column of the merge database exists in the game's; the FM4 integrity rules (`docs/DATABASE_RULES.md`); `ContentOffers` row and its `OfferId`; per car: `ModelId = Id`, the `Data_CarBody` row, an engine row |
| keys | a key that is already in the game's table (identical row = warning, other values = error, both listed with the row's values); keys at or below FM4's largest, per table with examples (normal for an official pack) |
| references | each row of the package that points (`GameDbSchema` relation) at a row that exists nowhere: the row, the column, the value, the target table. Relations that already dangle in the original game are only counted |
| cars | per car: the car folder and a LOD0 carbin, the thumbnail, the stock wheel row and wheel model, the engine rows |
| sound | per engine: `<engine>_ET.xml`, the `*_HT.xml` it names, the `<Soundbank>` `.fsb` of each, the car's `<car>_CMT.xml`. A file only another installed package has is information ("it must stay installed") |
| strings | every language has the same tables and keys (what a language lacks shows as blank text); `_&<n>` references of the database; the pack's own name and description are not empty. The DEV and LOC pseudo languages are skipped. The same finding in several languages is one line naming them |

## Fix...

Fix opens the list of what could be changed. Each item has a tick box; the original package goes to `bak\` (when `[Backup] Enabled` is on in `ForzaStudio.ini`), the package is packed again and checked again.

| Item | What it does | Ticked |
|---|---|---|
| database | deletes the rows of the package's merge database that repeat the game's row exactly (same key, same values): they only make the merge fail. Not offered when the game database already holds the package's cars (it is not a pristine game) | yes |
| strings | a key a language lacks is added, text copied from EN (else from the first language that has it) | yes |
| pack name | the empty `ContentOffers` name and description are written in every language, with the name and description typed in the window | yes |
| files, other package | a tuning or sound file only another installed package has is copied in, so the package stands on its own | yes |
| files, similar name | a file the package holds under a similar name (`ALF_ZagatoTZ3_12_ET.xml` for `ALF_ZagatoTZ3Stradale_12`) is copied under the name the database asks for. Only when exactly one candidate exists | no, check it first |

Never fixed automatically:

- keys that exist in the game with other values (it is not known which value is right);
- references to rows that do not exist;
- the `OfferId` and the package folder name (changing them can break the installation);
- a tuning file or sound bank that exists nowhere: the sound is a choice, use Tools > Change Car Sound.

Xenia should be closed before a fix (the tool warns if it is running). The fix works on STFS package files; fixing an extracted folder is written but was not tested.

## Reading the results

- The comparison game's database may already contain packages' rows (the DEV game does). Those rows show as "identical" warnings or as conflicts. Validate against a pristine game database when in doubt.
- *In neither the package nor the game* is an error only when the game folder is set; it can still be satisfied by a package installed elsewhere.
- An official pack normally comes out OK or with warnings only (checked on the 24 packs of the FM4 content folder, 2026-10-28).

## Testing without the window

`E:\DEV\fm4dev\DlcValidate` holds two small harnesses that load the built exe: `validate.exe normal|deep <out.txt> <package|folder>` and `fix.exe <package> [apply]` (always run `apply` on a copy).

## Not tested yet

The window with a real click-through, folder fix, FH1/FH2/FM3 packages (the integrity rules are FM4's and are not run for them), deep check on every package.
