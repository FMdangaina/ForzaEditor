using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

// Builds GameIdLimits.json and GameIdLimits.xlsx from the raw.tsv scan (scan.cs).
class Gen
{
    static readonly string[] Games = { "FM2", "FM3", "FM4", "FH1", "FH2" };
    static readonly Dictionary<string, string> GameName = new Dictionary<string, string> {
        {"FM2","Forza Motorsport 2"},{"FM3","Forza Motorsport 3"},{"FM4","Forza Motorsport 4"},{"FH1","Forza Horizon"},{"FH2","Forza Horizon 2"}};
    static readonly Dictionary<string, string> Title = new Dictionary<string, string> {
        {"FM2","4D5307EA"},{"FM3","4D53084D"},{"FM4","4D530910"},{"FH1","4D5309C9"},{"FH2","4D530AA4"}};

    class Arch { public string Id, Kind, File, Display; public List<string> Dbs = new List<string>(); public List<long> PubOffers = new List<long>(); public bool HasDb; }
    class Col { public bool Key; public long? Max, Min; public string MaxArch; public Dictionary<string, long> By = new Dictionary<string, long>(); }
    class Table { public SortedDictionary<string, Col> Cols = new SortedDictionary<string, Col>(StringComparer.Ordinal); public List<string> Order = new List<string>(); public long Rows; }
    class GameData { public List<Arch> Archives = new List<Arch>(); public SortedDictionary<string, Table> Tables = new SortedDictionary<string, Table>(StringComparer.Ordinal); }

    static string Short(string g)
    {
        if (g.StartsWith("FM") || g.StartsWith("FH")) return g;
        switch (g) { case "Forza Motorsport 2": return "FM2"; case "Forza Motorsport 3": return "FM3"; case "Forza Motorsport 4": return "FM4"; case "Forza Horizon": return "FH1"; case "Forza Horizon 2": return "FH2"; }
        throw new Exception(g);
    }

    static bool IdLike(string c)
    {
        return c == "id" || c.EndsWith("Id", StringComparison.Ordinal) || c.EndsWith("ID", StringComparison.Ordinal) || c.EndsWith("_id", StringComparison.Ordinal) || c.EndsWith("Ordinal", StringComparison.Ordinal);
    }

    static long? P(string s) { long v; return long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? (long?)v : null; }

