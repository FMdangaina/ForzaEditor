namespace ForzaStudio
{
    using System;
    using System.Drawing;
    using System.IO;
    using System.Windows.Forms;

    // ♪ > Add song...: MP3/WAV/OGG/FLAC -> XMA2 + playlist line in the open game folder (see MusicInstaller).
    public partial class ArchiveEditorForm
    {
        private void AddSong()
        {
            string root = this.musicRoot;
            if (root == null || !Directory.Exists(Path.Combine(Path.Combine(Path.Combine(root, "Media"), "audio"), "Music")) || this.musicGame != ForzaGame.Motorsport4)
            {
                MessageBox.Show(this, "Open a Forza Motorsport 4 game folder first: songs are added to its Media\\audio\\Music.", "Add song", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!File.Exists(MusicDecoder))
            {
                MessageBox.Show(this, "vgmstream-cli.exe is missing next to the program (vgmstream folder).", "Add song", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            // Next to the program (xma2encode\xma2encode.exe, like vgmstream), else the saved path, else the SDK's.
            string encoder = Path.Combine(Path.Combine(Application.StartupPath, "xma2encode"), "xma2encode.exe");
            if (!File.Exists(encoder))
            {
                encoder = AppSettings.Get("Xma2Encode", MusicInstaller.DefaultEncoder);
            }
            if (!File.Exists(encoder))
            {
                using (OpenFileDialog find = new OpenFileDialog())
                {
                    find.Title = "xma2encode.exe (Xbox 360 SDK, bin\\win32)";
                    find.Filter = "xma2encode.exe|xma2encode.exe";
                    if (find.ShowDialog(this) != DialogResult.OK)
                    {
                        return;
                    }
                    encoder = find.FileName;
                    AppSettings.Set("Xma2Encode", encoder);
                }
            }

            using (Form form = new Form())
            {
                form.Text = "Add song";
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.StartPosition = FormStartPosition.CenterParent;
                form.ClientSize = new Size(460, 236);
                form.BackColor = ForzaTheme.Background;
                form.ForeColor = ForzaTheme.Text;

                TextBox file = DialogRow(form, "Song file:", 0, 270);
                Button browse = new Button();
                browse.Text = "Browse...";
                browse.SetBounds(380, 11, 70, 24);
                browse.Click += delegate
                {
                    using (OpenFileDialog open = new OpenFileDialog())
                    {
                        open.Filter = "Audio|*.mp3;*.wav;*.ogg;*.flac;*.m4a;*.wma|All files|*.*";
                        if (open.ShowDialog(form) == DialogResult.OK)
                        {
                            file.Text = open.FileName;
                        }
                    }
                };
                form.Controls.Add(browse);
                TextBox artist = DialogRow(form, "Artist:", 1, 300);
                TextBox title = DialogRow(form, "Title:", 2, 300);
                file.TextChanged += delegate
                {
                    if (title.Text.Length == 0 && file.Text.Length > 0)
                    {
                        title.Text = Path.GetFileNameWithoutExtension(file.Text);
                    }
                };
                TextBox name = DialogRow(form, "File name:", 3, 300);
                title.TextChanged += delegate { name.Text = MusicInstaller.SafeName((artist.Text + "-" + title.Text)); };
                artist.TextChanged += delegate { name.Text = MusicInstaller.SafeName((artist.Text + "-" + title.Text)); };

                Label listLabel = DialogLabel("Playlist:", 4);
                form.Controls.Add(listLabel);
                ComboBox list = new ComboBox();
                list.DropDownStyle = ComboBoxStyle.DropDownList;
                list.Items.AddRange(MusicInstaller.PlaylistNames);
                list.Items.Add("(none: only the .xma)");
                list.SelectedIndex = 0;
                list.SetBounds(100, 12 + 4 * 32, 220, 24);
                form.Controls.Add(list);

                Label qualityLabel = DialogLabel("Quality (1-100):", 5);
                form.Controls.Add(qualityLabel);
                NumericUpDown quality = new NumericUpDown();
                quality.Minimum = 1;
                quality.Maximum = 100;
                quality.Value = 70;
                quality.SetBounds(100, 12 + 5 * 32, 60, 24);
                form.Controls.Add(quality);

                CheckBox startFirst = new CheckBox();
                startFirst.Text = "Menu music starts with this song";
                startFirst.SetBounds(180, 12 + 5 * 32, 270, 24);
                startFirst.Enabled = false;
                form.Controls.Add(startFirst);
                list.SelectedIndexChanged += delegate
                {
                    startFirst.Enabled = list.SelectedIndex == 2;      // "Menu (default)": the playlist that has a start="true" song
                    if (!startFirst.Enabled)
                    {
                        startFirst.Checked = false;
                    }
                };
                Button ok = new Button();
                ok.Text = "Add";
                ok.SetBounds(294, 200, 75, 26);
                form.Controls.Add(ok);
                Button cancel = new Button();
                cancel.Text = "Cancel";
                cancel.DialogResult = DialogResult.Cancel;
                cancel.SetBounds(375, 200, 75, 26);
                form.Controls.Add(cancel);
                form.CancelButton = cancel;
                form.AcceptButton = ok;
                ok.Click += delegate
                {
                    string input = file.Text.Trim('"', ' ');
                    string safe = MusicInstaller.SafeName(name.Text);
                    if (!File.Exists(input) || safe.Length == 0)
                    {
                        MessageBox.Show(form, "Pick an existing song file and a file name.", "Add song", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    int playlist = (list.SelectedIndex < MusicInstaller.PlaylistFiles.Length) ? list.SelectedIndex : -1;
                    Cursor previous = this.Cursor;
                    form.Cursor = Cursors.WaitCursor;
                    ok.Enabled = false;
                    try
                    {
                        string xma = MusicInstaller.Install(input, MusicDecoder, encoder, this.musicRoot, safe, (int) quality.Value, playlist,
                            title.Text, artist.Text, startFirst.Checked, delegate(string step) { this.SetStatus(step); Application.DoEvents(); });
                        this.SetStatus("Song added: " + xma);
                        form.DialogResult = DialogResult.OK;
                    }
                    catch (Exception exception)
                    {
                        MessageBox.Show(form, exception.Message, "Add song", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    finally
                    {
                        form.Cursor = previous;
                        ok.Enabled = true;
                    }
                };

                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    // Reload the lists so the new song shows up (and plays) like the others.
                    this.StopMusic();
                    this.musicRoot = null;
                    this.StartMusicFor(root);
                }
            }
        }

        private static Label DialogLabel(string text, int row)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = false;
            label.SetBounds(10, 14 + row * 32, 88, 20);
            label.ForeColor = ForzaTheme.Text;
            return label;
        }

        private static TextBox DialogRow(Form form, string text, int row, int width)
        {
            form.Controls.Add(DialogLabel(text, row));
            TextBox box = new TextBox();
            box.SetBounds(100, 12 + row * 32, width, 24);
            form.Controls.Add(box);
            return box;
        }
    }
}
