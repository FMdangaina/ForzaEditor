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

namespace ForzaStudio
{
    using Microsoft.Xna.Framework;
    using System;

    public class ForzaTrackSubSection
    {
        public int FaceCount;
        public ForzaStudio.IndexType IndexType;
        public int[] Indices;
        public uint Lod;
        public string Name;
        public Vector3 Offset;
        public ForzaTrackSection Parent;
        public EndianStream Stream;
        public Vector2 UVoffset;
        public Vector2 UVtile;
        public int VertexCount;
        public ForzaVertex[] Vertices;

        public ForzaTrackSubSection(ForzaTrackSection parent)
        {
            this.Parent = parent;
            this.Stream = parent.Stream;
            Utilities.AssertEquals(this.Stream.ReadUInt32(), 1);
            Utilities.AssertEquals(this.Stream.ReadUInt32(), 2);
            this.Name = this.Stream.ReadASCII(this.Stream.ReadInt32()).ToLowerInvariant();
            this.Lod = this.Stream.ReadUInt32();
            this.IndexType = (ForzaStudio.IndexType) this.Stream.ReadUInt32();
            if ((this.IndexType != ForzaStudio.IndexType.TriList) && (this.IndexType != ForzaStudio.IndexType.TriStrip))
            {
                throw new Exception("analyze this!");
            }
            uint num = this.Stream.ReadUInt32();
            Utilities.AssertEquals(this.Stream.ReadUInt32(), 1);
            Utilities.AssertEquals(this.Stream.ReadUInt32(), 0);
            Utilities.AssertEquals(this.Stream.ReadUInt32(), 0);
            Utilities.AssertEquals(this.Stream.ReadUInt32(), 0);
            Utilities.AssertEquals(this.Stream.ReadUInt32(), 0);
            Utilities.AssertEquals(this.Stream.ReadSingle(), 1f);
            Utilities.AssertEquals(this.Stream.ReadSingle(), 1f);
            Utilities.AssertEquals(this.Stream.ReadSingle(), 1f);
            Utilities.AssertEquals(this.Stream.ReadSingle(), 1f);
            this.UVoffset = new Vector2(this.Stream.ReadSingle(), this.Stream.ReadSingle());
            this.UVtile = new Vector2(this.Stream.ReadSingle(), this.Stream.ReadSingle());
            Utilities.AssertEquals(this.Stream.ReadUInt32(), 3);
            this.Indices = Utilities.ReadIndices(this.Stream, this.Stream.ReadInt32(), this.Stream.ReadInt32());
            num = this.Stream.ReadUInt32();
            if (((num != 0) && (num != 1)) && ((num != 2) && (num != 5)))
            {
                throw new Exception("analyze this!");
            }
            this.VertexCount = Utilities.CalculateVertexCount(this.Indices);
            this.FaceCount = Utilities.CalculateFaceCount(this.Indices, this.IndexType);
        }
    }
}

