namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;

    // The DLC packages installed for a game (Xenia: <CarPort.Xenia>\<title id>\00000002\<package>, STFS files),
    // read when the game's folder is opened so the database view shows their cars and tracks (TODO 7.3, user
    // 2026-10-24, all games). A package is extracted once (DlcSource.OpenPackage keeps it under %TEMP%).
    // Nothing is ever written to the game: the merged database is a read-only view.
    public class InstalledDlc
    {
        public string File;
        public string Name;
        public int Offer;
        public DlcSource Source;
        public byte[] Merge;            // db\patch\*_merge.slt, null when the package has none
        public string Problem;          // why the package is not in the view, or null

        public static uint TitleId(ForzaGame game)
        {
            switch (game)
            {
                case ForzaGame.Motorsport2: return 0x4D5307EA;
                case ForzaGame.Motorsport3: return 0x4D53084D;
                case ForzaGame.Motorsport4: return 0x4D530910;
                case ForzaGame.Horizon1: return 0x4D5309C9;
                case ForzaGame.Horizon2: return 0x4D530AA4;
            }
            return 0;
        }

        // Xenia's marketplace content folder of the game, or null when CarPort.Xenia is not set or has none.
        public static string ContentFolder(ForzaGame game)
        {
            string xenia = AppSettings.Get("CarPort.Xenia", "");
            uint title = TitleId(game);
            if (xenia.Length == 0 || title == 0)
            {
                return null;
            }
            string folder = Path.Combine(Path.Combine(xenia, title.ToString("X8", CultureInfo.InvariantCulture)), "00000002");
            return Directory.Exists(folder) ? folder : null;
        }

        // Every package of the folder that is a DLC of the game, lowest offer first. A file that is not a package of
        // this game, or has no database, is listed with its Problem. progress: called with the file being read.
        public static List<InstalledDlc> Scan(ForzaGame game, string folder, Action<string> progress)
        {
            List<InstalledDlc> found = new List<InstalledDlc>();
            uint title = TitleId(game);
            foreach (string file in Directory.GetFiles(folder))
            {
                InstalledDlc dlc = new InstalledDlc();
                dlc.File = file;
                dlc.Name = Path.GetFileName(file);
                try
                {
                    if (!StfsReader.IsPackage(file))
                    {
                        continue;
                    }
                    if (progress != null)
                    {
                        progress(dlc.Name);
                    }
                    DlcSource source = DlcSource.OpenPackage(file, true);
                    if (!string.IsNullOrEmpty(source.Title))
                    {
                        dlc.Name = source.Title;
                    }
                    if (source.TitleId != title)
                    {
                        continue;   // another game's content (the folder is per title; theme or avatar packs)
                    }
                    dlc.Source = source;
                    dlc.Offer = source.Offer;
                    if (source.Problem != null)
                    {
                        dlc.Problem = source.Problem;
                    }
                    else
                    {
                        dlc.Merge = source.Merge();
                        if (dlc.Merge == null)
                        {
                            dlc.Problem = "no database rows (db\\patch\\*_merge.slt): no cars or tracks to show";
                        }
                    }
                }
                catch (Exception e)
                {
                    dlc.Problem = e.Message;
                }
                found.Add(dlc);
            }
            found.Sort(delegate(InstalledDlc a, InstalledDlc b) { return a.Offer.CompareTo(b.Offer); });
            return found;
        }

        // The rows of every package's database added to db, in offer order (the game applies them after its own).
        // A row whose key already exists is a clash: the first one is kept and the clash is reported.
        // Fills db.DlcCars / DlcTracks and makes db read-only. Returns the packages merged.
        public static int Apply(GameDatabase db, List<InstalledDlc> dlcs, StringBuilder report)
        {
            int merged = 0;
            foreach (InstalledDlc dlc in dlcs)
            {
                if (dlc.Merge == null)
                {
                    report.AppendLine(dlc.Name + ": skipped, " + dlc.Problem);
                    continue;
                }
                try
                {
                    using (SqliteDatabase patch = SqliteDatabase.FromBytes(dlc.Merge))
                    {
                        int rows = 0;
                        int clashes = 0;
                        Dictionary<string, List<string>> own = DbIds.Schema(db.Sql);
                        foreach (KeyValuePair<string, List<string>> table in DbIds.Schema(patch))
                        {
                            List<string> columns;
                            if (table.Key.StartsWith("sqlite_", StringComparison.OrdinalIgnoreCase) || !own.TryGetValue(table.Key, out columns))
                            {
                                continue;
                            }
                            List<string> shared = table.Value.FindAll(delegate(string c) { return columns.Exists(delegate(string x) { return string.Equals(x, c, StringComparison.OrdinalIgnoreCase); }); });
                            if (shared.Count == 0)
                            {
                                continue;
                            }
                            string names = string.Join(", ", shared.ConvertAll<string>(delegate(string c) { return SqliteDatabase.Quote(c); }).ToArray());
                            string marks = string.Join(", ", shared.ConvertAll<string>(delegate(string c) { return "?"; }).ToArray());
                            foreach (object[] row in patch.Query("SELECT " + names + " FROM " + SqliteDatabase.Quote(table.Key)).Rows)
                            {
                                try
                                {
                                    db.Sql.Execute("INSERT INTO " + SqliteDatabase.Quote(table.Key) + " (" + names + ") VALUES (" + marks + ")", row);
                                    rows++;
                                }
                                catch (Exception)
                                {
                                    clashes++;
                                }
                            }
                        }
                        Mark(db.DlcCars, patch, "Data_Car", "Id", dlc.Name);
                        Mark(db.DlcTracks, patch, "Tracks", "Id", dlc.Name);
                        report.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0} (offer {1}): {2} rows{3}", dlc.Name, dlc.Offer, rows, (clashes > 0) ? ", " + clashes + " already in the database (kept)" : ""));
                        merged++;
                    }
                }
                catch (Exception e)
                {
                    report.AppendLine(dlc.Name + ": not merged, " + e.Message);
                }
            }
            db.ReadOnly = merged > 0;
            return merged;
        }

        private static void Mark(Dictionary<long, string> marks, SqliteDatabase patch, string table, string column, string pack)
        {
            if (!DbIds.HasTable(patch, table))
            {
                return;
            }
            foreach (object[] row in patch.Query("SELECT " + SqliteDatabase.Quote(column) + " FROM " + SqliteDatabase.Quote(table)).Rows)
            {
                if (row[0] is long && !marks.ContainsKey((long) row[0]))
                {
                    marks[(long) row[0]] = pack;
                }
            }
        }

        // The packages' strings (cars and tracks names) on top of the game's table.
        public static void ApplyStrings(StringTable table, List<InstalledDlc> dlcs, string language)
        {
            foreach (InstalledDlc dlc in dlcs)
            {
                if (dlc.Merge == null || dlc.Source == null)
                {
                    continue;
                }
                try
                {
                    foreach (KeyValuePair<string, byte[]> file in dlc.Source.StringFiles(language))
                    {
                        table.Overlay(file.Key, file.Value);
                    }
                }
                catch (Exception)
                {
                    // a package without readable text keeps the ids in the list
                }
            }
        }
    }
}
