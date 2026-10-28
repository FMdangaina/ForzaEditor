using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using ForzaStudio;

// Lists the string layers (base game + every FM4 package) and writes layer.tsv:
// layer, priority, mount, language, table, entries, keys (hex, comma separated)
class StrScan
{
    static StreamWriter w;

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
        w = new StreamWriter(a[0], false, new UTF8Encoding(false));
        string tmp = a[1];
        w.WriteLine("layer\tkind\tpriority\tmount\tlang\ttable\tentries\tkeys");
        string baseRoot = @"F:\Xenia\Games\FM4_Extracted\Media\StringTables";
        foreach (string z in Directory.GetFiles(baseRoot, "*.zip"))
            Zip("base", "base", 0, "StringTables/", z);
        string content = @"F:\Xenia\Emulators\Xenia Canary\content\0000000000000000\4D530910\00000002";
        foreach (string f in Directory.GetFiles(content))
        {
            if (!StfsReader.IsPackage(f)) continue;
            StfsReader p = StfsReader.Open(f);
            string dir = Path.Combine(tmp, "pkg");
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            p.Extract(dir);
            string name = Path.GetFileName(f);
            Console.WriteLine(name + " " + p.DisplayName);
            string zips = Path.Combine(dir, "Media", "DLCZips");
            if (!Directory.Exists(zips)) { Directory.Delete(dir, true); continue; }
            Dictionary<string, string> mounts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string zm = Path.Combine(zips, "zipmount.xml");
            if (File.Exists(zm))
                foreach (Match m in Regex.Matches(File.ReadAllText(zm), "<zip\\s+Name=\"([^\"]+)\"\\s+Mount=\"([^\"]*)\"", RegexOptions.IgnoreCase))
                    mounts[m.Groups[1].Value] = m.Groups[2].Value;
            string layer = name + " | " + p.DisplayName;
            foreach (string z in Directory.GetFiles(zips, "StringTables*.zip"))
            {
                string n = Path.GetFileName(z);
                Match pm = Regex.Match(n, "_pri_(\\d+)", RegexOptions.IgnoreCase);
                string mount; mounts.TryGetValue(n, out mount);
                Zip(layer, "pack", pm.Success ? int.Parse(pm.Groups[1].Value) : -1, mount ?? "(no mount)", z);
            }
            foreach (string d in Directory.GetDirectories(zips, "StringTables*"))
            {
                Match pm = Regex.Match(Path.GetFileName(d), "_pri_(\\d+)", RegexOptions.IgnoreCase);
                foreach (string s in Directory.GetFiles(d, "*.str", SearchOption.AllDirectories))
                    Str(layer, "pack-loose", pm.Success ? int.Parse(pm.Groups[1].Value) : -1, "(loose)", s.Substring(d.Length + 1).Replace('\\', '/'), File.ReadAllBytes(s));
            }
            // the other loose trees of the pack that carry .str files
            foreach (string s in Directory.GetFiles(zips, "*.str", SearchOption.AllDirectories))
                if (Path.GetFileName(Path.GetDirectoryName(s)).IndexOf("StringTables", StringComparison.OrdinalIgnoreCase) < 0 && s.IndexOf("StringTables", StringComparison.OrdinalIgnoreCase) < 0)
                    Console.WriteLine("  other str: " + s.Substring(dir.Length));
            w.Flush();
            Directory.Delete(dir, true);
        }
        w.Close();
        return 0;
    }

    static void Zip(string layer, string kind, int pri, string mount, string path)
    {
        try
        {
            ForzaZip z = new ForzaZip(path);
            foreach (ForzaZip.Entry e in z.Entries)
                if (!e.IsDirectory && e.Name.EndsWith(".str", StringComparison.OrdinalIgnoreCase))
                    Str(layer, kind, pri, mount, kind == "base" ? Path.GetFileNameWithoutExtension(path) + "/" + e.Name : e.Name, z.Read(e));
        }
        catch (Exception e) { Console.WriteLine("ERR " + path + " " + e.Message); }
    }

    // name: "<LANG>/<Table>.str" in a pack, "<Table>.str" in the base game's per-language zip
    static void Str(string layer, string kind, int pri, string mount, string name, byte[] data)
    {
        try
        {
            StrFile s = new StrFile(data);
            string[] parts = name.Replace('\\', '/').Split('/');
            string lang = parts.Length > 1 ? parts[parts.Length - 2] : "";
            string table = Path.GetFileNameWithoutExtension(parts[parts.Length - 1]);
            StringBuilder k = new StringBuilder();
            foreach (StrFile.Entry en in s.Entries) { if (k.Length > 0) k.Append(','); k.Append(en.Key.ToString("X")); }
            w.WriteLine(layer + "\t" + kind + "\t" + pri + "\t" + mount + "\t" + lang + "\t" + table + "\t" + s.Entries.Count + "\t" + k);
        }
        catch (Exception e) { Console.WriteLine("ERR str " + layer + " " + name + " " + e.Message); }
    }
}
