# Third-party notices

ForzaEditor is licensed under the GNU General Public License v3.0 (see [LICENSE](LICENSE)).
It includes MIT-licensed work, whose notices are kept here as that license requires.

## Forza Studio 4.6

ForzaEditor is derived from the source code of Forza Studio 4.6:

    Forza Studio: Forza Motorsport resource extraction tool.
    Copyright (C) 2012 Mike Davis (http://codeescape.com/)

That code is distributed under the MIT License reproduced below; the original notice is also kept
at the top of each of its source files.

## Binaries

This repository includes the following third-party binaries in `!ForzaStudio release/`.
Both are distributed under the MIT License, reproduced below.

| File | Project | Version | Copyright |
|------|---------|---------|-----------|
| `Be.Windows.Forms.HexBox.dll` | Be.HexEditor (Bernhard Elbl) | 1.4.7 | Copyright (c) 2011 Bernhard Elbl |
| `texconv.exe` | [DirectXTex](https://github.com/microsoft/DirectXTex) | 2026.4.1.1 | Copyright (c) Microsoft Corporation |

## MIT License

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

## Not included

These files are needed at build time or runtime but are not redistributable
(or not under a permissive license), so they are never committed. Place them in
`!ForzaStudio release/` yourself:

| File | Why it is excluded |
|------|--------------------|
| `xdevkit.dll`, `xcompress.dll` | Microsoft Xbox 360 SDK, proprietary |
| `xds.dll` | Unknown license |

Game data (Forza `.zip`, `.carbin`, `.xds`, `.xpr`, gamedb, etc.) is copyrighted
and is never committed either.
