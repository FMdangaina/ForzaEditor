/*
    This is applicate to all source code of all project
    
    Forza Studio: Forza Motorsport resource extraction tool.

    Copyright (C) 2012 Mike Davis (http://codeescape.com/)

    Permission is hereby granted, free of charge, to any person 
    obtaining a copy of this software and associated documentation
    files (the "Software"), to deal in the Software without restriction,
    including without limitation the rights to use, copy, modify, merge,
    publish, distribute, sublicense, and/or sell copies of the Software,
    and to permit persons to whom the Software is furnished to do so,
    subject to the following conditions:

    The above copyright notice and this permission notice shall be included 
    in all copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS
    OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
    THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
    FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
    DEALINGS IN THE SOFTWARE.
*/

using System;
using Microsoft.Xna.Framework;

namespace ForzaStudio
{
    // Token: 0x02000003 RID: 3
    public class ForzaTrackSection
    {
        // Token: 0x06000005 RID: 5 RVA: 0x000020A8 File Offset: 0x000002A8
        public ForzaTrackSection(ForzaTrackBin parent)
        {
            this.Parent = parent;
            this.Stream = parent.Stream;
            Utilities.AssertEquals(this.Stream.ReadUInt32(), 1u);
            new Vector3(this.Stream.ReadSingle(), this.Stream.ReadSingle(), this.Stream.ReadSingle());
            Utilities.AssertEquals(this.Stream.ReadSingle(), 0f);
            new Vector3(this.Stream.ReadSingle(), this.Stream.ReadSingle(), this.Stream.ReadSingle());
            Utilities.AssertEquals(this.Stream.ReadSingle(), 0f);
            new Vector3(this.Stream.ReadSingle(), this.Stream.ReadSingle(), this.Stream.ReadSingle());
            Utilities.AssertEquals(this.Stream.ReadSingle(), 0f);
            Utilities.AssertEquals(this.Stream.ReadUInt32(), 1u);
            this.Name = this.Stream.ReadASCII(this.Stream.ReadInt32()).ToLowerInvariant();
            string[] array = this.Name.Split(new char[]
			{
				'_'
			});
            this.Type = array[0].ToLowerInvariant();
            this.Name = this.Name.Substring(this.Name.IndexOf("_") + 1);
            Utilities.AssertEquals(this.Stream.ReadUInt32(), 2u);
            uint num = this.Stream.ReadUInt32();
            uint size = this.Stream.ReadUInt32();
            ForzaVertex[] array2 = new ForzaVertex[num];
            int num2 = 0;
            while ((long)num2 < (long)((ulong)num))
            {
                array2[num2] = new ForzaVertex(parent.Version, ForzaVertexType.Track, this.Stream, size);
                num2++;
            }
            Utilities.AssertEquals(this.Stream.ReadUInt32(), 1u);
            BoundingBox.FromVertices(array2);
            this.SubSections = new ForzaTrackSubSection[this.Stream.ReadUInt32()];
            for (int i = 0; i < this.SubSections.Length; i++)
            {
                ForzaTrackSubSection forzaTrackSubSection = new ForzaTrackSubSection(this);
                forzaTrackSubSection.Vertices = Utilities.GenerateVertices(array2, ref forzaTrackSubSection.Indices);
                for (int j = 0; j < forzaTrackSubSection.Vertices.Length; j++)
                {
                    ForzaVertex[] expr_229_cp_0 = forzaTrackSubSection.Vertices;
                    int expr_229_cp_1 = j;
                    expr_229_cp_0[expr_229_cp_1].texture0 = expr_229_cp_0[expr_229_cp_1].texture0 * forzaTrackSubSection.UVtile;
                    ForzaVertex[] expr_24E_cp_0 = forzaTrackSubSection.Vertices;
                    int expr_24E_cp_1 = j;
                    expr_24E_cp_0[expr_24E_cp_1].texture1 = expr_24E_cp_0[expr_24E_cp_1].texture1 * forzaTrackSubSection.UVtile;
                    ForzaVertex[] expr_273_cp_0 = forzaTrackSubSection.Vertices;
                    int expr_273_cp_1 = j;
                    expr_273_cp_0[expr_273_cp_1].texture0 = expr_273_cp_0[expr_273_cp_1].texture0 + forzaTrackSubSection.UVoffset;
                    ForzaVertex[] expr_298_cp_0 = forzaTrackSubSection.Vertices;
                    int expr_298_cp_1 = j;
                    expr_298_cp_0[expr_298_cp_1].texture1 = expr_298_cp_0[expr_298_cp_1].texture1 + forzaTrackSubSection.UVoffset;
                    forzaTrackSubSection.Vertices[j].texture0.Y = 1f - forzaTrackSubSection.Vertices[j].texture0.Y;
                    forzaTrackSubSection.Vertices[j].texture1.Y = 1f - forzaTrackSubSection.Vertices[j].texture1.Y;
                }
                if (forzaTrackSubSection.IndexType == IndexType.TriStrip)
                {
                    forzaTrackSubSection.Indices = Utilities.GenerateTriangleList(forzaTrackSubSection.Indices, forzaTrackSubSection.FaceCount);
                }
                this.SubSections[i] = forzaTrackSubSection;
            }
        }

        // Token: 0x04000006 RID: 6
        public string Name;

        // Token: 0x04000008 RID: 8
        public Vector3 Offset;

        // Token: 0x04000003 RID: 3
        public ForzaTrackBin Parent;

        // Token: 0x04000004 RID: 4
        public EndianStream Stream;

        // Token: 0x04000005 RID: 5
        public ForzaTrackSubSection[] SubSections;

        // Token: 0x04000007 RID: 7
        public string Type;
    }
}
