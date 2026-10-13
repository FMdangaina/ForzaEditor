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
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using System.Text;

    public class ForzaArchive : IDisposable
    {
        private List<ZipFile> files;
        private Stream io;

        public ForzaArchive(Stream stream)
        {
            this.files = new List<ZipFile>();
            this.Initialize(stream);
        }

        public ForzaArchive(string file)
        {
            this.files = new List<ZipFile>();
            this.Initialize(new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read));
        }

        public void Dispose()
        {
            if (this.io != null)
            {
                this.io.Dispose();
            }
            this.files = null;
        }

        ~ForzaArchive()
        {
            this.Dispose();
        }

        public static byte[] GetFileData(string archivePath, string filePath)
        {
            byte[] data;
            try
            {
                using (ForzaArchive archive = new ForzaArchive(archivePath))
                {
                    data = archive[filePath].Data;
                }
            }
            catch (Exception)
            {
                data = null;
            }
            return data;
        }

        private void Initialize(Stream stream)
        {
            this.io = stream;
            BinaryReader reader = new BinaryReader(this.io);
            this.io.Seek((long) (((int) this.io.Length) - 0x16), SeekOrigin.Begin);
            if (reader.ReadInt32() != 0x6054b50)
            {
                throw new InvalidDataException("This is not a valid Xbox 360 archive.");
            }
            this.io.Seek((long) (((int) this.io.Length) - 0x16), SeekOrigin.Begin);
            EndLocator locator = new EndLocator(this.io);
            this.io.Seek((long) locator.DirectoryOffset, SeekOrigin.Begin);
            for (int i = 0; i < locator.EntriesInDirectory; i++)
            {
                this.files.Add(new ZipFile(this.io));
            }
        }

        public List<ZipFile> Files
        {
            get
            {
                return this.files;
            }
            set
            {
                this.files = value;
            }
        }

        private Stream Io
        {
            get
            {
                return this.io;
            }
            set
            {
                this.io = value;
            }
        }

        public ZipFile this[string path]
        {
            get
            {
                foreach (ZipFile file in this.files)
                {
                    if (file.FileName.Equals(path, StringComparison.InvariantCultureIgnoreCase))
                    {
                        return file;
                    }
                }
                return null;
            }
        }

        protected class EndLocator
        {
            private string comment;
            private short commentLength;
            internal int DirectoryOffset;
            private int directorySize;
            private short diskNumber;
            internal short EntriesInDirectory;
            private short entriesOnDisk;
            private int signature;
            private short startDiskNumber;
            private Stream stream;

            public EndLocator(Stream stream)
            {
                this.stream = stream;
                BinaryReader reader = new BinaryReader(stream);
                this.signature = reader.ReadInt32();
                this.diskNumber = reader.ReadInt16();
                this.startDiskNumber = reader.ReadInt16();
                this.entriesOnDisk = reader.ReadInt16();
                this.EntriesInDirectory = reader.ReadInt16();
                this.directorySize = reader.ReadInt32();
                this.DirectoryOffset = reader.ReadInt32();
                this.commentLength = reader.ReadInt16();
                this.comment = Encoding.ASCII.GetString(reader.ReadBytes(this.commentLength));
            }

            public void Write(Stream ew)
            {
                BinaryWriter writer = new BinaryWriter(this.stream);
                writer.Write(this.signature);
                writer.Write(this.diskNumber);
                writer.Write(this.startDiskNumber);
                writer.Write(this.entriesOnDisk);
                writer.Write(this.EntriesInDirectory);
                writer.Write(this.directorySize);
                writer.Write(this.DirectoryOffset);
                writer.Write(this.commentLength);
                writer.Write(this.comment);
            }
        }
    }
}

