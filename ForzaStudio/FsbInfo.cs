namespace ForzaStudio
{
    using System;
    using System.IO;
    using System.Text;

    // What the header of an FMOD FSB4 bank (or the older FSB3 of the FM3 packs: 0x18 byte header, the same sample records) says about its samples (header 0x30 bytes, then one record per sample: size u16,
    // name 30 bytes, length in samples, compressed bytes, loop start/end, mode, default frequency, volume, pan, priority,
    // channels). Only the header is read, never the audio data, so a bank inside an Xbox 360 package costs a few KB.
    public sealed class FsbInfo
    {
        public int Samples;
        public long DataSize;
        public string Format = "";      // XMA, IMA ADPCM, MPEG or PCM, from the first sample
        public int Loops;               // samples with the loop flag
        public bool Partial;            // an FSB3 bank whose later sample records were not decoded: only the count is sure
        public string[] Names;
        public int[] Frequency;
        public int[] Channels;
        public int[] Length;            // in sample frames

        public double Seconds(int sample)
        {
            return (Frequency[sample] > 0) ? (double) this.Length[sample] / Frequency[sample] : 0;
        }

        // The streams the game can address: a harmonic tuning's wavebankindex counts the channels of every sample, so a bank of
        // stereo samples answers to twice as many indexes as it has samples.
        public int Waves()
        {
            int waves = 0;
            for (int i = 0; i < this.Samples; i++)
            {
                waves += Math.Max(1, this.Channels[i]);
            }
            return waves;
        }

        // Samples that cannot play: no frequency, no length or no channels.
        public int BrokenSamples()
        {
            int broken = 0;
            for (int i = 0; i < this.Samples; i++)
            {
                if (this.Frequency[i] <= 0 || this.Length[i] <= 0 || this.Channels[i] <= 0)
                {
                    broken++;
                }
            }
            return broken;
        }

        public static FsbInfo Read(Stream stream)
        {
            byte[] head = new byte[0x18];
            if (!Fill(stream, head))
            {
                throw new InvalidDataException("shorter than an FSB header");
            }
            string magic = Encoding.ASCII.GetString(head, 0, 4);
            if (magic != "FSB4" && magic != "FSB3")
            {
                throw new InvalidDataException("not an FSB3 or FSB4 bank");
            }
            if (magic == "FSB4" && !Fill(stream, new byte[0x18]))
            {
                throw new InvalidDataException("the FSB4 header is cut short");
            }
            int count = BitConverter.ToInt32(head, 4);
            int headers = BitConverter.ToInt32(head, 8);
            if (count < 0 || count > 100000 || headers < 0 || headers > 64 * 1024 * 1024)
            {
                throw new InvalidDataException("the FSB4 header is damaged (" + count + " samples, " + headers + " bytes of sample headers)");
            }
            byte[] table = new byte[headers];
            if (!Fill(stream, table))
            {
                throw new InvalidDataException("the FSB4 sample headers are cut short");
            }
            FsbInfo info = new FsbInfo();
            info.Samples = count;
            info.DataSize = BitConverter.ToUInt32(head, 12);
            info.Names = new string[count];
            info.Frequency = new int[count];
            info.Channels = new int[count];
            info.Length = new int[count];
            int p = 0;
            for (int i = 0; i < count; i++)
            {
                int size = (p + 2 <= headers) ? BitConverter.ToUInt16(table, p) : 0;
                if (size < 0x40 || p + size > headers)
                {
                    if (magic == "FSB3" && i > 0)
                    {
                        // some FSB3 banks of the FM3 packs carry records of another size: what was read stands, the rest is unknown
                        info.Partial = true;
                        for (int k = i; k < count; k++)
                        {
                            info.Names[k] = "";
                            info.Channels[k] = 1;
                        }
                        break;
                    }
                    throw new InvalidDataException("the " + magic + " sample header " + i + " is damaged");
                }
                int end = Array.IndexOf(table, (byte) 0, p + 2, 30);
                info.Names[i] = Encoding.GetEncoding(28591).GetString(table, p + 2, (end < 0 ? p + 32 : end) - (p + 2));
                info.Length[i] = BitConverter.ToInt32(table, p + 0x20);
                uint mode = BitConverter.ToUInt32(table, p + 0x30);
                info.Frequency[i] = BitConverter.ToInt32(table, p + 0x34);
                info.Channels[i] = BitConverter.ToUInt16(table, p + 0x3E);
                if ((mode & 2) != 0)
                {
                    info.Loops++;
                }
                if (i == 0)
                {
                    info.Format = ((mode & 0x01000000) != 0) ? "XMA" : ((mode & 0x00400000) != 0) ? "IMA ADPCM" : ((mode & 0x200) != 0) ? "MPEG" : "PCM";
                }
                p += size;
            }
            return info;
        }

        private static bool Fill(Stream stream, byte[] buffer)
        {
            int total = 0;
            while (total < buffer.Length)
            {
                int read = stream.Read(buffer, total, buffer.Length - total);
                if (read <= 0)
                {
                    return false;
                }
                total += read;
            }
            return true;
        }
    }
}
