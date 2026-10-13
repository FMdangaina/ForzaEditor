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
    using System.Text;

    public class EndianStream : Stream
    {
        private Stream @base;
        private EndianType endian;
        private BinaryReader reader;
        private BinaryWriter writer;

        public EndianStream(Stream stream, EndianType endian)
        {
            this.@base = stream;
            if (stream.CanRead)
            {
                this.reader = new BinaryReader(stream);
            }
            if (stream.CanWrite)
            {
                this.writer = new BinaryWriter(stream);
            }
            this.endian = endian;
        }

        public EndianStream(byte[] data, EndianType endian)
        {
            this.@base = new MemoryStream(data);
            if (this.@base.CanRead)
            {
                this.reader = new BinaryReader(this.@base);
            }
            if (this.@base.CanWrite)
            {
                this.writer = new BinaryWriter(this.@base);
            }
            this.endian = endian;
        }

        public void Dispose()
        {
            if (this.@base != null)
            {
                this.@base.Dispose();
            }
            if (this.reader != null)
            {
                this.reader.Close();
            }
            if (this.writer != null)
            {
                this.writer.Close();
            }
        }

        ~EndianStream()
        {
            this.Dispose();
        }

        public override void Flush()
        {
            this.@base.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return this.@base.Read(buffer, offset, count);
        }

        public string ReadASCII(int length)
        {
            return Encoding.ASCII.GetString(this.reader.ReadBytes(length));
        }

        public bool ReadBoolean()
        {
            return this.reader.ReadBoolean();
        }

        public byte ReadByte()
        {
            try
            {
                return this.reader.ReadByte();
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public byte[] ReadBytes(int count)
        {
            return this.reader.ReadBytes(count);
        }

        public char ReadChar()
        {
            return this.reader.ReadChar();
        }

        public double ReadDouble()
        {
            if (this.endian == EndianType.BigEndian)
            {
                byte[] array = this.reader.ReadBytes(8);
                Array.Reverse(array);
                return BitConverter.ToDouble(array, 0);
            }
            return this.reader.ReadDouble();
        }

        public short ReadInt16()
        {
            if (this.endian == EndianType.BigEndian)
            {
                byte[] array = this.reader.ReadBytes(2);
                Array.Reverse(array);
                return BitConverter.ToInt16(array, 0);
            }
            return this.reader.ReadInt16();
        }

        public int ReadInt32()
        {
            if (this.endian == EndianType.BigEndian)
            {
                byte[] array = this.reader.ReadBytes(4);
                Array.Reverse(array);
                return BitConverter.ToInt32(array, 0);
            }
            return this.reader.ReadInt32();
        }

        public long ReadInt64()
        {
            if (this.endian == EndianType.BigEndian)
            {
                byte[] array = this.reader.ReadBytes(8);
                Array.Reverse(array);
                return BitConverter.ToInt64(array, 0);
            }
            return this.reader.ReadInt64();
        }

        public float ReadSingle()
        {
            if (this.endian == EndianType.BigEndian)
            {
                byte[] array = this.reader.ReadBytes(4);
                Array.Reverse(array);
                return BitConverter.ToSingle(array, 0);
            }
            return this.reader.ReadSingle();
        }

        public string ReadString()
        {
            return this.reader.ReadString();
        }

        public ushort ReadUInt16()
        {
            if (this.endian == EndianType.BigEndian)
            {
                byte[] array = this.reader.ReadBytes(2);
                Array.Reverse(array);
                return BitConverter.ToUInt16(array, 0);
            }
            return this.reader.ReadUInt16();
        }

        public uint ReadUInt32()
        {
            if (this.endian == EndianType.BigEndian)
            {
                byte[] array = this.reader.ReadBytes(4);
                Array.Reverse(array);
                return BitConverter.ToUInt32(array, 0);
            }
            return this.reader.ReadUInt32();
        }

        public ulong ReadUInt64()
        {
            if (this.endian == EndianType.BigEndian)
            {
                byte[] array = this.reader.ReadBytes(8);
                Array.Reverse(array);
                return BitConverter.ToUInt64(array, 0);
            }
            return this.reader.ReadUInt64();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return this.@base.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            this.@base.SetLength(value);
        }

        public void Write(byte[] data)
        {
            this.writer.Write(data);
        }

        public void Write(object obj)
        {
            switch (Convert.GetTypeCode(obj))
            {
                case TypeCode.Object:
                {
                    byte[] buffer6 = obj as byte[];
                    if (buffer6 == null)
                    {
                        throw new NotSupportedException("Invalid datatype.");
                    }
                    this.writer.Write(buffer6);
                    return;
                }
                case TypeCode.Boolean:
                case TypeCode.Char:
                case TypeCode.Byte:
                    this.writer.Write((byte) obj);
                    return;

                case TypeCode.Int16:
                case TypeCode.UInt16:
                {
                    if (this.endian != EndianType.BigEndian)
                    {
                        this.writer.Write((ushort) obj);
                        return;
                    }
                    byte[] bytes = BitConverter.GetBytes((ushort) obj);
                    Array.Reverse(bytes);
                    this.writer.Write(bytes);
                    return;
                }
                case TypeCode.Int32:
                case TypeCode.UInt32:
                {
                    if (this.endian != EndianType.BigEndian)
                    {
                        this.writer.Write((uint) obj);
                        return;
                    }
                    byte[] array = BitConverter.GetBytes((uint) obj);
                    Array.Reverse(array);
                    this.writer.Write(array);
                    return;
                }
                case TypeCode.Int64:
                case TypeCode.UInt64:
                {
                    if (this.endian != EndianType.BigEndian)
                    {
                        this.writer.Write((ulong) obj);
                        return;
                    }
                    byte[] buffer3 = BitConverter.GetBytes((ulong) obj);
                    Array.Reverse(buffer3);
                    this.writer.Write(buffer3);
                    return;
                }
                case TypeCode.Single:
                {
                    if (this.endian != EndianType.BigEndian)
                    {
                        this.writer.Write((float) obj);
                        return;
                    }
                    byte[] buffer4 = BitConverter.GetBytes((float) obj);
                    Array.Reverse(buffer4);
                    this.writer.Write(buffer4);
                    return;
                }
                case TypeCode.Double:
                {
                    if (this.endian != EndianType.BigEndian)
                    {
                        this.writer.Write((double) obj);
                        return;
                    }
                    byte[] buffer5 = BitConverter.GetBytes((double) obj);
                    Array.Reverse(buffer5);
                    this.writer.Write(buffer5);
                    return;
                }
                case TypeCode.String:
                    this.writer.Write(Encoding.ASCII.GetBytes(((string) obj) + "\0"));
                    return;
            }
            throw new NotSupportedException("Invalid datatype.");
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            this.@base.Write(buffer, offset, count);
        }

        public override bool CanRead
        {
            get
            {
                return this.@base.CanRead;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return this.@base.CanSeek;
            }
        }

        public override bool CanTimeout
        {
            get
            {
                return this.@base.CanTimeout;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return this.@base.CanWrite;
            }
        }

        public override long Length
        {
            get
            {
                return this.@base.Length;
            }
        }

        public override long Position
        {
            get
            {
                return this.@base.Position;
            }
            set
            {
                this.@base.Position = value;
            }
        }
    }
}

