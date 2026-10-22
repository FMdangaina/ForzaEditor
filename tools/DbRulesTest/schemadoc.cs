using System; using System.IO; using System.Text; using System.Collections.Generic; using System.Globalization; using ForzaStudio;
class S {
  static string KeyOf(SqliteDatabase db, string table, out bool declared) {
    declared = false; int n = 0; string k = null;
    List<string> cols = new List<string>();
    foreach (object[] c in db.Query("PRAGMA table_info(" + SqliteDatabase.Quote(table) + ")").Rows) { cols.Add((string)c[1]); if (c[5] is long && (long)c[5] > 0) { k = (string)c[1]; n++; declared = true; } }
    if (n == 1) return k; if (n > 1) return "(composite)";
    if (cols.Count > 0 && cols[0].ToLowerInvariant() == "id") return cols[0] + " (first column, not declared)";
    return null;
  }
  static void Main(string[] a) {
    SqliteDatabase db = SqliteDatabase.FromBytes(File.ReadAllBytes(a[0]));
    Dictionary<string, List<string>> schema = DbIds.Schema(db);
    List<string> tables = new List<string>(schema.Keys); tables.Sort(StringComparer.OrdinalIgnoreCase);
    StringBuilder o = new StringBuilder();
    o.AppendLine("# FM4 game database (`gamedb.slt`): schema, relations, order of operations");
    o.AppendLine();
    o.AppendLine("Generated from the FM4 DVD1 database and `GameDbSchema` (`tools/DbRulesTest/schemadoc.cs`). The rules every writer follows are in `DATABASE_RULES.md`.");
    o.AppendLine();
    int declared = 0, relIn = 0;
    foreach (string t in tables) { bool d; KeyOf(db, t, out d); if (d) declared++; }
    o.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0} tables, {1} with a declared PRIMARY KEY, {2} relations in `GameDbSchema` (the database has no `REFERENCES`: SQLite does not enforce any of them). A *strict* relation has no dangling id in the original FM4; a *loose* one already dangles.", tables.Count, declared, GameDbSchema.Relations.Count));
    o.AppendLine();
    // order of operations: a table comes after the tables it points at (strict relations); cycles and self relations ignored
    Dictionary<string, int> level = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    foreach (string t in tables) level[t] = 0;
    for (int pass = 0; pass < 30; pass++) {
      bool changed = false;
      foreach (GameDbSchema.Relation r in GameDbSchema.Relations) {
        if (!r.Strict || !schema.ContainsKey(r.Table) || !schema.ContainsKey(r.Target) || string.Equals(r.Table, r.Target, StringComparison.OrdinalIgnoreCase)) continue;
        if (level[r.Table] <= level[r.Target] && level[r.Target] < 25) { level[r.Table] = level[r.Target] + 1; changed = true; }
      }
      if (!changed) break;
    }
    o.AppendLine("## Order of operations");
    o.AppendLine();
    o.AppendLine("Write (insert) the tables of level 0 first, then level 1, and so on: a row is written after the rows it points at. Remove in the opposite order: the rows that point at a row go before it. `DbIntegrity` refuses a write that leaves an id pointing at nothing, so a wrong order shows as an error, not as a silent break.");
    o.AppendLine();
    int max = 0; foreach (int v in level.Values) max = Math.Max(max, v);
    for (int l = 0; l <= max; l++) {
      List<string> at = new List<string>(); foreach (string t in tables) if (level[t] == l) at.Add(t);
      if (at.Count == 0) continue;
      o.AppendLine(string.Format(CultureInfo.InvariantCulture, "- **Level {0}** ({1}): {2}", l, at.Count, string.Join(", ", at.ToArray())));
    }
    o.AppendLine();
    o.AppendLine("A car (`Data_Car` row) is written in this order: the rows it points at must exist (`List_CarMake`, `CarClasses`, `Powertrains`, `List_FamilyModel`... levels 0 to 2), then `Data_Car` (level 3), then `Data_CarBody` with `Id` = car * 1000 (no relation in the schema: the formula links them, `DbIntegrity` checks it), then every table that has the car's Id as `Ordinal` / `CarId` (`List_Upgrade*`, `CarPartPositions`, `Combo_Colors`, the physics lists), whose own keys are `car * 1000 + n`. The string keys (`DisplayName`, `ModelShort`) are made first, in the `.str` tables. The port and Duplicate Car do this (`DbIds.RenumberCar`, `CarDuplicate`); `Combo_Engines` has a counter of its own (`EngineComboID`).");
    o.AppendLine();
    o.AppendLine("## Car columns");
    o.AppendLine();
    o.AppendLine("The columns that hold a car's Id (`DbIds.CarColumns`: by name `Ordinal`, `CarId`, `CarID`, `CarOrdinal`, plus every relation into `Data_Car.Id`):");
    o.AppendLine();
    foreach (string[] c in DbIds.CarColumns(schema)) o.AppendLine("- `" + c[0] + "." + c[1] + "`");
    o.AppendLine();
    o.AppendLine("## Tables");
    o.AppendLine();
    foreach (string t in tables) {
      bool d; string key = KeyOf(db, t, out d);
      long rows = Convert.ToInt64(db.Scalar("SELECT count(*) FROM " + SqliteDatabase.Quote(t)), CultureInfo.InvariantCulture);
      o.AppendLine("### " + t);
      o.AppendLine();
      o.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0} rows, level {1}. Key: {2}.", rows, level[t], key == null ? "none" : "`" + key + "`" + (d ? "" : "")));
      List<string> cols = new List<string>();
      foreach (object[] c in db.Query("PRAGMA table_info(" + SqliteDatabase.Quote(t) + ")").Rows) cols.Add(c[1] + " " + c[2]);
      o.AppendLine();
      o.AppendLine("Columns: " + string.Join(", ", cols.ToArray()));
      List<string> from = new List<string>(); foreach (GameDbSchema.Relation r in GameDbSchema.From(t)) from.Add("`" + r.Column + "` → `" + r.Target + "." + r.TargetColumn + "`" + (r.Strict ? "" : " (loose" + (r.Note != null ? ": " + r.Note : "") + ")"));
      List<string> into = new List<string>(); foreach (GameDbSchema.Relation r in GameDbSchema.Into(t)) into.Add("`" + r.Table + "." + r.Column + "`" + (r.Strict ? "" : " (loose)"));
      if (from.Count > 0) { o.AppendLine(); o.AppendLine("Points at: " + string.Join("; ", from.ToArray())); }
      if (into.Count > 0) { o.AppendLine(); o.AppendLine("Pointed at by: " + string.Join(", ", into.ToArray())); }
      o.AppendLine();
    }
    File.WriteAllText(a[1], o.ToString(), new UTF8Encoding(false));
    Console.WriteLine("written " + o.Length + " chars, levels 0.." + max);
  }
}
