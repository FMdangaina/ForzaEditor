# Current issues

Problems seen in game that are not explained or not checked yet. When the cause is found and
fixed, the issue moves to its ROADMAP item and is removed from here.

## 1. FH1/FH2 ports do not deform in a crash (open, ROADMAP 1)

- Seen 2026-10-17 in DEV: FH1/FH2 ports keep their shape after a crash; FM3 ports deform like FM4
  cars. Deformable flag: OK on the FH body parts after the port.
- Still to note: which cars, and whether they were ported before or after commit 80f5104 (FM4
  damage values in `carattribs.xml` for soft source cars).
- Per-vertex damage words, TOY_SupraRZ_98 in four games (`fm4dev\Deform\dmgcmp.exe`, outputs
  `fm3.txt`, `fh1.txt`, `fh2.txt`, `pairs_fh2.tsv`):
  - FM3 = FM4 on 93% of matched vertices.
  - FH1 = FH2, **a different format from FM4**: three signed fields 11:11:10, a short model-space
    vector (median length 43). FM4's word, same split, is a near-unit direction at full scale
    (median length 614); |FM4 y| follows |FH vector| (corr 0.81). Not decoded yet.
  - Likely cause: FM4 reads FH's short vectors as its own format.
- Next, one of: decode the FM4 word and convert FH words in CarPort; or take each vertex's word
  from the nearest vertex of the same car in FM4/FM3, else from a similar FM4 car.

## 2. FM3 ports: drag tyres (fixed 2026-10-16, in-game check pending, ROADMAP 9)

- FM3's `List_TireCompound` stops at 8; FM4 adds 9 (`9_DragTire`). `CarPort.DragTires()` adds
  FM4's Level 4 row (`TireCompoundID 9, Price 10000`) to every ported car without compound 9.
- X5 row inserted in DEV (backup `bin\Debug\bak\2026-10-16_drag_bak_DEV_gamedb.slt`).
- To close: check in game that the X5 offers the Drag compound.

## 3. The tyre looks the same with every compound (on hold)

- Not a port bug: FM4's own cars do the same in Xenia. The user checks on a real Xbox 360 later;
  if the tyre changes there, look at what picks `Media\wheels\TireA/B/C.zip` (`List_Wheels`,
  `WheelLayouts`, `Combo_TireBrandCompound`).

## 4. Strange ambient occlusion on some parts (probably closed)

- Seen 2026-10-16 on BMW_X5_09 ported from FM3. Most likely the X5's dark patches, which the
  user saw in FM3 too (FM3's own vertex bake, ROADMAP 9 3d.1). Remove this entry unless it shows
  up on another port; then note the car, the parts and a screenshot.
