# .gr2 (Granny 2) animations: FM4 vs FH (ROADMAP 3.2), first pass 2026-10-22

Scripts: `tools/GrannyCompare/gr2sections.py` (header and section table), `gr2decompress.py` (decompresses the sections with the `granny2_x64.dll` that ships with ForzaTechStudio, `GrannyDecompressData`).

## What is on this machine

60 `.gr2` files, **all little endian** (magic `e59b495e...`, file version `07000000` at 0x20): 30 of the FM4 Supra (`fm4_dev\samples\TOY_SupraRZ_98`), 28 of a POR 918 Spyder, 2 of the FH demo car DOD_ChargerFast7_70 (`Forza Studio 4.6 source and demo files`). They are the PC / tool versions. **The big-endian console files FH2 writes (`00000007` at 0x20, the form `CarPort` measures on the MP4-12C) are not here**, and the Supra zips of FH1 / FH2 (`TOY_SupraRZ_98-1/-2.zip`) hold no `.gr2` at all. The big-endian against little-endian comparison needs one FH2 car zip with animations and the FM4 zip of the same car.

## Header (Granny file v7, all files)

`0x10` header size 0x1C8, `0x14` format 0, `0x20` version 7, `0x24` file size, `0x28` CRC, `0x2C` section table offset (0x48), `0x30` 8 sections, `0x34` type ref (section 6), `0x3C` root ref, `0x44` tag. Section entry = 11 u32: compression, data offset, data size, expanded size, alignment, first16bit, first8bit, relocations (offset, count), marshallings (offset, count). Section 0 = data and strings, section 6 = type definitions.

## FM4 (30 Supra animation files) against the FH demo file

| | FM4 | FH demo car |
|---|---|---|
| Tag at 0x44 | `0x80000038` (30 of 30) | `0x80000033` |
| Section compression | 0 (none) in every section, 30 of 30 | 2 (Oodle1) on the data and type sections: 3984 B stored, 18436 expanded |
| Exporter | Granny Standard Exporter 2.8.40.0, 3ds Max | the same, 3ds Max 13 |
| Curve types | D3Constant32f, D4nK16uC15u, DaIdentity, D4nK8uC7u, D3I1K16uC16u, D3I1K8uC8u | D3Constant32f, D4Constant32f, D4nK16uC15u, D4nK8uC7u, DaIdentity |
| Sound | `AudioEvent:/Master/Cars/Autovista/AV_HOODOPEN` ... inside each file | none (FH keeps the events in `caranimationevents.xml`) |
| Type section | 8756 B in 11 of the Supra files (8976 B in 3, 8536 B in 2: same schema, other curve types) | 10692 B expanded |

The FH demo file is not an animation: it is the car's scene (the 3ds Max scene summary, `COL_Sphere*`, `anchor_*`, `body_LOD0..5` ...), so it says what an FH `.gr2` can hold, not how an FH door animation is built. The FM4 files are one animation each (`doorLF_open`, `hood_close`, `wiperFront` ...) with the bone names (`boneHood`, `boneDoorLF` ...).

## Open

- The FH2 console files: endianness (header, relocations and marshalling tables are what a converter has to swap), compression (Oodle1 in the demo file; does the console FH2 file compress too?), the tag 0x33 against 0x38.
- Whether FM4's Granny reads a big-endian or a compressed file as it is: if it does, no converter is needed and the port can copy the FH2 files (`CarPort` leaves them out today because FM4 reads little-endian ones, measured on the MP4-12C). Not tried in game.
- FM4's sound events live in the animation file; an FH animation has no `AudioEvent` string, so a copied animation would be silent.
