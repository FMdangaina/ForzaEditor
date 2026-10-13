namespace ForzaStudio
{
    using System;
    using System.Diagnostics;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Runtime.InteropServices;

    public partial class XdsTexture
    {
        // Stored values, no gamma conversion: this is what the game feeds to the GPU,
        // so exporting and re-importing them is lossless apart from block compression.
        public Bitmap GetLevelBitmap(int level)
        {
            Level info = this.GetLevel(level);
            byte[] pixels = this.DecodeLevel(level);
            Bitmap bitmap = new Bitmap(info.Width, info.Height, PixelFormat.Format32bppArgb);
            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, info.Width, info.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            for (int y = 0; y < info.Height; y++)
            {
                Marshal.Copy(pixels, y * info.Width * 4, new IntPtr(data.Scan0.ToInt64() + ((long) y * data.Stride)), info.Width * 4);
            }
            bitmap.UnlockBits(data);
            return bitmap;
        }

        public void ExportPng(string path)
        {
            using (Bitmap bitmap = this.GetLevelBitmap(0))
            {
                bitmap.Save(path, ImageFormat.Png);
            }
        }

        private string TexconvFormat
        {
            get
            {
                switch (this.Codec)
                {
                    case DxtFormat.Dxt1: return "BC1_UNORM";
                    case DxtFormat.Dxt3: return "BC2_UNORM";
                    case DxtFormat.Dxt5: return "BC3_UNORM";
                    case DxtFormat.Dxn: return "BC5_UNORM";
                    case DxtFormat.Dxt5A: return "BC4_UNORM";
                    case DxtFormat.A8R8G8B8: return "B8G8R8A8_UNORM";
                }
                throw new NotSupportedException();
            }
        }

        // Replaces every level with the given image (same size as the texture), keeping the
        // original header. Compression and mip generation are done by texconv (DirectXTex).
        public void ImportImage(string imagePath, string texconvPath)
        {
            string extension = Path.GetExtension(imagePath).ToLowerInvariant();
            if (extension == ".dds")
            {
                this.ImportDds(File.ReadAllBytes(imagePath));
                return;
            }
            int width;
            int height;
            if (extension == ".tga")
            {
                byte[] header = new byte[18];
                using (FileStream stream = File.OpenRead(imagePath))
                {
                    stream.Read(header, 0, header.Length);
                }
                width = BitConverter.ToUInt16(header, 12);
                height = BitConverter.ToUInt16(header, 14);
            }
            else
            {
                using (Image image = Image.FromFile(imagePath))
                {
                    width = image.Width;
                    height = image.Height;
                }
            }
            if (width != this.Width || height != this.Height)
            {
                throw new InvalidDataException(string.Format("The image is {0}x{1}, the texture is {2}x{3}.", width, height, this.Width, this.Height));
            }
            string tempDir = Path.Combine(Path.GetTempPath(), "ForzaStudio_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                string input = Path.Combine(tempDir, "input" + Path.GetExtension(imagePath));
                File.Copy(imagePath, input);

                // Gamma textures: treat input and output as sRGB so values are not converted,
                // while mips are filtered in linear space. Linear textures: ignore file metadata.
                string colorSpace = this.IsGamma ? "-srgb" : "--ignore-srgb";
                string arguments = string.Format("-nologo -y -dx10 -sepalpha {0} -f {1} -m {2} -o \"{3}\" \"{4}\"", colorSpace, this.TexconvFormat, this.LevelCount, tempDir, input);
                RunTexconv(texconvPath, arguments);

                string dds = Path.Combine(tempDir, "input.dds");
                if (!File.Exists(dds))
                {
                    throw new IOException("texconv did not produce an output file.");
                }
                this.ImportDds(File.ReadAllBytes(dds));
            }
            finally
            {
                try
                {
                    Directory.Delete(tempDir, true);
                }
                catch (IOException)
                {
                }
            }
        }

        // Expects a DDS with the texture's size and block format; uses as many levels as present.
        public void ImportDds(byte[] dds)
        {
            if (dds.Length < 128 || BitConverter.ToUInt32(dds, 0) != 0x20534444)
            {
                throw new InvalidDataException("Not a DDS file.");
            }
            int height = BitConverter.ToInt32(dds, 12);
            int width = BitConverter.ToInt32(dds, 16);
            int mipCount = Math.Max(BitConverter.ToInt32(dds, 28), 1);
            int offset = 128;
            if (BitConverter.ToUInt32(dds, 84) == 0x30315844)
            {
                offset += 20;
            }
            if (width != this.Width || height != this.Height)
            {
                throw new InvalidDataException(string.Format("The DDS is {0}x{1}, the texture is {2}x{3}.", width, height, this.Width, this.Height));
            }
            int levels = Math.Min(mipCount, this.LevelCount);
            for (int level = 0; level < levels; level++)
            {
                Level info = this.GetLevel(level);
                int size = info.WidthBlocks * info.HeightBlocks * this.BytesPerBlock;
                if (offset + size > dds.Length)
                {
                    throw new InvalidDataException("The DDS file is truncated.");
                }
                byte[] blocks = new byte[size];
                Buffer.BlockCopy(dds, offset, blocks, 0, size);
                this.WriteLevelBlocks(level, blocks);
                offset += size;
            }
        }

        private static void RunTexconv(string texconvPath, string arguments)
        {
            if (!File.Exists(texconvPath))
            {
                throw new FileNotFoundException("texconv.exe not found.", texconvPath);
            }
            ProcessStartInfo info = new ProcessStartInfo(texconvPath, arguments);
            info.UseShellExecute = false;
            info.CreateNoWindow = true;
            info.RedirectStandardOutput = true;
            info.RedirectStandardError = true;
            using (Process process = Process.Start(info))
            {
                string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    throw new IOException("texconv failed:\n" + output);
                }
            }
        }
    }
}
