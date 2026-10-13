namespace ForzaStudio
{
    using System;

    public partial class XdsTexture
    {
        public struct Level
        {
            public int Width;
            public int Height;
            public int WidthBlocks;
            public int HeightBlocks;
            public int Offset;          // start of the storage region inside Data
            public int PitchBlocks;     // tile aligned pitch of that region
            public int OffsetXBlocks;   // position inside a packed mip tail
            public int OffsetYBlocks;
        }

        // First level stored in the packed mip tail (levels whose smaller side is <= 16 texels).
        public int PackedLevel
        {
            get
            {
                if (!this.PackedMips)
                {
                    return int.MaxValue;
                }
                int log2 = Log2Ceil(Math.Min(this.Width, this.Height));
                return (log2 > 4) ? (log2 - 4) : 0;
            }
        }

        private int StoragePitchBlocks(int level, bool isBase)
        {
            int texels = isBase ? this.PitchTexels : Math.Max(NextPow2(this.Width) >> level, 1);
            return Align(DivUp(texels, this.BlockSize), TileBlocks);
        }

        private int StorageHeightBlocks(int level, bool isBase)
        {
            int texels = isBase ? this.Height : Math.Max(NextPow2(this.Height) >> level, 1);
            return Align(DivUp(texels, this.BlockSize), TileBlocks);
        }

        // Linear textures keep every block row 256 byte aligned.
        private int RowPitchBytes(int pitchBlocks)
        {
            int bytes = pitchBlocks * this.BytesPerBlock;
            return this.Tiled ? bytes : Align(bytes, 256);
        }

        private int StorageStride(int level, bool isBase)
        {
            return Align(this.RowPitchBytes(this.StoragePitchBlocks(level, isBase)) * this.StorageHeightBlocks(level, isBase), PageSize);
        }

        public Level GetLevel(int level)
        {
            Level info = new Level();
            info.Width = Math.Max(this.Width >> level, 1);
            info.Height = Math.Max(this.Height >> level, 1);
            info.WidthBlocks = DivUp(info.Width, this.BlockSize);
            info.HeightBlocks = DivUp(info.Height, this.BlockSize);

            int packedLevel = this.PackedLevel;
            if (level == 0 || this.MipsInBaseTail)
            {
                info.Offset = this.BasePage * PageSize;
                info.PitchBlocks = this.StoragePitchBlocks(0, true);
            }
            else
            {
                // With packed level 0 the mip region holds the whole tail, laid out as level 0.
                int storageLevel = (packedLevel == 0) ? 0 : Math.Min(level, packedLevel);
                int offset = this.MipPage * PageSize;
                for (int l = 1; l < storageLevel; l++)
                {
                    offset += this.StorageStride(l, false);
                }
                info.Offset = offset;
                info.PitchBlocks = this.StoragePitchBlocks(storageLevel, false);
            }
            if (level >= packedLevel)
            {
                this.GetPackedMipOffset(level, out info.OffsetXBlocks, out info.OffsetYBlocks);
            }
            return info;
        }

        private void GetPackedMipOffset(int mip, out int xBlocks, out int yBlocks)
        {
            int log2Width = Log2Ceil(this.Width);
            int log2Height = Log2Ceil(this.Height);
            int log2Size = Math.Min(log2Width, log2Height);
            int packedBase = (log2Size > 4) ? (log2Size - 4) : 0;
            int packed = mip - packedBase;
            xBlocks = 0;
            yBlocks = 0;
            if (packed < 3)
            {
                if (log2Width > log2Height)
                {
                    yBlocks = 16 >> packed;
                }
                else
                {
                    xBlocks = 16 >> packed;
                }
            }
            else if (log2Width > log2Height)
            {
                xBlocks = (1 << (log2Width - packedBase)) >> (packed - 2);
            }
            else
            {
                yBlocks = (1 << (log2Height - packedBase)) >> (packed - 2);
            }
            xBlocks /= this.BlockSize;
            yBlocks /= this.BlockSize;
        }

        private int BlockAddress(Level info, int x, int y)
        {
            int bpb = this.BytesPerBlock;
            x += info.OffsetXBlocks;
            y += info.OffsetYBlocks;
            if (this.Tiled)
            {
                return info.Offset + XenosTiling.Tiled2D(x, y, info.PitchBlocks, Log2Floor(bpb));
            }
            return info.Offset + (y * this.RowPitchBytes(info.PitchBlocks)) + (x * bpb);
        }

        // Returns the level as linear, little endian (PC order) blocks.
        public byte[] ReadLevelBlocks(int level)
        {
            Level info = this.GetLevel(level);
            int bpb = this.BytesPerBlock;
            byte[] blocks = new byte[info.WidthBlocks * info.HeightBlocks * bpb];
            for (int y = 0; y < info.HeightBlocks; y++)
            {
                for (int x = 0; x < info.WidthBlocks; x++)
                {
                    int src = this.BlockAddress(info, x, y);
                    if (src + bpb <= this.Data.Length)
                    {
                        Buffer.BlockCopy(this.Data, src, blocks, ((y * info.WidthBlocks) + x) * bpb, bpb);
                    }
                }
            }
            XenosTiling.SwapEndian(blocks, 0, blocks.Length, this.Endian);
            return blocks;
        }

        // Inverse of ReadLevelBlocks: blocks must be linear, little endian, exact level size.
        public void WriteLevelBlocks(int level, byte[] blocks)
        {
            Level info = this.GetLevel(level);
            int bpb = this.BytesPerBlock;
            if (blocks.Length != info.WidthBlocks * info.HeightBlocks * bpb)
            {
                throw new ArgumentException(string.Format("Level {0} expects {1} bytes, got {2}.", level, info.WidthBlocks * info.HeightBlocks * bpb, blocks.Length));
            }
            byte[] swapped = (byte[]) blocks.Clone();
            XenosTiling.SwapEndian(swapped, 0, swapped.Length, this.Endian);
            for (int y = 0; y < info.HeightBlocks; y++)
            {
                for (int x = 0; x < info.WidthBlocks; x++)
                {
                    int dst = this.BlockAddress(info, x, y);
                    if (dst + bpb > this.Data.Length)
                    {
                        throw new InvalidOperationException(string.Format("Level {0} block ({1},{2}) is outside the texture data.", level, x, y));
                    }
                    Buffer.BlockCopy(swapped, ((y * info.WidthBlocks) + x) * bpb, this.Data, dst, bpb);
                }
            }
        }

        // Decodes a level to BGRA (4 bytes per pixel).
        public byte[] DecodeLevel(int level)
        {
            Level info = this.GetLevel(level);
            return Dxt.Decode(this.Codec, this.ReadLevelBlocks(level), info.Width, info.Height);
        }
    }
}
