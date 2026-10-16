# Current issues

Problems seen in game that are not explained yet. When the cause is found, the issue moves to its
ROADMAP item (or becomes one) and is removed from here.

## 1. Strange ambient occlusion on some car parts

- Seen: 2026-10-16, in game (DEV), after the cockpit camera check of BMW_X5_09 ported from FM3.
- What it looks like: some car parts look like they have the wrong ambient occlusion (dark or
  light patches that don't match the shape). Screenshot: still to add.
- Which cars and parts: still to note (only the FM3 port, or other ports too? Exterior, cockpit or
  both?).
- Leads to check:
  - FM4 vertex bytes 24..31 (the "tail", 4 × S16). FM3 has the same 4 values; FM4 has them equal
    on the interior and slightly rebaked on the exterior. If this is baked occlusion, the FM3
    values could be wrong for FM4 on some parts.
  - Parts where the converter has no FM3 tail and generates one with the ReplaceGeometry formula
    (brakes, `t0 = 0.50 + 0.33*clamp((n.Y+1)/1.25)`): that is a guess, not baked AO.
  - Tangent frame rebuilt from the normal and the FM3 frame Z axis.
  - Maybe related to ROADMAP 9 3d.1 (textures and shaders too bright).

## 2. FM3 ports: no drag tires (fixed 2026-10-16, in-game check pending)

- Fix: `CarPort.DragTires()` adds FM4's drag row to every ported car without compound 9 (FH1 and
  FH2 also give drag tires to no car). X5 row inserted in DEV (backup
  `bin\Debug\bak\2026-10-16_drag_bak_DEV_gamedb.slt`). When checked in game: move to ROADMAP and
  remove from here.

- Seen: 2026-10-16, BMW_X5_09 ported from FM3: the Drag tire compound is missing from the
  upgrades.
- Cause: FM3's `List_TireCompound` stops at 8 (`8_RacingSlick`); FM4 adds 9 (`9_DragTire`). The
  port copies FM3's `List_UpgradeTireCompound` rows as they are, so the car gets Levels 0-3 only.
- FM4: 502 of 503 cars have a Level 4 row, always `TireCompoundID 9, Price 10000,
  ManufacturerID 0` (Id = car * 1000 + 4).
- Fix: in `CarPort` (`motorsport3` only), add that Level 4 row when the car has no compound 9.
  For the X5 in DEV: insert `(1079004, 1079, 4, 0, 9, 0, 10000, 0.0, 1.0, 1.0)`.

## 3. The tire looks the same with every compound (not a port bug, on hold)

- Seen: 2026-10-16, BMW_X5_09 ported from FM3, upgrade screen: Stock, Sport and Race compound
  show the same tire model and texture.
- **Not a port bug (user, 2026-10-16):** the same happens on FM4's own cars and on FH1, FH2 and
  FM3 ports. Probably Xenia. Next: the user checks on real hardware (Xbox 360) later; if the tire
  changes there, come back to the leads below.
- Known so far: the car's wheel zip holds only the rim carbin (`wheels\BMW_X5_09.zip`, as the
  FM4 X5M's). FM4 and FM3 both have shared tire models in `Media\wheels\TireA/B/C.zip` (plus a few
  car-specific `Tire[ABC]<car>.zip` and `_SLOD`), and FM4 drag wheels (`*_Drag.zip`).
- To check first: does an FM4 car (e.g. BMW_X5M_11) change its tire look between compounds in
  the same screen? If not, this is the game's normal behaviour, not a port bug.
- If it does: find what picks TireA/B/C (compound, tire width, rim size?) in the DB
  (`List_Wheels`, `WheelLayouts`, `Combo_TireBrandCompound`) and compare the X5's rows with the
  X5M's.
