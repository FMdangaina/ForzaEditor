namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;

    // Editable view of an FM4 .carbin (type 2: main, lod0 and cockpit files). The original bytes are
    // kept and edits are written in place, so everything not touched stays byte for byte identical.
    //
    // Vertex (32 bytes, big endian): 4 x SNORM16 position, 2 x UNORM16 UV1, 2 x UNORM16 UV2,
    // 4 x SNORM16 tangent-frame quaternion, 8 unknown bytes. The position shorts are a unit direction
    // plus a length: pos = origin + bboxCenter + dir * w * |bboxHalfExtent| (origin and bbox per section).
    public class CarbinFile
    {
        public class VertexBuffer
        {
            public int Offset;
            public int Count;
            public int Stride;
            public bool IsLod0;
        }

        public class SubSection
        {
            public string Name;
            public int Lod;
            public int IndexType;
            public int IndexOffset;
            public int IndexCount;
            public int IndexSize;
            public int UvParamsOffset;
        }

        public class Section
        {
            public string Name;
            public int Offset;
            public int BoundsOffset;    // origin(3) min(3) max(3) floats
            public bool IsDeformable;   // has per-vertex damage deformation data
            public double[] Origin = new double[3];
            public double[] Min = new double[3];
            public double[] Max = new double[3];
            public List<VertexBuffer> Buffers = new List<VertexBuffer>();
            public List<SubSection> SubSections = new List<SubSection>();

            public double Scale
            {
                get
                {
                    double sum = 0;
                    for (int c = 0; c < 3; c++)
                    {
                        double half = (this.Max[c] - this.Min[c]) / 2;
                        sum += half * half;
                    }
                    return Math.Sqrt(sum);
                }
            }
        }

        private byte[] data;
        private int position;

        public int Type { get; private set; }
        public List<Section> Sections = new List<Section>();

        public CarbinFile(byte[] file)
        {
            this.data = (byte[]) file.Clone();
            this.Parse();
        }

        public byte[] ToBytes()
        {
            return (byte[]) this.data.Clone();
        }

        #region Parsing

        private uint U32()
        {
            uint v = (uint) ((this.data[this.position] << 24) | (this.data[this.position + 1] << 16) | (this.data[this.position + 2] << 8) | this.data[this.position + 3]);
            this.position += 4;
            return v;
        }

        private float F32At(int offset)
        {
            byte[] b = { this.data[offset + 3], this.data[offset + 2], this.data[offset + 1], this.data[offset] };
            return BitConverter.ToSingle(b, 0);
        }

        private void WriteF32At(int offset, float value)
        {
            byte[] b = BitConverter.GetBytes(value);
            this.data[offset] = b[3];
            this.data[offset + 1] = b[2];
            this.data[offset + 2] = b[1];
            this.data[offset + 3] = b[0];
        }

        private void Parse()
        {
            this.position = 0;
            this.Type = (int) this.U32();
            if (this.Type != 2)
            {
                throw new NotSupportedException(string.Format("Editing .carbin type {0} is not supported yet (only FM4 type 2: main, lod0 and cockpit models).", this.Type));
            }
            this.position += 0x15c;
            uint records = this.U32();
            this.position += 4 + ((int) records * 140) + 0x340;
            uint tables = this.U32() * 2;
            for (int i = 0; i < tables; i++)
            {
                uint length = this.U32();
                this.position += (int) length * 4;
            }
            this.position += 4;

            uint count = this.U32();
            for (int s = 0; s < count; s++)
            {
                Section section = new Section();
                section.Offset = this.position;
                uint marker = this.U32();
                if (marker != 0x2c9 && marker != 2 && marker != 5)
                {
                    this.position -= 4;
                }
                section.BoundsOffset = this.position;
                for (int c = 0; c < 3; c++)
                {
                    section.Origin[c] = this.F32At(this.position + (c * 4));
                    section.Min[c] = this.F32At(this.position + 12 + (c * 4));
                    section.Max[c] = this.F32At(this.position + 24 + (c * 4));
                }
                this.position += 36 + 0x1c;
                uint a = this.U32();
                this.position += ((int) a * 16) + 4;
                uint b = this.U32();
                this.position += ((int) b * 2) + 4 + 8;
                int nameLength = this.data[this.position];
                section.Name = Encoding.ASCII.GetString(this.data, this.position + 1, nameLength);
                this.position += 1 + nameLength + 4;

                VertexBuffer lod = this.ReadBuffer(false);
                this.position += 4;
                uint subs = this.U32();
                for (int k = 0; k < subs; k++)
                {
                    SubSection sub = new SubSection();
                    this.position += 5;
                    sub.UvParamsOffset = this.position;
                    this.position += 32 + 0x24;
                    int subNameLength = (int) this.U32();
                    sub.Name = Encoding.ASCII.GetString(this.data, this.position, subNameLength);
                    this.position += subNameLength;
                    sub.Lod = (int) this.U32();
                    sub.IndexType = (int) this.U32();
                    this.position += 56 + 4;
                    sub.IndexCount = (int) this.U32();
                    sub.IndexSize = (int) this.U32();
                    sub.IndexOffset = this.position;
                    this.position += (sub.IndexCount * sub.IndexSize) + 4;
                    section.SubSections.Add(sub);
                }
                this.position += 4;
                VertexBuffer lod0 = this.ReadBuffer(true);
                section.IsDeformable = this.data[this.position + 4] != 0;
                this.position += 9;
                uint c1 = this.U32();
                uint c2 = this.U32();
                this.position += (int) (c1 * c2) + 4;
                uint d1 = this.U32();
                uint d2 = this.U32();
                this.position += (int) (d1 * d2);
                if (lod.Count > 0)
                {
                    section.Buffers.Add(lod);
                }
                if (lod0.Count > 0)
                {
                    section.Buffers.Add(lod0);
                }
                this.Sections.Add(section);
            }
            if (this.position > this.data.Length)
            {
                throw new InvalidDataException("Unexpected .carbin layout.");
            }
        }

        private VertexBuffer ReadBuffer(bool isLod0)
        {
            VertexBuffer buffer = new VertexBuffer();
            buffer.IsLod0 = isLod0;
            buffer.Count = (int) this.U32();
            buffer.Stride = (int) this.U32();
            if (buffer.Stride == 0x1c)
            {
                this.position += 8;
            }
            if (buffer.Count > 0 && buffer.Stride != 32)
            {
                throw new NotSupportedException(string.Format("Unsupported vertex size {0}.", buffer.Stride));
            }
            buffer.Offset = this.position;
            this.position += buffer.Count * buffer.Stride;
            return buffer;
        }

        public Section FindSection(string name)
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

        #endregion

        #region Positions

        private short S16(int offset)
        {
            return (short) ((this.data[offset] << 8) | this.data[offset + 1]);
        }

        private void WriteS16(int offset, int value)
        {
            this.data[offset] = (byte) (value >> 8);
            this.data[offset + 1] = (byte) value;
        }

        // Model-space positions, 3 doubles per vertex.
        public double[] GetPositions(Section section, VertexBuffer buffer)
        {
            double scale = section.Scale;
            double[] positions = new double[buffer.Count * 3];
            for (int i = 0; i < buffer.Count; i++)
            {
                int o = buffer.Offset + (i * buffer.Stride);
                double w = this.S16(o + 6) / 32767.0;
                for (int c = 0; c < 3; c++)
                {
                    double center = (section.Min[c] + section.Max[c]) / 2;
                    positions[(i * 3) + c] = section.Origin[c] + center + ((this.S16(o + (c * 2)) / 32767.0) * w * scale);
                }
            }
            return positions;
        }

        // Writes new positions. Vertices whose position is unchanged keep their bytes. If a vertex leaves
        // the section bounds the bounds grow (scale changes) and every vertex of the section is re-encoded.
        public void SetPositions(Section section, VertexBuffer buffer, double[] positions)
        {
            if (positions.Length != buffer.Count * 3)
            {
                throw new ArgumentException("Position count does not match the vertex buffer.");
            }
            double[] old = this.GetPositions(section, buffer);
            // The stored bounds are the vertices' AABB up to 16 bit quantization: overshoots within
            // two steps are noise, and the encoder clamps them onto the bounds.
            double tolerance = section.Scale * (2.0 / 32767);
            bool grow = false;
            for (int i = 0; i < buffer.Count && !grow; i++)
            {
                for (int c = 0; c < 3; c++)
                {
                    double local = positions[(i * 3) + c] - section.Origin[c];
                    if (local < section.Min[c] - tolerance || local > section.Max[c] + tolerance)
                    {
                        grow = true;
                    }
                }
            }
            if (!grow)
            {
                this.EncodePositions(section, buffer, positions, old);
                this.UpdateCentroid(section);
                return;
            }

            // New origin and bounds in the game's convention (all original sections): origin = center of
            // the vertices' AABB, bounds symmetric around it. The game decodes positions relative to the
            // origin only, so asymmetric bounds shift the part. Every vertex is re-encoded.
            Dictionary<VertexBuffer, double[]> all = new Dictionary<VertexBuffer, double[]>();
            double[] min = { double.MaxValue, double.MaxValue, double.MaxValue };
            double[] max = { double.MinValue, double.MinValue, double.MinValue };
            foreach (VertexBuffer other in section.Buffers)
            {
                double[] values = (other == buffer) ? positions : this.GetPositions(section, other);
                all[other] = values;
                for (int i = 0; i < other.Count; i++)
                {
                    for (int c = 0; c < 3; c++)
                    {
                        min[c] = Math.Min(min[c], values[(i * 3) + c]);
                        max[c] = Math.Max(max[c], values[(i * 3) + c]);
                    }
                }
            }
            float[] origin;
            float[] half;
            CarbinModel.SymmetricBounds(min, max, out origin, out half);
            for (int c = 0; c < 3; c++)
            {
                section.Origin[c] = origin[c];
                section.Min[c] = -half[c];
                section.Max[c] = half[c];
                this.WriteF32At(section.BoundsOffset + (c * 4), origin[c]);
                this.WriteF32At(section.BoundsOffset + 12 + (c * 4), -half[c]);
                this.WriteF32At(section.BoundsOffset + 24 + (c * 4), half[c]);
            }
            foreach (KeyValuePair<VertexBuffer, double[]> pair in all)
            {
                this.EncodePositions(section, pair.Key, pair.Value, null);
            }
            this.UpdateCentroid(section);
        }

        // The 3 floats after the bounds' 0x1c block start (BoundsOffset + 36 + 4) are the mean vertex
        // position relative to the origin, of the lod0 buffer when present (verified on all originals).
        private void UpdateCentroid(Section section)
        {
            VertexBuffer primary = null;
            foreach (VertexBuffer candidate in section.Buffers)
            {
                if (primary == null || candidate.IsLod0)
                {
                    primary = candidate;
                }
            }
            if (primary == null || primary.Count == 0)
            {
                return;
            }
            double[] positions = this.GetPositions(section, primary);
            for (int c = 0; c < 3; c++)
            {
                double sum = 0;
                for (int i = 0; i < primary.Count; i++)
                {
                    sum += positions[(i * 3) + c];
                }
                this.WriteF32At(section.BoundsOffset + 40 + (c * 4), (float) ((sum / primary.Count) - section.Origin[c]));
            }
        }

        // Applies an affine transform (XNA row-vector convention: p' = p * m) to every vertex of the
        // section: positions, and the tangent-frame quaternion so normals follow the rotation (and the
        // inverse-transpose for non-uniform scale). Bounds are refitted around the new vertices.
        public void TransformSection(Section section, Microsoft.Xna.Framework.Matrix m)
        {
            Microsoft.Xna.Framework.Quaternion rotation;
            Microsoft.Xna.Framework.Matrix normalMatrix;
            CarbinModel.FrameTransform(m, out rotation, out normalMatrix);

            Dictionary<VertexBuffer, double[]> all = new Dictionary<VertexBuffer, double[]>();
            foreach (VertexBuffer buffer in section.Buffers)
            {
                double[] positions = this.GetPositions(section, buffer);
                for (int i = 0; i < buffer.Count; i++)
                {
                    Microsoft.Xna.Framework.Vector3 p = new Microsoft.Xna.Framework.Vector3((float) positions[i * 3], (float) positions[(i * 3) + 1], (float) positions[(i * 3) + 2]);
                    p = Microsoft.Xna.Framework.Vector3.Transform(p, m);
                    positions[i * 3] = p.X;
                    positions[(i * 3) + 1] = p.Y;
                    positions[(i * 3) + 2] = p.Z;
                    this.TransformFrame(buffer.Offset + (i * buffer.Stride) + 16, rotation, normalMatrix);
                }
                all[buffer] = positions;
            }

            double[] min = { double.MaxValue, double.MaxValue, double.MaxValue };
            double[] max = { double.MinValue, double.MinValue, double.MinValue };
            foreach (double[] values in all.Values)
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
            CarbinModel.SymmetricBounds(min, max, out origin, out half);
            for (int c = 0; c < 3; c++)
            {
                section.Origin[c] = origin[c];
                section.Min[c] = -half[c];
                section.Max[c] = half[c];
                this.WriteF32At(section.BoundsOffset + (c * 4), origin[c]);
                this.WriteF32At(section.BoundsOffset + 12 + (c * 4), -half[c]);
                this.WriteF32At(section.BoundsOffset + 24 + (c * 4), half[c]);
            }
            foreach (KeyValuePair<VertexBuffer, double[]> pair in all)
            {
                this.EncodePositions(section, pair.Key, pair.Value, null);
            }
            this.UpdateCentroid(section);
        }

        private void TransformFrame(int offset, Microsoft.Xna.Framework.Quaternion rotation, Microsoft.Xna.Framework.Matrix normalMatrix)
        {
            CarbinModel.TransformFrame(this.data, offset, rotation, normalMatrix);
        }

        private void EncodePositions(Section section, VertexBuffer buffer, double[] positions, double[] unchangedIf)
        {
            double scale = section.Scale;
            for (int i = 0; i < buffer.Count; i++)
            {
                if (unchangedIf != null && positions[i * 3] == unchangedIf[i * 3] && positions[(i * 3) + 1] == unchangedIf[(i * 3) + 1] && positions[(i * 3) + 2] == unchangedIf[(i * 3) + 2])
                {
                    continue;
                }
                double[] q = new double[3];
                double length = 0;
                for (int c = 0; c < 3; c++)
                {
                    double center = (section.Min[c] + section.Max[c]) / 2;
                    q[c] = (positions[(i * 3) + c] - section.Origin[c] - center) / scale;
                    length += q[c] * q[c];
                }
                length = Math.Sqrt(length);
                int o = buffer.Offset + (i * buffer.Stride);
                if (length < 1e-12)
                {
                    this.WriteS16(o, 32767);
                    this.WriteS16(o + 2, 0);
                    this.WriteS16(o + 4, 0);
                    this.WriteS16(o + 6, 0);
                    continue;
                }
                for (int c = 0; c < 3; c++)
                {
                    this.WriteS16(o + (c * 2), Clamp((int) Math.Round((q[c] / length) * 32767)));
                }
                this.WriteS16(o + 6, Clamp((int) Math.Round(Math.Min(length, 1.0) * 32767)));
            }
        }

        private static int Clamp(int value)
        {
            return Math.Max(-32767, Math.Min(32767, value));
        }

        #endregion
    }
}
