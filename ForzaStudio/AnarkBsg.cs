using System;
using System.Collections.Generic;
using System.IO;

namespace ForzaStudio
{
    // Anark digital dash scene graph (.bsg, "FGBkranA"): the base transform of every scene node. Big-endian.
    //   0x00 header (0x30 bytes: ..., u32 node count at 0x24, u32 node bytes at 0x2C), nodes (96 bytes each), rest (kept as is).
    // Node: u32 parent (node index, -1 for the root), u32 id (the .fbf record id), u32 type (top byte: 1 group,
    //   2 model, 3 camera, 4 light, 5 text, 8 layer), float position[3], rotation[3] (radians), scale[3], pivot[3],
    //   bounds min[3], max[3], opacity (0..1), u32 flags, u32 unknown.
    // The .bgf properties override these values (the graph only stores what a slide sets).
    public class AnarkBsg
    {
        public class Node
        {
            public int Parent;
            public uint Id;
            public uint Type;
            public float[] Values = new float[19];
            public uint Flags;
            public uint Unknown;

            public const int Position = 0, Rotation = 3, Scale = 6, Pivot = 9, BoundsMin = 12, BoundsMax = 15, Opacity = 18;

            public override string ToString()
            {
                return string.Format("{0} type {1:X}", this.Id, this.Type >> 24);
            }
        }

        private const int HeaderSize = 0x30;
        private const int NodeSize = 96;

        public byte[] Header;
        public List<Node> Nodes = new List<Node>();
        public byte[] Rest;

        public static AnarkBsg Read(byte[] data)
        {
            if (data.Length < HeaderSize || data[8] != 'F' || data[9] != 'G' || data[10] != 'B' || data[11] != 'k')
            {
                throw new InvalidDataException("not an Anark .bsg");
            }
            AnarkBsg bsg = new AnarkBsg();
            bsg.Header = new byte[HeaderSize];
            Buffer.BlockCopy(data, 0, bsg.Header, 0, HeaderSize);
            int count = (int) AnarkFbf.ReadU32(data, 0x24);
            int bytes = (int) AnarkFbf.ReadU32(data, 0x2C);
            if (bytes != count * NodeSize || HeaderSize + bytes > data.Length)
            {
                throw new InvalidDataException(string.Format("{0} nodes in {1} bytes", count, bytes));
            }
            int o = HeaderSize;
            for (int n = 0; n < count; n++, o += NodeSize)
            {
                Node node = new Node();
                node.Parent = (int) AnarkFbf.ReadU32(data, o);
                node.Id = AnarkFbf.ReadU32(data, o + 4);
                node.Type = AnarkFbf.ReadU32(data, o + 8);
                for (int k = 0; k < 19; k++)
                {
                    node.Values[k] = AnarkFbf.ReadFloat(data, o + 12 + k * 4);
                }
                node.Flags = AnarkFbf.ReadU32(data, o + 88);
                node.Unknown = AnarkFbf.ReadU32(data, o + 92);
                bsg.Nodes.Add(node);
            }
            bsg.Rest = new byte[data.Length - o];
            Buffer.BlockCopy(data, o, bsg.Rest, 0, bsg.Rest.Length);
            return bsg;
        }

        public byte[] Write()
        {
            byte[] data = new byte[HeaderSize + this.Nodes.Count * NodeSize + this.Rest.Length];
            Buffer.BlockCopy(this.Header, 0, data, 0, HeaderSize);
            WriteU32(data, 0x24, (uint) this.Nodes.Count);
            WriteU32(data, 0x2C, (uint) (this.Nodes.Count * NodeSize));
            int o = HeaderSize;
            foreach (Node node in this.Nodes)
            {
                WriteU32(data, o, (uint) node.Parent);
                WriteU32(data, o + 4, node.Id);
                WriteU32(data, o + 8, node.Type);
                for (int k = 0; k < 19; k++)
                {
                    AnarkFbf.WriteFloat(data, o + 12 + k * 4, node.Values[k]);
                }
                WriteU32(data, o + 88, node.Flags);
                WriteU32(data, o + 92, node.Unknown);
                o += NodeSize;
            }
            Buffer.BlockCopy(this.Rest, 0, data, o, this.Rest.Length);
            return data;
        }

        public Node Find(uint id)
        {
            foreach (Node node in this.Nodes)
            {
                if (node.Id == id)
                {
                    return node;
                }
            }
            return null;
        }

        private static void WriteU32(byte[] data, int o, uint v)
        {
            data[o] = (byte) (v >> 24);
            data[o + 1] = (byte) (v >> 16);
            data[o + 2] = (byte) (v >> 8);
            data[o + 3] = (byte) v;
        }
    }
}
