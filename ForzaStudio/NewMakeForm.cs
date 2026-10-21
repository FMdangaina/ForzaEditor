namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Globalization;
    using System.IO;
    using System.Text.RegularExpressions;
    using System.Windows.Forms;

    // New car make (CarMake): its name, code, country, profile text and logo image. Two ways out:
    //   Create in the game   the make goes into the FM4 folder now (local database and files);
    //   Add to the pack      the make is kept by the port window and written into the trial folder of
    //                        every pack trial and single-car trial, so the DLC pack carries it.
    // Spec holds the result; InGame tells which button was used.
    internal sealed class NewMakeForm : Form
    {
        private sealed class Country
        {
            public long Id;
            public string Name;

            public override string ToString()
            {
                return this.Name;
            }
        }

        private readonly string target;
        private readonly long firstId;
        private readonly TextBox name;
        private readonly TextBox code;
        private readonly TextBox logoName;
        private readonly ComboBox country;
        private readonly TextBox id;
        private readonly TextBox profile;
        private readonly TextBox logoFile;
        private readonly PictureBox preview;
        private readonly Label problem;
        private bool logoNameEdited;

        public CarMakeSpec Spec;
        public bool InGame;

        // existingIds: makes already pending in the port window; xeniaContent: for the Id of a DLC make.
        public NewMakeForm(string target, string xeniaContent, List<long> pendingIds, bool canCreateInGame, bool canAddToPack)
        {
            this.target = target;
            this.AutoScaleMode = AutoScaleMode.None;
            this.Text = "New Car Make";
            this.ClientSize = new Size(560, 470);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.CenterParent;

            long free;
            List<Country> countries = new List<Country>();
            using (SqliteDatabase db = SqliteDatabase.FromBytes(File.ReadAllBytes(CarMake.DatabasePath(target))))
            {
                free = CarMake.FreeId(db, xeniaContent);
                StringTable names = null;
                try
                {
                    names = StringTable.Load(target, "EN");
                }
                catch (Exception)
                {
                }
                foreach (object[] row in db.Query("SELECT CountryID, Country FROM List_Country ORDER BY CountryID").Rows)
                {
                    string text = (names != null) ? names.Lookup("List_Country", "Country", row[1] as string) : null;
                    Country item = new Country();
                    item.Id = Convert.ToInt64(row[0], CultureInfo.InvariantCulture);
                    item.Name = string.IsNullOrEmpty(text) ? "country " + item.Id : text;
                    countries.Add(item);
                }
            }
            foreach (long taken in pendingIds)
            {
                free = Math.Max(free, taken + 1);
            }
            this.firstId = free;
            countries.Sort(delegate(Country a, Country b) { return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase); });

            int y = 14;
            this.name = this.AddRow("Name:", y, 360, "The make's name as the game shows it (every language gets it; change a language later in the string tables)");
            this.name.TextChanged += delegate { this.NameChanged(); };
            y += 32;
            this.code = this.AddRow("Code:", y, 70, "ManufacturerCode, 2-4 capital letters or digits (the game's: ACU, AST, POR)");
            this.code.CharacterCasing = CharacterCasing.Upper;
            this.code.MaxLength = 4;
            this.code.TextChanged += delegate { this.Recheck(); };
            Label idLabel = new Label();
            idLabel.Text = "Id:";
            idLabel.SetBounds(240, y + 3, 24, 20);
            this.id = new TextBox();
            this.id.SetBounds(268, y, 70, 24);
            this.id.Text = this.firstId.ToString(CultureInfo.InvariantCulture);
            this.id.TextChanged += delegate { this.Recheck(); };
            new ToolTip().SetToolTip(this.id, "List_CarMake.ID; the next free one, also past the makes of the DLC packs in Xenia's content folder");
            this.Controls.Add(idLabel);
            this.Controls.Add(this.id);
            y += 32;
            this.logoName = this.AddRow("Logo name:", y, 160, "IconPathBase: the logo files are L_MAN_<name>_Logo in the six logo folders of UI\\Textures.zip");
            this.logoName.TextChanged += delegate { this.logoNameEdited = this.logoName.Focused || this.logoNameEdited; this.Recheck(); };
            y += 32;
            Label countryLabel = new Label();
            countryLabel.Text = "Country:";
            countryLabel.SetBounds(12, y + 3, 80, 20);
            this.country = new ComboBox();
            this.country.DropDownStyle = ComboBoxStyle.DropDownList;
            this.country.SetBounds(96, y, 200, 24);
            this.country.MaxDropDownItems = 20;
            this.country.Items.AddRange(countries.ToArray());
            if (this.country.Items.Count > 0)
            {
                this.country.SelectedIndex = 0;
            }
            this.Controls.Add(countryLabel);
            this.Controls.Add(this.country);
            y += 32;
            Label profileLabel = new Label();
            profileLabel.Text = "Profile:";
            profileLabel.SetBounds(12, y + 3, 80, 20);
            this.profile = new TextBox();
            this.profile.Multiline = true;
            this.profile.ScrollBars = ScrollBars.Vertical;
            this.profile.SetBounds(96, y, 452, 110);
            new ToolTip().SetToolTip(this.profile, "The text of the make's page (Profile). May stay empty.");
            this.Controls.Add(profileLabel);
            this.Controls.Add(this.profile);
            y += 120;

            Label logoLabel = new Label();
            logoLabel.Text = "Logo:";
            logoLabel.SetBounds(12, y + 3, 80, 20);
            this.logoFile = new TextBox();
            this.logoFile.ReadOnly = true;
            this.logoFile.SetBounds(96, y, 360, 24);
            Button browse = new Button();
            browse.Text = "Image...";
            browse.SetBounds(464, y - 1, 84, 26);
            browse.Click += delegate { this.PickLogo(); };
            this.Controls.Add(logoLabel);
            this.Controls.Add(this.logoFile);
            this.Controls.Add(browse);
            y += 32;
            this.preview = new PictureBox();
            this.preview.SetBounds(96, y, 200, 90);
            this.preview.SizeMode = PictureBoxSizeMode.Zoom;
            this.preview.BackColor = Color.FromArgb(60, 60, 64);
            this.Controls.Add(this.preview);
            Label hint = new Label();
            hint.Text = "A PNG with transparency is best. It is fitted into each logo folder's size, taken from Acura's logos.";
            hint.SetBounds(304, y, 244, 90);
            hint.ForeColor = ForzaTheme.TextDim;
            this.Controls.Add(hint);
            y += 100;

            this.problem = new Label();
            this.problem.SetBounds(12, y, 536, 20);
            this.problem.ForeColor = ForzaTheme.Yellow;
            this.Controls.Add(this.problem);
            y += 26;

            Button game = new Button();
            game.Text = "Create in the game";
            game.SetBounds(12, y, 150, 30);
            game.Visible = canCreateInGame;
            game.Click += delegate { this.Accept(true); };
            new ToolTip().SetToolTip(game, "Writes the make into the FM4 folder's database, string tables and Textures.zip now (files backed up to bak\\)");
            Button pack = new Button();
            pack.Text = "Add to the DLC pack";
            pack.SetBounds(canCreateInGame ? 170 : 12, y, 150, 30);
            pack.Visible = canAddToPack;
            pack.Click += delegate { this.Accept(false); };
            new ToolTip().SetToolTip(pack, "Kept in the port window and written into every trial and the DLC pack, so the pack carries the make; the game folder stays untouched");
            Button close = new Button();
            close.Text = "Cancel";
            close.SetBounds(468, y, 80, 30);
            close.DialogResult = DialogResult.Cancel;
            this.Controls.Add(game);
            this.Controls.Add(pack);
            this.Controls.Add(close);
            this.CancelButton = close;
            ForzaTheme.Apply(this);
            this.Recheck();
        }

        private TextBox AddRow(string caption, int y, int width, string tip)
        {
            Label label = new Label();
            label.Text = caption;
            label.SetBounds(12, y + 3, 80, 20);
            TextBox box = new TextBox();
            box.SetBounds(96, y, width, 24);
            new ToolTip().SetToolTip(box, tip);
            this.Controls.Add(label);
            this.Controls.Add(box);
            return box;
        }

        // The logo name follows the name (letters and digits) until the user edits it.
        private void NameChanged()
        {
            if (!this.logoNameEdited)
            {
                this.logoName.Text = Regex.Replace(this.name.Text, "[^A-Za-z0-9]", string.Empty);
                this.logoNameEdited = false;
            }
            this.Recheck();
        }

        private void PickLogo()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Logo image";
                dialog.Filter = "Images (*.png;*.jpg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files|*.*";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                try
                {
                    using (Image image = Image.FromFile(dialog.FileName))
                    {
                        this.preview.Image = new Bitmap(image);
                    }
                    this.logoFile.Text = dialog.FileName;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Not an image: " + ex.Message, this.Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            this.Recheck();
        }

        private CarMakeSpec Build()
        {
            CarMakeSpec spec = new CarMakeSpec();
            long value;
            spec.Id = long.TryParse(this.id.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out value) ? value : 0;
            spec.Code = this.code.Text.Trim().ToUpperInvariant();
            spec.Base = this.logoName.Text.Trim();
            spec.Name = this.name.Text.Trim();
            spec.Profile = this.profile.Text.Trim();
            spec.Logo = this.logoFile.Text;
            Country selected = this.country.SelectedItem as Country;
            spec.CountryId = (selected != null) ? selected.Id : 0;
            return spec;
        }

        private bool Recheck()
        {
            if (this.problem == null)
            {
                return false;
            }
            CarMakeSpec spec = this.Build();
            string text = null;
            if (spec.Id <= 0)
            {
                text = "The Id is a number.";
            }
            else if (this.name.Text.Trim().Length > 0 || this.code.Text.Length > 0 || this.logoFile.Text.Length > 0)
            {
                try
                {
                    text = CarMake.Check(spec, this.target, null);
                }
                catch (Exception ex)
                {
                    text = ex.Message;
                }
            }
            else
            {
                text = "Fill in the make.";
            }
            this.problem.Text = text ?? string.Empty;
            return text == null;
        }

        private void Accept(bool inGame)
        {
            if (!this.Recheck())
            {
                return;
            }
            this.Spec = this.Build();
            this.InGame = inGame;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
