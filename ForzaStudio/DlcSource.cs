namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Xml;

    // A car DLC package of another Forza (Forza Horizon, later Forza Motorsport 3) as the source of a
    // port, on its own: the base source game is never needed (ROADMAP topic 3, decided 2026-10-17).
    //
    // An official FH1 pack (the extracted folder of the STFS package, 20 checked 2026-10-17):
    //   Media\<offer>.puboffer
    //   Media\DLCZips\zipmount.xml              the zips and folders to mount and where
    //   Media\DLCZips\<offer>_pri_65.zip        the whole Media tree (LZX): media\cars\<car>\,
    //                                           media\wheels\<car>\, media\db\patch\<n>_merge.slt,
    //                                           UI\Textures thumbnails, audio
    //   Media\DLCZips\<offer>_pri_65\Media\...  loose files (sound banks), mounted at the game root
    //   Media\DLCZips\StringTables_pri_<P>.zip  <LANG>/<Table>.str
    //   Media\DLCZips\cameras_pri_<P>.zip, EngineTuning_pri_<P>\, HarmonicTuning_pri_<P>\ ...
    //                                           mounted where zipmount.xml says
    // A zip or folder that zipmount.xml does not list is mounted at the game root (GAME:\), like the
    // _pri_65 ones; a higher priority (<name>_pri_<N>) goes on top.
    //
    // Stage() lays the package out as a source game folder that CarPort reads as it reads a game:
    //   Media\db\gamedb.slt            the package's merge database (only the pack's rows)
    //   Media\cars\<car>.zip           Media\wheels\<wheel>.zip     (stored)
    //   Media\StringTables\<LANG>.zip  <Table>.str
    //   Media\audio\cars\CarModelTuning.zip, Engines\EngineTuning.zip, Engines\HarmonicTuning.zip
    //   Media\audio\cars\Engines\Soundbanks\LOD1\*.fsb, Media\UI\Textures\Thumbnails\*.xds (loose)
    //   DlcSource.txt                  title id, offer, package: GameInfo.Detect reads the game from it
    // An FM3 pack (Hyundai Car Pack, checked 2026-10-18) is the same but has no .puboffer (the offer is in
    // DLCZips\0016_pri_65.zip's name), no ContentOffers in its merge (Media\LicenseMasks\*.sql unlocks the
    // cars instead) and its digital dashes in media\DigitalGauges\<dash>\ (staged as <dash>.zip).
    // What the pack lacks (shared engine sounds, engine swaps only in the base game) the port takes from
    // FM4 or asks in the review.
    // OpenPackage() takes the package file itself (STFS, StfsReader) and extracts it first.
    public class DlcSource
    {
        public const string Marker = "DlcSource.txt";

        public string Folder;           // the package folder (it holds Media\<offer>.puboffer)
        public int Offer;
        public uint TitleId;            // the source game's, from the merge database's ContentOffers.ID
        public ForzaGame Game;
        public string Problem;          // why the package can't be used, or null
        public string Title;            // the package's display name (STFS header), when it was read from a package file

        private readonly List<Layer> layers = new List<Layer>();

        private delegate byte[] Reader();

        private class Layer
        {
            public string Path;         // a zip or a folder
            public string Mount;        // relative to the game root, "" or "Media\audio\..."
            public int Priority;
        }

        // The package in a folder: the extracted package itself, or its Media folder.
        public static DlcSource Open(string folder)
        {
            return Open(folder, 0);
        }

        // titleId: the package header's, when the package file was read (0 for a folder).
        private static DlcSource Open(string folder, uint titleId)
        {
            string root = Path.GetFullPath(folder).TrimEnd('\\');
            if (string.Equals(Path.GetFileName(root), "Media", StringComparison.OrdinalIgnoreCase))
            {
                root = Path.GetDirectoryName(root);
            }
            int offer = ReadOffer(Path.Combine(root, "Media"));
            if (offer < 0)
            {
                return null;
            }
            DlcSource source = new DlcSource();
            source.Folder = root;
            source.Offer = offer;
            source.TitleId = titleId;
            try
            {
                source.ReadLayers();
                source.ReadTitle();
            }
            catch (Exception e)
            {
                source.Problem = e.Message;
            }
            return source;
        }

        // An Xbox 360 package file (STFS, as the console stores it), extracted under
        // %TEMP%\ForzaStudio\DlcPackage\<file name> and kept while the file is unchanged. Problem tells why
        // it can't be used: an FM4 package, an incomplete file, no cars.
        public static DlcSource OpenPackage(string file)
        {
            return OpenPackage(file, false);
        }

        // sameGame: an FM4 package is accepted too (the packages installed for the game being viewed, not a port source).
        public static DlcSource OpenPackage(string file, bool sameGame)
        {
            DlcSource failed = new DlcSource();
            failed.Folder = file;
            try
            {
                StfsReader package = StfsReader.Open(file);
                failed.TitleId = package.TitleId;
                failed.Game = GameInfo.FromTitleId(package.TitleId);
                failed.Title = package.DisplayName;
                if (failed.Game == ForzaGame.Motorsport4 && !sameGame)
                {
                    failed.Problem = "a Forza Motorsport 4 package (" + package.DisplayName + "): a port takes cars from FH, FH2 or FM3 packages";
                    return failed;
                }
                string folder = Path.Combine(Path.Combine(Path.Combine(Path.GetTempPath(), "ForzaStudio"), "DlcPackage"), Path.GetFileName(file));
                string marker = Path.Combine(folder, "StfsPackage.txt");
                FileInfo info = new FileInfo(file);
                string stamp = info.FullName + "|" + info.Length.ToString(CultureInfo.InvariantCulture) + "|" + info.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture);
                if (!File.Exists(marker) || File.ReadAllText(marker) != stamp)
                {
                    if (Directory.Exists(folder))
                    {
                        Directory.Delete(folder, true);
                    }
                    string stage = StageFolder(Path.GetFileName(file));
                    if (Directory.Exists(stage))
                    {
                        Directory.Delete(stage, true);
                    }
                    try
                    {
                        package.Extract(folder);
                    }
                    catch (Exception)
                    {
                        if (Directory.Exists(folder))
                        {
                            Directory.Delete(folder, true);
                        }
                        throw;
                    }
                    File.WriteAllText(marker, stamp);
                }
                DlcSource source = Open(folder, package.TitleId);
                if (source == null)
                {
                    failed.Problem = "not a car pack (no Media\\DLCZips): " + package.DisplayName;
                    return failed;
                }
                source.Title = package.DisplayName;
                if (source.TitleId == 0)
                {
                    source.TitleId = package.TitleId;
                    source.Game = failed.Game;
                }
                return source;
            }
            catch (Exception e)
            {
                failed.Problem = e.Message;
                return failed;
            }
        }

        // The offer of a package's Media folder: FH1 names a <offer>.puboffer file; FM3 has none, its offer
        // is in the media layer's name (DLCZips\0016_pri_65.zip). -1 when the folder is not a package.
        private static int ReadOffer(string media)
        {
            int offer;
            string[] offers = Directory.Exists(media) ? Directory.GetFiles(media, "*.puboffer") : new string[0];
            if (offers.Length == 1)
            {
                return int.TryParse(Path.GetFileNameWithoutExtension(offers[0]), NumberStyles.None, CultureInfo.InvariantCulture, out offer) ? offer : -1;
            }
            string zips = Path.Combine(media, "DLCZips");
            if (offers.Length == 0 && Directory.Exists(zips))
            {
                List<string> layers = new List<string>(Directory.GetFiles(zips, "*_pri_65.zip"));
                layers.AddRange(Directory.GetDirectories(zips, "*_pri_65"));
                foreach (string layer in layers)
                {
                    string name = Path.GetFileName(layer);
                    if (int.TryParse(name.Substring(0, name.IndexOf("_pri_", StringComparison.OrdinalIgnoreCase)), NumberStyles.None, CultureInfo.InvariantCulture, out offer))
                    {
                        return offer;
                    }
                }
                // FM2: DLC<n>_pri_<priority> (DLC1_pri_60 = the Nissan pack), the offer is n.
                List<string> fm2 = new List<string>(Directory.GetFiles(zips, "DLC*_pri_*.zip"));
                foreach (string layer in fm2)
                {
                    string name = Path.GetFileNameWithoutExtension(layer);
                    int end = name.IndexOf('_');
                    if (end > 3 && int.TryParse(name.Substring(3, end - 3), NumberStyles.None, CultureInfo.InvariantCulture, out offer))
                    {
                        return offer;
                    }
                }
            }
            return -1;
        }

        // The car folders an FM2 package holds (media/cars/<NAME>/ with a .carbin), by layer.
        private List<string> Fm2CarFolders()
        {
            List<string> cars = new List<string>();
            foreach (Layer layer in this.layers)
            {
                string prefix = (layer.Mount.Length > 0) ? layer.Mount.Replace('\\', '/') + "/" : string.Empty;
                foreach (KeyValuePair<string, Reader> file in Files(layer))
                {
                    string name = (prefix + file.Key).Replace('\\', '/');
                    string[] part = name.Split('/');
                    if (part.Length == 4 && part[0].Equals("media", StringComparison.OrdinalIgnoreCase) && part[1].Equals("cars", StringComparison.OrdinalIgnoreCase)
                        && part[3].EndsWith(".carbin", StringComparison.OrdinalIgnoreCase) && cars.FindIndex(delegate(string c) { return string.Equals(c, part[2], StringComparison.OrdinalIgnoreCase); }) < 0)
                    {
                        cars.Add(part[2]);
                    }
                }
            }
            return cars;
        }

        // Every package below a folder (a content folder, 00000002, or a folder of extracted packages).
        public static List<DlcSource> Find(string folder)
        {
            List<DlcSource> found = new List<DlcSource>();
            DlcSource self = Open(folder);
            if (self != null)
            {
                found.Add(self);
                return found;
            }
            foreach (string sub in Directory.GetDirectories(folder))
            {
                found.AddRange(Find(sub));
            }
            return found;
        }

        // True for a folder Stage() built.
        public static bool IsStage(string root)
        {
            return root != null && File.Exists(Path.Combine(root, Marker));
        }

        // True for the stage of an FM2 package (cars still in FM2's form): CarPort lays each car out on an FM4 donor
        // (Fm2Source) from it, with the FM2 game as the rest. The FM2 game folder staged by Fm2Source is not one.
        public static bool IsFm2Raw(string root)
        {
            return IsStage(root) && Array.IndexOf(File.ReadAllLines(Path.Combine(root, Marker)), "raw=fm2") >= 0;
        }

        // The title id written in a staged folder, 0 when it is not one.
        public static uint StageTitleId(string root)
        {
            if (!IsStage(root))
            {
                return 0;
            }
            foreach (string line in File.ReadAllLines(Path.Combine(root, Marker)))
            {
                uint id;
                if (line.StartsWith("title=", StringComparison.Ordinal) && uint.TryParse(line.Substring(6), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out id))
                {
                    return id;
                }
            }
            return 0;
        }

        private string DlcZips
        {
            get { return Path.Combine(Path.Combine(this.Folder, "Media"), "DLCZips"); }
        }

        private void ReadLayers()
        {
            Dictionary<string, string> mounts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string list = Path.Combine(this.DlcZips, "zipmount.xml");
            if (File.Exists(list))
            {
                XmlDocument xml = new XmlDocument();
                try
                {
                    xml.Load(list);
                }
                catch (XmlException)
                {
                    // Empty in the packages cut short (offers 2100 and 2500).
                    throw new InvalidDataException("zipmount.xml is damaged: the package is incomplete.");
                }
                foreach (XmlElement zip in xml.SelectNodes("/zipmount/zip"))
                {
                    string mount = zip.GetAttribute("Mount");
                    mounts[zip.GetAttribute("Name")] = GamePaths.IsGamePath(mount) ? GamePaths.RelativePath(mount).TrimEnd('\\') : string.Empty;
                }
            }
            List<string> paths = new List<string>(Directory.GetFiles(this.DlcZips, "*.zip"));
            paths.AddRange(Directory.GetDirectories(this.DlcZips));
            foreach (string path in paths)
            {
                string name = Path.GetFileName(path);
                string stem = Path.GetFileNameWithoutExtension(path);
                int at = (Directory.Exists(path) ? name : stem).LastIndexOf("_pri_", StringComparison.OrdinalIgnoreCase);
                int priority;
                if (at < 0 || !int.TryParse((Directory.Exists(path) ? name : stem).Substring(at + 5), NumberStyles.None, CultureInfo.InvariantCulture, out priority))
                {
                    continue;
                }
                Layer layer = new Layer();
                layer.Path = path;
                layer.Priority = priority;
                string mount;
                layer.Mount = mounts.TryGetValue(name, out mount) ? mount : string.Empty;
                if (File.Exists(path))
                {
                    // A package cut short (offers 2100 and 2500: 3 and 9.7 MB of a 53 and 48 MB zip)
                    // fails here.
                    try
                    {
                        new ForzaZip(path);
                    }
                    catch (Exception e)
                    {
                        throw new InvalidDataException(name + " is damaged or incomplete (" + e.Message + ")");
                    }
                }
                this.layers.Add(layer);
            }
            this.layers.Sort(delegate(Layer a, Layer b) { return a.Priority.CompareTo(b.Priority); });
        }

        // The merge database's ContentOffers.ID starts with the title id (FH1 4D5309C9...).
        private void ReadTitle()
        {
            byte[] merge = this.MergeDatabase();
            if (merge == null && (this.TitleId == Fm2Source.TitleId || this.TitleId == 0))
            {
                // FM2: no database in the package, the cars are loose folders (media\cars\<NAME>\); the rows come
                // from the FM2 game set in Tools > Settings (gamedb) merged with the package (Stage).
                if (this.Fm2CarFolders().Count > 0)
                {
                    this.TitleId = Fm2Source.TitleId;
                    this.Game = ForzaGame.Motorsport2;
                    if (GameInfo.ConfiguredFolder(ForzaGame.Motorsport2) == null)
                    {
                        throw new InvalidDataException("An FM2 package needs the Forza Motorsport 2 game for its database: set the folder in Tools > Settings > Folders.");
                    }
                    return;
                }
                if (this.TitleId == Fm2Source.TitleId)
                {
                    throw new InvalidDataException("The package has no cars (a track or other content).");
                }
            }
            if (merge == null)
            {
                throw new InvalidDataException("The package has no cars (no db\\patch\\*_merge.slt): rims or other content only.");
            }
            using (SqliteDatabase db = SqliteDatabase.FromBytes(merge))
            {
                string id = (db.Scalar("SELECT name FROM sqlite_master WHERE type = 'table' AND name = 'ContentOffers'") != null) ? db.Scalar("SELECT ID FROM ContentOffers LIMIT 1") as string : null;
                uint title;
                if (id != null && id.Length >= 8 && uint.TryParse(id.Substring(0, 8), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out title))
                {
                    this.TitleId = title;
                }
                else if (this.TitleId == 0 && Directory.Exists(Path.Combine(Path.Combine(this.Folder, "Media"), "LicenseMasks")))
                {
                    // FM3: no ContentOffers, the cars are in the merge and unlocked by
                    // Media\LicenseMasks\*.sql (UPDATE Data_Car SET IsPurchased=1 ...), a folder FH1 and FM4 lack.
                    this.TitleId = 0x4D53084D;
                }
                else if (this.TitleId == 0)
                {
                    throw new InvalidDataException("The merge database has no ContentOffers id: the source game is unknown.");
                }
                this.Game = GameInfo.FromTitleId(this.TitleId);
                if (this.Game == ForzaGame.Motorsport2 && this.Fm2CarFolders().Count == 0)
                {
                    throw new InvalidDataException("The package has no cars (a track or other content).");
                }
            }
        }

        // The cars of the pack (Data_Car.MediaName of the merge database).
        public List<string> Cars()
        {
            if (this.Game == ForzaGame.Motorsport2)
            {
                return this.Fm2CarFolders();
            }
            List<string> cars = new List<string>();
            byte[] merge = this.MergeDatabase();
            if (merge == null)
            {
                return cars;
            }
            using (SqliteDatabase db = SqliteDatabase.FromBytes(merge))
            {
                foreach (object[] row in db.Query("SELECT MediaName FROM Data_Car ORDER BY MediaName").Rows)
                {
                    if (row[0] is string)
                    {
                        cars.Add((string) row[0]);
                    }
                }
            }
            return cars;
        }

        // The package's merge database (db\patch\*_merge.slt), or null.
        public byte[] Merge()
        {
            return this.MergeDatabase();
        }

        // The package's string tables of a language (StringTables_pri_<P>.zip, "<LANG>/<Table>.str"): table name and
        // file, lowest priority first so that applying them in order leaves the highest priority's text.
        public List<KeyValuePair<string, byte[]>> StringFiles(string language)
        {
            List<KeyValuePair<string, byte[]>> found = new List<KeyValuePair<string, byte[]>>();
            foreach (Layer layer in this.layers)
            {
                if (!Path.GetFileName(layer.Path).StartsWith("StringTables", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                string prefix = language + "/";
                foreach (KeyValuePair<string, Reader> file in Files(layer))
                {
                    string name = file.Key.Replace('\\', '/');
                    if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && name.EndsWith(".str", StringComparison.OrdinalIgnoreCase))
                    {
                        found.Add(new KeyValuePair<string, byte[]>(Path.GetFileNameWithoutExtension(name), file.Value()));
                    }
                }
            }
            return found;
        }

        // The highest priority db\patch\*_merge.slt of the package.
        private byte[] MergeDatabase()
        {
            byte[] found = null;
            foreach (Layer layer in this.layers)
            {
                string prefix = (layer.Mount.Length > 0) ? layer.Mount.Replace('\\', '/') + "/" : string.Empty;
                foreach (KeyValuePair<string, Reader> file in Files(layer))
                {
                    string name = (prefix + file.Key).Replace('\\', '/');
                    if (name.StartsWith("Media/db/patch/", StringComparison.OrdinalIgnoreCase) && name.EndsWith("_merge.slt", StringComparison.OrdinalIgnoreCase))
                    {
                        found = file.Value();
                    }
                }
            }
            return found;
        }

        // A layer's files: name relative to its mount ('/'), and how to read it.
        private static List<KeyValuePair<string, Reader>> Files(Layer layer)
        {
            List<KeyValuePair<string, Reader>> files = new List<KeyValuePair<string, Reader>>();
            if (Directory.Exists(layer.Path))
            {
                string root = Path.GetFullPath(layer.Path).TrimEnd('\\') + "\\";
                foreach (string file in Directory.GetFiles(layer.Path, "*", SearchOption.AllDirectories))
                {
                    string path = file;
                    files.Add(new KeyValuePair<string, Reader>(Path.GetFullPath(file).Substring(root.Length).Replace('\\', '/'), delegate { return File.ReadAllBytes(path); }));
                }
                return files;
            }
            ForzaZip zip = new ForzaZip(layer.Path);
            foreach (ForzaZip.Entry entry in zip.Entries)
            {
                if (!entry.IsDirectory)
                {
                    ForzaZip.Entry one = entry;
                    files.Add(new KeyValuePair<string, Reader>(entry.Name, delegate { return zip.Read(one); }));
                }
            }
            return files;
        }

        // The package laid out as a source game folder (see the class comment), under
        // %TEMP%\ForzaStudio\DlcSource\<package folder name>; kept and reused while its marker is there.
        public string Stage(StringBuilder report)
        {
            if (this.Problem != null)
            {
                throw new InvalidOperationException(this.Problem);
            }
            string root = StageFolder(Path.GetFileName(this.Folder));
            if (IsStage(root))
            {
                return root;
            }
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
            Directory.CreateDirectory(root);
            try
            {
                return this.Stage(root, report);
            }
            catch (Exception)
            {
                Directory.Delete(root, true);
                throw;
            }
        }

        private static string StageFolder(string name)
        {
            return Path.Combine(Path.Combine(Path.Combine(Path.GetTempPath(), "ForzaStudio"), "DlcSource"), name);
        }

        private string Stage(string root, StringBuilder report)
        {
            int count = 0;
            foreach (Layer layer in this.layers)
            {
                foreach (KeyValuePair<string, Reader> file in Files(layer))
                {
                    if (file.Key.EndsWith("_build_report.html", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    string path = Path.Combine(Path.Combine(root, layer.Mount), file.Key.Replace('/', '\\'));
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllBytes(path, file.Value());
                    count++;
                }
            }
            string media = Path.Combine(root, "Media");

            // The merge database as the game's.
            string patch = Path.Combine(Path.Combine(media, "db"), "patch");
            bool fm2 = this.Game == ForzaGame.Motorsport2;
            if (fm2)
            {
                this.MergeFm2Database(media, report);
            }
            else
            {
                string[] merges = Directory.Exists(patch) ? Directory.GetFiles(patch, "*_merge.slt") : new string[0];
                if (merges.Length != 1)
                {
                    throw new InvalidDataException(string.Format(CultureInfo.InvariantCulture, "The package has {0} merge databases; one is needed.", merges.Length));
                }
                File.Copy(merges[0], Path.Combine(Path.Combine(media, "db"), "gamedb.slt"));
            }

            // Folders the port opens as zips.
            int zips = 0;
            foreach (string kind in new string[] { "cars", "wheels" })
            {
                string parent = Path.Combine(media, kind);
                if (!Directory.Exists(parent))
                {
                    continue;
                }
                foreach (string folder in Directory.GetDirectories(parent))
                {
                    if (kind == "wheels" || Directory.GetFiles(folder, "*.carbin").Length > 0)
                    {
                        zips += ToZip(folder);
                    }
                }
            }
            // FM3 digital dashes (Media\DigitalGauges\<List_DigitalGauges.MediaName>.zip in the game).
            string gauges = Path.Combine(media, "DigitalGauges");
            if (Directory.Exists(gauges))
            {
                foreach (string folder in Directory.GetDirectories(gauges))
                {
                    zips += ToZip(folder);
                }
            }
            string tables = Path.Combine(media, "StringTables");
            if (fm2)
            {
                // The package's Data_Car.str (the car names) overrides one table of the game's string zip of each
                // language: the game's files fill in what the package lacks.
                Directory.CreateDirectory(tables);
                foreach (string zip in Directory.GetFiles(Path.Combine(Path.Combine(GameInfo.ConfiguredFolder(ForzaGame.Motorsport2), "Media"), "StringTables"), "*.zip"))
                {
                    string language = Path.Combine(tables, Path.GetFileNameWithoutExtension(zip).ToUpperInvariant());
                    Directory.CreateDirectory(language);
                    ForzaZip baseZip = new ForzaZip(zip);
                    foreach (ForzaZip.Entry entry in baseZip.Entries)
                    {
                        string path = Path.Combine(language, entry.Name.Replace('/', '\\'));
                        if (!entry.IsDirectory && !File.Exists(path))
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(path));
                            File.WriteAllBytes(path, baseZip.Read(entry));
                        }
                    }
                }
                foreach (string language in Directory.GetDirectories(tables))
                {
                    zips += ToZip(language);
                }
            }
            else if (Directory.Exists(tables))
            {
                foreach (string language in Directory.GetDirectories(tables))
                {
                    zips += ToZip(language);
                }
            }
            string audio = Path.Combine(Path.Combine(media, "audio"), "cars");
            string engines = Path.Combine(audio, "Engines");
            foreach (string folder in new string[] { Path.Combine(audio, "CarModelTuning"), Path.Combine(engines, "EngineTuning"), Path.Combine(engines, "HarmonicTuning") })
            {
                // Always there (an empty zip when the pack has none): the port opens them.
                Directory.CreateDirectory(folder);
                zips += ToZip(folder);
            }

            File.WriteAllLines(Path.Combine(root, Marker), new string[]
            {
                "title=" + this.TitleId.ToString("X8", CultureInfo.InvariantCulture),
                "offer=" + this.Offer.ToString(CultureInfo.InvariantCulture),
                "package=" + this.Folder,
                fm2 ? "raw=fm2" : "raw=",
            });
            if (report != null)
            {
                report.AppendLine(string.Format(CultureInfo.InvariantCulture, "DLC {0} (offer {1}, {2}): {3} files in {4}, {5} zips", Path.GetFileName(this.Folder), this.Offer, GameInfo.Tag(this.Game), count, root, zips));
            }
            return root;
        }

        // FM2: the game's gamedb (Tools > Settings > Folders) with the package's SQL run over it (Media\db\patch\*.sql,
        // Media\LicenseMasks\*.sql: unlocks and garage rows), statement by statement; a statement the old schema can't run
        // is counted and left out. The cars' own rows are the game database's: a car it lacks is named in the report.
        private void MergeFm2Database(string media, StringBuilder report)
        {
            string baseRoot = GameInfo.ConfiguredFolder(ForzaGame.Motorsport2);
            if (baseRoot == null)
            {
                throw new InvalidDataException("Set the Forza Motorsport 2 folder in Tools > Settings > Folders.");
            }
            string dbDir = Path.Combine(media, "db");
            Directory.CreateDirectory(dbDir);
            string db = Path.Combine(dbDir, "gamedb.slt");
            File.Copy(Path.Combine(Path.Combine(Path.Combine(baseRoot, "Media"), "DB"), "gamedb.slt"), db, true);
            // db\patch\<n>_merge.slt (the new rows, the game's schema) and <n>_execute.sql in the order of n, then the
            // unlocks of Media\LicenseMasks.
            List<string> scripts = new List<string>();
            string patch = Path.Combine(dbDir, "patch");
            if (Directory.Exists(patch))
            {
                List<string> files = new List<string>(Directory.GetFiles(patch, "*_merge.slt"));
                files.AddRange(Directory.GetFiles(patch, "*_execute.sql"));
                files.Sort(delegate(string a, string b)
                {
                    int x, y;
                    int.TryParse(Path.GetFileName(a).Substring(0, Path.GetFileName(a).IndexOf('_')), NumberStyles.None, CultureInfo.InvariantCulture, out x);
                    int.TryParse(Path.GetFileName(b).Substring(0, Path.GetFileName(b).IndexOf('_')), NumberStyles.None, CultureInfo.InvariantCulture, out y);
                    return x.CompareTo(y);
                });
                scripts.AddRange(files);
            }
            string masks = Path.Combine(media, "LicenseMasks");
            if (Directory.Exists(masks))
            {
                scripts.AddRange(Directory.GetFiles(masks, "*.sql"));
            }
            int done = 0;
            int failed = 0;
            int rows = 0;
            using (SqliteDatabase merged = SqliteDatabase.FromBytes(File.ReadAllBytes(db)))
            {
                foreach (string script in scripts)
                {
                    if (script.EndsWith(".slt", StringComparison.OrdinalIgnoreCase))
                    {
                        rows += MergeRows(merged, script);
                        continue;
                    }
                    foreach (string statement in File.ReadAllText(script).Split(';'))
                    {
                        if (statement.Trim().Length == 0)
                        {
                            continue;
                        }
                        try
                        {
                            merged.Execute(statement);
                            done++;
                        }
                        catch (Exception)
                        {
                            failed++;
                        }
                    }
                }
                File.WriteAllBytes(db, merged.ToBytes());
                List<string> missing = new List<string>();
                foreach (string car in this.Fm2CarFolders())
                {
                    if (merged.Scalar("SELECT Id FROM Data_Car WHERE MediaName = ? COLLATE NOCASE", car) == null)
                    {
                        missing.Add(car);
                    }
                }
                if (report != null)
                {
                    report.AppendLine(string.Format(CultureInfo.InvariantCulture, "DLC {0}: FM2 gamedb + {4} rows of the package's merge database + {1} SQL statements ({2} skipped){3}", Path.GetFileName(this.Folder), done, failed,
                        (missing.Count > 0) ? "; no row in the merged database: " + string.Join(" ", missing.ToArray()) : "", rows));
                }
            }
        }

        // The rows of a package's merge database put into the game's (same table, the columns both have), a row with the
        // same key replaced; the number of rows.
        private static int MergeRows(SqliteDatabase target, string mergePath)
        {
            int count = 0;
            using (SqliteDatabase patch = SqliteDatabase.FromBytes(File.ReadAllBytes(mergePath)))
            {
                foreach (object[] table in patch.Query("SELECT name FROM sqlite_master WHERE type = 'table'").Rows)
                {
                    string name = (string) table[0];
                    List<string> have = new List<string>();
                    foreach (object[] column in target.Query("PRAGMA table_info(" + SqliteDatabase.Quote(name) + ")").Rows)
                    {
                        have.Add((string) column[1]);
                    }
                    List<string> columns = new List<string>();
                    foreach (object[] column in patch.Query("PRAGMA table_info(" + SqliteDatabase.Quote(name) + ")").Rows)
                    {
                        if (have.Contains((string) column[1]))
                        {
                            columns.Add(SqliteDatabase.Quote((string) column[1]));
                        }
                    }
                    if (columns.Count == 0)
                    {
                        continue;
                    }
                    string list = string.Join(", ", columns.ToArray());
                    string marks = string.Join(", ", new List<string>(columns).ConvertAll<string>(delegate(string c) { return "?"; }).ToArray());
                    foreach (object[] row in patch.Query("SELECT " + list + " FROM " + SqliteDatabase.Quote(name)).Rows)
                    {
                        target.Execute("INSERT OR REPLACE INTO " + SqliteDatabase.Quote(name) + " (" + list + ") VALUES (" + marks + ")", row);
                        count++;
                    }
                }
            }
            return count;
        }

        // A folder replaced by <folder>.zip of its files.
        private static int ToZip(string folder)
        {
            DlcPackage.WriteStoredZip(folder, folder + ".zip");
            Directory.Delete(folder, true);
            return 1;
        }
    }
}
