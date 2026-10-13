namespace ForzaStudio
{
    using System;
    using System.IO;

    // .xds = 52 byte big endian D3DBaseTexture header (the last 6 dwords are the Xenos
    // texture fetch constant) followed by texture memory: base level at BasePage and
    // mips at MipPage (4 KiB pages). Layout rules follow Xenia's texture_util.cc.
    public partial class XdsTexture
    {
        public const int HeaderSize = 52;
        private const int PageSize = 4096;
        private const int TileBlocks = 32;

        public uint[] Header = new uint[13];
        public byte[] Data;

        public XdsTexture(byte[] file)
        {
            if (file == null || file.Length < HeaderSize)
            {
                throw new InvalidDataException("File too small to be an XDS texture.");
            }
            for (int i = 0; i < 13; i++)
            {
                this.Header[i] = (uint) ((file[i * 4] << 24) | (file[(i * 4) + 1] << 16) | (file[(i * 4) + 2] << 8) | file[(i * 4) + 3]);
            }
            if (this.Header[0] != 3 || this.Header[1] != 1)
            {
                throw new InvalidDataException("Not a valid XDS file.");
            }
            this.Data = new byte[file.Length - HeaderSize];
            Buffer.BlockCopy(file, HeaderSize, this.Data, 0, this.Data.Length);
            if (this.Dimension != 1 || this.Codec == DxtFormat.Unknown)
            {
                throw new NotSupportedException(string.Format("Unsupported XDS texture (dimension {0}, format 0x{1:X2}).", this.Dimension, this.Format));
            }
        }

        public static XdsTexture FromFile(string path)
        {
            return new XdsTexture(File.ReadAllBytes(path));
        }

        private XdsTexture()
        {
        }

        // The formats the game's own textures use (fetch dword 1 low byte = format | endian << 6).
        public static readonly DxtFormat[] CreatableFormats = { DxtFormat.Dxt1, DxtFormat.Dxt5, DxtFormat.Dxn, DxtFormat.Dxt5A, DxtFormat.A8R8G8B8 };

        // A blank texture laid out like the game's: tiled, base at page 0, mips (full chain unless
        // 'levels' is smaller) from the page after the base, packed mip tail. Header fields as in the
        // game files: rebuilt from (size, format, gamma, levels) they match all 11775 FM4 car textures.
        public static XdsTexture Create(int width, int height, DxtFormat codec, bool gamma, int levels)
        {
            int format;
            switch (codec)
            {
                case DxtFormat.Dxt1: format = 0x73; break;      // DXT1 AS_16, 8in16
                case DxtFormat.Dxt5: format = 0x75; break;      // DXT4_5 AS_16, 8in16
                case DxtFormat.Dxn: format = 0x71; break;       // DXN, 8in16
                case DxtFormat.Dxt5A: format = 0x7B; break;     // DXT5A, 8in16
                case DxtFormat.A8R8G8B8: format = 0xB2; break;  // 8_8_8_8 AS_16, 8in32
                default: throw new NotSupportedException("Cannot create " + codec + " textures.");
            }
            if (width < 1 || height < 1 || width > 8192 || height > 8192)
            {
                throw new ArgumentException("Texture size must be 1..8192.");
            }
            XdsTexture texture = new XdsTexture();
            texture.Header[0] = 3;
            texture.Header[1] = 1;
            texture.Header[5] = 0xFFFF0000;
            texture.Header[6] = 0xFFFF0000;
            int blockSize = (codec == DxtFormat.A8R8G8B8) ? 1 : 4;
            // Smaller side <= 16: the whole mip tail lives in the base level, whose storage then
            // spans power-of-two sizes (as in the game's small UI textures).
            bool tailInBase = Math.Min(width, height) <= 16;
            int pitchTexels = Align(DivUp(tailInBase ? NextPow2(width) : width, blockSize), TileBlocks) * blockSize;
            texture.Header[7] = 0x80000000u | ((uint) (pitchTexels >> 5) << 22) | (gamma ? 0xFEu : 0x02u);
            texture.Header[8] = (uint) format;
            texture.Header[9] = (uint) (width - 1) | ((uint) (height - 1) << 13);
            texture.Header[10] = (codec == DxtFormat.A8R8G8B8) ? 0xC14u : 0xD10u;
            int maxLevels = Log2Floor(Math.Max(width, height)) + 1;
            levels = Math.Max(1, Math.Min(levels, maxLevels));
            texture.Header[11] = (uint) (levels - 1) << 6;
            texture.Header[12] = 0xA00;

            // Sizes: base region, then (when the mips do not live in the base tail) the mip regions.
            texture.Data = new byte[0];
            int size = tailInBase
                ? Align(texture.RowPitchBytes(pitchTexels / blockSize) * Align(DivUp(NextPow2(height), blockSize), TileBlocks), PageSize)
                : texture.StorageStride(0, true);
            if (levels > 1 && !tailInBase)
            {
                texture.Header[12] |= (uint) (size / PageSize) << 12;
                Level last = texture.GetLevel(levels - 1);
                int storageLevel = Math.Min(levels - 1, texture.PackedLevel);
                size = last.Offset + texture.StorageStride(storageLevel, false);
            }
            texture.Data = new byte[size];
            return texture;
        }

        public string FormatName
        {
            get { return FormatLabel(this.Codec); }
        }

        public static string FormatLabel(DxtFormat codec)
        {
            switch (codec)
            {
                case DxtFormat.Dxt1: return "DXT1 (color, no/1-bit alpha)";
                case DxtFormat.Dxt5: return "DXT5 (color + alpha)";
                case DxtFormat.Dxn: return "DXN (normal map)";
                case DxtFormat.Dxt5A: return "DXT5A (single channel)";
                case DxtFormat.A8R8G8B8: return "A8R8G8B8 (uncompressed)";
            }
            return codec.ToString();
        }

        public byte[] ToBytes()
        {
            byte[] file = new byte[HeaderSize + this.Data.Length];
            for (int i = 0; i < 13; i++)
            {
                uint v = this.Header[i];
                file[i * 4] = (byte) (v >> 24);
                file[(i * 4) + 1] = (byte) (v >> 16);
                file[(i * 4) + 2] = (byte) (v >> 8);
                file[(i * 4) + 3] = (byte) v;
            }
            Buffer.BlockCopy(this.Data, 0, file, HeaderSize, this.Data.Length);
            return file;
        }

        private uint Fetch(int dword)
        {
            return this.Header[7 + dword];
        }

        public bool Tiled { get { return (this.Fetch(0) >> 31) != 0; } }
        public int PitchTexels { get { return (int) ((this.Fetch(0) >> 22) & 0x1FF) << 5; } }
        public bool IsGamma { get { return ((this.Fetch(0) >> 2) & 3) == 3; } }
        public int Format { get { return (int) (this.Fetch(1) & 0x3F); } }
        public int Endian { get { return (int) ((this.Fetch(1) >> 6) & 3); } }
        public int BasePage { get { return (int) (this.Fetch(1) >> 12); } }
        public int Width { get { return (int) (this.Fetch(2) & 0x1FFF) + 1; } }
        public int Height { get { return (int) ((this.Fetch(2) >> 13) & 0x1FFF) + 1; } }
        public int MipMaxLevel { get { return (int) ((this.Fetch(4) >> 6) & 0xF); } }
        public int Dimension { get { return (int) ((this.Fetch(5) >> 9) & 3); } }
        public bool PackedMips { get { return ((this.Fetch(5) >> 11) & 1) != 0; } }
        public int MipPage { get { return (int) (this.Fetch(5) >> 12); } }

        public DxtFormat Codec
        {
            get
            {
                switch (this.Format)
                {
                    case 6:
                    case 50: return DxtFormat.A8R8G8B8;
                    case 18:
                    case 51: return DxtFormat.Dxt1;
                    case 19:
                    case 52: return DxtFormat.Dxt3;
                    case 20:
                    case 53: return DxtFormat.Dxt5;
                    case 49: return DxtFormat.Dxn;
                    case 59: return DxtFormat.Dxt5A;
                }
                return DxtFormat.Unknown;
            }
        }

        public int BytesPerBlock { get { return Dxt.BytesPerBlock(this.Codec); } }
        public int BlockSize { get { return (this.Codec == DxtFormat.A8R8G8B8) ? 1 : 4; } }

        // Mip page 0 means "no mips", except when the whole chain is packed in the base tail
        // (small textures), where the mips share the base page.
        public int LevelCount
        {
            get
            {
                if (this.MipPage == 0 && !this.MipsInBaseTail)
                {
                    return 1;
                }
                return Math.Min(this.MipMaxLevel, Log2Floor(Math.Max(this.Width, this.Height))) + 1;
            }
        }

        internal bool MipsInBaseTail
        {
            get { return this.MipPage == 0 && this.PackedLevel == 0; }
        }

        public override string ToString()
        {
            return string.Format("{0}x{1} {2}{3} levels={4} packed={5} tiled={6}", this.Width, this.Height, this.Codec, this.IsGamma ? " gamma" : "", this.LevelCount, this.PackedMips, this.Tiled);
        }

        internal static int DivUp(int value, int divisor) { return (value + divisor - 1) / divisor; }
        internal static int Align(int value, int alignment) { return DivUp(value, alignment) * alignment; }

        internal static int Log2Floor(int value)
        {
            int log = 0;
            while ((1 << (log + 1)) <= value)
            {
                log++;
            }
            return log;
        }

        internal static int Log2Ceil(int value)
        {
            int log = 0;
            while ((1 << log) < value)
            {
                log++;
            }
            return log;
        }

        internal static int NextPow2(int value) { return 1 << Log2Ceil(value); }
    }
}
