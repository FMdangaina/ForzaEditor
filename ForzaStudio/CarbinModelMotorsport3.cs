namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Microsoft.Xna.Framework;

    // FM3 -> FM4 conversion of a .carbin read with CarbinModel(data, true). Kept apart from the FM4 and
    // FH code: it only runs on IsMotorsport3 models and leaves them as plain FM4 models.
    //
    // Measured on the Supra RZ '98 (both games) and the 119 cars, 244 wheels and 120 Media\brakes files
    // the two games share (fm4dev\FM3_FM4: supracmp, vcorr, structcmp, headscan, wheelrule):
    //  - header: the format block [0xD, 0xB, 1, N] becomes [0x10, 0x10, 1, N]; the N records of 140
    //    bytes and the next 0x114 bytes stay; FM3's 4 blocks of 0x24 bytes become 15 (the 11 new ones
    //    hold the default values 0,0,0,1,0,0,1,0,0); the two floats at the end of the last block (x and
    //    y = -2x, about -0.33) stay, and brakes, where FM3 has 0, get FM4's usual brake values; before the
    //    final (4, 4) FM4 adds (1, 1, 0) on cars, (0, 1, 0) on brakes and wheels. In front of the format
    //    block FM4 cars have 0x50 more bytes (leftover memory; the word at 0x150 is 1) and wheels 12
    //    floats measured on the wheel (see WheelMeasures);
    //  - sections: marker 2 -> 5; FM3's word before VbTest2 is FM4's Pre word (VbTest1 then 0); Post9
    //    (deformable flag), the damage tables and Mid are added; subsections get Pre5 0000000300, their
    //    UV offset/scale and the fixed 36 bytes;
    //  - vertices, 40 bytes on car parts: 4 halves position (origin + xyz * w), uv0 and uv1 as halves,
    //    the normal (u32 11:11:10), the 4 S16 values FM4 keeps at 24..31, the FM4 damage word, a word FM4
    //    dropped and the tangent frame's Z axis (11:11:10). A part is deformable in FM4 exactly when its
    //    damage words are not all 000003FF (115 of 115 Supra parts). 16 bytes (brakes): position, uv0,
    //    normal. 44 bytes (wheels): position, uv0, normal, the 4 S16 values, then constants.
    public partial class CarbinModel
    {
        private const uint Motorsport3PlainDamage = 0x000003FF;

        // FM4's usual end floats of brake headers (x, y = -2x), where FM3 brakes have 0.
        private const float BrakeEndX = -0.3265f;
        private const float BrakeEndY = 0.6550f;

        public string ConvertMotorsport3ToMotorsport4()
        {
            if (!this.IsMotorsport3)
            {
                throw new InvalidOperationException("Not a Forza Motorsport 3 model.");
            }
            System.Text.StringBuilder report = new System.Text.StringBuilder();
            bool brake = this.Type == 1 && ReadU32(this.Head, 4) == 0xD;
            bool wheel = this.Type == 2;
            // The header first: the wheel measures are read from the FM3 vertices.
            this.Head = this.ConvertMotorsport3Head(brake, wheel);
            foreach (Section section in this.Sections)
            {
                this.ConvertMotorsport3Section(section, brake, report);
            }
            this.Type = wheel ? 3u : (brake ? 1u : 2u);
            this.motorsport3 = false;
            return report.ToString();
        }

        private byte[] ConvertMotorsport3Head(bool brake, bool wheel)
        {
            byte[] head = this.Head;
            int format = brake ? 4 : (wheel ? 0x18 : 0x104);
            int records = (int) ReadU32(head, format + 12);
            int tail = format + 16 + (records * 140);
            if (ReadU32(head, format) != 0xD || ReadU32(head, format + 4) != 0xB || head.Length != tail + 0x1AC)
            {
                throw new NotSupportedException("FM3 header layout not recognised.");
            }
            using (MemoryStream output = new MemoryStream())
            {
                byte[] prefix = new byte[format];
                Buffer.BlockCopy(head, 0, prefix, 0, format);
                prefix[3] = (byte) (wheel ? 3 : (brake ? 1 : 2));
                output.Write(prefix, 0, prefix.Length);
                if (wheel)
                {
                    foreach (float value in this.WheelMeasures())
                    {
                        WriteF32(output, value);
                    }
                }
                else if (!brake)
                {
                    output.Write(new byte[0x4C], 0, 0x4C);
                    WriteU32(output, 1);
                }
                WriteU32(output, 0x10);
                WriteU32(output, 0x10);
                output.Write(head, format + 8, 8 + (records * 140) + 0x114);
                // Blocks: FM3's four without the end floats, then 11 default ones, then the end floats.
                int blocks = tail + 0x114;
                output.Write(head, blocks, 0x88);
                for (int i = 0; i < 11; i++)
                {
                    foreach (float value in new float[] { 0, 0, 0, 0, 0, 1, 0, 0, 1 })
                    {
                        WriteF32(output, value);
                    }
                }
                float x = ReadF32(head, blocks + 0x88);
                float y = ReadF32(head, blocks + 0x8C);
                if (brake && x == 0 && y == 0)
                {
                    x = BrakeEndX;
                    y = BrakeEndY;
                }
                WriteF32(output, x);
                WriteF32(output, y);
                WriteU32(output, (brake || wheel) ? 0u : 1u);
                WriteU32(output, 1);
                WriteU32(output, 0);
                output.Write(head, blocks + 0x90, 8);
                return output.ToArray();
            }
        }

        private static float ReadF32(byte[] data, int offset)
        {
            return BitConverter.ToSingle(BitConverter.GetBytes(ReadU32(data, offset)), 0);
        }

        private void ConvertMotorsport3Section(Section section, bool brake, System.Text.StringBuilder report)
        {
            double[] lodPositions = Motorsport3Positions(section, section.Lod);
            double[] lod0Positions = Motorsport3Positions(section, section.Lod0);
            if (IsZeroBounds(section.Bounds))
            {
                // FM3 brakes carry no bounds; FM4's are centred on the vertices.
                SetBounds(section, lodPositions, lod0Positions);
            }

            bool deformable = HasMotorsport3Damage(section.Lod) || HasMotorsport3Damage(section.Lod0);
            section.DamageLod = deformable ? Motorsport3Damage(section.Lod) : new Table();
            section.DamageLod0 = deformable ? Motorsport3Damage(section.Lod0) : new Table();

            // UV offset/scale per group of subsections sharing vertices (the LODs of a material can).
            foreach (SubSection sub in section.SubSections)
            {
                sub.Pre5 = new byte[] { 0, 0, 0, 3, 0 };
                sub.Fixed36 = new byte[0x24];
                sub.Fixed36[0x23] = 2;
                sub.Uv = new float[8];
            }
            int[] uvOf = new int[0];
            int[] uv0Of = new int[0];
            List<float[]> lodUv = Motorsport3UvGroups(section, false, out uvOf);
            List<float[]> lod0Uv = Motorsport3UvGroups(section, true, out uv0Of);

            section.Lod = this.Motorsport3Vertices(section, section.Lod, lodPositions, false, lodUv, uvOf, brake);
            section.Lod0 = this.Motorsport3Vertices(section, section.Lod0, lod0Positions, true, lod0Uv, uv0Of, brake);

            section.Marker = new byte[] { 0, 0, 0, 5 };
            section.Unk1c[0] = 0x3F;
            section.Unk1c[1] = 0x80;
            section.Unk1c[2] = 0;
            section.Unk1c[3] = 0;
            // Then the mean vertex position relative to the origin (FM4 recomputed it on re-exported parts).
            double[] mean = (lod0Positions.Length > 0) ? lod0Positions : lodPositions;
            if (mean.Length > 0)
            {
                for (int c = 0; c < 3; c++)
                {
                    double sum = 0;
                    for (int i = c; i < mean.Length; i += 3)
                    {
                        sum += mean[i];
                    }
                    byte[] value = BitConverter.GetBytes((float) ((sum / (mean.Length / 3)) - section.Bounds[c]));
                    for (int k = 0; k < 4; k++)
                    {
                        section.Unk1c[4 + (c * 4) + k] = value[3 - k];
                    }
                }
            }
            section.Pre = new byte[] { (byte) (section.VbTest1 >> 24), (byte) (section.VbTest1 >> 16), (byte) (section.VbTest1 >> 8), (byte) section.VbTest1 };
            section.VbTest1 = 0;
            section.Post9 = new byte[] { 0, 0, 0, 1, (byte) (deformable ? 1 : 0), 0, 0, 0, 2 };
            section.Mid = new byte[] { 0, 0, 0, 2 };
            report.AppendFormat("{0}: lod {1} lod0 {2} vertices{3}\r\n", section.Name, section.Lod.Count, section.Lod0.Count, deformable ? ", deformable" : "");
        }

        private static bool IsZeroBounds(float[] bounds)
        {
            foreach (float value in bounds)
            {
                if (value != 0)
                {
                    return false;
                }
            }
            return true;
        }

        private static void SetBounds(Section section, double[] a, double[] b)
        {
            double[] lo = { double.MaxValue, double.MaxValue, double.MaxValue };
            double[] hi = { double.MinValue, double.MinValue, double.MinValue };
            foreach (double[] p in new double[][] { a, b })
            {
                for (int i = 0; i < p.Length; i++)
                {
                    lo[i % 3] = Math.Min(lo[i % 3], p[i]);
                    hi[i % 3] = Math.Max(hi[i % 3], p[i]);
                }
            }
            if (lo[0] > hi[0])
            {
                return;
            }
            for (int c = 0; c < 3; c++)
            {
                double half = (hi[c] - lo[c]) / 2;
                section.Bounds[c] = (float) ((hi[c] + lo[c]) / 2);
                section.Bounds[3 + c] = (float) -half;
                section.Bounds[6 + c] = (float) half;
            }
        }

        private static double Half(byte[] d, int o)
        {
            int h = (d[o] << 8) | d[o + 1];
            int e = (h >> 10) & 31;
            int m = h & 1023;
            double v = (e == 0) ? (m / 1024.0) * Math.Pow(2, -14) : (e == 31 ? 0 : (1 + (m / 1024.0)) * Math.Pow(2, e - 15));
            return ((h & 0x8000) != 0) ? -v : v;
        }

        // Packed 11:11:10 vector: x bits 0-10, y bits 11-21 (signed, /1023), z bits 22-31 (signed, /511).
        private static Vector3 Packed111110(byte[] d, int o)
        {
            uint w = ReadU32(d, o);
            int x = (int) (w & 0x7FF);
            int y = (int) ((w >> 11) & 0x7FF);
            int z = (int) (w >> 22);
            if (x >= 0x400)
            {
                x -= 0x800;
            }
            if (y >= 0x400)
            {
                y -= 0x800;
            }
            if (z >= 0x200)
            {
                z -= 0x400;
            }
            return new Vector3(x / 1023f, y / 1023f, z / 511f);
        }

        private static double[] Motorsport3Positions(Section section, VertexBuffer buffer)
        {
            double[] positions = new double[buffer.Count * 3];
            for (int i = 0; i < buffer.Count; i++)
            {
                int o = i * buffer.Stride;
                double w = Half(buffer.Data, o + 6);
                for (int c = 0; c < 3; c++)
                {
                    double center = (section.Bounds[3 + c] + section.Bounds[6 + c]) / 2;
                    positions[(i * 3) + c] = section.Bounds[c] + center + (Half(buffer.Data, o + (c * 2)) * w);
                }
            }
            return positions;
        }

        private static bool HasMotorsport3Damage(VertexBuffer buffer)
        {
            if (buffer.Stride != 40)
            {
                return false;
            }
            for (int i = 0; i < buffer.Count; i++)
            {
                if (ReadU32(buffer.Data, (i * 40) + 28) != Motorsport3PlainDamage)
                {
                    return true;
                }
            }
            return false;
        }

        private static Table Motorsport3Damage(VertexBuffer buffer)
        {
            Table table = new Table();
            if (buffer.Count == 0)
            {
                return table;
            }
            table.Count = buffer.Count;
            table.Stride = 4;
            table.Data = new byte[buffer.Count * 4];
            for (int i = 0; i < buffer.Count; i++)
            {
                Buffer.BlockCopy(buffer.Data, (i * 40) + 28, table.Data, i * 4, 4);
            }
            table.ExtraFor = table.Data;
            return table;
        }

        // Groups of subsections of one buffer (Lod0 or Lod) that share vertices, with their UV ranges:
        // per group 8 floats (offset/scale of U, V, U2, V2) as FM4 stores them per subsection.
        private static List<float[]> Motorsport3UvGroups(Section section, bool lod0, out int[] groupOf)
        {
            VertexBuffer buffer = lod0 ? section.Lod0 : section.Lod;
            List<SubSection> subs = new List<SubSection>();
            foreach (SubSection sub in section.SubSections)
            {
                if ((sub.Lod == 0) == lod0)
                {
                    subs.Add(sub);
                }
            }
            int[] parent = new int[subs.Count];
            for (int i = 0; i < parent.Length; i++)
            {
                parent[i] = i;
            }
            int[] owner = new int[buffer.Count];
            for (int i = 0; i < owner.Length; i++)
            {
                owner[i] = -1;
            }
            for (int s = 0; s < subs.Count; s++)
            {
                foreach (int index in subs[s].Indices)
                {
                    if (IsRestart(subs[s], index) || index >= buffer.Count)
                    {
                        continue;
                    }
                    if (owner[index] < 0)
                    {
                        owner[index] = s;
                    }
                    else
                    {
                        int a = Root(parent, owner[index]);
                        int b = Root(parent, s);
                        parent[a] = b;
                    }
                }
            }
            int uvSets = (buffer.Stride == 40) ? 2 : 1;
            Dictionary<int, int> groupIndex = new Dictionary<int, int>();
            List<float[]> ranges = new List<float[]>();
            int[] subGroup = new int[subs.Count];
            for (int s = 0; s < subs.Count; s++)
            {
                int root = Root(parent, s);
                int g;
                if (!groupIndex.TryGetValue(root, out g))
                {
                    g = ranges.Count;
                    groupIndex[root] = g;
                    ranges.Add(new float[] { float.MaxValue, float.MinValue, float.MaxValue, float.MinValue, float.MaxValue, float.MinValue, float.MaxValue, float.MinValue });
                }
                subGroup[s] = g;
            }
            groupOf = new int[buffer.Count];
            for (int i = 0; i < buffer.Count; i++)
            {
                groupOf[i] = (owner[i] < 0) ? -1 : subGroup[owner[i]];
                if (groupOf[i] < 0)
                {
                    continue;
                }
                float[] r = ranges[groupOf[i]];
                for (int channel = 0; channel < uvSets * 2; channel++)
                {
                    float value = (float) Half(buffer.Data, (i * buffer.Stride) + 8 + (channel * 2));
                    r[channel * 2] = Math.Min(r[channel * 2], value);
                    r[(channel * 2) + 1] = Math.Max(r[(channel * 2) + 1], value);
                }
            }
            List<float[]> parameters = new List<float[]>();
            foreach (float[] r in ranges)
            {
                float[] uv = new float[8];
                for (int channel = 0; channel < 4; channel++)
                {
                    if (channel < uvSets * 2 && r[channel * 2] <= r[(channel * 2) + 1])
                    {
                        uv[channel * 2] = r[channel * 2];
                        uv[(channel * 2) + 1] = Math.Max(r[(channel * 2) + 1] - r[channel * 2], 1e-6f);
                    }
                }
                parameters.Add(uv);
            }
            for (int s = 0; s < subs.Count; s++)
            {
                subs[s].Uv = (float[]) parameters[subGroup[s]].Clone();
            }
            return parameters;
        }

        private static int Root(int[] parent, int i)
        {
            while (parent[i] != i)
            {
                parent[i] = parent[parent[i]];
                i = parent[i];
            }
            return i;
        }

        // FM3 vertices (40, 16 or 44 bytes) -> FM4 32-byte vertices.
        private VertexBuffer Motorsport3Vertices(Section section, VertexBuffer buffer, double[] positions, bool lod0, List<float[]> uvGroups, int[] groupOf, bool brake)
        {
            VertexBuffer result = new VertexBuffer();
            if (buffer.Count == 0)
            {
                return result;
            }
            int stride = buffer.Stride;
            if (stride != 40 && stride != 16 && stride != 44)
            {
                throw new NotSupportedException(string.Format("{0}: FM3 vertex size {1} is not known.", section.Name, stride));
            }
            result.Count = buffer.Count;
            result.Stride = 0x20;
            result.Data = new byte[buffer.Count * 0x20];
            Encode(section, result, positions);

            // Without the stored frame axis (16- and 44-byte vertices), dP/dv from the UVs of the faces.
            Vector3[] bitangents = null;
            if (stride != 40)
            {
                bitangents = new Vector3[buffer.Count];
                foreach (SubSection sub in section.SubSections)
                {
                    if ((sub.Lod == 0) != lod0)
                    {
                        continue;
                    }
                    foreach (int[] face in StripTriangles(sub))
                    {
                        if (face[0] >= buffer.Count || face[1] >= buffer.Count || face[2] >= buffer.Count)
                        {
                            continue;
                        }
                        Vector3[] p = new Vector3[3];
                        float[] u = new float[3];
                        float[] v = new float[3];
                        for (int k = 0; k < 3; k++)
                        {
                            int i = face[k];
                            p[k] = new Vector3((float) positions[i * 3], (float) positions[(i * 3) + 1], (float) positions[(i * 3) + 2]);
                            u[k] = (float) Half(buffer.Data, (i * stride) + 8);
                            v[k] = (float) Half(buffer.Data, (i * stride) + 10);
                        }
                        Vector3 e1 = p[1] - p[0];
                        Vector3 e2 = p[2] - p[0];
                        float du1 = u[1] - u[0];
                        float dv1 = v[1] - v[0];
                        float du2 = u[2] - u[0];
                        float dv2 = v[2] - v[0];
                        float det = (du1 * dv2) - (du2 * dv1);
                        if (Math.Abs(det) < 1e-12f)
                        {
                            continue;
                        }
                        Vector3 b = ((e2 * du1) - (e1 * du2)) / det;
                        foreach (int i in face)
                        {
                            bitangents[i] += b;
                        }
                    }
                }
            }

            int uvSets = (stride == 40) ? 2 : 1;
            for (int i = 0; i < buffer.Count; i++)
            {
                int o = i * stride;
                byte[] vertex = new byte[0x20];
                Buffer.BlockCopy(result.Data, i * 0x20, vertex, 0, 8);
                float[] uv = (groupOf[i] >= 0) ? uvGroups[groupOf[i]] : new float[] { 0, 1, 0, 1, 0, 1, 0, 1 };
                for (int channel = 0; channel < uvSets * 2; channel++)
                {
                    double value = Half(buffer.Data, o + 8 + (channel * 2));
                    double scale = uv[(channel * 2) + 1];
                    double normalized = (scale > 0) ? (value - uv[channel * 2]) / scale : 0;
                    int raw = Math.Max(0, Math.Min(65535, (int) Math.Round(normalized * 65535)));
                    vertex[8 + (channel * 2)] = (byte) (raw >> 8);
                    vertex[9 + (channel * 2)] = (byte) raw;
                }
                Vector3 normal = Packed111110(buffer.Data, o + ((stride == 40) ? 16 : 12));
                if (stride == 40)
                {
                    WriteFrame(vertex, normal, Vector3.Zero, -Packed111110(buffer.Data, o + 36));
                    Buffer.BlockCopy(buffer.Data, o + 20, vertex, 24, 8);
                }
                else
                {
                    WriteFrame(vertex, normal, Vector3.Zero, bitangents[i]);
                    if (stride == 44)
                    {
                        Buffer.BlockCopy(buffer.Data, o + 16, vertex, 24, 8);
                    }
                    else
                    {
                        WriteTail(vertex, normal, 0x20);
                    }
                }
                Buffer.BlockCopy(vertex, 0, result.Data, i * 0x20, 0x20);
            }
            return result;
        }

        // The 12 floats FM4 wheel headers hold at 0x18 (fitted on FM4's 437 wheels; the material names
        // are the game's): wheel_black (lod0) min x, inner_rim (lod1) min x, lod0 max x, inner_rim (lod1)
        // max x, wheel_black min x again, tire (lod1) min x, wheel_black max x, tire max x, two radii
        // whose rule is not known (the lod0 radius is used: right on a third of the wheels), inner_rim
        // (lod1) radius, tire radius. A missing material falls back on the whole LOD.
        private float[] WheelMeasures()
        {
            Section section = (this.Sections.Count > 0) ? this.Sections[0] : null;
            if (section == null)
            {
                return new float[12];
            }
            double[] lod = Motorsport3Positions(section, section.Lod);
            double[] lod0 = Motorsport3Positions(section, section.Lod0);
            double[] black = this.WheelMaterial(section, "wheel_black", true, lod, lod0);
            double[] inner = this.WheelMaterial(section, "inner_rim", false, lod, lod0);
            double[] tire = this.WheelMaterial(section, "tire", false, lod, lod0);
            double[] all0 = this.WheelMaterial(section, null, true, lod, lod0);
            return new float[]
            {
                (float) black[0], (float) inner[0], (float) all0[1], (float) inner[1],
                (float) black[0], (float) tire[0], (float) black[1], (float) tire[1],
                (float) all0[2], (float) inner[2], (float) all0[2], (float) tire[2],
            };
        }

        // Min x, max x and largest YZ radius of a material's vertices (null: every material) at LOD 0
        // (lod0) or 1; the whole LOD when the material is not there.
        private double[] WheelMaterial(Section section, string material, bool lod0, double[] lod, double[] lod0Positions)
        {
            double[] result = { double.MaxValue, double.MinValue, 0 };
            for (int pass = 0; pass < 2 && result[0] > result[1]; pass++)
            {
                foreach (SubSection sub in section.SubSections)
                {
                    if (sub.Lod != (lod0 ? 0u : 1u) || (pass == 0 && material != null && sub.Name != material))
                    {
                        continue;
                    }
                    double[] p = lod0 ? lod0Positions : lod;
                    int count = p.Length / 3;
                    foreach (int index in sub.Indices)
                    {
                        if (IsRestart(sub, index) || index >= count)
                        {
                            continue;
                        }
                        result[0] = Math.Min(result[0], p[index * 3]);
                        result[1] = Math.Max(result[1], p[index * 3]);
                        result[2] = Math.Max(result[2], Math.Sqrt((p[(index * 3) + 1] * p[(index * 3) + 1]) + (p[(index * 3) + 2] * p[(index * 3) + 2])));
                    }
                }
            }
            if (result[0] > result[1])
            {
                return new double[3];
            }
            return result;
        }
    }
}
