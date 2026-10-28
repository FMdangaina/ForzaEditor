# Digital dash: copy between cars and UV mapping of the `digital_gauge` mesh (2026-10-28/29)

## FM3 -> FM4 port: where the dash comes from
- FM3 links it by the database, not by name: `Data_Car.GaugeID` -> `List_DigitalGauges.MediaName` -> `Media\DigitalGauges\<MediaName>.zip` (+ images in `Media\UI\Textures.zip`).
- FM4 keeps it in the car zip, `digitalgauge/dash_<car MediaName>.fbf/.bgf/.bsg` + `digitalgauge/<image>.xds`, `Data_Car.GaugeID = -1`.
- The port already does this (`CarPort.AddMotorsport3Dash`, `ConvertMotorsport3Dash`): names rewritten to the car, images converted, missing images taken from FM4 cars' dashes.
- FM3 database: 186 of 415 cars have a gauge, 229 have `GaugeID = -1` (no dash, the port adds none). No gauge is shared by two cars. `CHE_3_CorvetteC5R_04` = gauge 46 (`Dash_CHE_3_CorvetteC5R_04`); `CHE_50_CorvetteC5R_03` = -1, no dash in FM3.

## Archive > Copy Dash From Another Car... (`ArchiveEditorForm.DashCopy.cs`)
Copies `dash_<source>.fbf/.bgf/.bsg` into the open car zip as `dash_<target>`, rewrites the image paths in the .fbf (`GAME:\MEDIA\CARS\<CAR>\DIGITALGAUGE\<NAME>.TGA` -> target car), copies the `.xds` the dash names (kept when the car already has one). Refuses FM3 dashes (port them instead). Reports images not in the source and whether the source name is still inside the .bgf/.bsg. Nothing is written until Save. **In game: not tested.** Open: whether the game accepts labels named after another car (the FM4 C5R dash has `che_3_*_label` images).

## Model > Export UV Map to TGA... (`ArchiveEditorForm.UvMap.cs`)
UV layout (LOD 0, UV set 1) of the chosen materials of all the car's model files (main, lod0, cockpit) as a TGA; materials named dash/digital/gauge/screen/display are pre-checked. Black background, one colour per material, y top-down as in the texture file. The mesh of the dash screen is the material **`digital_gauge`** (in `<car>_cockpit.carbin`, section `interior` or `steering_wheel`).

## Findings: the size of the dash "screen" is not in the files
- .fbf Camera: orthographic, position (0, 0, -600), clip 10..100000, fov 60 (unused in orthographic). Layer: no size. Same values on every dash checked (Brera, C5R, NSX).
- `shadersettings.xml` has nothing for `digital_gauge` and `Shaders\Cars` has no such shader: the render target size is in the game code.
- Dash extents (origin always in the centre) vary: 300x512 (Brera, 8C), 256x256, about 1284 x 520 (many), 1280x720-like (some).
- The UV window of the `digital_gauge` mesh differs per car even for the same dash extents (Brera u 0.415-0.585 / v 0.282-0.719; 8C u 0.409-0.593 / v 0.278-0.724; wide dashes u windows 0.54-0.68). So there is no fixed units-per-UV: the modeller chose the window and the dash was drawn to fill it.

## Next (proposed, not done)
Export the dash itself (Dash view, at its real extent, scale chosen) as a TGA to use as the reference texture in ZModeler, with the mesh's UV rectangle drawn over it. Check in game on the original Brera whether the Background (300x512) fills the screen quad exactly (then the rule is "UV window = dash extent").
