namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;

    // Reads an Xbox 360 STFS package (CON, LIVE, PIRS): the header fields the port needs and its files
    // (ROADMAP topic 3 j). The layout is the one StfsPackage writes (Free60 / Velocity): blocks of 4 KB
    // after the header, the data blocks interleaved with their hash tables. A package built by the
    // authoring tool or by us has one table per level (block separation bit 0 set); a console save has two
    // copies of each (the current one is found by the SHA-1 of the block). Files whose blocks are not
    // consecutive follow the chain of the level-0 table.
    public class StfsReader
    {
        private const int Block = 0x1000;
        private const int PerTable = 170;
        private const int Super = PerTable * PerTable;

        public string Path;
        public string Magic;            // "CON ", "LIVE" or "PIRS"
        public uint TitleId;
        public uint ContentType;
        public string DisplayName;
        public byte[] Header;           // everything before the first block (StfsPackage.Rewrite keeps it)

        private long firstBlock;        // the header size rounded up to a block
        private int shift;              // 0: one hash table per level, 1: two
        private int tableBlocks;
        private int tableStart;
        private readonly List<Entry> entries = new List<Entry>();
        private Dictionary<string, Entry> byPath;

        private class Entry
        {
            public long[] Offsets;      // OpenFile: where each block of the file sits
            public string Name;
            public bool IsFolder;
            public bool Consecutive;
            public int Blocks;
            public int Start;
            public int Parent;
            public long Size;
        }

        // True when the file starts with an STFS magic.
        public static bool IsPackage(string path)
        {
            try
            {
                using (FileStream file = File.OpenRead(path))
                {
                    byte[] magic = new byte[4];
                    return file.Length > 0xA000 && file.Read(magic, 0, 4) == 4 && MagicOf(magic) != null;
                }
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static string MagicOf(byte[] magic)
        {
            string text = Encoding.ASCII.GetString(magic, 0, 4);
            return (text == "CON " || text == "LIVE" || text == "PIRS") ? text : null;
        }

        public static StfsReader Open(string path)
        {
            StfsReader package = new StfsReader();
            package.Path = path;
            using (FileStream file = File.OpenRead(path))
            {
                byte[] header = new byte[0x1712];
                if (file.Length < 0xA000 || file.Read(header, 0, header.Length) != header.Length || (package.Magic = MagicOf(header)) == null)
                {
                    throw new InvalidDataException("Not an Xbox 360 package (no CON, LIVE or PIRS header): " + path);
                }
                package.ContentType = Big32(header, 0x344);
                package.TitleId = Big32(header, 0x360);
                package.DisplayName = Encoding.BigEndianUnicode.GetString(header, 0x411, 0x80).TrimEnd('\0');
                package.firstBlock = (Big32(header, 0x340) + Block - 1) & ~(long) (Block - 1);
                if (header[0x379] != 0x24)
                {
                    throw new InvalidDataException("Not an STFS package (an SVOD or unknown volume): " + path);
                }
                package.shift = ((header[0x37B] & 1) == 1) ? 0 : 1;
                package.tableBlocks = header[0x37C] | (header[0x37D] << 8);
                package.tableStart = Little24(header, 0x37E);
                if (package.firstBlock < header.Length || package.firstBlock > 0x10000 || package.firstBlock > file.Length)
                {
                    throw new InvalidDataException("The package header is damaged (header size " + package.firstBlock + "): " + path);
                }
                package.Header = new byte[package.firstBlock];
                file.Position = 0;
                ReadFull(file, package.Header);
                package.ReadTable(file);
            }
            return package;
        }

        private void ReadTable(FileStream file)
        {
            byte[] table = this.ReadChain(file, this.tableStart, this.tableBlocks, (long) this.tableBlocks * Block, true);
            for (int at = 0; at + 0x40 <= table.Length; at += 0x40)
            {
                int length = table[at + 0x28] & 0x3F;
                if (length == 0)
                {
                    break;
                }
                Entry entry = new Entry();
                entry.Name = Encoding.ASCII.GetString(table, at, length);
                entry.IsFolder = (table[at + 0x28] & 0x80) != 0;
                entry.Consecutive = (table[at + 0x28] & 0x40) != 0;
                entry.Blocks = Little24(table, at + 0x29);
                entry.Start = Little24(table, at + 0x2F);
                entry.Parent = (table[at + 0x32] << 8) | table[at + 0x33];
                entry.Size = Big32(table, at + 0x34);
                this.entries.Add(entry);
            }
        }

        // The package's files, path relative to its root ('\').
        public List<string> Files()
        {
            List<string> files = new List<string>();
            for (int i = 0; i < this.entries.Count; i++)
            {
                if (!this.entries[i].IsFolder)
                {
                    files.Add(this.RelativePath(i));
                }
            }
            return files;
        }

        // One file's bytes (path relative to the package root, '\' or '/', any case); null when the
        // package has no such file.
        public byte[] ReadFile(string relativePath)
        {
            string wanted = relativePath.Replace('/', '\\');
            for (int i = 0; i < this.entries.Count; i++)
            {
                if (!this.entries[i].IsFolder && string.Equals(this.RelativePath(i), wanted, StringComparison.OrdinalIgnoreCase))
                {
                    Entry entry = this.entries[i];
                    using (FileStream file = File.OpenRead(this.Path))
                    {
                        return this.ReadChain(file, entry.Start, entry.Blocks, entry.Size, entry.Consecutive);
                    }
                }
            }
            return null;
        }

        private Entry FindFile(string relativePath)
        {
            string wanted = relativePath.Replace('/', '\\');
            lock (this.entries)
            {
                if (this.byPath == null)
                {
                    this.byPath = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < this.entries.Count; i++)
                    {
                        if (!this.entries[i].IsFolder)
                        {
                            this.byPath[this.RelativePath(i)] = this.entries[i];
                        }
                    }
                }
                Entry found;
                return this.byPath.TryGetValue(wanted, out found) ? found : null;
            }
        }

        // The size of one file (path as in ReadFile), -1 when the package has no such file.
        public long FileSize(string relativePath)
        {
            Entry entry = this.FindFile(relativePath);
            return (entry != null) ? entry.Size : -1;
        }

        // One file as a read-only seekable stream, read from the package in place (a zip of a few hundred MB is
        // opened without extracting it). null when the package has no such file.
        public Stream OpenFile(string relativePath)
        {
            Entry entry = this.FindFile(relativePath);
            if (entry == null)
            {
                return null;
            }
            long[] offsets;
            lock (entry)
            {
                if (entry.Offsets == null)
                {
                    entry.Offsets = this.BlockOffsets(entry);
                }
                offsets = entry.Offsets;
            }
            return new FileView(new FileStream(this.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete), offsets, entry.Size);
        }

        // Where each 4 KB block of a file sits in the package file.
        private long[] BlockOffsets(Entry entry)
        {
            int count = (int) ((entry.Size + Block - 1) / Block);
            long[] offsets = new long[count];
            int block = entry.Start;
            using (FileStream file = new FileStream(this.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                byte[] buffer = new byte[Block];
                for (int k = 0; k < count; k++)
                {
                    long at = this.firstBlock + (this.Backing(block) * Block);
                    if (at + Block > file.Length)
                    {
                        throw new InvalidDataException(string.Format("The package is incomplete ({0:N0} of at least {1:N0} bytes): {2}", file.Length, at + Block, this.Path));
                    }
                    offsets[k] = at;
                    if (k + 1 < count)
                    {
                        if (entry.Consecutive)
                        {
                            block++;
                        }
                        else
                        {
                            file.Position = at;
                            ReadFull(file, buffer);
                            block = this.Next(file, block, buffer);
                        }
                    }
                }
            }
            return offsets;
        }

        private class FileView : Stream
        {
            private readonly FileStream file;
            private readonly long[] offsets;
            private readonly long length;
            private long position;

            public FileView(FileStream file, long[] offsets, long length)
            {
                this.file = file;
                this.offsets = offsets;
                this.length = length;
            }

            public override bool CanRead { get { return true; } }
            public override bool CanSeek { get { return true; } }
            public override bool CanWrite { get { return false; } }
            public override long Length { get { return this.length; } }

            public override long Position
            {
                get { return this.position; }
                set { this.position = value; }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                int done = 0;
                while (count > 0 && this.position < this.length)
                {
                    int block = (int) (this.position / Block);
                    int within = (int) (this.position % Block);
                    // Blocks that follow each other in the package are read in one go.
                    int run = 1;
                    while (block + run < this.offsets.Length && this.offsets[block + run] == this.offsets[block] + (long) run * Block && (long) run * Block - within < count)
                    {
                        run++;
                    }
                    long take = Math.Min(Math.Min((long) run * Block - within, count), this.length - this.position);
                    this.file.Position = this.offsets[block] + within;
                    int read = this.file.Read(buffer, offset, (int) take);
                    if (read <= 0)
                    {
                        throw new EndOfStreamException();
                    }
                    this.position += read;
                    offset += read;
                    count -= read;
                    done += read;
                }
                return done;
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                switch (origin)
                {
                    case SeekOrigin.Begin:
                        this.position = offset;
                        break;
                    case SeekOrigin.Current:
                        this.position += offset;
                        break;
                    default:
                        this.position = this.length + offset;
                        break;
                }
                return this.position;
            }

            public override void Flush()
            {
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    this.file.Dispose();
                }
                base.Dispose(disposing);
            }
        }

        private string RelativePath(int index)
        {
            string path = this.entries[index].Name;
            int parent = this.entries[index].Parent;
            for (int guard = 0; parent != 0xFFFF && guard < 64; guard++)
            {
                if (parent >= this.entries.Count)
                {
                    throw new InvalidDataException("The package's file table is damaged: " + this.Path);
                }
                path = System.IO.Path.Combine(this.entries[parent].Name, path);
                parent = this.entries[parent].Parent;
            }
            return path;
        }

        // Every file into 'folder'; returns how many.
        public int Extract(string folder)
        {
            int count = 0;
            using (FileStream file = File.OpenRead(this.Path))
            {
                for (int i = 0; i < this.entries.Count; i++)
                {
                    Entry entry = this.entries[i];
                    string path = System.IO.Path.Combine(folder, this.RelativePath(i));
                    if (entry.IsFolder)
                    {
                        Directory.CreateDirectory(path);
                        continue;
                    }
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                    // Streamed: a car pack's zip can be a few hundred MB (the editor is 32-bit).
                    using (FileStream output = new FileStream(path, FileMode.Create, FileAccess.Write))
                    {
                        this.CopyChain(file, entry.Start, entry.Blocks, entry.Size, entry.Consecutive, output);
                    }
                    count++;
                }
            }
            return count;
        }

        // 'size' bytes from 'blocks' data blocks starting at 'start'.
        private byte[] ReadChain(FileStream file, int start, int blocks, long size, bool consecutive)
        {
            using (MemoryStream data = new MemoryStream((int) size))
            {
                this.CopyChain(file, start, blocks, size, consecutive, data);
                return data.ToArray();
            }
        }

        private void CopyChain(FileStream file, int start, int blocks, long size, bool consecutive, Stream output)
        {
            byte[] buffer = new byte[Block];
            int block = start;
            long done = 0;
            for (int k = 0; k < blocks && done < size; k++)
            {
                long at = this.firstBlock + (this.Backing(block) * Block);
                if (at + Block > file.Length)
                {
                    throw new InvalidDataException(string.Format("The package is incomplete ({0:N0} of at least {1:N0} bytes): {2}", file.Length, at + Block, this.Path));
                }
                file.Position = at;
                ReadFull(file, buffer);
                int take = (int) Math.Min(Block, size - done);
                output.Write(buffer, 0, take);
                done += take;
                if (k + 1 < blocks)
                {
                    block = consecutive ? block + 1 : this.Next(file, block, buffer);
                }
            }
        }

        // The next block of a chain: the level-0 entry of 'block' (whose data is in 'data').
        private int Next(FileStream file, int block, byte[] data)
        {
            byte[] entry = new byte[0x18];
            long table = this.firstBlock + (this.Level0(block) * Block) + ((block % PerTable) * 0x18);
            file.Position = table;
            ReadFull(file, entry);
            if (this.shift == 1)
            {
                // Two copies of the table: take the one that holds this block's hash.
                byte[] hash;
                using (SHA1 sha = SHA1.Create())
                {
                    hash = sha.ComputeHash(data);
                }
                if (!Same(hash, entry))
                {
                    file.Position = table + Block;
                    ReadFull(file, entry);
                }
            }
            return Big24(entry, 0x15);
        }

        // Where data block b sits among the blocks after the header (Velocity's ComputeDataBlockNumber).
        private long Backing(int b)
        {
            long r = ((long) ((b + PerTable) / PerTable) << this.shift) + b;
            if (b < PerTable)
            {
                return r;
            }
            if (b < Super)
            {
                return r + ((long) ((b + Super) / Super) << this.shift);
            }
            return (1L << this.shift) + r + ((long) ((b + Super) / Super) << this.shift);
        }

        // The level-0 hash table of data block b (Velocity's ComputeLevel0BackingHashBlockNumber).
        private long Level0(int b)
        {
            if (b < PerTable)
            {
                return 0;
            }
            long n = (long) (b / PerTable) * ((this.shift == 0) ? 0xAB : 0xAC);
            n += (long) ((b / Super) + 1) << this.shift;
            return (b / Super == 0) ? n : n + (1L << this.shift);
        }

        private static bool Same(byte[] hash, byte[] entry)
        {
            for (int i = 0; i < 20; i++)
            {
                if (hash[i] != entry[i])
                {
                    return false;
                }
            }
            return true;
        }

        private static void ReadFull(Stream input, byte[] buffer)
        {
            int done = 0;
            while (done < buffer.Length)
            {
                int read = input.Read(buffer, done, buffer.Length - done);
                if (read <= 0)
                {
                    throw new EndOfStreamException();
                }
                done += read;
            }
        }

        private static uint Big32(byte[] data, int offset)
        {
            return (uint) ((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3]);
        }

        private static int Big24(byte[] data, int offset)
        {
            return (data[offset] << 16) | (data[offset + 1] << 8) | data[offset + 2];
        }

        private static int Little24(byte[] data, int offset)
        {
            return data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16);
        }
    }
}
