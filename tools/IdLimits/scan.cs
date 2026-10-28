using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using ForzaStudio;

class Scan
{
    static StreamWriter outw;
    static string game;

    static int Main(string[] a)
    {
        string exe = @"C:\Users\Dan\Desktop\ForzaEditor\bin\Debug\ForzaStudioEVO.exe";
        AppDomain.CurrentDomain.AssemblyResolve += delegate(object s, ResolveEventArgs e)
        {
            string n = new AssemblyName(e.Name).Name;
            foreach (string ext in new[] { ".exe", ".dll" })
            {
                string p = Path.Combine(Path.GetDirectoryName(exe), n + ext);
                if (File.Exists(p)) return Assembly.LoadFrom(p);
            }
            return null;
        };
        return Run(a);
    }

    static int Run(string[] a)
    {
        string raw = a[0];
        string tmp = a[1];
        outw = new StreamWriter(raw, false, new UTF8Encoding(false));
        outw.WriteLine("game\tarchive\tkind\tdb\ttable\tcolumn\tpk\trows\tmin\tmax");
        string[][] bases = {
            new[]{"FM2", @"F:\Xenia\Games\FM2_Extracted"}, new[]{"FM3", @"F:\Xenia\Games\FM3_Extracted"},
            new[]{"FM4", @"F:\Xenia\Games\FM4_Extracted"}, new[]{"FH1", @"F:\Xenia\Games\FH1_Extracted"},
            new[]{"FH2", @"F:\Xenia\Games\FH2_Extracted"} };
        foreach (string[] b in bases)
        {
            game = b[0];
            foreach (string f in Directory.GetFiles(b[1], "*.slt", SearchOption.AllDirectories))
                Db(Path.GetFileName(b[1]), "base", f.Substring(b[1].Length + 1), File.ReadAllBytes(f));
        }
        string content = @"F:\Xenia\Emulators\Xenia Canary\content\0000000000000000";
        foreach (string f in Directory.GetFiles(content, "*", SearchOption.AllDirectories))
        {
            if (!StfsReader.IsPackage(f)) { Console.WriteLine("skip " + f); continue; }
            StfsReader p;
            try { p = StfsReader.Open(f); } catch (Exception e) { Console.WriteLine("ERR open " + f + " " + e.Message); continue; }
            game = GameInfo.Name(GameInfo.FromTitleId(p.TitleId));
            string arch = Path.GetFileName(f);
            Console.WriteLine(game + " " + arch + " " + p.DisplayName + " ct=" + p.ContentType.ToString("X"));
            List<string> files = p.Files();
            foreach (string rel in files)
            {
                string r = rel.Replace((char)92, '/');
                if (r.EndsWith(".puboffer", StringComparison.OrdinalIgnoreCase) || r.EndsWith(".offer", StringComparison.OrdinalIgnoreCase))
                    outw.WriteLine(game + "\t" + arch + "\tofferfile\t" + r + "\t\t\t\t\t\t" + Path.GetFileNameWithoutExtension(r));
            }
            string dir = Path.Combine(tmp, "pkg");
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            try { p.Extract(dir); } catch (Exception e) { Console.WriteLine("ERR extract " + f + " " + e.Message); outw.WriteLine(game + "\t" + arch + "\terror\t" + e.Message.Replace('\t',' ') + "\t\t\t\t\t\t"); continue; }
            string label = arch + " | " + p.DisplayName;
            foreach (string x in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
            {
                string rel = x.Substring(dir.Length + 1);
                if (x.EndsWith(".slt", StringComparison.OrdinalIgnoreCase)) Db(label, "pkg", rel, File.ReadAllBytes(x));
                else if (x.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        ForzaZip z = new ForzaZip(x);
                        foreach (ForzaZip.Entry en in z.Entries)
                            if (!en.IsDirectory && en.Name.EndsWith(".slt", StringComparison.OrdinalIgnoreCase))
                                Db(label, "pkg", rel + "!" + en.Name, z.Read(en));
                    }
                    catch (Exception e) { Console.WriteLine("ERR zip " + x + " " + e.Message); }
                }
            }
            Directory.Delete(dir, true);
            outw.Flush();
        }
        outw.Close();
        return 0;
    }

    static void Db(string arch, string kind, string path, byte[] data)
    {
        try
        {
            using (SqliteDatabase db = SqliteDatabase.FromBytes(data))
            {
                SqliteDatabase.Result tabs = db.Query("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'");
                foreach (object[] t in tabs.Rows)
                {
                    string table = (string)t[0];
                    SqliteDatabase.Result info = db.Query("PRAGMA table_info(" + SqliteDatabase.Quote(table) + ")");
                    List<string> cols = new List<string>(); List<int> pks = new List<int>();
                    StringBuilder q = new StringBuilder("SELECT count(*)");
                    foreach (object[] c in info.Rows)
                    {
                        string type = Convert.ToString(c[2]).ToUpperInvariant();
                        if (type.IndexOf("INT") < 0) continue;
                        cols.Add((string)c[1]); pks.Add(Convert.ToInt32(c[5]));
                        q.Append(",min(" + SqliteDatabase.Quote((string)c[1]) + "),max(" + SqliteDatabase.Quote((string)c[1]) + ")");
                    }
                    q.Append(" FROM " + SqliteDatabase.Quote(table));
                    object[] row = db.Query(q.ToString()).Rows[0];
                    string rows = Convert.ToString(row[0]);
                    if (cols.Count == 0) outw.WriteLine(game + "\t" + arch + "\tdb\t" + path + "\t" + table + "\t\t\t" + rows + "\t\t");
                    for (int i = 0; i < cols.Count; i++)
                        outw.WriteLine(game + "\t" + arch + "\t" + kind + "\t" + path + "\t" + table + "\t" + cols[i] + "\t" + pks[i] + "\t" + rows + "\t" + Convert.ToString(row[1 + 2 * i]) + "\t" + Convert.ToString(row[2 + 2 * i]));
                }
            }
        }
        catch (Exception e) { Console.WriteLine("ERR db " + arch + " " + path + " " + e.Message); }
    }
}
