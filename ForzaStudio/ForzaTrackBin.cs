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

    public class ForzaTrackBin : ForzaResource
    {
        public ForzaTrackSection[] Sections;
        public EndianStream Stream;
        public string Type;

        public ForzaTrackBin(string filePath) : base(filePath, (string) null)
        {
            this.Initialize();
        }

        public ForzaTrackBin(string filePath, byte[] data) : base(filePath, data)
        {
            this.Initialize();
        }

        public ForzaTrackBin(string archivePath, string filePath) : base(filePath, archivePath)
        {
            this.Initialize();
        }

        public override void Dispose()
        {
            if (this.Stream != null)
            {
                this.Stream.Dispose();
            }
            this.Sections = null;
        }

        ~ForzaTrackBin()
        {
            this.Dispose();
        }

        public void Initialize()
        {
            this.Stream = new EndianStream(base.GetDataStream(), EndianType.BigEndian);
            base.Name = base.FileName;
            switch (this.Stream.ReadUInt32())
            {
                case 4:
                    base.Version = ForzaVersion.Three;
                    break;

                case 6:
                    base.Version = ForzaVersion.Four;
                    break;
            }
            this.Stream.Position += 0x70L;
            uint num2 = this.Stream.ReadUInt32();
            this.Sections = new ForzaTrackSection[num2];
            for (int i = 0; i < this.Sections.Length; i++)
            {
                this.Sections[i] = new ForzaTrackSection(this);
            }
            this.Stream.Dispose();
        }
    }
}

