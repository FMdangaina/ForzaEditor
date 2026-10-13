namespace ForzaStudio
{
    using System;

    // Xbox 360 (Xenos) 2D tiled texture addressing, after Xenia's texture_address.h.
    public static class XenosTiling
    {
        public static int Tiled2D(int x, int y, int pitchBlocks, int bytesPerBlockLog2)
        {
            int outerBlocks = (((y >> 5) * (pitchBlocks >> 5)) + (x >> 5)) << 6;
            int innerBlocks = (((y >> 1) & 7) << 3) | (x & 7);
            int bytes = (outerBlocks | innerBlocks) << bytesPerBlockLog2;
            int bank = (y >> 4) & 1;
            int pipe = ((x >> 3) & 3) ^ (((y >> 3) & 1) << 1);
            return ((y & 1) << 4) | (pipe << 6) | (bank << 11)
                | (bytes & 0xF)
                | (((bytes >> 4) & 1) << 5)
                | (((bytes >> 5) & 7) << 8)
                | ((bytes >> 8) << 12);
        }

        public static void SwapEndian(byte[] data, int offset, int length, int endian)
        {
            int end = offset + length;
            switch (endian)
            {
                case 1:
                    for (int i = offset; i + 1 < end; i += 2)
                    {
                        byte t = data[i];
                        data[i] = data[i + 1];
                        data[i + 1] = t;
                    }
                    break;

                case 2:
                    for (int i = offset; i + 3 < end; i += 4)
                    {
                        byte t0 = data[i];
                        byte t1 = data[i + 1];
                        data[i] = data[i + 3];
                        data[i + 1] = data[i + 2];
                        data[i + 2] = t1;
                        data[i + 3] = t0;
                    }
                    break;

                case 3:
                    for (int i = offset; i + 3 < end; i += 4)
                    {
                        byte t0 = data[i];
                        byte t1 = data[i + 1];
                        data[i] = data[i + 2];
                        data[i + 1] = data[i + 3];
                        data[i + 2] = t0;
                        data[i + 3] = t1;
                    }
                    break;
            }
        }
    }
}
