namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;

    // Structural model of an FM4 .carbin (type 2: main, lod0 and cockpit files) for edits that change
    // sizes: moving or merging subsections between sections and removing sections. Every block the
    // tool does not understand is kept as raw bytes, so an untouched model writes back byte for byte.
    //
    // Header draw lists: after the fixed header come 2 x N lists (N = LOD count of the file, two
    // render passes). Each entry is (section index << 16) | subsection index, so every structural
    // change must renumber them.
    public class CarbinModel
    {
        public class VertexBuffer
        {
            public int Count;
            public int Stride;
            public byte[] Extra = new byte[0];   // 8 bytes when the stride is 0x1c (not used by FM4 cars)
            public byte[] Data = new byte[0];
        }

        public class Table
        {
            public int Count;
            public int Stride;
            public byte[] Data = new byte[0];
        }

        public class SubSection
        {
            public byte[] Pre5;
            public float[] Uv = new float[8];    // offset/scale for U, V, U2, V2
            public byte[] Fixed36;
            public string Name;
            public uint Lod;
            public uint IndexType;
            public byte[] Fixed56;
            public uint Three;
            public int IndexSize;
            public List<int> Indices = new List<int>();  // raw values, restart markers included
            public byte[] Tail;

            public SubSection Copy()
            {
                SubSection copy = (SubSection) this.MemberwiseClone();
                copy.Pre5 = (byte[]) this.Pre5.Clone();
                copy.Uv = (float[]) this.Uv.Clone();
                copy.Fixed36 = (byte[]) this.Fixed36.Clone();
                copy.Fixed56 = (byte[]) this.Fixed56.Clone();
                copy.Tail = (byte[]) this.Tail.Clone();
                copy.Indices = new List<int>(this.Indices);
                return copy;
            }
        }

        public class Section
        {
            public byte[] Marker = new byte[0];
            public float[] Bounds = new float[9];        // origin, min, max (min/max relative to origin)
            public byte[] Unk1c;
            public uint ACount;
            public byte[] A;
            public byte[] AfterA;
            public uint BCount;
            public byte[] B;
            public byte[] Pre;
            public uint VbTest1;
            public uint VbTest2;
            public string Name;
            public byte[] PostName;
            public VertexBuffer Lod = new VertexBuffer();
            public byte[] AfterLod;
            public List<SubSection> SubSections = new List<SubSection>();
            public byte[] AfterSubs;
            public VertexBuffer Lod0 = new VertexBuffer();
            public byte[] Post9;
            public Table DamageLod = new Table();   // 4 bytes per Lod vertex, only on deformable sections
            public byte[] Mid;
            public Table DamageLod0 = new Table();  // 4 bytes per Lod0 vertex, only on deformable sections

            public bool IsDeformable
            {
                get { return this.Post9[4] != 0; }
            }
        }

        public uint Type;
        public byte[] Head;                         // everything before the draw lists
        public List<uint[]> DrawLists = new List<uint[]>();
        public byte[] BeforeSections;
        public List<Section> Sections = new List<Section>();
        public byte[] Trailer;

        private byte[] data;
        private int position;

        public CarbinModel(byte[] file)
        {
            this.data = file;
            this.Parse();
            this.data = null;
        }

        public Section Find(string name)
        {
            foreach (Section section in this.Sections)
            {
                if (section.Name == name)
                {
                    return section;
                }
            }
            return null;
        }

        #region Reading

        private uint U32()
        {
            uint v = (uint) ((this.data[this.position] << 24) | (this.data[this.position + 1] << 16) | (this.data[this.position + 2] << 8) | this.data[this.position + 3]);
            this.position += 4;
            return v;
        }

        private float F32()
        {
            byte[] b = { this.data[this.position + 3], this.data[this.position + 2], this.data[this.position + 1], this.data[this.position] };
            this.position += 4;
            return BitConverter.ToSingle(b, 0);
        }

        private byte[] Bytes(int length)
        {
            byte[] result = new byte[length];
            Buffer.BlockCopy(this.data, this.position, result, 0, length);
            this.position += length;
            return result;
        }

        private VertexBuffer ReadBuffer()
        {
            VertexBuffer buffer = new VertexBuffer();
            buffer.Count = (int) this.U32();
            buffer.Stride = (int) this.U32();
            if (buffer.Stride == 0x1c)
            {
                buffer.Extra = this.Bytes(8);
            }
            buffer.Data = this.Bytes(buffer.Count * buffer.Stride);
            return buffer;
        }

        private Table ReadTable()
        {
            Table table = new Table();
            table.Count = (int) this.U32();
            table.Stride = (int) this.U32();
            table.Data = this.Bytes(table.Count * table.Stride);
            return table;
        }

        private void Parse()
        {
            this.position = 0;
            this.Type = this.U32();
            if (this.Type != 2)
            {
                throw new NotSupportedException(string.Format("Structural editing of .carbin type {0} is not supported (only FM4 type 2).", this.Type));
            }
            this.position += 0x15c;
            uint records = this.U32();
            this.position += 4 + ((int) records * 140) + 0x340;
            int headLength = this.position;
            this.position = 0;
            this.Head = this.Bytes(headLength);
            uint lists = this.U32() * 2;
            for (int i = 0; i < lists; i++)
            {
                uint[] list = new uint[this.U32()];
                for (int k = 0; k < list.Length; k++)
                {
                    list[k] = this.U32();
                }
                this.DrawLists.Add(list);
            }
            this.BeforeSections = this.Bytes(4);
            uint count = this.U32();
            for (int s = 0; s < count; s++)
            {
                this.Sections.Add(this.ReadSection());
            }
            this.Trailer = this.Bytes(this.data.Length - this.position);
        }

        private Section ReadSection()
        {
            Section section = new Section();
            int start = this.position;
            uint marker = this.U32();
            if (marker == 0x2c9 || marker == 2 || marker == 5)
            {
                this.position = start;
                section.Marker = this.Bytes(4);
            }
            else
            {
                this.position = start;
            }
            for (int i = 0; i < 9; i++)
            {
                section.Bounds[i] = this.F32();
            }
            section.Unk1c = this.Bytes(0x1c);
            section.ACount = this.U32();
            section.A = this.Bytes((int) section.ACount * 16);
            section.AfterA = this.Bytes(4);
            section.BCount = this.U32();
            section.B = this.Bytes((int) section.BCount * 2);
            section.Pre = this.Bytes(4);
            section.VbTest1 = this.U32();
            section.VbTest2 = this.U32();
            int nameLength = this.data[this.position++];
            section.Name = Encoding.ASCII.GetString(this.data, this.position, nameLength);
            this.position += nameLength;
            section.PostName = this.Bytes(4);
            section.Lod = this.ReadBuffer();
            section.AfterLod = this.Bytes(4);
            uint subs = this.U32();
            for (int k = 0; k < subs; k++)
            {
                SubSection sub = new SubSection();
                sub.Pre5 = this.Bytes(5);
                for (int i = 0; i < 8; i++)
                {
                    sub.Uv[i] = this.F32();
                }
                sub.Fixed36 = this.Bytes(0x24);
                int subNameLength = (int) this.U32();
                sub.Name = Encoding.ASCII.GetString(this.data, this.position, subNameLength);
                this.position += subNameLength;
                sub.Lod = this.U32();
                sub.IndexType = this.U32();
                sub.Fixed56 = this.Bytes(56);
                sub.Three = this.U32();
                int indexCount = (int) this.U32();
                sub.IndexSize = (int) this.U32();
                for (int i = 0; i < indexCount; i++)
                {
                    if (sub.IndexSize == 2)
                    {
                        sub.Indices.Add((this.data[this.position] << 8) | this.data[this.position + 1]);
                        this.position += 2;
                    }
                    else
                    {
                        sub.Indices.Add((int) this.U32());
                    }
                }
                sub.Tail = this.Bytes(4);
                section.SubSections.Add(sub);
            }
            section.AfterSubs = this.Bytes(4);
            section.Lod0 = this.ReadBuffer();
            section.Post9 = this.Bytes(9);
            section.DamageLod = this.ReadTable();
            section.Mid = this.Bytes(4);
            section.DamageLod0 = this.ReadTable();
            return section;
        }

        #endregion

        #region Writing

        public byte[] ToBytes()
        {
            using (MemoryStream output = new MemoryStream())
            {
                Write(output, this.Head);
                WriteU32(output, (uint) (this.DrawLists.Count / 2));
                foreach (uint[] list in this.DrawLists)
                {
                    WriteU32(output, (uint) list.Length);
                    foreach (uint entry in list)
                    {
                        WriteU32(output, entry);
                    }
                }
                Write(output, this.BeforeSections);
                WriteU32(output, (uint) this.Sections.Count);
                foreach (Section section in this.Sections)
                {
                    WriteSection(output, section);
                }
                Write(output, this.Trailer);
                return output.ToArray();
            }
        }

        private static void WriteSection(Stream output, Section section)
        {
            Write(output, section.Marker);
            foreach (float value in section.Bounds)
            {
                WriteF32(output, value);
            }
            Write(output, section.Unk1c);
            WriteU32(output, section.ACount);
            Write(output, section.A);
            Write(output, section.AfterA);
            WriteU32(output, section.BCount);
            Write(output, section.B);
            Write(output, section.Pre);
            WriteU32(output, section.VbTest1);
            WriteU32(output, section.VbTest2);
            byte[] name = Encoding.ASCII.GetBytes(section.Name);
            output.WriteByte((byte) name.Length);
            Write(output, name);
            Write(output, section.PostName);
            WriteBuffer(output, section.Lod);
            Write(output, section.AfterLod);
            WriteU32(output, (uint) section.SubSections.Count);
            foreach (SubSection sub in section.SubSections)
            {
                Write(output, sub.Pre5);
                foreach (float value in sub.Uv)
                {
                    WriteF32(output, value);
                }
                Write(output, sub.Fixed36);
                byte[] subName = Encoding.ASCII.GetBytes(sub.Name);
                WriteU32(output, (uint) subName.Length);
                Write(output, subName);
                WriteU32(output, sub.Lod);
                WriteU32(output, sub.IndexType);
                Write(output, sub.Fixed56);
                WriteU32(output, sub.Three);
                WriteU32(output, (uint) sub.Indices.Count);
                WriteU32(output, (uint) sub.IndexSize);
                foreach (int index in sub.Indices)
                {
                    if (sub.IndexSize == 2)
                    {
                        output.WriteByte((byte) (index >> 8));
                        output.WriteByte((byte) index);
                    }
                    else
                    {
                        WriteU32(output, (uint) index);
                    }
                }
                Write(output, sub.Tail);
            }
            Write(output, section.AfterSubs);
            WriteBuffer(output, section.Lod0);
            Write(output, section.Post9);
            WriteTable(output, section.DamageLod);
            Write(output, section.Mid);
            WriteTable(output, section.DamageLod0);
        }

        private static void WriteBuffer(Stream output, VertexBuffer buffer)
        {
            WriteU32(output, (uint) buffer.Count);
            WriteU32(output, (uint) buffer.Stride);
            Write(output, buffer.Extra);
            Write(output, buffer.Data);
        }

        private static void WriteTable(Stream output, Table table)
        {
            WriteU32(output, (uint) table.Count);
            WriteU32(output, (uint) table.Stride);
            Write(output, table.Data);
        }

        private static void Write(Stream output, byte[] bytes)
        {
            output.Write(bytes, 0, bytes.Length);
        }

        private static void WriteU32(Stream output, uint value)
        {
            output.WriteByte((byte) (value >> 24));
            output.WriteByte((byte) (value >> 16));
            output.WriteByte((byte) (value >> 8));
            output.WriteByte((byte) value);
        }

        private static void WriteF32(Stream output, float value)
        {
            byte[] b = BitConverter.GetBytes(value);
            output.WriteByte(b[3]);
            output.WriteByte(b[2]);
            output.WriteByte(b[1]);
            output.WriteByte(b[0]);
        }

        #endregion

        #region Vertices

        private static short S16(byte[] d, int o)
        {
            return (short) ((d[o] << 8) | d[o + 1]);
        }

        private static void WriteS16(byte[] d, int o, int value)
        {
            d[o] = (byte) (value >> 8);
            d[o + 1] = (byte) value;
        }

        private static double Scale(float[] bounds)
        {
            double sum = 0;
            for (int c = 0; c < 3; c++)
            {
                double half = (bounds[6 + c] - bounds[3 + c]) / 2;
                sum += half * half;
            }
            return Math.Sqrt(sum);
        }

        // Model-space positions of a buffer, 3 doubles per vertex.
        public static double[] Positions(Section section, VertexBuffer buffer)
        {
            double scale = Scale(section.Bounds);
            double[] positions = new double[buffer.Count * 3];
            for (int i = 0; i < buffer.Count; i++)
            {
                int o = i * buffer.Stride;
                double w = S16(buffer.Data, o + 6) / 32767.0;
                for (int c = 0; c < 3; c++)
                {
                    double center = (section.Bounds[3 + c] + section.Bounds[6 + c]) / 2;
                    positions[(i * 3) + c] = section.Bounds[c] + center + ((S16(buffer.Data, o + (c * 2)) / 32767.0) * w * scale);
                }
            }
            return positions;
        }

        private static void Encode(Section section, VertexBuffer buffer, double[] positions)
        {
            double scale = Scale(section.Bounds);
            for (int i = 0; i < buffer.Count; i++)
            {
                double[] q = new double[3];
                double length = 0;
                for (int c = 0; c < 3; c++)
                {
                    double center = (section.Bounds[3 + c] + section.Bounds[6 + c]) / 2;
                    q[c] = (positions[(i * 3) + c] - section.Bounds[c] - center) / scale;
                    length += q[c] * q[c];
                }
                length = Math.Sqrt(length);
                int o = i * buffer.Stride;
                if (length < 1e-12)
                {
                    WriteS16(buffer.Data, o, 32767);
                    WriteS16(buffer.Data, o + 2, 0);
                    WriteS16(buffer.Data, o + 4, 0);
                    WriteS16(buffer.Data, o + 6, 0);
                    continue;
                }
                for (int c = 0; c < 3; c++)
                {
                    WriteS16(buffer.Data, o + (c * 2), Clamp((int) Math.Round((q[c] / length) * 32767)));
                }
                WriteS16(buffer.Data, o + 6, Clamp((int) Math.Round(Math.Min(length, 1.0) * 32767)));
            }
        }

        private static int Clamp(int value)
        {
            return Math.Max(-32767, Math.Min(32767, value));
        }

        // The game's convention (all 24430 original sections): the origin is the center of the vertices'
        // AABB and the bounds are symmetric (min = -max). Sets origin and bounds that way for the given
        // positions and re-encodes every vertex, then updates the centroid.
        private static void Reencode(Section section, double[] lod, double[] lod0)
        {
            double[] min = { double.MaxValue, double.MaxValue, double.MaxValue };
            double[] max = { double.MinValue, double.MinValue, double.MinValue };
            foreach (double[] values in new double[][] { lod, lod0 })
            {
                for (int i = 0; i < values.Length; i++)
                {
                    min[i % 3] = Math.Min(min[i % 3], values[i]);
                    max[i % 3] = Math.Max(max[i % 3], values[i]);
                }
            }
            if (min[0] > max[0])
            {
                return;
            }
            float[] origin;
            float[] half;
            SymmetricBounds(min, max, out origin, out half);
            for (int c = 0; c < 3; c++)
            {
                section.Bounds[c] = origin[c];
                section.Bounds[3 + c] = -half[c];
                section.Bounds[6 + c] = half[c];
            }
            Encode(section, section.Lod, lod);
            Encode(section, section.Lod0, lod0);
            UpdateCentroid(section);
        }

        // Origin = AABB center as float; half extents rounded outwards so the float bounds contain every
        // vertex.
        public static void SymmetricBounds(double[] min, double[] max, out float[] origin, out float[] half)
        {
            origin = new float[3];
            half = new float[3];
            for (int c = 0; c < 3; c++)
            {
                origin[c] = (float) ((min[c] + max[c]) / 2);
                double extent = Math.Max(max[c] - origin[c], origin[c] - min[c]);
                float h = (float) extent;
                if (h < extent)
                {
                    h = (float) (extent + (Math.Abs(extent) * 1e-7) + 1e-9);
                }
                half[c] = h;
            }
        }

        // Unk1c floats 1..3 are the mean vertex position relative to the origin (lod0 buffer when the
        // section has one, else the LOD buffer): verified on all 24430 original sections.
        private static void UpdateCentroid(Section section)
        {
            VertexBuffer buffer = (section.Lod0.Count > 0) ? section.Lod0 : section.Lod;
            if (buffer.Count == 0)
            {
                return;
            }
            double[] positions = Positions(section, buffer);
            for (int c = 0; c < 3; c++)
            {
                double sum = 0;
                for (int i = 0; i < buffer.Count; i++)
                {
                    sum += positions[(i * 3) + c];
                }
                byte[] b = BitConverter.GetBytes((float) ((sum / buffer.Count) - section.Bounds[c]));
                section.Unk1c[4 + (c * 4)] = b[3];
                section.Unk1c[5 + (c * 4)] = b[2];
                section.Unk1c[6 + (c * 4)] = b[1];
                section.Unk1c[7 + (c * 4)] = b[0];
            }
        }

        #endregion

        #region Structural edits

        private static bool IsRestart(SubSection sub, int index)
        {
            // Restart markers: 0xFFFF (16-bit) and 0x00FFFFFF (32-bit) in every original file.
            return (sub.IndexSize == 2) ? index == 0xFFFF : (index < 0 || index >= 0xFFFFFF);
        }

        private static int Restart(SubSection sub)
        {
            if (sub.IndexSize == 2)
            {
                return 0xFFFF;
            }
            foreach (int index in sub.Indices)
            {
                if (IsRestart(sub, index))
                {
                    return index;
                }
            }
            return 0x00FFFFFF;
        }

        private static VertexBuffer BufferOf(Section section, SubSection sub)
        {
            return (sub.Lod == 0) ? section.Lod0 : section.Lod;
        }

        private static Table DamageOf(Section section, VertexBuffer buffer)
        {
            return (buffer == section.Lod0) ? section.DamageLod0 : section.DamageLod;
        }

        private static float[] UvRange(SubSection sub, VertexBuffer buffer, int channel, List<int> vertices)
        {
            float lo = float.MaxValue;
            float hi = float.MinValue;
            foreach (int v in vertices)
            {
                int raw = (buffer.Data[(v * buffer.Stride) + 8 + (channel * 2)] << 8) | buffer.Data[(v * buffer.Stride) + 9 + (channel * 2)];
                float value = ((raw / 65535f) * sub.Uv[(channel * 2) + 1]) + sub.Uv[channel * 2];
                lo = Math.Min(lo, value);
                hi = Math.Max(hi, value);
            }
            return new float[] { lo, hi };
        }

        private static List<int> UsedVertices(SubSection sub)
        {
            Dictionary<int, bool> seen = new Dictionary<int, bool>();
            List<int> result = new List<int>();
            foreach (int index in sub.Indices)
            {
                if (!IsRestart(sub, index) && !seen.ContainsKey(index))
                {
                    seen[index] = true;
                    result.Add(index);
                }
            }
            return result;
        }

        // Moves every subsection of 'sourceName' into 'targetName' and removes the source section.
        // A source subsection whose name matches a target subsection of the same LOD is merged into it
        // (strips joined with a restart, UV ranges widened to cover both); the others are appended.
        // Damage words for the new vertices come from the source when it is deformable, otherwise from
        // the nearest target vertex. Returns a short report.
        public string MergeSection(string sourceName, string targetName)
        {
            Section source = this.Find(sourceName);
            Section target = this.Find(targetName);
            if (source == null || target == null || source == target)
            {
                throw new ArgumentException("Source and target must be two different existing sections.");
            }
            StringBuilder report = new StringBuilder();
            int sourceIndex = this.Sections.IndexOf(source);
            int targetIndex = this.Sections.IndexOf(target);

            // Positions in model space, before anything moves.
            double[] targetLod = Positions(target, target.Lod);
            double[] targetLod0 = Positions(target, target.Lod0);
            double[] sourceLod = Positions(source, source.Lod);
            double[] sourceLod0 = Positions(source, source.Lod0);
            List<double> newLod = new List<double>(targetLod);
            List<double> newLod0 = new List<double>(targetLod0);
            MemoryStream lodData = new MemoryStream();
            MemoryStream lod0Data = new MemoryStream();
            lodData.Write(target.Lod.Data, 0, target.Lod.Data.Length);
            lod0Data.Write(target.Lod0.Data, 0, target.Lod0.Data.Length);
            MemoryStream lodDamage = new MemoryStream();
            MemoryStream lod0Damage = new MemoryStream();
            lodDamage.Write(target.DamageLod.Data, 0, target.DamageLod.Data.Length);
            lod0Damage.Write(target.DamageLod0.Data, 0, target.DamageLod0.Data.Length);
            int lodCount = target.Lod.Count;
            int lod0Count = target.Lod0.Count;

            // Appends a copy of a source (or target) vertex; uvFrom/uvTo re-encode its UVs between
            // subsection parameter sets. Returns the new index in the target buffer.
            AppendVertex append = delegate(Section owner, VertexBuffer buffer, double[] ownerPositions, int vertex, SubSection uvFrom, float[] uvTo)
            {
                bool isLod0 = buffer == owner.Lod0;
                int stride = buffer.Stride;
                byte[] copy = new byte[stride];
                Buffer.BlockCopy(buffer.Data, vertex * stride, copy, 0, stride);
                if (uvFrom != null)
                {
                    for (int channel = 0; channel < 4; channel++)
                    {
                        float scale = uvFrom.Uv[(channel * 2) + 1];
                        float newScale = uvTo[(channel * 2) + 1];
                        if (newScale == 0)
                        {
                            continue;
                        }
                        int raw = (copy[8 + (channel * 2)] << 8) | copy[9 + (channel * 2)];
                        float value = ((raw / 65535f) * scale) + uvFrom.Uv[channel * 2];
                        int encoded = (int) Math.Round(((value - uvTo[channel * 2]) / newScale) * 65535);
                        encoded = Math.Max(0, Math.Min(65535, encoded));
                        copy[8 + (channel * 2)] = (byte) (encoded >> 8);
                        copy[9 + (channel * 2)] = (byte) encoded;
                    }
                }
                double x = ownerPositions[vertex * 3];
                double y = ownerPositions[(vertex * 3) + 1];
                double z = ownerPositions[(vertex * 3) + 2];
                Table ownerDamage = DamageOf(owner, buffer);
                byte[] word = new byte[4];
                if (ownerDamage.Count == buffer.Count && ownerDamage.Stride == 4)
                {
                    Buffer.BlockCopy(ownerDamage.Data, vertex * 4, word, 0, 4);
                }
                else
                {
                    // Rigid source: borrow the word of the nearest original target vertex.
                    Table targetDamage = isLod0 ? target.DamageLod0 : target.DamageLod;
                    double[] reference = isLod0 ? targetLod0 : targetLod;
                    int best = -1;
                    double bestDistance = double.MaxValue;
                    for (int i = 0; i < reference.Length / 3; i++)
                    {
                        double dx = reference[i * 3] - x;
                        double dy = reference[(i * 3) + 1] - y;
                        double dz = reference[(i * 3) + 2] - z;
                        double distance = (dx * dx) + (dy * dy) + (dz * dz);
                        if (distance < bestDistance)
                        {
                            bestDistance = distance;
                            best = i;
                        }
                    }
                    if (best >= 0 && targetDamage.Stride == 4)
                    {
                        Buffer.BlockCopy(targetDamage.Data, best * 4, word, 0, 4);
                    }
                }
                if (isLod0)
                {
                    lod0Data.Write(copy, 0, stride);
                    lod0Damage.Write(word, 0, 4);
                    newLod0.Add(x);
                    newLod0.Add(y);
                    newLod0.Add(z);
                    return lod0Count++;
                }
                lodData.Write(copy, 0, stride);
                lodDamage.Write(word, 0, 4);
                newLod.Add(x);
                newLod.Add(y);
                newLod.Add(z);
                return lodCount++;
            };

            // Shared copies of source vertices for appended subsections (one copy per vertex and buffer).
            Dictionary<int, int> sharedLod = new Dictionary<int, int>();
            Dictionary<int, int> sharedLod0 = new Dictionary<int, int>();
            Dictionary<int, int> newIndexOfSource = new Dictionary<int, int>();   // source sub -> target sub (-1 = merged)
            int originalTargetSubs = target.SubSections.Count;

            for (int k = 0; k < source.SubSections.Count; k++)
            {
                SubSection sub = source.SubSections[k];
                VertexBuffer buffer = BufferOf(source, sub);
                double[] sourcePositions = (buffer == source.Lod0) ? sourceLod0 : sourceLod;
                SubSection match = null;
                for (int t = 0; t < originalTargetSubs; t++)
                {
                    SubSection candidate = target.SubSections[t];
                    if (candidate.Name == sub.Name && candidate.Lod == sub.Lod)
                    {
                        match = candidate;
                        break;
                    }
                }

                if (match == null)
                {
                    Dictionary<int, int> shared = (buffer == source.Lod0) ? sharedLod0 : sharedLod;
                    SubSection moved = sub.Copy();
                    moved.IndexSize = (sub.Lod == 0) ? IndexSizeOf(target, true, sub.IndexSize) : IndexSizeOf(target, false, sub.IndexSize);
                    int restart = Restart(moved);
                    moved.Indices.Clear();
                    foreach (int index in sub.Indices)
                    {
                        if (IsRestart(sub, index))
                        {
                            moved.Indices.Add(restart);
                            continue;
                        }
                        int mapped;
                        if (!shared.TryGetValue(index, out mapped))
                        {
                            mapped = append(source, buffer, sourcePositions, index, null, null);
                            shared[index] = mapped;
                        }
                        moved.Indices.Add(mapped);
                    }
                    target.SubSections.Add(moved);
                    newIndexOfSource[k] = target.SubSections.Count - 1;
                    report.AppendFormat("moved {0} (LOD {1}): {2} indices\r\n", sub.Name, sub.Lod, sub.Indices.Count);
                    continue;
                }

                // Merge: one UV parameter set must cover both parts. Widen it if needed, then give the
                // merged subsection its own vertex copies so other subsections keep their UVs.
                VertexBuffer targetBuffer = BufferOf(target, match);
                double[] targetPositions = (targetBuffer == target.Lod0) ? targetLod0 : targetLod;
                List<int> targetUsed = UsedVertices(match);
                List<int> sourceUsed = UsedVertices(sub);
                float[] uv = (float[]) match.Uv.Clone();
                bool widened = false;
                for (int channel = 0; channel < 4; channel++)
                {
                    if (match.Uv[(channel * 2) + 1] == 0 && sub.Uv[(channel * 2) + 1] == 0)
                    {
                        continue;
                    }
                    float[] a = UvRange(match, targetBuffer, channel, targetUsed);
                    float[] b = UvRange(sub, buffer, channel, sourceUsed);
                    float lo = Math.Min(a[0], b[0]);
                    float hi = Math.Max(a[1], b[1]);
                    float offset = match.Uv[channel * 2];
                    float scale = match.Uv[(channel * 2) + 1];
                    if (lo < offset || hi > offset + scale)
                    {
                        uv[channel * 2] = Math.Min(lo, offset);
                        uv[(channel * 2) + 1] = Math.Max(hi, offset + scale) - uv[channel * 2];
                        widened = true;
                    }
                }
                List<int> merged = new List<int>();
                int mergedRestart = Restart(match);
                Dictionary<int, int> ownTarget = new Dictionary<int, int>();
                foreach (int index in match.Indices)
                {
                    if (IsRestart(match, index))
                    {
                        merged.Add(mergedRestart);
                        continue;
                    }
                    if (!widened)
                    {
                        merged.Add(index);
                        continue;
                    }
                    int mapped;
                    if (!ownTarget.TryGetValue(index, out mapped))
                    {
                        mapped = append(target, targetBuffer, targetPositions, index, match, uv);
                        ownTarget[index] = mapped;
                    }
                    merged.Add(mapped);
                }
                merged.Add(mergedRestart);
                Dictionary<int, int> ownSource = new Dictionary<int, int>();
                foreach (int index in sub.Indices)
                {
                    if (IsRestart(sub, index))
                    {
                        merged.Add(mergedRestart);
                        continue;
                    }
                    int mapped;
                    if (!ownSource.TryGetValue(index, out mapped))
                    {
                        mapped = append(source, buffer, sourcePositions, index, sub, uv);
                        ownSource[index] = mapped;
                    }
                    merged.Add(mapped);
                }
                match.Indices = merged;
                match.Uv = uv;
                newIndexOfSource[k] = -1;
                report.AppendFormat("merged {0} (LOD {1}) into {2}.{0}: {3} + {4} indices{5}\r\n", sub.Name, sub.Lod, target.Name, match.Indices.Count - sub.Indices.Count - 1, sub.Indices.Count, widened ? ", UV range widened" : "");
            }

            // New buffers and damage tables.
            target.Lod.Data = lodData.ToArray();
            target.Lod.Count = lodCount;
            target.Lod0.Data = lod0Data.ToArray();
            target.Lod0.Count = lod0Count;
            if (target.Lod.Count > 0 && target.Lod.Stride == 0)
            {
                target.Lod.Stride = source.Lod.Stride;
            }
            if (target.Lod0.Count > 0 && target.Lod0.Stride == 0)
            {
                target.Lod0.Stride = source.Lod0.Stride;
            }
            // A deformable section carries one damage word per vertex of each non-empty buffer.
            if (target.IsDeformable)
            {
                SetDamage(target.DamageLod, lodCount, lodDamage);
                SetDamage(target.DamageLod0, lod0Count, lod0Damage);
            }
            CheckIndexRange(target);
            Reencode(target, newLod.ToArray(), newLod0.ToArray());

            // Draw lists: the source's entries become the target's new subsections (merged ones drop
            // out), and sections after the removed one shift down by one.
            int dropped = 0;
            for (int l = 0; l < this.DrawLists.Count; l++)
            {
                List<uint> list = new List<uint>();
                foreach (uint entry in this.DrawLists[l])
                {
                    int s = (int) (entry >> 16);
                    int k = (int) (entry & 0xFFFF);
                    if (s == sourceIndex)
                    {
                        int mapped;
                        if (!newIndexOfSource.TryGetValue(k, out mapped) || mapped < 0)
                        {
                            dropped++;
                            continue;
                        }
                        s = targetIndex;
                        k = mapped;
                    }
                    if (s > sourceIndex)
                    {
                        s--;
                    }
                    list.Add((uint) ((s << 16) | k));
                }
                this.DrawLists[l] = list.ToArray();
            }
            this.Sections.Remove(source);
            this.CheckDrawLists();
            report.AppendFormat("{0} removed; {1}: LOD vertices {2}, LOD0 vertices {3}; draw list entries dropped: {4}\r\n", source.Name, target.Name, target.Lod.Count, target.Lod0.Count, dropped);
            return report.ToString();
        }

        // Removes every subsection named 'subName' (all LODs) from a section, drops the vertices nobody
        // uses any more and refits the bounds. A section left without subsections is removed.
        // Returns the number of subsections removed.
        public int RemoveSubSections(string sectionName, string subName)
        {
            Section section = this.Find(sectionName);
            if (section == null)
            {
                return 0;
            }
            int sectionIndex = this.Sections.IndexOf(section);
            List<int> removed = new List<int>();
            for (int k = 0; k < section.SubSections.Count; k++)
            {
                if (section.SubSections[k].Name == subName)
                {
                    removed.Add(k);
                }
            }
            if (removed.Count == 0)
            {
                return 0;
            }
            if (removed.Count == section.SubSections.Count)
            {
                this.RemoveSection(sectionName);
                return removed.Count;
            }
            double[] lod = Positions(section, section.Lod);
            double[] lod0 = Positions(section, section.Lod0);
            for (int i = removed.Count - 1; i >= 0; i--)
            {
                section.SubSections.RemoveAt(removed[i]);
            }

            // Draw lists: drop the removed subsections, renumber the later ones of this section.
            for (int l = 0; l < this.DrawLists.Count; l++)
            {
                List<uint> list = new List<uint>();
                foreach (uint entry in this.DrawLists[l])
                {
                    int s = (int) (entry >> 16);
                    int k = (int) (entry & 0xFFFF);
                    if (s == sectionIndex)
                    {
                        if (removed.Contains(k))
                        {
                            continue;
                        }
                        int shift = 0;
                        foreach (int gone in removed)
                        {
                            if (gone < k)
                            {
                                shift++;
                            }
                        }
                        k -= shift;
                    }
                    list.Add((uint) ((s << 16) | k));
                }
                this.DrawLists[l] = list.ToArray();
            }

            lod = Compact(section, section.Lod, section.DamageLod, false, lod);
            lod0 = Compact(section, section.Lod0, section.DamageLod0, true, lod0);
            Reencode(section, lod, lod0);
            CheckIndexRange(section);
            this.CheckDrawLists();
            return removed.Count;
        }

        // Removes a whole section; later sections shift down by one in the draw lists.
        public void RemoveSection(string name)
        {
            Section section = this.Find(name);
            if (section == null)
            {
                return;
            }
            int index = this.Sections.IndexOf(section);
            for (int l = 0; l < this.DrawLists.Count; l++)
            {
                List<uint> list = new List<uint>();
                foreach (uint entry in this.DrawLists[l])
                {
                    int s = (int) (entry >> 16);
                    if (s == index)
                    {
                        continue;
                    }
                    if (s > index)
                    {
                        s--;
                    }
                    list.Add((uint) ((s << 16) | (entry & 0xFFFF)));
                }
                this.DrawLists[l] = list.ToArray();
            }
            this.Sections.RemoveAt(index);
            this.CheckDrawLists();
        }

        // Keeps only the vertices of 'buffer' that a subsection still uses (with their damage words)
        // and remaps the indices. Returns the kept vertices' positions.
        private static double[] Compact(Section section, VertexBuffer buffer, Table damage, bool lod0, double[] positions)
        {
            if (buffer.Count == 0)
            {
                return positions;
            }
            bool[] used = new bool[buffer.Count];
            foreach (SubSection sub in section.SubSections)
            {
                if ((sub.Lod == 0) != lod0)
                {
                    continue;
                }
                foreach (int index in sub.Indices)
                {
                    if (!IsRestart(sub, index))
                    {
                        used[index] = true;
                    }
                }
            }
            int[] map = new int[buffer.Count];
            int count = 0;
            for (int i = 0; i < buffer.Count; i++)
            {
                map[i] = used[i] ? count++ : -1;
            }
            if (count == buffer.Count)
            {
                return positions;
            }
            bool hasDamage = damage.Count == buffer.Count && damage.Stride == 4;
            byte[] data = new byte[count * buffer.Stride];
            byte[] words = new byte[count * 4];
            double[] kept = new double[count * 3];
            for (int i = 0; i < buffer.Count; i++)
            {
                if (map[i] < 0)
                {
                    continue;
                }
                Buffer.BlockCopy(buffer.Data, i * buffer.Stride, data, map[i] * buffer.Stride, buffer.Stride);
                if (hasDamage)
                {
                    Buffer.BlockCopy(damage.Data, i * 4, words, map[i] * 4, 4);
                }
                kept[map[i] * 3] = positions[i * 3];
                kept[(map[i] * 3) + 1] = positions[(i * 3) + 1];
                kept[(map[i] * 3) + 2] = positions[(i * 3) + 2];
            }
            foreach (SubSection sub in section.SubSections)
            {
                if ((sub.Lod == 0) != lod0)
                {
                    continue;
                }
                for (int k = 0; k < sub.Indices.Count; k++)
                {
                    if (!IsRestart(sub, sub.Indices[k]))
                    {
                        sub.Indices[k] = map[sub.Indices[k]];
                    }
                }
            }
            buffer.Data = data;
            buffer.Count = count;
            if (count == 0)
            {
                buffer.Stride = 0;
            }
            if (hasDamage)
            {
                damage.Count = count;
                damage.Data = words;
                if (count == 0)
                {
                    damage.Stride = 0;
                }
            }
            return kept;
        }

        #region OBJ geometry

        // Triangles of a strip subsection, front faces with normal = (b - a) x (c - a) (the winding that
        // matches the stored normals on every original part); degenerate ones skipped.
        public static List<int[]> StripTriangles(SubSection sub)
        {
            List<int[]> faces = new List<int[]>();
            List<int> strip = new List<int>();
            List<int> all = new List<int>(sub.Indices);
            all.Add(-1);
            foreach (int index in all)
            {
                if (index >= 0 && !IsRestart(sub, index))
                {
                    strip.Add(index);
                    continue;
                }
                for (int i = 0; i + 2 < strip.Count; i++)
                {
                    int a = strip[i];
                    int b = strip[i + 1];
                    int c = strip[i + 2];
                    if (a == b || b == c || a == c)
                    {
                        continue;
                    }
                    faces.Add((i % 2 == 0) ? new int[] { a, b, c } : new int[] { b, a, c });
                }
                strip.Clear();
            }
            return faces;
        }

        public static Microsoft.Xna.Framework.Vector2 VertexUv(VertexBuffer buffer, int v, SubSection sub, int set)
        {
            int o = (v * buffer.Stride) + 8 + (set * 4);
            float u = ((((buffer.Data[o] << 8) | buffer.Data[o + 1]) / 65535f) * sub.Uv[(set * 4) + 1]) + sub.Uv[set * 4];
            float w = ((((buffer.Data[o + 2] << 8) | buffer.Data[o + 3]) / 65535f) * sub.Uv[(set * 4) + 3]) + sub.Uv[(set * 4) + 2];
            return new Microsoft.Xna.Framework.Vector2(u, w);
        }

        private static Microsoft.Xna.Framework.Quaternion Frame(VertexBuffer buffer, int v)
        {
            int o = (v * buffer.Stride) + 16;
            Microsoft.Xna.Framework.Quaternion q = new Microsoft.Xna.Framework.Quaternion(S16(buffer.Data, o) / 32767f, S16(buffer.Data, o + 2) / 32767f, S16(buffer.Data, o + 4) / 32767f, S16(buffer.Data, o + 6) / 32767f);
            if (q.LengthSquared() > 1e-8f)
            {
                q.Normalize();
            }
            return q;
        }

        public static Microsoft.Xna.Framework.Vector3 VertexNormal(VertexBuffer buffer, int v)
        {
            return Microsoft.Xna.Framework.Vector3.Transform(Microsoft.Xna.Framework.Vector3.UnitX, Frame(buffer, v));
        }

        // Replaces the geometry of a part with an OBJ mesh, in every LOD the part has in this file (the
        // main file's LODs share one vertex buffer). 'materialMap' maps OBJ material -> car material.
        // fitUv: OBJ UVs are normalized per material and placed in this car's region of the material
        // (part from another car); otherwise they are used as they are (same car). Per vertex, the
        // unknown trailing bytes, UV2 and the damage word come from the nearest original vertex of the
        // part. Returns a report.
        public string ReplaceGeometry(string sectionName, ObjMesh mesh, Dictionary<string, string> materialMap, bool fitUv)
        {
            Section section = this.Find(sectionName);
            if (section == null || section.SubSections.Count == 0)
            {
                return null;
            }
            int sectionIndex = this.Sections.IndexOf(section);
            List<uint> lods = new List<uint>();
            foreach (SubSection sub in section.SubSections)
            {
                if (!lods.Contains(sub.Lod))
                {
                    lods.Add(sub.Lod);
                }
            }
            lods.Sort();
            bool useLod0 = lods.Contains(0);
            VertexBuffer target = useLod0 ? section.Lod0 : section.Lod;
            Table damage = useLod0 ? section.DamageLod0 : section.DamageLod;
            bool deformable = section.IsDeformable && damage.Count == target.Count && damage.Stride == 4;
            if (useLod0 && lods.Count > 1)
            {
                throw new NotSupportedException(sectionName + " mixes lod0 and LOD subsections in one file.");
            }

            // Reference: the part's original vertices (for trailing bytes, UV2 and damage words).
            double[] reference = Positions(section, target);
            byte[] referenceData = target.Data;
            byte[] referenceDamage = damage.Data;
            int referenceCount = target.Count;
            // Their absolute UVs (each subsection has its own UV range), NaN when unused.
            float[] referenceUv = new float[referenceCount * 2];
            for (int i = 0; i < referenceUv.Length; i++)
            {
                referenceUv[i] = float.NaN;
            }
            foreach (SubSection sub in section.SubSections)
            {
                if ((sub.Lod == 0) != useLod0)
                {
                    continue;
                }
                foreach (int index in sub.Indices)
                {
                    if (!IsRestart(sub, index) && index < referenceCount)
                    {
                        Microsoft.Xna.Framework.Vector2 uv = VertexUv(target, index, sub, 0);
                        referenceUv[index * 2] = uv.X;
                        referenceUv[(index * 2) + 1] = uv.Y;
                    }
                }
            }

            // Which draw list each (material, LOD) goes to: where the part had it, else where the model
            // draws that material at that LOD, else the busiest list of the LOD.
            Dictionary<string, int> listOf = new Dictionary<string, int>();
            Dictionary<int, uint> lodOfList = new Dictionary<int, uint>();
            for (int l = 0; l < this.DrawLists.Count; l++)
            {
                foreach (uint entry in this.DrawLists[l])
                {
                    SubSection sub = this.Sections[(int) (entry >> 16)].SubSections[(int) (entry & 0xFFFF)];
                    lodOfList[l] = sub.Lod;
                    string key = sub.Name + "@" + sub.Lod;
                    bool own = (int) (entry >> 16) == sectionIndex;
                    if (own || !listOf.ContainsKey(key))
                    {
                        listOf[key] = l;
                    }
                }
            }

            SubSection template = section.SubSections[0].Copy();
            int indexSize = template.IndexSize;
            int restart = (indexSize == 2) ? 0xFFFF : 0x00FFFFFF;

            // Vertices: one per distinct (material, position, uv, normal).
            List<byte[]> vertices = new List<byte[]>();
            List<double> positions = new List<double>();
            List<byte[]> words = new List<byte[]>();
            List<SubSection> newSubs = new List<SubSection>();
            StringBuilder report = new StringBuilder();
            foreach (ObjMesh.Material material in mesh.Materials)
            {
                string carMaterial;
                if (!materialMap.TryGetValue(material.Name, out carMaterial) || string.IsNullOrEmpty(carMaterial))
                {
                    report.AppendFormat("OBJ material {0} skipped (no car material chosen)\r\n", material.Name);
                    continue;
                }
                SubSection sub = template.Copy();
                sub.Name = carMaterial;
                sub.Indices = new List<int>();
                SubSection own = this.FindMaterial(carMaterial, useLod0);
                float[] uvParams = (own != null) ? (float[]) own.Uv.Clone() : (float[]) template.Uv.Clone();

                // UV range of this material in the OBJ (Forza convention: v = 1 - OBJ v).
                float[] lo = { float.MaxValue, float.MaxValue };
                float[] hi = { float.MinValue, float.MinValue };
                foreach (ObjMesh.Corner[] triangle in material.Triangles)
                {
                    foreach (ObjMesh.Corner corner in triangle)
                    {
                        float u = corner.Uv.X;
                        float w = 1f - corner.Uv.Y;
                        lo[0] = Math.Min(lo[0], u);
                        hi[0] = Math.Max(hi[0], u);
                        lo[1] = Math.Min(lo[1], w);
                        hi[1] = Math.Max(hi[1], w);
                    }
                }
                if (!fitUv)
                {
                    // Region = the OBJ's own range.
                    for (int c = 0; c < 2; c++)
                    {
                        uvParams[c * 2] = lo[c];
                        uvParams[(c * 2) + 1] = Math.Max(hi[c] - lo[c], 1e-6f);
                    }
                }

                // Per-corner tangents from the UV gradients, accumulated per vertex.
                Dictionary<string, int> unique = new Dictionary<string, int>();
                List<Microsoft.Xna.Framework.Vector3> normalSum = new List<Microsoft.Xna.Framework.Vector3>();
                List<Microsoft.Xna.Framework.Vector3> tangentSum = new List<Microsoft.Xna.Framework.Vector3>();
                List<Microsoft.Xna.Framework.Vector3> bitangentSum = new List<Microsoft.Xna.Framework.Vector3>();
                List<int> first = new List<int>();
                List<int[]> faces = new List<int[]>();
                List<ObjMesh.Corner> cornersOf = new List<ObjMesh.Corner>();
                int flipped = 0;
                int kept = 0;
                foreach (ObjMesh.Corner[] triangle in material.Triangles)
                {
                    Microsoft.Xna.Framework.Vector3 face = Microsoft.Xna.Framework.Vector3.Cross(triangle[1].Position - triangle[0].Position, triangle[2].Position - triangle[0].Position);
                    if (face.LengthSquared() < 1e-16f)
                    {
                        continue;
                    }
                    ObjMesh.Corner[] t = triangle;
                    if (triangle[0].HasNormal && triangle[1].HasNormal && triangle[2].HasNormal
                        && Microsoft.Xna.Framework.Vector3.Dot(face, triangle[0].Normal + triangle[1].Normal + triangle[2].Normal) < 0)
                    {
                        // Winding disagrees with the normals: the game's front face is (b-a)x(c-a).
                        t = new ObjMesh.Corner[] { triangle[1], triangle[0], triangle[2] };
                        face = -face;
                        flipped++;
                    }
                    int[] ids = new int[3];
                    for (int k = 0; k < 3; k++)
                    {
                        ObjMesh.Corner corner = t[k];
                        string key = string.Format(CultureInfo.InvariantCulture, "{0:R},{1:R},{2:R}|{3:R},{4:R}|{5:R},{6:R},{7:R}", corner.Position.X, corner.Position.Y, corner.Position.Z, corner.Uv.X, corner.Uv.Y, corner.Normal.X, corner.Normal.Y, corner.Normal.Z);
                        int id;
                        if (!unique.TryGetValue(key, out id))
                        {
                            id = cornersOf.Count;
                            unique[key] = id;
                            cornersOf.Add(corner);
                            normalSum.Add(Microsoft.Xna.Framework.Vector3.Zero);
                            tangentSum.Add(Microsoft.Xna.Framework.Vector3.Zero);
                            bitangentSum.Add(Microsoft.Xna.Framework.Vector3.Zero);
                        }
                        ids[k] = id;
                    }
                    // UV gradient tangent frame of the triangle.
                    Microsoft.Xna.Framework.Vector3 e1 = t[1].Position - t[0].Position;
                    Microsoft.Xna.Framework.Vector3 e2 = t[2].Position - t[0].Position;
                    float du1 = t[1].Uv.X - t[0].Uv.X;
                    float dv1 = (1f - t[1].Uv.Y) - (1f - t[0].Uv.Y);
                    float du2 = t[2].Uv.X - t[0].Uv.X;
                    float dv2 = (1f - t[2].Uv.Y) - (1f - t[0].Uv.Y);
                    float det = (du1 * dv2) - (du2 * dv1);
                    Microsoft.Xna.Framework.Vector3 tangent = Microsoft.Xna.Framework.Vector3.Zero;
                    Microsoft.Xna.Framework.Vector3 bitangent = Microsoft.Xna.Framework.Vector3.Zero;
                    if (Math.Abs(det) > 1e-12f)
                    {
                        tangent = ((e1 * dv2) - (e2 * dv1)) / det;
                        bitangent = ((e2 * du1) - (e1 * du2)) / det;
                    }
                    foreach (int id in ids)
                    {
                        normalSum[id] += face;
                        tangentSum[id] += tangent;
                        bitangentSum[id] += bitangent;
                    }
                    faces.Add(ids);
                }
                if (faces.Count == 0)
                {
                    continue;
                }
                int baseIndex = vertices.Count;
                for (int id = 0; id < cornersOf.Count; id++)
                {
                    ObjMesh.Corner corner = cornersOf[id];
                    Microsoft.Xna.Framework.Vector3 n = corner.HasNormal ? corner.Normal : Microsoft.Xna.Framework.Vector3.Normalize(normalSum[id]);
                    int nearest = Nearest(reference, referenceCount, corner.Position);
                    byte[] vertex = new byte[32];
                    if (nearest >= 0)
                    {
                        Buffer.BlockCopy(referenceData, nearest * 32, vertex, 0, 32);
                    }
                    // UV1 in the chosen region.
                    float u = corner.Uv.X;
                    float w = 1f - corner.Uv.Y;
                    float[] range = { hi[0] - lo[0], hi[1] - lo[1] };
                    float[] value = { u, w };
                    for (int c = 0; c < 2; c++)
                    {
                        float normalized = fitUv ? ((range[c] > 1e-9f) ? (value[c] - lo[c]) / range[c] : 0.5f) : (value[c] - uvParams[c * 2]) / uvParams[(c * 2) + 1];
                        int raw = Math.Max(0, Math.Min(65535, (int) Math.Round(normalized * 65535)));
                        vertex[8 + (c * 2)] = (byte) (raw >> 8);
                        vertex[9 + (c * 2)] = (byte) raw;
                    }
                    // A vertex the part already had (same place, normal and UV: the untouched areas of
                    // an edited part) keeps the game's own tangent frame, copied above.
                    int same = SameVertex(reference, referenceCount, referenceUv, target, corner.Position, n, u, w);
                    if (same >= 0)
                    {
                        // Frame and the 8 trailing bytes as they were.
                        Buffer.BlockCopy(referenceData, (same * 32) + 16, vertex, 16, 16);
                        kept++;
                    }
                    else
                    {
                        WriteFrame(vertex, n, tangentSum[id], bitangentSum[id]);
                        WriteTail(vertex, n);
                    }
                    vertices.Add(vertex);
                    positions.Add(corner.Position.X);
                    positions.Add(corner.Position.Y);
                    positions.Add(corner.Position.Z);
                    byte[] word = new byte[4];
                    if (deformable && nearest >= 0)
                    {
                        Buffer.BlockCopy(referenceDamage, nearest * 4, word, 0, 4);
                    }
                    words.Add(word);
                }
                foreach (int[] face in faces)
                {
                    if (sub.Indices.Count > 0)
                    {
                        sub.Indices.Add(restart);
                    }
                    sub.Indices.Add(face[0] + baseIndex);
                    sub.Indices.Add(face[1] + baseIndex);
                    sub.Indices.Add(face[2] + baseIndex);
                }
                sub.Uv = uvParams;
                newSubs.Add(sub);
                report.AppendFormat("{0} -> {1}: {2} triangles, {3} vertices ({4} unchanged, game tangent frame kept){5}\r\n", material.Name, carMaterial, faces.Count, cornersOf.Count, kept, (flipped > 0) ? string.Format(" ({0} triangles turned to match the normals)", flipped) : "");
            }
            if (newSubs.Count == 0)
            {
                throw new InvalidDataException("The OBJ has no usable triangles for this part.");
            }
            if (indexSize == 2 && vertices.Count >= 0xFFFF)
            {
                throw new InvalidDataException(string.Format("{0} vertices do not fit the 16-bit indices of this file.", vertices.Count));
            }

            // New subsections: every material at every LOD the part had in this file.
            section.SubSections.Clear();
            List<KeyValuePair<int, SubSection>> placed = new List<KeyValuePair<int, SubSection>>();
            foreach (uint lod in lods)
            {
                foreach (SubSection sub in newSubs)
                {
                    SubSection copy = sub.Copy();
                    copy.Lod = lod;
                    section.SubSections.Add(copy);
                    int list;
                    if (!listOf.TryGetValue(copy.Name + "@" + lod, out list))
                    {
                        list = -1;
                        foreach (KeyValuePair<int, uint> pair in lodOfList)
                        {
                            if (pair.Value == lod && (list < 0 || this.DrawLists[pair.Key].Length > this.DrawLists[list].Length))
                            {
                                list = pair.Key;
                            }
                        }
                    }
                    if (list < 0)
                    {
                        throw new InvalidDataException("No draw list for LOD " + lod + ".");
                    }
                    placed.Add(new KeyValuePair<int, SubSection>(list, copy));
                }
            }

            // Vertex buffer and damage words.
            byte[] data = new byte[vertices.Count * 32];
            byte[] damageData = new byte[vertices.Count * 4];
            for (int i = 0; i < vertices.Count; i++)
            {
                Buffer.BlockCopy(vertices[i], 0, data, i * 32, 32);
                Buffer.BlockCopy(words[i], 0, damageData, i * 4, 4);
            }
            target.Data = data;
            target.Count = vertices.Count;
            target.Stride = 32;
            if (deformable)
            {
                damage.Count = vertices.Count;
                damage.Stride = 4;
                damage.Data = damageData;
            }

            // Draw lists: old entries of the part out, new ones in.
            for (int l = 0; l < this.DrawLists.Count; l++)
            {
                List<uint> list = new List<uint>();
                foreach (uint entry in this.DrawLists[l])
                {
                    if ((int) (entry >> 16) != sectionIndex)
                    {
                        list.Add(entry);
                    }
                }
                for (int k = 0; k < section.SubSections.Count; k++)
                {
                    foreach (KeyValuePair<int, SubSection> pair in placed)
                    {
                        if (pair.Key == l && pair.Value == section.SubSections[k])
                        {
                            list.Add((uint) ((sectionIndex << 16) | k));
                        }
                    }
                }
                this.DrawLists[l] = list.ToArray();
            }

            double[] all = positions.ToArray();
            Reencode(section, useLod0 ? new double[0] : all, useLod0 ? all : new double[0]);
            CheckIndexRange(section);
            this.CheckDrawLists();
            report.AppendFormat("{0}: {1} vertices, LODs {2}", sectionName, vertices.Count, string.Join(",", lods.ConvertAll<string>(delegate(uint x) { return x.ToString(); }).ToArray()));
            return report.ToString();
        }

        // The 8 trailing vertex bytes: four SNORM16, t0 a level (high on open surfaces, low in
        // recesses) and (t1, t2, t3) a vector along the normal, world = (-t3, -t1, t2), of length
        // ~1.24 t0 - 0.07 (fitted on 278702 vertices of two cars; looks like baked ambient light,
        // exact recipe unknown). Copying them from the nearest old vertex made blotches on new shapes;
        // a new vertex gets the level of an open panel facing its way (0.83 facing up or sideways,
        // down to 0.50 facing the ground: 75th percentile of the two cars' exterior panels by normal)
        // and its own normal, which shades smoothly and as bright as the bodywork around it.
        private static void WriteTail(byte[] vertex, Microsoft.Xna.Framework.Vector3 n)
        {
            Microsoft.Xna.Framework.Vector3 d = Microsoft.Xna.Framework.Vector3.Normalize(n);
            float level = 0.50f + (0.33f * Math.Max(0f, Math.Min(1f, (d.Y + 1f) / 1.25f)));
            float length = Math.Max(0f, Math.Min(1f, (1.24f * level) - 0.07f));
            WriteS16(vertex, 24, Clamp((int) Math.Round(level * 32767)));
            WriteS16(vertex, 26, Clamp((int) Math.Round(-d.Y * length * 32767)));
            WriteS16(vertex, 28, Clamp((int) Math.Round(d.Z * length * 32767)));
            WriteS16(vertex, 30, Clamp((int) Math.Round(-d.X * length * 32767)));
        }

        // The original vertex within 0.1 mm, 1 degree and 1e-4 in UV of the new one, or -1. Several
        // originals share a place on hard edges and UV seams, so all of them are tried.
        private static int SameVertex(double[] reference, int count, float[] referenceUv, VertexBuffer buffer, Microsoft.Xna.Framework.Vector3 p, Microsoft.Xna.Framework.Vector3 n, float u, float v)
        {
            Microsoft.Xna.Framework.Vector3 wanted = Microsoft.Xna.Framework.Vector3.Normalize(n);
            for (int index = 0; index < count; index++)
            {
                double dx = reference[index * 3] - p.X;
                double dy = reference[(index * 3) + 1] - p.Y;
                double dz = reference[(index * 3) + 2] - p.Z;
                if ((dx * dx) + (dy * dy) + (dz * dz) > 1e-8)
                {
                    continue;
                }
                bool uv = Math.Abs(referenceUv[index * 2] - u) < 1e-4f && Math.Abs(referenceUv[(index * 2) + 1] - v) < 1e-4f;
                if (uv && Microsoft.Xna.Framework.Vector3.Dot(VertexNormal(buffer, index), wanted) > 0.99985f)
                {
                    return index;
                }
            }
            return -1;
        }

        private static int Nearest(double[] reference, int count, Microsoft.Xna.Framework.Vector3 p)
        {
            int best = -1;
            double bestDistance = double.MaxValue;
            for (int i = 0; i < count; i++)
            {
                double dx = reference[i * 3] - p.X;
                double dy = reference[(i * 3) + 1] - p.Y;
                double dz = reference[(i * 3) + 2] - p.Z;
                double distance = (dx * dx) + (dy * dy) + (dz * dz);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }
            return best;
        }

        // Tangent frame quaternion as in the game's models (measured on 221742 vertices of two cars):
        // X = normal, -Z = the direction the texture v grows (always), Y = Z x X (so +-dP/du, by UV
        // handedness). The frame carries no handedness; the sign of w is free (q and -q are the
        // same rotation) and kept >= 0.
        private static void WriteFrame(byte[] vertex, Microsoft.Xna.Framework.Vector3 n, Microsoft.Xna.Framework.Vector3 tangent, Microsoft.Xna.Framework.Vector3 bitangent)
        {
            if (n.LengthSquared() < 1e-12f)
            {
                n = Microsoft.Xna.Framework.Vector3.UnitY;
            }
            n.Normalize();
            // dP/dv in the normal's plane; without UVs (untextured materials such as plastic2, where
            // the game's own frames follow no rule) Z leans on the car's length axis, else on X.
            Microsoft.Xna.Framework.Vector3 v = bitangent - (n * Microsoft.Xna.Framework.Vector3.Dot(n, bitangent));
            if (v.LengthSquared() < 1e-12f)
            {
                Microsoft.Xna.Framework.Vector3 axis = (Math.Abs(n.Z) < 0.9f) ? Microsoft.Xna.Framework.Vector3.UnitZ : Microsoft.Xna.Framework.Vector3.UnitX;
                v = -(axis - (n * Microsoft.Xna.Framework.Vector3.Dot(n, axis)));
            }
            v.Normalize();
            Microsoft.Xna.Framework.Vector3 z = -v;
            Microsoft.Xna.Framework.Vector3 y = Microsoft.Xna.Framework.Vector3.Cross(z, n);
            Microsoft.Xna.Framework.Matrix m = Microsoft.Xna.Framework.Matrix.Identity;
            m.M11 = n.X;
            m.M12 = n.Y;
            m.M13 = n.Z;
            m.M21 = y.X;
            m.M22 = y.Y;
            m.M23 = y.Z;
            m.M31 = z.X;
            m.M32 = z.Y;
            m.M33 = z.Z;
            Microsoft.Xna.Framework.Quaternion q = Microsoft.Xna.Framework.Quaternion.CreateFromRotationMatrix(m);
            q.Normalize();
            if (q.W < 0)
            {
                q = Microsoft.Xna.Framework.Quaternion.Negate(q);
            }
            WriteS16(vertex, 16, Clamp((int) Math.Round(q.X * 32767)));
            WriteS16(vertex, 18, Clamp((int) Math.Round(q.Y * 32767)));
            WriteS16(vertex, 20, Clamp((int) Math.Round(q.Z * 32767)));
            WriteS16(vertex, 22, Clamp((int) Math.Round(q.W * 32767)));
        }

        #endregion

        #region Import from another model

        private static Section CopySection(Section s)
        {
            Section copy = new Section();
            copy.Marker = (byte[]) s.Marker.Clone();
            copy.Bounds = (float[]) s.Bounds.Clone();
            copy.Unk1c = (byte[]) s.Unk1c.Clone();
            copy.ACount = s.ACount;
            copy.A = (byte[]) s.A.Clone();
            copy.AfterA = (byte[]) s.AfterA.Clone();
            copy.BCount = s.BCount;
            copy.B = (byte[]) s.B.Clone();
            copy.Pre = (byte[]) s.Pre.Clone();
            copy.VbTest1 = s.VbTest1;
            copy.VbTest2 = s.VbTest2;
            copy.Name = s.Name;
            copy.PostName = (byte[]) s.PostName.Clone();
            copy.Lod = CopyBuffer(s.Lod);
            copy.AfterLod = (byte[]) s.AfterLod.Clone();
            foreach (SubSection sub in s.SubSections)
            {
                copy.SubSections.Add(sub.Copy());
            }
            copy.AfterSubs = (byte[]) s.AfterSubs.Clone();
            copy.Lod0 = CopyBuffer(s.Lod0);
            copy.Post9 = (byte[]) s.Post9.Clone();
            copy.DamageLod = CopyTable(s.DamageLod);
            copy.Mid = (byte[]) s.Mid.Clone();
            copy.DamageLod0 = CopyTable(s.DamageLod0);
            return copy;
        }

        private static VertexBuffer CopyBuffer(VertexBuffer b)
        {
            VertexBuffer copy = new VertexBuffer();
            copy.Count = b.Count;
            copy.Stride = b.Stride;
            copy.Extra = (byte[]) b.Extra.Clone();
            copy.Data = (byte[]) b.Data.Clone();
            return copy;
        }

        private static Table CopyTable(Table t)
        {
            Table copy = new Table();
            copy.Count = t.Count;
            copy.Stride = t.Stride;
            copy.Data = (byte[]) t.Data.Clone();
            return copy;
        }

        // Copies a section of another model of the same kind (main / lod0 / cockpit) into this one,
        // with its vertices, tangent frames, damage data and all LODs, under 'newName'. Its draw-list
        // entries go to the same lists (same LOD and render pass) as in the source. When
        // 'useOwnMaterialRegions' is set, each imported material takes the UV region this model already
        // uses for a material of the same name: UVs index the car's own texture sheets, so the source
        // car's regions would sample unrelated pixels here. Returns a short report.
        public string ImportSection(CarbinModel source, string sourceName, string newName, bool useOwnMaterialRegions)
        {
            Section from = source.Find(sourceName);
            if (from == null)
            {
                throw new ArgumentException("The source model has no part " + sourceName + ".");
            }
            if (this.Find(newName) != null)
            {
                throw new ArgumentException("This model already has a part named " + newName + ".");
            }
            if (source.DrawLists.Count != this.DrawLists.Count)
            {
                throw new NotSupportedException(string.Format("The models have different LOD layouts ({0} and {1} draw lists): import between files of the same kind (main, lod0, cockpit).", source.DrawLists.Count / 2, this.DrawLists.Count / 2));
            }
            Section copy = CopySection(from);
            copy.Name = newName;
            foreach (SubSection sub in copy.SubSections)
            {
                int expected = IndexSizeOfFile(this, sub.Lod == 0);
                if (expected != 0 && expected != sub.IndexSize)
                {
                    ConvertIndices(sub, expected);
                }
            }
            StringBuilder report = new StringBuilder();
            if (useOwnMaterialRegions)
            {
                foreach (SubSection sub in copy.SubSections)
                {
                    SubSection own = this.FindMaterial(sub.Name, sub.Lod == 0);
                    if (own != null)
                    {
                        sub.Uv = (float[]) own.Uv.Clone();
                    }
                    else
                    {
                        report.AppendFormat("material {0} is not used by this car: it keeps the source UV region\r\n", sub.Name);
                    }
                }
            }
            CheckIndexRange(copy);
            int sourceIndex = source.Sections.IndexOf(from);
            this.Sections.Add(copy);
            int index = this.Sections.Count - 1;
            for (int l = 0; l < source.DrawLists.Count; l++)
            {
                List<uint> list = new List<uint>(this.DrawLists[l]);
                foreach (uint entry in source.DrawLists[l])
                {
                    if ((int) (entry >> 16) == sourceIndex)
                    {
                        list.Add((uint) ((index << 16) | (entry & 0xFFFF)));
                    }
                }
                this.DrawLists[l] = list.ToArray();
            }
            this.CheckDrawLists();
            report.AppendFormat("{0} imported as {1}: {2} materials, {3} + {4} vertices", sourceName, newName, copy.SubSections.Count, copy.Lod.Count, copy.Lod0.Count);
            return report.ToString();
        }

        // A copy of one of this model's parts under a new name (same materials, damage data, LODs and
        // draw lists): the template for a part added from an OBJ.
        public void DuplicateSection(string name, string newName)
        {
            this.ImportSection(this, name, newName, false);
        }

        // A subsection of this model using the material, preferring the given buffer kind.
        private SubSection FindMaterial(string name, bool lod0)
        {
            SubSection fallback = null;
            foreach (Section section in this.Sections)
            {
                foreach (SubSection sub in section.SubSections)
                {
                    if (sub.Name != name)
                    {
                        continue;
                    }
                    if ((sub.Lod == 0) == lod0)
                    {
                        return sub;
                    }
                    if (fallback == null)
                    {
                        fallback = sub;
                    }
                }
            }
            return fallback;
        }

        private static int IndexSizeOfFile(CarbinModel model, bool lod0)
        {
            foreach (Section section in model.Sections)
            {
                foreach (SubSection sub in section.SubSections)
                {
                    if ((sub.Lod == 0) == lod0)
                    {
                        return sub.IndexSize;
                    }
                }
            }
            return 0;
        }

        private static void ConvertIndices(SubSection sub, int size)
        {
            int restart = (size == 2) ? 0xFFFF : 0x00FFFFFF;
            for (int i = 0; i < sub.Indices.Count; i++)
            {
                if (IsRestart(sub, sub.Indices[i]))
                {
                    sub.Indices[i] = restart;
                }
            }
            sub.IndexSize = size;
        }

        // Moves / rotates / scales a whole section (positions and tangent frames), then refits bounds.
        public void TransformSection(string name, Microsoft.Xna.Framework.Matrix m)
        {
            Section section = this.Find(name);
            if (section == null)
            {
                return;
            }
            Microsoft.Xna.Framework.Quaternion rotation;
            Microsoft.Xna.Framework.Matrix normalMatrix;
            FrameTransform(m, out rotation, out normalMatrix);
            double[][] all = new double[2][];
            VertexBuffer[] buffers = { section.Lod, section.Lod0 };
            for (int b = 0; b < 2; b++)
            {
                VertexBuffer buffer = buffers[b];
                double[] positions = Positions(section, buffer);
                for (int i = 0; i < buffer.Count; i++)
                {
                    Microsoft.Xna.Framework.Vector3 p = new Microsoft.Xna.Framework.Vector3((float) positions[i * 3], (float) positions[(i * 3) + 1], (float) positions[(i * 3) + 2]);
                    p = Microsoft.Xna.Framework.Vector3.Transform(p, m);
                    positions[i * 3] = p.X;
                    positions[(i * 3) + 1] = p.Y;
                    positions[(i * 3) + 2] = p.Z;
                    TransformFrame(buffer.Data, (i * buffer.Stride) + 16, rotation, normalMatrix);
                }
                all[b] = positions;
            }
            Reencode(section, all[0], all[1]);
        }

        #endregion

        #region Transforms

        // Rotation and normal matrix of an affine transform (XNA row vectors: p' = p * m).
        public static void FrameTransform(Microsoft.Xna.Framework.Matrix m, out Microsoft.Xna.Framework.Quaternion rotation, out Microsoft.Xna.Framework.Matrix normalMatrix)
        {
            Microsoft.Xna.Framework.Matrix linear = m;
            linear.Translation = Microsoft.Xna.Framework.Vector3.Zero;
            normalMatrix = Microsoft.Xna.Framework.Matrix.Transpose(Microsoft.Xna.Framework.Matrix.Invert(linear));
            Microsoft.Xna.Framework.Vector3 scale;
            Microsoft.Xna.Framework.Vector3 translation;
            if (!linear.Decompose(out scale, out rotation, out translation))
            {
                rotation = Microsoft.Xna.Framework.Quaternion.Identity;
            }
            if (scale.X * scale.Y * scale.Z < 0)
            {
                throw new NotSupportedException("Mirroring (negative scale) would flip the triangle winding.");
            }
        }

        // Tangent frame at 'offset': 4 x SNORM16 quaternion; the normal is its matrix's first row (X axis
        // image). The rotation is applied after the original frame; a remaining normal difference
        // (non-uniform scale) is closed by the shortest arc. The sign of w (likely the bitangent
        // handedness) is kept.
        public static void TransformFrame(byte[] data, int offset, Microsoft.Xna.Framework.Quaternion rotation, Microsoft.Xna.Framework.Matrix normalMatrix)
        {
            Microsoft.Xna.Framework.Quaternion q = new Microsoft.Xna.Framework.Quaternion(
                S16(data, offset) / 32767f, S16(data, offset + 2) / 32767f, S16(data, offset + 4) / 32767f, S16(data, offset + 6) / 32767f);
            if (q.LengthSquared() < 1e-6f)
            {
                return;
            }
            bool negativeW = q.W < 0;
            q.Normalize();
            Microsoft.Xna.Framework.Vector3 normal = Microsoft.Xna.Framework.Vector3.Transform(Microsoft.Xna.Framework.Vector3.UnitX, q);
            Microsoft.Xna.Framework.Vector3 wanted = Microsoft.Xna.Framework.Vector3.TransformNormal(normal, normalMatrix);
            q = Microsoft.Xna.Framework.Quaternion.Concatenate(q, rotation);
            if (wanted.LengthSquared() > 1e-12f)
            {
                wanted.Normalize();
                Microsoft.Xna.Framework.Vector3 current = Microsoft.Xna.Framework.Vector3.Transform(Microsoft.Xna.Framework.Vector3.UnitX, q);
                float dot = Microsoft.Xna.Framework.Vector3.Dot(current, wanted);
                Microsoft.Xna.Framework.Vector3 axis = Microsoft.Xna.Framework.Vector3.Cross(current, wanted);
                if (dot < 0.999999f && axis.LengthSquared() > 1e-12f)
                {
                    axis.Normalize();
                    float angle = (float) Math.Acos(Math.Max(-1f, Math.Min(1f, dot)));
                    q = Microsoft.Xna.Framework.Quaternion.Concatenate(q, Microsoft.Xna.Framework.Quaternion.CreateFromAxisAngle(axis, angle));
                }
            }
            q.Normalize();
            if ((q.W < 0) != negativeW)
            {
                q = Microsoft.Xna.Framework.Quaternion.Negate(q);
            }
            WriteS16(data, offset, Clamp((int) Math.Round(q.X * 32767)));
            WriteS16(data, offset + 2, Clamp((int) Math.Round(q.Y * 32767)));
            WriteS16(data, offset + 4, Clamp((int) Math.Round(q.Z * 32767)));
            WriteS16(data, offset + 6, Clamp((int) Math.Round(q.W * 32767)));
        }

        // AABB center and half size of the vertices used by the named subsections (all LODs), from the
        // lod0 buffer when they use it, else the LOD buffer. False if the material is not in the section.
        public static bool MaterialBounds(Section section, string subName, out double[] center, out double[] half)
        {
            center = new double[3];
            half = new double[3];
            foreach (bool lod0 in new bool[] { true, false })
            {
                VertexBuffer buffer = lod0 ? section.Lod0 : section.Lod;
                double[] positions = null;
                double[] min = { double.MaxValue, double.MaxValue, double.MaxValue };
                double[] max = { double.MinValue, double.MinValue, double.MinValue };
                foreach (SubSection sub in section.SubSections)
                {
                    if (sub.Name != subName || (sub.Lod == 0) != lod0)
                    {
                        continue;
                    }
                    if (positions == null)
                    {
                        positions = Positions(section, buffer);
                    }
                    foreach (int index in sub.Indices)
                    {
                        if (IsRestart(sub, index))
                        {
                            continue;
                        }
                        for (int c = 0; c < 3; c++)
                        {
                            min[c] = Math.Min(min[c], positions[(index * 3) + c]);
                            max[c] = Math.Max(max[c], positions[(index * 3) + c]);
                        }
                    }
                }
                if (positions != null && min[0] <= max[0])
                {
                    for (int c = 0; c < 3; c++)
                    {
                        center[c] = (min[c] + max[c]) / 2;
                        half[c] = (max[c] - min[c]) / 2;
                    }
                    return true;
                }
            }
            return false;
        }

        // Transforms only the vertices of the named subsections (one material of a part, all LODs).
        // Vertices they share with other subsections are duplicated first, so the other materials stay
        // where they are. Bounds and centroid are refitted. Returns the number of vertices moved.
        public int TransformSubSections(string sectionName, string subName, Microsoft.Xna.Framework.Matrix m)
        {
            Section section = this.Find(sectionName);
            if (section == null)
            {
                return 0;
            }
            Microsoft.Xna.Framework.Quaternion rotation;
            Microsoft.Xna.Framework.Matrix normalMatrix;
            FrameTransform(m, out rotation, out normalMatrix);
            double[] lod = Positions(section, section.Lod);
            double[] lod0 = Positions(section, section.Lod0);
            int moved = 0;
            lod = TransformMaterial(section, section.Lod, section.DamageLod, false, lod, subName, m, rotation, normalMatrix, ref moved);
            lod0 = TransformMaterial(section, section.Lod0, section.DamageLod0, true, lod0, subName, m, rotation, normalMatrix, ref moved);
            if (moved > 0)
            {
                Reencode(section, lod, lod0);
                CheckIndexRange(section);
            }
            return moved;
        }

        private static double[] TransformMaterial(Section section, VertexBuffer buffer, Table damage, bool lod0, double[] positions, string subName,
            Microsoft.Xna.Framework.Matrix m, Microsoft.Xna.Framework.Quaternion rotation, Microsoft.Xna.Framework.Matrix normalMatrix, ref int moved)
        {
            if (buffer.Count == 0)
            {
                return positions;
            }
            bool[] mine = new bool[buffer.Count];
            bool[] others = new bool[buffer.Count];
            foreach (SubSection sub in section.SubSections)
            {
                if ((sub.Lod == 0) != lod0)
                {
                    continue;
                }
                bool[] marks = (sub.Name == subName) ? mine : others;
                foreach (int index in sub.Indices)
                {
                    if (!IsRestart(sub, index))
                    {
                        marks[index] = true;
                    }
                }
            }

            // Shared vertices: the material gets its own copies (appended, damage word copied).
            bool hasDamage = damage.Count == buffer.Count && damage.Stride == 4;
            Dictionary<int, int> copies = new Dictionary<int, int>();
            List<double> newPositions = new List<double>(positions);
            MemoryStream data = new MemoryStream();
            data.Write(buffer.Data, 0, buffer.Data.Length);
            MemoryStream words = new MemoryStream();
            words.Write(damage.Data, 0, damage.Data.Length);
            int count = buffer.Count;
            for (int i = 0; i < buffer.Count; i++)
            {
                if (mine[i] && others[i])
                {
                    copies[i] = count++;
                    data.Write(buffer.Data, i * buffer.Stride, buffer.Stride);
                    if (hasDamage)
                    {
                        words.Write(damage.Data, i * 4, 4);
                    }
                    newPositions.Add(positions[i * 3]);
                    newPositions.Add(positions[(i * 3) + 1]);
                    newPositions.Add(positions[(i * 3) + 2]);
                }
            }
            if (copies.Count > 0)
            {
                foreach (SubSection sub in section.SubSections)
                {
                    if ((sub.Lod == 0) != lod0 || sub.Name != subName)
                    {
                        continue;
                    }
                    for (int k = 0; k < sub.Indices.Count; k++)
                    {
                        int copy;
                        if (!IsRestart(sub, sub.Indices[k]) && copies.TryGetValue(sub.Indices[k], out copy))
                        {
                            sub.Indices[k] = copy;
                        }
                    }
                }
                buffer.Data = data.ToArray();
                buffer.Count = count;
                if (hasDamage)
                {
                    damage.Data = words.ToArray();
                    damage.Count = count;
                }
            }

            // Which vertices move: the material's own ones (originals not shared, plus the copies).
            bool[] move = new bool[count];
            for (int i = 0; i < mine.Length; i++)
            {
                move[i] = mine[i] && !others[i];
            }
            foreach (int copy in copies.Values)
            {
                move[copy] = true;
            }
            double[] result = newPositions.ToArray();
            for (int i = 0; i < count; i++)
            {
                if (!move[i])
                {
                    continue;
                }
                Microsoft.Xna.Framework.Vector3 p = new Microsoft.Xna.Framework.Vector3((float) result[i * 3], (float) result[(i * 3) + 1], (float) result[(i * 3) + 2]);
                p = Microsoft.Xna.Framework.Vector3.Transform(p, m);
                result[i * 3] = p.X;
                result[(i * 3) + 1] = p.Y;
                result[(i * 3) + 2] = p.Z;
                TransformFrame(buffer.Data, (i * buffer.Stride) + 16, rotation, normalMatrix);
                moved++;
            }
            return result;
        }

        #endregion

        private delegate int AppendVertex(Section owner, VertexBuffer buffer, double[] ownerPositions, int vertex, SubSection uvFrom, float[] uvTo);

        private static void SetDamage(Table table, int vertices, MemoryStream words)
        {
            if (vertices == 0)
            {
                return;
            }
            table.Count = vertices;
            table.Stride = 4;
            table.Data = words.ToArray();
            if (table.Data.Length != vertices * 4)
            {
                throw new InvalidDataException("Damage table does not match the vertex count.");
            }
        }

        // Index width used by the target's subsections for that buffer (files use one width for all).
        private static int IndexSizeOf(Section target, bool lod0, int fallback)
        {
            foreach (SubSection sub in target.SubSections)
            {
                if ((sub.Lod == 0) == lod0)
                {
                    return sub.IndexSize;
                }
            }
            return fallback;
        }

        private static void CheckIndexRange(Section section)
        {
            foreach (SubSection sub in section.SubSections)
            {
                VertexBuffer buffer = BufferOf(section, sub);
                foreach (int index in sub.Indices)
                {
                    if (IsRestart(sub, index))
                    {
                        continue;
                    }
                    if (index < 0 || index >= buffer.Count)
                    {
                        throw new InvalidDataException(string.Format("{0}.{1}: index {2} outside the vertex buffer ({3}).", section.Name, sub.Name, index, buffer.Count));
                    }
                }
                if (sub.IndexSize == 2 && buffer.Count >= 0xFFFF)
                {
                    throw new InvalidDataException(string.Format("{0}: {1} vertices do not fit 16-bit indices.", section.Name, buffer.Count));
                }
            }
        }

        // Every subsection must be drawn exactly once, and every entry must point at a subsection.
        public void CheckDrawLists()
        {
            Dictionary<uint, int> seen = new Dictionary<uint, int>();
            foreach (uint[] list in this.DrawLists)
            {
                foreach (uint entry in list)
                {
                    int s = (int) (entry >> 16);
                    int k = (int) (entry & 0xFFFF);
                    if (s >= this.Sections.Count || k >= this.Sections[s].SubSections.Count)
                    {
                        throw new InvalidDataException(string.Format("Draw list entry {0:X8} points outside the model.", entry));
                    }
                    int count;
                    seen.TryGetValue(entry, out count);
                    seen[entry] = count + 1;
                }
            }
            for (int s = 0; s < this.Sections.Count; s++)
            {
                for (int k = 0; k < this.Sections[s].SubSections.Count; k++)
                {
                    int count;
                    seen.TryGetValue((uint) ((s << 16) | k), out count);
                    if (count != 1)
                    {
                        throw new InvalidDataException(string.Format("{0}.{1} appears {2} times in the draw lists.", this.Sections[s].Name, this.Sections[s].SubSections[k].Name, count));
                    }
                }
            }
        }

        #endregion
    }
}
