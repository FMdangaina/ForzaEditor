using System;
using System.Collections.Generic;
using System.IO;
using ForzaStudio;
class T {
  static SqliteDatabase db;
  static void Try(string sql) {
    Console.WriteLine("> " + sql);
    try { SqlGuard.Outcome o = SqlGuard.Plan(db, sql, ForzaGame.Motorsport4);
          Console.WriteLine("  PLAN OK: " + (o.Rows != null ? o.Rows.Rows.Count + " rows" : o.Message + " | " + string.Join(" | ", o.Summary.ToArray()))); }
    catch (Exception e) { Console.WriteLine("  REFUSED: " + e.Message.Replace("\r\n", "\n  ")); }
  }
  static void Main(string[] a) {
    db = SqliteDatabase.FromBytes(File.ReadAllBytes(a[0]));
    string id = Convert.ToString(db.Scalar("SELECT Id FROM Data_Car ORDER BY Id LIMIT 1"));
    Console.WriteLine("car " + id);
    Try("SELECT count(*) FROM Data_Car");
    Try("INSERT INTO Data_Car SELECT * FROM Data_Car WHERE Id = " + id);
    Try("DELETE FROM Data_Car WHERE Id = " + id);
    Try("UPDATE Data_Car SET Year = 2000 WHERE Id = " + id);
    Try("UPDATE Data_Car SET Id = 99999 WHERE Id = " + id);
    Try("UPDATE Data_Car SET ModelId = 3 WHERE Id = " + id);
    Try("UPDATE Data_Car SET MakeID = 99999 WHERE Id = " + id);
    Try("DELETE FROM Combo_Engines WHERE Ordinal = " + id);
    Try("INSERT INTO StartGridPositions SELECT * FROM StartGridPositions LIMIT 1");
    Try("DROP TABLE Data_Car");
    SqlGuard.Apply(db, "UPDATE Data_Car SET Year = 2001 WHERE Id = " + id, ForzaGame.Motorsport4);
    Console.WriteLine("applied: year now " + db.Scalar("SELECT Year FROM Data_Car WHERE Id = " + id));
  }
}
