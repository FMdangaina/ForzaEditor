using System;
using System.Collections.Generic;
using System.IO;
using ForzaStudio;
class T {
  static string P(string s) { return s.Replace('/', Path.DirectorySeparatorChar); }
  static void Copy(string src, string dst) { Directory.CreateDirectory(Path.GetDirectoryName(dst)); File.Copy(src, dst, true); }
  static string MakeRoot(string orig, string root, string car) {
    if (Directory.Exists(root)) Directory.Delete(root, true);
    string m = Path.Combine(orig, "Media");
    string r = Path.Combine(root, "Media");
    Copy(Path.Combine(m, P("db/gamedb.slt")), Path.Combine(r, P("db/gamedb.slt")));
    Copy(Path.Combine(m, "zipmanifest.xml"), Path.Combine(r, "zipmanifest.xml"));
    Copy(Path.Combine(m, P("cars/" + car + ".zip")), Path.Combine(r, P("cars/" + car + ".zip")));
    foreach (string f in Directory.GetFiles(Path.Combine(m, "StringTables"), "*.zip")) Copy(f, Path.Combine(r, P("StringTables/" + Path.GetFileName(f))));
    SqliteDatabase db = SqliteDatabase.FromBytes(File.ReadAllBytes(Path.Combine(m, P("db/gamedb.slt"))));
    string th = db.Scalar("SELECT Thumbnail FROM Data_Car WHERE MediaName = ?", car) as string;
    Console.WriteLine("thumbnail " + th);
    GamePaths.GameFile g = GamePaths.Locate(orig, th);
    if (g != null && g.InZip) { string rel = g.ZipPath.Substring(orig.TrimEnd(Path.DirectorySeparatorChar).Length + 1); Console.WriteLine("thumb zip " + rel); Copy(g.ZipPath, Path.Combine(root, rel)); }
    return root;
  }
  static CarDuplicate Make(string root, string work, string mode, string car) {
    CarDuplicate d = new CarDuplicate();
    d.TargetRoot = root; d.SourceMediaName = car; d.NewMediaName = car + "_B"; d.DisplayName = "NSX Copy"; d.ModelShort = "NSX Copy"; d.Year = 1999;
    d.BackupFolder = Path.Combine(work, "bak");
    if (mode == "pack") { d.PackContent = Path.Combine(work, "content"); Directory.CreateDirectory(d.PackContent); d.XeniaContent = d.PackContent; d.PackOffer = 91; d.PackName = "Test pack"; }
    return d;
  }
  static void Main(string[] a) {
    string orig = a[0], work = a[1], mode = a[2];
    string car = "ACU_NSX_97";
    string root = MakeRoot(orig, Path.Combine(work, "game"), car);
    CarDuplicate d = Make(root, work, mode, car);
    foreach (string l in d.Plan()) Console.WriteLine("PLAN " + l);
    d = Make(root, work, mode, car);
    d.Run();
    Console.WriteLine("RUN done, id " + d.NewId);
    Console.WriteLine(d.Report.ToString());
  }
}
