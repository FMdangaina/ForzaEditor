namespace ForzaStudio
{
    using System;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Runtime.InteropServices;

    // Truevision TGA reader/writer. Pixels are kept as top-down BGRA; saving reuses the original
    // header settings (type, RLE, bit depth, origin, descriptor, id field) so a replaced image keeps
    // the exact layout the game expects. 32 bit images keep their 4th channel even when the
    // descriptor declares no alpha bits (Forza livery masks store mask data there).
    public class TgaImage
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        public byte[] Pixels;   // BGRA, top-down

        private byte[] header = new byte[18];
        private byte[] imageId = new byte[0];
        private bool hasFooter;

        public int ImageType { get { return this.header[2]; } }
        public int BitsPerPixel { get { return this.header[16]; } }
        public bool IsRle { get { return this.ImageType >= 9; } }
        public int AlphaBits { get { return this.header[17] & 0xF; } }
        private bool TopOrigin { get { return (this.header[17] & 0x20) != 0; } }
        private bool RightOrigin { get { return (this.header[17] & 0x10) != 0; } }
        private bool IsGray { get { return this.ImageType == 3 || this.ImageType == 11; } }

        public string Description
        {
            get
            {
                string kind;
                switch (this.ImageType)
                {
                    case 1: kind = "color-mapped"; break;
                    case 2: kind = "true color"; break;
                    case 3: kind = "grayscale"; break;
                    case 9: kind = "color-mapped RLE"; break;
                    case 10: kind = "true color RLE"; break;
                    case 11: kind = "grayscale RLE"; break;
                    default: kind = "type " + this.ImageType; break;
                }
                return string.Format("{0}, {1} bit", kind, this.BitsPerPixel);
            }
        }

        public TgaImage(byte[] file)
        {
            if (file.Length < 18)
            {
                throw new InvalidDataException("File too small to be a TGA image.");
            }
            Buffer.BlockCopy(file, 0, this.header, 0, 18);
            this.Width = BitConverter.ToUInt16(file, 12);
            this.Height = BitConverter.ToUInt16(file, 14);
            int type = this.ImageType;
            int bpp = this.BitsPerPixel;
            if (type != 1 && type != 2 && type != 3 && type != 9 && type != 10 && type != 11)
            {
                throw new NotSupportedException("Unsupported TGA image type " + type + ".");
            }
            if (bpp != 8 && bpp != 15 && bpp != 16 && bpp != 24 && bpp != 32)
            {
                throw new NotSupportedException("Unsupported TGA bit depth " + bpp + ".");
            }
            int position = 18;
            this.imageId = Slice(file, position, file[0]);
            position += file[0];

            // Color map (types 1 and 9).
            byte[][] palette = null;
            int mapLength = BitConverter.ToUInt16(file, 5);
            int mapEntryBits = file[7];
            if (file[1] != 0)
            {
                int mapFirst = BitConverter.ToUInt16(file, 3);
                int entryBytes = (mapEntryBits + 7) / 8;
                palette = new byte[mapFirst + mapLength][];
                for (int i = 0; i < mapLength; i++)
                {
                    palette[mapFirst + i] = ToBgra(file, position + (i * entryBytes), mapEntryBits, false);
                }
                position += mapLength * entryBytes;
            }
            if ((type == 1 || type == 9) && palette == null)
            {
                throw new InvalidDataException("Color-mapped TGA without a color map.");
            }

            int pixelBytes = (bpp + 7) / 8;
            int count = this.Width * this.Height;
            byte[] raw = new byte[count * pixelBytes];
            if (this.IsRle)
            {
                int output = 0;
                while (output < raw.Length)
                {
                    int packet = file[position++];
                    int length = (packet & 0x7F) + 1;
                    if ((packet & 0x80) != 0)
                    {
                        for (int i = 0; i < length && output < raw.Length; i++)
                        {
                            Buffer.BlockCopy(file, position, raw, output, pixelBytes);
                            output += pixelBytes;
                        }
                        position += pixelBytes;
                    }
                    else
                    {
                        int bytes = Math.Min(length * pixelBytes, raw.Length - output);
                        Buffer.BlockCopy(file, position, raw, output, bytes);
                        output += bytes;
                        position += length * pixelBytes;
                    }
                }
            }
            else
            {
                Buffer.BlockCopy(file, position, raw, 0, raw.Length);
                position += raw.Length;
            }
            this.hasFooter = file.Length >= 26 && System.Text.Encoding.ASCII.GetString(file, file.Length - 18, 16) == "TRUEVISION-XFILE";

            this.Pixels = new byte[count * 4];
            for (int i = 0; i < count; i++)
            {
                byte[] bgra;
                if (palette != null)
                {
                    int index = (pixelBytes == 1) ? raw[i] : BitConverter.ToUInt16(raw, i * 2);
                    bgra = (index < palette.Length && palette[index] != null) ? palette[index] : new byte[] { 0, 0, 0, 255 };
                }
                else
                {
                    bgra = ToBgra(raw, i * pixelBytes, bpp, this.IsGray);
                }
                int x = i % this.Width;
                int y = i / this.Width;
                if (this.RightOrigin)
                {
                    x = this.Width - 1 - x;
                }
                if (!this.TopOrigin)
                {
                    y = this.Height - 1 - y;
                }
                Buffer.BlockCopy(bgra, 0, this.Pixels, ((y * this.Width) + x) * 4, 4);
            }
        }

        public static TgaImage FromFile(string path)
        {
            return new TgaImage(File.ReadAllBytes(path));
        }

        private static byte[] ToBgra(byte[] data, int offset, int bits, bool gray)
        {
            switch (bits)
            {
                case 8:
                    return new byte[] { data[offset], data[offset], data[offset], 255 };
                case 15:
                case 16:
                    int v = data[offset] | (data[offset + 1] << 8);
                    int b = v & 0x1F;
                    int g = (v >> 5) & 0x1F;
                    int r = (v >> 10) & 0x1F;
                    byte a = (bits == 16 && (v & 0x8000) == 0) ? (byte) 0 : (byte) 255;
                    return new byte[] { (byte) ((b << 3) | (b >> 2)), (byte) ((g << 3) | (g >> 2)), (byte) ((r << 3) | (r >> 2)), a };
                case 24:
                    return new byte[] { data[offset], data[offset + 1], data[offset + 2], 255 };
                default:
                    return new byte[] { data[offset], data[offset + 1], data[offset + 2], data[offset + 3] };
            }
        }

        private byte[] FromBgra(byte[] pixels, int offset)
        {
            byte b = pixels[offset];
            byte g = pixels[offset + 1];
            byte r = pixels[offset + 2];
            byte a = pixels[offset + 3];
            switch (this.BitsPerPixel)
            {
                case 8:
                    return new byte[] { (byte) (((r * 77) + (g * 150) + (b * 29)) >> 8) };
                case 15:
                case 16:
                    int v = (b >> 3) | ((g >> 3) << 5) | ((r >> 3) << 10) | ((this.BitsPerPixel == 16 && a >= 128) ? 0x8000 : 0);
                    return new byte[] { (byte) v, (byte) (v >> 8) };
                case 24:
                    return new byte[] { b, g, r };
                default:
                    return new byte[] { b, g, r, a };
            }
        }

        // Replaces the pixels (BGRA top-down, same size) keeping the original file format.
        public void SetPixels(byte[] bgra)
        {
            if (bgra.Length != this.Width * this.Height * 4)
            {
                throw new ArgumentException("Pixel data does not match the image size.");
            }
            this.Pixels = bgra;
        }

        public byte[] ToBytes()
        {
            if (this.ImageType == 1 || this.ImageType == 9)
            {
                throw new NotSupportedException("Writing color-mapped TGA images is not supported.");
            }
            int pixelBytes = (this.BitsPerPixel + 7) / 8;
            using (MemoryStream stream = new MemoryStream())
            {
                byte[] head = (byte[]) this.header.Clone();
                head[0] = (byte) this.imageId.Length;
                stream.Write(head, 0, head.Length);
                stream.Write(this.imageId, 0, this.imageId.Length);

                // Rows in file order, then RLE per scanline (packets never cross rows).
                byte[] row = new byte[this.Width * pixelBytes];
                for (int fileRow = 0; fileRow < this.Height; fileRow++)
                {
                    int y = this.TopOrigin ? fileRow : (this.Height - 1 - fileRow);
                    for (int i = 0; i < this.Width; i++)
                    {
                        int x = this.RightOrigin ? (this.Width - 1 - i) : i;
                        byte[] value = this.FromBgra(this.Pixels, ((y * this.Width) + x) * 4);
                        Buffer.BlockCopy(value, 0, row, i * pixelBytes, pixelBytes);
                    }
                    if (this.IsRle)
                    {
                        WriteRleRow(stream, row, pixelBytes);
                    }
                    else
                    {
                        stream.Write(row, 0, row.Length);
                    }
                }
                if (this.hasFooter)
                {
                    stream.Write(new byte[8], 0, 8);
                    byte[] signature = System.Text.Encoding.ASCII.GetBytes("TRUEVISION-XFILE.");
                    stream.Write(signature, 0, signature.Length);
                    stream.WriteByte(0);
                }
                return stream.ToArray();
            }
        }

        private static bool SamePixel(byte[] row, int a, int b, int size)
        {
            for (int i = 0; i < size; i++)
            {
                if (row[(a * size) + i] != row[(b * size) + i])
                {
                    return false;
                }
            }
            return true;
        }

        // Same packetization as the game's own files (verified byte for byte on Fm4.zip livery masks):
        // every pixel is emitted as a literal; when 3 or more copies of it follow, the pending raw
        // packet is closed and a run packet of those copies is written. A raw packet and the run
        // that follows it never cover more than 128 pixels together.
        private static void WriteRleRow(Stream stream, byte[] row, int size)
        {
            int count = row.Length / size;
            int rawStart = 0;
            int rawLength = 0;
            int i = 0;
            while (i < count)
            {
                if (rawLength == 0)
                {
                    rawStart = i;
                }
                rawLength++;
                int repeats = 0;
                while (i + 1 + repeats < count && repeats < 128 - rawLength && SamePixel(row, i, i + 1 + repeats, size))
                {
                    repeats++;
                }
                if (repeats >= 3)
                {
                    WriteRaw(stream, row, rawStart, rawLength, size);
                    rawLength = 0;
                    stream.WriteByte((byte) (0x80 | (repeats - 1)));
                    stream.Write(row, i * size, size);
                    i += 1 + repeats;
                    continue;
                }
                if (rawLength == 128)
                {
                    WriteRaw(stream, row, rawStart, rawLength, size);
                    rawLength = 0;
                }
                i++;
            }
            if (rawLength > 0)
            {
                WriteRaw(stream, row, rawStart, rawLength, size);
            }
        }

        private static void WriteRaw(Stream stream, byte[] row, int start, int length, int size)
        {
            stream.WriteByte((byte) (length - 1));
            stream.Write(row, start * size, length * size);
        }

        // Plain uncompressed 32 bit TGA with 8 declared alpha bits: image editors such as Photoshop
        // open it as RGB plus a separate alpha channel, so color under transparent pixels survives.
        public static byte[] Encode32(int width, int height, byte[] bgra)
        {
            byte[] file = new byte[18 + (width * height * 4)];
            file[2] = 2;
            file[12] = (byte) width;
            file[13] = (byte) (width >> 8);
            file[14] = (byte) height;
            file[15] = (byte) (height >> 8);
            file[16] = 32;
            file[17] = 0x28;    // 8 alpha bits, top-left origin
            Buffer.BlockCopy(bgra, 0, file, 18, width * height * 4);
            return file;
        }

        public Bitmap ToBitmap()
        {
            Bitmap bitmap = new Bitmap(this.Width, this.Height, PixelFormat.Format32bppArgb);
            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, this.Width, this.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            for (int y = 0; y < this.Height; y++)
            {
                Marshal.Copy(this.Pixels, y * this.Width * 4, new IntPtr(data.Scan0.ToInt64() + ((long) y * data.Stride)), this.Width * 4);
            }
            bitmap.UnlockBits(data);
            return bitmap;
        }

        // Loads any supported image (TGA through this class, others through GDI+) as top-down BGRA.
        public static byte[] LoadPixels(string path, out int width, out int height)
        {
            if (path.EndsWith(".tga", StringComparison.OrdinalIgnoreCase))
            {
                TgaImage tga = FromFile(path);
                width = tga.Width;
                height = tga.Height;
                return tga.Pixels;
            }
            // LockBits straight from the decoded file: drawing it would discard the color of fully
            // transparent pixels, which livery masks use as independent data.
            using (Bitmap bitmap = new Bitmap(path))
            {
                width = bitmap.Width;
                height = bitmap.Height;
                byte[] pixels = new byte[width * height * 4];
                BitmapData data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                for (int y = 0; y < height; y++)
                {
                    Marshal.Copy(new IntPtr(data.Scan0.ToInt64() + ((long) y * data.Stride)), pixels, y * width * 4, width * 4);
                }
                bitmap.UnlockBits(data);
                return pixels;
            }
        }

        private static byte[] Slice(byte[] source, int offset, int length)
        {
            byte[] result = new byte[length];
            Buffer.BlockCopy(source, offset, result, 0, length);
            return result;
        }
    }
}
