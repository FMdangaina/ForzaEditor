namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Text.RegularExpressions;

    // Brings a car from Forza Horizon 2 into Forza Motorsport 4, as a new car (measured on the cars
    // both games have; see the notes on each step):
    //   model     the FH2 .carbin files converted to FM4 (CarbinModel.ConvertHorizonToMotorsport4),
    //             FH2-only materials renamed to FM4 ones; brakes (type 1) taken from a donor FM4 car;
    //   files     textures, livery masks, MAX data, shader settings, animations and gauges as they
    //             are (the digital dash .fbf gets FM4's version number); CarAttribs.xml rebuilt in the FM4 version-17 form from the donor's;
    //   wheel     the stock wheel's zip converted the same way;
    //   database  every row of the car (FM4 and FH2 share almost all table layouts) plus its engine,
    //             drivetrain, torque curve and wheel rows; PI mapped to the FM4 scale, class from it;
    //   strings   the car's names added to Data_Car.str in every FM4 language;
    //   sound     the engine tuning and car model tuning XML (FH2 reuses FM4 engine samples);
    //   thumbnail the car's thumbnail added to the UI textures;
    //   manifest  every rewritten or new zip listed in zipmanifest.xml.
    // Every file the port replaces is backed up first (<exe>\bak\<stamp>_bak_<name>).
    public class CarPort
    {
        public string SourceRoot;       // FH2 game folder (with media)
        public string TargetRoot;       // FM4 game folder
        public string MediaName;        // the car, e.g. MCL_P1_13
        public string DonorMediaName;   // an FM4 car close to it, e.g. MCL_MP412C_11 (brakes, templates)
        public bool Arcade = true;      // Data_Car.IsArcade
        public string OutputRoot;       // trial run: files written here (same layout) instead of TargetRoot
        public string BackupFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bak");
        public StringBuilder Report = new StringBuilder();

        private string stamp;
        private SqliteDatabase source;
        private SqliteDatabase target;
        private long carId;
        private long donorId;
        private readonly List<string> engineMedia = new List<string>();
        private readonly List<long> wheelIds = new List<long>();
        private readonly List<long> originalWheels = new List<long>();   // wheels FM4 already had before the port
        private readonly Dictionary<string, ForzaZip> changedZips = new Dictionary<string, ForzaZip>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> newZips = new List<string>();

        // FH2 materials FM4 has no shader for -> FM4 materials (null: the subsection goes).
        private static readonly Dictionary<string, string> MaterialMap = BuildMaterialMap();

        private static Dictionary<string, string> BuildMaterialMap()
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.Ordinal);
            map["taillight2S"] = "tail_light";
            map["taillightst"] = "tail_light";
            map["xenonhead"] = "head_light";
            map["drlwhite"] = "head_light";
            map["drlinlorange"] = "lights_amber";
            map["indicator_left"] = "lights_amber";
            map["indicator_right"] = "lights_amber";
            map["z_gauge_emissive_alpha"] = "gauge_emissive";
            map["deprecated"] = null;
            return map;
        }

        public void Run()
        {
            this.stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
            if (GameInfo.Detect(this.SourceRoot) != ForzaGame.Horizon2 || GameInfo.Detect(this.TargetRoot) != ForzaGame.Motorsport4)
            {
                throw new InvalidOperationException("The source must be a Forza Horizon 2 folder and the target a Forza Motorsport 4 folder.");
            }
            string sourceDbPath = Path.Combine(Path.Combine(Path.Combine(this.SourceRoot, "Media"), "db"), "gamedb.slt");
            string targetDbPath = Path.Combine(Path.Combine(Path.Combine(this.TargetRoot, "Media"), "db"), "gamedb.slt");
            this.source = SqliteDatabase.FromBytes(File.ReadAllBytes(sourceDbPath));
            this.target = SqliteDatabase.FromBytes(File.ReadAllBytes(targetDbPath));
            try
            {
                this.carId = ToLong(this.source.Scalar("SELECT Id FROM Data_Car WHERE MediaName = ? COLLATE NOCASE", this.MediaName), "the source database has no car " + this.MediaName);
                this.donorId = ToLong(this.target.Scalar("SELECT Id FROM Data_Car WHERE MediaName = ? COLLATE NOCASE", this.DonorMediaName), "the target database has no car " + this.DonorMediaName);
                object existing = this.target.Scalar("SELECT MediaName FROM Data_Car WHERE Id = ?", this.carId);
                if (existing != null && !string.Equals((string) existing, this.MediaName, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(string.Format("Car id {0} is {1} in the target database.", this.carId, existing));
                }
                this.Line("car {0}: id {1}, donor {2} (id {3})", this.MediaName, this.carId, this.DonorMediaName, this.donorId);

                this.PortDatabase();
                this.PortStrings();
                this.BuildCarZip();
                this.BuildWheelZips();
                this.PortSound();
                this.PortThumbnail();

                // Write everything: backups first, then files, then the manifest.
                this.Write(targetDbPath, this.target.ToBytes());
                foreach (KeyValuePair<string, ForzaZip> pair in this.changedZips)
                {
                    string output = this.Out(pair.Key);
                    this.Backup(output);
                    pair.Value.Save(output);
                    this.Line("saved {0}", this.Relative(pair.Key));
                }
                this.UpdateManifest();
            }
            finally
            {
                this.source.Dispose();
                this.target.Dispose();
            }
        }

        #region Database

        private Dictionary<string, List<string>> Schema(SqliteDatabase db)
        {
            Dictionary<string, List<string>> schema = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (object[] row in db.Query("SELECT name FROM sqlite_master WHERE type = 'table'").Rows)
            {
                List<string> columns = new List<string>();
                foreach (object[] column in db.Query("PRAGMA table_info(" + SqliteDatabase.Quote((string) row[0]) + ")").Rows)
                {
                    columns.Add((string) column[1]);
                }
                schema[(string) row[0]] = columns;
            }
            return schema;
        }

        private void PortDatabase()
        {
            Dictionary<string, List<string>> from = this.Schema(this.source);
            Dictionary<string, List<string>> to = this.Schema(this.target);
            string[] carColumns = { "Ordinal", "CarId", "CarID", "CarOrdinal" };
            int tables = 0;
            int rows = 0;
            List<object[]> engineRows = new List<object[]>();
            foreach (KeyValuePair<string, List<string>> table in to)
            {
                List<string> sourceColumns;
                if (!from.TryGetValue(table.Key, out sourceColumns) || table.Key == "Data_Car")
                {
                    continue;
                }
                string where = null;
                foreach (string column in carColumns)
                {
                    if (table.Value.Contains(column) && sourceColumns.Contains(column))
                    {
                        where = SqliteDatabase.Quote(column) + " = " + this.carId;
                        break;
                    }
                }
                if (where == null && table.Key.StartsWith("List_UpgradeCarBody", StringComparison.Ordinal) && table.Value.Count > 0 && table.Value[0] == "Id")
                {
                    where = "Id / 1000 = " + this.carId;      // body part tables: Id = car * 1000 + level
                }
                if (where == null)
                {
                    continue;
                }
                int copied = this.CopyRows(table.Key, sourceColumns, table.Value, where);
                if (copied > 0)
                {
                    tables++;
                    rows += copied;
                }
            }
            this.PortCarRow(from["Data_Car"], to["Data_Car"]);
            this.Line("database: {0} rows in {1} car tables + Data_Car", rows, tables);

            // Engine, drivetrain, torque curves, wheel: rows the car refers to that FM4 lacks.
            List<long> engines = this.Ids("SELECT DISTINCT EngineID FROM List_UpgradeEngine WHERE Ordinal = ?");
            foreach (long engine in engines)
            {
                object media = this.source.Scalar("SELECT MediaName FROM Data_Engine WHERE EngineID = ?", engine);
                if (media != null)
                {
                    this.engineMedia.Add((string) media);
                }
                object present = this.target.Scalar("SELECT MediaName FROM Data_Engine WHERE EngineID = ?", engine);
                if (present != null && !string.Equals((string) present, (string) media, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(string.Format("Engine {0} is {1} in FM4 and {2} in FH2.", engine, present, media));
                }
            }
            this.CopyById(from, to, "EngineID", engines, "List_UpgradeEngine");
            List<long> drivetrains = this.Ids("SELECT DISTINCT DrivetrainID FROM List_UpgradeDrivetrain WHERE Ordinal = ?");
            this.CopyById(from, to, "DrivetrainID", drivetrains, "List_UpgradeDrivetrain");
            this.CopyTorqueCurves(from, to, engines);
            object wheel = this.source.Scalar("SELECT StockWheelID FROM Data_Car WHERE Id = ?", this.carId);
            if (wheel != null)
            {
                this.wheelIds.Add(Convert.ToInt64(wheel));
                // A wheel FM4 lists for a car other than this one is FM4's own (a port before this one
                // added the car's wheel together with the car).
                if (Convert.ToInt64(this.target.Scalar("SELECT count(*) FROM Data_Car WHERE StockWheelID = ? AND Id <> ?", Convert.ToInt64(wheel), this.carId)) > 0)
                {
                    this.originalWheels.Add(Convert.ToInt64(wheel));
                }
                this.CopyById(from, to, "ID", this.wheelIds, null, "List_Wheels");
                this.CopyById(from, to, "WheelID", this.wheelIds, null, "WheelExceptions");
            }
            this.CopyReferences(from, to);
        }

        // Every "...ID" value in the rows of the car (and of its engine and drivetrain) that names
        // the key of a List_/Data_ table (first column of that name) must exist in FM4: the missing
        // rows come from FH2, and their own references after them (the P1's bumpers and wing point
        // at List_AeroPhysics rows FM4 does not have).
        private void CopyReferences(Dictionary<string, List<string>> from, Dictionary<string, List<string>> to)
        {
            Dictionary<string, string> keyed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, List<string>> table in to)
            {
                List<string> sourceColumns;
                if (table.Value.Count > 0 && table.Value[0].Length > 2 && table.Value[0].EndsWith("ID", StringComparison.OrdinalIgnoreCase)
                    && (table.Key.StartsWith("List_") || table.Key.StartsWith("Data_")) && from.TryGetValue(table.Key, out sourceColumns)
                    && sourceColumns.Count > 0 && sourceColumns[0] == table.Value[0] && !keyed.ContainsKey(table.Value[0]))
                {
                    keyed[table.Value[0]] = table.Key;
                }
            }
            // Rows to scan: the car's, then whatever gets copied.
            List<string[]> pending = new List<string[]>();
            foreach (KeyValuePair<string, List<string>> table in to)
            {
                foreach (string column in new string[] { "Ordinal", "CarId", "CarID" })
                {
                    if (table.Value.Contains(column))
                    {
                        pending.Add(new string[] { table.Key, SqliteDatabase.Quote(column) + " = " + this.carId });
                        break;
                    }
                }
                if (table.Key.StartsWith("List_UpgradeCarBody", StringComparison.Ordinal) && table.Value.Count > 0 && table.Value[0] == "Id")
                {
                    pending.Add(new string[] { table.Key, "Id / 1000 = " + this.carId });
                }
                if (table.Key == "Data_Car")
                {
                    pending.Add(new string[] { table.Key, "Id = " + this.carId });
                }
            }
            foreach (string column in new string[] { "EngineID", "DrivetrainID" })
            {
                foreach (KeyValuePair<string, List<string>> table in to)
                {
                    if (table.Value.Contains(column))
                    {
                        pending.Add(new string[] { table.Key, SqliteDatabase.Quote(column) + " IN (SELECT " + column + " FROM " + ((column == "EngineID") ? "List_UpgradeEngine" : "List_UpgradeDrivetrain") + " WHERE Ordinal = " + this.carId + ")" });
                    }
                }
            }
            int copied = 0;
            for (int i = 0; i < pending.Count && i < 5000; i++)
            {
                SqliteDatabase.Result rows = this.target.Query("SELECT * FROM " + SqliteDatabase.Quote(pending[i][0]) + " WHERE " + pending[i][1]);
                for (int c = 0; c < rows.Columns.Length; c++)
                {
                    string column = rows.Columns[c];
                    string table;
                    if (!keyed.TryGetValue(column, out table) || table == pending[i][0])
                    {
                        continue;
                    }
                    foreach (object[] row in rows.Rows)
                    {
                        if (row[c] == null || !(row[c] is long) || (long) row[c] < 0)
                        {
                            continue;
                        }
                        string where = SqliteDatabase.Quote(column) + " = " + (long) row[c];
                        if (Convert.ToInt64(this.target.Scalar("SELECT count(*) FROM " + SqliteDatabase.Quote(table) + " WHERE " + where)) > 0)
                        {
                            continue;
                        }
                        int added = this.CopyRows(table, from[table], to[table], where);
                        if (added > 0)
                        {
                            copied += added;
                            this.Line("  {0}: {1} rows ({2}, referenced by {3})", table, added, where.Replace("[", "").Replace("]", ""), pending[i][0]);
                            pending.Add(new string[] { table, where });
                        }
                    }
                }
            }
            this.Line("references: {0} more rows copied", copied);
        }

        private List<long> Ids(string sql)
        {
            List<long> ids = new List<long>();
            foreach (object[] row in this.source.Query(sql, this.carId).Rows)
            {
                if (row[0] != null)
                {
                    ids.Add(Convert.ToInt64(row[0]));
                }
            }
            return ids;
        }

        // Rows whose 'column' is one of the ids, in every table having that column (or only in
        // 'onlyTable'), when FM4 does not have them yet.
        private void CopyById(Dictionary<string, List<string>> from, Dictionary<string, List<string>> to, string column, List<long> ids, string skipTable, params string[] onlyTable)
        {
            if (ids.Count == 0)
            {
                return;
            }
            List<string> list = new List<string>();
            foreach (long id in ids)
            {
                list.Add(id.ToString(CultureInfo.InvariantCulture));
            }
            string where = SqliteDatabase.Quote(column) + " IN (" + string.Join(",", list.ToArray()) + ")";
            foreach (KeyValuePair<string, List<string>> table in to)
            {
                List<string> sourceColumns;
                if (table.Key == skipTable || !from.TryGetValue(table.Key, out sourceColumns) || !table.Value.Contains(column) || !sourceColumns.Contains(column))
                {
                    continue;
                }
                if (onlyTable.Length > 0 && Array.IndexOf(onlyTable, table.Key) < 0)
                {
                    continue;
                }
                int copied = this.CopyRows(table.Key, sourceColumns, table.Value, where);
                if (copied > 0)
                {
                    this.Line("  {0}: {1} rows ({2} {3})", table.Key, copied, column, string.Join(",", list.ToArray()));
                }
            }
        }

        private void CopyTorqueCurves(Dictionary<string, List<string>> from, Dictionary<string, List<string>> to, List<long> engines)
        {
            List<long> curves = new List<long>();
            foreach (KeyValuePair<string, List<string>> table in from)
            {
                if (!table.Value.Contains("EngineID") || engines.Count == 0)
                {
                    continue;
                }
                foreach (string column in table.Value)
                {
                    if (column.IndexOf("TorqueCurve", StringComparison.OrdinalIgnoreCase) < 0 || !column.EndsWith("ID", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    foreach (long engine in engines)
                    {
                        foreach (object[] row in this.source.Query("SELECT " + SqliteDatabase.Quote(column) + " FROM " + SqliteDatabase.Quote(table.Key) + " WHERE EngineID = ?", engine).Rows)
                        {
                            if (row[0] != null && !curves.Contains(Convert.ToInt64(row[0])))
                            {
                                curves.Add(Convert.ToInt64(row[0]));
                            }
                        }
                    }
                }
            }
            this.CopyById(from, to, "TorqueCurveID", curves, null, "List_TorqueCurve");
        }

        // Copies the rows matching 'where' (common columns); rows already there are replaced.
        private int CopyRows(string table, List<string> sourceColumns, List<string> targetColumns, string where)
        {
            List<string> columns = new List<string>();
            foreach (string column in targetColumns)
            {
                if (sourceColumns.Contains(column))
                {
                    columns.Add(column);
                }
            }
            if (columns.Count == 0)
            {
                return 0;
            }
            List<string> quoted = columns.ConvertAll<string>(delegate(string c) { return SqliteDatabase.Quote(c); });
            SqliteDatabase.Result rows = this.source.Query("SELECT " + string.Join(",", quoted.ToArray()) + " FROM " + SqliteDatabase.Quote(table) + " WHERE " + where);
            if (rows.Rows.Count == 0)
            {
                return 0;
            }
            this.target.Execute("DELETE FROM " + SqliteDatabase.Quote(table) + " WHERE " + where);
            string marks = string.Join(",", columns.ConvertAll<string>(delegate(string c) { return "?"; }).ToArray());
            string insert = "INSERT INTO " + SqliteDatabase.Quote(table) + " (" + string.Join(",", quoted.ToArray()) + ") VALUES (" + marks + ")";
            foreach (object[] row in rows.Rows)
            {
                this.target.Execute(insert, row);
            }
            return rows.Rows.Count;
        }

        // Data_Car: the FH2 row in FM4's columns; PI on the FM4 scale (fit on 31 cars both games
        // have, FH2 PI > 0.7: FM4 = 1.025 FH2 - 0.261), class from FM4's PI ranges, base-game content.
        private void PortCarRow(List<string> sourceColumns, List<string> targetColumns)
        {
            this.CopyRows("Data_Car", sourceColumns, targetColumns, "Id = " + this.carId);
            double pi = Convert.ToDouble(this.source.Scalar("SELECT PerformanceIndex FROM Data_Car WHERE Id = ?", this.carId), CultureInfo.InvariantCulture);
            double fm4Pi = Math.Max(0.001, Math.Min(0.999, (1.025 * pi) - 0.261));
            object classId = this.target.Scalar("SELECT ClassID FROM Data_Car WHERE Id <> ? AND PerformanceIndex <= ? GROUP BY ClassID ORDER BY max(PerformanceIndex) DESC LIMIT 1", this.carId, fm4Pi);
            this.target.Execute("UPDATE Data_Car SET PerformanceIndex = ?, ClassID = ?, ContentId = 0, IsArcade = ?, IsPurchased = 1, IsInstalled = 1 WHERE Id = ?",
                fm4Pi, (classId != null) ? Convert.ToInt64(classId) : 9L, this.Arcade ? 1L : 0L, this.carId);
            this.Line("Data_Car {0}: PI {1:0.000} (FH2) -> {2:0.000} (FM4), class {3}, IsArcade {4}", this.carId, pi, fm4Pi, classId, this.Arcade ? 1 : 0);
        }

        private static long ToLong(object value, string error)
        {
            if (value == null)
            {
                throw new InvalidOperationException(error);
            }
            return Convert.ToInt64(value);
        }

        #endregion

        #region Strings

        // DisplayName / ModelShort are "_&<id>": the low 16 bits are the key in Data_Car.str.
        private void PortStrings()
        {
            List<int> keys = new List<int>();
            foreach (string column in new string[] { "DisplayName", "ModelShort" })
            {
                object value = this.source.Scalar("SELECT " + column + " FROM Data_Car WHERE Id = ?", this.carId);
                uint id;
                if (value is string && ((string) value).StartsWith("_&") && uint.TryParse(((string) value).Substring(2), NumberStyles.None, CultureInfo.InvariantCulture, out id))
                {
                    keys.Add((int) (id & 0xFFFF));
                }
            }
            string targetTables = Path.Combine(Path.Combine(this.TargetRoot, "Media"), "StringTables");
            string sourceTables = Path.Combine(Path.Combine(this.SourceRoot, "Media"), "StringTables");
            int languages = 0;
            foreach (string targetZip in Directory.GetFiles(targetTables, "*.zip"))
            {
                string sourceZip = Path.Combine(sourceTables, Path.GetFileName(targetZip));
                if (!File.Exists(sourceZip))
                {
                    sourceZip = Path.Combine(sourceTables, "EN.zip");
                }
                ForzaZip sourceArchive = new ForzaZip(sourceZip);
                ForzaZip.Entry sourceEntry = sourceArchive.Find("Data_Car.str");
                ForzaZip archive = this.OpenChanged(targetZip);
                ForzaZip.Entry entry = archive.Find("Data_Car.str");
                if (sourceEntry == null || entry == null)
                {
                    continue;
                }
                StrFile from = new StrFile(sourceArchive.Read(sourceEntry));
                StrFile to = new StrFile(archive.Read(entry));
                foreach (int key in keys)
                {
                    StrFile.Entry text = null;
                    foreach (StrFile.Entry candidate in from.Entries)
                    {
                        if (candidate.Key == key)
                        {
                            text = candidate;
                        }
                    }
                    if (text == null)
                    {
                        continue;
                    }
                    foreach (StrFile.Entry existing in to.Entries)
                    {
                        if (existing.Key == key && existing.Name != text.Name)
                        {
                            throw new InvalidOperationException(string.Format("String key {0:X4} is already used by {1} in {2}.", key, existing.Name, Path.GetFileName(targetZip)));
                        }
                    }
                    to.Set(key, text.Text, text.Name);
                }
                archive.Replace(entry, to.ToBytes());
                languages++;
            }
            this.Line("strings: {0} keys in Data_Car.str of {1} languages", keys.Count, languages);
        }

        #endregion

        #region Car and wheel zips

        private void BuildCarZip()
        {
            string sourcePath = GamePaths.FindCar(this.SourceRoot, this.MediaName);
            string donorPath = GamePaths.FindCar(this.TargetRoot, this.DonorMediaName);
            if (sourcePath == null || donorPath == null || !sourcePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Car zips not found (" + this.MediaName + " in FH2, " + this.DonorMediaName + " in FM4).");
            }
            ForzaZip from = new ForzaZip(sourcePath);
            ForzaZip donor = new ForzaZip(donorPath);
            ForzaZip car = ForzaZip.CreateLike(donor);
            string prefix = this.MediaName.ToLowerInvariant();
            string donorPrefix = this.DonorMediaName.ToLowerInvariant();
            int models = 0;
            // Granny animations: FM4 reads little-endian .gr2 files, FH2 writes big-endian ones (file
            // version 7 at 0x20 as 00000007, measured on the MP4-12C both games have). Without a
            // converter the car goes without them, as FM4 cars without moving parts do.
            bool animations = true;
            foreach (ForzaZip.Entry entry in from.Entries)
            {
                if (entry.Name.EndsWith(".gr2", StringComparison.OrdinalIgnoreCase))
                {
                    byte[] granny = from.Read(entry);
                    animations &= granny.Length > 0x24 && granny[0x20] == 7 && granny[0x23] == 0;
                }
            }
            if (!animations)
            {
                this.Line("  animations: FH2 .gr2 files are big-endian, left out (no moving parts)");
            }
            foreach (ForzaZip.Entry entry in from.Entries)
            {
                string name = entry.Name.ToLowerInvariant();
                if (name.StartsWith("stripped_") || name == "physicsdefinition.bin" || name == "manufacturercolors.xml")
                {
                    continue;   // FH2 only
                }
                if (!animations && (name.EndsWith(".gr2") || name == "caranimationevents.xml"))
                {
                    continue;
                }
                byte[] data = from.Read(entry);
                if (name.EndsWith(".fbf") && data.Length > 4 && data[0] == 0 && data[1] == 0 && data[2] == 3 && data[3] == 0xF0)
                {
                    // Anark dash layout: FH2 writes version 0x3F0 (text layers keep a default string
                    // where FM4's 0x3EF files have an empty one, same length field); .bgf/.bsg are identical.
                    data[3] = 0xEF;
                }
                if (name.EndsWith(".carbin"))
                {
                    if (name.Contains("_caliper") || name.Contains("_rotor"))
                    {
                        // Brakes are carbin type 1 (no converter yet): the donor's, renamed.
                        ForzaZip.Entry brake = donor.Find(donorPrefix + name.Substring(prefix.Length));
                        if (brake == null)
                        {
                            continue;
                        }
                        data = donor.Read(brake);
                    }
                    else
                    {
                        data = ConvertModel(data, name.EndsWith("_lod0.carbin"), this.Report);
                        models++;
                    }
                }
                else if (name == "carattribs.xml")
                {
                    data = ConvertCarAttributes(Encoding.UTF8.GetString(data), Encoding.UTF8.GetString(donor.Read(donor.Find("carattribs.xml"))), animations);
                }
                car.Add(name, data);
            }
            ForzaZip.Entry version = donor.Find("versiondata.xml");
            if (version != null && car.Find("versiondata.xml") == null)
            {
                car.Add("versiondata.xml", donor.Read(version));
            }
            string path = Path.Combine(Path.Combine(Path.Combine(this.TargetRoot, "Media"), "cars"), this.MediaName + ".zip");
            this.SaveNew(car, path);
            this.Line("car zip: {0} entries, {1} models converted -> {2}", car.Entries.Count, models, this.Relative(path));
        }

        // One FH2 model file in FM4 form: converted, FH2-only materials renamed (lod0 lights to the
        // _lod0 variants FM4 uses there), the 'deprecated' material removed.
        public static byte[] ConvertModel(byte[] data, bool lod0, StringBuilder report)
        {
            CarbinModel model = new CarbinModel(data);
            model.ConvertHorizonToMotorsport4();
            Dictionary<string, int> renamed = new Dictionary<string, int>();
            List<string[]> remove = new List<string[]>();
            foreach (CarbinModel.Section section in model.Sections)
            {
                foreach (CarbinModel.SubSection sub in section.SubSections)
                {
                    string to;
                    if (!MaterialMap.TryGetValue(sub.Name, out to))
                    {
                        continue;
                    }
                    if (to == null)
                    {
                        remove.Add(new string[] { section.Name, sub.Name });
                        continue;
                    }
                    if (lod0 && to == "tail_light")
                    {
                        to = "tail_light_lod0";
                    }
                    int count;
                    renamed.TryGetValue(sub.Name + " -> " + to, out count);
                    renamed[sub.Name + " -> " + to] = count + 1;
                    sub.Name = to;
                }
            }
            foreach (string[] pair in remove)
            {
                CarbinModel.Section owner = model.Find(pair[0]);
                if (owner.SubSections.Count > 1)
                {
                    model.RemoveSubSections(pair[0], pair[1]);
                }
                else
                {
                    owner.SubSections[0].Name = "black";
                }
            }
            if (report != null && renamed.Count > 0)
            {
                List<string> text = new List<string>();
                foreach (KeyValuePair<string, int> pair in renamed)
                {
                    text.Add(pair.Key + " (" + pair.Value + ")");
                }
                report.AppendLine("  materials: " + string.Join(", ", text.ToArray()) + ((remove.Count > 0) ? string.Format(", {0} 'deprecated' removed", remove.Count) : ""));
            }
            return model.ToBytes();
        }

        // FM4 CarAttribs version 17: the donor's file with the car's own gauges, driver placement,
        // part drops and animations (FH2 version 21 writes <animation Version="2" ... inrace="1"/>,
        // FM4 <Animation ... inrace="true" />; FH2 repeats each one per LOD).
        public static byte[] ConvertCarAttributes(string fh2, string fm4Donor, bool animations)
        {
            string result = fm4Donor;
            foreach (string element in new string[] { "DriverHandsSteeringWheelOverride", "DriverHandsOffset", "SteeringWheelAngleOverride", "Driver" })
            {
                Match from = Regex.Match(fh2, "<" + element + "\\b[^>]*/>");
                Match to = Regex.Match(result, "<" + element + "\\b[^>]*/>");
                if (from.Success && to.Success)
                {
                    result = result.Substring(0, to.Index) + from.Value + result.Substring(to.Index + to.Length);
                }
            }
            foreach (string block in new string[] { "Gauges", "PartDropValues" })
            {
                Match from = Regex.Match(fh2, "<" + block + ">.*?</" + block + ">", RegexOptions.Singleline);
                Match to = Regex.Match(result, "<" + block + ">.*?</" + block + ">", RegexOptions.Singleline);
                if (from.Success && to.Success)
                {
                    result = result.Substring(0, to.Index) + from.Value + result.Substring(to.Index + to.Length);
                }
            }
            Match target = Regex.Match(result, "[ \t]*<Animations>.*?</Animations>\r?\n?", RegexOptions.Singleline);
            if (!animations)
            {
                // No animation files: no <Animations> element (as on FM4 cars without moving parts).
                return Encoding.UTF8.GetBytes(target.Success ? result.Remove(target.Index, target.Length) : result);
            }
            Match source = Regex.Match(fh2, "<Animations>.*?</Animations>", RegexOptions.Singleline);
            target = Regex.Match(result, "<Animations>.*?</Animations>", RegexOptions.Singleline);
            if (source.Success && target.Success)
            {
                StringBuilder list = new StringBuilder("<Animations>\r\n");
                List<string> seen = new List<string>();
                foreach (Match animation in Regex.Matches(source.Value, "<animation\\b([^>]*)/>", RegexOptions.IgnoreCase))
                {
                    string attributes = Regex.Replace(animation.Groups[1].Value, "\\s*Version=\"[^\"]*\"", "");
                    attributes = Regex.Replace(attributes, "inrace=\"1\"", "inrace=\"true\"");
                    attributes = attributes.Trim();
                    if (seen.Contains(attributes))
                    {
                        continue;
                    }
                    seen.Add(attributes);
                    list.Append("    <Animation " + attributes + " />\r\n");
                }
                list.Append("  </Animations>");
                result = result.Substring(0, target.Index) + list.ToString() + result.Substring(target.Index + target.Length);
            }
            return Encoding.UTF8.GetBytes(result);
        }

        private void BuildWheelZips()
        {
            string donorWheelMedia = this.target.Scalar("SELECT w.MediaName FROM List_Wheels w JOIN Data_Car c ON c.StockWheelID = w.ID WHERE c.Id = ?", this.donorId) as string;
            foreach (long id in this.wheelIds)
            {
                string media = this.source.Scalar("SELECT MediaName FROM List_Wheels WHERE ID = ?", id) as string;
                string sourcePath = GamePaths.FindMedia(this.SourceRoot, "wheels", media);
                string donorPath = (donorWheelMedia != null) ? GamePaths.FindMedia(this.TargetRoot, "wheels", donorWheelMedia) : null;
                if (sourcePath == null || donorPath == null)
                {
                    this.Line("wheel {0}: zip not found, skipped", media);
                    continue;
                }
                string path = Path.Combine(Path.Combine(Path.Combine(this.TargetRoot, "Media"), "wheels"), media + ".zip");
                object original = this.originalWheels.Contains(id) ? (object) true : null;
                if (File.Exists(path) && original != null)
                {
                    this.Line("wheel {0}: an FM4 wheel, kept", media);
                    continue;
                }
                ForzaZip from = new ForzaZip(sourcePath);
                ForzaZip donor = new ForzaZip(donorPath);
                ForzaZip wheel = ForzaZip.CreateLike(donor);
                foreach (ForzaZip.Entry entry in from.Entries)
                {
                    string name = entry.Name.ToLowerInvariant();
                    byte[] data = from.Read(entry);
                    if (name.EndsWith(".carbin"))
                    {
                        CarbinModel model = new CarbinModel(data);
                        model.ConvertHorizonToMotorsport4();
                        data = model.ToBytes();
                    }
                    wheel.Add(name, data);
                }
                ForzaZip.Entry version = donor.Find("versiondata.xml");
                if (version != null && wheel.Find("versiondata.xml") == null)
                {
                    wheel.Add("versiondata.xml", donor.Read(version));
                }
                this.SaveNew(wheel, path);
                this.Line("wheel zip: {0} ({1} entries)", this.Relative(path), wheel.Entries.Count);
            }
        }

        #endregion

        #region Sound and thumbnail

        // FH2's engine tuning (<engine media>_ET.xml) and car model tuning (<car>_CMT.xml) have the FM4
        // layout and point at harmonic tunings by name; when FM4 lacks one, the donor's tuning is used.
        private void PortSound()
        {
            string sourceAudio = Path.Combine(Path.Combine(Path.Combine(this.SourceRoot, "Media"), "audio"), "cars");
            string targetAudio = Path.Combine(Path.Combine(Path.Combine(this.TargetRoot, "Media"), "audio"), "Cars");
            ForzaZip sourceTuning = new ForzaZip(Path.Combine(Path.Combine(sourceAudio, "Engines"), "EngineTuning.zip"));
            ForzaZip harmonic = new ForzaZip(Path.Combine(Path.Combine(targetAudio, "Engines"), "harmonictuning.zip"));
            ForzaZip engineTuning = this.OpenChanged(Path.Combine(Path.Combine(targetAudio, "Engines"), "enginetuning.zip"));
            string donorEngine = this.target.Scalar("SELECT e.MediaName FROM Data_Engine e JOIN List_UpgradeEngine u ON u.EngineID = e.EngineID WHERE u.Ordinal = ? AND u.Level = 0", this.donorId) as string;
            foreach (string media in this.engineMedia)
            {
                ForzaZip.Entry entry = sourceTuning.Find(media + "_ET.xml");
                byte[] data = (entry != null) ? sourceTuning.Read(entry) : null;
                List<string> missing = new List<string>();
                if (data != null)
                {
                    foreach (Match reference in Regex.Matches(Encoding.UTF8.GetString(data), "\"([^\"]+_HT\\.xml)\"", RegexOptions.IgnoreCase))
                    {
                        if (harmonic.Find(reference.Groups[1].Value) == null && !missing.Contains(reference.Groups[1].Value))
                        {
                            missing.Add(reference.Groups[1].Value);
                        }
                    }
                }
                if (data == null || missing.Count > 0)
                {
                    ForzaZip.Entry fallback = (donorEngine != null) ? engineTuning.Find(donorEngine + "_ET.xml") : null;
                    if (fallback == null)
                    {
                        this.Line("engine sound {0}: no tuning", media);
                        continue;
                    }
                    data = engineTuning.Read(fallback);
                    this.Line("engine sound {0}: the donor's ({1}; FM4 lacks {2})", media, donorEngine, (missing.Count > 0) ? string.Join(", ", missing.ToArray()) : "the FH2 tuning");
                }
                else
                {
                    this.Line("engine sound {0}: FH2 tuning, its harmonic tunings are all in FM4", media);
                }
                this.Put(engineTuning, media + "_ET.xml", data);
            }

            ForzaZip sourceModels = new ForzaZip(Path.Combine(sourceAudio, "CarModelTuning.zip"));
            ForzaZip carModels = this.OpenChanged(Path.Combine(targetAudio, "CarModelTuning.zip"));
            ForzaZip.Entry modelTuning = sourceModels.Find(this.MediaName + "_CMT.xml");
            if (modelTuning != null)
            {
                this.Put(carModels, this.MediaName + "_CMT.xml", sourceModels.Read(modelTuning));
                this.Line("car model tuning {0}_CMT.xml added", this.MediaName);
            }
        }

        private void PortThumbnail()
        {
            string thumbnail = this.source.Scalar("SELECT Thumbnail FROM Data_Car WHERE Id = ?", this.carId) as string;
            string donorThumbnail = this.target.Scalar("SELECT Thumbnail FROM Data_Car WHERE Id = ?", this.donorId) as string;
            GamePaths.GameFile from = GamePaths.Locate(this.SourceRoot, thumbnail);
            GamePaths.GameFile like = GamePaths.Locate(this.TargetRoot, donorThumbnail);
            if (from == null || like == null || !like.InZip)
            {
                this.Line("thumbnail: not found, skipped");
                return;
            }
            ForzaZip textures = this.OpenChanged(like.ZipPath);
            string name = like.EntryName.Substring(0, like.EntryName.LastIndexOf('/') + 1) + Path.GetFileName(thumbnail.Replace('\\', '/').Replace('/', Path.DirectorySeparatorChar));
            this.Put(textures, name, GamePaths.Read(from));
            this.Line("thumbnail: {0} in {1}", name, this.Relative(like.ZipPath));
        }

        #endregion

        #region Files

        private ForzaZip OpenChanged(string path)
        {
            ForzaZip archive;
            if (!this.changedZips.TryGetValue(path, out archive))
            {
                archive = new ForzaZip(path);
                this.changedZips[path] = archive;
            }
            return archive;
        }

        private void Put(ForzaZip archive, string name, byte[] data)
        {
            ForzaZip.Entry entry = archive.Find(name);
            if (entry != null)
            {
                archive.Replace(entry, data);
            }
            else
            {
                archive.Add(name, data);
            }
        }

        private void SaveNew(ForzaZip archive, string path)
        {
            string output = this.Out(path);
            if (File.Exists(output))
            {
                this.Backup(output);
            }
            archive.Save(output);
            this.newZips.Add(path);
        }

        // Where a target file is written: itself, or its place under OutputRoot on a trial run.
        private string Out(string path)
        {
            if (this.OutputRoot == null)
            {
                return path;
            }
            string output = Path.Combine(this.OutputRoot, this.Relative(path));
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            return output;
        }

        private void Write(string path, byte[] data)
        {
            path = this.Out(path);
            if (File.Exists(path))
            {
                this.Backup(path);
            }
            string temp = path + ".tmp";
            File.WriteAllBytes(temp, data);
            File.Delete(path);
            File.Move(temp, path);
            this.Line("saved {0}", this.Relative(path));
        }

        private void Backup(string path)
        {
            if (!File.Exists(path))
            {
                return;
            }
            string folder = this.BackupFolder;
            Directory.CreateDirectory(folder);
            string copy = Path.Combine(folder, this.stamp + "_bak_" + Path.GetFileName(path));
            for (int i = 2; File.Exists(copy); i++)
            {
                copy = Path.Combine(folder, this.stamp + "_bak" + i + "_" + Path.GetFileName(path));
            }
            File.Copy(path, copy);
        }

        private void UpdateManifest()
        {
            string manifest = this.Out(ZipManifest.PathFor(this.TargetRoot));
            if (this.OutputRoot != null && !File.Exists(manifest))
            {
                File.Copy(ZipManifest.PathFor(this.TargetRoot), manifest);
            }
            this.Backup(manifest);
            foreach (string path in this.changedZips.Keys)
            {
                ZipManifest.Update(manifest, ZipManifest.GamePath(this.TargetRoot, path), this.Out(path));
            }
            foreach (string path in this.newZips)
            {
                string folder = Path.GetFileName(Path.GetDirectoryName(path)).ToLowerInvariant();
                string like = "game:\\media\\" + folder + "\\" + ((folder == "cars") ? this.DonorMediaName : this.DonorWheel()).ToLowerInvariant() + ".zip";
                if (!ZipManifest.Add(manifest, ZipManifest.GamePath(this.TargetRoot, path), this.Out(path), like))
                {
                    throw new InvalidOperationException("zipmanifest.xml has no line like " + like + ".");
                }
            }
            this.Line("zipmanifest.xml: {0} lines updated, {1} added", this.changedZips.Count, this.newZips.Count);
        }

        private string DonorWheel()
        {
            return (this.target.Scalar("SELECT w.MediaName FROM List_Wheels w JOIN Data_Car c ON c.StockWheelID = w.ID WHERE c.Id = ?", this.donorId) as string) ?? this.DonorMediaName;
        }

        private string Relative(string path)
        {
            return path.StartsWith(this.TargetRoot, StringComparison.OrdinalIgnoreCase) ? path.Substring(this.TargetRoot.TrimEnd('\\').Length + 1) : path;
        }

        private void Line(string format, params object[] args)
        {
            this.Report.AppendLine(string.Format(CultureInfo.InvariantCulture, format, args));
        }

        #endregion
    }
}