    static int Main(string[] a)
    {
        string raw = a[0], outJson = a[1], outXlsx = a[2];
        Dictionary<string, GameData> data = new Dictionary<string, GameData>();
        foreach (string g in Games) data[g] = new GameData();
        Dictionary<string, HashSet<string>> pk = new Dictionary<string, HashSet<string>>();
        List<string[]> rows = new List<string[]>();
        foreach (string line in File.ReadLines(raw).Skip(1))
        {
            string[] f = line.Split('\t');
            if (f.Length < 10) { string[] g2 = new string[10]; for (int i = 0; i < 10; i++) g2[i] = i < f.Length ? f[i] : ""; f = g2; }
            f[0] = Short(f[0]);
            rows.Add(f);
        }
        Dictionary<string, Arch> archs = new Dictionary<string, Arch>();
        foreach (string[] f in rows)
        {
            GameData gd = data[f[0]];
            string[] p = f[1].Split(new[] { " | " }, 2, StringSplitOptions.None);
            f[1] = p[0];
            string key = f[0] + "|" + f[1];
            Arch ar;
            if (!archs.TryGetValue(key, out ar))
            {
                ar = new Arch();
                if (f[2] == "base") { ar.Id = "base"; ar.Kind = "base"; ar.File = f[1]; ar.Display = "base game"; }
                else { ar.Id = p[0]; ar.Kind = "package"; ar.File = p[0]; ar.Display = p[0]; }
                archs[key] = ar; gd.Archives.Add(ar);
            }
            if (p.Length > 1) ar.Display = p[1];
            if (f[2] == "offerfile") { long n; if (long.TryParse(f[9], out n)) ar.PubOffers.Add(n); continue; }
            if (f[2] == "error") continue;
            if (!ar.Dbs.Contains(f[3])) ar.Dbs.Add(f[3]);
            ar.HasDb = true;
            if (f[6] != "" && f[6] != "0")
            {
                HashSet<string> s; string k = f[0] + "|" + f[4];
                if (!pk.TryGetValue(k, out s)) pk[k] = s = new HashSet<string>();
                s.Add(f[5]);
            }
        }
        foreach (string[] f in rows)
        {
            if (f[2] == "offerfile" || f[2] == "error" || f[5] == "") continue;
            GameData gd = data[f[0]];
            Arch ar = archs[f[0] + "|" + f[1]];
            Table t;
            if (!gd.Tables.TryGetValue(f[4], out t)) gd.Tables[f[4]] = t = new Table();
            Col c;
            HashSet<string> keys; bool hasPk = pk.TryGetValue(f[0] + "|" + f[4], out keys);
            if (!t.Cols.TryGetValue(f[5], out c))
            {
                c = new Col(); t.Cols[f[5]] = c; t.Order.Add(f[5]);
            }
            long? max = P(f[9]), min = P(f[8]);
            long rowsN = P(f[7]) ?? 0;
            if (rowsN > t.Rows) t.Rows = rowsN;
            if (max.HasValue)
            {
                long cur;
                if (!c.By.TryGetValue(ar.Id, out cur) || max.Value > cur) c.By[ar.Id] = max.Value;
            }
            if (min.HasValue && (!c.Min.HasValue || min.Value < c.Min.Value)) c.Min = min;
            if (hasPk && keys.Contains(f[5])) c.Key = true;
        }
        foreach (string g in Games)
            foreach (KeyValuePair<string, Table> kv in data[g].Tables)
            {
                bool any = kv.Value.Cols.Values.Any(x => x.Key);
                if (!any && kv.Value.Order.Count > 0 && IdLike(kv.Value.Order[0])) kv.Value.Cols[kv.Value.Order[0]].Key = true;
            }
        foreach (string g in Games)
        {
            List<string> dead = new List<string>();
            foreach (KeyValuePair<string, Table> kv in data[g].Tables)
            {
                foreach (string cn in kv.Value.Order.ToArray())
                    if (!kv.Value.Cols[cn].Key && !IdLike(cn)) { kv.Value.Cols.Remove(cn); kv.Value.Order.Remove(cn); }
                if (kv.Value.Cols.Count == 0) dead.Add(kv.Key);
            }
            foreach (string d in dead) data[g].Tables.Remove(d);
            foreach (KeyValuePair<string, Table> kv in data[g].Tables)
                foreach (Col c in kv.Value.Cols.Values)
                    foreach (Arch ar in data[g].Archives)
                    {
                        long v;
                        if (c.By.TryGetValue(ar.Id, out v) && (!c.Max.HasValue || v > c.Max.Value)) { c.Max = v; c.MaxArch = ar.Id; }
                    }
        }
        All = data;
        WriteJson(outJson, data);
        WriteXlsx(outXlsx, data);
        return 0;
    }

    static readonly Dictionary<string, string> What = new Dictionary<string, string> {
        {"Data_Car.Id","Car (Ordinal): every car table references it"},
        {"Data_CarBody.Id","Car body (car * 1000 + n)"},
        {"Data_Engine.EngineID","Engine"},
        {"Data_Drivetrain.DrivetrainID","Drivetrain"},
        {"Data_Motor.MotorID","Electric motor"},
        {"List_CarMake.ID","Car make (manufacturer)"},
        {"List_Wheels.Id","Wheel (rim) in the wheel list"},
        {"List_Wheels.ID","Wheel (rim) in the wheel list"},
        {"Events.Id","Event"},
        {"Races.Id","Race of an event"},
        {"EventParticipants.ID","AI participant of a race"},
        {"AIPlayers.Id","AI driver"},
        {"Tracks.Id","Track"},
        {"Tracks.id","Track"},
        {"Combo_Colors.ID","Car colour combination (car * 1000 + n)"},
        {"Combo_Engines.EngineComboID","Engine swap combination"},
        {"ContentOffers.OfferId","Content offer id (title id << 32 | offer number): identity of a DLC pack"},
        {"ContentOffersMapping.Id","Row of the offer -> content (car / track) mapping"},
        {"ContentOffersMapping.ContentId","Id of the car or track an offer unlocks"},
        {"ContentOffers.PubOfferingID","Published offering number of a content offer (the .puboffer file name)"},
        {"MultiDiscContentPacks.PubOfferingID","Published offering number of a multi-disc content pack"},
        {"List_PartsStrings.Id","Part name string"},
        {"List_PartManufacturer.Id","Part manufacturer / aftermarket upgrade part"},
        {"List_PartAttribute.PartAttributeID","Part attribute"},
        {"List_TorqueCurve.TorqueCurveID","Torque curve (engine * 1000 + n)"},
        {"List_BadgeAndTitleUnlocks.UnlockId","Badge / title unlock"},
        {"CarClasses.Id","Car class (D..X)"},
        {"List_Country.CountryID","Country"},
    };

