namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;

    // A whole .str string table ("LSB2"), for adding strings. Layout (big endian), measured on the
    // FM4 and FH2 tables:
    //   0x00 "LSB2", 1, 2, 0x24, 4, 0x24 (header words), then the offsets of sections B, C (0 when
    //        absent) and D at 0x18 / 0x1C / 0x20;
    //   A    at 0x24: its length (counting itself), the entry count, 6-byte entries
    //        (16-bit key, offset in characters) sorted by key, an FFFF entry whose offset is the
    //        text length, the zero-terminated UTF-16 strings and one more zero character;
    //   B    length, count, 8-byte entries (key, 1-based rank in the source order, offset into the
    //        names) sorted by key, then the zero-terminated ASCII names ("IDS_DisplayName_461"...)
    //        in source order;
    //   C    optional: the localizers' comments, kept as they are;
    //   D    optional: length, count, the keys in source order (not alphabetical: IDS_Bio_9 comes
    //        before IDS_Bio_10).
    // The game looks strings up in A; B and D are kept consistent with it.
    public class StrFile
    {
        public class Entry
        {
            public int Key;
            public string Text;
            public string Name;
            public int Rank;            // 1-based position in the source order (B and D)
        }

        public List<Entry> Entries = new List<Entry>();
        private byte[] comments;        // section C, or null
        private byte[] header;          // the words before the section offsets
        private bool hasNames;          // section B present
        private bool hasOrder;          // section D present

        public StrFile(byte[] data)
        {
            if (data.Length < 44 || Encoding.ASCII.GetString(data, 0, 4) != "LSB2")
            {
                throw new InvalidDataException("Not a string table (no LSB2 header).");
            }
            this.header = new byte[0x18];
            Buffer.BlockCopy(data, 0, this.header, 0, 0x18);
            int b = (int) U32(data, 0x18);
            int c = (int) U32(data, 0x1C);
            int d = (int) U32(data, 0x20);
            Dictionary<int, Entry> byKey = new Dictionary<int, Entry>();
            int count = (int) U32(data, 0x28);
            int text = 0x2C + ((count + 1) * 6);
            for (int i = 0; i < count; i++)
            {
                int at = 0x2C + (i * 6);
                Entry entry = new Entry();
                entry.Key = (data[at] << 8) | data[at + 1];
                entry.Text = ReadUtf16(data, text + ((int) U32(data, at + 2) * 2));
                this.Entries.Add(entry);
                byKey[entry.Key] = entry;
            }
            this.hasNames = b > 0;
            this.hasOrder = d > 0;
            if (b > 0)
            {
                int names = (int) U32(data, b + 4);
                int nameText = b + 8 + (names * 8);
                for (int i = 0; i < names; i++)
                {
                    int at = b + 8 + (i * 8);
                    int key = (data[at] << 8) | data[at + 1];
                    Entry entry;
                    if (byKey.TryGetValue(key, out entry))
                    {
                        entry.Rank = (data[at + 2] << 8) | data[at + 3];
                        entry.Name = ReadAscii(data, nameText + (int) U32(data, at + 4));
                    }
                }
            }
            if (c > 0)
            {
                int length = (int) U32(data, c);
                this.comments = new byte[length];
                Buffer.BlockCopy(data, c, this.comments, 0, length);
            }
        }

        public bool Contains(int key)
        {
            foreach (Entry entry in this.Entries)
            {
                if (entry.Key == key)
                {
                    return true;
                }
            }
            return false;
        }

        // Adds or replaces the string of a 16-bit key.
        public void Set(int key, string text, string name)
        {
            foreach (Entry entry in this.Entries)
            {
                if (entry.Key == key)
                {
                    entry.Text = text;
                    entry.Name = name ?? entry.Name;
                    return;
                }
            }
            Entry added = new Entry();
            added.Key = key;
            added.Text = text;
            added.Name = name ?? string.Format("IDS_Added_{0:X4}", key);
            int last = 0;
            foreach (Entry entry in this.Entries)
            {
                last = Math.Max(last, entry.Rank);
            }
            added.Rank = last + 1;
            this.Entries.Add(added);
        }

        // Removes the string of a key (an uninstalled car's); false when the table has none.
        public bool Remove(int key)
        {
            return this.Entries.RemoveAll(delegate(Entry entry) { return entry.Key == key; }) > 0;
        }

        public byte[] ToBytes()
        {
            List<Entry> byKey = new List<Entry>(this.Entries);
            byKey.Sort(delegate(Entry x, Entry y) { return x.Key.CompareTo(y.Key); });
            // Source order (the B ranks): names and D follow it; added strings come last.
            List<Entry> byName = new List<Entry>(this.Entries);
            byName.Sort(delegate(Entry x, Entry y) { return x.Rank.CompareTo(y.Rank); });

            // A: entries, strings.
            MemoryStream a = new MemoryStream();
            WriteU32(a, 0);
            WriteU32(a, (uint) byKey.Count);
            MemoryStream strings = new MemoryStream();
            foreach (Entry entry in byKey)
            {
                WriteU16(a, entry.Key);
                WriteU32(a, (uint) (strings.Length / 2));
                foreach (char ch in entry.Text)
                {
                    WriteU16(strings, ch);
                }
                WriteU16(strings, 0);
            }
            WriteU16(a, 0xFFFF);
            WriteU32(a, (uint) (strings.Length / 2));
            strings.WriteTo(a);
            WriteU16(a, 0);     // one more empty character closes the section
            byte[] sectionA = a.ToArray();
            PutU32(sectionA, 0, (uint) sectionA.Length);

            // B: entries by key, names stored in source order.
            MemoryStream names = new MemoryStream();
            Dictionary<Entry, int> nameOffset = new Dictionary<Entry, int>();
            foreach (Entry entry in byName)
            {
                nameOffset[entry] = (int) names.Length;
                byte[] ascii = Encoding.ASCII.GetBytes(entry.Name ?? "");
                names.Write(ascii, 0, ascii.Length);
                names.WriteByte(0);
            }
            MemoryStream bEntries = new MemoryStream();
            List<Entry> named = new List<Entry>(byKey);
            foreach (Entry entry in named)
            {
                WriteU16(bEntries, entry.Key);
                WriteU16(bEntries, entry.Rank);
                WriteU32(bEntries, (uint) nameOffset[entry]);
            }
            MemoryStream b = new MemoryStream();
            WriteU32(b, (uint) (8 + bEntries.Length + names.Length));
            WriteU32(b, (uint) named.Count);
            bEntries.WriteTo(b);
            names.WriteTo(b);

            // D: keys in source order.
            MemoryStream d = new MemoryStream();
            WriteU32(d, (uint) (8 + (byName.Count * 2)));
            WriteU32(d, (uint) byName.Count);
            foreach (Entry entry in byName)
            {
                WriteU16(d, entry.Key);
            }

            MemoryStream file = new MemoryStream();
            file.Write(this.header, 0, this.header.Length);
            int afterA = 0x24 + sectionA.Length;
            int offsetB = this.hasNames ? afterA : 0;
            int afterB = afterA + (this.hasNames ? (int) b.Length : 0);
            int offsetC = (this.comments != null) ? afterB : 0;
            int offsetD = this.hasOrder ? afterB + ((this.comments != null) ? this.comments.Length : 0) : 0;
            WriteU32(file, (uint) offsetB);
            WriteU32(file, (uint) offsetC);
            WriteU32(file, (uint) offsetD);
            file.Write(sectionA, 0, sectionA.Length);
            if (this.hasNames)
            {
                b.WriteTo(file);
            }
            if (this.comments != null)
            {
                file.Write(this.comments, 0, this.comments.Length);
            }
            if (this.hasOrder)
            {
                d.WriteTo(file);
            }
            return file.ToArray();
        }

        private static string ReadUtf16(byte[] data, int position)
        {
            StringBuilder value = new StringBuilder();
            while (position + 1 < data.Length && (data[position] != 0 || data[position + 1] != 0))
            {
                value.Append((char) ((data[position] << 8) | data[position + 1]));
                position += 2;
            }
            return value.ToString();
        }

        private static string ReadAscii(byte[] data, int position)
        {
            int end = position;
            while (end < data.Length && data[end] != 0)
            {
                end++;
            }
            return Encoding.ASCII.GetString(data, position, end - position);
        }

        private static uint U32(byte[] data, int offset)
        {
            return (uint) ((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3]);
        }

        private static void PutU32(byte[] data, int offset, uint value)
        {
            data[offset] = (byte) (value >> 24);
            data[offset + 1] = (byte) (value >> 16);
            data[offset + 2] = (byte) (value >> 8);
            data[offset + 3] = (byte) value;
        }

        private static void WriteU32(Stream output, uint value)
        {
            output.WriteByte((byte) (value >> 24));
            output.WriteByte((byte) (value >> 16));
            output.WriteByte((byte) (value >> 8));
            output.WriteByte((byte) value);
        }

        private static void WriteU16(Stream output, int value)
        {
            output.WriteByte((byte) (value >> 8));
            output.WriteByte((byte) value);
        }
    }
}
