using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ForzaStudio
{
    // Anark digital dash graph (.bgf, "AnarkBGF"), the companion of the .fbf. Big-endian.
    //   0x00 header (0x88 bytes, kept as is), u32 string bytes, strings (u32 length + text),
    //   u32 graph bytes, elements, then slides and animations (kept as is).
    // Element: u32 hash, u32 parent (element index, -1 for the root), u32 id (the .fbf record id), u8 type, u8 flag,
    //   [type 7 (component): u8 + u32 duration], u32 count, count x (u32 property hash, u32 value).
    // A property hash is the sdbm hash ((h + c) * 65599) of the name in its low 27 bits; the top 5 bits give the
    // value type (see ValueKind), +16 for behaviour parameters. A property that is missing has its default value.
    // Verified: the 431 .bgf of FM4, FH2 and FH1 are read and written back byte for byte.
    public class AnarkBgf
    {
        public enum ElementType
        {
            Node = 1,          // scene, layer, group, model
            Camera = 2,
            Light = 3,
            Text = 4,
            Material = 5,
            Image = 6,
            Component = 7,
            Behaviour = 8,
        }

        public enum ValueKind
        {
            Int = 1,
            Float = 3,
            Bool = 4,
            String = 5,        // index into Strings
        }

        public class Property
        {
            public uint Hash;
            public uint Value;

            public uint NameHash { get { return this.Hash & 0x07FFFFFF; } }
            public ValueKind Kind { get { return (ValueKind) ((this.Hash >> 27) & 15); } }
            public bool IsBehaviourParameter { get { return (this.Hash & 0x80000000) != 0; } }
            public string Name { get { return PropertyName(this.Hash); } }

            public float Float
            {
                get { return BitConverter.ToSingle(BitConverter.GetBytes(this.Value), 0); }
                set { this.Value = BitConverter.ToUInt32(BitConverter.GetBytes(value), 0); }
            }
        }

        public class Element
        {
            public uint Hash;
            public int Parent;
            public uint Id;
            public ElementType Type;
            public byte Flag;
            public byte[] ComponentPrefix;   // type 7 only: u8 + u32 duration
            public List<Property> Properties = new List<Property>();

            public Property Find(string name)
            {
                uint hash = HashName(name);
                foreach (Property p in this.Properties)
                {
                    if (p.NameHash == hash)
                    {
                        return p;
                    }
                }
                return null;
            }

            public float GetFloat(string name, float fallback)
            {
                Property p = this.Find(name);
                return (p != null && p.Kind == ValueKind.Float) ? p.Float : fallback;
            }

            public int GetInt(string name, int fallback)
            {
                Property p = this.Find(name);
                return (p != null) ? (int) p.Value : fallback;
            }

            public Element Copy()
            {
                Element e = (Element) this.MemberwiseClone();
                if (this.ComponentPrefix != null)
                {
                    e.ComponentPrefix = (byte[]) this.ComponentPrefix.Clone();
                }
                e.Properties = new List<Property>();
                foreach (Property p in this.Properties)
                {
                    Property c = new Property();
                    c.Hash = p.Hash;
                    c.Value = p.Value;
                    e.Properties.Add(c);
                }
                return e;
            }

            public override string ToString()
            {
                return this.Type + " " + this.Id;
            }
        }

        // The counts the header keeps (checked on the 233 dashes of FM4): nodes (elements that are not components) at
        // 0x24 and 0x70, components 0x28 / 0x74, properties 0x2C / 0x78, behaviours 0x30 / 0x7C, strings 0x3C.
        // Call it after elements, properties or strings were added.
        public void UpdateHeader()
        {
            int components = 0, behaviours = 0, properties = 0;
            foreach (Element e in this.Elements)
            {
                properties += e.Properties.Count;
                if (e.Type == ElementType.Component)
                {
                    components++;
                }
                else if (e.Type == ElementType.Behaviour)
                {
                    behaviours++;
                }
            }
            int nodes = this.Elements.Count - components;
            int[][] fields = new int[][]
            {
                new int[] { 0x24, nodes }, new int[] { 0x70, nodes }, new int[] { 0x28, components }, new int[] { 0x74, components },
                new int[] { 0x2C, properties }, new int[] { 0x78, properties }, new int[] { 0x30, behaviours }, new int[] { 0x7C, behaviours },
                new int[] { 0x3C, this.Strings.Count },
            };
            foreach (int[] f in fields)
            {
                this.Header[f[0]] = (byte) (f[1] >> 24);
                this.Header[f[0] + 1] = (byte) (f[1] >> 16);
                this.Header[f[0] + 2] = (byte) (f[1] >> 8);
                this.Header[f[0] + 3] = (byte) f[1];
            }
        }

        private const int HeaderSize = 0x88;
        private static readonly Encoding Latin1 = Encoding.GetEncoding(28591);

        public byte[] Header;
        public List<string> Strings = new List<string>();
        public List<Element> Elements = new List<Element>();
        public byte[] Rest;                  // slides and animations

        public static uint HashName(string name)
        {
            uint h = 0;
            foreach (char c in name)
            {
                h = (h + c) * 65599;
            }
            return h & 0x07FFFFFF;
        }

        // Names recognised so far (UI Composer / Qt 3D Studio property names, which come from Anark Gameface).
        private static readonly string[] KnownNames = new string[]
        {
            "position.x", "position.y", "position.z", "rotation.x", "rotation.y", "rotation.z", "scale.x", "scale.y", "scale.z",
            "pivot.x", "pivot.y", "pivot.z", "opacity", "rotationorder", "orientation", "endtime", "starttime", "eyeball",
            "clipnear", "clipfar", "fov", "orthographic",
            "diffuse.r", "diffuse.g", "diffuse.b", "diffuse.a",
            "textstring", "font", "size", "textcolor.r", "textcolor.g", "textcolor.b", "textcolor.a",
            "horzalign", "vertalign", "leading", "tracking", "wordwrap",
            "positionu", "positionv", "scaleu", "scalev", "rotationuv", "tilingmodehorz", "tilingmodevert",
        };

        private static Dictionary<uint, string> names;

        public static string PropertyName(uint hash)
        {
            if (names == null)
            {
                Dictionary<uint, string> map = new Dictionary<uint, string>();
                foreach (string name in KnownNames)
                {
                    map[HashName(name)] = name;
                }
                names = map;
            }
            string found;
            return names.TryGetValue(hash & 0x07FFFFFF, out found) ? found : null;
        }

        public static AnarkBgf Read(byte[] data)
        {
            if (data.Length < HeaderSize + 4 || Latin1.GetString(data, 6, 8) != "AnarkBGF")
            {
                throw new InvalidDataException("not an Anark .bgf");
            }
            AnarkBgf bgf = new AnarkBgf();
            int o = 0;
            bgf.Header = Bytes(data, ref o, HeaderSize);
            int end = U32(data, ref o);
            end += o;
            while (o < end)
            {
                int n = U32(data, ref o);
                bgf.Strings.Add(Latin1.GetString(Bytes(data, ref o, n)));
            }
            if (o != end)
            {
                throw new InvalidDataException("string section overrun");
            }
            int graphEnd = U32(data, ref o);
            graphEnd += o;
            if (graphEnd > data.Length)
            {
                throw new InvalidDataException("graph section past the end of the file");
            }
            while (o < graphEnd)
            {
                Element e = new Element();
                e.Hash = (uint) U32(data, ref o);
                e.Parent = U32(data, ref o);
                e.Id = (uint) U32(data, ref o);
                byte[] tf = Bytes(data, ref o, 2);
                e.Type = (ElementType) tf[0];
                e.Flag = tf[1];
                if (e.Type == ElementType.Component)
                {
                    e.ComponentPrefix = Bytes(data, ref o, 5);
                }
                int count = U32(data, ref o);
                if (count < 0 || o + count * 8 > graphEnd)
                {
                    throw new InvalidDataException(string.Format("element {0}: {1} properties past the graph section", bgf.Elements.Count, count));
                }
                for (int k = 0; k < count; k++)
                {
                    Property p = new Property();
                    p.Hash = (uint) U32(data, ref o);
                    p.Value = (uint) U32(data, ref o);
                    e.Properties.Add(p);
                }
                bgf.Elements.Add(e);
            }
            if (o != graphEnd)
            {
                throw new InvalidDataException("graph section overrun");
            }
            bgf.Rest = Bytes(data, ref o, data.Length - o);
            return bgf;
        }

        public byte[] Write()
        {
            MemoryStream strings = new MemoryStream();
            foreach (string s in this.Strings)
            {
                byte[] b = Latin1.GetBytes(s);
                WriteU32(strings, (uint) b.Length);
                strings.Write(b, 0, b.Length);
            }
            MemoryStream graph = new MemoryStream();
            foreach (Element e in this.Elements)
            {
                WriteU32(graph, e.Hash);
                WriteU32(graph, (uint) e.Parent);
                WriteU32(graph, e.Id);
                graph.WriteByte((byte) e.Type);
                graph.WriteByte(e.Flag);
                if (e.Type == ElementType.Component)
                {
                    graph.Write(e.ComponentPrefix, 0, e.ComponentPrefix.Length);
                }
                WriteU32(graph, (uint) e.Properties.Count);
                foreach (Property p in e.Properties)
                {
                    WriteU32(graph, p.Hash);
                    WriteU32(graph, p.Value);
                }
            }
            MemoryStream s2 = new MemoryStream();
            s2.Write(this.Header, 0, this.Header.Length);
            WriteU32(s2, (uint) strings.Length);
            strings.WriteTo(s2);
            WriteU32(s2, (uint) graph.Length);
            graph.WriteTo(s2);
            s2.Write(this.Rest, 0, this.Rest.Length);
            return s2.ToArray();
        }

        public Element FindById(uint id)
        {
            foreach (Element e in this.Elements)
            {
                if (e.Id == id && e.Type != ElementType.Behaviour)
                {
                    return e;
                }
            }
            return null;
        }

        public string GetString(Property p)
        {
            return (p != null && p.Value < this.Strings.Count) ? this.Strings[(int) p.Value] : null;
        }

        private static int U32(byte[] data, ref int o)
        {
            if (o + 4 > data.Length)
            {
                throw new InvalidDataException(string.Format("unexpected end of file at 0x{0:X}", o));
            }
            int v = (data[o] << 24) | (data[o + 1] << 16) | (data[o + 2] << 8) | data[o + 3];
            o += 4;
            return v;
        }

        private static byte[] Bytes(byte[] data, ref int o, int n)
        {
            if (n < 0 || o + n > data.Length)
            {
                throw new InvalidDataException(string.Format("unexpected end of file at 0x{0:X} (+{1})", o, n));
            }
            byte[] b = new byte[n];
            Buffer.BlockCopy(data, o, b, 0, n);
            o += n;
            return b;
        }

        private static void WriteU32(Stream s, uint v)
        {
            s.WriteByte((byte) (v >> 24));
            s.WriteByte((byte) (v >> 16));
            s.WriteByte((byte) (v >> 8));
            s.WriteByte((byte) v);
        }
    }
}
