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
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    public abstract class ForzaResource : IDisposable
    {
        public Guid Id = Guid.NewGuid();
        public Dictionary<string, object> Tags = new Dictionary<string, object>();

        protected ForzaResource(string filePath, string archivePath = null)
        {
            if (filePath == null)
            {
                throw new ArgumentNullException("A file path must be specified");
            }
            this.FilePath = filePath.ToLowerInvariant();
            if (archivePath != null)
            {
                this.ArchivePath = archivePath.ToLowerInvariant();
            }
        }

        private byte[] memoryData;

        // Resource backed by data already in memory (e.g. an entry of an open ForzaZip).
        protected ForzaResource(string filePath, byte[] data) : this(filePath, (string) null)
        {
            this.memoryData = data;
        }

        public abstract void Dispose();
        public byte[] GetData()
        {
            if (this.memoryData != null)
            {
                return this.memoryData;
            }
            try
            {
                if ((!Utilities.IsNullOrWhiteSpace(this.ArchivePath) && !Utilities.IsNullOrWhiteSpace(this.FilePath)) && File.Exists(this.ArchivePath))
                {
                    using (ForzaArchive archive = new ForzaArchive(this.ArchivePath))
                    {
                        return archive[this.FilePath].Data;
                    }
                }
                if (!Utilities.IsNullOrWhiteSpace(this.FilePath) && File.Exists(this.FilePath))
                {
                    using (FileStream stream = new FileStream(this.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        byte[] buffer = new byte[stream.Length];
                        stream.Read(buffer, 0, buffer.Length);
                        return buffer;
                    }
                }
            }
            catch (Exception)
            {
            }
            return null;
        }

        public Stream GetDataStream()
        {
            byte[] data = this.GetData();
            if (data != null)
            {
                return new MemoryStream(data);
            }
            return null;
        }

        public string ArchivePath { get; protected set; }

        public string Description { get; set; }

        public string Extension
        {
            get
            {
                return Path.GetExtension(this.FilePath).ToLowerInvariant();
            }
        }

        public string FileName
        {
            get
            {
                return Path.GetFileNameWithoutExtension(this.FilePath).ToLowerInvariant();
            }
        }

        public string FilePath { get; protected set; }

        public string Name { get; set; }

        public ForzaVersion Version { get; set; }
    }
}