    static string Describe(string table, string col, bool key)
    {
        string d;
        if (What.TryGetValue(table + "." + col, out d)) return d;
        string t = table;
        if (t.StartsWith("List_Upgrade")) return "Upgrade option: " + Spaced(t.Substring(12)) + (key ? " (car * 1000 + n)" : "");
        if (t.StartsWith("Combo_")) return "Combination: " + Spaced(t.Substring(6));
        if (t.StartsWith("List_")) t = t.Substring(5);
        else if (t.StartsWith("Data_")) t = t.Substring(5);
        return (key ? "Id of " : "Id column " + col + " of ") + Spaced(t);
    }

    static string Spaced(string s)
    {
        return Regex.Replace(s.Replace('_', ' '), "(?<=[a-z0-9])(?=[A-Z])", " ");
    }

    static string Js(string s) { return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""; }
    static string Num(long v) { return v.ToString(CultureInfo.InvariantCulture); }

    static void WriteJson(string path, Dictionary<string, GameData> data)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("{\n  \"format\": \"forza-id-limits\",\n  \"version\": 1,\n  \"generated\": " + Js(DateTime.Now.ToString("yyyy-MM-dd")) + ",\n");
        sb.Append("  \"notes\": [\n    \"max = largest value of the column in any archive of the game (the game's own gamedb.slt plus every DLC / title update package).\",\n    \"byArchive = the largest value in each archive (archive id 'base' = the game's gamedb.slt, otherwise the package file name).\",\n    \"key = primary key (or first id column of tables without one); the other columns are references to other tables.\",\n    \"Values above 2^53 (OfferId) are exact here; spreadsheets show them as text.\"\n  ],\n");
        sb.Append("  \"games\": {\n");
        for (int gi = 0; gi < Games.Length; gi++)
        {
            string g = Games[gi]; GameData gd = data[g];
            sb.Append("    " + Js(g) + ": {\n      \"name\": " + Js(GameName[g]) + ",\n      \"titleId\": " + Js(Title[g]) + ",\n      \"archives\": [\n");
            for (int i = 0; i < gd.Archives.Count; i++)
            {
                Arch ar = gd.Archives[i];
                sb.Append("        { \"id\": " + Js(ar.Id) + ", \"kind\": " + Js(ar.Kind) + ", \"name\": " + Js(ar.Display) + ", \"databases\": [" + string.Join(", ", ar.Dbs.Select(Js)) + "], \"pubOffers\": [" + string.Join(", ", ar.PubOffers.Select(Num)) + "] }" + (i + 1 < gd.Archives.Count ? "," : "") + "\n");
            }
            sb.Append("      ],\n      \"tables\": {\n");
            int ti = 0;
            foreach (KeyValuePair<string, Table> kv in gd.Tables)
            {
                ti++;
                sb.Append("        " + Js(kv.Key) + ": { \"rows\": " + Num(kv.Value.Rows) + ", \"columns\": {\n");
                int ci = 0;
                foreach (KeyValuePair<string, Col> cv in Ordered(kv.Value))
                {
                    ci++; Col c = cv.Value;
                    sb.Append("          " + Js(cv.Key) + ": { \"key\": " + (c.Key ? "true" : "false"));
                    if (c.Max.HasValue)
                    {
                        sb.Append(", \"max\": " + Num(c.Max.Value) + ", \"maxIn\": " + Js(c.MaxArch));
                        if (c.Max.Value > 9007199254740992L) sb.Append(", \"maxHex\": " + Js(c.Max.Value.ToString("X")));
                    }
                    if (c.Min.HasValue) sb.Append(", \"min\": " + Num(c.Min.Value));
                    sb.Append(", \"byArchive\": { " + string.Join(", ", c.By.Select(x => Js(x.Key) + ": " + Num(x.Value))) + " } }" + (ci < kv.Value.Cols.Count ? "," : "") + "\n");
                }
                sb.Append("        } }" + (ti < gd.Tables.Count ? "," : "") + "\n");
            }
            sb.Append("      }\n    }" + (gi + 1 < Games.Length ? "," : "") + "\n");
        }
        sb.Append("  },\n  \"overall\": [\n");
        List<Overall> all = BuildOverall(data);
        for (int i = 0; i < all.Count; i++)
        {
            Overall o = all[i];
            sb.Append("    { \"table\": " + Js(o.Table) + ", \"column\": " + Js(o.Column) + ", \"what\": " + Js(o.What) + ", \"max\": " + Num(o.Max) + ", \"game\": " + Js(o.Game) + ", \"archive\": " + Js(o.Archive) + ", \"perGame\": { " + string.Join(", ", o.PerGame.Select(x => Js(x.Key) + ": " + Num(x.Value))) + " } }" + (i + 1 < all.Count ? "," : "") + "\n");
        }
        sb.Append("  ],\n  \"offers\": {\n");
        List<string> og = new List<string>();
        foreach (string g in Games)
        {
            GameData gd = data[g];
            Table co; Col oid = null, pub = null;
            if (gd.Tables.TryGetValue("ContentOffers", out co)) { co.Cols.TryGetValue("OfferId", out oid); co.Cols.TryGetValue("PubOfferingID", out pub); }
            List<long> files = gd.Archives.SelectMany(x => x.PubOffers).Distinct().OrderBy(x => x).ToList();
            if (oid == null && pub == null && files.Count == 0) continue;
            List<string> parts = new List<string>();
            if (oid != null && oid.Max.HasValue) parts.Add("\"maxOfferId\": " + Num(oid.Max.Value) + ", \"maxOfferIdHex\": " + Js(oid.Max.Value.ToString("X16")) + ", \"maxOfferIdIn\": " + Js(oid.MaxArch) + ", \"offerIdByArchive\": { " + string.Join(", ", oid.By.Select(x => Js(x.Key) + ": " + Num(x.Value))) + " }");
            if (pub != null && pub.Max.HasValue) parts.Add("\"maxPubOfferingId\": " + Num(pub.Max.Value) + ", \"maxPubOfferingIdIn\": " + Js(pub.MaxArch));
            if (files.Count > 0) parts.Add("\"maxPubOfferFile\": " + Num(files.Max()) + ", \"pubOfferFiles\": [" + string.Join(", ", files.Select(Num)) + "]");
            og.Add("    " + Js(g) + ": { " + string.Join(", ", parts) + " }");
        }
        sb.Append(string.Join(",\n", og) + "\n  }\n}\n");
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }

    static IEnumerable<KeyValuePair<string, Col>> Ordered(Table t)
    {
        return t.Cols.Where(x => x.Value.Key).Concat(t.Cols.Where(x => !x.Value.Key));
    }

    class Overall { public string Table, Column, What, Game, Archive; public long Max; public SortedDictionary<string, long> PerGame = new SortedDictionary<string, long>(); }

    static List<Overall> BuildOverall(Dictionary<string, GameData> data)
    {
        Dictionary<string, Overall> map = new Dictionary<string, Overall>();
        foreach (string g in Games)
            foreach (KeyValuePair<string, Table> kv in data[g].Tables)
                foreach (KeyValuePair<string, Col> cv in kv.Value.Cols)
                {
                    bool offerCol = kv.Key == "ContentOffers" || kv.Key == "ContentOffersMapping" || kv.Key == "MultiDiscContentPacks";
                    if ((!cv.Value.Key && !(offerCol && IdLike(cv.Key) || cv.Key == "PubOfferingID")) || !cv.Value.Max.HasValue) continue;
                    string k = kv.Key + "." + cv.Key;
                    Overall o;
                    if (!map.TryGetValue(k, out o)) { o = new Overall(); o.Table = kv.Key; o.Column = cv.Key; o.What = Describe(kv.Key, cv.Key, cv.Value.Key); o.Max = long.MinValue; map[k] = o; }
                    o.PerGame[g] = cv.Value.Max.Value;
                    if (cv.Value.Max.Value > o.Max) { o.Max = cv.Value.Max.Value; o.Game = g; o.Archive = cv.Value.MaxArch; }
                }
        foreach (string g in Games)
        {
            List<long> files = data[g].Archives.SelectMany(x => x.PubOffers).ToList();
            if (files.Count == 0) continue;
            Overall o;
            if (!map.TryGetValue("(.puboffer file).Number", out o)) { o = new Overall(); o.Table = "(.puboffer file)"; o.Column = "Number"; o.What = "Number in the name of Media\\<n>.puboffer inside a DLC package"; o.Max = long.MinValue; map["(.puboffer file).Number"] = o; }
            long m = files.Max(); o.PerGame[g] = m;
            if (m > o.Max) { o.Max = m; o.Game = g; o.Archive = data[g].Archives.First(x => x.PubOffers.Contains(m)).Id; }
        }
        return map.Values.OrderBy(x => x.Table, StringComparer.Ordinal).ThenBy(x => x.Column, StringComparer.Ordinal).ToList();
    }

    class Sheet { public string Name; public List<object[]> Rows = new List<object[]>(); public int FrozenCols; public int HeaderRows; }

    static Dictionary<string, GameData> All;
    static string Disp(string g, string id) { Arch a = All[g].Archives.First(x => x.Id == id); return a.Kind == "base" ? "base game" : a.Display + " (" + a.File + ")"; }

    static void WriteXlsx(string path, Dictionary<string, GameData> data)
    {
        List<Sheet> sheets = new List<Sheet>();
        foreach (string g in Games)
        {
            GameData gd = data[g];
            Sheet sh = new Sheet(); sh.Name = g;
            List<Arch> ars = gd.Archives.Where(x => x.HasDb).ToList();
            List<object> r1 = new List<object>(new object[] { "Table", "Column", "Key", "Rows", "MAX", "MAX in" });
            List<object> r2 = new List<object>(new object[] { "", "", "", "", "", "Package file" });
            List<object> r3 = new List<object>(new object[] { "", "", "", "", "", "Offer (.puboffer)" });
            foreach (Arch ar in ars) { r1.Add(ar.Display); r2.Add(ar.Kind == "base" ? "gamedb.slt" : ar.File); r3.Add(string.Join(",", ar.PubOffers.Select(Num))); }
            sh.Rows.Add(r1.ToArray()); sh.Rows.Add(r2.ToArray()); sh.Rows.Add(r3.ToArray());
            sh.HeaderRows = 3; sh.FrozenCols = 6;
            foreach (KeyValuePair<string, Table> kv in gd.Tables)
                foreach (KeyValuePair<string, Col> cv in Ordered(kv.Value))
                {
                    List<object> row = new List<object>(new object[] { kv.Key, cv.Key, cv.Value.Key ? "key" : "", kv.Value.Rows, cv.Value.Max.HasValue ? (object)cv.Value.Max.Value : "", cv.Value.MaxArch == null ? "" : Disp(g, cv.Value.MaxArch) });
                    foreach (Arch ar in ars) { long v; row.Add(cv.Value.By.TryGetValue(ar.Id, out v) ? (object)v : ""); }
                    sh.Rows.Add(row.ToArray());
                }
            sheets.Add(sh);
        }
        Sheet fin = new Sheet(); fin.Name = "MAX ID"; fin.HeaderRows = 1; fin.FrozenCols = 2;
        fin.Rows.Add(new object[] { "Table", "Column", "What it is", "MAX ID", "From game", "Archive", "FM2", "FM3", "FM4", "FH1", "FH2" });
        foreach (Overall o in BuildOverall(data))
        {
            List<object> row = new List<object>(new object[] { o.Table, o.Column, o.What, o.Max, o.Game, Disp(o.Game, o.Archive) });
            foreach (string g in Games) { long v; row.Add(o.PerGame.TryGetValue(g, out v) ? (object)v : ""); }
            fin.Rows.Add(row.ToArray());
        }
        sheets.Add(fin);
        if (File.Exists(path)) File.Delete(path);
        using (ZipArchive z = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            Put(z, "[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" + string.Concat(sheets.Select((s, i) => "<Override PartName=\"/xl/worksheets/sheet" + (i + 1) + ".xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>")) + "</Types>");
            Put(z, "_rels/.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
            Put(z, "xl/workbook.xml", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>" + string.Concat(sheets.Select((s, i) => "<sheet name=\"" + s.Name + "\" sheetId=\"" + (i + 1) + "\" r:id=\"rId" + (i + 1) + "\"/>")) + "</sheets></workbook>");
            Put(z, "xl/_rels/workbook.xml.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" + string.Concat(sheets.Select((s, i) => "<Relationship Id=\"rId" + (i + 1) + "\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet" + (i + 1) + ".xml\"/>")) + "<Relationship Id=\"rId" + (sheets.Count + 1) + "\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/></Relationships>");
            Put(z, "xl/styles.xml", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><fonts count=\"2\"><font><sz val=\"10\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"10\"/><color rgb=\"FFFFFFFF\"/><name val=\"Calibri\"/></font></fonts><fills count=\"3\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill><fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFC8102E\"/></patternFill></fill></fills><borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders><cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs><cellXfs count=\"2\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/><xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\"><alignment wrapText=\"1\" vertical=\"top\"/></xf></cellXfs></styleSheet>");
            for (int i = 0; i < sheets.Count; i++) Put(z, "xl/worksheets/sheet" + (i + 1) + ".xml", SheetXml(sheets[i]));
        }
    }

    static void Put(ZipArchive z, string name, string text)
    {
        ZipArchiveEntry e = z.CreateEntry(name, CompressionLevel.Optimal);
        using (StreamWriter w = new StreamWriter(e.Open(), new UTF8Encoding(false))) w.Write(text);
    }

    static string ColName(int i)
    {
        string s = "";
        for (i++; i > 0; i = (i - 1) / 26) s = (char)('A' + (i - 1) % 26) + s;
        return s;
    }

    static string Esc(string s) { return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;"); }

    static string SheetXml(Sheet sh)
    {
        StringBuilder sb = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
        int cols = sh.Rows.Max(r => r.Length);
        sb.Append("<sheetViews><sheetView workbookViewId=\"0\"><pane xSplit=\"" + sh.FrozenCols + "\" ySplit=\"" + sh.HeaderRows + "\" topLeftCell=\"" + ColName(sh.FrozenCols) + (sh.HeaderRows + 1) + "\" activePane=\"bottomRight\" state=\"frozen\"/></sheetView></sheetViews>");
        sb.Append("<sheetFormatPr defaultRowHeight=\"13\"/><cols>");
        for (int c = 0; c < cols; c++)
        {
            int w;
            if (sh.Name == "MAX ID") w = (c == 0) ? 34 : c == 1 ? 26 : c == 2 ? 60 : c == 5 ? 46 : 20;
            else w = (c == 0) ? 36 : c == 1 ? 30 : (c == 2 || c == 3) ? 7 : c == 4 ? 22 : c == 5 ? 28 : 18;
            sb.Append("<col min=\"" + (c + 1) + "\" max=\"" + (c + 1) + "\" width=\"" + w + "\" customWidth=\"1\"/>");
        }
        sb.Append("</cols><sheetData>");
        for (int r = 0; r < sh.Rows.Count; r++)
        {
            sb.Append("<row r=\"" + (r + 1) + "\">");
            object[] row = sh.Rows[r];
            for (int c = 0; c < row.Length; c++)
            {
                object v = row[c];
                string cell = ColName(c) + (r + 1);
                string style = r < sh.HeaderRows ? " s=\"1\"" : "";
                if (v is long || v is int)
                {
                    long n = Convert.ToInt64(v);
                    if (n > 9007199254740992L || n < -9007199254740992L) sb.Append("<c r=\"" + cell + "\" t=\"inlineStr\"" + style + "><is><t>" + n + "</t></is></c>");
                    else sb.Append("<c r=\"" + cell + "\"" + style + "><v>" + n + "</v></c>");
                }
                else
                {
                    string s = Convert.ToString(v);
                    if (s == "" && r >= sh.HeaderRows) continue;
                    sb.Append("<c r=\"" + cell + "\" t=\"inlineStr\"" + style + "><is><t xml:space=\"preserve\">" + Esc(s) + "</t></is></c>");
                }
            }
            sb.Append("</row>");
        }
        sb.Append("</sheetData><autoFilter ref=\"A" + sh.HeaderRows + ":" + ColName(cols - 1) + sh.Rows.Count + "\"/></worksheet>");
        return sb.ToString();
    }
}
