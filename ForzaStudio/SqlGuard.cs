namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text.RegularExpressions;

    // Runs one SQL statement the user typed (ROADMAP 18.5). SELECT / PRAGMA read; INSERT, UPDATE and
    // DELETE write, on any table. Before an INSERT or UPDATE touches the database the statement runs on
    // a copy: every key the statement creates in the table must be above the largest key of that column
    // in FM4 and its DLC (docs/GameIdLimits.json), so a new row can never take an Id that an official
    // package uses (rule decided 2026-10-22). The check only reports; DELETE and UPDATE of other columns
    // are never refused. Anything else (DROP, ALTER, CREATE, ATTACH...) is refused.
    public static class SqlGuard
    {
        private static readonly Regex Write = new Regex(@"^\s*(INSERT(?:\s+OR\s+\w+)?\s+INTO|REPLACE\s+INTO|UPDATE(?:\s+OR\s+\w+)?|DELETE\s+FROM)\s+[""`\[]?(\w+)[""`\]]?", RegexOptions.IgnoreCase);
        private static readonly Regex Read = new Regex(@"^\s*(SELECT|WITH|PRAGMA\s+table_info|PRAGMA\s+foreign_key_list)\b", RegexOptions.IgnoreCase);

        public class Outcome
        {
            public SqliteDatabase.Result Rows;      // a read statement's rows
            public string Table;                    // the table a write statement changed
            public int Changed;                     // rows it changed
            public string Message;
        }

        // Throws InvalidOperationException with the reason when the statement is refused.
        public static Outcome Run(SqliteDatabase db, string sql, ForzaGame game)
        {
            sql = (sql ?? "").Trim();
            while (sql.EndsWith(";", StringComparison.Ordinal))
            {
                sql = sql.Substring(0, sql.Length - 1).TrimEnd();
            }
            if (sql.Length == 0)
            {
                throw new InvalidOperationException("Type a statement.");
            }
            if (sql.IndexOf(';') >= 0)
            {
                throw new InvalidOperationException("One statement at a time.");
            }
            Outcome outcome = new Outcome();
            if (Read.IsMatch(sql))
            {
                outcome.Rows = db.Query(sql);
                outcome.Message = outcome.Rows.Rows.Count + " rows.";
                return outcome;
            }
            Match match = Write.Match(sql);
            if (!match.Success)
            {
                throw new InvalidOperationException("Only SELECT, INSERT, UPDATE and DELETE are accepted.");
            }
            string table = match.Groups[2].Value;
            if (!DbIds.HasTable(db, table))
            {
                throw new InvalidOperationException("There is no table " + table + ".");
            }
            // The table's own name, as the schema spells it.
            foreach (string name in DbIds.Schema(db).Keys)
            {
                if (string.Equals(name, table, StringComparison.OrdinalIgnoreCase))
                {
                    table = name;
                }
            }
            string operation = match.Groups[1].Value.ToUpperInvariant();
            if (!operation.StartsWith("DELETE", StringComparison.Ordinal))
            {
                string problem = CheckKeys(db, table, sql, game);
                if (problem != null)
                {
                    throw new InvalidOperationException(problem);
                }
            }
            outcome.Table = table;
            outcome.Changed = db.Execute(sql);
            outcome.Message = outcome.Changed + " rows changed in " + table + ".";
            return outcome;
        }

        // The statement on a copy: the keys it adds to the table against FM4's largest.
        private static string CheckKeys(SqliteDatabase db, string table, string sql, ForzaGame game)
        {
            Dictionary<string, List<string>> schema = DbIds.Schema(db);
            string key = DbIds.KeyColumn(db, table, schema[table]);
            if (key == null)
            {
                return null;
            }
            long limit = GameIdLimits.Max(game, table, key);
            if (limit < 0)
            {
                return null;
            }
            string select = "SELECT " + SqliteDatabase.Quote(key) + " FROM " + SqliteDatabase.Quote(table);
            Dictionary<long, bool> before = new Dictionary<long, bool>();
            foreach (object[] row in db.Query(select).Rows)
            {
                if (row[0] is long)
                {
                    before[(long) row[0]] = true;
                }
            }
            using (SqliteDatabase copy = SqliteDatabase.FromBytes(db.ToBytes()))
            {
                try
                {
                    copy.Execute(sql);
                }
                catch (Exception ex)
                {
                    return ex.Message;
                }
                foreach (object[] row in copy.Query(select).Rows)
                {
                    if (row[0] is long && !before.ContainsKey((long) row[0]) && (long) row[0] <= limit)
                    {
                        return string.Format(CultureInfo.InvariantCulture, "{0}.{1} = {2} is not above FM4's largest ({3}, base game and every DLC): use {4} or more (the next free one is {5}).",
                            table, key, row[0], limit, limit + 1, DbIds.Next(db, table, key, game));
                    }
                }
            }
            return null;
        }
    }
}
