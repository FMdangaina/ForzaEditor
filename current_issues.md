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

## 5. FM3 port with a fully upgraded engine: no engine sound (fixed, checked in game; ROADMAP 3.9)

- 2026-10-17: the user confirms the X5 has sound fully upgraded. Remove this entry at the next
  cleanup; the rule is in `CarPort.CompleteEngineTuning`.

- Seen 2026-10-17 by the user: BMW_X5_09 (FM3 port), every upgrade at the top level, zero sound.
- Not the cause: the harmonic tunings. CarPort already takes every level (L0-L3), and FM3 and FM4
  harmonic tunings have the same elements.
- **Likely cause: FM3's engine tuning is an older, shorter format, and CarPort copies it as is**
  (DEV's `BMW_X5_09_ET.xml` = FM3's, same md5). Field census on all files
  (`fm4dev\FM3_FM4\tools\etfields.py`, 490 FM3 vs 501 FM4):
  - FM3 lacks blocks every FM4 file has: `HarmonicTunings` (EmissionGroup0-2 DSP), `FocusPEQ`,
    `Distortion`, `Compressor`, `ShiftVolumeScalar`, `HeavyBraking`, `TrashDSP`,
    `ExhaustReflection`, and `DSPScalar` (PEQ/TrashBand maxima, `DSPMaxScalar`, `TrashVolCompMin`).
  - FM3 also lacks the `EngineSettings` attributes `num_cylinders`, `blowoffL1-3Level/Choice` and
    `opencockpit`, and `VolumeAdjustments.ElectricMotorVolume`.
  - `DSPScalar` looks like the DSP scaled by upgrade level: no effect when stock, and undefined
    values when fully upgraded.
- 160 of FM3's 491 engine tunings exist only in FM3: every FM3-only car is exposed.
- Test installed in DEV 2026-10-17 (waiting for the user): the X5's FM3 tuning plus the missing
  FM4 attributes and blocks from BMW_X5M_11, `num_cylinders` 8 (`tools\etpatch.py`,
  `tools\zipput.exe`, `tools\putzip.exe`). Backup `bin\Debug\bak\2026-10-17_04-41-02_bak_DEV_*`.
- **Confirmed in game 2026-10-17**: with the patched tuning the fully upgraded X5 has sound.
- Fixed in code: `CarPort.CompleteEngineTuning` completes a source tuning that lacks FM4's DSP
  blocks. FM3 values are kept; the missing attributes and blocks come from the donor's engine, or
  from the FM4 tuning the user picks in the review ("sound" issue). num_cylinders comes from the
  engine's `List_Cylinders.Number`. The trial on the X5 writes the same tuning as the one tested in
  game (only line endings differ, CRLF as in FM3).
- To close: an in-game check of a car ported with the new code.

## 4. Strange ambient occlusion on some parts (probably closed)

- Seen 2026-10-16 on BMW_X5_09 ported from FM3. Most likely the X5's dark patches, which the
  user saw in FM3 too (FM3's own vertex bake, ROADMAP 9 3d.1). Remove this entry unless it shows
  up on another port; then note the car, the parts and a screenshot.
