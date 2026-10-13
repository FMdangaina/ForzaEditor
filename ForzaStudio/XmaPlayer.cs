namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Drawing;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading;
    using System.Windows.Forms;

    // The game's music (GAME:\Media\audio\Music\*.xma, Xbox XMA2). vgmstream-cli (in the
    // vgmstream folder next to the exe) decodes a track to PCM on its standard output and the audio is
    // streamed to the sound card through waveOut: no temporary files, a second of buffering.
    internal sealed class XmaPlayer : IDisposable
    {
        private const int BufferCount = 6;
        private const int BufferSize = 32768;
        private const int WaveMapper = -1;
        private const int HeaderDone = 1;
        private const int HeaderFlagsOffset = 16;   // WAVEHDR.dwFlags (32-bit layout)

        [StructLayout(LayoutKind.Sequential)]
        private struct WaveFormat
        {
            public short Tag;
            public short Channels;
            public int SampleRate;
            public int ByteRate;
            public short BlockAlign;
            public short Bits;
            public short Extra;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WaveHeader
        {
            public IntPtr Data;
            public int Length;
            public int Recorded;
            public IntPtr User;
            public int Flags;
            public int Loops;
            public IntPtr Next;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MmTime
        {
            public int Type;
            public int Value;
            public int Pad;
        }

        [DllImport("winmm.dll")] private static extern int waveOutOpen(out IntPtr handle, int device, ref WaveFormat format, IntPtr callback, IntPtr instance, int flags);
        [DllImport("winmm.dll")] private static extern int waveOutPrepareHeader(IntPtr handle, IntPtr header, int size);
        [DllImport("winmm.dll")] private static extern int waveOutUnprepareHeader(IntPtr handle, IntPtr header, int size);
        [DllImport("winmm.dll")] private static extern int waveOutWrite(IntPtr handle, IntPtr header, int size);
        [DllImport("winmm.dll")] private static extern int waveOutReset(IntPtr handle);
        [DllImport("winmm.dll")] private static extern int waveOutClose(IntPtr handle);
        [DllImport("winmm.dll")] private static extern int waveOutPause(IntPtr handle);
        [DllImport("winmm.dll")] private static extern int waveOutRestart(IntPtr handle);
        [DllImport("winmm.dll")] private static extern int waveOutSetVolume(IntPtr handle, uint volume);
        [DllImport("winmm.dll")] private static extern int waveOutGetPosition(IntPtr handle, ref MmTime time, int size);

        private readonly string decoder;
        private Process process;
        private Thread thread;
        private IntPtr device;
        private volatile bool stopping;
        private int byteRate;
        private long dataLength;
        private int volume = 0xC000;

        public event EventHandler Finished;

        public bool Paused { get; private set; }
        public bool Playing { get; private set; }

        public XmaPlayer(string decoder)
        {
            this.decoder = decoder;
        }

        public TimeSpan Length
        {
            get { return (this.byteRate > 0) ? TimeSpan.FromSeconds((double) this.dataLength / this.byteRate) : TimeSpan.Zero; }
        }

        public TimeSpan Position
        {
            get
            {
                IntPtr handle = this.device;
                if (handle == IntPtr.Zero || this.byteRate == 0)
                {
                    return TimeSpan.Zero;
                }
                MmTime time = new MmTime();
                time.Type = 4;  // TIME_BYTES
                waveOutGetPosition(handle, ref time, Marshal.SizeOf(typeof(MmTime)));
                return TimeSpan.FromSeconds((double) (uint) time.Value / this.byteRate);
            }
        }

        // 0..100
        public int Volume
        {
            get { return (this.volume * 100) / 0xFFFF; }
            set
            {
                this.volume = Math.Max(0, Math.Min(100, value)) * 0xFFFF / 100;
                if (this.device != IntPtr.Zero)
                {
                    waveOutSetVolume(this.device, (uint) ((this.volume << 16) | this.volume));
                }
            }
        }

        public void Play(string file)
        {
            this.Stop();
            ProcessStartInfo start = new ProcessStartInfo(this.decoder, "-p \"" + file + "\"");
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            start.RedirectStandardOutput = true;
            start.RedirectStandardError = true;
            start.WorkingDirectory = Path.GetDirectoryName(this.decoder);
            this.process = Process.Start(start);
            this.process.ErrorDataReceived += delegate { };
            this.process.BeginErrorReadLine();
            Stream pcm = this.process.StandardOutput.BaseStream;
            WaveFormat format = ReadWaveHeader(pcm, out this.dataLength);
            this.byteRate = format.ByteRate;
            if (waveOutOpen(out this.device, WaveMapper, ref format, IntPtr.Zero, IntPtr.Zero, 0) != 0)
            {
                this.Stop();
                throw new InvalidOperationException("Cannot open the sound device.");
            }
            this.Volume = this.Volume;
            this.stopping = false;
            this.Paused = false;
            this.Playing = true;
            this.thread = new Thread(delegate() { this.Stream(pcm); });
            this.thread.IsBackground = true;
            this.thread.Start();
        }

        // RIFF/WAVE from vgmstream: the fmt chunk, then the data chunk (its size gives the length).
        private static WaveFormat ReadWaveHeader(Stream pcm, out long dataLength)
        {
            byte[] riff = ReadExactly(pcm, 12);
            if (Encoding.ASCII.GetString(riff, 0, 4) != "RIFF" || Encoding.ASCII.GetString(riff, 8, 4) != "WAVE")
            {
                throw new InvalidDataException("The decoder did not return audio.");
            }
            WaveFormat format = new WaveFormat();
            while (true)
            {
                byte[] chunk = ReadExactly(pcm, 8);
                string id = Encoding.ASCII.GetString(chunk, 0, 4);
                int size = BitConverter.ToInt32(chunk, 4);
                if (id == "data")
                {
                    dataLength = (uint) size;
                    return format;
                }
                byte[] body = ReadExactly(pcm, size + (size & 1));
                if (id == "fmt ")
                {
                    format.Tag = BitConverter.ToInt16(body, 0);
                    format.Channels = BitConverter.ToInt16(body, 2);
                    format.SampleRate = BitConverter.ToInt32(body, 4);
                    format.ByteRate = BitConverter.ToInt32(body, 8);
                    format.BlockAlign = BitConverter.ToInt16(body, 12);
                    format.Bits = BitConverter.ToInt16(body, 14);
                }
            }
        }

        private static byte[] ReadExactly(Stream stream, int length)
        {
            byte[] data = new byte[length];
            int total = 0;
            while (total < length)
            {
                int read = stream.Read(data, total, length - total);
                if (read <= 0)
                {
                    throw new EndOfStreamException("The decoder stopped early.");
                }
                total += read;
            }
            return data;
        }

        private void Stream(Stream pcm)
        {
            int headerSize = Marshal.SizeOf(typeof(WaveHeader));
            IntPtr[] headers = new IntPtr[BufferCount];
            bool[] queued = new bool[BufferCount];
            byte[] chunk = new byte[BufferSize];
            for (int i = 0; i < BufferCount; i++)
            {
                WaveHeader header = new WaveHeader();
                header.Data = Marshal.AllocHGlobal(BufferSize);
                headers[i] = Marshal.AllocHGlobal(headerSize);
                Marshal.StructureToPtr(header, headers[i], false);
            }
            bool ended = false;
            bool natural = false;
            try
            {
                while (!this.stopping)
                {
                    bool busy = false;
                    for (int i = 0; i < BufferCount && !this.stopping; i++)
                    {
                        if (queued[i] && (Marshal.ReadInt32(headers[i], HeaderFlagsOffset) & HeaderDone) == 0)
                        {
                            busy = true;
                            continue;
                        }
                        if (queued[i])
                        {
                            waveOutUnprepareHeader(this.device, headers[i], headerSize);
                            queued[i] = false;
                        }
                        if (ended)
                        {
                            continue;
                        }
                        int filled = 0;
                        while (filled < BufferSize)
                        {
                            int read = pcm.Read(chunk, filled, BufferSize - filled);
                            if (read <= 0)
                            {
                                ended = true;
                                break;
                            }
                            filled += read;
                        }
                        filled -= filled % 4;
                        if (filled == 0)
                        {
                            continue;
                        }
                        WaveHeader header = (WaveHeader) Marshal.PtrToStructure(headers[i], typeof(WaveHeader));
                        Marshal.Copy(chunk, 0, header.Data, filled);
                        header.Length = filled;
                        header.Flags = 0;
                        Marshal.StructureToPtr(header, headers[i], false);
                        waveOutPrepareHeader(this.device, headers[i], headerSize);
                        waveOutWrite(this.device, headers[i], headerSize);
                        queued[i] = true;
                        busy = true;
                    }
                    if (ended && !busy)
                    {
                        natural = true;
                        break;
                    }
                    Thread.Sleep(15);
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                waveOutReset(this.device);
                for (int i = 0; i < BufferCount; i++)
                {
                    if (queued[i])
                    {
                        waveOutUnprepareHeader(this.device, headers[i], headerSize);
                    }
                    WaveHeader header = (WaveHeader) Marshal.PtrToStructure(headers[i], typeof(WaveHeader));
                    Marshal.FreeHGlobal(header.Data);
                    Marshal.FreeHGlobal(headers[i]);
                }
            }
            if (natural && this.Finished != null)
            {
                this.Finished(this, EventArgs.Empty);
            }
        }

        public void TogglePause()
        {
            if (this.device == IntPtr.Zero)
            {
                return;
            }
            if (this.Paused)
            {
                waveOutRestart(this.device);
            }
            else
            {
                waveOutPause(this.device);
            }
            this.Paused = !this.Paused;
        }

        public void Stop()
        {
            this.stopping = true;
            this.Playing = false;
            if (this.process != null)
            {
                try
                {
                    if (!this.process.HasExited)
                    {
                        this.process.Kill();
                    }
                }
                catch (Exception)
                {
                }
            }
            if (this.thread != null)
            {
                this.thread.Join(2000);
                this.thread = null;
            }
            if (this.device != IntPtr.Zero)
            {
                waveOutReset(this.device);
                waveOutClose(this.device);
                this.device = IntPtr.Zero;
            }
            if (this.process != null)
            {
                this.process.Dispose();
                this.process = null;
            }
            this.Paused = false;
        }

        public void Dispose()
        {
            this.Stop();
        }
    }
}
