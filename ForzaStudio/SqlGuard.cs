namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;

    // Runs one SQL statement the user typed (ROADMAP 18.5, 1.4). SELECT / PRAGMA read; INSERT, UPDATE and
    // DELETE write, on any table. A write runs first on a copy of the database and DbIntegrity compares
    // the copy with the database: the statement is refused when it breaks a rule (a new key at or below
    // FM4's largest, a key twice, an id that points at nothing, a row that others point at going away, a
    // car without its rows). Plan() does that and returns what the statement would do, per table, without
    // touching anything; Apply() plans again and then writes inside a transaction, so a failure leaves the
    // database as it was. Anything else (DROP, ALTER, CREATE, ATTACH...) is refused.
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
            public List<string> Summary = new List<string>();   // a write: what it does, per table, and the warnings
        }

        // The statement as it will run, or throws InvalidOperationException with the reason it is refused.
        public static Outcome Plan(SqliteDatabase db, string sql, ForzaGame game)
        {
            sql = Clean(sql);
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
            using (SqliteDatabase copy = SqliteDatabase.FromBytes(db.ToBytes()))
            {
                try
                {
                    outcome.Changed = copy.Execute(sql);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(ex.Message);
                }
                DbIntegrity.Result check = DbIntegrity.Compare(db, copy, game, new string[] { table });
                if (!check.Ok)
                {
                    throw new InvalidOperationException("Refused, the database would break:\r\n  " + string.Join("\r\n  ", check.Errors.ToArray()));
                }
                outcome.Summary.AddRange(check.Summary);
                outcome.Summary.AddRange(check.Warnings.ConvertAll<string>(delegate(string w) { return "warning: " + w; }));
            }
            outcome.Table = table;
            outcome.Message = outcome.Changed + " rows changed in " + table + ".";
            return outcome;
        }

        // Plans, then writes in a transaction. Throws when the statement is refused or fails.
        public static Outcome Apply(SqliteDatabase db, string sql, ForzaGame game)
        {
            Outcome outcome = Plan(db, sql, game);
            if (outcome.Table == null)
            {
                return outcome;
            }
            sql = Clean(sql);
            db.Begin();
            try
            {
                outcome.Changed = db.Execute(sql);
                db.Commit();
            }
            catch (Exception)
            {
                db.Rollback();
                throw;
            }
            outcome.Message = outcome.Changed + " rows changed in " + outcome.Table + ".";
            return outcome;
        }

        public static Outcome Run(SqliteDatabase db, string sql, ForzaGame game)
        {
            return Apply(db, sql, game);
        }

        // One statement, without the closing semicolons.
        private static string Clean(string sql)
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
            return sql;
        }
    }
}
