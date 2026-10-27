namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Media;
    using System.Text;
    using System.Windows.Forms;

    // Listens to the turbo sounds of a game folder (Media\audio\cars\Turbos\turbos.fsb: BlowOff_L<level>_<choice>_<HI|MED>_<n> and
    // the turbo1_loop*_V5 whistle loops; format and decoding from the Fm4 Sound SDK). vgmstream-cli decodes the whole bank once
    // into the temp folder (one WAV per sample, numbered in bank order), the names come from the FSB4 header.
    public static class TurboPreview
    {
        private static SoundPlayer player;
        private static string preparedFor;
        private static string folder;
        private static string[] names;

        public static string Decoder
        {
            get { return Path.Combine(Path.Combine(Application.StartupPath, "vgmstream"), "vgmstream-cli.exe"); }
        }

        public static string BankPath(string root)
        {
            return Path.Combine(Path.Combine(Path.Combine(Path.Combine(Path.Combine(root, "Media"), "audio"), "cars"), "Turbos"), "turbos.fsb");
        }

        // The FSB4 sample names, in bank order (header 0x30, one record per sample: size u16, name 30 bytes...).
        private static string[] ReadNames(byte[] d)
        {
            if (d.Length < 0x30 || Encoding.ASCII.GetString(d, 0, 4) != "FSB4")
            {
                throw new InvalidDataException("turbos.fsb is not an FSB4 bank.");
            }
            int count = BitConverter.ToInt32(d, 4);
            string[] result = new string[count];
            int p = 0x30;
            for (int i = 0; i < count; i++)
            {
                int size = BitConverter.ToUInt16(d, p);
                int end = Array.IndexOf(d, (byte) 0, p + 2, 30);
                result[i] = Encoding.GetEncoding(28591).GetString(d, p + 2, (end < 0 ? p + 32 : end) - (p + 2));
                p += size;
            }
            return result;
        }

        // Decodes the bank (once per game folder and file date); throws a readable message when it cannot.
        private static void Prepare(string root)
        {
            string bank = BankPath(root);
            if (!File.Exists(bank))
            {
                throw new FileNotFoundException("The game folder has no Media\\audio\\cars\\Turbos\\turbos.fsb.");
            }
            string key = bank + "|" + new FileInfo(bank).Length + "|" + File.GetLastWriteTimeUtc(bank).Ticks;
            if (key == preparedFor)
            {
                return;
            }
            if (!File.Exists(Decoder))
            {
                throw new FileNotFoundException("vgmstream-cli.exe is missing next to the program (vgmstream folder).");
            }
            names = ReadNames(File.ReadAllBytes(bank));
            string tag = Math.Abs(key.GetHashCode()).ToString("x8");
            folder = Path.Combine(Path.Combine(Path.GetTempPath(), "ForzaStudioTurbo"), tag);
            if (!Directory.Exists(folder) || Directory.GetFiles(folder, "*.wav").Length < names.Length)
            {
                Directory.CreateDirectory(folder);
                ProcessStartInfo start = new ProcessStartInfo(Decoder, "-i -S 0 -o \"" + Path.Combine(folder, "?03s.wav") + "\" \"" + bank + "\"");
                start.UseShellExecute = false;
                start.CreateNoWindow = true;
                start.RedirectStandardOutput = true;
                start.RedirectStandardError = true;
                using (Process process = Process.Start(start))
                {
                    string error = null;
                    process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e) { error += e.Data + "\n"; };
                    process.BeginErrorReadLine();
                    process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    if (process.ExitCode != 0)
                    {
                        throw new IOException("vgmstream could not decode turbos.fsb:\n" + error);
                    }
                }
            }
            preparedFor = key;
        }

        // Plays the first sample whose name starts with the prefix (e.g. "BlowOff_L1_1_HI", "turbo1_loop7"). Returns the name played.
        public static string Play(string root, string prefix)
        {
            Prepare(root);
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    string wav = Path.Combine(folder, (i + 1).ToString("000") + ".wav");
                    if (!File.Exists(wav))
                    {
                        throw new FileNotFoundException("The decoded sample " + names[i] + " is missing.");
                    }
                    Stop();
                    player = new SoundPlayer(wav);
                    player.Play();
                    return names[i];
                }
            }
            throw new InvalidOperationException("The bank has no sample " + prefix + "*.");
        }

        public static void Stop()
        {
            if (player != null)
            {
                player.Stop();
                player.Dispose();
                player = null;
            }
        }
    }
}
