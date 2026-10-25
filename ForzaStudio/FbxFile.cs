namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Text;

    // One record of an FBX file: a name, typed properties and child records.
    // Property values are bool, short, int, long, float, double, string (UTF-8), byte[] (raw)
    // or the arrays bool[], int[], long[], float[], double[].
    public class FbxNode
    {
        public string Name;
        public List<object> Props = new List<object>();
        public List<FbxNode> Children = new List<FbxNode>();

        public FbxNode(string name)
        {
            this.Name = name;
        }

        public FbxNode Add(FbxNode child)
        {
            this.Children.Add(child);
            return child;
        }

        public FbxNode Add(string name, params object[] props)
        {
            FbxNode child = new FbxNode(name);
            child.Props.AddRange(props);
            this.Children.Add(child);
            return child;
        }

        public FbxNode Find(string name)
        {
            foreach (FbxNode child in this.Children)
            {
                if (child.Name == name)
                {
                    return child;
                }
            }
            return null;
        }

        public IEnumerable<FbxNode> FindAll(string name)
        {
            foreach (FbxNode child in this.Children)
            {
                if (child.Name == name)
                {
                    yield return child;
                }
            }
        }

        public object Prop(int index)
        {
            return (index < this.Props.Count) ? this.Props[index] : null;
        }

        // Numeric property as a double (any integer / float type), or the fallback.
        public double Number(int index, double fallback)
        {
            object value = this.Prop(index);
            if (value is double) { return (double) value; }
            if (value is float) { return (float) value; }
            if (value is int) { return (int) value; }
            if (value is long) { return (long) value; }
            if (value is short) { return (short) value; }
            if (value is bool) { return ((bool) value) ? 1 : 0; }
            return fallback;
        }

        public long Long(int index)
        {
            return (long) this.Number(index, 0);
        }

        public string Text(int index)
        {
            return this.Prop(index) as string;
        }
    }

    // FBX binary files (version 7.x): reading, and writing version 7.4 as Blender and 3ds Max read it.
    // Arrays are zlib-compressed on write when large. ASCII FBX is not read: export "Binary" from the editor.
    public static class FbxFile
    {
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("Kaydara FBX Binary  \0\x1a\0");

        public static bool IsBinary(byte[] data)
        {
            if (data.Length < Magic.Length + 4)
            {
                return false;
            }
            for (int i = 0; i < Magic.Length; i++)
            {
                if (data[i] != Magic[i])
                {
                    return false;
                }
            }
            return true;
        }

        #region Reading

        // The top-level records (FBXHeaderExtension, GlobalSettings, Objects, Connections...) in a fake root node.
        public static FbxNode Read(byte[] data)
        {
            if (!IsBinary(data))
            {
                throw new InvalidDataException("Not a binary FBX file (ASCII FBX is not supported: save it as Binary).");
            }
            int version = BitConverter.ToInt32(data, Magic.Length);
            bool wide = version >= 7500;
            int[] position = new int[] { Magic.Length + 4 };
            FbxNode root = new FbxNode("");
            while (true)
            {
                FbxNode node = ReadNode(data, position, wide);
                if (node == null)
                {
                    break;
                }
                root.Children.Add(node);
            }
            return root;
        }

        public static int VersionOf(byte[] data)
        {
            return BitConverter.ToInt32(data, Magic.Length);
        }

        private static FbxNode ReadNode(byte[] data, int[] position, bool wide)
        {
            int p = position[0];
            long end;
            long count;
            long listLength;
            if (wide)
            {
                end = BitConverter.ToInt64(data, p);
                count = BitConverter.ToInt64(data, p + 8);
                listLength = BitConverter.ToInt64(data, p + 16);
                p += 24;
            }
            else
            {
                end = BitConverter.ToUInt32(data, p);
                count = BitConverter.ToUInt32(data, p + 4);
                listLength = BitConverter.ToUInt32(data, p + 8);
                p += 12;
            }
            int nameLength = data[p++];
            if (end == 0)
            {
                position[0] = p + nameLength;
                return null;   // the null record that closes a list
            }
            FbxNode node = new FbxNode(Encoding.UTF8.GetString(data, p, nameLength));
            p += nameLength;
            for (long i = 0; i < count; i++)
            {
                node.Props.Add(ReadProperty(data, ref p));
            }
            // Children until the record's end.
            position[0] = p;
            while (position[0] < end)
            {
                FbxNode child = ReadNode(data, position, wide);
                if (child == null)
                {
                    break;
                }
                node.Children.Add(child);
            }
            position[0] = (int) end;
            return node;
        }

        private static object ReadProperty(byte[] data, ref int p)
        {
            char type = (char) data[p++];
            switch (type)
            {
                case 'C': return data[p++] != 0;
                case 'Y': { short v = BitConverter.ToInt16(data, p); p += 2; return v; }
                case 'I': { int v = BitConverter.ToInt32(data, p); p += 4; return v; }
                case 'L': { long v = BitConverter.ToInt64(data, p); p += 8; return v; }
                case 'F': { float v = BitConverter.ToSingle(data, p); p += 4; return v; }
                case 'D': { double v = BitConverter.ToDouble(data, p); p += 8; return v; }
                case 'S':
                case 'R':
                {
                    int length = BitConverter.ToInt32(data, p);
                    p += 4;
                    byte[] raw = new byte[length];
                    Buffer.BlockCopy(data, p, raw, 0, length);
                    p += length;
                    return (type == 'S') ? (object) Encoding.UTF8.GetString(raw) : raw;
                }
                case 'b':
                case 'i':
                case 'l':
                case 'f':
                case 'd':
                {
                    int length = BitConverter.ToInt32(data, p);
                    int encoding = BitConverter.ToInt32(data, p + 4);
                    int stored = BitConverter.ToInt32(data, p + 8);
                    p += 12;
                    byte[] bytes = Inflate(data, p, stored, encoding);
                    p += stored;
                    return Convert(type, bytes, length);
                }
            }
            throw new InvalidDataException("Unknown FBX property type '" + type + "'.");
        }

        private static byte[] Inflate(byte[] data, int offset, int stored, int encoding)
        {
            if (encoding == 0)
            {
                byte[] plain = new byte[stored];
                Buffer.BlockCopy(data, offset, plain, 0, stored);
                return plain;
            }
            // zlib: two header bytes, deflate data, adler32 at the end.
            using (MemoryStream input = new MemoryStream(data, offset + 2, stored - 2))
            using (DeflateStream inflate = new DeflateStream(input, CompressionMode.Decompress))
            using (MemoryStream output = new MemoryStream())
            {
                byte[] buffer = new byte[65536];
                int read;
                while ((read = inflate.Read(buffer, 0, buffer.Length)) > 0)
                {
                    output.Write(buffer, 0, read);
                }
                byte[] result = output.ToArray();
                // zlib (the FBX SDK's) checks the trailing adler32; so do we.
                if (StrictChecksum && stored >= 6)
                {
                    uint expected = (uint) ((data[offset + stored - 4] << 24) | (data[offset + stored - 3] << 16) | (data[offset + stored - 2] << 8) | data[offset + stored - 1]);
                    if (Adler32(result) != expected)
                    {
                        throw new InvalidDataException("FBX array: adler32 checksum mismatch.");
                    }
                }
                return result;
            }
        }

        private static object Convert(char type, byte[] bytes, int length)
        {
            switch (type)
            {
                case 'b':
                {
                    bool[] result = new bool[length];
                    for (int i = 0; i < length; i++) { result[i] = bytes[i] != 0; }
                    return result;
                }
                case 'i':
                {
                    int[] result = new int[length];
                    Buffer.BlockCopy(bytes, 0, result, 0, length * 4);
                    return result;
                }
                case 'l':
                {
                    long[] result = new long[length];
                    Buffer.BlockCopy(bytes, 0, result, 0, length * 8);
                    return result;
                }
                case 'f':
                {
                    float[] result = new float[length];
                    Buffer.BlockCopy(bytes, 0, result, 0, length * 4);
                    return result;
                }
                default:
                {
                    double[] result = new double[length];
                    Buffer.BlockCopy(bytes, 0, result, 0, length * 8);
                    return result;
                }
            }
        }

        #endregion

        #region Writing

        private static readonly byte[] FooterId = new byte[] { 0xfa, 0xbc, 0xab, 0x09, 0xd0, 0xc8, 0xd4, 0x66, 0xb1, 0x76, 0xfb, 0x83, 0x1c, 0xf7, 0x26, 0x7e };
        private static readonly byte[] FooterMagic = new byte[] { 0xf8, 0x5a, 0x8c, 0x6a, 0xde, 0xf5, 0xd9, 0x7e, 0xec, 0xe9, 0x0c, 0xe3, 0x75, 0x8f, 0x29, 0x0b };
        private const int Version = 7400;

        // 'top' holds the top-level records; written as FBX 7.4 binary with the standard footer.
        public static byte[] Write(FbxNode top)
        {
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(Magic);
                writer.Write(Version);
                foreach (FbxNode node in top.Children)
                {
                    WriteNode(writer, node);
                }
                writer.Write(new byte[13]);   // null record
                writer.Write(FooterId);
                writer.Write(new byte[4]);
                int pad = (int) (16 - (stream.Position % 16));
                writer.Write(new byte[pad]);
                writer.Write(Version);
                writer.Write(new byte[120]);
                writer.Write(FooterMagic);
                writer.Flush();
                return stream.ToArray();
            }
        }

        private static void WriteNode(BinaryWriter writer, FbxNode node)
        {
            long start = writer.BaseStream.Position;
            writer.Write(0u);   // end offset, patched below
            writer.Write((uint) node.Props.Count);
            writer.Write(0u);   // property list length, patched below
            byte[] name = Encoding.UTF8.GetBytes(node.Name);
            writer.Write((byte) name.Length);
            writer.Write(name);
            long propertiesStart = writer.BaseStream.Position;
            foreach (object value in node.Props)
            {
                WriteProperty(writer, value);
            }
            long propertiesEnd = writer.BaseStream.Position;
            if (node.Children.Count > 0)
            {
                foreach (FbxNode child in node.Children)
                {
                    WriteNode(writer, child);
                }
                writer.Write(new byte[13]);
            }
            long end = writer.BaseStream.Position;
            writer.BaseStream.Position = start;
            writer.Write((uint) end);
            writer.Write((uint) node.Props.Count);
            writer.Write((uint) (propertiesEnd - propertiesStart));
            writer.BaseStream.Position = end;
        }

        private static void WriteProperty(BinaryWriter writer, object value)
        {
            if (value is bool) { writer.Write((byte) 'C'); writer.Write((byte) (((bool) value) ? 1 : 0)); }
            else if (value is short) { writer.Write((byte) 'Y'); writer.Write((short) value); }
            else if (value is int) { writer.Write((byte) 'I'); writer.Write((int) value); }
            else if (value is long) { writer.Write((byte) 'L'); writer.Write((long) value); }
            else if (value is float) { writer.Write((byte) 'F'); writer.Write((float) value); }
            else if (value is double) { writer.Write((byte) 'D'); writer.Write((double) value); }
            else if (value is string)
            {
                byte[] text = Encoding.UTF8.GetBytes((string) value);
                writer.Write((byte) 'S');
                writer.Write(text.Length);
                writer.Write(text);
            }
            else if (value is byte[])
            {
                byte[] raw = (byte[]) value;
                writer.Write((byte) 'R');
                writer.Write(raw.Length);
                writer.Write(raw);
            }
            else if (value is int[]) { WriteArray(writer, 'i', ((int[]) value).Length, 4, value); }
            else if (value is long[]) { WriteArray(writer, 'l', ((long[]) value).Length, 8, value); }
            else if (value is float[]) { WriteArray(writer, 'f', ((float[]) value).Length, 4, value); }
            else if (value is double[]) { WriteArray(writer, 'd', ((double[]) value).Length, 8, value); }
            else
            {
                throw new ArgumentException("Unsupported FBX property " + ((value == null) ? "null" : value.GetType().Name));
            }
        }

        private static void WriteArray(BinaryWriter writer, char type, int length, int size, object array)
        {
            byte[] bytes = new byte[length * size];
            Buffer.BlockCopy((Array) array, 0, bytes, 0, bytes.Length);
            writer.Write((byte) type);
            writer.Write(length);
            if (bytes.Length < 128)
            {
                writer.Write(0);
                writer.Write(bytes.Length);
                writer.Write(bytes);
                return;
            }
            byte[] packed = Deflate(bytes);
            writer.Write(1);
            writer.Write(packed.Length);
            writer.Write(packed);
        }

        // Checked on read; files from other programs are zlib streams too, so it stays on.
        public static bool StrictChecksum = true;

        public static uint Adler32(byte[] bytes)
        {
            uint a = 1;
            uint b = 0;
            foreach (byte value in bytes)
            {
                a = (a + value) % 65521;
                b = (b + a) % 65521;
            }
            return (b << 16) | a;
        }

        // zlib stream: 78 9C, deflate data, big-endian adler32.
        private static byte[] Deflate(byte[] bytes)
        {
            using (MemoryStream output = new MemoryStream())
            {
                output.WriteByte(0x78);
                output.WriteByte(0x9c);
                using (DeflateStream deflate = new DeflateStream(output, CompressionMode.Compress, true))
                {
                    deflate.Write(bytes, 0, bytes.Length);
                }
                uint adler = Adler32(bytes);
                output.WriteByte((byte) (adler >> 24));
                output.WriteByte((byte) (adler >> 16));
                output.WriteByte((byte) (adler >> 8));
                output.WriteByte((byte) adler);
                return output.ToArray();
            }
        }

        #endregion
    }
}
