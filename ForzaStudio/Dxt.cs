namespace ForzaStudio
{
    using System;

    public enum DxtFormat
    {
        Unknown,
        Dxt1,
        Dxt3,
        Dxt5,
        Dxn,
        Dxt5A,
        A8R8G8B8
    }

    // Block decoders for little endian (PC order) block data. Output is BGRA, 4 bytes per pixel.
    public static class Dxt
    {
        public static int BytesPerBlock(DxtFormat format)
        {
            switch (format)
            {
                case DxtFormat.Dxt1:
                case DxtFormat.Dxt5A:
                    return 8;
                case DxtFormat.Dxt3:
                case DxtFormat.Dxt5:
                case DxtFormat.Dxn:
                    return 16;
                case DxtFormat.A8R8G8B8:
                    return 4;
            }
            return 0;
        }

        // blocks: widthBlocks * heightBlocks linear blocks. Returns width * height * 4 BGRA bytes.
        public static byte[] Decode(DxtFormat format, byte[] blocks, int width, int height)
        {
            byte[] output = new byte[width * height * 4];
            if (format == DxtFormat.A8R8G8B8)
            {
                Buffer.BlockCopy(blocks, 0, output, 0, Math.Min(blocks.Length, output.Length));
                return output;
            }
            int bpb = BytesPerBlock(format);
            int widthBlocks = (width + 3) / 4;
            int heightBlocks = (height + 3) / 4;
            byte[] pixels = new byte[64];
            for (int by = 0; by < heightBlocks; by++)
            {
                for (int bx = 0; bx < widthBlocks; bx++)
                {
                    DecodeBlock(format, blocks, ((by * widthBlocks) + bx) * bpb, pixels);
                    for (int py = 0; py < 4; py++)
                    {
                        int y = (by * 4) + py;
                        if (y >= height)
                        {
                            break;
                        }
                        for (int px = 0; px < 4; px++)
                        {
                            int x = (bx * 4) + px;
                            if (x >= width)
                            {
                                break;
                            }
                            Buffer.BlockCopy(pixels, ((py * 4) + px) * 4, output, ((y * width) + x) * 4, 4);
                        }
                    }
                }
            }
            return output;
        }

        private static void DecodeBlock(DxtFormat format, byte[] src, int offset, byte[] pixels)
        {
            switch (format)
            {
                case DxtFormat.Dxt1:
                    DecodeColor(src, offset, pixels, true);
                    break;

                case DxtFormat.Dxt3:
                    DecodeColor(src, offset + 8, pixels, false);
                    for (int i = 0; i < 16; i++)
                    {
                        int a = (src[offset + (i >> 1)] >> ((i & 1) * 4)) & 0xF;
                        pixels[(i * 4) + 3] = (byte) (a * 17);
                    }
                    break;

                case DxtFormat.Dxt5:
                    DecodeColor(src, offset + 8, pixels, false);
                    DecodeAlpha(src, offset, pixels, 3);
                    break;

                case DxtFormat.Dxn:
                    // BC5: first block is red (X), second is green (Y). Z is left at 0.
                    DecodeAlpha(src, offset, pixels, 2);
                    DecodeAlpha(src, offset + 8, pixels, 1);
                    for (int i = 0; i < 16; i++)
                    {
                        pixels[i * 4] = 0;
                        pixels[(i * 4) + 3] = 255;
                    }
                    break;

                case DxtFormat.Dxt5A:
                    DecodeAlpha(src, offset, pixels, 2);
                    for (int i = 0; i < 16; i++)
                    {
                        pixels[i * 4] = pixels[(i * 4) + 2];
                        pixels[(i * 4) + 1] = pixels[(i * 4) + 2];
                        pixels[(i * 4) + 3] = 255;
                    }
                    break;
            }
        }

        private static void DecodeColor(byte[] src, int offset, byte[] pixels, bool allowTransparent)
        {
            int c0 = src[offset] | (src[offset + 1] << 8);
            int c1 = src[offset + 2] | (src[offset + 3] << 8);
            uint indices = (uint) (src[offset + 4] | (src[offset + 5] << 8) | (src[offset + 6] << 16) | (src[offset + 7] << 24));

            int[] r = new int[4];
            int[] g = new int[4];
            int[] b = new int[4];
            int[] a = new int[] { 255, 255, 255, 255 };
            Expand565(c0, out r[0], out g[0], out b[0]);
            Expand565(c1, out r[1], out g[1], out b[1]);
            if ((c0 > c1) || !allowTransparent)
            {
                r[2] = ((2 * r[0]) + r[1]) / 3;
                g[2] = ((2 * g[0]) + g[1]) / 3;
                b[2] = ((2 * b[0]) + b[1]) / 3;
                r[3] = (r[0] + (2 * r[1])) / 3;
                g[3] = (g[0] + (2 * g[1])) / 3;
                b[3] = (b[0] + (2 * b[1])) / 3;
            }
            else
            {
                r[2] = (r[0] + r[1]) / 2;
                g[2] = (g[0] + g[1]) / 2;
                b[2] = (b[0] + b[1]) / 2;
                r[3] = g[3] = b[3] = 0;
                a[3] = 0;
            }
            for (int i = 0; i < 16; i++)
            {
                int index = (int) ((indices >> (i * 2)) & 3);
                pixels[i * 4] = (byte) b[index];
                pixels[(i * 4) + 1] = (byte) g[index];
                pixels[(i * 4) + 2] = (byte) r[index];
                pixels[(i * 4) + 3] = (byte) a[index];
            }
        }

        private static void DecodeAlpha(byte[] src, int offset, byte[] pixels, int channel)
        {
            int a0 = src[offset];
            int a1 = src[offset + 1];
            int[] values = new int[8];
            values[0] = a0;
            values[1] = a1;
            if (a0 > a1)
            {
                for (int i = 1; i < 7; i++)
                {
                    values[i + 1] = (((7 - i) * a0) + (i * a1)) / 7;
                }
            }
            else
            {
                for (int i = 1; i < 5; i++)
                {
                    values[i + 1] = (((5 - i) * a0) + (i * a1)) / 5;
                }
                values[6] = 0;
                values[7] = 255;
            }
            ulong bits = 0;
            for (int i = 0; i < 6; i++)
            {
                bits |= ((ulong) src[offset + 2 + i]) << (8 * i);
            }
            for (int i = 0; i < 16; i++)
            {
                pixels[(i * 4) + channel] = (byte) values[(int) ((bits >> (i * 3)) & 7)];
            }
        }

        private static void Expand565(int c, out int r, out int g, out int b)
        {
            r = (c >> 11) & 0x1F;
            g = (c >> 5) & 0x3F;
            b = c & 0x1F;
            r = (r << 3) | (r >> 2);
            g = (g << 2) | (g >> 4);
            b = (b << 3) | (b >> 2);
        }
    }
}
