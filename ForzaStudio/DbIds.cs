namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;

    // The Ids Forza Studio gives to new database rows, and the checks before a port is written
    // (ROADMAP C2 / C3, rules decided by the user 2026-10-20):
    //   cars    a ported car always gets an Id of its own from 4000 up, never the source game's: the Id
    //           it already has in the target (an earlier install; old installs that kept the source Id
    //           keep it) or in an installed Forza Studio pack, else the first Id from 4000 that neither
    //           the target, the source nor an installed pack uses. Car Ids are one list across the games
    //           (FM4 / FH1 / FH2: the same Id is the same car in all 344 shared cases), which is why
    //           ports used to keep them.
    //   rows    tables with a reserved range (wheels, events, races, AI participants) take the next Id
    //           in it, the others the next one after the largest. Ids built by a formula keep it
    //           (car * 1000 + n, engine * 1000 + level, 100000 * k + car for drive conversions, offers
    //           from 91).
    // The first car Id and the ranges are in Port.ini [Ids] (PortSettings). Car Ids 4000 / 4001 work in
    // game (Data_Car.ModelId = Id, 2026-10-20). Largest Ids of the games (FM4 / FH1 / FH2): List_Wheels
    // 11066 / 11153 / 5011150, Events 726 / 246 / 911, Races 3675 / 182 / 716, EventParticipants 72470 /
    // 1337 / 4754; the row ranges are not checked in game yet.
    public static class DbIds
    {
        // The first car Id (Port.ini [Ids] FirstCar, default 4000).
        public static long FirstCar
        {
            get { return Math.Max(1, PortSettings.GetLong("Ids", "FirstCar", 4000)); }
        }

        // In the other tables a car's Id is one of these columns (as CarPort.PortDatabase copies them).
        private static readonly string[] CarColumnNames = { "Ordinal", "CarId", "CarID", "CarOrdinal" };

        // The first Id of the table's reserved range (Port.ini [Ids] <table>), or 0 when it has none.
        public static long RangeStart(string table)
        {
            return Math.Max(0, PortSettings.GetLong("Ids", table, 0));
        }

        // The Id for a new row: the next one in the table's range, else the next after the largest.
        public static long Next(SqliteDatabase db, string table, string column)
        {
            long start = RangeStart(table);
            string c = SqliteDatabase.Quote(column);
            object max = db.Scalar("SELECT max(" + c + ") FROM " + SqliteDatabase.Quote(table) + (start > 0 ? " WHERE " + c + " >= " + start.ToString(CultureInfo.InvariantCulture) : ""));
            if (max == null)
            {
                return (start > 0) ? start : 1;
            }
            return Convert.ToInt64(max) + 1;
        }

        #region Cars

        // The merge databases of the packages in Xenia's content folder (every db\patch\*_merge.slt).
        public static List<string> PackDatabases(string content)
        {
            List<string> found = new List<string>();
            if (string.IsNullOrEmpty(content) || !Directory.Exists(content))
            {
                return found;
            }
            try
            {
                found.AddRange(Directory.GetFiles(content, "*_merge.slt", SearchOption.AllDirectories));
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            return found;
        }

        // The Id a ported car gets in the target, and why.
        public static long CarId(SqliteDatabase target, SqliteDatabase source, string mediaName, List<string> packs, out string reason)
        {
            object installed = target.Scalar("SELECT Id FROM Data_Car WHERE MediaName = ? COLLATE NOCASE", mediaName);
            if (installed != null)
            {
                reason = "its id in the target (installed earlier)";
                return Convert.ToInt64(installed);
            }
            List<long> taken = new List<long>();
            foreach (string pack in packs)
            {
                SqliteDatabase merge;
                try
                {
                    merge = SqliteDatabase.FromBytes(File.ReadAllBytes(pack));
                }
                catch (Exception)
                {
                    continue;
                }
                using (merge)
                {
                    if (!HasTable(merge, "Data_Car"))
                    {
                        continue;
                    }
                    foreach (object[] row in merge.Query("SELECT Id, MediaName FROM Data_Car").Rows)
                    {
                        long id = Convert.ToInt64(row[0]);
                        if (string.Equals(row[1] as string, mediaName, StringComparison.OrdinalIgnoreCase) && !Taken(target, id))
                        {
                            reason = "its id in the installed pack " + Path.GetFileName(pack);
                            return id;
                        }
                        taken.Add(id);
                    }
                }
            }
            long first = FirstCar;
            for (long id = first; ; id++)
            {
                if (!taken.Contains(id) && !Taken(target, id) && !Taken(source, id))
                {
                    reason = string.Format(CultureInfo.InvariantCulture, "first id from {0} ({1}) free in the target, the source and {2} installed pack(s)", first, PortSettings.FileName, packs.Count);
                    return id;
                }
            }
        }

        // A car Id some row already uses: a car, or its body row (Data_CarBody.Id = car * 1000).
        private static bool Taken(SqliteDatabase db, long id)
        {
            if (Convert.ToInt64(db.Scalar("SELECT count(*) FROM Data_Car WHERE Id = ?", id)) > 0)
            {
                return true;
            }
            return HasTable(db, "Data_CarBody") && Convert.ToInt64(db.Scalar("SELECT count(*) FROM Data_CarBody WHERE Id = ?", id * 1000)) > 0;
        }

        // Rewrites car 'from' as car 'to' in a database (CarPort: the in-memory copy of the source, so
        // that the whole port then reads and writes the new Id). Car columns take the new Id; the keys
        // car * 1000 + n of the car's rows (upgrades, colours, part positions, physics, body) and of
        // the rows they point at in that range (Data_CarBody, physics, rear wing aero) become
        // to * 1000 + n, with every reference to them. Values outside the car's range are left alone
        // (shared aero rows, engine ids, torque curves engine * 1000 + level).
        public static List<string> RenumberCar(SqliteDatabase db, long from, long to)
        {
            List<string> lines = new List<string>();
            if (from == to)
            {
                return lines;
            }
            Dictionary<string, List<string>> schema = Schema(db);
            List<string[]> carColumns = CarColumns(schema);
            long low = from * 1000;
            long high = low + 999;

            // Keys to move: table.column -> old values.
            Dictionary<string, List<long>> keys = new Dictionary<string, List<long>>(StringComparer.OrdinalIgnoreCase);
            List<string> owners = new List<string>();
            foreach (string[] car in carColumns)
            {
                if (!owners.Contains(car[0]))
                {
                    owners.Add(car[0]);
                }
                string key = KeyColumn(db, car[0], schema[car[0]]);
                if (key == null || string.Equals(key, car[1], StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                AddKeys(keys, car[0], key, db.Query("SELECT DISTINCT " + SqliteDatabase.Quote(key) + " FROM " + SqliteDatabase.Quote(car[0]) + " WHERE " + SqliteDatabase.Quote(car[1]) + " = ? AND " + SqliteDatabase.Quote(key) + " BETWEEN ? AND ?", from, low, high));
            }
            foreach (KeyValuePair<string, List<string>> table in schema)
            {
                // Body part tables have no car column: Id = car * 1000 + level.
                if (table.Key.StartsWith("List_UpgradeCarBody", StringComparison.Ordinal) && Has(table.Value, "Id"))
                {
                    if (!owners.Contains(table.Key))
                    {
                        owners.Add(table.Key);
                    }
                    AddKeys(keys, table.Key, "Id", db.Query("SELECT DISTINCT Id FROM " + SqliteDatabase.Quote(table.Key) + " WHERE Id BETWEEN ? AND ?", low, high));
                }
            }
            foreach (string owner in owners)
            {
                foreach (GameDbSchema.Relation relation in GameDbSchema.From(owner))
                {
                    List<string> targetColumns;
                    if (relation.IsComposite || !Has(schema[owner], relation.Column) || !schema.TryGetValue(relation.Target, out targetColumns) || !Has(targetColumns, relation.TargetColumn))
                    {
                        continue;
                    }
                    string c = SqliteDatabase.Quote(relation.Column);
                    AddKeys(keys, relation.Target, relation.TargetColumn, db.Query("SELECT DISTINCT " + c + " FROM " + SqliteDatabase.Quote(owner) + " WHERE " + c + " BETWEEN ? AND ?", low, high));
                }
            }

            int moved = 0;
            List<string> movedTables = new List<string>();
            foreach (KeyValuePair<string, List<long>> pair in keys)
            {
                int dot = pair.Key.IndexOf('.');
                string table = pair.Key.Substring(0, dot);
                string column = pair.Key.Substring(dot + 1);
                string t = SqliteDatabase.Quote(table);
                string c = SqliteDatabase.Quote(column);
                foreach (long old in pair.Value)
                {
                    long now = (to * 1000) + (old - low);
                    if (Convert.ToInt64(db.Scalar("SELECT count(*) FROM " + t + " WHERE " + c + " = ?", now)) > 0)
                    {
                        throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "{0}.{1}: id {2} (car {3}) is already used, the car cannot take id {4}.", table, column, now, from, to));
                    }
                    db.Execute("UPDATE " + t + " SET " + c + " = ? WHERE " + c + " = ?", now, old);
                    foreach (GameDbSchema.Relation relation in GameDbSchema.Into(table))
                    {
                        List<string> columns;
                        if (relation.IsComposite || !string.Equals(relation.TargetColumn, column, StringComparison.OrdinalIgnoreCase)
                            || !schema.TryGetValue(relation.Table, out columns) || !Has(columns, relation.Column))
                        {
                            continue;
                        }
                        db.Execute("UPDATE " + SqliteDatabase.Quote(relation.Table) + " SET " + SqliteDatabase.Quote(relation.Column) + " = ? WHERE " + SqliteDatabase.Quote(relation.Column) + " = ?", now, old);
                    }
                    moved++;
                }
                movedTables.Add(string.Format(CultureInfo.InvariantCulture, "{0}.{1} {2}", table, column, pair.Value.Count));
            }

            int rows = 0;
            int tables = 0;
            foreach (string[] car in carColumns)
            {
                int changed = db.Execute("UPDATE " + SqliteDatabase.Quote(car[0]) + " SET " + SqliteDatabase.Quote(car[1]) + " = ? WHERE " + SqliteDatabase.Quote(car[1]) + " = ?", to, from);
                if (changed > 0)
                {
                    rows += changed;
                    tables++;
                }
            }
            db.Execute("UPDATE Data_Car SET Id = ? WHERE Id = ?", to, from);
            // Data_Car.ModelId = Id on all 503 FM4 cars; a car whose ModelId kept the old Id does not
            // show in the game (BMW_X5_09 as 4001 with ModelId 1079, 2026-10-20).
            if (Has(schema["Data_Car"], "ModelId"))
            {
                db.Execute("UPDATE Data_Car SET ModelId = ? WHERE Id = ? AND ModelId = ?", to, to, from);
            }
            lines.Add(string.Format(CultureInfo.InvariantCulture, "source car {0} renumbered as {1}: {2} rows in {3} tables, {4} keys car * 1000 + n", from, to, rows, tables, moved));
            if (movedTables.Count > 0)
            {
                lines.Add("  keys moved: " + string.Join(", ", movedTables.ToArray()));
            }
            return lines;
        }

        // C3: the car's rows in the target before anything is written. Errors name the table and column.
        public static List<string> CheckCar(SqliteDatabase db, long car, long sourceId, string mediaName)
        {
            List<string> errors = new List<string>();
            Dictionary<string, List<string>> schema = Schema(db);
            long byId = Convert.ToInt64(db.Scalar("SELECT count(*) FROM Data_Car WHERE Id = ?", car));
            long byName = Convert.ToInt64(db.Scalar("SELECT count(*) FROM Data_Car WHERE MediaName = ? COLLATE NOCASE", mediaName));
            if (byId != 1 || byName != 1 || !string.Equals(db.Scalar("SELECT MediaName FROM Data_Car WHERE Id = ?", car) as string, mediaName, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(string.Format(CultureInfo.InvariantCulture, "Data_Car.Id: {0} rows with id {1}, {2} named {3} (one row expected)", byId, car, byName, mediaName));
            }
            if (Has(schema["Data_Car"], "ModelId"))
            {
                object model = db.Scalar("SELECT ModelId FROM Data_Car WHERE Id = ?", car);
                if (model != null && Convert.ToInt64(model) != car)
                {
                    errors.Add(string.Format(CultureInfo.InvariantCulture, "Data_Car.ModelId: {0}, not the car's id {1} (FM4: ModelId = Id on every car)", model, car));
                }
            }
            if (HasTable(db, "Data_CarBody") && Convert.ToInt64(db.Scalar("SELECT count(*) FROM Data_CarBody WHERE Id = ?", car * 1000)) == 0)
            {
                errors.Add(string.Format(CultureInfo.InvariantCulture, "Data_CarBody.Id: no row {0} (car * 1000)", car * 1000));
            }
            foreach (string[] column in CarColumns(schema))
            {
                string key = KeyColumn(db, column[0], schema[column[0]]);
                if (key == null || string.Equals(key, column[1], StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                string t = SqliteDatabase.Quote(column[0]);
                string k = SqliteDatabase.Quote(key);
                string mine = " WHERE " + SqliteDatabase.Quote(column[1]) + " = ?";
                object twice = db.Scalar("SELECT " + k + " FROM " + t + mine + " GROUP BY " + k + " HAVING count(*) > 1 LIMIT 1", car);
                if (twice != null)
                {
                    errors.Add(string.Format(CultureInfo.InvariantCulture, "{0}.{1}: id {2} twice among the car's rows", column[0], key, twice));
                }
                if (sourceId != car)
                {
                    object old = db.Scalar("SELECT " + k + " FROM " + t + mine + " AND " + k + " BETWEEN ? AND ? LIMIT 1", car, sourceId * 1000, (sourceId * 1000) + 999);
                    if (old != null)
                    {
                        errors.Add(string.Format(CultureInfo.InvariantCulture, "{0}.{1}: id {2} still built from the source id {3}", column[0], key, old, sourceId));
                    }
                }
            }
            return errors;
        }

        #endregion

        private static void AddKeys(Dictionary<string, List<long>> keys, string table, string column, SqliteDatabase.Result values)
        {
            string name = table + "." + column;
            List<long> list;
            if (!keys.TryGetValue(name, out list))
            {
                list = new List<long>();
            }
            foreach (object[] row in values.Rows)
            {
                if (row[0] is long && !list.Contains((long) row[0]))
                {
                    list.Add((long) row[0]);
                }
            }
            if (list.Count > 0)
            {
                keys[name] = list;
            }
        }

        // Every table.column holding a car's Id: the car columns by name, plus the relations into Data_Car.
        private static List<string[]> CarColumns(Dictionary<string, List<string>> schema)
        {
            List<string[]> found = new List<string[]>();
            foreach (KeyValuePair<string, List<string>> table in schema)
            {
                if (table.Key == "Data_Car")
                {
                    continue;
                }
                foreach (string column in table.Value)
                {
                    if (Array.Exists(CarColumnNames, delegate(string c) { return string.Equals(c, column, StringComparison.OrdinalIgnoreCase); }))
                    {
                        found.Add(new string[] { table.Key, column });
                    }
                }
            }
            foreach (GameDbSchema.Relation relation in GameDbSchema.Into("Data_Car"))
            {
                List<string> columns;
                if (relation.IsComposite || relation.Table == "Data_Car" || !schema.TryGetValue(relation.Table, out columns) || !Has(columns, relation.Column)
                    || found.Exists(delegate(string[] f) { return string.Equals(f[0], relation.Table, StringComparison.OrdinalIgnoreCase) && string.Equals(f[1], relation.Column, StringComparison.OrdinalIgnoreCase); }))
                {
                    continue;
                }
                found.Add(new string[] { relation.Table, relation.Column });
            }
            return found;
        }

        // The table's single-column key (PRAGMA pk), else a first column named Id; null otherwise.
        private static string KeyColumn(SqliteDatabase db, string table, List<string> columns)
        {
            string key = null;
            int count = 0;
            foreach (object[] row in db.Query("PRAGMA table_info(" + SqliteDatabase.Quote(table) + ")").Rows)
            {
                if (row[5] is long && (long) row[5] > 0)
                {
                    key = (string) row[1];
                    count++;
                }
            }
            if (count == 1)
            {
                return key;
            }
            return (count == 0 && columns.Count > 0 && string.Equals(columns[0], "Id", StringComparison.OrdinalIgnoreCase)) ? columns[0] : null;
        }

        private static Dictionary<string, List<string>> Schema(SqliteDatabase db)
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

        private static bool HasTable(SqliteDatabase db, string table)
        {
            return db.Scalar("SELECT name FROM sqlite_master WHERE type = 'table' AND name = ?", table) != null;
        }

        private static bool Has(List<string> columns, string column)
        {
            return columns.Exists(delegate(string c) { return string.Equals(c, column, StringComparison.OrdinalIgnoreCase); });
        }
    }
}
