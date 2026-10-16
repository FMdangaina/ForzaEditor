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
