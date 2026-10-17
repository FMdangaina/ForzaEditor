# Forza Studio user manual (beta)

Forza Studio is a modding tool for the Xbox 360 Forza games. It works directly on the game's
files: you open a game folder or one of its `.zip` archives, change what you need, and save.

> **Beta.** Some features are not fully tested in game yet (see
> [Known limitations](#13-known-limitations)). Always work on a copy of your game folder.

## Contents

1. [Supported games](#1-supported-games)
2. [Install](#2-install)
3. [The main window](#3-the-main-window)
4. [Saving, backups and undo](#4-saving-backups-and-undo)
5. [3D models (.carbin)](#5-3d-models-carbin)
6. [Textures (.xds)](#6-textures-xds)
7. [Adding files and new textures](#7-adding-files-and-new-textures)
8. [Text and XML files](#8-text-and-xml-files)
9. [Game database (gamedb.slt)](#9-game-database-gamedbslt)
10. [Digital dash (.fbf)](#10-digital-dash-fbf)
11. [Porting cars from Forza Horizon or Forza Motorsport 3 to FM4](#11-porting-cars-from-forza-horizon-or-forza-motorsport-3-to-fm4)
12. [Music](#12-music)
13. [Known limitations](#13-known-limitations)
14. [Other tools](#14-other-tools)
15. [Files Forza Studio writes](#15-files-forza-studio-writes)
16. [Credits and license](#16-credits-and-license)

---

## 1. Supported games

| Game | What works |
|---|---|
| Forza Motorsport 4 | Everything: models, textures, XML, database, dash view, music. Target of car ports. |
| Forza Horizon 2 | Models, textures, XML, database, music. Source for car ports. |
| Forza Horizon (1) | Textures, XML, database, music. Source for car ports. |
| Forza Motorsport 3 | Models can be viewed (not edited), textures, database, music. Source for car ports. |
| Forza Motorsport 2 | Archives, textures, model viewing and export, music. Models cannot be imported back. |

The game is detected from its `default.xex`. You need the game **extracted** to a folder, the one
that holds `default.xex` and `Media`.

## 2. Install

**Requirements**

- Windows 10 or 11.
- **.NET Framework 3.5.** Enable it in Control Panel > Programs > *Turn Windows features on or
  off* > *.NET Framework 3.5 (includes .NET 2.0 and 3.0)*.
- **Microsoft XNA Framework Redistributable 3.1**, used by the 3D view.
- **Xenia Canary**, only to play ported cars as DLC packages.
- **`xcompress.dll`, not in the zip.** It comes from the Xbox 360 SDK and is Microsoft's, so it
  cannot be shared. Copy your own next to `ForzaEditor.exe`. It opens and saves the game's
  compressed zips; without it, most game files cannot be read.

**Steps**

1. Unzip the whole Forza Studio folder somewhere you can write to, such as the Desktop. Avoid
   *Program Files*.
2. Copy `xcompress.dll` next to the exe.
3. Run `ForzaEditor.exe`.

Keep every file of the zip next to the exe:

- `texconv.exe` builds textures.
- `vgmstream\` plays the music.
- `xds.dll` (from Forza Studio 4.6) and the hex editor DLL are used by the classic tools.

The program is 32-bit by design.

You can also start it from a command line or a shortcut:

```
ForzaEditor.exe <archive.zip | folder | file> [entry to select]
```

## 3. The main window

The menu bar holds:

- **File**: open, save, close.
- **Archive**: add, extract, replace and revert files of the open folder or archive.
- **Model**: OBJ export and import of the model shown.
- **Texture**: new textures, image export and import of the texture shown.
- **Tools**, **Help** and **◀ Back**.

 The left side shows the tree of
the open folder or archive. The right side shows the selected file.

### Opening things

| Command | Shortcut | What it opens |
|---|---|---|
| File > Open Folder... | Ctrl+Shift+O | A game folder, or any folder inside it. The tree loads as you expand it. **Double-click a `.zip`** in the tree (or Enter, or Archive > Open Selected Archive) to open it. |
| File > Open Archive... | Ctrl+O | One `.zip`, a `gamedb.slt` or a single `.xds`. |

Opening a `gamedb.slt` directly opens the folder it is in, so links to other game files can be
followed.

### What each file shows

| File | View |
|---|---|
| `.carbin`, `*.rmb.bin` | 3D model (cars, wheels and brakes; track models) |
| `.xds`, `.xdc`, `.tga` | Texture |
| `.xml`, `.ini`, `.cfg`, `.txt` | Text editor |
| `.html`, `.htm`, `.rdf` | Read-only text |
| `gamedb.slt` | Game database |
| `.fbf`, `.bgf`, `.bsg` | Digital dash (view and edit) |
| anything else | Name and size. Archive > Extract or Replace Selected File. |

### The tree and the Archive menu

The **Archive** menu works on the file selected in the tree (the same commands are on its right-click menu):

- **Open Selected Archive** opens a `.zip` selected in a game folder (also double-click or Enter).
- **Replace Selected File...** puts any file from disk in its place.
- **Revert Selected File** undoes the pending change. On a file you added it reads **Remove Added File**.
- **Add File...** adds a file (see [section 7](#7-adding-files-and-new-textures)); on the right-click menu it is **Add File Here...**.
- **Extract All...** writes the whole archive to a folder.

The tree marks pending changes: an edited file is **bold** with `*`, and an added file has `+`.

**File > Close** closes the open folder or archive.

### Going back

Many actions jump to another file:

- opening a zip from a folder;
- *Open Car Model*;
- a `GAME:\` link;
- *Go to* in the database.

**◀ Back** (Ctrl+B or Alt+Left) returns to where you were, with the same selection.

## 4. Saving, backups and undo

- **Edits are kept in memory** until **File > Save** (Ctrl+S). The window title shows `*` while
  there are unsaved changes. Closing or opening something else asks whether to save.
- **Archive > Revert Selected File** undoes everything not yet saved on it.
- **Every save makes a backup first.** The version being overwritten is copied to `bak\` next to
  the exe, named `yyyy-MM-dd_HH-mm-ss_bak_<file>`. To undo a save, copy the backup back.
- **Save As...** writes an archive under a new name. It works for zips only.
- When a saved zip is listed in `Media\zipmanifest.xml`, the manifest is updated as well, after
  its own backup. The game needs this to load an edited zip.

> Close Xenia, or the game, before saving files it uses.

## 5. 3D models (.carbin)

Select a `.carbin` to see the model.

### The model tree and the viewport

The model tree on the left lists **LOD > part > material**. Check a node to show it in the view.

The toolbar has these controls (**Model > Export Checked Parts to OBJ...** writes the checked parts as one OBJ):

- **Wireframe**.
- **Camera speed**: slow, normal or fast.
- **FOV**: the field of view of one of the game's cameras: photo mode (45°, the default), bumper
  (53°), chase (54°), cockpit (62°) or hood (66°). Next to it, the current FOV. The wheel changes it
  between 0.5° and 90°, as the game's free camera does.
- **Go to Camera**: see [Cockpit camera](#cockpit-camera-camera-tab).
- The vertex and face count.
- A red warning, **"N material(s) not in FM4"**, when the model has any. Click it (*click to fix*) to select the
  first one.

**Camera**

| Input | Action |
|---|---|
| Drag | Look around |
| Right drag or middle drag | Move |
| Wheel | Zoom (changes the FOV) |
| W A S D, Q E | Move |
| Arrow keys | Look |

**Red materials.** A material that no FM4 car uses is drawn red in the tree, and it may render
black in game. Select it and press **Rename Material...** in the Part tab (or right-click it), then pick an FM4 material from the list. You
can rename it in every part and in every model file of the car (main, lod0, cockpit).

### Cockpit camera (CAMERA tab)

The model view's side panel has three tabs: **Part**, **Texture** and **Camera** (Camera only for car models).

Open a car model from the game folder (FM4): the Camera tab reads the car's cockpit camera from
`GAME:\Media\db\gamedb.slt`. Values are in the database's axes, where Z points forward.

- **Steering wheel position**: the `CarPartPositions` row PartId 9. The game hangs the cockpit
  camera on this point. Without it, the cockpit view sits at 0,0,0 (cars ported from FM3 lack it).
  When it is missing, the tab rebuilds it at once from the cockpit model and shows it in red as
  not saved.
- **Eye offset from the wheel**: `CameraOverrides` CamCockpitOffset X/Y/Z. 0, 0, 0 means the game's
  default (0, 0.21, -0.51, from `Media\Camera\CameraSettings.ini`).
- The view shows the wheel point (yellow cross), the eye (cyan cross) and what it sees (FOV 62°).
- **Wheel From Cockpit Model** computes the wheel point from the `steering_wheel` part of the car's
  `_cockpit.carbin` (within about 1 cm of the game's own points on FM4 cars). It also sets the eye
  offset X to 0, so the eye sits right behind the wheel (FM3 cars often carry a side offset of about
  2 cm; FM4 cars have 0).
- **View From Camera** puts the viewer at the eye, looking forward, with the cockpit FOV.
- **Save to Game Database Now** writes both rows into `gamedb.slt` (missing rows are added) after a
  backup in the `bak` folder. It writes at once, not with File > Save. Close the game first.

The port tool adds the steering wheel point by itself when the source car has none.

**Go to Camera** (model toolbar) moves the viewer to a game camera:

- **Cockpit**: the eye of the Camera tab, FOV 62°.
- The garage, photo mode, livery and upgrade cameras of `Media\UICarTargetCameraPresets.xml`
  (position, direction and FOV as the game stores them; a preset made for this car is used instead
  of the general one).
- The race chase, hood and bumper cameras are not offered: the game builds them from the car's
  box, the `CameraOverrides` offsets and the global settings in `Media\Camera\CameraSettings.ini`
  and `CameraPhysics.xml` (plain text files, editable as such).

### Editing parts (PART tab)

Part editing works on FM4 car models and on Forza Horizon 2 models. Select a part in the tree, or
one of its materials to edit only that material.

- **Move** (metres), **Rotate** (degrees), **Scale** (%). Tick **Same scale on all axes** to scale them
  together.
- **Pivot**: the part's center, the model origin, or a custom point.
- **Also in the car's other model files (main, lod0, cockpit)** keeps the three files in step.
  Leave it on unless you know why you want otherwise.
- The change is previewed live in red. **Apply** keeps it, **Reset** clears it.
- **Focus On Part** centres the camera on the part. **Show only this part** hides the rest.
- **Merge...** joins this part into the one picked in **Merge this part into**. Materials with the same name are
  joined, and the source part is removed.
- **Duplicate...** copies a part under a new name. A free variant letter is
  suggested, e.g. `bumperRa` → `bumperRb`.
- **Delete...** removes the selected part or material. The **Del** key does the same.

### Parts from other cars and from 3D programs (PART tab)

- **Copy Part From Another Car...** copies a part from another car's zip, with all its LODs and damage
  data:
  - add it as a new part, or replace one of this car's parts;
  - optionally place it at the replaced part's center;
  - optionally use this car's texture regions for its materials.
- **Export OBJ...** and **Import OBJ...** (one part):
  - import an OBJ to replace a part, or to add a new part based on a template;
  - choose which car material each OBJ material becomes;
  - optionally fit the UVs into this car's material regions.

### Whole model as an OBJ work folder

**Model > Export Model to OBJ Folder...** writes the whole car to a folder:

- one folder per model file, e.g. `toy_supra_98`, `toy_supra_98_lod0`;
- inside it, `lod0`, `lod1`, …;
- one OBJ per part in each LOD folder.

Edit the OBJs in Blender or any 3D program, then **Model > Import Model from OBJ Folder...**. Import
works like this:

- only the OBJs you changed are rebuilt;
- a deleted OBJ removes that LOD of the part;
- a new OBJ file name adds a new part, and you choose a template part for its materials and
  damage data;
- before anything changes, you see the plan; afterwards, a report.

Forza Motorsport 2 models can be exported but not imported back.

### Texture preview

The **Texture** tab shows a texture on the selected material. It changes only what you
see: the model file is not touched.

- Pick an `.xds` from the car's zip, or **Texture From File...** for one on disk.
- Choose the UV set.
- The choice is remembered per material in `materials.txt`.

## 6. Textures (.xds)

Select an `.xds` to view it.

**Viewing**

- The side panel shows the size, format, color space and mip levels.
- **Mip level** chooses which mip to show.
- **Channels**: RGB + Alpha, RGB only, or Alpha only.

Both commands below are in the side panel and in the **Texture** menu.

**Export Image...** saves a PNG, or a 32-bit TGA.

**Import Image...** replaces the texture:

- Accepted files: PNG, TGA, BMP, TIF or DDS.
- The image **must be the same size** as the texture.
- It is compressed to the texture's own format, and the mips are rebuilt.
- The change is written when you save.

## 7. Adding files and new textures

**Archive > Add File...** (Ctrl+Shift+A), or right-click > **Add File Here...**, adds a file to the
archive. Choose:

- the folder in the archive;
- the name;
- the extension.

An image (PNG, JPG, BMP, TIF) becomes an `.xds` texture by default. Any other file is copied as it
is. For textures you also choose:

| Option | Choices |
|---|---|
| Format | **DXT1**: color, no alpha or 1-bit alpha. **DXT5**: color + alpha. **DXN**: normal maps. **DXT5A**: one channel. **A8R8G8B8**: uncompressed. |
| Gamma color | On for color maps. Off for normal maps and masks. |
| Mip levels | The full chain, or none. |

**Texture > New Texture from Image...** makes a standalone `.xds` file with the same options.

## 8. Text and XML files

Text and XML files open in an editor.

| Key or button | Action |
|---|---|
| **Apply** (Ctrl+Enter) | Keep your edits (they are saved with File > Save). |
| **Discard Edits** | Drop the edits not yet applied. |
| **Check XML** | Show whether the file is well formed. If not, it gives the line and column of the error. |
| Ctrl+F, F3 | Find, find next. |

- The file keeps its original encoding, BOM and line endings.
- Applying broken XML asks for confirmation first, because the game may refuse the file.

## 9. Game database (gamedb.slt)

Opening `Media\db\gamedb.slt` shows the database. Edits go into the database at once and are
written with **File > Save**, with a backup.

### Sections

The section buttons are **Cars | Wheels | Tracks | Events | Tables**.

- **Search** filters the list on every column.
- **Text** chooses the language used for names (English the first time; your choice is remembered).

**Cars.** The header shows the thumbnail and the make logo, with two buttons:

- **Open Car Model** opens the car's zip.
- **Stock Wheel** jumps to its wheel.

The tabs:

- **Car**, **Body**, **Upgrades**, **Colors**, **Other** hold the car's rows.
- **Engine**: choose the engine, or **Add Engine Swap...**. A red line means the engine is
  *shared by N cars*: editing it changes all of them.
- **Power**:
  - every upgrade part and its level, with its torque factor;
  - a torque/power graph. Tick **Edit curve** and drag the red line to reshape it;
  - **New Level** adds an upgrade level.

**Wheels.** **Open Wheel Model**, and the **Used by** tab.

**Tracks.** Track data, and races, grid and AI.

**Events.** The event's data, races and participants. The buttons:

- **New Event (copy of this)**. The copy still uses the original's name and description texts.
- **Add Race...**
- **Add Competitor...**

**Tables.** Any table as a raw grid:

- **Duplicate Row** and **Delete Row**.
- A **Filter** that takes an SQL WHERE condition, e.g. `MediaName LIKE '%supra%'`.
- Id cells are drop-downs, color cells have a picker, and `GAME:\` paths open with a double-click.

### Tools

- **Right-click a field**, or **Go to / Used by** in a table's toolbar, for these commands:
  - **Go to** the row it points at;
  - **Used by** lists the rows pointing here;
  - **Open Linked File** (a `GAME:\` path; also in the top toolbar).
- **Check References** lists ids that point at missing rows. Double-click a line to jump to the
  rows.
- **Changes** counts your edits, and its drop-down lists the last ones.
- **Delete Row** warns when other rows still point at the row.

## 10. Digital dash (.fbf)

Select a dash file (`.fbf`, `.bgf` or `.bsg`) to see the car's digital dash:

- the element tree on the left;
- a preview in the middle;
- the properties on the right.

On the preview, the wheel zooms, a drag pans, a click selects an element and a double-click fits
the dash to the view. **Outlines** shows the element borders.

Select an element to edit it in the panel: position, rotation, scale, pivot, opacity, color, text,
font and texture. Each value is changed where the dash already keeps it, so nothing is added to the
files; a value the element does not have is greyed out. Edits are pending until **File > Save**
writes the car's zip.

## 11. Porting cars from Forza Horizon or Forza Motorsport 3 to FM4

**Tools > Port Car From Another Forza...** converts a Forza Horizon 1, Forza Horizon 2 or Forza
Motorsport 3 car into FM4. A port includes:

- the model, wheels and brakes;
- textures and dash;
- the database rows;
- names in every language;
- the engine sound;
- the thumbnail.

From Forza Motorsport 3, the digital dash (kept by FM3 in `Media\DigitalGauges`) goes into the car
zip with its textures, as FM4 keeps it. The Porsches need a make FM4 lacks: the port adds it (name, logo, profile and livery
decals).

### Setting up

1. Set the **source** folder (FH, FH2 or FM3) and the **Forza Motorsport 4** folder: the folders
   with `default.xex` and `Media`. Press **Load Cars**.
2. The list shows every car with its PI and class in both games.
   - **Only cars FM4 lacks** hides the cars FM4 already has.
   - Dimmed rows cannot be ported. The **In FM4** column says why.
3. Select a car and choose its **FM4 template car** (the donor): the FM4 car used as a template for the data FM4
   needs. The ★ cars are the closest matches.

### Way 1: DLC package for Xenia (recommended)

The game's own files are not touched. The cars live in a package you can remove at any time.

1. Press **Add Selected Car** for every car you want in the pack.
2. Give the pack a **Name**: it is the name the game and Xenia show.
3. Press **Trial Pack**. Every car is converted into a temporary folder; nothing is installed.
4. Check the **Review** column. Select a car in the pack to see its review in the **Review** tab,
   and pick a fix for each line, or leave it as is. The usual lines are:
   - a value pointing at data FM4 lacks;
   - a material FM4 renders black.

   If you change fixes, run **Trial Pack** again.
5. Set **Xenia's folder** (the one with `xenia_canary.exe`).
6. **Close Xenia**, then press **Build DLC Package**. The package goes straight into Xenia's
   content folder. Rebuilding a pack replaces it, after a confirmation.
7. Start FM4 in Xenia. The cars are listed under their makes, like any other car.

The other buttons:

- **Remove** takes a car out of the pack.
- **New Pack** starts a new empty pack.
- **Remove Pack** deletes the built package from Xenia.

**Important**

- **Put all your cars in one pack.** With two Forza Studio packs installed, only one shows its car
  names.
- Use an FM4 folder where these cars were **never installed with Install In FM4**. The game loads
  the old installed copy instead of the pack's. Build DLC Package warns you about this.
- A pack trial keeps a copy of a large game file (about 440 MB) in
  `%TEMP%\ForzaStudio\CarPort`. Starting a new pack trial deletes the earlier ones.

### Way 2: Install In FM4

**Trial Run**, then **Install In FM4...**, writes one car into the game's own files:

- the database;
- the car's zips;
- the string tables;
- the sound and texture zips;
- `zipmanifest.xml`.

Every file is backed up to `bak\` first. Use this only if you want the car in the base game.

### If a ported car looks wrong

- The **Report** tab shows the full log. It is also saved as `port_log.txt` inside the converted
  car zip.
- Black parts: run the trial again with the material fixes from the Review. Or open the car in the
  main window and rename the red materials (see [section 5](#5-3d-models-carbin)).
- A bug report should include the car, the donor and `port_log.txt`.

Technical details of the package format are in [DLC_PACKAGES.md](DLC_PACKAGES.md).

## 12. Music

When you open a game folder, Forza Studio plays that game's music in the status bar:

- the FM soundtracks and playlists;
- the Forza Horizon radio stations.

The controls:

- ⏮ ⏸ ⏭ and a volume slider;
- the **♪** menu, to choose a playlist or station, turn on **Shuffle**, choose whether music
  starts when a game folder opens, or **Stop**.

## 13. Known limitations

**Not fully tested in game yet**

- Packs with several cars.
- Material fixes for lights, number plates and fog lamps.
- Models rebuilt from an OBJ work folder.
- **Merge Into**.

**Known problems**

- Tyres may look too narrow on ported cars with unusually wide wheels.
- Only one Forza Studio DLC pack at a time shows its car names.

**Not available yet**

- **FM2 and FM3 models:** FM2 can be exported only; FM3 can be viewed, and converted only by the
  port tool.
- **Automatic LODs.**
- **Profile saves** are signed and encrypted, so they cannot be edited in the main window.

## 14. Other tools

All in the **Tools** menu.

| Command | What it does |
|---|---|
| **Port Car From Another Forza...** | See [Porting cars](#11-porting-cars-from-forza-horizon-or-forza-motorsport-3-to-fm4). |
| **Pay Respects (F)** | The original Forza Studio 4.6 window, kept as a tribute. |

## 15. Files Forza Studio writes

Forza Studio writes only these files:

| Where | What |
|---|---|
| `settings.txt` (next to the exe) | Folders, language, music and port window options |
| `materials.txt` (next to the exe) | Texture previews chosen per material |
| `bak\` (next to the exe) | A copy of every file before it is overwritten |
| `%TEMP%\ForzaStudio\CarPort\` | Port trial folders; safe to delete |
| The files you save | In the game folder you opened |
| `Xenia\content\...` | DLC packages you build |

## 16. Credits and license

- Forza Studio is maintained by **FMdangaina**.
- It is derived from **Forza Studio 4.6** by Mike Davis, with an earlier edit by **JYSB59**.
- The DLC package format was worked out with help from **Jew (@mr.abortion)** and
  **XTD (@xtd_ac)** on Discord.
- Music is played with **vgmstream** (https://github.com/vgmstream/vgmstream, ISC-style license,
  see `vgmstream\COPYING`).
- `texconv.exe` is from Microsoft **DirectXTex** (MIT). `xds.dll` comes from Forza Studio 4.6.

Forza Studio is free software under the **GNU General Public License v3.0**. Forza Studio 4.6 code
is MIT-licensed; see `THIRD-PARTY-NOTICES.md`.

Forza is a trademark of Microsoft. Forza Studio is not affiliated with Microsoft, Turn 10 Studios or
Playground Games. No game data is included: use it only with games you own.
