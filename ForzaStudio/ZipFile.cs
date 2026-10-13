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
    using System.IO;
    using System.IO.Compression;
    using System.Runtime.InteropServices;
    using System.Text;

    public class ZipFile
    {
        private int compressedSize;
        private CompressionType compression;
        private uint crc;
        private short diskNumberStart;
        private int externalAttributes;
        private string extraField;
        private short extraFieldLength;
        private short fileCommentLength;
        private short fileDate;
        private string fileName;
        private short fileNameLength;
        private short fileTime;
        private short flags;
        private int headerOffset;
        private short internalAttributes;
        private int offset;
        private int signature;
        private Stream stream;
        private int uncompressedSize;
        private short versionMadeBy;
        private short versionToExtract;

        public ZipFile(Stream stream)
        {
            this.stream = stream;
            BinaryReader reader = new BinaryReader(stream);
            this.offset = (int) stream.Position;
            this.signature = reader.ReadInt32();
            this.versionMadeBy = reader.ReadInt16();
            this.versionToExtract = reader.ReadInt16();
            this.flags = reader.ReadInt16();
            this.compression = (CompressionType) reader.ReadInt16();
            this.fileTime = reader.ReadInt16();
            this.fileDate = reader.ReadInt16();
            this.crc = reader.ReadUInt32();
            this.compressedSize = reader.ReadInt32();
            this.uncompressedSize = reader.ReadInt32();
            this.fileNameLength = reader.ReadInt16();
            this.extraFieldLength = reader.ReadInt16();
            this.fileCommentLength = reader.ReadInt16();
            this.diskNumberStart = reader.ReadInt16();
            this.internalAttributes = reader.ReadInt16();
            this.externalAttributes = reader.ReadInt32();
            this.headerOffset = reader.ReadInt32();
            this.fileName = Encoding.ASCII.GetString(reader.ReadBytes(this.fileNameLength));
            this.extraField = Encoding.ASCII.GetString(reader.ReadBytes(this.extraFieldLength));
        }

        public void SaveAs(string filename)
        {
            using (FileStream stream = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                byte[] data = this.Data;
                stream.Write(data, 0, data.Length);
            }
        }

        public byte[] Data
        {
            get
            {
                this.stream.Seek((long) this.headerOffset, SeekOrigin.Begin);
                Info info = new Info(this.stream);
                return info.DecompressData();
            }
        }

        public string FileName
        {
            get
            {
                return this.fileName;
            }
            set
            {
                this.fileName = value;
                this.fileNameLength = (short) this.fileName.Length;
            }
        }

        protected class Info
        {
            internal int CompressedSize;
            private CompressionType compression;
            internal uint Crc;
            private byte[] data;
            private string extraField;
            private short extraFieldLength;
            private short fileDate;
            private string fileName;
            private short fileNameLength;
            private short fileTime;
            private short flags;
            private int signature;
            private Stream stream;
            internal int UncompressedSize;
            private short version;

            public Info(Stream stream)
            {
                this.stream = stream;
                BinaryReader reader = new BinaryReader(stream);
                this.signature = reader.ReadInt32();
                this.version = reader.ReadInt16();
                this.flags = reader.ReadInt16();
                this.compression = (CompressionType) reader.ReadInt16();
                this.fileTime = reader.ReadInt16();
                this.fileDate = reader.ReadInt16();
                this.Crc = reader.ReadUInt32();
                this.CompressedSize = reader.ReadInt32();
                this.UncompressedSize = reader.ReadInt32();
                this.fileNameLength = reader.ReadInt16();
                this.extraFieldLength = reader.ReadInt16();
                this.fileName = Encoding.ASCII.GetString(reader.ReadBytes(this.fileNameLength));
                this.extraField = Encoding.ASCII.GetString(reader.ReadBytes(this.extraFieldLength));
                this.data = reader.ReadBytes(this.CompressedSize);
            }

            private static uint ComputeCRC32(byte[] bytes)
            {
                uint[] numArray = new uint[0x100];
                for (uint i = 0; i < numArray.Length; i++)
                {
                    uint num2 = i;
                    for (int k = 8; k > 0; k--)
                    {
                        if ((num2 & 1) == 1)
                        {
                            num2 = (num2 >> 1) ^ 0xedb88320;
                        }
                        else
                        {
                            num2 = num2 >> 1;
                        }
                    }
                    numArray[i] = num2;
                }
                uint maxValue = uint.MaxValue;
                for (int j = 0; j < bytes.Length; j++)
                {
                    byte index = (byte) ((maxValue & 0xff) ^ bytes[j]);
                    maxValue = (maxValue >> 8) ^ numArray[index];
                }
                return ~maxValue;
            }

            public byte[] DecompressData()
            {
                byte[] buffer;
                CompressionType compression = this.compression;
                if (compression != CompressionType.Stored)
                {
                    if (compression != CompressionType.Deflate)
                    {
                        if (compression != CompressionType.LZX)
                        {
                            throw new NotSupportedException(string.Format("Compression type of {0} is currently not supported.", this.compression));
                        }
                        int pContext = 0;
                        XMemCreateDecompressionContext(XMemCodecType.LZX, 0, 0, ref pContext);
                        XMemResetDecompressionContext(pContext);
                        buffer = new byte[this.UncompressedSize];
                        XMemDecompressStream(pContext, buffer, ref this.UncompressedSize, this.data, ref this.CompressedSize);
                        XMemDestroyDecompressionContext(pContext);
                        goto Label_00C2;
                    }
                }
                else
                {
                    return this.data;
                }
                MemoryStream stream = new MemoryStream(this.data);
                DeflateStream stream2 = new DeflateStream(stream, CompressionMode.Decompress);
                buffer = new byte[this.UncompressedSize];
                if (stream2.Read(buffer, 0, this.UncompressedSize) != this.UncompressedSize)
                {
                    throw new InvalidDataException("Decompression failed.");
                }
            Label_00C2:
                if (ComputeCRC32(buffer) != this.Crc)
                {
                    throw new InvalidDataException("Invalid CRC detected.");
                }
                return buffer;
            }

            [DllImport("xcompress.dll")]
            public static extern int XMemCompressStream(int context, byte[] pDestination, ref int pDestSize, byte[] pSource, ref int pSrcSize);
            [DllImport("xcompress.dll")]
            public static extern int XMemCreateCompressionContext(XMemCodecType codecType, int pCodecParams, int flags, ref int pContext);
            [DllImport("xcompress.dll")]
            public static extern int XMemCreateDecompressionContext(XMemCodecType codecType, int pCodecParams, int flags, ref int pContext);
            [DllImport("xcompress.dll")]
            public static extern int XMemDecompressStream(int context, byte[] pDestination, ref int pDestSize, byte[] pSource, ref int pSrcSize);
            [DllImport("xcompress.dll")]
            public static extern void XMemDestroyCompressionContext(int context);
            [DllImport("xcompress.dll")]
            public static extern void XMemDestroyDecompressionContext(int context);
            [DllImport("xcompress.dll")]
            public static extern int XMemResetCompressionContext(int context);
            [DllImport("xcompress.dll")]
            public static extern int XMemResetDecompressionContext(int context);
        }
    }
}

