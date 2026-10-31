namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.Globalization;
    using System.IO;
    using System.Text;

    // The livery decals of a car make: its own logo decal and the common ones, as the official packs add them
    // (offer 30, Ultima, make 96; the same for MG, Viper, Ascari, Hudson). What a pack holds:
    //   Livery_Decals            one row: ID, Filename GAME:\Media\Livery\Decals\decalNNN.carbin, DisplayName
    //                            (Livery_Decals.str, every language), AlphaSort = the make's name;
    //   Livery_DecalsSortOrder   46 rows for the make: Sequence 1 = its own decal, the others the common ones
    //                            (Turn 10, FM4, FM3, FM2, FM, Top Gear, MSN, the sponsors, XBOX) = a copy of
    //                            another make's rows;
    //   Media\Livery\decals\decalNNN.carbin   the decal itself (loose in the pack: Livery.zip is mounted at
    //                            Media\Livery, a file the zip lacks comes from the pack's loose folder).
    // A decal carbin is always 350075 bytes: the same header and trailer (a quad) and a 256x256 texture,
    // 4 bytes a texel R, G, B, A (straight alpha), Xbox 360 tiled, from 0x154: the base level, then the mips
    // 128, 64 and 32 (each tiled), then a tail of smaller mips (to 0x556C8) that this fills with the
    // average texel. Checked by decoding the official decals (Turn 10, Ultima) to images.
    public static class MakeDecals
    {
        public const string Table = "Livery_Decals";
        public const string Sort = "Livery_DecalsSortOrder";
        public const string TemplateEntry = "decals/decal378.carbin";       // Turn 10: any official decal gives the header
        private const int Size = 256;
        private const int Start = 0x154;
        private const int TailStart = 0x55154;
        private const int TailEnd = 0x556C8;
        private const int Length = 350075;

        public static string LiveryPath(string root)
        {
            return Path.Combine(Path.Combine(root, "Media"), "Livery.zip");
        }

        public static bool Supported(SqliteDatabase db)
        {
            return DbIds.HasTable(db, Table) && DbIds.HasTable(db, Sort);
        }

        // The carbin of a decal showing the image (fitted in the square), on an official decal's header.
        public static byte[] BuildCarbin(Image logo, byte[] template)
        {
            int[] rgba = new int[Size * Size * 4];
            using (Bitmap fitted = CarMake.Fit(logo, Size, Size))
            {
                for (int y = 0; y < Size; y++)
                {
                    for (int x = 0; x < Size; x++)
                    {
                        Color c = fitted.GetPixel(x, y);
                        int at = ((y * Size) + x) * 4;
                        rgba[at] = c.R;
                        rgba[at + 1] = c.G;
                        rgba[at + 2] = c.B;
                        rgba[at + 3] = c.A;
                    }
                }
            }
            return BuildCarbin(rgba, template);
        }

        // The base texels (256x256, R, G, B, A each) of a decal carbin of FM4 or of a game that writes them the
        // same way (FH2's Bowler decal is 8 bytes longer in its header; its texture starts at 0x154 too).
        public static int[] ReadTexels(byte[] carbin)
        {
            if (carbin.Length < Start + (Size * Size * 4))
            {
                throw new InvalidDataException("A decal carbin of " + carbin.Length + " bytes has no 256x256 texture.");
            }
            int[] rgba = new int[Size * Size * 4];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    int p = Start + XenosTiling.Tiled2D(x, y, Size, 2);
                    for (int c = 0; c < 4; c++)
                    {
                        rgba[(((y * Size) + x) * 4) + c] = carbin[p + c];
                    }
                }
            }
            return rgba;
        }

        // The carbin of a decal with these texels (256x256, R, G, B, A), on an official decal's header.
        public static byte[] BuildCarbin(int[] rgba, byte[] template)
        {
            if (template.Length != Length)
            {
                throw new InvalidDataException("The template decal is " + template.Length + " bytes, not " + Length + ".");
            }
            byte[] data = (byte[]) template.Clone();
            rgba = (int[]) rgba.Clone();
            // The colour under the transparent texels: the logo's own (a white or black fringe would show
            // when the game filters the edge).
            long weight = 0;
            long[] sum = new long[3];
            for (int i = 0; i < Size * Size; i++)
            {
                int a = rgba[(i * 4) + 3];
                weight += a;
                for (int c = 0; c < 3; c++)
                {
                    sum[c] += rgba[(i * 4) + c] * a;
                }
            }
            int[] fill = (weight > 0) ? new int[] { (int) (sum[0] / weight), (int) (sum[1] / weight), (int) (sum[2] / weight) } : new int[] { 255, 255, 255 };
            for (int i = 0; i < Size * Size; i++)
            {
                if (rgba[(i * 4) + 3] == 0)
                {
                    for (int c = 0; c < 3; c++)
                    {
                        rgba[(i * 4) + c] = fill[c];
                    }
                }
            }
            int offset = Start;
            int[] level = rgba;
            for (int width = Size; width >= 32; width /= 2)
            {
                int pitch = ((width + 31) / 32) * 32;
                for (int y = 0; y < width; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int p = offset + XenosTiling.Tiled2D(x, y, pitch, 2);
                        for (int c = 0; c < 4; c++)
                        {
                            data[p + c] = (byte) level[(((y * width) + x) * 4) + c];
                        }
                    }
                }
                offset += width * width * 4;
                level = Halve(level, width, fill);
            }
            // The tail: what is left of the chain, as the average texel (alpha-weighted colour).
            int[] average = level;
            for (int width = 16; width > 1; width /= 2)
            {
                average = Halve(average, width, fill);
            }
            for (int p = TailStart; p + 4 <= TailEnd; p += 4)
            {
                for (int c = 0; c < 4; c++)
                {
                    data[p + c] = (byte) average[c];
                }
            }
            return data;
        }

        // The next mip: 2x2 texels averaged, the colour weighted by alpha.
        private static int[] Halve(int[] level, int width, int[] fill)
        {
            int half = Math.Max(1, width / 2);
            int[] next = new int[half * half * 4];
            for (int y = 0; y < half; y++)
            {
                for (int x = 0; x < half; x++)
                {
                    long a = 0;
                    long[] color = new long[3];
                    for (int j = 0; j < 4; j++)
                    {
                        int at = ((((y * 2) + (j / 2)) * width) + (x * 2) + (j % 2)) * 4;
                        a += level[at + 3];
                        for (int c = 0; c < 3; c++)
                        {
                            color[c] += level[at + c] * level[at + 3];
                        }
                    }
                    int to = ((y * half) + x) * 4;
                    for (int c = 0; c < 3; c++)
                    {
                        next[to + c] = (a > 0) ? (int) (color[c] / a) : fill[c];
                    }
                    next[to + 3] = (int) ((a + 2) / 4);
                }
            }
            return next;
        }

        // Adds the make's own decal (the Livery_Decals row and its name in every language) and its Sort rows
        // (the common decals copied from another make). livery: the Livery.zip the carbin goes in (only read
        // here, to find a free file name). Returns the carbin's entry name in Livery.zip.
        public static string Add(SqliteDatabase db, long makeId, string name, ForzaZip livery, Dictionary<string, ForzaZip> zips, StringBuilder report)
        {
            if (!Supported(db))
            {
                report.AppendLine("  decals: the database has no " + Table + ", none added");
                return null;
            }
            long id = DbIds.Next(db, Table, "ID");
            long number = id + 1;
            while (livery.Find("decals/decal" + number.ToString("000", CultureInfo.InvariantCulture) + ".carbin") != null
                || Convert.ToInt64(db.Scalar("SELECT count(*) FROM " + Table + " WHERE lower(Filename) LIKE ?", "%\\decal" + number.ToString("000", CultureInfo.InvariantCulture) + ".carbin")) > 0)
            {
                number++;
            }
            string file = "decal" + number.ToString("000", CultureInfo.InvariantCulture) + ".carbin";
            string text = DisplayName(db, zips, id, name);
            db.Execute("INSERT INTO " + Table + " (ID, Filename, DisplayName, AlphaSort) VALUES (?, ?, ?, ?)", id, @"GAME:\Media\Livery\Decals\" + file, text, name);

            // The common decals: another make's rows without its own decal (the row with Sequence 1 that no
            // other make has).
            long template = TemplateMake(db);
            long first = DbIds.Next(db, Sort, "ID");
            int copied = 0;
            foreach (object[] row in db.Query("SELECT Sequence, Livery_DecalID FROM " + Sort + " WHERE MakeID = ? ORDER BY Sequence, ID", template).Rows)
            {
                long sequence = Convert.ToInt64(row[0], CultureInfo.InvariantCulture);
                long decal = Convert.ToInt64(row[1], CultureInfo.InvariantCulture);
                if (sequence == 1 && Convert.ToInt64(db.Scalar("SELECT count(*) FROM " + Sort + " WHERE Livery_DecalID = ? AND MakeID <> ?", decal, template)) == 0)
                {
                    continue;
                }
                db.Execute("INSERT INTO " + Sort + " (ID, MakeID, Sequence, Livery_DecalID) VALUES (?, ?, ?, ?)", first + copied, makeId, sequence, decal);
                copied++;
            }
            db.Execute("INSERT INTO " + Sort + " (ID, MakeID, Sequence, Livery_DecalID) VALUES (?, ?, 1, ?)", first + copied, makeId, id);
            report.AppendLine(string.Format(CultureInfo.InvariantCulture, "  decals: {0} (decal {1}, {2}) and {3} common ones (as make {4}'s) in {5}", name, id, file, copied, template, Sort));
            return "decals/" + file;
        }

        // The make with the most rows (the lowest Id among equals): the common decals every make shows.
        private static long TemplateMake(SqliteDatabase db)
        {
            object make = db.Scalar("SELECT MakeID FROM " + Sort + " GROUP BY MakeID ORDER BY count(*) DESC, MakeID LIMIT 1");
            if (make == null || make == DBNull.Value)
            {
                throw new InvalidOperationException(Sort + " has no rows to take the common decals from.");
            }
            return Convert.ToInt64(make, CultureInfo.InvariantCulture);
        }

        // The decal's name in Livery_Decals.str of every language: a key free in all of them (from FFFE
        // down), named IDS_DisplayName_<2000 + id> as the packs' (Ultima 514: 2514). Returns "_&<id>".
        private static string DisplayName(SqliteDatabase db, Dictionary<string, ForzaZip> zips, long id, string name)
        {
            Dictionary<string, StrFile> files = new Dictionary<string, StrFile>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, ForzaZip> pair in zips)
            {
                ForzaZip.Entry entry = pair.Value.Find(Table + ".str");
                if (entry != null)
                {
                    files[pair.Key] = new StrFile(pair.Value.Read(entry));
                }
            }
            if (files.Count == 0)
            {
                throw new InvalidOperationException("No string table has " + Table + ".str.");
            }
            object other = db.Scalar("SELECT DisplayName FROM " + Table + " WHERE DisplayName LIKE '\\_&%' ESCAPE '\\' LIMIT 1");
            uint value;
            if (!(other is string) || !uint.TryParse(((string) other).Substring(2), NumberStyles.None, CultureInfo.InvariantCulture, out value))
            {
                throw new InvalidOperationException(Table + " has no string id to model the new one on.");
            }
            int key = 0;
            for (int candidate = 0xFFFE; candidate > 0 && key == 0; candidate--)
            {
                bool used = false;
                foreach (StrFile file in files.Values)
                {
                    used = used || file.Contains(candidate);
                }
                if (!used)
                {
                    key = candidate;
                }
            }
            if (key == 0)
            {
                throw new InvalidOperationException(Table + ".str has no free key.");
            }
            long number = 2000 + id;
            string label = "IDS_DisplayName_" + number.ToString(CultureInfo.InvariantCulture);
            bool taken = true;
            while (taken)
            {
                taken = false;
                foreach (StrFile file in files.Values)
                {
                    foreach (StrFile.Entry entry in file.Entries)
                    {
                        taken = taken || entry.Name == label;
                    }
                }
                if (taken)
                {
                    label = "IDS_DisplayName_" + (++number).ToString(CultureInfo.InvariantCulture);
                }
            }
            foreach (KeyValuePair<string, StrFile> pair in files)
            {
                pair.Value.Set(key, name, label);
                ForzaZip zip = zips[pair.Key];
                zip.Replace(zip.Find(Table + ".str"), pair.Value.ToBytes());
            }
            return "_&" + ((value & 0xFFFF0000u) | (uint) key).ToString(CultureInfo.InvariantCulture);
        }
    }
}
