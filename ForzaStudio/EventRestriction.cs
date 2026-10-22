namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;

    // EventRestrictions.Value (ROADMAP 1.5): the cars an event accepts, as a hex string (all 340 values of
    // FM4 decode with no byte left over, docs/EVENT_RESTRICTIONS.md).
    //   5 bytes  a flag field, big endian: the first four bytes then one byte "S"; bit by bit it says which
    //            filters follow
    //   then     the value of every set filter, in the fixed order of Fields below (one byte, two or four)
    // Filters: the car's year (min / max), kind (production / tuned / race), body family, region, car,
    // model family (the "Club" events), make, country, cylinders, engine layout, drivetrain, doors,
    // aspiration, class. Filters in one value are all required (an AND); a value of 0000000004 has none.
    // Not decoded: bit 0x04 of S (set on 338 of 340 values, no byte) and bit 0x01 of S (one byte, one value).
    public static class EventRestriction
    {
        public sealed class Field
        {
            public readonly string Name;        // what it restricts
            public readonly int Size;           // bytes in the value
            public readonly long Bit;           // its bit in the 40-bit flag field
            public readonly string Table;       // the table the value is an id of, or null
            public readonly string Column;      // that table's key column
            public long Value;

            public Field(string name, int size, long bit, string table, string column)
            {
                this.Name = name;
                this.Size = size;
                this.Bit = bit;
                this.Table = table;
                this.Column = column;
            }
        }

        // The filters in the order their values are stored; the bit is (first four bytes << 8) | S.
        private static readonly Field[] Fields = new Field[]
        {
            new Field("Class", 1, 0x1000000L << 8, "CarClasses", "Id"),
            new Field("Year from", 4, 0x10000L << 8, null, null),
            new Field("Year to", 4, 0x20000L << 8, null, null),
            new Field("Kind", 1, 0x200000L << 8, "List_CarType", "Id"),
            new Field("Body", 1, 0x80000L << 8, "List_FamilyBody", "Id"),
            new Field("Region", 1, 0x800000L << 8, "List_Region", "Id"),
            new Field("Car", 2, 0x8000L << 8, "Data_Car", "Id"),
            new Field("Model family", 2, 0x2000L << 8, "List_FamilyModel", "Id"),
            new Field("Make", 2, 0x800L << 8, "List_CarMake", "ID"),
            new Field("Country", 1, 0x200L << 8, "List_Country", "CountryID"),
            new Field("Cylinders", 1, 0x80L << 8, "List_Cylinders", "CylinderID"),
            new Field("Engine layout", 1, 0x8L << 8, "List_EngineConfig", "ConfigID"),
            new Field("Drivetrain", 1, 0x2L << 8, "List_DriveType", "ID"),
            new Field("Doors", 1, 0x10L, null, null),
            new Field("Aspiration", 1, 0x02L, "List_Aspiration", "AspirationID"),
            new Field("Flag 0x01", 1, 0x01L, null, null)
        };

        private const long Marker = 0x04L;      // set on almost every value, carries no byte

        // The filters of a value, with their numbers. Throws FormatException when the value does not fit the
        // format (a bit not known, or bytes left over or missing).
        public static List<Field> Decode(string value)
        {
            List<Field> result = new List<Field>();
            if (value == null || value.Length < 10 || value.Length % 2 != 0)
            {
                throw new FormatException("an EventRestrictions value is at least 5 bytes of hex");
            }
            byte[] bytes = new byte[value.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = byte.Parse(value.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }
            long flags = 0;
            for (int i = 0; i < 5; i++)
            {
                flags = (flags << 8) | bytes[i];
            }
            long known = Marker;
            int position = 5;
            foreach (Field template in Fields)
            {
                known |= template.Bit;
                if ((flags & template.Bit) == 0)
                {
                    continue;
                }
                if (position + template.Size > bytes.Length)
                {
                    throw new FormatException("the value ends before the " + template.Name + " filter");
                }
                Field field = new Field(template.Name, template.Size, template.Bit, template.Table, template.Column);
                for (int i = 0; i < template.Size; i++)
                {
                    field.Value = (field.Value << 8) | bytes[position++];
                }
                result.Add(field);
            }
            if ((flags & ~known) != 0)
            {
                throw new FormatException("flag bits not known: 0x" + (flags & ~known).ToString("X", CultureInfo.InvariantCulture));
            }
            if (position != bytes.Length)
            {
                throw new FormatException((bytes.Length - position) + " bytes left after the filters");
            }
            return result;
        }

        // "Make 13 (Ferrari), Year to 1974": the ids resolved in the database when it has the table; names, when
        // the string table is given, in its language. A value that does not decode comes back as "?" + the hex.
        public static string Describe(string value, SqliteDatabase db, StringTable names)
        {
            List<Field> fields;
            try
            {
                fields = Decode(value);
            }
            catch (FormatException)
            {
                return "? " + value;
            }
            if (fields.Count == 0)
            {
                return "(no restriction)";
            }
            StringBuilder text = new StringBuilder();
            foreach (Field field in fields)
            {
                if (text.Length > 0)
                {
                    text.Append(", ");
                }
                text.Append(field.Name).Append(' ').Append(field.Value.ToString(CultureInfo.InvariantCulture));
                string label = Label(field, db, names);
                if (label != null)
                {
                    text.Append(" (").Append(label).Append(')');
                }
            }
            return text.ToString();
        }

        private static string Label(Field field, SqliteDatabase db, StringTable names)
        {
            if (db == null || field.Table == null || !DbIds.HasTable(db, field.Table))
            {
                return null;
            }
            try
            {
                if (field.Table == "Data_Car")
                {
                    return db.Scalar("SELECT MediaName FROM Data_Car WHERE Id = ?", field.Value) as string;
                }
                SqliteDatabase.Result row = db.Query("SELECT * FROM " + SqliteDatabase.Quote(field.Table) + " WHERE " + SqliteDatabase.Quote(field.Column) + " = ?", field.Value);
                if (row.Rows.Count == 0)
                {
                    return "no such row";
                }
                for (int i = 0; i < row.Columns.Length; i++)
                {
                    string text = row.Rows[0][i] as string;
                    if (text != null && text.StartsWith("_&", StringComparison.Ordinal))
                    {
                        string shown = (names == null) ? null : names.Lookup(field.Table, row.Columns[i], text);
                        if (!string.IsNullOrEmpty(shown))
                        {
                            return shown;
                        }
                    }
                }
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
