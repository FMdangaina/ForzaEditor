namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Xml;

    // The music a game folder offers, as named track lists:
    //   FM3, FM4  Media\audio\Music\*.xma: the menu music (UI_ tracks, in the order of the UI playlists),
    //        the in-race playlists and the whole soundtrack (without FM3's song intros).
    //   FM2  Media\audio\Music\*.xma (numbered files): one list per *Playlist.xml (titles and artists
    //        from it) and the whole soundtrack.
    //   FH1, FH2  Media\audio\radio\Radio_Music.fsb, one FMOD bank: its streams are the tracks, in the
    //        order of Radio_Music.lst; RadioSystem.xml (not well-formed XML: bare '&') gives the stations.
    public static class GameMusic
    {
        public class Track
        {
            public string File;
            public int Subsong;         // stream of a multi-track bank (1-based), 0 for a single-track file
            public string Title;
            public string Artist;

            public string Display
            {
                get { return string.IsNullOrEmpty(this.Artist) ? this.Title : this.Artist + " – " + this.Title; }
            }
        }

        public class Source
        {
            public string Name;
            public List<Track> Tracks = new List<Track>();
        }

        // The sources of the game folder 'root' (or of 'folder' itself when it holds .xma files).
        public static List<Source> Load(string root, string folder)
        {
            List<Source> sources = new List<Source>();
            string audio = (root == null) ? null : Path.Combine(Path.Combine(root, "Media"), "audio");
            if (audio != null)
            {
                string radio = Path.Combine(audio, "radio");
                if (File.Exists(Path.Combine(radio, "RadioSystem.xml")) && File.Exists(Path.Combine(radio, "Radio_Music.fsb")))
                {
                    LoadRadio(radio, sources);
                    if (sources.Count > 0)
                    {
                        return sources;
                    }
                }
            }
            string music = (audio != null) ? Path.Combine(audio, "Music") : null;
            if (music == null || !Directory.Exists(music))
            {
                music = folder;
            }
            if (music == null || !Directory.Exists(music) || Directory.GetFiles(music, "*.xma").Length == 0)
            {
                return sources;
            }
            LoadXma(music, sources);
            return sources;
        }

        #region XMA folders (FM2, FM4)

        private static void LoadXma(string music, List<Source> sources)
        {
            // Titles and artists the playlists give (FM2), by file.
            Dictionary<string, Track> known = new Dictionary<string, Track>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, List<Track>> playlists = new Dictionary<string, List<Track>>();
            List<string> playlistOrder = new List<string>();
            // FM3's songs open with a separate intro file the game plays first: not a track of its own.
            Dictionary<string, bool> intros = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in Directory.GetFiles(music, "*Playlist*.xml"))
            {
                List<Track> tracks = ReadPlaylist(music, path, known, intros);
                if (tracks.Count > 0)
                {
                    string name = Path.GetFileNameWithoutExtension(path);
                    playlists[name] = tracks;
                    playlistOrder.Add(name);
                }
            }
            playlistOrder.Sort(StringComparer.OrdinalIgnoreCase);

            List<string> files = new List<string>(Directory.GetFiles(music, "*.xma"));
            files.Sort(NaturalCompare);
            Source all = new Source();
            all.Name = "Whole soundtrack";
            foreach (string file in files)
            {
                Track track;
                if (intros.ContainsKey(file) && !known.ContainsKey(file))
                {
                    continue;
                }
                if (!known.TryGetValue(file, out track))
                {
                    track = new Track();
                    track.File = file;
                    SplitName(Path.GetFileNameWithoutExtension(file), out track.Artist, out track.Title);
                }
                all.Tracks.Add(track);
            }

            if (playlists.ContainsKey("InDefaultUIPlaylist") || HasUiTracks(files))
            {
                // FM4: the menu music, UI playlists first, then any other UI_ track.
                Source menu = new Source();
                menu.Name = "Menu music (UI_ tracks)";
                foreach (string name in new string[] { "InDefaultUIPlaylist", "InUIPlaylist2" })
                {
                    List<Track> list;
                    if (playlists.TryGetValue(name, out list))
                    {
                        AddUnique(menu, list);
                    }
                }
                foreach (Track track in all.Tracks)
                {
                    if (Path.GetFileName(track.File).StartsWith("UI_", StringComparison.OrdinalIgnoreCase))
                    {
                        AddUnique(menu, new List<Track>(new Track[] { track }));
                    }
                }
                if (menu.Tracks.Count > 0)
                {
                    sources.Add(menu);
                }
                // The race music: InRaceLayer1Playlist -> "In race layer 1".
                foreach (string name in playlistOrder)
                {
                    if (name.IndexOf("UI", StringComparison.Ordinal) < 0)
                    {
                        Source source = new Source();
                        source.Name = PlaylistName(name);
                        source.Tracks.AddRange(playlists[name]);
                        sources.Add(source);
                    }
                }
            }
            else
            {
                // FM2: one list per playlist, "OutOfCareerPlaylist" -> "Out of career".
                foreach (string name in playlistOrder)
                {
                    Source source = new Source();
                    source.Name = PlaylistName(name);
                    source.Tracks.AddRange(playlists[name]);
                    sources.Add(source);
                }
                sources.Sort(delegate(Source a, Source b) { return string.Compare(b.Name, a.Name, StringComparison.OrdinalIgnoreCase); });
            }
            sources.Add(all);
        }

        private static bool HasUiTracks(List<string> files)
        {
            foreach (string file in files)
            {
                if (Path.GetFileName(file).StartsWith("UI_", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        // <Song filename="Game:\Media\Audio\Music\0.xma" title="..." artist="..."/>, read with a
        // pattern: FM2's playlists have bare '&' and are not well-formed XML. "na" means no title.
        private static List<Track> ReadPlaylist(string music, string path, Dictionary<string, Track> known, Dictionary<string, bool> intros)
        {
            List<Track> tracks = new List<Track>();
            foreach (Match song in Regex.Matches(File.ReadAllText(path), "<Song\\s([^>]*)>", RegexOptions.IgnoreCase))
            {
                string intro = Text(Attribute(song.Groups[1].Value, "intro"));
                if (intro != null)
                {
                    intros[Path.Combine(music, Path.GetFileName(intro.Replace('/', '\\')))] = true;
                }
                string name = Attribute(song.Groups[1].Value, "filename");
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }
                string file = Path.Combine(music, name.Replace('/', '\\').Substring(name.Replace('/', '\\').LastIndexOf('\\') + 1));
                if (!File.Exists(file))
                {
                    continue;
                }
                Track track;
                if (!known.TryGetValue(file, out track))
                {
                    track = new Track();
                    track.File = file;
                    string title = Text(Attribute(song.Groups[1].Value, "title"));
                    if (title != null)
                    {
                        track.Title = title;
                        track.Artist = Text(Attribute(song.Groups[1].Value, "artist"));
                    }
                    else
                    {
                        SplitName(Path.GetFileNameWithoutExtension(file), out track.Artist, out track.Title);
                    }
                    known[file] = track;
                }
                if (!tracks.Contains(track))
                {
                    tracks.Add(track);
                }
            }
            return tracks;
        }

        // An attribute value as text: entities decoded, null when empty or "na".
        private static string Text(string value)
        {
            if (value == null)
            {
                return null;
            }
            value = value.Replace("&quot;", "\"").Replace("&apos;", "'").Replace("&lt;", "<").Replace("&gt;", ">").Replace("&amp;", "&").Trim();
            return (value.Length == 0 || string.Equals(value, "na", StringComparison.OrdinalIgnoreCase)) ? null : value;
        }
        private static void AddUnique(Source source, List<Track> tracks)
        {
            foreach (Track track in tracks)
            {
                bool present = false;
                foreach (Track existing in source.Tracks)
                {
                    present |= string.Equals(existing.File, track.File, StringComparison.OrdinalIgnoreCase);
                }
                if (!present)
                {
                    source.Tracks.Add(track);
                }
            }
        }

        // "OutOfCareerPlaylist" -> "Out of career playlist" without the word: "Out of career".
        private static string PlaylistName(string name)
        {
            if (name.EndsWith("Playlist", StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring(0, name.Length - "Playlist".Length);
            }
            string spaced = Regex.Replace(Spaced(name), "(?<=[a-z])(?=\\d)", " ");
            return (spaced.Length > 1) ? spaced.Substring(0, 1) + spaced.Substring(1).ToLowerInvariant() : spaced;
        }

        // "2.xma" before "10.xma".
        private static int NaturalCompare(string a, string b)
        {
            string x = Path.GetFileNameWithoutExtension(a);
            string y = Path.GetFileNameWithoutExtension(b);
            int i;
            int k;
            if (int.TryParse(x, out i) && int.TryParse(y, out k))
            {
                return i.CompareTo(k);
            }
            return string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region FH2 radio

        // FH1's stations have no stinger idents to take a name from: the game names them only on the
        // station stickers (gamedb Livery_Decals 519-521), in Radio1-3 order.
        private static readonly string[] Fh1Stations = { "Horizon Bass Arena", "Horizon Pulse", "Horizon Rocks" };

        // The song of a bank stream without its list prefix, the same for a station track and the menu
        // edit of it: R1_Encore, PreRace_R1_Encore -> Encore (FH1); HZ2_R1_Chromeo_Jealous,
        // HZ2_PreRace_Chromeo_Jealous -> Chromeo_Jealous (FH2).
        private static string SongKey(string fmod)
        {
            return Regex.Replace(fmod, "^(?:HZ\\d+_)?(?:[A-Za-z]+_)?(?:R\\d+_)?", "");
        }

        private static void LoadRadio(string radio, List<Source> sources)
        {
            string bank = Path.Combine(radio, "Radio_Music.fsb");
            // Stream number of each FMOD name: the bank's streams are the .lst lines in order.
            Dictionary<string, int> stream = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            string list = Path.Combine(radio, "Radio_Music.lst");
            if (File.Exists(list))
            {
                int index = 0;
                foreach (string line in File.ReadAllLines(list))
                {
                    string path = line.Split(',')[0].Trim();
                    if (path.Length == 0)
                    {
                        continue;
                    }
                    index++;
                    stream[Path.GetFileNameWithoutExtension(path.Replace('/', '\\'))] = index;
                }
            }
            string system = File.ReadAllText(Path.Combine(radio, "RadioSystem.xml"), Encoding.UTF8);
            string[] blocks = Regex.Split(system, "<RadioStation ");

            // Title and artist of each song the stations name; the menu lists carry "None" and get them
            // from here (HZ2_PreRace_Chromeo_Jealous plays HZ2_R1_Chromeo_Jealous's song).
            Dictionary<string, Match> songs = new Dictionary<string, Match>(StringComparer.OrdinalIgnoreCase);
            foreach (Match track in Regex.Matches(system, "<MusicTrack ([^>]*)>"))
            {
                string fmod = Attribute(track.Groups[1].Value, "fmodname");
                if (fmod != null && !IsUntitled(track.Groups[1].Value) && !songs.ContainsKey(SongKey(fmod)))
                {
                    songs[SongKey(fmod)] = track;
                }
            }

            Source all = new Source();
            all.Name = "All stations";
            List<Source> stations = new List<Source>();
            List<Source> others = new List<Source>();
            foreach (string block in blocks)
            {
                Match name = Regex.Match(block, "^name=\"([^\"]*)\"");
                if (!name.Success)
                {
                    continue;
                }
                Source source = new Source();
                Match ident = Regex.Match(block, "Stinger_([A-Za-z0-9]+)_");
                Match numbered = Regex.Match(name.Groups[1].Value, "^Radio(\\d+)$");
                int third = name.Groups[1].Value.IndexOf("_3d_", StringComparison.OrdinalIgnoreCase);
                bool station = third < 0 && (ident.Success || numbered.Success);
                if (third >= 0)
                {
                    source.Name = "Menu: " + Spaced(name.Groups[1].Value.Substring(third + 4));   // Radio5_3d_PreRace -> Menu: Pre Race
                }
                else if (ident.Success)
                {
                    source.Name = Spaced(ident.Groups[1].Value);            // HorizonPulse -> Horizon Pulse
                }
                else if (numbered.Success)
                {
                    int number = int.Parse(numbered.Groups[1].Value);       // FH1 Radio1 -> Horizon Bass Arena
                    source.Name = (number >= 1 && number <= Fh1Stations.Length) ? Fh1Stations[number - 1] : "Radio " + number;
                }
                else
                {
                    continue;                                               // Radio4_Silent
                }
                foreach (Match track in Regex.Matches(block, "<MusicTrack ([^>]*)>"))
                {
                    string fmod = Attribute(track.Groups[1].Value, "fmodname");
                    int number;
                    if (fmod == null || !stream.TryGetValue(fmod, out number) || fmod.IndexOf("Blank", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        continue;
                    }
                    Track item = new Track();
                    item.File = bank;
                    item.Subsong = number;
                    Match song;
                    if (!IsUntitled(track.Groups[1].Value))
                    {
                        item.Title = Text(Attribute(track.Groups[1].Value, "name"));
                        item.Artist = Text(Attribute(track.Groups[1].Value, "artist"));
                    }
                    else if (songs.TryGetValue(SongKey(fmod), out song))
                    {
                        item.Title = Text(Attribute(song.Groups[1].Value, "name"));
                        item.Artist = Text(Attribute(song.Groups[1].Value, "artist"));
                    }
                    else
                    {
                        // HZ2_PreRace_Chromeo_Jealous -> Chromeo – Jealous
                        string[] parts = SongKey(fmod).Split('_');
                        item.Artist = (parts.Length >= 2) ? Spaced(parts[parts.Length - 2]) : null;
                        item.Title = Spaced(parts[parts.Length - 1]);
                    }
                    source.Tracks.Add(item);
                    if (station)
                    {
                        bool present = false;
                        foreach (Track existing in all.Tracks)
                        {
                            present |= existing.Subsong == item.Subsong;
                        }
                        if (!present)
                        {
                            all.Tracks.Add(item);
                        }
                    }
                }
                if (source.Tracks.Count > 0)
                {
                    (station ? stations : others).Add(source);
                }
            }
            sources.AddRange(stations);
            if (all.Tracks.Count > 0)
            {
                sources.Add(all);
            }
            sources.AddRange(others);
        }

        // A menu list's <MusicTrack name="None01" artist="None" ...>.
        private static bool IsUntitled(string attributes)
        {
            string title = Attribute(attributes, "name");
            return string.IsNullOrEmpty(title) || title.StartsWith("None", StringComparison.OrdinalIgnoreCase) || Attribute(attributes, "artist") == "None";
        }

        private static string Attribute(string attributes, string name)
        {
            Match match = Regex.Match(attributes, "\\b" + name + "=\"([^\"]*)\"", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : null;
        }

        #endregion

        // "UI_BallisticCircuit" -> "Ballistic Circuit"; "AlexMetric-ItStarts" -> "Alex Metric", "It Starts".
        private static void SplitName(string name, out string artist, out string title)
        {
            artist = null;
            if (name.StartsWith("UI_", StringComparison.OrdinalIgnoreCase))
            {
                title = Spaced(name.Substring(3));
                return;
            }
            int dash = name.IndexOf('-');
            if (dash > 0)
            {
                artist = Spaced(name.Substring(0, dash));
                title = Spaced(name.Substring(dash + 1));
                return;
            }
            title = Spaced(name);
        }

        public static string Spaced(string name)
        {
            StringBuilder text = new StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                // "HolyGhost" -> "Holy Ghost", "IMightSurvive" -> "I Might Survive", "R1" stays.
                bool wordStart = i > 0 && char.IsUpper(name[i]) && (char.IsLower(name[i - 1]) || char.IsDigit(name[i - 1]) || (char.IsUpper(name[i - 1]) && i + 1 < name.Length && char.IsLower(name[i + 1])));
                if (i > 0 && wordStart && name[i - 1] != '_' && name[i - 1] != ' ')
                {
                    text.Append(' ');
                }
                text.Append(name[i] == '_' ? ' ' : name[i]);
            }
            return text.ToString();
        }
    }
}
