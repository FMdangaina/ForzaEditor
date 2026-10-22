namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;

    // Reusable diagnostic queries for the SQL console (ROADMAP 1.4.6): every one is a SELECT, so running it
    // changes nothing. Built from the open database, so the table and column names are the ones it has.
    public static class SqlDiagnostics
    {
        // A query Forza Studio answers itself (the result is built in code, not by a SELECT): Build is
        // not null and Sql holds a note.
        public delegate SqliteDatabase.Result Builder(SqliteDatabase db, StringTable names);

        public sealed class Query
        {
            public readonly string Name;
            public readonly string Sql;
            public readonly Builder Build;

            public Query(string name, string sql)
            {
                this.Name = name;
                this.Sql = sql;
            }

            public Query(string name, string note, Builder build)
            {
                this.Name = name;
                this.Sql = note;
                this.Build = build;
            }

            public override string ToString()
            {
                return this.Name;
            }
        }

        public static List<Query> For(SqliteDatabase db, ForzaGame game)
        {
            List<Query> list = new List<Query>();
            Dictionary<string, List<string>> schema = DbIds.Schema(db);
            List<string> tables = new List<string>(schema.Keys);
            tables.Sort(StringComparer.OrdinalIgnoreCase);

            // EventRestrictions.Value, decoded (EventRestriction.cs).
            if (schema.ContainsKey("EventRestrictions") && schema.ContainsKey("Events"))
            {
                list.Add(new Query("Event restrictions decoded (the Value of EventRestrictions in words)", "-- built by Forza Studio (EventRestriction.Decode), not a SELECT: press Run", DecodedRestrictions));
            }

            // Rows per table.
            List<string> parts = new List<string>();
            foreach (string table in tables)
            {
                parts.Add("SELECT " + Text(table) + " AS tbl, count(*) AS rows FROM " + SqliteDatabase.Quote(table));
            }
            list.Add(new Query("Rows in every table", Union(parts) + " ORDER BY rows DESC"));

            // Keys twice.
            parts = new List<string>();
            foreach (string table in tables)
            {
                string key = DbIds.KeyColumn(db, table, schema[table]);
                if (key != null && !DeclaredKey(db, table))
                {
                    parts.Add("SELECT " + Text(table) + " AS tbl, " + SqliteDatabase.Quote(key) + " AS key, count(*) AS n FROM " + SqliteDatabase.Quote(table) + " GROUP BY " + SqliteDatabase.Quote(key) + " HAVING count(*) > 1");
                }
            }
            if (parts.Count > 0)
            {
                list.Add(new Query("Keys twice, tables without a declared primary key (StartGridPositions, CarPartPositions, Combo_Colors... repeat their first column by design)", Union(parts)));
            }

            // Dangling references.
            List<string> strict = new List<string>();
            List<string> all = new List<string>();
            foreach (GameDbSchema.Relation relation in GameDbSchema.Relations)
            {
                if (!schema.ContainsKey(relation.Table) || !schema.ContainsKey(relation.Target))
                {
                    continue;
                }
                bool columns = true;
                foreach (string c in relation.Columns)
                {
                    columns = columns && schema[relation.Table].Exists(delegate(string x) { return string.Equals(x, c, StringComparison.OrdinalIgnoreCase); });
                }
                foreach (string c in relation.TargetColumns)
                {
                    columns = columns && schema[relation.Target].Exists(delegate(string x) { return string.Equals(x, c, StringComparison.OrdinalIgnoreCase); });
                }
                if (!columns)
                {
                    continue;
                }
                string select = "SELECT " + Text(relation.ToString()) + " AS relation, count(*) AS dangling FROM " + SqliteDatabase.Quote(relation.Table) + " WHERE " + GameDatabase.DanglingWhere(relation);
                all.Add(select);
                if (relation.Strict)
                {
                    strict.Add(select);
                }
            }
            if (strict.Count > 0)
            {
                list.Add(new Query("Dangling ids, strict relations (none in the original FM4: every row here is an edit's)", "SELECT * FROM (" + Union(strict) + ") WHERE dangling > 0"));
            }
            if (all.Count > 0)
            {
                list.Add(new Query("Dangling ids, every relation (the loose ones already dangle in FM4)", "SELECT * FROM (" + Union(all) + ") WHERE dangling > 0"));
            }

            // Cars.
            if (schema.ContainsKey("Data_Car"))
            {
                if (schema["Data_Car"].Exists(delegate(string c) { return string.Equals(c, "ModelId", StringComparison.OrdinalIgnoreCase); }))
                {
                    list.Add(new Query("Cars with ModelId different from Id", "SELECT Id, MediaName, ModelId FROM Data_Car WHERE ModelId <> Id"));
                }
                if (schema.ContainsKey("Data_CarBody"))
                {
                    list.Add(new Query("Cars without their Data_CarBody row (Id * 1000)", "SELECT Id, MediaName FROM Data_Car WHERE Id * 1000 NOT IN (SELECT Id FROM Data_CarBody)"));
                }
                list.Add(new Query("MediaName on more than one car", "SELECT MediaName, count(*) AS n, group_concat(Id) AS ids FROM Data_Car GROUP BY MediaName COLLATE NOCASE HAVING count(*) > 1"));
                list.Add(new Query("Cars above FM4's last official car (installed by Forza Studio or a pack)", "SELECT Id, MediaName FROM Data_Car WHERE Id > " + GameIdLimits.Max(game, "Data_Car", "Id").ToString(CultureInfo.InvariantCulture) + " ORDER BY Id"));
                list.Add(new Query("Rows of a car in every table (change the Id)", CarRows(db, schema, tables)));
            }

            // Keys above FM4's largest: the rows an edit added.
            parts = new List<string>();
            foreach (string table in tables)
            {
                string key = DbIds.KeyColumn(db, table, schema[table]);
                long limit = (key == null) ? -1 : GameIdLimits.Max(game, table, key);
                if (limit >= 0)
                {
                    string k = SqliteDatabase.Quote(key);
                    parts.Add("SELECT " + Text(table) + " AS tbl, count(*) AS rows, min(" + k + ") AS first, max(" + k + ") AS last FROM " + SqliteDatabase.Quote(table) + " WHERE " + k + " > " + limit.ToString(CultureInfo.InvariantCulture));
                }
            }
            if (parts.Count > 0)
            {
                list.Add(new Query("Rows with a key above FM4's largest (what was added)", "SELECT * FROM (" + Union(parts) + ") WHERE rows > 0"));
            }
            return list;
        }

        private static SqliteDatabase.Result DecodedRestrictions(SqliteDatabase db, StringTable names)
        {
            SqliteDatabase.Result result = new SqliteDatabase.Result();
            result.Columns = new string[] { "EventID", "Event", "Value", "Restriction" };
            foreach (object[] row in db.Query("SELECT r.EventID, e.Name, r.Value FROM EventRestrictions r LEFT JOIN Events e ON e.Id = r.EventID ORDER BY r.EventID, r.ID").Rows)
            {
                string name = row[1] as string;
                string shown = (names != null && name != null) ? names.Lookup("Events", "Name", name) : null;
                string value = row[2] as string;
                result.Rows.Add(new object[] { row[0], string.IsNullOrEmpty(shown) ? name : shown, value, EventRestriction.Describe(value, db, names) });
            }
            return result;
        }

        // The tables a car's Id is in, as SELECT count(*) per table, for car 1 (the user changes it).
        private static string CarRows(SqliteDatabase db, Dictionary<string, List<string>> schema, List<string> tables)
        {
            List<string> parts = new List<string>();
            foreach (string[] column in DbIds.CarColumns(schema))
            {
                parts.Add("SELECT " + Text(column[0]) + " AS tbl, count(*) AS rows FROM " + SqliteDatabase.Quote(column[0]) + " WHERE " + SqliteDatabase.Quote(column[1]) + " = 1");
            }
            return "SELECT * FROM (" + Union(parts) + ") WHERE rows > 0 ORDER BY tbl";
        }

        // The table has a PRIMARY KEY, so SQLite itself refuses a key twice.
        private static bool DeclaredKey(SqliteDatabase db, string table)
        {
            foreach (object[] row in db.Query("PRAGMA table_info(" + SqliteDatabase.Quote(table) + ")").Rows)
            {
                if (row[5] is long && (long) row[5] > 0)
                {
                    return true;
                }
            }
            return false;
        }

        private static string Union(List<string> parts)
        {
            return string.Join("\nUNION ALL ", parts.ToArray());
        }

        private static string Text(string value)
        {
            return "'" + value.Replace("'", "''") + "'";
        }
    }
}
