using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using ForzaStudio;

class Coll
{
    // archive -> table.key -> (keyvalue -> row text)
    static Dictionary<string, Dictionary<string, Dictionary<string, string>>> data = new Dictionary<string, Dictionary<string, Dictionary<string, string>>>();

    static void Load(string arch, byte[] bytes)
    {
        var tabs = new Dictionary<string, Dictionary<string, string>>();
        data[arch] = tabs;
        using (SqliteDatabase db = SqliteDatabase.FromBytes(bytes))
        {
            foreach (object[] t in db.Query("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'").Rows)
            {
                string table = (string)t[0];
                string key = null; int pk = 0; string first = null;
                foreach (object[] c in db.Query("PRAGMA table_info(" + SqliteDatabase.Quote(table) + ")").Rows)
                {
                    if (first == null) first = (string)c[1];
                    if (c[5] is long && (long)c[5] > 0) { key = (string)c[1]; pk++; }
                }
                if (pk != 1) key = (first != null && first.ToLower().EndsWith("id")) ? first : null;
                if (key == null) continue;
                var rows = new Dictionary<string, string>();
                var res = db.Query("SELECT * FROM " + SqliteDatabase.Quote(table));
                int ki = Array.FindIndex(res.Columns, delegate(string s) { return s == key; });
                if (ki < 0) continue;
                foreach (object[] r in res.Rows)
                {
                    var sb = new StringBuilder();
                    foreach (object o in r) sb.Append(Convert.ToString(o)).Append('|');
                    rows[Convert.ToString(r[ki])] = sb.ToString();
                }
                tabs[table + "." + key] = rows;
            }
        }
    }

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
        string root = @"F:\Xenia\Games\FM4_Extracted";
        Load("base", File.ReadAllBytes(Path.Combine(root, @"Media\db\gamedb.slt")));
        string content = @"F:\Xenia\Emulators\Xenia Canary\content\0000000000000000";
        int pk = 0;
        foreach (string f in Directory.GetFiles(content, "*", SearchOption.AllDirectories))
        {
            if (!StfsReader.IsPackage(f)) { Console.WriteLine("notpkg " + f); continue; }
            StfsReader p;
            try { p = StfsReader.Open(f); } catch (Exception e) { Console.WriteLine("open " + f + " " + e.Message); continue; } Console.WriteLine(p.DisplayName + " " + GameInfo.Name(GameInfo.FromTitleId(p.TitleId)));
            if (GameInfo.Name(GameInfo.FromTitleId(p.TitleId)) != "Forza Motorsport 4") continue;
            string dir = Path.Combine(Path.GetTempPath(), "collpkg" + (pk++));
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            try { p.Extract(dir); } catch (Exception e) { Console.WriteLine("ERR " + f + " " + e.Message); continue; }
            foreach (string x in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
            {
                Console.WriteLine("  file " + x); string label = p.DisplayName + " [" + Path.GetFileName(f) + "]";
                if (x.EndsWith(".slt", StringComparison.OrdinalIgnoreCase)) Load(label + ":" + Path.GetFileName(x), File.ReadAllBytes(x));
                else if (x.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    try { ForzaZip z = new ForzaZip(x); foreach (ForzaZip.Entry en in z.Entries) if (!en.IsDirectory && en.Name.EndsWith(".slt", StringComparison.OrdinalIgnoreCase)) Load(label + ":" + Path.GetFileName(en.Name), z.Read(en)); }
                    catch (Exception e) { Console.WriteLine("zip " + x + " " + e.Message); }
                }
            }
            Directory.Delete(dir, true);
        }
        var names = new List<string>(data.Keys);
        Console.WriteLine("archives: " + names.Count);
        // every table.key seen
        var all = new SortedDictionary<string, bool>();
        foreach (var d in data.Values) foreach (string k in d.Keys) all[k] = true;
        foreach (string tk in all.Keys)
        {
            var seen = new Dictionary<string, string>();   // key -> first archive
            int same = 0, conflict = 0; string sample = null;
            var conflictPairs = new SortedDictionary<string, int>();
            foreach (string arch in names)
            {
                Dictionary<string, string> rows;
                if (!data[arch].TryGetValue(tk, out rows)) continue;
                foreach (var kv in rows)
                {
                    string prev;
                    if (seen.TryGetValue(kv.Key, out prev))
                    {
                        if (data[prev][tk][kv.Key] == kv.Value) same++;
                        else { conflict++; if (sample == null) sample = kv.Key + " " + prev + " vs " + arch; string cp = (prev == "base" ? "base" : "pack") + "/pack"; int n; conflictPairs.TryGetValue(cp, out n); conflictPairs[cp] = n + 1; }
                    }
                    else seen[kv.Key] = arch;
                }
            }
            if (same + conflict > 0) Console.WriteLine(tk + "\tidentical-overlap=" + same + "\tCONFLICT=" + conflict + "\t" + (sample ?? ""));
        }
        return 0;
    }
}
