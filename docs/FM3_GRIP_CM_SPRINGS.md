# FM3 / FM2 ports grip too high: centre of mass and springs (research 2026-10-30)

User report: cars ported from FM3 seem to have too much grip. Read-only comparison of the game databases, no code changed yet.
Tools: `E:\DEV\fm4dev\GripCmp` (x86 harnesses, build with v4.0 csc against `bin\Debug\ForzaStudioEVO.exe`):
`cmcmp` (10 cars in all games), `rh` (CM vs ride height), `cm2` (FM2), `spr` .. `spr7` (springs). Older: `Traffic\tools\gripcmp`,
output `grip_fm3_fm4.txt`.

## What is NOT the cause
- Tyre compounds: the port keeps FM4's `List_TireCompound` rows (FM3 lateral scale 1.25-1.5 vs FM4 1.2-1.35), so the physics is FM4's.
- Tyre widths, mass, brake friction are the same as FM4 for the same car.
- FM4, FH1 and FH2 have identical CM / spring values for the same car (47 cars common to all four). Only FM3 (and FM2) differ.

## Centre of mass height (`CMHeight`, stock row of the weight table)
- FM3 is about 0.17 m lower than FM4 on all 315 common cars (FM3 median 0.28, FM4 0.47).
- The difference is NOT a ratio (FM4/FM3 median 1.6, sd 0.30) but almost the stock ride height:
  **FM4 CMHeight = FM3 CMHeight + stock ride height**, residual median +0.009 m, sd 0.02 m, 138/315 within 1 cm.
- Outliers (40 cars > 3 cm): old classics (Chevelle 70, Corvette Stingray 67, Camaro 69, Cobra 427...) that FM4 raised by hand.
- FM2 (188 cars common to FM2/FM3/FM4): FM2 is on the FM3 scale (FM3-FM2 median -0.023, sd 0.045). Half of the 310 FM2 cars hold the
  fallback value 0.30. Same rule works but is less precise: FM4-FM2-rideH median -0.007, sd 0.049. The ride height to use is the one the
  ported car really has (FM2's differs from FM4's on some cars, e.g. E-Type 0.242 vs 0.185). Ride height itself is NOT to be touched.

## Springs (stock `DefSpringRate`, tables `List_SpringDamperPhysics`)
- FM3 and FM4 use the same physics ids (car*1000+level), so the port copies the FM3 rows as they are; FM4 values are softer
  (FM4/FM3 median 0.805, p10..p90 0.68..0.97).
- Per car the ratio is the same front and rear (front/rear ratio unchanged) and takes few fixed values: 0.8052 (71 cars), 0.7476 (42),
  0.8574 (36), 0.9027 (25), 0.8424 (17), 0.9732 (16), 1.0 (14), 0.9411 (13)...
- NOT a function of the FM3 rate, NOT a ride-frequency target (spread per class is the same in both games), mass correlation -0.1.
- Depends on class (corr 0.63) and PI (0.61). Median ratio per FM4 class:

| class | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 |
|---|---|---|---|---|---|---|---|---|---|---|
| ratio | 0.605 | 0.748 | 0.748 | 0.805 | 0.805 | 0.857 | 0.903 | 0.941 | 0.973 | 1.000 |

- Accuracy of the rule "FM4 rate = FM3 rate x class ratio": median error 3.4 %, 166/315 within 5 %, 237/315 within 10 %, p90 16 %
  (constant 0.805: 7.2 % median; linear on PI: 5.1 %). Leftovers are per-car hand edits in FM4.
- Dampers (not analysed further): bump about x1.2, rebound about x0.9 FM4/FM3; anti-roll bars differ in both directions. FM2 springs not analysed.

## Proposed rules (not implemented)
1. FM3 / FM2 port: `CMHeight = source CMHeight + stock ride height of the ported car` (stock row; other levels via their own diffs).
2. FM3 port: scale Def/Min/Max spring rates (front and rear, all physics rows of the car) by the class ratio above, class = the port's PI class.
3. Dampers / anti-roll: decide after the in-game check. Check FM2 springs before applying rule 2 there.
4. In-game test: one FM3 car ported with rule 1 only, then with rule 2, against the same FM4 car.

Open question to the user: also dampers in the first test, or only CM + springs? Also ask whether the grip is too high in corners only
(points to CM/springs) or also under braking/acceleration (points to tyres).

## Update 2026-10-31: everything implemented in CarPort.TuneMotorsport3Handling (FM3 ports only)
Systematic scan of the 315 cars FM3 and FM4 share (harnesses `E:\DEV\fm4dev\GripCmp`: scan, dist, dist2, phys, cam, tp, tp2, cmpft) and of the 4 `_FM3` test ports in the game DB.
Exact rules (all applied):
- Data_Car steering: SteerMaxAngle x 1.2 (35->42 in 275 cars, 30->36, 25->30; 2 cars 35->36); SteerMaxAngVelTurning 70 -> 105, SteerMaxAngVelStraighten and SteerAngVelCountersteer 140 -> 105 (315/315).
- List_UpgradeBrakes: ReleasePointABSRearFullSteer 1 -> 0.75 (315/315); GameFrictionScaleBraking 1 -> 1, 1.01 -> 1.002, 1.03 -> 1.005, 1.05 -> 1.01 (315/315).
- List_SpringDamperPhysics camber change: independent axles (SuspensionPhysicsTypeID 0) with 0 / 0 in FM3 -> 0.25 / 0.25 in FM4 (89/89 axles); solid axles stay 0.
- Dampers: rebound = sqrt(spring class ratio), bump = rebound x 1.333 (bump {1.03,1.153,1.153,1.196,1.196,1.235,1.267,1.293,1.315,1.333}, rebound {.773,.865,.865,.897,.897,.926,.95,.97,.987,1}).
- Anti-roll bars (classes 0-6): front {.844,1.044,1.044,1.124,1.124,1.197,1.261}, rear {.839,.851,.851,.916,.916,.976,1.027}.
- Bug fixed: Min/Max spring, damper and sway are the tuning slider limits (FM4 keeps 20/150, 1/12, 1/40 on the adjustable rows), so they scale only where they equal the default (fixed parts).
NOT derivable (FM4 hand edits on a minority, mostly CarTypeID 3 race cars): BodyAeroForwardDownforce Front/Rear (272/315 equal), GameTorqueScale (247/315 equal), GameDragScale (252/315 equal), the stock tyre of cars whose FM3 stock is level 1 compound 6 (FM4 gives 5 to 35 of them and 6 to 34, no rule by class or PI; the port copies FM3 faithfully).
Not rules: PI, Sim* statistics and ratings are stored numbers in FM4 (the port maps them with a line fit, errors 0.2-0.4 on the bars, PI +-0.01-0.03).
FM2 still only has the CMHeight rule (not applied yet), its other ratios are not clean (spring x0.6-1.5 by class).
Trial check (BMW_X5_09, trial2): all rules written, limits kept.
