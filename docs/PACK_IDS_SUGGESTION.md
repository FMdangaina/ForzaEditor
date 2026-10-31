# DLC pack window: suggested Offer id and First car id (2026-10-31)

## Problem
The "DLC PACK (XENIA / XBOX 360)" panel of the port window (PortMode.Dlc) showed Offer id 400 and
First car id 8550 with an empty pack. They were old values saved in ForzaStudio.ini
(`CarPort.PackOffer`, `CarPort.PackFirstCar`), not computed ones. `SuggestPackIds` only recomputed
the first car when it was empty or <= 1637 (FM4 max Data_Car), and the offer only when it was 0.

## Rule (user, 2026-10-31)
- Suggested value = max of everything installed + 1, for both ids.
- Whatever the user types by hand is kept: the suggestion never overwrites it.

## Changes
- `CarPortForm.SuggestPackIds(bool force)`: force = recompute both even if a value is present.
  - Shown and RestorePack: `force = pack is empty && user has not typed an id` (`packIdsTyped`).
  - New Pack: resets `packIdsTyped` and always recomputes.
  - `packIdsTyped` is set when the user edits either box while it has focus.
- `DlcPackage.FreeOffer`: no more floor of 90. Max of FM4 + official packs (GameIdLimits), the game
  DB ContentOffers, `.puboffer` files in Xenia content, and the ContentOffers of every installed
  package's merge DB (`DbIds.PackDatabases`, which reads STFS packages too), then +1.
- First car id: `DbIds.FirstPackCar` already did max(Data_Car) over GameIdLimits, game DB and pack
  merge DBs, +1 (unchanged).

## Status
Edited, NOT compiled, NOT tested. To check: open the pack window with an empty pack (values
should be max+1, not 400/8550), type a value and reopen (kept), New Pack (recomputed).

## Open
- A pack offer can now be lower than 91 (e.g. 40). If a high floor is wanted to stay apart from
  official packs, say so.
- Other callers of `FreeOffer` / `FirstPackCar` (Add To Package, install wizard) not reviewed.
