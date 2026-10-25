namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;

    // The game's localized text: GAME:\Media\StringTables\<language>.zip holds one .str file per
    // database table (Data_Car.str, Tracks.str...). The database stores "_&<id>"; the .str file of
    // the table maps the low 16 bits of the id to the text.
    //
    // .str ("LSB2", big endian): the number of entries at offset 40, then 6-byte entries
    // (16-bit key, 32-bit offset in characters) from offset 44, an end entry (FFFF), and the
    // zero-terminated UTF-16 strings.
    public class StringTable
    {
        private readonly Dictionary<string, Dictionary<int, string>> files = new Dictionary<string, Dictionary<int, string>>(StringComparer.OrdinalIgnoreCase);

        public string Language { get; private set; }
        public string SourcePath { get; private set; }

        public static List<string> Languages(string gameRoot)
        {
            List<string> languages = new List<string>();
            string folder = (gameRoot == null) ? null : Path.Combine(Path.Combine(gameRoot, "Media"), "StringTables");
            if (folder != null && Directory.Exists(folder))
            {
                foreach (string file in Directory.GetFiles(folder, "*.zip"))
                {
                    languages.Add(Path.GetFileNameWithoutExtension(file).ToUpperInvariant());
                }
            }
            languages.Sort(StringComparer.OrdinalIgnoreCase);
            return languages;
        }

        public static StringTable Load(string gameRoot, string language)
        {
            string path = Path.Combine(Path.Combine(Path.Combine(gameRoot, "Media"), "StringTables"), language + ".zip");
            StringTable table = new StringTable();
            table.Language = language;
            table.SourcePath = path;
            ForzaZip zip = new ForzaZip(path);
            foreach (ForzaZip.Entry entry in zip.Entries)
            {
                if (entry.Name.EndsWith(".str", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        table.files[Path.GetFileNameWithoutExtension(entry.Name)] = Parse(zip.Read(entry));
                    }
                    catch (Exception)
                    {
                    }
                }
            }
            return table;
        }

        // A DLC package's .str file of a table on top of the game's: its keys replace the game's.
        public void Overlay(string table, byte[] strFile)
        {
            Dictionary<int, string> own;
            if (!this.files.TryGetValue(table, out own))
            {
                own = new Dictionary<int, string>();
                this.files[table] = own;
            }
            foreach (KeyValuePair<int, string> text in Parse(strFile))
            {
                own[text.Key] = text.Value;
            }
        }

        public static Dictionary<int, string> Parse(byte[] data)
        {
            if (data.Length < 44 || Encoding.ASCII.GetString(data, 0, 4) != "LSB2")
            {
                throw new InvalidDataException("Not a string table (no LSB2 header).");
            }
            Dictionary<int, string> strings = new Dictionary<int, string>();
            int count = (int) ReadUInt32(data, 40);
            int text = 44 + ((count + 1) * 6);
            for (int i = 0; i < count; i++)
            {
                int entry = 44 + (i * 6);
                int key = (data[entry] << 8) | data[entry + 1];
                int position = text + ((int) ReadUInt32(data, entry + 2) * 2);
                StringBuilder value = new StringBuilder();
                while (position + 1 < data.Length && (data[position] != 0 || data[position + 1] != 0))
                {
                    value.Append((char) ((data[position] << 8) | data[position + 1]));
                    position += 2;
                }
                if (!strings.ContainsKey(key))
                {
                    strings[key] = value.ToString();
                }
            }
            return strings;
        }

        // The text for a database value "_&<id>" of a table's column, or null.
        public string Lookup(string table, string column, string value)
        {
            uint id;
            if (value == null || !value.StartsWith("_&") || !uint.TryParse(value.Substring(2), NumberStyles.None, CultureInfo.InvariantCulture, out id))
            {
                return null;
            }
            // The make name of a car is the make's own string.
            string file = (string.Equals(table, "Data_Car", StringComparison.OrdinalIgnoreCase) && string.Equals(column, "MakeName", StringComparison.OrdinalIgnoreCase)) ? "List_CarMake" : table;
            Dictionary<int, string> strings;
            string text;
            return (this.files.TryGetValue(file, out strings) && strings.TryGetValue((int) (id & 0xFFFF), out text)) ? text : null;
        }

        private static uint ReadUInt32(byte[] data, int offset)
        {
            return (uint) ((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3]);
        }
    }

    // The tool's preferences: the [App] section of ForzaStudio.ini next to the exe (AppIni), key=value lines.
    public static class AppSettings
    {
        public static string Get(string key, string fallback)
        {
            return AppIni.GetApp(key, fallback);
        }

        public static void Set(string key, string value)
        {
            AppIni.SetApp(key, value);
        }

        // A colour stored as RRGGBB (hex): its three channels, or the fallback's when the value is not one.
        public static int[] GetRgb(string key, string fallback)
        {
            string text = Get(key, fallback).TrimStart('#');
            int value;
            if (text.Length != 6 || !int.TryParse(text, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out value))
            {
                value = int.Parse(fallback.TrimStart('#'), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
            }
            return new int[] { (value >> 16) & 255, (value >> 8) & 255, value & 255 };
        }
    }
}
