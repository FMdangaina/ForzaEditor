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
    // What the pack lacks (shared engine sounds, engine swaps only in the base game) the port takes from
    // FM4 or asks in the review.
    public class DlcSource
    {
        public const string Marker = "DlcSource.txt";

        public string Folder;           // the package folder (it holds Media\<offer>.puboffer)
        public int Offer;
        public uint TitleId;            // the source game's, from the merge database's ContentOffers.ID
        public ForzaGame Game;
        public string Problem;          // why the package can't be used, or null

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
            string root = Path.GetFullPath(folder).TrimEnd('\\');
            if (string.Equals(Path.GetFileName(root), "Media", StringComparison.OrdinalIgnoreCase))
            {
                root = Path.GetDirectoryName(root);
            }
            string media = Path.Combine(root, "Media");
            string[] offers = Directory.Exists(media) ? Directory.GetFiles(media, "*.puboffer") : new string[0];
            int offer;
            if (offers.Length != 1 || !int.TryParse(Path.GetFileNameWithoutExtension(offers[0]), NumberStyles.None, CultureInfo.InvariantCulture, out offer))
            {
                return null;
            }
            DlcSource source = new DlcSource();
            source.Folder = root;
            source.Offer = offer;
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
            if (merge == null)
            {
                throw new InvalidDataException("The package has no cars (no db\\patch\\*_merge.slt): rims or other content only.");
            }
            using (SqliteDatabase db = SqliteDatabase.FromBytes(merge))
            {
                string id = db.Scalar("SELECT ID FROM ContentOffers LIMIT 1") as string;
                uint title;
                if (id == null || id.Length < 8 || !uint.TryParse(id.Substring(0, 8), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out title))
                {
                    throw new InvalidDataException("The merge database has no ContentOffers id: the source game is unknown.");
                }
                this.TitleId = title;
                this.Game = GameInfo.FromTitleId(title);
            }
        }

        // The cars of the pack (Data_Car.MediaName of the merge database).
        public List<string> Cars()
        {
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
            string root = Path.Combine(Path.Combine(Path.Combine(Path.GetTempPath(), "ForzaStudio"), "DlcSource"), Path.GetFileName(this.Folder));
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
            string[] merges = Directory.Exists(patch) ? Directory.GetFiles(patch, "*_merge.slt") : new string[0];
            if (merges.Length != 1)
            {
                throw new InvalidDataException(string.Format(CultureInfo.InvariantCulture, "The package has {0} merge databases; one is needed.", merges.Length));
            }
            File.Copy(merges[0], Path.Combine(Path.Combine(media, "db"), "gamedb.slt"));

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
            string tables = Path.Combine(media, "StringTables");
            if (Directory.Exists(tables))
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
            });
            if (report != null)
            {
                report.AppendLine(string.Format(CultureInfo.InvariantCulture, "DLC {0} (offer {1}, {2}): {3} files in {4}, {5} zips", Path.GetFileName(this.Folder), this.Offer, GameInfo.Tag(this.Game), count, root, zips));
            }
            return root;
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
