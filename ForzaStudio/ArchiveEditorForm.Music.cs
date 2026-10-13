namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.IO;
    using System.Text;
    using System.Windows.Forms;
    using System.Xml;

    // The game's music in the status bar. Opening a game folder starts its menu music (the UI_
    // tracks, in the order of the game's own UI playlists); the whole soundtrack is one click away.
    public partial class ArchiveEditorForm
    {
        private XmaPlayer musicPlayer;
        private string musicFolder;
        private List<string> musicTracks = new List<string>();
        private int musicIndex = -1;
        private readonly Random musicRandom = new Random();
        private Timer musicTimer;

        private ToolStripSeparator sepMusic;
        private ToolStripLabel lblMusic;
        private ToolStripButton btnMusicPrevious;
        private ToolStripButton btnMusicPlay;
        private ToolStripButton btnMusicNext;
        private ToolStripLabel lblMusicTime;
        private ToolStripControlHost hostMusicVolume;
        private ToolStripDropDownButton btnMusicOptions;
        private ToolStripMenuItem mnuMusicMenu;
        private ToolStripMenuItem mnuMusicAll;
        private ToolStripMenuItem mnuMusicShuffle;
        private ToolStripMenuItem mnuMusicAutoPlay;

        private static string MusicDecoder
        {
            get { return Path.Combine(Path.Combine(Application.StartupPath, "vgmstream"), "vgmstream-cli.exe"); }
        }

        private void BuildMusicControls()
        {
            this.sepMusic = new ToolStripSeparator();
            this.lblMusic = new ToolStripLabel();
            this.lblMusic.ForeColor = ForzaTheme.Red;
            this.lblMusic.Font = ForzaTheme.Bold;
            this.btnMusicPrevious = MusicButton("⏮", "Previous track", delegate { this.PlayMusic(this.musicIndex <= 0 ? this.musicTracks.Count - 1 : this.musicIndex - 1); });
            this.btnMusicPlay = MusicButton("⏸", "Pause / play", delegate { this.ToggleMusic(); });
            this.btnMusicNext = MusicButton("⏭", "Next track", delegate { this.NextMusic(); });
            this.lblMusicTime = new ToolStripLabel();
            this.lblMusicTime.ForeColor = ForzaTheme.TextDim;

            TrackBar volume = new TrackBar();
            volume.Minimum = 0;
            volume.Maximum = 100;
            volume.TickStyle = TickStyle.None;
            volume.AutoSize = false;
            volume.Size = new Size(90, 22);
            volume.BackColor = ForzaTheme.Background;
            volume.Value = MusicSetting("MusicVolume", 60);
            volume.ValueChanged += delegate
            {
                if (this.musicPlayer != null)
                {
                    this.musicPlayer.Volume = volume.Value;
                }
                AppSettings.Set("MusicVolume", volume.Value.ToString());
            };
            // The bar keeps the focus after a click, and the wheel meant for the 3D view (zoom) would
            // then turn the music down to silence: volume changes by dragging or clicking only.
            volume.MouseWheel += delegate(object sender, MouseEventArgs e)
            {
                HandledMouseEventArgs handled = e as HandledMouseEventArgs;
                if (handled != null)
                {
                    handled.Handled = true;
                }
            };
            this.hostMusicVolume = new ToolStripControlHost(volume);
            this.hostMusicVolume.ToolTipText = "Volume";

            this.btnMusicOptions = new ToolStripDropDownButton("♪");
            this.btnMusicOptions.ToolTipText = "Music";
            this.mnuMusicMenu = new ToolStripMenuItem("Menu music (UI_ tracks)", null, delegate { this.SetMusicMode(false); });
            this.mnuMusicAll = new ToolStripMenuItem("Whole soundtrack", null, delegate { this.SetMusicMode(true); });
            this.mnuMusicShuffle = new ToolStripMenuItem("Shuffle", null, delegate
            {
                this.mnuMusicShuffle.Checked = !this.mnuMusicShuffle.Checked;
                AppSettings.Set("MusicShuffle", this.mnuMusicShuffle.Checked ? "1" : "0");
            });
            this.mnuMusicShuffle.Checked = MusicSetting("MusicShuffle", 0) != 0;
            this.mnuMusicAutoPlay = new ToolStripMenuItem("Play when a game folder opens", null, delegate
            {
                this.mnuMusicAutoPlay.Checked = !this.mnuMusicAutoPlay.Checked;
                AppSettings.Set("MusicAutoPlay", this.mnuMusicAutoPlay.Checked ? "1" : "0");
            });
            this.mnuMusicAutoPlay.Checked = MusicSetting("MusicAutoPlay", 1) != 0;
            bool all = AppSettings.Get("MusicMode", "menu") == "all";
            this.mnuMusicMenu.Checked = !all;
            this.mnuMusicAll.Checked = all;
            this.btnMusicOptions.DropDownItems.Add(this.mnuMusicMenu);
            this.btnMusicOptions.DropDownItems.Add(this.mnuMusicAll);
            this.btnMusicOptions.DropDownItems.Add(new ToolStripSeparator());
            this.btnMusicOptions.DropDownItems.Add(this.mnuMusicShuffle);
            this.btnMusicOptions.DropDownItems.Add(this.mnuMusicAutoPlay);
            this.btnMusicOptions.DropDownItems.Add(new ToolStripSeparator());
            this.btnMusicOptions.DropDownItems.Add(new ToolStripMenuItem("Stop", null, delegate { this.StopMusic(); }));

            this.status.Items.AddRange(new ToolStripItem[] { this.sepMusic, this.lblMusic, this.btnMusicPrevious, this.btnMusicPlay, this.btnMusicNext, this.lblMusicTime, this.hostMusicVolume, this.btnMusicOptions });
            this.ShowMusicControls(false);

            this.musicTimer = new Timer();
            this.musicTimer.Interval = 500;
            this.musicTimer.Tick += delegate { this.UpdateMusicTime(); };
            this.FormClosed += delegate
            {
                this.musicTimer.Stop();
                if (this.musicPlayer != null)
                {
                    this.musicPlayer.Dispose();
                }
            };
        }

        private static ToolStripButton MusicButton(string text, string tip, EventHandler click)
        {
            ToolStripButton button = new ToolStripButton(text, null, click);
            button.ToolTipText = tip;
            button.Font = new Font("Segoe UI Symbol", 9f);
            return button;
        }

        private static int MusicSetting(string key, int fallback)
        {
            int value;
            return int.TryParse(AppSettings.Get(key, fallback.ToString()), out value) ? value : fallback;
        }

        private void ShowMusicControls(bool visible)
        {
            foreach (ToolStripItem item in new ToolStripItem[] { this.sepMusic, this.lblMusic, this.btnMusicPrevious, this.btnMusicPlay, this.btnMusicNext, this.lblMusicTime, this.hostMusicVolume, this.btnMusicOptions })
            {
                item.Visible = visible;
            }
        }

        // A folder was opened: if it belongs to a game with music, start the menu music.
        private void StartMusicFor(string folder)
        {
            string root = GamePaths.FindRoot(folder);
            string music = (root == null) ? null : Path.Combine(Path.Combine(Path.Combine(root, "Media"), "audio"), "Music");
            if (music == null || !Directory.Exists(music))
            {
                music = folder;
            }
            if (!File.Exists(MusicDecoder) || !Directory.Exists(music) || Directory.GetFiles(music, "*.xma").Length == 0)
            {
                return;
            }
            if (string.Equals(music, this.musicFolder, StringComparison.OrdinalIgnoreCase) && this.musicPlayer != null && this.musicPlayer.Playing)
            {
                return;
            }
            this.musicFolder = music;
            this.LoadMusicTracks();
            this.ShowMusicControls(true);
            if (this.mnuMusicAutoPlay.Checked)
            {
                this.PlayMusic(this.mnuMusicShuffle.Checked ? this.musicRandom.Next(this.musicTracks.Count) : 0);
            }
            else
            {
                this.musicIndex = -1;
                this.UpdateMusicLabel();
            }
        }

        private void SetMusicMode(bool all)
        {
            this.mnuMusicMenu.Checked = !all;
            this.mnuMusicAll.Checked = all;
            AppSettings.Set("MusicMode", all ? "all" : "menu");
            if (this.musicFolder != null)
            {
                this.LoadMusicTracks();
                this.PlayMusic(0);
            }
        }

        // Menu music: the game's UI playlists first, in their order, then any other UI_ track.
        private void LoadMusicTracks()
        {
            List<string> tracks = new List<string>();
            if (this.mnuMusicAll.Checked)
            {
                tracks.AddRange(Directory.GetFiles(this.musicFolder, "*.xma"));
                tracks.Sort(StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                foreach (string playlist in new string[] { "InDefaultUIPlaylist.xml", "InUIPlaylist2.xml" })
                {
                    string path = Path.Combine(this.musicFolder, playlist);
                    if (!File.Exists(path))
                    {
                        continue;
                    }
                    try
                    {
                        XmlDocument document = new XmlDocument();
                        document.Load(path);
                        foreach (XmlElement song in document.GetElementsByTagName("Song"))
                        {
                            string file = Path.Combine(this.musicFolder, Path.GetFileName(song.GetAttribute("filename").Replace('/', '\\')));
                            if (File.Exists(file) && !tracks.Exists(delegate(string t) { return string.Equals(t, file, StringComparison.OrdinalIgnoreCase); }))
                            {
                                tracks.Add(file);
                            }
                        }
                    }
                    catch (XmlException)
                    {
                    }
                }
                List<string> others = new List<string>(Directory.GetFiles(this.musicFolder, "UI_*.xma"));
                others.Sort(StringComparer.OrdinalIgnoreCase);
                foreach (string file in others)
                {
                    if (!tracks.Exists(delegate(string t) { return string.Equals(t, file, StringComparison.OrdinalIgnoreCase); }))
                    {
                        tracks.Add(file);
                    }
                }
                if (tracks.Count == 0)
                {
                    tracks.AddRange(Directory.GetFiles(this.musicFolder, "*.xma"));
                }
            }
            this.musicTracks = tracks;
            this.musicIndex = -1;
        }

        private void PlayMusic(int index)
        {
            if (index < 0 || index >= this.musicTracks.Count)
            {
                return;
            }
            if (this.musicPlayer == null)
            {
                this.musicPlayer = new XmaPlayer(MusicDecoder);
                this.musicPlayer.Volume = ((TrackBar) this.hostMusicVolume.Control).Value;
                this.musicPlayer.Finished += delegate
                {
                    try
                    {
                        this.BeginInvoke(new MethodInvoker(this.NextMusic));
                    }
                    catch (InvalidOperationException)
                    {
                        // The window is closing.
                    }
                };
            }
            try
            {
                this.musicPlayer.Play(this.musicTracks[index]);
                this.musicIndex = index;
                this.musicTimer.Start();
            }
            catch (Exception exception)
            {
                this.musicIndex = -1;
                this.SetStatus("Cannot play " + Path.GetFileName(this.musicTracks[index]) + ": " + exception.Message);
            }
            this.UpdateMusicLabel();
        }

        private void NextMusic()
        {
            int count = this.musicTracks.Count;
            if (count == 0)
            {
                return;
            }
            int next = (this.musicIndex + 1) % count;
            if (this.mnuMusicShuffle.Checked && count > 1)
            {
                do
                {
                    next = this.musicRandom.Next(count);
                }
                while (next == this.musicIndex);
            }
            this.PlayMusic(next);
        }

        private void ToggleMusic()
        {
            if (this.musicPlayer != null && this.musicPlayer.Playing)
            {
                this.musicPlayer.TogglePause();
            }
            else
            {
                this.PlayMusic(Math.Max(0, this.musicIndex));
            }
            this.UpdateMusicLabel();
        }

        private void StopMusic()
        {
            if (this.musicPlayer != null)
            {
                this.musicPlayer.Stop();
            }
            this.musicTimer.Stop();
            this.UpdateMusicLabel();
        }

        private void UpdateMusicLabel()
        {
            bool playing = this.musicPlayer != null && this.musicPlayer.Playing;
            this.btnMusicPlay.Text = (playing && !this.musicPlayer.Paused) ? "⏸" : "▶";
            if (this.musicIndex < 0 || this.musicIndex >= this.musicTracks.Count)
            {
                this.lblMusic.Text = "♪ FM4 music";
                this.lblMusicTime.Text = "";
                return;
            }
            this.lblMusic.Text = "♪ " + TrackName(this.musicTracks[this.musicIndex]);
            this.lblMusic.ToolTipText = Path.GetFileName(this.musicTracks[this.musicIndex]) + string.Format("  ({0} of {1})", this.musicIndex + 1, this.musicTracks.Count);
            this.UpdateMusicTime();
        }

        private void UpdateMusicTime()
        {
            if (this.musicPlayer == null || !this.musicPlayer.Playing)
            {
                this.lblMusicTime.Text = "";
                return;
            }
            TimeSpan position = this.musicPlayer.Position;
            TimeSpan length = this.musicPlayer.Length;
            this.lblMusicTime.Text = string.Format("{0}:{1:00} / {2}:{3:00}", (int) position.TotalMinutes, position.Seconds, (int) length.TotalMinutes, length.Seconds);
        }

        // "UI_BallisticCircuit" -> "Ballistic Circuit"; "AlexMetric-ItStarts" -> "Alex Metric - It Starts".
        private static string TrackName(string file)
        {
            string name = Path.GetFileNameWithoutExtension(file);
            if (name.StartsWith("UI_", StringComparison.OrdinalIgnoreCase))
            {
                return Spaced(name.Substring(3));
            }
            int dash = name.IndexOf('-');
            return (dash > 0) ? Spaced(name.Substring(0, dash)) + " – " + Spaced(name.Substring(dash + 1)) : Spaced(name);
        }

        private static string Spaced(string name)
        {
            StringBuilder text = new StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]) && char.IsLower(name[i - 1]))
                {
                    text.Append(' ');
                }
                text.Append(name[i] == '_' ? ' ' : name[i]);
            }
            return text.ToString();
        }
    }
}
