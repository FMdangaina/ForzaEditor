# Driver position and hands (ROADMAP 6.2), first pass 2026-10-22

Data: the FM4 and FH1 Supra (`TOY_SupraRZ_98`) and, from the dev sources (`fm4_dev\samples`), the Supra's and the 918 Spyder's `Locators.xml`. Only one car has both the cockpit file and the locators, so what follows are observations, not a rule.

## Where the driver is stored (game files)

| Where | What |
|---|---|
| `_cockpit.carbin` header 0x1C8-0x2FF | the rig, big-endian floats, model axes (decoded earlier, `CarPort.CockpitRig`): six hand grips (3 radii x +X / -X), wheel pivot (0x228), wheel axis (0x238), a point at 0x248, a point at 0x258 (shifter side), four records of point / direction / value from 0x270 |
| `carattribs.xml` | `DriverHandsOffset` (x y z), the `Driver` element: `ButtOffset`, `ButtOffsetLOD0`, `LeftHandOffset`, `RightHandOffset` (+ LOD0), `ShifterOffset`, `HandBrakeOffset`, `IKGuideTranslation` (x y z each), `BodyLean`, `BodyScale`, `HasPaddleShifter`, `HasNoHandBrake` |
| DB | `CarPartPositions` part 9 (steering wheel point, the cockpit camera) and `CameraOverrides` |

On the Supra every `Driver` offset is 0 except `ButtOffset.z = -0.1`; `DriverHandsOffset` is 0.338, 0.203, 0.272 (FM4) and 0.340, 0.174, 0.287 (FH1).

## FH carries the same rig

The 312 bytes 0x1C8-0x2FF of the FM4 cockpit are **identical** to the FH1 cockpit's, found 4 bytes later (the FH header word that differs); the port therefore gets the Supra's rig as it is (the FM2 and traffic cases are the ones `CockpitRig` rebuilds).

## What the points are, against the source scene's `Locators.xml`

The scene locators (dev files, not in the game zips) are in the car's frame (Z forward); the cockpit file's frame is the same with Z shifted by about -0.27 m (and Z pointing to the rear in the carbin).

| Rig value (cockpit file) | Locator | Match |
|---|---|---|
| wheel pivot 0.339, 0.602, -0.144 | `carLocator_steeringWheel` 0.338, 0.604, 0.121 | x, y within 2 mm |
| point 0x258 -0.031, 0.369, -0.140 | `stickShift` -0.033, 0.371, 0.135 | x, y within 2 mm: **the shifter** |
| point 0x248 0.435, 0.165, 0.322 | `Handbrake` 0.056, 0.273, 0.134 | no match: not the hand brake (? the right-hand drive's door side) |
| records 1-3 (0.22 / 0.34 / 0.49, 0.6, -0.38), direction (0.008, 0.193, -0.981) | none | three points along the dash looking forward, value 4.1 / 4.0 / 1.4 (? gauge cluster or eye points) |

The seats: `seatR` origin 0.377, 0.458, 0.307 in the cockpit file; the scene has `carLocator_seatCamRF` at x 0.380 (the same x), y 0.829, z -0.536. So the seat camera sits on the seat's x, 0.37 m above its origin.

## Editing by hand

The model view has a **DRIVER** tab (like EXHAUST) on a car's `_cockpit.carbin`: lists the header points and the carattribs offsets, three number boxes, crosses and lines in the view, Apply writes the cockpit file and `carattribs.xml` into the archive (pending until File > Save). Names with "?" are guesses. Not checked in game; what each carattribs offset moves is the open part: change one value at a time and look (butt, hands, shifter, hand brake).

## Open

- Whether `ButtOffset` / `BodyLean` / `BodyScale` move the whole seated body, and in which frame (the Supra has only `ButtOffset.z = -0.1`).
- What the records at 0x270 and the point at 0x248 drive.
- The rule from the seat to the body position: a comparison across cars needs the cockpit files and `Locators.xml` of more than one car (the dev folders have no cockpit file for the 918).
