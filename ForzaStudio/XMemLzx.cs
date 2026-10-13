namespace ForzaStudio
{
    using System;
    using System.Runtime.InteropServices;

    // XMem LZX (zip method 21) through xcompress.dll, default codec parameters.
    // XMemCompress with defaults reproduces the game's archives byte for byte.
    public static class XMemLzx
    {
        public static byte[] Compress(byte[] source)
        {
            int context = 0;
            int result = XMemCreateCompressionContext(XMemCodecType.LZX, 0, 0, ref context);
            if (result != 0)
            {
                throw new InvalidOperationException(string.Format("XMemCreateCompressionContext failed (0x{0:X8}).", result));
            }
            try
            {
                byte[] buffer = new byte[source.Length + (source.Length / 8) + 65536];
                int size = buffer.Length;
                result = XMemCompress(context, buffer, ref size, source, source.Length);
                if (result != 0)
                {
                    throw new InvalidOperationException(string.Format("XMemCompress failed (0x{0:X8}).", result));
                }
                byte[] output = new byte[size];
                Buffer.BlockCopy(buffer, 0, output, 0, size);
                return output;
            }
            finally
            {
                XMemDestroyCompressionContext(context);
            }
        }

        public static byte[] Decompress(byte[] source, int uncompressedSize)
        {
            int context = 0;
            int result = XMemCreateDecompressionContext(XMemCodecType.LZX, 0, 0, ref context);
            if (result != 0)
            {
                throw new InvalidOperationException(string.Format("XMemCreateDecompressionContext failed (0x{0:X8}).", result));
            }
            try
            {
                XMemResetDecompressionContext(context);
                byte[] output = new byte[uncompressedSize];
                int outputSize = uncompressedSize;
                int sourceSize = source.Length;
                result = XMemDecompressStream(context, output, ref outputSize, source, ref sourceSize);
                if (result != 0 || outputSize != uncompressedSize)
                {
                    throw new InvalidOperationException(string.Format("XMemDecompressStream failed (0x{0:X8}).", result));
                }
                return output;
            }
            finally
            {
                XMemDestroyDecompressionContext(context);
            }
        }

        [DllImport("xcompress.dll")]
        private static extern int XMemCreateCompressionContext(XMemCodecType codecType, int pCodecParams, int flags, ref int pContext);
        [DllImport("xcompress.dll")]
        private static extern int XMemCompress(int context, byte[] pDestination, ref int pDestSize, byte[] pSource, int srcSize);
        [DllImport("xcompress.dll")]
        private static extern void XMemDestroyCompressionContext(int context);
        [DllImport("xcompress.dll")]
        private static extern int XMemCreateDecompressionContext(XMemCodecType codecType, int pCodecParams, int flags, ref int pContext);
        [DllImport("xcompress.dll")]
        private static extern int XMemResetDecompressionContext(int context);
        [DllImport("xcompress.dll")]
        private static extern int XMemDecompressStream(int context, byte[] pDestination, ref int pDestSize, byte[] pSource, ref int pSrcSize);
        [DllImport("xcompress.dll")]
        private static extern void XMemDestroyDecompressionContext(int context);
    }
}
