namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;

    // Port.ini next to the exe: the port's own rules in a plain file (ROADMAP 14.1 "all port settings
    // in clear .ini files", 12.3 Settings window, user 2026-10-20): car and row Ids, the PI line of car
    // DLC packages, the drag tyre row. Written with the defaults when missing; Tools > Settings > PORT
    // edits it. Upgrades.ini (CarUpgrades) holds the generic upgrade rules.
    internal static class PortSettings
    {
        public const string FileName = "Port.ini";

        public const string DefaultIni =
@"; Forza Studio - port settings (ROADMAP C2, 14.1)
;
; Edit freely, here or in Tools > Settings > PORT; delete this file to get the defaults back.
; The generic upgrades of ported cars are in Upgrades.ini.

[Ids]
; A car installed in the game gets the first free Id above every FM4 / DLC car (1638 today), never the
; source game's (user 2026-10-22). Set InstallFirstCar > 0 ONLY in development / testing builds.
; A DLC pack asks for its own first Id in the pack window.
; FM4's own cars use 247-1473, FH1 DLC 1519-1568.
InstallFirstCar = 0
; Ids Forza Studio gives to new rows of these tables: the next free one from this value
; (0 = the next after the largest, as before). Not checked in game yet.
List_Wheels = 9000000
Events = 40000
Races = 400000
EventParticipants = 4000000

[PI]
; Car DLC packages come without their base game to compare with: FM4 PI = Slope x source PI + Offset,
; fit on the cars that game shares with FM4 (FH1 2026-10-17, error 0.0145; FM3 2026-10-18, 315 cars,
; error 0.0191).
FH1.Slope = 1.189
FH1.Offset = -0.363
FM3.Slope = 0.7779
FM3.Offset = 0.1668

[Tires]
; FM4 sells drag tyres (compound 9) to 502 of its 503 cars, always level 4 for 10000 Cr. A ported car
; without them gets that row (the next free level if this one is taken). on / off.
DragTires = on
DragLevel = 4
DragPrice = 10000

[Damage]
; EXPERIMENT (ROADMAP B1 tests T2 / T3): how the per-vertex damage words of a ported FH car are written.
; copy = as they are in the FH file (the old behaviour); normalize = unit direction in FM4's 10:11:11 format.
Words = copy
; with Words = normalize: both | main | lod0 (which of the car's two tables is converted)
Tables = both
; with Words = normalize: sections whose sign is reversed, comma separated, or * for all
; (T3 on the Supra: bumperFi, bumperFc, bumperRc, skirtRa, skirtLa, skirtRd)
FlipSections =
";

        public static string IniPath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FileName); }
        }

        private static Dictionary<string, Dictionary<string, string>> cache;
        private static DateTime cacheTime;

        public static string Get(string section, string key, string fallback)
        {
            Dictionary<string, string> values;
            string value;
            return (Load().TryGetValue(section, out values) && values.TryGetValue(key, out value) && value.Length > 0) ? value : fallback;
        }

        public static double GetDouble(string section, string key, double fallback)
        {
            double value;
            return double.TryParse(Get(section, key, ""), NumberStyles.Float, CultureInfo.InvariantCulture, out value) ? value : fallback;
        }

        public static long GetLong(string section, string key, long fallback)
        {
            return (long) Math.Round(GetDouble(section, key, fallback));
        }

        public static bool GetBool(string section, string key, bool fallback)
        {
            string value = Get(section, key, fallback ? "on" : "off").ToLowerInvariant();
            return value == "on" || value == "1" || value == "yes" || value == "true";
        }

        // The file's sections (re-read when it changes), with the default sections it lacks.
        private static Dictionary<string, Dictionary<string, string>> Load()
        {
            string path = IniPath;
            DateTime time = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
            if (cache != null && time == cacheTime)
            {
                return cache;
            }
            if (time == DateTime.MinValue)
            {
                IniFile.WriteDefaults(path, DefaultIni);
                time = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
            }
            cache = IniFile.Load(path, DefaultIni);
            cacheTime = time;
            return cache;
        }
    }

    // Plain .ini files of the tool (Port.ini, Upgrades.ini): "; comments", [sections], key = value.
    internal static class IniFile
    {
        // A section as written: its comment (the lines under the header before the first key) and its
        // keys in order, each with the comment lines just above it.
        public sealed class Section
        {
            public string Name;
            public string Comment = "";
            public List<string[]> Entries = new List<string[]>();   // key, value, note
        }

        public static List<Section> Layout(string text)
        {
            List<Section> sections = new List<Section>();
            Section section = null;
            List<string> comments = new List<string>();
            foreach (string raw in text.Replace("\r\n", "\n").Split('\n'))
            {
                string line = raw.Trim();
                if (line.StartsWith(";", StringComparison.Ordinal))
                {
                    comments.Add(line.Substring(1).Trim());
                    continue;
                }
                if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal))
                {
                    section = new Section();
                    section.Name = line.Substring(1, line.Length - 2).Trim();
                    sections.Add(section);
                    comments.Clear();
                    continue;
                }
                int comment = line.IndexOf(';');
                string body = (comment >= 0) ? line.Substring(0, comment) : line;
                int equals = body.IndexOf('=');
                if (section == null || equals <= 0)
                {
                    if (line.Length == 0 && section != null && section.Entries.Count == 0 && comments.Count > 0)
                    {
                        // A blank line ends the section's own comment.
                        section.Comment = string.Join(" ", comments.ToArray());
                        comments.Clear();
                    }
                    continue;
                }
                if (section.Entries.Count == 0 && section.Comment.Length == 0 && comments.Count > 0 && LooksLikeSectionNote(section.Name))
                {
                    section.Comment = string.Join(" ", comments.ToArray());
                    comments.Clear();
                }
                string note = string.Join(" ", comments.ToArray());
                if (comment >= 0)
                {
                    note = (note + " " + line.Substring(comment + 1).Trim()).Trim();
                }
                section.Entries.Add(new string[] { body.Substring(0, equals).Trim(), body.Substring(equals + 1).Trim(), note });
                comments.Clear();
            }
            return sections;
        }

        // Sections whose first comment describes the section, not its first key.
        private static bool LooksLikeSectionNote(string name)
        {
            return name.StartsWith("Aspiration", StringComparison.OrdinalIgnoreCase) || name.StartsWith("Drivetrain", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("List_", StringComparison.OrdinalIgnoreCase);
        }

        // The file's sections; the sections it lacks come from the defaults (added to them later).
        public static Dictionary<string, Dictionary<string, string>> Load(string path, string defaults)
        {
            Dictionary<string, Dictionary<string, string>> sections = CarUpgrades.ParseIni(File.Exists(path) ? File.ReadAllText(path) : defaults);
            foreach (KeyValuePair<string, Dictionary<string, string>> section in CarUpgrades.ParseIni(defaults))
            {
                if (!sections.ContainsKey(section.Key))
                {
                    sections[section.Key] = section.Value;
                }
            }
            return sections;
        }

        // Writes the defaults (Windows line ends); false when the folder is read-only.
        public static bool WriteDefaults(string path, string defaults)
        {
            try
            {
                File.WriteAllText(path, defaults.Replace("\r\n", "\n").Replace("\n", "\r\n"), Encoding.UTF8);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        // One value changed in place: the comments and the order of the file stay; a key the section
        // lacks is added at its end, a missing section at the end of the file.
        public static void Set(string path, string defaults, string section, string key, string value)
        {
            if (!File.Exists(path))
            {
                WriteDefaults(path, defaults);
            }
            List<string> lines = new List<string>(File.ReadAllText(path).Replace("\r\n", "\n").Split('\n'));
            int start = -1;
            int end = lines.Count;
            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i].Trim();
                if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal))
                {
                    if (start >= 0)
                    {
                        end = i;
                        break;
                    }
                    if (string.Equals(line.Substring(1, line.Length - 2).Trim(), section, StringComparison.OrdinalIgnoreCase))
                    {
                        start = i;
                    }
                }
            }
            if (start < 0)
            {
                if (lines.Count > 0 && lines[lines.Count - 1].Trim().Length > 0)
                {
                    lines.Add("");
                }
                lines.Add("[" + section + "]");
                lines.Add(key + " = " + value);
            }
            else
            {
                int found = -1;
                int last = start;
                for (int i = start + 1; i < end; i++)
                {
                    string line = lines[i];
                    int comment = line.IndexOf(';');
                    string body = (comment >= 0) ? line.Substring(0, comment) : line;
                    int equals = body.IndexOf('=');
                    if (body.Trim().Length > 0)
                    {
                        last = i;
                    }
                    if (equals > 0 && string.Equals(body.Substring(0, equals).Trim(), key, StringComparison.OrdinalIgnoreCase))
                    {
                        found = i;
                    }
                }
                if (found >= 0)
                {
                    string line = lines[found];
                    int comment = line.IndexOf(';');
                    lines[found] = line.Substring(0, line.IndexOf('=')).TrimEnd() + " = " + value + ((comment >= 0) ? "   " + line.Substring(comment) : "");
                }
                else
                {
                    lines.Insert(last + 1, key + " = " + value);
                }
            }
            File.WriteAllText(path, string.Join("\r\n", lines.ToArray()), Encoding.UTF8);
        }
    }
}
