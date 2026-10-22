# DB rules test

Console tests against a real FM4 DVD1 folder (`Media\db\gamedb.slt`, `StringTables`, a car zip, `Textures.zip`). Compile with the .NET Framework 4 `csc.exe`, `/platform:x86`,
referencing a build of Forza Studio (`/r:ForzaStudio.exe`), with `xcompress.dll` next to the exe.

- `sqltest.cs <gamedb.slt>`: plans a set of statements through `SqlGuard` (a duplicate key, a delete that leaves ids dangling, a renumbered car, a wrong ModelId... are refused).
- `duptest.cs <FM4 root> <work folder> game|pack`: Duplicate Car on a staged copy, into the game folder or into a DLC pack (offer 91, Xenia content folder in the work folder).
