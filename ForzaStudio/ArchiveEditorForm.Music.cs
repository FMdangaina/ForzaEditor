namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.IO;
    using System.Windows.Forms;

    // The game's music in the status bar. Opening a game folder starts its music (see GameMusic):
    // FM4 the menu music, FM2 its playlists, FH2 the radio stations. The ♪ menu picks the list.
    public partial class ArchiveEditorForm
    {
        private XmaPlayer musicPlayer;
        private string musicRoot;
        private ForzaGame musicGame;
        private List<GameMusic.Source> musicSources = new List<GameMusic.Source>();
        private GameMusic.Source musicSource;
        private List<GameMusic.Track> musicTracks = new List<GameMusic.Track>();
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
            this.BuildMusicMenu();

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

        // The ♪ menu: one item per music list of the game (stations on FH2), then the options.
        private void BuildMusicMenu()
        {
            this.btnMusicOptions.DropDownItems.Clear();
            foreach (GameMusic.Source source in this.musicSources)
            {
                GameMusic.Source pick = source;
                ToolStripMenuItem item = new ToolStripMenuItem(string.Format("{0} ({1})", source.Name, source.Tracks.Count), null, delegate { this.SetMusicSource(pick, true); });
                item.Checked = source == this.musicSource;
                this.btnMusicOptions.DropDownItems.Add(item);
            }
            if (this.musicSources.Count > 0)
            {
                this.btnMusicOptions.DropDownItems.Add(new ToolStripSeparator());
            }
            this.btnMusicOptions.DropDownItems.Add(this.mnuMusicShuffle);
            this.btnMusicOptions.DropDownItems.Add(this.mnuMusicAutoPlay);
            this.btnMusicOptions.DropDownItems.Add(new ToolStripSeparator());
            this.btnMusicOptions.DropDownItems.Add(new ToolStripMenuItem("Stop", null, delegate { this.StopMusic(); }));
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

        // A folder was opened: if it belongs to a game with music, start it.
        private void StartMusicFor(string folder)
        {
            if (!File.Exists(MusicDecoder))
            {
                return;
            }
            string root = GamePaths.FindRoot(folder);
            string key = root ?? folder;
            if (string.Equals(key, this.musicRoot, StringComparison.OrdinalIgnoreCase) && this.musicPlayer != null && this.musicPlayer.Playing)
            {
                return;
            }
            List<GameMusic.Source> sources = GameMusic.Load(root, folder);
            if (sources.Count == 0)
            {
                return;
            }
            this.musicRoot = key;
            this.musicGame = GameInfo.Detect(root);
            this.musicSources = sources;

            // The list last picked for this game, else the first (menu music, first station...).
            string remembered = AppSettings.Get("MusicSource." + GameInfo.Tag(this.musicGame), null);
            if (remembered == null && this.musicGame == ForzaGame.Motorsport4 && AppSettings.Get("MusicMode", "menu") == "all")
            {
                remembered = "Whole soundtrack";
            }
            GameMusic.Source chosen = sources[0];
            foreach (GameMusic.Source source in sources)
            {
                if (source.Name == remembered)
                {
                    chosen = source;
                }
            }
            this.ShowMusicControls(true);
            this.SetMusicSource(chosen, this.mnuMusicAutoPlay.Checked);
        }

        private void SetMusicSource(GameMusic.Source source, bool play)
        {
            this.musicSource = source;
            this.musicTracks = source.Tracks;
            this.musicIndex = -1;
            AppSettings.Set("MusicSource." + GameInfo.Tag(this.musicGame), source.Name);
            this.BuildMusicMenu();
            if (play)
            {
                this.PlayMusic(this.mnuMusicShuffle.Checked ? this.musicRandom.Next(this.musicTracks.Count) : 0);
            }
            else
            {
                this.UpdateMusicLabel();
            }
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
            GameMusic.Track track = this.musicTracks[index];
            try
            {
                this.musicPlayer.Play(track.File, track.Subsong);
                this.musicIndex = index;
                this.musicTimer.Start();
            }
            catch (Exception exception)
            {
                this.musicIndex = -1;
                this.SetStatus("Cannot play " + track.Display + ": " + exception.Message);
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
            string game = GameInfo.Tag(this.musicGame);
            if (this.musicIndex < 0 || this.musicIndex >= this.musicTracks.Count)
            {
                this.lblMusic.Text = "♪ " + game + ((this.musicSource != null) ? " " + this.musicSource.Name : " music");
                this.lblMusicTime.Text = "";
                return;
            }
            GameMusic.Track track = this.musicTracks[this.musicIndex];
            this.lblMusic.Text = "♪ " + track.Display;
            this.lblMusic.ToolTipText = string.Format("{0} {1}: {2} of {3}\n{4}{5}", game, this.musicSource.Name, this.musicIndex + 1, this.musicTracks.Count,
                Path.GetFileName(track.File), (track.Subsong > 0) ? " #" + track.Subsong : "");
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
    }
}
