namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.RegularExpressions;

    // Building the audio index: each source (the game folder, each DLC package) is scanned once into a Scan; a Scan is kept
    // in memory and on disk (%TEMP%\ForzaStudio\SoundIndex\<hash>.tsv) under the source's own stamp, so installing one
    // package scans only that package, and the first start after a restart reads text instead of the packages.
    public sealed partial class SoundIndex
    {
        private const string Version = "v3";
        private static readonly object gate = new object();
        private static readonly Dictionary<string, Scan> scans = new Dictionary<string, Scan>();
        private static string lastKey;
        private static SoundIndex last;

        private static readonly Regex Attributes = new Regex("(\\w+)=\"([^\"]*)\"");
        private static readonly Regex GroupOrLoop = new Regex("<(\\w+)\\s*>|<Loop\\s+([^>]*?)/?>", RegexOptions.IgnoreCase);
        private static readonly Regex BankName = new Regex("Soundbank\\s+name=\"([^\"]+)\"", RegexOptions.IgnoreCase);
        private static readonly string[] Tunings = { "enginetuning.zip", "harmonictuning.zip", "carmodeltuning.zip" };
        private static readonly string[] Wanted = { ".fsb", ".fev", ".xma", ".xsb", ".xwb", ".wav", ".xml", ".zip" };

        // What one source holds; filled by ScanVolume, written to and read from the cache.
        private sealed class Scan
        {
            public string Name;
            public string Problem;
            public bool Skip;                           // a package of another game
            public readonly List<SoundFile> Files = new List<SoundFile>();
            public readonly List<HarmonicTuning> Harmonics = new List<HarmonicTuning>();
            public readonly List<EngineTuning> Engines = new List<EngineTuning>();
            public readonly List<string> Models = new List<string>();
        }

        // Files of a source, read the same way from a folder or from inside a package.
        private abstract class Volume
        {
            public abstract List<string> Files();
            public abstract long Size(string relative);
            public abstract Stream Open(string relative);
            public abstract byte[] Read(string relative);
            public abstract string ZipPath(string relative);
        }

        private sealed class DiskVolume : Volume
        {
            private readonly string root;

            public DiskVolume(string root)
            {
                this.root = root;
            }

            public override List<string> Files()
            {
                List<string> files = new List<string>();
                string audio = System.IO.Path.Combine(System.IO.Path.Combine(this.root, "Media"), "audio");
                if (Directory.Exists(audio))
                {
                    foreach (string file in Directory.GetFiles(audio, "*", SearchOption.AllDirectories))
                    {
                        files.Add(file.Substring(this.root.Length).TrimStart('\\', '/'));
                    }
                }
                return files;
            }

            public override long Size(string relative)
            {
                return new FileInfo(System.IO.Path.Combine(this.root, relative)).Length;
            }

            public override Stream Open(string relative)
            {
                return new FileStream(System.IO.Path.Combine(this.root, relative), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            }

            public override byte[] Read(string relative)
            {
                return File.ReadAllBytes(System.IO.Path.Combine(this.root, relative));
            }

            public override string ZipPath(string relative)
            {
                return System.IO.Path.Combine(this.root, relative);
            }
        }

        private sealed class PackageVolume : Volume
        {
            private readonly StfsReader package;

            public PackageVolume(StfsReader package)
            {
                this.package = package;
            }

            public override List<string> Files()
            {
                return this.package.Files();
            }

            public override long Size(string relative)
            {
                return this.package.FileSize(relative);
            }

            public override Stream Open(string relative)
            {
                return this.package.OpenFile(relative);
            }

            public override byte[] Read(string relative)
            {
                return this.package.ReadFile(relative);
            }

            public override string ZipPath(string relative)
            {
                string path = "stfs:" + this.package.Path + "|" + relative;
                StfsReader reader = this.package;
                string inner = relative;
                ForzaZip.Mount(path, delegate { return reader.OpenFile(inner); });
                return path;
            }
        }

        // The index of a game: gameRoot = the game folder (null: only the installed packages are read). The same index
        // object comes back while nothing on disk changed.
        public static SoundIndex Get(ForzaGame game, string gameRoot, Action<string> progress)
        {
            return Get(game, gameRoot, null, progress);
        }

        // extraRoot: the work folder of an open DLC pack (its Media folder inside), read as one more source, "Open pack".
        public static SoundIndex Get(ForzaGame game, string gameRoot, string extraRoot, Action<string> progress)
        {
            List<KeyValuePair<string, string>> work = new List<KeyValuePair<string, string>>();    // key, path (a folder or a package file)
            if (!string.IsNullOrEmpty(gameRoot) && Directory.Exists(Path.Combine(Path.Combine(gameRoot, "Media"), "audio")))
            {
                string stamp = FolderStamp(gameRoot);
                work.Add(new KeyValuePair<string, string>("folder|" + gameRoot + "|" + stamp, gameRoot));
            }
            if (!string.IsNullOrEmpty(extraRoot) && Directory.Exists(Path.Combine(Path.Combine(extraRoot, "Media"), "audio")))
            {
                work.Add(new KeyValuePair<string, string>("open|" + extraRoot + "|" + FolderStamp(extraRoot), extraRoot));
            }
            string content = InstalledDlc.ContentFolder(game);
            if (content != null)
            {
                string[] files = Directory.GetFiles(content);
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                foreach (string file in files)
                {
                    FileInfo info = new FileInfo(file);
                    work.Add(new KeyValuePair<string, string>("package|" + game + "|" + info.FullName + "|" + info.Length + "|" + info.LastWriteTimeUtc.Ticks, file));
                }
            }
            StringBuilder all = new StringBuilder(game.ToString());
            foreach (KeyValuePair<string, string> item in work)
            {
                all.Append('\n').Append(item.Key);
            }
            lock (gate)
            {
                if (last != null && lastKey == all.ToString())
                {
                    return last;
                }
                SoundIndex index = new SoundIndex();
                index.Game = game;
                foreach (KeyValuePair<string, string> item in work)
                {
                    Scan scan;
                    if (!scans.TryGetValue(item.Key, out scan))
                    {
                        scan = ReadCache(item.Key);
                        if (scan == null)
                        {
                            scan = Directory.Exists(item.Value) ? ScanFolder(item.Value, item.Key.StartsWith("open|") ? "Open pack" : "Game folder", progress) : ScanPackage(game, item.Value, progress);
                            WriteCache(item.Key, scan);
                        }
                        scans[item.Key] = scan;
                    }
                    if (!scan.Skip)
                    {
                        index.Add(scan, item.Value);
                    }
                }
                index.Finish();
                lastKey = all.ToString();
                last = index;
                return index;
            }
        }

        // Forgets what is kept in memory (the disk cache stays); the next Get re-checks every source.
        public static void Reset()
        {
            lock (gate)
            {
                scans.Clear();
                last = null;
                lastKey = null;
            }
        }

        private void Add(Scan scan, string path)
        {
            SoundSource source = new SoundSource();
            source.Name = scan.Name;
            source.Path = path;
            source.Packaged = !Directory.Exists(path);
            source.Problem = scan.Problem;
            this.Sources.Add(source);
            foreach (SoundFile file in scan.Files)
            {
                SoundFile copy = new SoundFile();
                copy.Name = file.Name;
                copy.Path = file.Path;
                copy.Size = file.Size;
                copy.Category = file.Category;
                copy.Info = file.Info;
                copy.Problem = file.Problem;
                copy.Source = source;
                this.Files.Add(copy);
                Push(this.byName, copy.Name, copy);
            }
            foreach (HarmonicTuning ht in scan.Harmonics)
            {
                HarmonicTuning copy = new HarmonicTuning();
                copy.Name = ht.Name;
                copy.Bank = ht.Bank;
                copy.Loops = ht.Loops;
                copy.Source = source;
                Push(this.harmonics, copy.Name, copy);
            }
            foreach (EngineTuning engine in scan.Engines)
            {
                EngineTuning copy = new EngineTuning();
                copy.Media = engine.Media;
                copy.Idle = engine.Idle;
                copy.Redline = engine.Redline;
                copy.Cylinders = engine.Cylinders;
                copy.BlowOff = engine.BlowOff;
                copy.Slots = engine.Slots;
                copy.Source = source;
                Push(this.engines, copy.Media, copy);
            }
            foreach (string model in scan.Models)
            {
                Push(this.models, model, source);
            }
        }

        private void Finish()
        {
            this.Files.Sort(delegate(SoundFile a, SoundFile b) { return string.Compare(a.Path, b.Path, StringComparison.OrdinalIgnoreCase); });
        }

        private static void Push<T>(Dictionary<string, List<T>> map, string key, T value)
        {
            List<T> list;
            if (!map.TryGetValue(key, out list))
            {
                list = new List<T>();
                map[key] = list;
            }
            list.Add(value);
        }

        // Size and time of every audio file of the game folder, folded into one string.
        private static string FolderStamp(string root)
        {
            StringBuilder text = new StringBuilder();
            string audio = Path.Combine(Path.Combine(root, "Media"), "audio");
            string[] files = Directory.GetFiles(audio, "*", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            foreach (string file in files)
            {
                FileInfo info = new FileInfo(file);
                text.Append(info.FullName.Substring(audio.Length)).Append(':').Append(info.Length).Append(':').Append(info.LastWriteTimeUtc.Ticks).Append('|');
            }
            return files.Length + "-" + Hash(text.ToString());
        }

        private static string Hash(string text)
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(text));
                StringBuilder hex = new StringBuilder();
                foreach (byte b in hash)
                {
                    hex.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                }
                return hex.ToString();
            }
        }

        // ---- scanning ----

        private static Scan ScanFolder(string root, string name, Action<string> progress)
        {
            Scan scan = new Scan();
            scan.Name = name;
            if (progress != null)
            {
                progress(name);
            }
            ScanVolume(new DiskVolume(root), scan);
            return scan;
        }

        private static Scan ScanPackage(ForzaGame game, string file, Action<string> progress)
        {
            Scan scan = new Scan();
            scan.Name = Path.GetFileName(file);
            try
            {
                if (!StfsReader.IsPackage(file))
                {
                    scan.Skip = true;
                    return scan;
                }
                StfsReader package = StfsReader.Open(file);
                if (package.TitleId != InstalledDlc.TitleId(game))
                {
                    scan.Skip = true;
                    return scan;
                }
                scan.Name = string.IsNullOrEmpty(package.DisplayName) ? Path.GetFileName(file) : package.DisplayName;
                if (progress != null)
                {
                    progress(scan.Name);
                }
                ScanVolume(new PackageVolume(package), scan);
            }
            catch (Exception e)
            {
                scan.Problem = e.Message;
            }
            return scan;
        }

        private static void ScanVolume(Volume volume, Scan scan)
        {
            foreach (string relative in volume.Files())
            {
                string full = "\\" + relative.Replace('/', '\\');
                int at = full.IndexOf("\\audio\\", StringComparison.OrdinalIgnoreCase);
                if (at < 0)
                {
                    continue;
                }
                string[] parts = full.Substring(at + 7).Split('\\');
                string name = parts[parts.Length - 1];
                string extension = Path.GetExtension(name).ToLowerInvariant();
                if (Array.IndexOf(Wanted, extension) < 0)
                {
                    continue;
                }
                SoundFile file = new SoundFile();
                file.Name = name;
                file.Path = full.TrimStart('\\');
                file.Category = (parts.Length > 2 && parts[0].Equals("cars", StringComparison.OrdinalIgnoreCase)) ? parts[1] : parts[0];
                try
                {
                    file.Size = volume.Size(relative);
                    string lower = name.ToLowerInvariant();
                    if (extension == ".fsb")
                    {
                        using (Stream stream = volume.Open(relative))
                        {
                            file.Info = FsbInfo.Read(stream);
                        }
                    }
                    else if (lower.EndsWith("_et.xml"))
                    {
                        scan.Engines.Add(ParseEngine(name.Substring(0, name.Length - 7), Text(volume.Read(relative))));
                    }
                    else if (lower.EndsWith("_ht.xml"))
                    {
                        scan.Harmonics.Add(ParseHarmonic(name, Text(volume.Read(relative))));
                    }
                    else if (lower.EndsWith("_cmt.xml"))
                    {
                        scan.Models.Add(name.Substring(0, name.Length - 8));
                    }
                    else if (Array.IndexOf(Tunings, lower) >= 0)
                    {
                        ScanTunings(volume.ZipPath(relative), scan);
                    }
                }
                catch (Exception e)
                {
                    file.Problem = e.Message;
                }
                scan.Files.Add(file);
            }
        }

        private static void ScanTunings(string zipPath, Scan scan)
        {
            ForzaZip zip = new ForzaZip(zipPath);
            foreach (ForzaZip.Entry entry in zip.Entries)
            {
                if (entry.IsDirectory)
                {
                    continue;
                }
                string name = entry.Name.Replace('\\', '/');
                name = name.Substring(name.LastIndexOf('/') + 1);
                string lower = name.ToLowerInvariant();
                if (lower.EndsWith("_et.xml"))
                {
                    scan.Engines.Add(ParseEngine(name.Substring(0, name.Length - 7), Text(zip.Read(entry))));
                }
                else if (lower.EndsWith("_ht.xml"))
                {
                    scan.Harmonics.Add(ParseHarmonic(name, Text(zip.Read(entry))));
                }
                else if (lower.EndsWith("_cmt.xml"))
                {
                    scan.Models.Add(name.Substring(0, name.Length - 8));
                }
            }
        }

        private static string Text(byte[] data)
        {
            return Encoding.GetEncoding(28591).GetString(data);
        }

        private static string Attribute(string attributes, string key)
        {
            foreach (Match m in Attributes.Matches(attributes))
            {
                if (string.Equals(m.Groups[1].Value, key, StringComparison.OrdinalIgnoreCase))
                {
                    return m.Groups[2].Value;
                }
            }
            return null;
        }

        private static int Number(string attributes, string key)
        {
            string value = Attribute(attributes, key);
            double number;
            return (value != null && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number)) ? (int) Math.Round(number) : 0;
        }

        private static EngineTuning ParseEngine(string media, string text)
        {
            EngineTuning engine = new EngineTuning();
            engine.Media = media;
            Match settings = Regex.Match(text, "<EngineSettings([^>]*?)/?>", RegexOptions.Singleline);
            if (settings.Success)
            {
                string a = settings.Groups[1].Value;
                engine.Idle = Number(a, "audio_rpm_idle");
                engine.Redline = Number(a, "audio_rpm_redline");
                engine.Cylinders = Number(a, "num_cylinders");
                List<string> blow = new List<string>();
                for (int level = 1; level <= 3; level++)
                {
                    string lv = Attribute(a, "blowoffL" + level + "Level");
                    string choice = Attribute(a, "blowoffL" + level + "Choice");
                    if (lv != null && choice != null)
                    {
                        blow.Add("L" + level + " " + lv + "/" + choice);
                    }
                }
                engine.BlowOff = string.Join(" ", blow.ToArray());
            }
            foreach (string part in Parts)
            {
                Match upgrade = Regex.Match(text, "<" + part + ">\\s*<Upgrade([^>]*)/?>", RegexOptions.Singleline);
                if (!upgrade.Success)
                {
                    continue;
                }
                for (int level = 0; level <= 3; level++)
                {
                    string ht = Attribute(upgrade.Groups[1].Value, "L" + level);
                    if (!string.IsNullOrEmpty(ht))
                    {
                        EngineSlot slot = new EngineSlot();
                        slot.Part = part;
                        slot.Level = level;
                        slot.Ht = ht;
                        engine.Slots.Add(slot);
                    }
                }
            }
            return engine;
        }

        private static HarmonicTuning ParseHarmonic(string fileName, string text)
        {
            HarmonicTuning ht = new HarmonicTuning();
            ht.Name = fileName;
            Match bank = BankName.Match(text);
            ht.Bank = bank.Success ? bank.Groups[1].Value : "";
            string group = "";
            foreach (Match m in GroupOrLoop.Matches(text))
            {
                if (m.Groups[1].Success)
                {
                    group = m.Groups[1].Value;
                    continue;
                }
                string a = m.Groups[2].Value;
                HarmonicLoop loop = new HarmonicLoop();
                loop.Group = group;
                loop.Wave = Attribute(a, "wavebankindex") == null ? -1 : Number(a, "wavebankindex");
                loop.RpmMin = Number(a, "rpm_min");
                loop.RpmSample = Number(a, "rpm_sample");
                loop.RpmMax = Number(a, "rpm_max");
                ht.Loops.Add(loop);
            }
            return ht;
        }

        // ---- the disk cache ----

        private static string CachePath(string key)
        {
            return Path.Combine(Path.Combine(Path.Combine(Path.GetTempPath(), "ForzaStudio"), "SoundIndex"), Hash(key) + ".tsv");
        }

        private static string Clean(string text)
        {
            return (text ?? "").Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
        }

        private static string Join<T>(IEnumerable<T> items, Converter<T, string> format, string separator)
        {
            List<string> parts = new List<string>();
            foreach (T item in items)
            {
                parts.Add(format(item));
            }
            return string.Join(separator, parts.ToArray());
        }

        private static void WriteCache(string key, Scan scan)
        {
            try
            {
                string path = CachePath(key);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                StringBuilder text = new StringBuilder();
                text.Append(Version).Append('\t').Append(Clean(key)).Append('\n');
                text.Append("N\t").Append(Clean(scan.Name)).Append('\n');
                if (scan.Skip)
                {
                    text.Append("X\n");
                }
                if (scan.Problem != null)
                {
                    text.Append("P\t").Append(Clean(scan.Problem)).Append('\n');
                }
                foreach (SoundFile f in scan.Files)
                {
                    text.Append("F\t").Append(Clean(f.Name)).Append('\t').Append(Clean(f.Path)).Append('\t').Append(f.Size).Append('\t').Append(Clean(f.Category)).Append('\t').Append(Clean(f.Problem)).Append('\n');
                    if (f.Info != null)
                    {
                        FsbInfo i = f.Info;
                        text.Append("I\t").Append(i.Samples).Append('\t').Append(i.Format).Append('\t').Append(i.Loops).Append('\t').Append(i.DataSize).Append('\t')
                            .Append(Join<string>(i.Names, delegate(string s) { return Clean(s).Replace('\u0001', ' '); }, "\u0001")).Append('\t')
                            .Append(Join<int>(i.Frequency, delegate(int v) { return v.ToString(CultureInfo.InvariantCulture); }, ",")).Append('\t')
                            .Append(Join<int>(i.Channels, delegate(int v) { return v.ToString(CultureInfo.InvariantCulture); }, ",")).Append('\t')
                            .Append(Join<int>(i.Length, delegate(int v) { return v.ToString(CultureInfo.InvariantCulture); }, ",")).Append('\n');
                    }
                }
                foreach (EngineTuning e in scan.Engines)
                {
                    text.Append("E\t").Append(Clean(e.Media)).Append('\t').Append(e.Idle).Append('\t').Append(e.Redline).Append('\t').Append(e.Cylinders).Append('\t').Append(Clean(e.BlowOff)).Append('\t')
                        .Append(Join<EngineSlot>(e.Slots, delegate(EngineSlot s) { return s.Part + ":" + s.Level + ":" + Clean(s.Ht); }, ";")).Append('\n');
                }
                foreach (HarmonicTuning h in scan.Harmonics)
                {
                    text.Append("H\t").Append(Clean(h.Name)).Append('\t').Append(Clean(h.Bank)).Append('\t')
                        .Append(Join<HarmonicLoop>(h.Loops, delegate(HarmonicLoop l) { return l.Group + ":" + l.Wave + ":" + l.RpmMin + ":" + l.RpmSample + ":" + l.RpmMax; }, ";")).Append('\n');
                }
                foreach (string model in scan.Models)
                {
                    text.Append("M\t").Append(Clean(model)).Append('\n');
                }
                File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
            }
            catch (Exception)
            {
                // no cache: the next start scans again
            }
        }

        private static int[] Ints(string text)
        {
            if (text.Length == 0)
            {
                return new int[0];
            }
            string[] parts = text.Split(',');
            int[] values = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                values[i] = int.Parse(parts[i], CultureInfo.InvariantCulture);
            }
            return values;
        }

        private static Scan ReadCache(string key)
        {
            try
            {
                string path = CachePath(key);
                if (!File.Exists(path))
                {
                    return null;
                }
                string[] lines = File.ReadAllText(path, Encoding.UTF8).Split('\n');
                if (lines.Length == 0 || lines[0] != Version + "\t" + Clean(key))
                {
                    return null;
                }
                Scan scan = new Scan();
                SoundFile current = null;
                for (int n = 1; n < lines.Length; n++)
                {
                    if (lines[n].Length == 0)
                    {
                        continue;
                    }
                    string[] f = lines[n].Split('\t');
                    switch (f[0])
                    {
                        case "N":
                            scan.Name = f[1];
                            break;
                        case "X":
                            scan.Skip = true;
                            break;
                        case "P":
                            scan.Problem = f[1];
                            break;
                        case "F":
                            current = new SoundFile();
                            current.Name = f[1];
                            current.Path = f[2];
                            current.Size = long.Parse(f[3], CultureInfo.InvariantCulture);
                            current.Category = f[4];
                            current.Problem = (f[5].Length > 0) ? f[5] : null;
                            scan.Files.Add(current);
                            break;
                        case "I":
                            FsbInfo info = new FsbInfo();
                            info.Samples = int.Parse(f[1], CultureInfo.InvariantCulture);
                            info.Format = f[2];
                            info.Loops = int.Parse(f[3], CultureInfo.InvariantCulture);
                            info.DataSize = long.Parse(f[4], CultureInfo.InvariantCulture);
                            info.Names = (info.Samples == 0) ? new string[0] : f[5].Split('\u0001');
                            info.Frequency = Ints(f[6]);
                            info.Channels = Ints(f[7]);
                            info.Length = Ints(f[8]);
                            info.Partial = f.Length > 9 && f[9] == "1";
                            if (info.Names.Length != info.Samples || info.Frequency.Length != info.Samples || info.Channels.Length != info.Samples || info.Length.Length != info.Samples)
                            {
                                return null;
                            }
                            current.Info = info;
                            break;
                        case "E":
                            EngineTuning engine = new EngineTuning();
                            engine.Media = f[1];
                            engine.Idle = int.Parse(f[2], CultureInfo.InvariantCulture);
                            engine.Redline = int.Parse(f[3], CultureInfo.InvariantCulture);
                            engine.Cylinders = int.Parse(f[4], CultureInfo.InvariantCulture);
                            engine.BlowOff = f[5];
                            foreach (string item in f[6].Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                            {
                                string[] s = item.Split(':');
                                EngineSlot slot = new EngineSlot();
                                slot.Part = s[0];
                                slot.Level = int.Parse(s[1], CultureInfo.InvariantCulture);
                                slot.Ht = s[2];
                                engine.Slots.Add(slot);
                            }
                            scan.Engines.Add(engine);
                            break;
                        case "H":
                            HarmonicTuning ht = new HarmonicTuning();
                            ht.Name = f[1];
                            ht.Bank = f[2];
                            foreach (string item in f[3].Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                            {
                                string[] s = item.Split(':');
                                HarmonicLoop loop = new HarmonicLoop();
                                loop.Group = s[0];
                                loop.Wave = int.Parse(s[1], CultureInfo.InvariantCulture);
                                loop.RpmMin = int.Parse(s[2], CultureInfo.InvariantCulture);
                                loop.RpmSample = int.Parse(s[3], CultureInfo.InvariantCulture);
                                loop.RpmMax = int.Parse(s[4], CultureInfo.InvariantCulture);
                                ht.Loops.Add(loop);
                            }
                            scan.Harmonics.Add(ht);
                            break;
                        case "M":
                            scan.Models.Add(f[1]);
                            break;
                    }
                }
                return scan;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ---- reading a file out of a source ----

        // The file as a path on disk: a file of the game folder as it is, one inside a package copied to the temp folder.
        // null when the source cannot be read.
        public string Extract(SoundFile file)
        {
            try
            {
                if (!file.Source.Packaged)
                {
                    return Path.Combine(file.Source.Path, file.Path);
                }
                string folder = Path.Combine(Path.Combine(Path.Combine(Path.GetTempPath(), "ForzaStudio"), "SoundBanks"), Hash(file.Source.Path + "|" + file.Size).Substring(0, 8));
                Directory.CreateDirectory(folder);
                string target = Path.Combine(folder, file.Name);
                if (File.Exists(target) && new FileInfo(target).Length == file.Size)
                {
                    return target;
                }
                StfsReader package = StfsReader.Open(file.Source.Path);
                using (Stream source = package.OpenFile(file.Path))
                {
                    if (source == null)
                    {
                        return null;
                    }
                    using (FileStream output = File.Create(target))
                    {
                        byte[] buffer = new byte[64 * 1024];
                        int read;
                        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            output.Write(buffer, 0, read);
                        }
                    }
                }
                return target;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
