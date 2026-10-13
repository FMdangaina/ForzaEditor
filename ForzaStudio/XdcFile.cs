namespace ForzaStudio
{
    using System;
    using System.IO;
    using System.IO.Compression;

    // .xdc: an .xds texture compressed with deflate (profile thumbnail sheets and the like).
    // Header, big endian: "cxds", version (2), size, compressed size, size again; then raw deflate data.
    public static class XdcFile
    {
        private const int HeaderSize = 20;

        public static bool IsXdc(byte[] data)
        {
            return data.Length >= HeaderSize && data[0] == 'c' && data[1] == 'x' && data[2] == 'd' && data[3] == 's';
        }

        public static byte[] Unpack(byte[] data)
        {
            if (!IsXdc(data))
            {
                throw new InvalidDataException("Not an .xdc file (no cxds header).");
            }
            int size = ReadInt(data, 8);
            byte[] output = new byte[size];
            using (DeflateStream stream = new DeflateStream(new MemoryStream(data, HeaderSize, data.Length - HeaderSize), CompressionMode.Decompress))
            {
                int total = 0;
                int read;
                while (total < size && (read = stream.Read(output, total, size - total)) > 0)
                {
                    total += read;
                }
                if (total != size)
                {
                    throw new InvalidDataException(string.Format("The .xdc data is {0} bytes, the header says {1}.", total, size));
                }
            }
            return output;
        }

        // Compresses a texture with the header of the original file (its version is kept).
        public static byte[] Pack(byte[] xds, byte[] original)
        {
            byte[] compressed;
            using (MemoryStream output = new MemoryStream())
            {
                using (DeflateStream stream = new DeflateStream(output, CompressionMode.Compress, true))
                {
                    stream.Write(xds, 0, xds.Length);
                }
                compressed = output.ToArray();
            }
            byte[] file = new byte[HeaderSize + compressed.Length];
            Buffer.BlockCopy(original, 0, file, 0, 8);
            WriteInt(file, 8, xds.Length);
            WriteInt(file, 12, compressed.Length);
            WriteInt(file, 16, xds.Length);
            Buffer.BlockCopy(compressed, 0, file, HeaderSize, compressed.Length);
            return file;
        }

        private static int ReadInt(byte[] data, int offset)
        {
            return (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];
        }

        private static void WriteInt(byte[] data, int offset, int value)
        {
            data[offset] = (byte) (value >> 24);
            data[offset + 1] = (byte) (value >> 16);
            data[offset + 2] = (byte) (value >> 8);
            data[offset + 3] = (byte) value;
        }
    }
}
