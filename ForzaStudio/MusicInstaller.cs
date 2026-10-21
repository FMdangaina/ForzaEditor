namespace ForzaStudio
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Text;

    // Adds a song to an FM4 folder (ideas and format from the Fm4 Sound SDK): any audio file vgmstream
    // reads -> PCM WAV -> XMA2 with the Xbox 360 SDK's xma2encode (64 KB blocks, no loop, like the
    // originals) -> Media\audio\Music\<name>.xma plus a <Song> line in one of the playlists.
    public static class MusicInstaller
    {
        public const string DefaultEncoder = @"C:\Program Files (x86)\Microsoft Xbox 360 SDK\bin\win32\xma2encode.exe";

        // Playlist file by the name the dialog shows.
        public static readonly string[] PlaylistNames = { "In race, layer 1", "In race, layer 2", "Menu (default)", "Menu 2" };
        public static readonly string[] PlaylistFiles = { "InRaceLayer1Playlist.xml", "InRaceLayer2Playlist.xml", "InDefaultUIPlaylist.xml", "InUIPlaylist2.xml" };

        // Letters, digits, '-' and '_' only: the name becomes the file name and the game path.
        public static string SafeName(string name)
        {
            StringBuilder text = new StringBuilder();
            foreach (char c in name)
            {
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-' || c == '_')
                {
                    text.Append(c);
                }
            }
            return text.ToString();
        }

        // Installs the song into the game root's Media\audio\Music. playlist = index in PlaylistFiles, -1 = no playlist.
        // Returns the .xma path; throws with a readable message on failure.
        public static string Install(string input, string decoder, string encoder, string root, string name, int quality,
            int playlist, string title, string artist, Action<string> progress)
        {
            if (!File.Exists(input))
            {
                throw new FileNotFoundException("Song file not found: " + input);
            }
            if (!File.Exists(decoder))
            {
                throw new FileNotFoundException("vgmstream-cli.exe not found: " + decoder);
            }
            if (!File.Exists(encoder))
            {
                throw new FileNotFoundException("xma2encode.exe (Xbox 360 SDK) not found: " + encoder);
            }
            name = SafeName(name);
            if (name.Length == 0)
            {
                throw new ArgumentException("The name needs letters or digits.");
            }
            string music = Path.Combine(Path.Combine(Path.Combine(root, "Media"), "audio"), "Music");
            if (!Directory.Exists(music))
            {
                throw new DirectoryNotFoundException("Not an FM4 folder (no Media\\audio\\Music): " + root);
            }
            string xma = Path.Combine(music, name + ".xma");
            if (File.Exists(xma))
            {
                throw new IOException(name + ".xma already exists in the game: pick another name.");
            }

            string wav = Path.Combine(Path.GetTempPath(), "forzastudio_song_" + Guid.NewGuid().ToString("N") + ".wav");
            string temp = null;
            try
            {
                progress("Decoding " + Path.GetFileName(input) + "...");
                string log = Run(decoder, "-i -o \"" + wav + "\" \"" + input + "\"");
                if (!File.Exists(wav))
                {
                    throw new IOException("Cannot read the audio file:\n" + log);
                }
                progress("Encoding XMA2 (quality " + quality + ")...");
                string tempXma = Path.Combine(Path.GetTempPath(), "forzastudio_song_" + Guid.NewGuid().ToString("N") + ".xma");
                temp = tempXma;
                log = Run(encoder, "\"" + wav + "\" /TargetFile \"" + tempXma + "\" /UseLoopPoints /BlockSize 64 /Quality " + quality);
                if (!File.Exists(tempXma) || new FileInfo(tempXma).Length < 65536 / 4)
                {
                    throw new IOException("xma2encode failed:\n" + log);
                }
                progress("Checking the result...");
                string info = Run(decoder, "-m \"" + tempXma + "\"");
                if (info.IndexOf("Xbox Media Audio 2", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    throw new IOException("The encoded file is not XMA2:\n" + info);
                }
                File.Copy(tempXma, xma, false);
            }
            finally
            {
                TryDelete(wav);
                TryDelete(temp);
            }

            if (playlist >= 0)
            {
                progress("Adding to " + PlaylistFiles[playlist] + "...");
                AddSong(Path.Combine(music, PlaylistFiles[playlist]), name, title, artist);
            }
            return xma;
        }

        // <Song .../> before </Playlist>, the rest of the XML untouched. A copy of the playlist is kept once as .orig.
        public static void AddSong(string playlist, string name, string title, string artist)
        {
            if (!File.Exists(playlist))
            {
                throw new FileNotFoundException("Playlist not found: " + playlist);
            }
            string xml = File.ReadAllText(playlist);
            string file = "Game:\\Media\\Audio\\Music\\" + name + ".xma";
            if (xml.IndexOf(file, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return;
            }
            int end = xml.LastIndexOf("</Playlist>", StringComparison.Ordinal);
            if (end < 0)
            {
                throw new InvalidDataException("No </Playlist> in " + playlist);
            }
            string line = string.Format("  <Song filename=\"{0}\" title=\"{1}\" artist=\"{2}\" album=\"na\" label=\"na\" intro=\"na\" introoffset=\"na\"/>",
                file, Attr(title), Attr(artist));
            string newline = (xml.IndexOf("\r\n", StringComparison.Ordinal) >= 0) ? "\r\n" : "\n";
            if (!File.Exists(playlist + ".orig"))
            {
                File.Copy(playlist, playlist + ".orig");
            }
            File.WriteAllText(playlist, xml.Substring(0, end) + line + newline + xml.Substring(end), new UTF8Encoding(false));
        }

        // Empty -> "na" (the game's "no value"); the quotes and angle brackets escaped.
        private static string Attr(string value)
        {
            value = (value == null) ? "" : value.Trim();
            if (value.Length == 0)
            {
                return "na";
            }
            return value.Replace("&", "&amp;").Replace("\"", "&quot;").Replace("<", "&lt;").Replace(">", "&gt;");
        }

        private static string Run(string exe, string arguments)
        {
            ProcessStartInfo start = new ProcessStartInfo(exe, arguments);
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            start.RedirectStandardOutput = true;
            start.RedirectStandardError = true;
            using (Process process = Process.Start(start))
            {
                string error = null;
                process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e) { error += e.Data + "\n"; };
                process.BeginErrorReadLine();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                return output + error;
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
            }
        }
    }
}
