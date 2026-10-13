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
    using System;

    public class ForzaCarSubSection
    {
        public int FaceCount;
        public ForzaStudio.IndexType IndexType;
        public int[] Indices;
        public int Lod;
        public string Name;
        public ForzaCarSection Parent;
        public EndianStream Stream;
        public static uint strippedCheck;
        public static int subSImax = 0;
        public int VertexCount;
        public ForzaVertex[] Vertices;
        public float XUV2Offset;
        public float XUV2Scale = 1f;
        public float XUVOffset;
        public float XUVScale = 1f;
        public float YUV2Offset;
        public float YUV2Scale = 1f;
        public float YUVOffset;
        public float YUVScale = 1f;

        public ForzaCarSubSection(ForzaCarSection parentSection)
        {
            this.Parent = parentSection;
            this.Stream = parentSection.Stream;
            switch (parentSection.Parent.Version)
            {
                case ForzaVersion.Two:
                    this.Stream.Position += 8L;
                    break;

                case ForzaVersion.Three:
                    Utilities.AssertEquals(this.Stream.ReadUInt32(), 2);
                    this.Stream.ReadByte();
                    Utilities.AssertEquals(this.Stream.ReadUInt32(), 2);
                    break;

                case ForzaVersion.Four:
                    this.Stream.Position += 5L;
                    this.XUVOffset = this.Stream.ReadSingle();
                    this.XUVScale = this.Stream.ReadSingle();
                    this.YUVOffset = this.Stream.ReadSingle();
                    this.YUVScale = this.Stream.ReadSingle();
                    this.XUV2Offset = this.Stream.ReadSingle();
                    this.XUV2Scale = this.Stream.ReadSingle();
                    this.YUV2Offset = this.Stream.ReadSingle();
                    this.YUV2Scale = this.Stream.ReadSingle();
                    this.Stream.Position += 0x24L;
                    break;

                case ForzaVersion.Horizon:
                    this.Stream.Position += 5L;
                    this.XUVOffset = this.Stream.ReadSingle();
                    this.XUVScale = this.Stream.ReadSingle();
                    this.YUVOffset = this.Stream.ReadSingle();
                    this.YUVScale = this.Stream.ReadSingle();
                    this.XUV2Offset = this.Stream.ReadSingle();
                    this.XUV2Scale = this.Stream.ReadSingle();
                    this.YUV2Offset = this.Stream.ReadSingle();
                    this.YUV2Scale = this.Stream.ReadSingle();
                    this.Stream.Position += 0x24L;
                    break;

                default:
                    throw new NotSupportedException();
            }
            this.Name = this.Stream.ReadASCII(this.Stream.ReadInt32());
            if (parentSection.Parent.Version == ForzaVersion.Two)
            {
                this.IndexType = (ForzaStudio.IndexType) this.Stream.ReadUInt32();
                this.Lod = this.Stream.ReadInt32();
            }
            else
            {
                this.Lod = this.Stream.ReadInt32();
                this.IndexType = (ForzaStudio.IndexType) this.Stream.ReadUInt32();
                Utilities.AssertEquals(this.Stream.ReadUInt32(), 0);
                Utilities.AssertEquals(this.Stream.ReadUInt32(), 1);
                Utilities.AssertEquals(this.Stream.ReadUInt32(), 0);
                Utilities.AssertEquals(this.Stream.ReadUInt32(), 0);
                Utilities.AssertEquals(this.Stream.ReadUInt32(), 0);
                Utilities.AssertEquals(this.Stream.ReadUInt32(), 0);
                Utilities.AssertEquals(this.Stream.ReadSingle(), 1f);
                Utilities.AssertEquals(this.Stream.ReadSingle(), 1f);
                Utilities.AssertEquals(this.Stream.ReadSingle(), 1f);
                Utilities.AssertEquals(this.Stream.ReadSingle(), 1f);
                Utilities.AssertEquals(this.Stream.ReadSingle(), 0f);
                Utilities.AssertEquals(this.Stream.ReadSingle(), 0f);
                Utilities.AssertEquals(this.Stream.ReadSingle(), 1f);
                Utilities.AssertEquals(this.Stream.ReadSingle(), 1f);
            }
            if (parentSection.Parent.Version == ForzaVersion.Two)
            {
                Utilities.AssertEquals(this.Stream.ReadUInt32(), 3);
                this.Indices = Utilities.ReadIndices(this.Stream, this.Stream.ReadInt32(), this.Stream.ReadInt32());
                subSImax = 0;
                for (int i = 0; i < this.Indices.Length; i++)
                {
                    if (this.Indices[i] > subSImax)
                    {
                        subSImax = this.Indices[i];
                    }
                }
            }
            else if ((parentSection.Parent.Version == ForzaVersion.Three) || (parentSection.Parent.Version == ForzaVersion.Four))
            {
                Utilities.AssertEquals(this.Stream.ReadUInt32(), 3);
                this.Indices = Utilities.ReadIndices(this.Stream, this.Stream.ReadInt32(), this.Stream.ReadInt32());
                this.Stream.Position += 4L;
            }
            else if (parentSection.Parent.Version == ForzaVersion.Horizon)
            {
                Utilities.AssertEquals(this.Stream.ReadUInt32(), 4);
                strippedCheck = this.Stream.ReadUInt32();
                if (strippedCheck == 2)
                {
                    this.Stream.Position += 8L;
                    this.Indices = Utilities.ReadIndices(this.Stream, 0, 0);
                }
                else
                {
                    this.Indices = Utilities.ReadIndices(this.Stream, this.Stream.ReadInt32(), this.Stream.ReadInt32());
                }
                this.Stream.Position += 4L;
            }
            this.FaceCount = Utilities.CalculateFaceCount(this.Indices, this.IndexType);
            this.VertexCount = Utilities.CalculateVertexCount(this.Indices);
        }
    }
}

