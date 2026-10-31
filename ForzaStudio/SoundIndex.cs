namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;

    // A place the game reads audio from: its own folder, or one installed DLC package.
    public sealed class SoundSource
    {
        public string Name;             // "Game folder" or the package's display name
        public string Path;             // the package file; the game root for the folder
        public bool Packaged;
        public string Problem;          // why it could not be read, or null
    }

    // One audio file (bank, event file, music, tuning zip...) of a source.
    public sealed class SoundFile
    {
        public string Name;
        public string Path;             // inside the source, '\' separated
        public long Size;
        public string Category;         // Engines, Turbos, SuperChargers, Music... (the folder under audio\ or audio\cars\)
        public SoundSource Source;
        public FsbInfo Info;            // .fsb: what its header says
        public string Problem;          // .fsb: why the header could not be read
    }

    public sealed class HarmonicLoop
    {
        public string Group;            // ExhaustL, ExhaustR, ...
        public int Wave;                // wavebankindex: the sample of the bank
        public int RpmMin;
        public int RpmSample;
        public int RpmMax;
    }

    // <name>_HT.xml: the bank and the loops it plays with the rpm each was recorded at.
    public sealed class HarmonicTuning
    {
        public string Name;
        public string Bank;
        public SoundSource Source;
        public List<HarmonicLoop> Loops = new List<HarmonicLoop>();
    }

    public sealed class EngineSlot
    {
        public string Part;             // Exhaust, EngineIntake, EngineAmbient
        public int Level;               // upgrade level 0-3
        public string Ht;
    }

    // <MediaName>_ET.xml: which harmonic tuning each part plays at each upgrade level, and the audio settings of the engine.
    public sealed class EngineTuning
    {
        public string Media;
        public SoundSource Source;
        public int Idle;
        public int Redline;
        public int Cylinders;
        public string BlowOff = "";     // "L1 1/1 L2 2/1 L3 3/1": level/choice of the turbo blow-off
        public List<EngineSlot> Slots = new List<EngineSlot>();
    }

    public sealed class BankUse
    {
        public string Media;
        public string Part;
        public int Level;
        public string Ht;
    }

    public sealed class PartStatus
    {
        public string Part;
        public int Level;
        public string Ht;
        public string HtSource;
        public string Bank;
        public string BankSource;       // the package or folder the bank is read from
        public int State;               // 0 = in the game folder, 1 = only in an installed package, 2 = nowhere
        public int Samples = -1;        // samples of the bank (-1: unknown)
        public List<string> Problems = new List<string>();
    }

    public sealed class EngineCoverage
    {
        public string Media;
        public string Source;           // where the engine tuning comes from
        public List<PartStatus> Parts = new List<PartStatus>();

        // 0 = everything in the game folder, 1 = some sound only in packages, 2 = something missing or broken.
        public int Worst
        {
            get
            {
                int worst = 0;
                foreach (PartStatus part in this.Parts)
                {
                    worst = Math.Max(worst, (part.Problems.Count > 0) ? 2 : part.State);
                }
                return worst;
            }
        }

        // True when no level of any part can play.
        public bool Silent
        {
            get
            {
                foreach (PartStatus part in this.Parts)
                {
                    if (part.State != 2 && part.Problems.Count == 0)
                    {
                        return false;
                    }
                }
                return true;
            }
        }
    }

    public sealed class SoundIssue
    {
        public string Severity;         // error, warning, info
        public string Subject;
        public string Text;

        public override string ToString()
        {
            return this.Severity + ": " + this.Subject + ": " + this.Text;
        }
    }

    // Everything the audio of a game can be reached from, indexed once: the files of the game folder and of every installed
    // DLC package (with the FSB4 header of each bank), the engine tunings (car -> harmonic tuning per part and level), the
    // harmonic tunings (-> bank and loops) and the car model tunings. From it: which cars use a bank, what a car plays and
    // where each bank is read from, what breaks when a package goes, what is broken or never used. Built by SoundIndex.Get.
    public sealed partial class SoundIndex
    {
        public static readonly string[] Parts = { "Exhaust", "EngineIntake", "EngineAmbient" };

        public ForzaGame Game;
        public readonly List<SoundSource> Sources = new List<SoundSource>();
        public readonly List<SoundFile> Files = new List<SoundFile>();

        private readonly Dictionary<string, List<SoundFile>> byName = new Dictionary<string, List<SoundFile>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<HarmonicTuning>> harmonics = new Dictionary<string, List<HarmonicTuning>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<EngineTuning>> engines = new Dictionary<string, List<EngineTuning>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<SoundSource>> models = new Dictionary<string, List<SoundSource>>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, List<BankUse>> uses;

        public bool HasEngines { get { return this.engines.Count > 0; } }

        // ---- files (1) ----

        // Every copy of a file in every source (game folder first, then the packages by name).
        public List<SoundFile> Copies(string name)
        {
            List<SoundFile> list;
            return this.byName.TryGetValue(name, out list) ? list : new List<SoundFile>();
        }

        public List<SoundFile> InCategory(string category)
        {
            return this.Files.FindAll(delegate(SoundFile f) { return string.Equals(f.Category, category, StringComparison.OrdinalIgnoreCase); });
        }

        public List<string> Categories()
        {
            List<string> found = new List<string>();
            foreach (SoundFile file in this.Files)
            {
                if (!found.Exists(delegate(string c) { return string.Equals(c, file.Category, StringComparison.OrdinalIgnoreCase); }))
                {
                    found.Add(file.Category);
                }
            }
            found.Sort(StringComparer.OrdinalIgnoreCase);
            return found;
        }

        public IEnumerable<string> EngineMedia()
        {
            List<string> names = new List<string>(this.engines.Keys);
            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
        }

        public EngineTuning Engine(string media)
        {
            return this.FindEngine(media, null);
        }

        public HarmonicTuning Harmonic(string name)
        {
            return this.FindHarmonic(name, null);
        }

        public bool HasModel(string media)
        {
            return this.models.ContainsKey(media);
        }

        private EngineTuning FindEngine(string media, string exclude)
        {
            List<EngineTuning> list;
            if (this.engines.TryGetValue(media, out list))
            {
                foreach (EngineTuning engine in list)
                {
                    if (!Is(engine.Source, exclude))
                    {
                        return engine;
                    }
                }
            }
            return null;
        }

        private HarmonicTuning FindHarmonic(string name, string exclude)
        {
            List<HarmonicTuning> list;
            if (this.harmonics.TryGetValue(name, out list))
            {
                foreach (HarmonicTuning ht in list)
                {
                    if (!Is(ht.Source, exclude))
                    {
                        return ht;
                    }
                }
            }
            return null;
        }

        private static bool Is(SoundSource source, string name)
        {
            return name != null && string.Equals(source.Name, name, StringComparison.OrdinalIgnoreCase);
        }

        // 0 = the bank is in the game folder, 1 = only in an installed package (where = it), 2 = nowhere.
        // exclude: a source to leave out (what happens when that package is removed).
        private int BankState(string bank, string exclude, out SoundSource where)
        {
            where = null;
            if (string.Equals(bank, "NA.fsb", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }
            SoundSource packaged = null;
            foreach (SoundFile copy in this.Copies(bank))
            {
                if (Is(copy.Source, exclude))
                {
                    continue;
                }
                if (!copy.Source.Packaged)
                {
                    where = copy.Source;
                    return 0;
                }
                packaged = packaged ?? copy.Source;
            }
            where = packaged;
            return (packaged != null) ? 1 : 2;
        }

        private SoundFile FindBank(string bank, string exclude)
        {
            foreach (SoundFile copy in this.Copies(bank))
            {
                if (!Is(copy.Source, exclude) && copy.Name.EndsWith(".fsb", StringComparison.OrdinalIgnoreCase))
                {
                    return copy;
                }
            }
            return null;
        }

        // ---- graph: car -> engine tuning -> harmonic tuning -> bank (3) ----

        // The cars (engine tunings) that play the bank, with the part and level.
        public List<BankUse> BankUsers(string bank)
        {
            if (this.uses == null)
            {
                this.uses = new Dictionary<string, List<BankUse>>(StringComparer.OrdinalIgnoreCase);
                foreach (string media in this.EngineMedia())
                {
                    EngineTuning engine = this.FindEngine(media, null);
                    foreach (EngineSlot slot in engine.Slots)
                    {
                        HarmonicTuning ht = this.FindHarmonic(slot.Ht, null);
                        if (ht == null || string.IsNullOrEmpty(ht.Bank))
                        {
                            continue;
                        }
                        List<BankUse> list;
                        if (!this.uses.TryGetValue(ht.Bank, out list))
                        {
                            list = new List<BankUse>();
                            this.uses[ht.Bank] = list;
                        }
                        BankUse use = new BankUse();
                        use.Media = media;
                        use.Part = slot.Part;
                        use.Level = slot.Level;
                        use.Ht = slot.Ht;
                        list.Add(use);
                    }
                }
            }
            List<BankUse> found;
            return this.uses.TryGetValue(bank, out found) ? found : new List<BankUse>();
        }

        // The distinct cars that play the bank.
        public List<string> BankCars(string bank)
        {
            List<string> cars = new List<string>();
            foreach (BankUse use in this.BankUsers(bank))
            {
                if (!cars.Contains(use.Media))
                {
                    cars.Add(use.Media);
                }
            }
            return cars;
        }

        // The cars whose engine tuning names the harmonic tuning.
        public List<string> HarmonicCars(string ht)
        {
            List<string> cars = new List<string>();
            foreach (string media in this.EngineMedia())
            {
                foreach (EngineSlot slot in this.FindEngine(media, null).Slots)
                {
                    if (string.Equals(slot.Ht, ht, StringComparison.OrdinalIgnoreCase) && !cars.Contains(media))
                    {
                        cars.Add(media);
                    }
                }
            }
            return cars;
        }

        // ---- coverage per car (4) ----

        public EngineCoverage Coverage(string media)
        {
            return this.Coverage(media, null);
        }

        // What a car plays and where each piece comes from; exclude = a source taken away (null: none). null when no engine
        // tuning remains.
        public EngineCoverage Coverage(string media, string exclude)
        {
            EngineTuning engine = this.FindEngine(media, exclude);
            if (engine == null)
            {
                return null;
            }
            EngineCoverage cover = new EngineCoverage();
            cover.Media = media;
            cover.Source = engine.Source.Name;
            foreach (EngineSlot slot in engine.Slots)
            {
                if (slot.Ht.StartsWith("NA.", StringComparison.OrdinalIgnoreCase))
                {
                    continue;       // the part has no sound
                }
                PartStatus status = new PartStatus();
                status.Part = slot.Part;
                status.Level = slot.Level;
                status.Ht = slot.Ht;
                cover.Parts.Add(status);
                HarmonicTuning ht = this.FindHarmonic(slot.Ht, exclude);
                if (ht == null)
                {
                    status.State = 2;
                    status.Problems.Add("harmonic tuning " + slot.Ht + " is missing");
                    continue;
                }
                status.HtSource = ht.Source.Name;
                status.Bank = ht.Bank;
                if (string.IsNullOrEmpty(ht.Bank))
                {
                    status.State = 2;
                    status.Problems.Add(ht.Name + " names no sound bank");
                    continue;
                }
                SoundSource where;
                status.State = this.BankState(ht.Bank, exclude, out where);
                status.BankSource = (where != null) ? where.Name : null;
                if (status.State == 2)
                {
                    status.Problems.Add("sound bank " + ht.Bank + " is missing");
                    continue;
                }
                SoundFile file = this.FindBank(ht.Bank, exclude);
                if (file != null)
                {
                    if (file.Info != null)
                    {
                        status.Samples = file.Info.Samples;
                    }
                    status.Problems.AddRange(CheckLoops(ht, file));
                }
            }
            return cover;
        }

        // ---- checks (2) ----

        // The loops of a harmonic tuning against the header of its bank.
        public static List<string> CheckLoops(HarmonicTuning ht, SoundFile bank)
        {
            List<string> problems = new List<string>();
            if (bank.Info == null)
            {
                problems.Add("bank " + bank.Name + " cannot be read" + ((bank.Problem != null) ? " (" + bank.Problem + ")" : ""));
                return problems;
            }
            if (bank.Info.Samples == 0)
            {
                problems.Add("bank " + bank.Name + " has no samples");
            }
            int broken = bank.Info.Partial ? 0 : bank.Info.BrokenSamples();
            if (broken > 0)
            {
                problems.Add("bank " + bank.Name + " has " + broken + " sample(s) without frequency, length or channels");
            }
            foreach (HarmonicLoop loop in ht.Loops)
            {
                // The shipped cars address the right group (ExhaustR) a second time: index = left index + number of streams (30 of
                // the stock HTs do this over 16-stream banks), so up to twice the streams is how the game's own data is written.
                if (loop.Wave < 0 || loop.Wave >= 2 * Math.Max(1, bank.Info.Waves()))
                {
                    problems.Add(ht.Name + " " + loop.Group + ": loop sample " + loop.Wave + " is outside the bank (" + bank.Info.Waves() + " streams in " + bank.Info.Samples + " samples)");
                }
                if (loop.RpmMin > loop.RpmMax)
                {
                    problems.Add(string.Format(CultureInfo.InvariantCulture, "{0} {1}: rpm_min {2} is above rpm_max {3} (sample {4})", ht.Name, loop.Group, loop.RpmMin, loop.RpmMax, loop.Wave));
                }
            }
            return problems;
        }

        // The loops of a harmonic tuning's text against a bank file on disk (a pack being edited is not in any index).
        public static List<string> CheckBankFile(string htName, string htText, string bankPath)
        {
            using (FileStream stream = File.OpenRead(bankPath))
            {
                return CheckBankStream(htName, htText, Path.GetFileName(bankPath), stream);
            }
        }

        // The same with the bank's bytes (a bank inside a package being validated).
        public static List<string> CheckBankData(string htName, string htText, string bankName, byte[] data)
        {
            return CheckBankStream(htName, htText, bankName, new MemoryStream(data, false));
        }

        private static List<string> CheckBankStream(string htName, string htText, string bankName, Stream stream)
        {
            HarmonicTuning ht = ParseHarmonic(htName, htText);
            SoundFile file = new SoundFile();
            file.Name = bankName;
            try
            {
                file.Info = FsbInfo.Read(stream);
            }
            catch (Exception e)
            {
                file.Problem = e.Message;
            }
            return CheckLoops(ht, file);
        }

        // A readable account of what a car plays: where its tuning comes from, and per part and level the harmonic tuning, the
        // bank (where it is read from, its samples) and the other cars that play the same bank; problems after each.
        public string Describe(string media)
        {
            EngineTuning engine = this.FindEngine(media, null);
            if (engine == null)
            {
                return media + ": no engine tuning (" + media + "_ET.xml) in the game or the packages.";
            }
            EngineCoverage cover = this.Coverage(media);
            StringBuilder text = new StringBuilder();
            text.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0}: engine tuning from {1}; audio {2}-{3} rpm, {4} cylinders{5}{6}",
                media, engine.Source.Name, engine.Idle, engine.Redline, engine.Cylinders,
                (engine.BlowOff.Length > 0) ? ", blow-off " + engine.BlowOff : "", this.HasModel(media) ? "" : "; NO car model tuning (silent)"));
            foreach (string part in Parts)
            {
                List<PartStatus> rows = cover.Parts.FindAll(delegate(PartStatus s) { return s.Part == part; });
                if (rows.Count == 0)
                {
                    text.AppendLine("  " + part + ": no sound");
                    continue;
                }
                text.AppendLine("  " + part + ":");
                for (int i = 0; i < rows.Count; i++)
                {
                    int last = i;
                    while (last + 1 < rows.Count && rows[last + 1].Ht == rows[i].Ht)
                    {
                        last++;
                    }
                    PartStatus s = rows[i];
                    string levels = (last == i) ? "L" + s.Level : "L" + s.Level + "-L" + rows[last].Level;
                    text.Append("    " + levels + "  " + s.Ht);
                    HarmonicTuning ht = this.FindHarmonic(s.Ht, null);
                    if (ht != null)
                    {
                        text.Append(" (" + ht.Source.Name + ", " + ht.Loops.Count + " loops");
                        if (ht.Loops.Count > 0)
                        {
                            int lo = int.MaxValue;
                            int hi = 0;
                            foreach (HarmonicLoop loop in ht.Loops)
                            {
                                lo = Math.Min(lo, loop.RpmSample);
                                hi = Math.Max(hi, loop.RpmSample);
                            }
                            text.Append(", recorded at " + lo + "-" + hi + " rpm");
                        }
                        text.Append(")");
                    }
                    if (s.Bank != null && s.Bank.Length > 0)
                    {
                        text.Append("  ->  " + s.Bank);
                        SoundFile bank = this.FindBank(s.Bank, null);
                        if (s.State == 2)
                        {
                            text.Append(" (MISSING)");
                        }
                        else if (bank != null && bank.Info != null)
                        {
                            text.Append(string.Format(CultureInfo.InvariantCulture, " ({0}{1}, {2} samples, {3} {4} Hz)", s.BankSource, (s.State == 1) ? " only" : "", bank.Info.Samples, bank.Info.Format, bank.Info.Samples > 0 ? bank.Info.Frequency[0] : 0));
                        }
                        else if (s.BankSource != null)
                        {
                            text.Append(" (" + s.BankSource + ")");
                        }
                        List<string> others = this.BankCars(s.Bank);
                        others.Remove(media);
                        if (others.Count > 0)
                        {
                            text.Append("; also played by " + others.Count + " car(s): " + string.Join(", ", others.GetRange(0, Math.Min(4, others.Count)).ToArray()) + (others.Count > 4 ? ", ..." : ""));
                        }
                    }
                    text.AppendLine();
                    foreach (string problem in s.Problems)
                    {
                        text.AppendLine("        PROBLEM: " + problem);
                    }
                    i = last;
                }
            }
            return text.ToString();
        }

        // Every problem and oddity found: broken references first. Conflicts (the same file in two sources with different
        // size), cars that only play from a package, banks and tunings nobody uses.
        public List<SoundIssue> Issues()
        {
            List<SoundIssue> issues = new List<SoundIssue>();
            foreach (SoundSource source in this.Sources)
            {
                if (source.Problem != null)
                {
                    issues.Add(Issue("warning", source.Name, "cannot be read: " + source.Problem));
                }
            }
            // One issue per problem text, with the cars that hit it (a harmonic tuning is shared by many cars).
            List<string> order = new List<string>();
            Dictionary<string, List<string>> carsOf = new Dictionary<string, List<string>>();
            Dictionary<string, string> kindOf = new Dictionary<string, string>();
            foreach (string media in this.EngineMedia())
            {
                EngineCoverage cover = this.Coverage(media);
                foreach (PartStatus part in cover.Parts)
                {
                    List<string> texts = new List<string>(part.Problems);
                    if (part.State == 1 && part.Problems.Count == 0)
                    {
                        texts.Add("plays " + part.Bank + " only from the package \"" + part.BankSource + "\": silent without it");
                    }
                    foreach (string text in texts)
                    {
                        List<string> cars;
                        if (!carsOf.TryGetValue(text, out cars))
                        {
                            cars = new List<string>();
                            carsOf[text] = cars;
                            kindOf[text] = (part.Problems.Count > 0) ? "error" : "warning";
                            order.Add(text);
                        }
                        if (!cars.Contains(media))
                        {
                            cars.Add(media);
                        }
                    }
                }
            }
            foreach (string text in order)
            {
                List<string> cars = carsOf[text];
                issues.Add(Issue(kindOf[text], (cars.Count == 1) ? cars[0] : cars[0] + " +" + (cars.Count - 1) + " cars", text));
            }
            // The same path in two sources (a file of the same name in another folder, as the voice banks per language, is no clash).
            Dictionary<string, List<SoundFile>> byPath = new Dictionary<string, List<SoundFile>>(StringComparer.OrdinalIgnoreCase);
            foreach (SoundFile file in this.Files)
            {
                if (file.Name.EndsWith(".fsb", StringComparison.OrdinalIgnoreCase) || file.Name.EndsWith(".fev", StringComparison.OrdinalIgnoreCase))
                {
                    Push(byPath, file.Path, file);
                }
            }
            foreach (KeyValuePair<string, List<SoundFile>> pair in byPath)
            {
                if (pair.Value.Count > 1)
                {
                    bool differ = pair.Value.Exists(delegate(SoundFile f) { return f.Size != pair.Value[0].Size; });
                    issues.Add(Issue(differ ? "warning" : "info", pair.Value[0].Name, "in " + pair.Value.Count + " places (" + SourceNames(pair.Value) + ")" + (differ ? " with different sizes: only one is used" : "")));
                }
            }
            foreach (KeyValuePair<string, List<EngineTuning>> pair in this.engines)
            {
                if (pair.Value.Count > 1)
                {
                    issues.Add(Issue("warning", pair.Key + "_ET.xml", "engine tuning in " + pair.Value.Count + " sources: the first one (" + pair.Value[0].Source.Name + ") is used"));
                }
            }
            foreach (SoundFile file in this.Files)
            {
                if (file.Problem != null)
                {
                    issues.Add(Issue("error", file.Name, "bank header: " + file.Problem + " (" + file.Source.Name + ")"));
                }
            }
            List<string> usedBanks = new List<string>();
            List<string> usedHts = new List<string>();
            foreach (string media in this.EngineMedia())
            {
                foreach (EngineSlot slot in this.FindEngine(media, null).Slots)
                {
                    if (!usedHts.Contains(slot.Ht.ToLowerInvariant()))
                    {
                        usedHts.Add(slot.Ht.ToLowerInvariant());
                    }
                }
            }
            foreach (List<HarmonicTuning> list in this.harmonics.Values)
            {
                foreach (HarmonicTuning ht in list)
                {
                    if (!string.IsNullOrEmpty(ht.Bank) && !usedBanks.Contains(ht.Bank.ToLowerInvariant()))
                    {
                        usedBanks.Add(ht.Bank.ToLowerInvariant());
                    }
                    if (!usedHts.Contains(ht.Name.ToLowerInvariant()))
                    {
                        issues.Add(Issue("info", ht.Name, "harmonic tuning no engine tuning names (" + ht.Source.Name + ")"));
                    }
                }
            }
            foreach (SoundFile file in this.Files)
            {
                if (file.Info != null && string.Equals(file.Category, "Engines", StringComparison.OrdinalIgnoreCase) && file.Path.ToLowerInvariant().Contains("\\soundbanks\\")
                    && !usedBanks.Contains(file.Name.ToLowerInvariant()))
                {
                    issues.Add(Issue("info", file.Name, "engine bank no harmonic tuning names (" + file.Source.Name + ")"));
                }
            }
            issues.Sort(delegate(SoundIssue a, SoundIssue b) { return Rank(a.Severity).CompareTo(Rank(b.Severity)); });
            return issues;
        }

        // What goes silent when a source (a package's display name) is removed: "<car>: <part> L<level> (<bank>)".
        // Cars whose own engine tuning is in that package go with it and are not listed.
        public List<string> SilencedBy(string sourceName)
        {
            List<string> lost = new List<string>();
            foreach (string media in this.EngineMedia())
            {
                EngineCoverage cover = this.Coverage(media, sourceName);
                if (cover == null)
                {
                    continue;
                }
                foreach (PartStatus part in cover.Parts)
                {
                    if (part.State == 2)
                    {
                        lost.Add(media + ": " + part.Part + " L" + part.Level + " (" + (part.Bank ?? part.Ht) + ")");
                    }
                }
            }
            return lost;
        }

        public string Summary()
        {
            int banks = 0;
            foreach (SoundFile file in this.Files)
            {
                if (file.Info != null)
                {
                    banks++;
                }
            }
            int harmonicCount = 0;
            foreach (List<HarmonicTuning> list in this.harmonics.Values)
            {
                harmonicCount += list.Count;
            }
            return string.Format(CultureInfo.InvariantCulture, "{0} sources, {1} audio files, {2} banks, {3} engine tunings, {4} harmonic tunings, {5} car models",
                this.Sources.Count, this.Files.Count, banks, this.engines.Count, harmonicCount, this.models.Count);
        }

        private static string SourceNames(List<SoundFile> files)
        {
            List<string> names = new List<string>();
            foreach (SoundFile file in files)
            {
                names.Add(file.Source.Name + ", " + file.Size.ToString("N0", CultureInfo.InvariantCulture) + " bytes");
            }
            return string.Join("; ", names.ToArray());
        }

        private static int Rank(string severity)
        {
            return (severity == "error") ? 0 : (severity == "warning") ? 1 : 2;
        }

        private static SoundIssue Issue(string severity, string subject, string text)
        {
            SoundIssue issue = new SoundIssue();
            issue.Severity = severity;
            issue.Subject = subject;
            issue.Text = text;
            return issue;
        }
    }
}
