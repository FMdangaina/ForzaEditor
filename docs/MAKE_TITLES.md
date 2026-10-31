# Make titles (affinity titles of a new car make)

FM4 gives every make three earnable titles: "<Make> Fan", "<Make> Enthusiast" and
"<Make> Aficionado", at affinity levels 1, 10 and 25. A make created by Forza Studio
(`CarMake.Create`) or added by a port (`CarPort.PortMake`, e.g. Bowler from FH2) gets them too
(`MakeTitles.cs`).

## What the game has

| Where | What |
|---|---|
| `List_BadgeAndTitleUnlocks` | three rows `<Base>_Affinity_1..3` (Base = `List_CarMake.IconPathBase`). Category by the alphabet: 4 A-C, 5 D-J, 6 K-M, 7 N-R, 8 S-W. `UnlockBadgeTexture` / `BadgeSelectionTexture` = the make's logo in `Manufacturer_Logos_SM` / `_SM_wtbg`. `SortOrder` puts it after the make before it in the alphabet. |
| `List_BadgeAndTitleUnlocks.str` | `IDS_UnlockDescription_<id>` ("Reach Eagle Affinity Level 10.") and `IDS_UnlockTitleString_<id>` ("Eagle Enthusiast"). The wording of each language is the one of Ferrari's rows with the name replaced; a language whose text lacks "Ferrari" gets the English text. |
| `Media\GameTunableSettings\BadgesAndTitles\BadgeAndTitleRules.xml` | rule `Drive_<Base>`: `<Manufacturer id="<List_CarMake.ID>"/>`, `testValue="AFFINITY"`, triggers 1 / 10 / 25 with the unlock ids. The progress itself is on the player's save, keyed by the make id (`NewProfile_Career_Affinity`). |

Unlock ids: after the largest of the table, of FM4 with its official DLC (1064) and of the installed
packs (`DbIds.Next`), so the first new make gets 1065-1067.

## Where it is written

- **Installation in the game:** the rows and strings in the game's database and string tables; the
  rule is added to `BadgeAndTitleRules.xml` (backed up first). The game reads that file by name, so a
  second XML in the folder would not be read.
- **DLC pack:** nothing of the game is touched. The trial folder gets `<Base>.xml` per make; the pack
  build joins them into `Media\GameTunableSettings\BadgesAndTitles\<offer>.xml` of the pack, as the
  official offer 31 does (`30.xml`, rule `Drive_Ultima`, unlocks 1030-1032). The rows travel in the
  merge database and the strings in the pack's `List_BadgeAndTitleUnlocks.str` (the whole table, like
  any changed table).
- **Uninstall of a port-added make** (`CarUninstall`): the three rows, their strings and the rule go
  with the make when no car is left under it.

## Checked

- Local create and DLC trial create of a make, port of BOW_NemesisEXR_12 (FH2) into a trial:
  rows, 18 languages (DE, FR checked), rule, pack join into `31.xml`, the game's XML untouched in the
  DLC case, add/remove of the rule gives the original file byte for byte.
- **Not checked in game** (Xenia): that the title shows and unlocks; that a pack's extra rules file is
  read for a make of its own (the official pack does it for Ultima).
- Not tested end to end: the uninstall of a make with titles.

## Livery decals (`MakeDecals.cs`)

Checked on the official packs in Xenia's `OG_GAME` folder (STFS): Ultima (offer 30), MG, Viper, Ascari,
Hudson each add a decal. What a pack holds for a make:

| Where | What |
|---|---|
| `Livery_Decals` | one row: ID (after 522 for the next one), `Filename` `GAME:\Media\Livery\Decals\decalNNN.carbin` (NNN = ID + 1), `DisplayName` (`Livery_Decals.str`, `IDS_DisplayName_<2000+ID>`), `AlphaSort` = the make's name. |
| `Livery_DecalsSortOrder` | 46 rows for the make: Sequence 1 = its own decal, the rest the common ones (Turn 10, FM4, FM3, FM2, FM, Top Gear, MSN, sponsors, XBOX) = a copy of another make's rows. |
| `Media\Livery\decals\decalNNN.carbin` | loose in the pack (`Livery.zip` mounts at `Media\Livery`, priority 95; a file the zip lacks comes from the pack). |

A decal carbin is always 350075 bytes: the same header and trailer (a quad) around a 256x256 texture,
R, G, B, A (straight alpha), Xbox 360 tiled, from 0x154: base level, mips 128, 64, 32 (tiled), then a
tail of smaller mips (to 0x556C8). Forza Studio builds one on an official decal's header: the logo
image for a make created by `CarMake.Create`, the source game's own decal for a make the port adds
(FH2's Bowler decal is 8 bytes longer in its header; the texture decodes the same). The tail mips are
filled with the average texel (their packed layout is not decoded); the base, 128, 64 and 32 levels are
exact. The common rows come from the make with the most rows (make 1).

The port adds decals only when `PortMake` kept no Sort row (all of the source's name decals FM4 lacks);
FM3's Porsche keeps its own rows. Uninstalling a port-added make removes its own decal (row, name,
carbin) when no other make shows it.

Checked: Bowler created locally and in a DLC trial (carbin 350075 bytes, header and trailer identical to
Turn 10's, texture decoded), pack join (`Livery\decals\decal524.carbin`, `31.xml`), Bowler ported from
FH2 (decoded decal = FH2's). Not checked in game: that the decal shows in the livery editor and the
make's decal list; the tail mips.
