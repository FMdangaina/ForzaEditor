namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;

    // FM2 upgrade tables -> the FM4 ones, for the car of a stage database (FM2 -> FM4 port, 2026-10-20).
    // FM2 keeps one table per part keyed by the car (Ordinal); FM4 keys body parts by CarBodyID (car * 1000),
    // splits tire width / rim size / anti-roll bar into front and rear tables, and moved the suspension and
    // anti-roll physics behind ids that follow the upgrade Id (front car*1000+n, rear car*1000+100+n).
    // PartsStringId, ManufacturerId and the other lookup ids have the same meaning in both games (a front
    // bumper PartsStringId 27 is the same in FM2 and FM4), so rows are copied column by column.
    // The donor's rows of the car are removed first, so nothing points at parts the FM2 model does not have.
    // Not here: engine and drivetrain parts (FM2 has no camshaft / valves / turbo tables; its transmission,
    // clutch, differential, driveline and flywheel tables are keyed by car, FM4's by DrivetrainID / EngineID).
    public static class Fm2CarUpgrades
    {
        // FM2 table, FM4 table, FM4 key column. Keyed by car (Ordinal) in FM2 and, for the body parts, by CarBodyID in FM4.
        private static readonly string[][] Direct = new string[][]
        {
            new string[] { "List_UpgradeFrontBumper", "List_UpgradeCarBodyFrontBumper", "CarBodyID" },
            new string[] { "List_UpgradeRearBumper", "List_UpgradeCarBodyRearBumper", "CarBodyID" },
            new string[] { "List_UpgradeHood", "List_UpgradeCarBodyHood", "CarBodyID" },
            new string[] { "List_UpgradeSideSkirt", "List_UpgradeCarBodySideSkirt", "CarBodyID" },
            new string[] { "List_UpgradeWeight", "List_UpgradeCarBodyWeight", "CarBodyId" },
            new string[] { "List_UpgradeRearWing", "List_UpgradeRearWing", "Ordinal" },
            new string[] { "List_UpgradeBrakes", "List_UpgradeBrakes", "Ordinal" },
            new string[] { "List_UpgradeTireCompound", "List_UpgradeTireCompound", "Ordinal" },
        };

        // Returns a log. carName = MediaName of the car in both databases.
        public static List<string> Apply(SqliteDatabase target, SqliteDatabase fm2, string mediaName)
        {
            List<string> log = new List<string>();
            object fromObj = fm2.Scalar("SELECT Id FROM Data_Car WHERE MediaName = ? COLLATE NOCASE", mediaName);
            object toObj = target.Scalar("SELECT Id FROM Data_Car WHERE MediaName = ? COLLATE NOCASE", mediaName);
            if (fromObj == null || toObj == null)
            {
                log.Add("Fm2CarUpgrades: " + mediaName + " not found in " + (fromObj == null ? "the FM2" : "the target") + " database");
                return log;
            }
            long from = Convert.ToInt64(fromObj), car = Convert.ToInt64(toObj), body = car * 1000;

            foreach (string[] map in Direct)
            {
                if (!Has(fm2, map[0]) || !Has(target, map[1]))
                {
                    continue;
                }
                bool bodyKey = map[2] != "Ordinal";
                CopyRows(target, fm2, map[0], "Ordinal", from, map[1], map[2], bodyKey ? body : car, car * 1000, null, log);
            }

            // Tire width and rim size: one FM2 table, a front and a rear one in FM4.
            CopyRows(target, fm2, "List_UpgradeTireWidth", "Ordinal", from, "List_UpgradeCarBodyTireWidthFront", "CarBodyId", body, car * 1000, new string[] { "FrontTireWidth" }, log);
            CopyRows(target, fm2, "List_UpgradeTireWidth", "Ordinal", from, "List_UpgradeCarBodyTireWidthRear", "CarBodyId", body, car * 1000, new string[] { "RearTireWidth" }, log);
            CopyRows(target, fm2, "List_UpgradeRimSize", "Ordinal", from, "List_UpgradeRimSizeFront", "Ordinal", car, car * 1000, new string[] { "FrontWheelDiameter" }, log);
            CopyRows(target, fm2, "List_UpgradeRimSize", "Ordinal", from, "List_UpgradeRimSizeRear", "Ordinal", car, car * 1000, new string[] { "RearWheelDiameter" }, log);

            // Aero physics of the wings and bumpers: per-car rows, copied under new ids (car * 1000 + n).
            Dictionary<long, long> aero = new Dictionary<long, long>();
            long nextAero = 1;
            foreach (string table in new string[] { "List_UpgradeCarBodyFrontBumper", "List_UpgradeRearWing" })
            {
                if (!Has(target, table))
                {
                    continue;
                }
                SqliteDatabase.Result rows = target.Query("SELECT Id, AeroPhysicsID FROM " + table + " WHERE " + (table == "List_UpgradeRearWing" ? "Ordinal = ?" : "CarBodyID = ?"), table == "List_UpgradeRearWing" ? car : body);
                foreach (object[] row in rows.Rows)
                {
                    if (row[1] == null)
                    {
                        continue;
                    }
                    long old = Convert.ToInt64(row[1]), mapped;
                    if (!aero.TryGetValue(old, out mapped))
                    {
                        SqliteDatabase.Result src = fm2.Query("SELECT * FROM List_AeroPhysics WHERE AeroPhysicsID = ?", old);
                        if (src.Rows.Count == 0)
                        {
                            continue;
                        }
                        while (Convert.ToInt64(target.Scalar("SELECT COUNT(*) FROM List_AeroPhysics WHERE AeroPhysicsID = ?", car * 1000 + nextAero)) > 0)
                        {
                            nextAero++;
                        }
                        mapped = car * 1000 + nextAero++;
                        Physics(target, fm2, "List_AeroPhysics", "AeroPhysicsID", old, mapped, car);
                        aero[old] = mapped;
                    }
                    target.Execute("UPDATE " + table + " SET AeroPhysicsID = ? WHERE Id = ?", mapped, row[0]);
                }
            }
            log.Add("List_AeroPhysics: " + aero.Count + " rows copied");

            Suspension(target, fm2, from, car, log);
            return log;
        }

        // Springs / dampers and anti-roll bars: the FM2 upgrade rows with their physics rows, FM4 ids.
        private static void Suspension(SqliteDatabase target, SqliteDatabase fm2, long from, long car, List<string> log)
        {
            if (!Has(fm2, "List_UpgradeSpringDamper") || !Has(target, "List_SpringDamperPhysics"))
            {
                return;
            }
            target.Execute("DELETE FROM List_SpringDamperPhysics WHERE Ordinal = ?", car);
            target.Execute("DELETE FROM List_AntiSwayPhysics WHERE Ordinal = ?", car);
            target.Execute("DELETE FROM List_UpgradeSpringDamper WHERE Ordinal = ?", car);
            target.Execute("DELETE FROM List_UpgradeAntiSwayFront WHERE Ordinal = ?", car);
            target.Execute("DELETE FROM List_UpgradeAntiSwayRear WHERE Ordinal = ?", car);
            int n = 0;
            SqliteDatabase.Result springs = fm2.Query("SELECT * FROM List_UpgradeSpringDamper WHERE Ordinal = ? ORDER BY Id", from);
            foreach (object[] row in springs.Rows)
            {
                n++;
                long front = car * 1000 + n, rear = car * 1000 + 100 + n;
                Insert(target, "List_UpgradeSpringDamper", springs, row, "Id", UpgradeId(springs, row, from, car, n), new Dictionary<string, object> {
                    { "Ordinal", car }, { "FrontSpringDamperPhysicsID", front }, { "RearSpringDamperPhysicsID", rear } });
                Physics(target, fm2, "List_SpringDamperPhysics", "SpringDamperPhysicsID", Value(springs, row, "FrontSpringDamperPhysicsID"), front, car);
                Physics(target, fm2, "List_SpringDamperPhysics", "SpringDamperPhysicsID", Value(springs, row, "RearSpringDamperPhysicsID"), rear, car);
            }
            n = 0;
            if (Has(fm2, "List_UpgradeAntiSway") && Has(target, "List_UpgradeAntiSwayFront"))
            {
                SqliteDatabase.Result bars = fm2.Query("SELECT * FROM List_UpgradeAntiSway WHERE Ordinal = ? ORDER BY Id", from);
                foreach (object[] row in bars.Rows)
                {
                    n++;
                    long front = car * 1000 + n, rear = car * 1000 + 100 + n, id = UpgradeId(bars, row, from, car, n);
                    Insert(target, "List_UpgradeAntiSwayFront", bars, row, "Id", id, new Dictionary<string, object> { { "Ordinal", car }, { "AntiSwayPhysicsID", front } });
                    Insert(target, "List_UpgradeAntiSwayRear", bars, row, "Id", id, new Dictionary<string, object> { { "Ordinal", car }, { "AntiSwayPhysicsID", rear } });
                    Physics(target, fm2, "List_AntiSwayPhysics", "AntiSwayPhysicsID", Value(bars, row, "FrontAntiSwayPhysicsID"), front, car);
                    Physics(target, fm2, "List_AntiSwayPhysics", "AntiSwayPhysicsID", Value(bars, row, "RearAntiSwayPhysicsID"), rear, car);
                }
            }
            log.Add("Suspension: " + springs.Rows.Count + " spring/damper rows, " + n + " anti-roll bar rows");
        }

        private static void Physics(SqliteDatabase target, SqliteDatabase fm2, string table, string key, object oldId, long newId, long car)
        {
            if (oldId == null)
            {
                return;
            }
            SqliteDatabase.Result src = fm2.Query("SELECT * FROM " + table + " WHERE " + key + " = ?", oldId);
            if (src.Rows.Count == 0)
            {
                return;
            }
            Dictionary<string, object> extra = new Dictionary<string, object> { { "Ordinal", car } };
            if (table == "List_SpringDamperPhysics")
            {
                extra["MaxStretchDeltaFromRideHeight"] = 0.5;   // 0.5 in all 4 games' FM4 rows; FM2 has AxleInertia instead
                // Supra RZ 98, stock rows FM2 vs FM4: spring x1.0277, rebound x1.1405, bump = 2/3 rebound (FM2: 1/2),
                // clamps 30 -> 10, bumpstop 120/3 -> 200/2, MaxCompressHeight 0.0911 -> 0.1351 (x1.483)
                object[] r = src.Rows[0];
                foreach (string c in new string[] { "Def", "Min", "Max" })
                {
                    extra[c + "SpringRate"] = Num(src, r, c + "SpringRate") * 1.0277;
                    double rebound = Num(src, r, c + "DampenReboundRate") * 1.1405;
                    extra[c + "DampenReboundRate"] = rebound;
                    extra[c + "DampenBumpRate"] = rebound * 2.0 / 3.0;
                }
                extra["DampenBumpClamp"] = 10.0;
                extra["DampenReboundClamp"] = 10.0;
                extra["BumpstopStiffness"] = 200.0;
                extra["BumpstopDamping"] = 2.0;
                extra["MaxCompressHeight"] = Num(src, r, "MaxCompressHeight") * 1.483;
            }
            if (table == "List_AeroPhysics")
            {
                // FM2 drag columns go to 84.5 / 169, FM4's to 20 / 87.69 (downforce is in range): clamp to FM4's maxima
                object[] r = src.Rows[0];
                extra["Drag0"] = Math.Min(Num(src, r, "Drag0"), 20.0);
                extra["Drag1"] = Math.Min(Num(src, r, "Drag1"), 87.6899);
            }
            Insert(target, table, src, src.Rows[0], key, newId, extra);
        }

        // Id of an upgrade row: car * 1000 + (FM2 Id - FM2 car * 1000) when that is a level slot, else the running number.
        private static long UpgradeId(SqliteDatabase.Result result, object[] row, long from, long car, int n)
        {
            long slot = Convert.ToInt64(Value(result, row, "Id")) - from * 1000;
            return car * 1000 + ((slot >= 0 && slot < 100) ? slot : n);
        }

        // Replaces the target's rows of the key with the FM2 rows (all columns the two share). only = FM2 columns
        // whose value goes to the FM4 column of the same role: the first name is copied as the FM4 column named
        // like the target's single column ending in it (tire width / rim size front and rear).
        internal static void CopyRows(SqliteDatabase target, SqliteDatabase fm2, string srcTable, string srcKey, long srcValue, string dstTable, string dstKey, long dstValue, long idBase, string[] only, List<string> log)
        {
            CopyRows(target, fm2, srcTable, srcKey, srcValue, dstTable, dstKey, dstValue, idBase, only, null, log);
        }

        // constants = FM4 columns FM2 lacks and their value in every row.
        internal static void CopyRows(SqliteDatabase target, SqliteDatabase fm2, string srcTable, string srcKey, long srcValue, string dstTable, string dstKey, long dstValue, long idBase, string[] only, Dictionary<string, object> constants, List<string> log)
        {
            if (!Has(fm2, srcTable) || !Has(target, dstTable))
            {
                return;
            }
            target.Execute("DELETE FROM " + dstTable + " WHERE " + SqliteDatabase.Quote(dstKey) + " = ?", dstValue);
            SqliteDatabase.Result src = fm2.Query("SELECT * FROM " + srcTable + " WHERE " + srcKey + " = ? ORDER BY Id", srcValue);
            int n = 0;
            foreach (object[] row in src.Rows)
            {
                n++;
                long slot = Convert.ToInt64(Value(src, row, "Id")) - srcValue * 1000;
                long id = idBase + ((slot >= 0 && slot < 1000) ? slot : n);
                Dictionary<string, object> extra = new Dictionary<string, object>();
                extra[dstKey] = dstValue;
                if (constants != null)
                {
                    foreach (KeyValuePair<string, object> pair in constants)
                    {
                        extra[pair.Key] = pair.Value;
                    }
                }
                if (only != null)
                {
                    foreach (string column in only)
                    {
                        extra[column] = Value(src, row, column);
                    }
                }
                Insert(target, dstTable, src, row, "Id", id, extra);
            }
            if (n > 0)
            {
                log.Add(dstTable + ": " + n + " rows");
            }
        }


        // Engine part tables FM2 has no source for (valves, pistons / compression, oil cooling): the car gets its own
        // rows, levels 0-3 (0 = stock), every column the median of the FM4 rows of that level (all engines; manufacturer 255 = generic), so the
        // values are FM4's typical ones and not another engine's. WeightDistDiff = MassDiff * side / (CurbWeight * 100)
        // like the FM4 rows (side +0.3 front engine, -0.3 otherwise).
        internal static void GenerateEngineParts(SqliteDatabase target, long car, string[] tables, List<string> log)
        {
            SqliteDatabase.Result info = target.Query("SELECT CurbWeight, EnginePlacementID FROM Data_Car WHERE Id = ?", car);
            double weight = (info.Rows.Count > 0 && info.Rows[0][0] != null) ? Convert.ToDouble(info.Rows[0][0], System.Globalization.CultureInfo.InvariantCulture) * 100 : 0;
            double side = (info.Rows.Count > 0 && info.Rows[0][1] != null && Convert.ToInt64(info.Rows[0][1]) == 1) ? 0.3 : -0.3;
            foreach (string table in tables)
            {
                if (!Has(target, table))
                {
                    continue;
                }
                target.Execute("DELETE FROM " + table + " WHERE EngineID = ?", car);   // a car ported earlier left its own rows
                SqliteDatabase.Result all = target.Query("SELECT * FROM " + table + " WHERE Level BETWEEN 0 AND 3 AND EngineID <> ?", car);
                int count = 0;
                for (int level = 0; level <= 3; level++)
                {
                    List<object[]> rows = all.Rows.FindAll(delegate(object[] r) { return Convert.ToInt64(Value(all, r, "Level")) == level && (Convert.ToInt64(Value(all, r, "IsStock")) == 1) == (level == 0); });
                    if (rows.Count == 0)
                    {
                        continue;
                    }
                    Dictionary<string, object> values = new Dictionary<string, object>();
                    values["EngineID"] = car;
                    values["Level"] = (long) level;
                    values["IsStock"] = (level == 0) ? 1L : 0L;
                    foreach (string column in all.Columns)
                    {
                        if (column == "Id" || values.ContainsKey(column))
                        {
                            continue;
                        }
                        List<double> numbers = new List<double>();
                        foreach (object[] r in rows)
                        {
                            object v = Value(all, r, column);
                            if (v != null)
                            {
                                numbers.Add(Convert.ToDouble(v, System.Globalization.CultureInfo.InvariantCulture));
                            }
                        }
                        if (numbers.Count == 0)
                        {
                            continue;
                        }
                        numbers.Sort();
                        double median = (numbers.Count % 2 == 1) ? numbers[numbers.Count / 2] : (numbers[numbers.Count / 2 - 1] + numbers[numbers.Count / 2]) / 2;
                        values[column] = (column == "ManufacturerID") ? (object) ((level == 0) ? 0.0 : 255.0) : ((column == "Price") ? (object) Math.Round(median) : median);
                    }
                    if (values.ContainsKey("MassDiff") && Array.IndexOf(all.Columns, "WeightDistDiff") >= 0 && weight > 0)
                    {
                        values["WeightDistDiff"] = Convert.ToDouble(values["MassDiff"], System.Globalization.CultureInfo.InvariantCulture) * side / weight;
                    }
                    Insert(target, table, all, rows[0], "Id", car * 1000 + level, values);
                    count++;
                }
                log.Add(table + ": " + count + " rows made (median of the FM4 rows per level; FM2 has none)");
            }
        }

        // INSERT of the columns the row shares with the target table; extra overrides / adds values; the key column = id.
        internal static void Insert(SqliteDatabase target, string table, SqliteDatabase.Result src, object[] row, string idColumn, long id, Dictionary<string, object> extra)
        {
            List<string> columns = new List<string>();
            List<string> marks = new List<string>();
            List<object> values = new List<object>();
            SqliteDatabase.Result info = target.Query("PRAGMA table_info(" + SqliteDatabase.Quote(table) + ")");
            foreach (object[] column in info.Rows)
            {
                string name = (string) column[1];
                object value;
                if (string.Equals(name, idColumn, StringComparison.OrdinalIgnoreCase))
                {
                    value = id;
                }
                else if (extra != null && TryGet(extra, name, out value))
                {
                }
                else
                {
                    int index = Array.FindIndex(src.Columns, delegate(string c) { return string.Equals(c, name, StringComparison.OrdinalIgnoreCase); });
                    if (index < 0)
                    {
                        // column FM2 lacks (e.g. torque curve v86..v150): FM4 rows hold 0, never NULL
                        string type = column[2] as string ?? "";
                        if (type.IndexOf("INT", StringComparison.OrdinalIgnoreCase) < 0 && type.IndexOf("REAL", StringComparison.OrdinalIgnoreCase) < 0 && type.IndexOf("FLOA", StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            continue;
                        }
                        value = 0;
                    }
                    else
                    {
                        value = row[index];
                    }
                }
                columns.Add(SqliteDatabase.Quote(name));
                marks.Add("?");
                values.Add(value);
            }
            target.Execute("INSERT INTO " + SqliteDatabase.Quote(table) + " (" + string.Join(", ", columns.ToArray()) + ") VALUES (" + string.Join(", ", marks.ToArray()) + ")", values.ToArray());
        }

        private static bool TryGet(Dictionary<string, object> map, string name, out object value)
        {
            foreach (KeyValuePair<string, object> pair in map)
            {
                if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = pair.Value;
                    return true;
                }
            }
            value = null;
            return false;
        }

        private static double Num(SqliteDatabase.Result result, object[] row, string column)
        {
            object v = Value(result, row, column);
            return v == null ? 0 : Convert.ToDouble(v);
        }

        internal static object Value(SqliteDatabase.Result result, object[] row, string column)
        {
            int index = Array.FindIndex(result.Columns, delegate(string c) { return string.Equals(c, column, StringComparison.OrdinalIgnoreCase); });
            return (index < 0) ? null : row[index];
        }

        internal static bool Has(SqliteDatabase db, string table)
        {
            return Convert.ToInt64(db.Scalar("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = ? COLLATE NOCASE", table)) > 0;
        }
    }
}
