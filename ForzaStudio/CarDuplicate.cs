namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Text.RegularExpressions;

    // Copies a car installed in an FM4 folder under a new MediaName and Id (ROADMAP 18.3). FM4 is the only
    // reference; the rules come from the audit of the base game and its 16 DLC packages (tools/FM4Audit):
    //   Id        the first free car Id above every car of FM4 and its DLC (DbIds.InstallFirstCar), also free
    //             as car * 1000 in the car body rows;
    //   database  DbIds.RenumberCar on a copy of the game database moves the car's rows (car columns and the
    //             keys car * 1000 + n) to the new Id; the rows that are new in that copy are inserted. Rows
    //             that only point at the car (an event's car, a showroom challenge) are not copied;
    //   strings   DisplayName / ModelShort get a key of their own in Data_Car.str of every language, a key no
    //             FM4 package uses (GameStringKeys), named IDS_DisplayName_<Id> / IDS_ModelShort_<Id>;
    //   files     the car zip under the new name (every entry name and the text files, case kept), the
    //             thumbnail Thumbnail_<Id>.xds in the folder of the source's, zipmanifest.xml lines.
    // The engine, drivetrain, wheel and the other shared rows stay shared with the source car; a wizard
    // step gives the copy its own engine. Plan() reports without writing, Run() does it, every file
    // backed up first.
    public class CarDuplicate
    {
        public string TargetRoot;
        public string SourceMediaName;
        public string NewMediaName;
        public string XeniaContent;                 // installed packs: their car Ids stay free
        public string DisplayName;                  // EN text for every language lacking its own; null = the source's
        public string ModelShort;
        public Dictionary<string, string> DisplayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);     // language (EN, IT...) -> text
        public Dictionary<string, string> ModelShorts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public long? Year;
        public long? MakeId;
        public Dictionary<string, object> Data = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);              // Data_Car column -> value, applied last
        public string BackupFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bak");
        public StringBuilder Report = new StringBuilder();
        public long NewId;
        public EngineSpec Engine;                   // an engine of its own for the copy (CarEngine); null = the source's, shared

        // A DLC pack instead of the game folder: the copy is built on a staged part of the game (the database,
        // the string tables, the source's zip and thumbnail zip), then DlcPackage.BuildFolder turns what
        // changed into the pack in Xenia's content folder. The game folder itself is not touched.
        public string PackContent;                  // Xenia's content folder; null = write into the game folder
        public int PackOffer;                       // the pack's offer number
        public string PackName;                     // the name the pack has in the game's store
        public bool ReplacePack;                    // the offer exists in PackContent: remove it first

        private string stamp;

        public List<string> Plan()
        {
            List<string> plan = this.Execute(false);
            if (this.PackContent != null)
            {
                plan.Insert(0, string.Format(CultureInfo.InvariantCulture, "destination: a new DLC pack \"{0}\" (offer {1}) in {2}; the game folder is not changed", this.PackName, this.PackOffer, this.PackContent));
                this.Report.Insert(0, plan[0] + "\r\n");
            }
            return plan;
        }

        public void Run()
        {
            if (this.PackContent == null)
            {
                this.Execute(true);
                return;
            }
            this.RunPack();
        }

        private void RunPack()
        {
            if (string.IsNullOrEmpty(this.PackName))
            {
                throw new InvalidOperationException("The pack needs a name.");
            }
            if (this.PackOffer <= 0)
            {
                throw new InvalidOperationException("The pack needs an offer number.");
            }
            if (Directory.Exists(DlcPackage.PackagePath(this.PackContent, this.PackOffer)))
            {
                if (!this.ReplacePack)
                {
                    throw new InvalidOperationException("A package with offer " + this.PackOffer + " is already in Xenia's content folder.");
                }
                DlcPackage.Remove(this.PackContent, this.PackOffer);
            }
            string stage = Path.Combine(Path.Combine(Path.GetTempPath(), "ForzaStudio"), "DuplicatePack_" + DateTime.Now.Ticks.ToString(CultureInfo.InvariantCulture));
            try
            {
                this.Stage(stage);
                CarDuplicate inner = new CarDuplicate();
                inner.TargetRoot = stage;
                inner.SourceMediaName = this.SourceMediaName;
                inner.NewMediaName = this.NewMediaName;
                inner.XeniaContent = this.XeniaContent ?? this.PackContent;
                inner.DisplayName = this.DisplayName;
                inner.ModelShort = this.ModelShort;
                inner.Year = this.Year;
                inner.MakeId = this.MakeId;
                foreach (KeyValuePair<string, string> pair in this.DisplayNames)
                {
                    inner.DisplayNames[pair.Key] = pair.Value;
                }
                foreach (KeyValuePair<string, string> pair in this.ModelShorts)
                {
                    inner.ModelShorts[pair.Key] = pair.Value;
                }
                foreach (KeyValuePair<string, object> pair in this.Data)
                {
                    inner.Data[pair.Key] = pair.Value;
                }
                inner.Engine = this.Engine;
                inner.BackupFolder = Path.Combine(stage, "bak");
                inner.Run();
                this.NewId = inner.NewId;
                this.Report.Append(inner.Report.ToString());
                StringBuilder text = new StringBuilder();
                List<string> others = DbIds.PackDatabases(this.PackContent);
                string package = DlcPackage.BuildFolder(this.TargetRoot, this.TargetRoot, stage, this.PackContent, this.PackContent, this.PackOffer, this.PackName, text);

                // The package's database as the game will merge it into its own (and the other packs'): the
                // same integrity rules, so a pack never leaves a broken database behind.
                string offerFolder = this.PackOffer.ToString(CultureInfo.InvariantCulture) + "_pri_65";
                string patch = Path.Combine(Path.Combine(Path.Combine(Path.Combine(Path.Combine(Path.Combine(package, "Media"), "DLCZips"), offerFolder), "Media"), "db"), "patch");
                string mergeFile = Path.Combine(patch, this.PackOffer.ToString(CultureInfo.InvariantCulture) + "000_merge.slt");
                DbIntegrity.Result merged = DbIntegrity.CheckMerge(File.ReadAllBytes(Path.Combine(Path.Combine(this.Media, "db"), "gamedb.slt")), others, File.ReadAllBytes(mergeFile), ForzaGame.Motorsport4);
                if (!merged.Ok)
                {
                    throw new InvalidOperationException("The pack's database would break the game's, nothing was left in Xenia:\r\n  " + string.Join("\r\n  ", merged.Errors.ToArray()));
                }
                this.Report.AppendLine("pack database check: merged into the game's database the rules hold");
                foreach (string line in merged.Warnings)
                {
                    this.Report.AppendLine("  warning: " + line);
                }
                this.Report.AppendLine("DLC package \"" + this.PackName + "\" (offer " + this.PackOffer + ") in " + this.PackContent + ":");
                this.Report.Append(text.ToString());
            }
            catch (Exception)
            {
                try
                {
                    DlcPackage.Remove(this.PackContent, this.PackOffer);
                }
                catch (IOException)
                {
                }
                throw;
            }
            finally
            {
                try
                {
                    if (Directory.Exists(stage))
                    {
                        Directory.Delete(stage, true);
                    }
                }
                catch (IOException)
                {
                }
            }
        }

        // The files a copy reads or changes, at the same place under a temporary root.
        private void Stage(string stage)
        {
            List<string> files = new List<string>();
            files.Add(Path.Combine(Path.Combine(this.Media, "db"), "gamedb.slt"));
            files.Add(ZipManifest.PathFor(this.TargetRoot));
            files.Add(Path.Combine(Path.Combine(this.Media, "cars"), this.SourceMediaName + ".zip"));
            files.AddRange(Directory.GetFiles(Path.Combine(this.Media, "StringTables"), "*.zip"));
            using (SqliteDatabase db = SqliteDatabase.FromBytes(File.ReadAllBytes(files[0])))
            {
                string thumbnail = db.Scalar("SELECT Thumbnail FROM Data_Car WHERE MediaName = ? COLLATE NOCASE", this.SourceMediaName) as string;
                if (!string.IsNullOrEmpty(thumbnail))
                {
                    GamePaths.GameFile from = GamePaths.Locate(this.TargetRoot, thumbnail);
                    if (from != null && from.InZip)
                    {
                        files.Add(from.ZipPath);
                    }
                }
            }
            string root = this.TargetRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            foreach (string file in files)
            {
                if (!file.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(file + " is not under the game folder.");
                }
                string copy = Path.Combine(stage, file.Substring(root.Length));
                Directory.CreateDirectory(Path.GetDirectoryName(copy));
                File.Copy(file, copy, true);
            }
        }

        private string Media
        {
            get { return Path.Combine(this.TargetRoot, "Media"); }
        }

        // The name is a file name of the game: letters, digits, underscore.
        public static bool IsValidName(string name)
        {
            return name != null && Regex.IsMatch(name, "^[A-Za-z0-9_]{3,40}$");
        }

        private List<string> Execute(bool apply)
        {
            this.stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
            List<string> plan = new List<string>();
            if (!IsValidName(this.NewMediaName))
            {
                throw new InvalidOperationException("The new MediaName must be 3-40 letters, digits or underscores (like ACU_NSX_97).");
            }
            string carsFolder = Path.Combine(this.Media, "cars");
            string sourceZipPath = Path.Combine(carsFolder, this.SourceMediaName + ".zip");
            string newZipPath = Path.Combine(carsFolder, this.NewMediaName + ".zip");
            if (!File.Exists(sourceZipPath))
            {
                throw new InvalidOperationException("The FM4 folder has no " + this.SourceMediaName + ".zip.");
            }
            if (File.Exists(newZipPath))
            {
                throw new InvalidOperationException(this.NewMediaName + ".zip already exists.");
            }
            string dbPath = Path.Combine(Path.Combine(this.Media, "db"), "gamedb.slt");
            string manifestPath = ZipManifest.PathFor(this.TargetRoot);
            SqliteDatabase db = SqliteDatabase.FromBytes(File.ReadAllBytes(dbPath));
            SqliteDatabase original = SqliteDatabase.FromBytes(File.ReadAllBytes(dbPath));
            SqliteDatabase work = null;
            bool writing = false;
            try
            {
                object sourceValue = db.Scalar("SELECT Id FROM Data_Car WHERE MediaName = ? COLLATE NOCASE", this.SourceMediaName);
                if (sourceValue == null)
                {
                    throw new InvalidOperationException("The database has no car " + this.SourceMediaName + ".");
                }
                if (db.Scalar("SELECT Id FROM Data_Car WHERE MediaName = ? COLLATE NOCASE", this.NewMediaName) != null)
                {
                    throw new InvalidOperationException("The database already has a car " + this.NewMediaName + ".");
                }
                long sourceId = Convert.ToInt64(sourceValue, CultureInfo.InvariantCulture);
                string reason;
                this.NewId = DbIds.CarId(db, db, this.NewMediaName, DbIds.PackDatabases(this.XeniaContent), DbIds.InstallFirstCar, false, out reason);
                plan.Add(string.Format(CultureInfo.InvariantCulture, "{0} (id {1}) -> {2}: id {3}, {4}", this.SourceMediaName, sourceId, this.NewMediaName, this.NewId, reason));

                // Database: the rows of a renumbered copy that the game database lacks.
                work = SqliteDatabase.FromBytes(db.ToBytes());
                plan.AddRange(DbIds.RenumberCar(work, sourceId, this.NewId));
                Dictionary<string, List<string>> schema = DbIds.Schema(db);
                List<string[]> carColumns = DbIds.CarColumns(schema);
                // Every write to the database is one transaction: an error puts all of it back.
                db.Begin();
                writing = true;
                int inserted = 0;
                List<string> inserts = new List<string>();
                List<string> skipped = new List<string>();
                foreach (KeyValuePair<string, List<string>> table in schema)
                {
                    List<string> columns = table.Value;
                    string key = DbIds.KeyColumn(db, table.Key, columns);
                    int keyIndex = (key == null) ? -1 : columns.FindIndex(delegate(string c) { return string.Equals(c, key, StringComparison.OrdinalIgnoreCase); });
                    List<int> carIndexes = new List<int>();
                    foreach (string[] car in carColumns)
                    {
                        if (string.Equals(car[0], table.Key, StringComparison.OrdinalIgnoreCase))
                        {
                            carIndexes.Add(columns.FindIndex(delegate(string c) { return string.Equals(c, car[1], StringComparison.OrdinalIgnoreCase); }));
                        }
                    }
                    bool keyIsCar = keyIndex >= 0 && carIndexes.Contains(keyIndex);
                    SqliteDatabase.Result before = db.Query("SELECT * FROM " + SqliteDatabase.Quote(table.Key));
                    SqliteDatabase.Result after = work.Query("SELECT * FROM " + SqliteDatabase.Quote(table.Key));
                    Dictionary<string, bool> had = new Dictionary<string, bool>();
                    foreach (object[] row in before.Rows)
                    {
                        had[RowText(row)] = true;
                    }
                    int copied = 0;
                    foreach (object[] row in after.Rows)
                    {
                        if (had.ContainsKey(RowText(row)))
                        {
                            continue;
                        }
                        bool take;
                        if (table.Key == "Data_Car")
                        {
                            take = Convert.ToInt64(row[0], CultureInfo.InvariantCulture) == this.NewId;
                        }
                        else if (keyIndex >= 0 && row[keyIndex] is long && (long) row[keyIndex] >= this.NewId * 1000 && (long) row[keyIndex] <= (this.NewId * 1000) + 999)
                        {
                            take = true;
                        }
                        else if (carIndexes.Count > 0 && (keyIndex < 0 || keyIsCar))
                        {
                            take = false;
                            foreach (int index in carIndexes)
                            {
                                take = take || (index >= 0 && row[index] is long && (long) row[index] == this.NewId);
                            }
                        }
                        else if (string.Equals(table.Key, "Combo_Engines", StringComparison.OrdinalIgnoreCase) && keyIndex >= 0 && carIndexes.Exists(delegate(int i) { return i >= 0 && row[i] is long && (long) row[i] == this.NewId; }))
                        {
                            // The old engine table has a counter of its own (EngineComboID), not car * 1000 + n: the
                            // copy's row (same engine, same stock flag) gets the next free key.
                            row[keyIndex] = DbIds.Next(db, table.Key, key, ForzaGame.Motorsport4);
                            take = true;
                        }
                        else
                        {
                            take = false;
                        }
                        if (!take)
                        {
                            bool pointsAtCar = false;
                            foreach (int index in carIndexes)
                            {
                                pointsAtCar = pointsAtCar || (index >= 0 && row[index] is long && (long) row[index] == this.NewId);
                            }
                            if (pointsAtCar)
                            {
                                skipped.Add(table.Key);
                            }
                            continue;
                        }
                        List<string> quoted = columns.ConvertAll<string>(delegate(string c) { return SqliteDatabase.Quote(c); });
                        string sql = "INSERT INTO " + SqliteDatabase.Quote(table.Key) + " (" + string.Join(",", quoted.ToArray()) + ") VALUES (" + string.Join(",", columns.ConvertAll<string>(delegate(string c) { return "?"; }).ToArray()) + ")";
                        db.Execute(sql, row);
                        copied++;
                    }
                    if (copied > 0)
                    {
                        inserted += copied;
                        inserts.Add(table.Key + " " + copied);
                    }
                }
                plan.Add(string.Format(CultureInfo.InvariantCulture, "database: {0} rows in {1} tables ({2})", inserted, inserts.Count, string.Join(", ", inserts.ToArray())));
                if (skipped.Count > 0)
                {
                    plan.Add("  rows that only point at the car were not copied: " + string.Join(", ", skipped.ToArray()));
                }

                // Data_Car of the copy.
                string thumbnailName = "Thumbnail_" + this.NewId.ToString(CultureInfo.InvariantCulture) + ".xds";
                string oldThumbnail = db.Scalar("SELECT Thumbnail FROM Data_Car WHERE Id = ?", sourceId) as string;
                string newThumbnail = null;
                if (!string.IsNullOrEmpty(oldThumbnail))
                {
                    newThumbnail = oldThumbnail.Substring(0, oldThumbnail.LastIndexOf('\\') + 1) + thumbnailName;
                }
                db.Execute("UPDATE Data_Car SET MediaName = ? WHERE Id = ?", this.NewMediaName, this.NewId);
                if (newThumbnail != null)
                {
                    db.Execute("UPDATE Data_Car SET Thumbnail = ? WHERE Id = ?", newThumbnail, this.NewId);
                }
                if (this.Year.HasValue)
                {
                    db.Execute("UPDATE Data_Car SET Year = ? WHERE Id = ?", this.Year.Value, this.NewId);
                }
                if (this.MakeId.HasValue)
                {
                    object makeName = db.Scalar("SELECT DisplayName FROM List_CarMake WHERE ID = ?", this.MakeId.Value);
                    if (makeName == null)
                    {
                        throw new InvalidOperationException("List_CarMake has no make " + this.MakeId.Value + ".");
                    }
                    db.Execute("UPDATE Data_Car SET MakeID = ?, MakeName = ? WHERE Id = ?", this.MakeId.Value, makeName, this.NewId);
                    object icon = db.Scalar("SELECT MakeIcon FROM Data_Car WHERE MakeID = ? AND Id <> ? LIMIT 1", this.MakeId.Value, this.NewId);
                    if (icon != null)
                    {
                        db.Execute("UPDATE Data_Car SET MakeIcon = ? WHERE Id = ?", icon, this.NewId);
                    }
                }
                foreach (KeyValuePair<string, object> pair in this.Data)
                {
                    if (!schema["Data_Car"].Exists(delegate(string c) { return string.Equals(c, pair.Key, StringComparison.OrdinalIgnoreCase); }))
                    {
                        throw new InvalidOperationException("Data_Car has no column " + pair.Key + ".");
                    }
                    db.Execute("UPDATE Data_Car SET " + SqliteDatabase.Quote(pair.Key) + " = ? WHERE Id = ?", pair.Value, this.NewId);
                }

                // The copy's own engine (CarEngine), before the checks.
                if (this.Engine != null)
                {
                    CarEngine.Clone(db, this.NewId, this.Engine, plan);
                }

                // The integrity rules (DbIntegrity, DbIds.CheckCar): nothing is written when one breaks.
                List<string> errors = DbIds.CheckCar(db, this.NewId, sourceId, this.NewMediaName);
                DbIntegrity.Result check = DbIntegrity.Compare(original, db, ForzaGame.Motorsport4, null);
                errors.AddRange(check.Errors);
                if (errors.Count > 0)
                {
                    throw new InvalidOperationException("Database check failed, nothing was written:\r\n  " + string.Join("\r\n  ", errors.ToArray()));
                }
                db.Commit();
                writing = false;
                plan.Add("database check: the rules hold (keys above FM4's largest and unique, every id points at a row, the car's Data_CarBody row, ModelId = Id)");
                foreach (string line in check.Summary)
                {
                    plan.Add("  " + line);
                }
                foreach (string line in check.Warnings)
                {
                    plan.Add("  warning: " + line);
                }

                // Strings: a key of its own in Data_Car.str of every language.
                Dictionary<string, StrFile> files = new Dictionary<string, StrFile>(StringComparer.OrdinalIgnoreCase);
                Dictionary<string, ForzaZip> stringZips = new Dictionary<string, ForzaZip>(StringComparer.OrdinalIgnoreCase);
                string tables = Path.Combine(this.Media, "StringTables");
                foreach (string zipPath in Directory.GetFiles(tables, "*.zip"))
                {
                    ForzaZip archive = new ForzaZip(zipPath);
                    ForzaZip.Entry entry = archive.Find("Data_Car.str");
                    if (entry != null)
                    {
                        stringZips[zipPath] = archive;
                        files[zipPath] = new StrFile(archive.Read(entry));
                    }
                }
                string[][] stringColumns = { new string[] { "DisplayName", "IDS_DisplayName_" }, new string[] { "ModelShort", "IDS_ModelShort_" } };
                foreach (string[] column in stringColumns)
                {
                    string value = db.Scalar("SELECT " + column[0] + " FROM Data_Car WHERE Id = ?", sourceId) as string;
                    uint stringId;
                    if (value == null || !value.StartsWith("_&", StringComparison.Ordinal) || !uint.TryParse(value.Substring(2), NumberStyles.None, CultureInfo.InvariantCulture, out stringId))
                    {
                        continue;
                    }
                    int oldKey = (int) (stringId & 0xFFFF);
                    int newKey = GameStringKeys.Free("Data_Car", files.Values);
                    Dictionary<string, string> perLanguage = (column[0] == "DisplayName") ? this.DisplayNames : this.ModelShorts;
                    string common = (column[0] == "DisplayName") ? this.DisplayName : this.ModelShort;
                    foreach (KeyValuePair<string, StrFile> file in files)
                    {
                        string language = Path.GetFileNameWithoutExtension(file.Key);
                        string text = null;
                        string given;
                        if (perLanguage.TryGetValue(language, out given) && !string.IsNullOrEmpty(given))
                        {
                            text = given;
                        }
                        else if (!string.IsNullOrEmpty(common))
                        {
                            text = common;
                        }
                        else
                        {
                            foreach (StrFile.Entry old in file.Value.Entries)
                            {
                                if (old.Key == oldKey)
                                {
                                    text = old.Text;
                                }
                            }
                        }
                        if (!string.IsNullOrEmpty(text))
                        {
                            file.Value.Set(newKey, text, column[1] + this.NewId.ToString(CultureInfo.InvariantCulture));
                        }
                    }
                    string newValue = "_&" + ((stringId & 0xFFFF0000u) | (uint) newKey).ToString(CultureInfo.InvariantCulture);
                    db.Execute("UPDATE Data_Car SET " + column[0] + " = ? WHERE Id = ?", newValue, this.NewId);
                    plan.Add(string.Format(CultureInfo.InvariantCulture, "strings: {0} key {1} (free in FM4 and its DLC) in {2} languages", column[0], newKey, files.Count));
                }

                // Car zip under the new name.
                ForzaZip sourceZip = new ForzaZip(sourceZipPath);
                ForzaZip newZip = ForzaZip.CreateLike(sourceZip);
                Regex name = new Regex(Regex.Escape(this.SourceMediaName), RegexOptions.IgnoreCase);
                string lower = this.NewMediaName.ToLowerInvariant();
                MatchEvaluator keepCase = delegate(Match m) { return (m.Value == m.Value.ToLowerInvariant()) ? lower : this.NewMediaName; };
                Encoding bytes = Encoding.GetEncoding(28591);
                int renamed = 0;
                int edited = 0;
                foreach (ForzaZip.Entry entry in sourceZip.Entries)
                {
                    if (entry.IsDirectory)
                    {
                        continue;
                    }
                    byte[] data = sourceZip.Read(entry);
                    string entryName = name.Replace(entry.Name, keepCase);
                    if (entryName != entry.Name)
                    {
                        renamed++;
                    }
                    string ext = Path.GetExtension(entry.Name).ToLowerInvariant();
                    if (ext == ".xml" || ext == ".txt" || ext == ".html")
                    {
                        string text = bytes.GetString(data);
                        string replaced = name.Replace(text, keepCase);
                        if (replaced != text)
                        {
                            data = bytes.GetBytes(replaced);
                            edited++;
                        }
                    }
                    newZip.Add(entryName, data);
                }
                plan.Add(string.Format(CultureInfo.InvariantCulture, "car zip: {0} files, {1} renamed, {2} text files with the name replaced", newZip.Entries.Count, renamed, edited));

                // Thumbnail: the source's file under the new name, in the same zip and folder.
                ForzaZip thumbnailZip = null;
                string thumbnailZipPath = null;
                if (oldThumbnail != null)
                {
                    GamePaths.GameFile from = GamePaths.Locate(this.TargetRoot, oldThumbnail);
                    if (from != null && from.InZip)
                    {
                        thumbnailZipPath = from.ZipPath;
                        thumbnailZip = new ForzaZip(from.ZipPath);
                        thumbnailZip.Add(from.EntryName.Substring(0, from.EntryName.LastIndexOf('/') + 1) + thumbnailName, GamePaths.Read(from));
                        plan.Add("thumbnail: " + thumbnailName + " in " + Path.GetFileName(from.ZipPath));
                    }
                    else
                    {
                        plan.Add("thumbnail: the source's file was not found in a zip, none made");
                    }
                }

                if (apply)
                {
                    List<string> locked = new List<string>();
                    List<string> targets = new List<string>();
                    targets.Add(dbPath);
                    targets.Add(manifestPath);
                    targets.AddRange(files.Keys);
                    if (thumbnailZipPath != null)
                    {
                        targets.Add(thumbnailZipPath);
                    }
                    foreach (string path in targets)
                    {
                        try
                        {
                            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                            {
                            }
                        }
                        catch (IOException)
                        {
                            locked.Add(Path.GetFileName(path));
                        }
                    }
                    if (locked.Count > 0)
                    {
                        throw new IOException("Files in use (close the game or the emulator first): " + string.Join(", ", locked.ToArray()));
                    }

                    // New files first, the database last: an error before it leaves the game as it was.
                    newZip.Save(newZipPath);
                    List<string> changed = new List<string>();
                    foreach (KeyValuePair<string, StrFile> file in files)
                    {
                        this.Backup(file.Key);
                        ForzaZip archive = stringZips[file.Key];
                        archive.Replace(archive.Find("Data_Car.str"), file.Value.ToBytes());
                        archive.Save(file.Key);
                        changed.Add(file.Key);
                    }
                    if (thumbnailZip != null)
                    {
                        this.Backup(thumbnailZipPath);
                        thumbnailZip.Save(thumbnailZipPath);
                        changed.Add(thumbnailZipPath);
                    }
                    this.Backup(dbPath);
                    string temp = dbPath + ".tmp";
                    File.WriteAllBytes(temp, db.ToBytes());
                    File.Delete(dbPath);
                    File.Move(temp, dbPath);
                    this.Backup(manifestPath);
                    foreach (string path in changed)
                    {
                        ZipManifest.Update(manifestPath, ZipManifest.GamePath(this.TargetRoot, path), path);
                    }
                    string like = "game:\\media\\cars\\" + this.SourceMediaName.ToLowerInvariant() + ".zip";
                    if (!ZipManifest.Add(manifestPath, ZipManifest.GamePath(this.TargetRoot, newZipPath), newZipPath, like))
                    {
                        throw new InvalidOperationException("zipmanifest.xml has no line like " + like + ".");
                    }
                    plan.Add(string.Format(CultureInfo.InvariantCulture, "zipmanifest.xml: {0} lines updated, 1 added", changed.Count));
                }
            }
            finally
            {
                if (writing)
                {
                    db.Rollback();
                }
                db.Dispose();
                original.Dispose();
                if (work != null)
                {
                    work.Dispose();
                }
            }
            foreach (string line in plan)
            {
                this.Report.AppendLine(line);
            }
            return plan;
        }

        // A row as text, to tell the rows a copy added from the ones it had.
        private static string RowText(object[] row)
        {
            StringBuilder text = new StringBuilder();
            foreach (object value in row)
            {
                if (value == null || value == DBNull.Value)
                {
                    text.Append("\u0001null|");
                }
                else if (value is byte[])
                {
                    text.Append(Convert.ToBase64String((byte[]) value)).Append('|');
                }
                else
                {
                    text.Append(Convert.ToString(value, CultureInfo.InvariantCulture)).Append('|');
                }
            }
            return text.ToString();
        }

        private void Backup(string path)
        {
            if (!File.Exists(path))
            {
                return;
            }
            Directory.CreateDirectory(this.BackupFolder);
            string copy = Path.Combine(this.BackupFolder, this.stamp + "_bak_" + Path.GetFileName(path));
            for (int i = 2; File.Exists(copy); i++)
            {
                copy = Path.Combine(this.BackupFolder, this.stamp + "_bak" + i + "_" + Path.GetFileName(path));
            }
            File.Copy(path, copy);
        }
    }
}
