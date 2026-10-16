namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Drawing;
    using System.Globalization;
    using System.IO;
    using System.Threading;
    using System.Windows.Forms;

    // Tools > Port Car From Another Forza: pick the FH, FH2 or FM3 folder (FH and FH2 write the same car
    // files and database layout; FM3 has its own conversion, see CarPort) and the FM4 one, a car and an FM4 donor, then a trial
    // run (files written to a temporary folder, the report shown) and the install (CarPort).
    // The car list says what each car would be in FM4: new, ported earlier (the install updates it),
    // already an FM4 car or with its id taken (both refused), or without files.
    //
    // DLC pack (right panel): the cars go into a DLC package instead of the game's own files
    // (DlcPackage). Add n cars of the source game with their donors, name the pack, run the pack trial
    // (every car ported in turn into one trial folder, each starting from the one before, so their ids
    // and string keys never clash; each car's review is shown when it is selected in the pack), then
    // build the package straight into Xenia's content folder. The pack is kept in the settings.
    internal class CarPortForm : Form
    {
        private readonly TextBox packName;
        private readonly Label packOfferLabel;
        private readonly ListView packList;
        private readonly TextBox xeniaBox;
        private readonly Label packStatus;
        private readonly Button addToPack;
        private readonly Button removeFromPack;
        private readonly Button newPack;
        private readonly Button trialPack;
        private readonly Button buildPack;
        private readonly Button removePack;
        private readonly Button buildXbox;
        private readonly List<PackEntry> pack = new List<PackEntry>();
        private int packOffer;              // 0 until the first build
        private string packTrialFolder;     // the last pack trial, null when the pack changed since
        private Dictionary<string, Dictionary<string, object>> packTrialFixes;   // the fixes that trial used, per car
        private List<Car> horizonCars = new List<Car>();

        private class PackEntry
        {
            public Car Car;
            public string Donor;
            public bool Arcade;
            public List<CarPort.Issue> Issues;
            public int Applied;
        }

        private readonly TextBox sourceBox;
        private readonly TextBox targetBox;
        private readonly TextBox search;
        private readonly CheckBox onlyNew;
        private readonly ListView cars;
        private readonly ComboBox donors;
        private readonly CheckBox arcade;
        private readonly ToolStripStatusLabel details;
        private readonly TextBox report;
        private readonly Panel reviewPanel;
        private readonly Label reviewTitle;
        private readonly DataGridView review;
        private readonly Dictionary<string, Dictionary<string, object>> fixes = new Dictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase);   // per car media name
        private string reviewCar;
        private bool showingReview;         // ShowReview filling the grid (not a fix chosen by hand)
        private const string LeaveAsIs = "(leave as is)";
        private readonly Button load;
        private readonly Button trial;
        private readonly Button install;
        private readonly Button openTrial;
        private readonly List<ListViewItem> items = new List<ListViewItem>();

        private List<Car> fm4Cars = new List<Car>();
        private string trialFolder;
        private string sourceTag = "FH";    // FH, FH2 or FM3, once the cars are loaded
        private bool busy;

        private enum Status
        {
            New,
            Ported,
            InFm4,
            IdTaken,
            NoFiles
        }

        private class Car
        {
            public long Id;
            public string MediaName;
            public string Name;
            public long Year;
            public long Make;
            public long Placement;
            public long Drive;
            public long Type;
            public double Weight;
            public double Pi;           // own game's PI
            public double Fm4Pi;        // FH/FH2 cars: the PI they would get in FM4
            public string Fm4Class;
            public Status State;
            public string Note;
        }

        private class DonorChoice
        {
            public Car Car;
            public bool Suggested;

            public override string ToString()
            {
                return (this.Suggested ? "★ " : "") + this.Car.Name + "  (" + this.Car.MediaName + ", " + this.Car.Year + ")";
            }
        }

        public CarPortForm()
        {
            this.Text = "Port Car From Another Forza";
            this.Size = new Size(1360, 900);
            this.MinimumSize = new Size(1100, 720);
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = ForzaTheme.Regular;
            try
            {
                this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch (ArgumentException)
            {
            }

            // The main window's look: the menu bar with the brand and the red rule, sections under
            // upper-case headers, splitters, the game-style tabs, a status bar.
            MenuStrip header = new MenuStrip();
            ToolStripLabel forza = new ToolStripLabel("FORZA");
            forza.Font = new Font("Segoe UI", 11f, FontStyle.Bold | FontStyle.Italic);
            forza.ForeColor = ForzaTheme.Red;
            forza.Margin = new Padding(6, 1, 0, 2);
            ToolStripLabel studio = new ToolStripLabel("STUDIO");
            studio.Font = new Font("Segoe UI Light", 11f, FontStyle.Italic);
            studio.Margin = new Padding(0, 1, 18, 2);
            ToolStripLabel title = new ToolStripLabel("PORT CAR FROM FORZA HORIZON");
            title.Font = ForzaTheme.Bold;
            title.ForeColor = ForzaTheme.TextDim;
            header.Items.Add(forza);
            header.Items.Add(studio);
            header.Items.Add(title);

            StatusStrip status = new StatusStrip();
            this.details = new ToolStripStatusLabel("Choose the two game folders and load the cars.");
            this.details.Spring = true;
            this.details.TextAlign = ContentAlignment.MiddleLeft;
            status.Items.Add(this.details);

            // Game folders.
            Panel folders = new Panel();
            folders.Dock = DockStyle.Top;
            folders.Height = 74;
            folders.Width = this.ClientSize.Width;
            this.sourceBox = FolderRow(folders, "Source (FH, FH2 or FM3):", 10, AppSettings.Get("CarPort.Source", ""));
            this.targetBox = FolderRow(folders, "Forza Motorsport 4:", 42, AppSettings.Get("CarPort.Target", ""));
            this.load = new Button();
            this.load.Text = "Load Cars";
            this.load.Size = new Size(110, 56);
            this.load.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            this.load.Location = new Point(folders.Width - 122, 9);
            this.load.Click += delegate { this.LoadCars(); };
            folders.Controls.Add(this.load);

            // Cars (left): header, search, list, donor and the one-car actions.
            Panel filter = new Panel();
            filter.Dock = DockStyle.Top;
            filter.Height = 32;
            Label find = new Label();
            find.Text = "Search:";
            find.Location = new Point(8, 9);
            find.AutoSize = true;
            this.search = new TextBox();
            this.search.Location = new Point(62, 7);
            this.search.Width = 260;
            this.search.TextChanged += delegate { this.Filter(); };
            this.onlyNew = new CheckBox();
            this.onlyNew.Text = "Only cars FM4 lacks (and earlier ports)";
            this.onlyNew.Checked = true;
            this.onlyNew.Location = new Point(338, 7);
            this.onlyNew.AutoSize = true;
            this.onlyNew.CheckedChanged += delegate { this.Filter(); };
            filter.Controls.Add(find);
            filter.Controls.Add(this.search);
            filter.Controls.Add(this.onlyNew);

            this.cars = new ListView();
            this.cars.Dock = DockStyle.Fill;
            this.cars.View = View.Details;
            this.cars.FullRowSelect = true;
            this.cars.MultiSelect = false;
            this.cars.HideSelection = false;
            this.cars.Columns.Add("Id", 56);
            this.cars.Columns.Add("Car", 220);
            this.cars.Columns.Add("Year", 50);
            this.cars.Columns.Add("Media name", 180);
            this.cars.Columns.Add("PI FH", 60);
            this.cars.Columns.Add("PI FM4", 60);
            this.cars.Columns.Add("Class", 76);
            this.cars.Columns.Add("In FM4", 220);
            this.cars.SelectedIndexChanged += delegate { this.CarSelected(); };
            this.StyleList(this.cars);

            Panel options = new Panel();
            options.Dock = DockStyle.Bottom;
            options.Height = 92;
            Label donorLabel = new Label();
            donorLabel.Text = "FM4 donor (templates; ★ = closest make, layout, drive, year, weight, PI):";
            donorLabel.Location = new Point(8, 8);
            donorLabel.AutoSize = true;
            this.donors = new ComboBox();
            this.donors.DropDownStyle = ComboBoxStyle.DropDownList;
            this.donors.Location = new Point(8, 28);
            this.donors.Width = 460;
            this.donors.MaxDropDownItems = 24;
            this.donors.SelectedIndexChanged += delegate { this.UpdateButtons(); };
            this.arcade = new CheckBox();
            this.arcade.Text = "In arcade (Data_Car.IsArcade)";
            this.arcade.Checked = true;
            this.arcade.Location = new Point(484, 30);
            this.arcade.AutoSize = true;
            this.trial = ActionButton(options, "Trial Run", 8, 62, 100);
            this.trial.Click += delegate { this.Port(false); };
            this.install = ActionButton(options, "Install In FM4...", 114, 62, 120);
            this.install.Click += delegate { this.Port(true); };
            this.openTrial = ActionButton(options, "Open Trial Folder", 240, 62, 130);
            this.openTrial.Click += delegate
            {
                if (this.trialFolder != null && Directory.Exists(this.trialFolder))
                {
                    Process.Start("explorer.exe", "\"" + this.trialFolder + "\"");
                }
            };
            options.Controls.Add(donorLabel);
            options.Controls.Add(this.donors);
            options.Controls.Add(this.arcade);

            Panel carsArea = new Panel();
            carsArea.Dock = DockStyle.Fill;
            carsArea.Controls.Add(this.cars);
            carsArea.Controls.Add(options);
            carsArea.Controls.Add(filter);
            carsArea.Controls.Add(SectionHeader("Cars"));

            // DLC pack (right).
            Panel packPanel = new Panel();
            packPanel.Dock = DockStyle.Fill;
            Label nameLabel = new Label();
            nameLabel.Text = "Name:";
            nameLabel.Location = new Point(8, 39);
            nameLabel.AutoSize = true;
            this.packName = new TextBox();
            this.packName.Location = new Point(58, 37);
            this.packName.Width = 300;
            this.packName.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            this.packName.Text = AppSettings.Get("CarPort.PackName", "ForzaEditor pack");
            this.packName.TextChanged += delegate { AppSettings.Set("CarPort.PackName", this.packName.Text); this.UpdateButtons(); };
            this.packOfferLabel = new Label();
            this.packOfferLabel.Location = new Point(8, 64);
            this.packOfferLabel.Size = new Size(350, 18);
            this.packOfferLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            this.packOfferLabel.ForeColor = SystemColors.GrayText;
            this.packList = new ListView();
            this.packList.Location = new Point(8, 86);
            this.packList.Size = new Size(350, 150);
            this.packList.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            this.packList.View = View.Details;
            this.packList.FullRowSelect = true;
            this.packList.MultiSelect = false;
            this.packList.HideSelection = false;
            this.packList.Columns.Add("Car", 140);
            this.packList.Columns.Add("Donor", 130);
            this.packList.Columns.Add("Review", 70);
            this.packList.SelectedIndexChanged += delegate { this.PackSelected(); };
            this.StyleList(this.packList);

            int bottom = 300;   // placed from the panel's bottom edge in Layout below
            this.addToPack = PackButton(packPanel, "Add Selected Car", 8, bottom, 130);
            this.addToPack.Click += delegate { this.AddToPack(); };
            this.removeFromPack = PackButton(packPanel, "Remove", 144, bottom, 80);
            this.removeFromPack.Click += delegate { this.RemoveFromPack(); };
            this.newPack = PackButton(packPanel, "New Pack", 230, bottom, 80);
            this.newPack.Click += delegate { this.NewPack(); };
            Label xeniaLabel = new Label();
            xeniaLabel.Text = "Xenia folder (with xenia_canary.exe):";
            xeniaLabel.AutoSize = true;
            packPanel.Controls.Add(xeniaLabel);
            this.xeniaBox = new TextBox();
            this.xeniaBox.Width = 312;
            this.xeniaBox.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            this.xeniaBox.Text = AppSettings.Get("CarPort.Xenia", "");
            this.xeniaBox.TextChanged += delegate { AppSettings.Set("CarPort.Xenia", this.xeniaBox.Text.Trim()); this.UpdatePackInfo(); };
            Button xeniaBrowse = new Button();
            xeniaBrowse.Text = "...";
            xeniaBrowse.Size = new Size(32, 24);
            xeniaBrowse.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            xeniaBrowse.Click += delegate
            {
                using (FolderBrowserDialog dialog = new FolderBrowserDialog())
                {
                    dialog.Description = "Xenia's folder (the one with xenia_canary.exe and its config)";
                    dialog.ShowNewFolderButton = false;
                    if (Directory.Exists(this.xeniaBox.Text))
                    {
                        dialog.SelectedPath = this.xeniaBox.Text;
                    }
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                    {
                        this.xeniaBox.Text = dialog.SelectedPath;
                    }
                }
            };
            packPanel.Controls.Add(this.xeniaBox);
            packPanel.Controls.Add(xeniaBrowse);
            this.trialPack = PackButton(packPanel, "Trial Pack", 8, bottom, 100);
            this.trialPack.Click += delegate { this.TrialPack(); };
            this.buildPack = PackButton(packPanel, "Build DLC Package", 114, bottom, 130);
            this.buildPack.Click += delegate { this.BuildPack(); };
            this.removePack = PackButton(packPanel, "Remove Pack", 250, bottom, 108);
            this.removePack.Click += delegate { this.RemovePack(); };
            this.buildXbox = PackButton(packPanel, "Build for Xbox 360...", 8, bottom, 238);
            this.buildXbox.Click += delegate { this.BuildXbox(); };
            this.packStatus = new Label();
            this.packStatus.Size = new Size(350, 48);
            this.packStatus.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            this.packStatus.ForeColor = SystemColors.GrayText;
            packPanel.Controls.Add(this.packStatus);
            packPanel.Controls.Add(nameLabel);
            packPanel.Controls.Add(this.packName);
            packPanel.Controls.Add(this.packOfferLabel);
            packPanel.Controls.Add(this.packList);
            Control packHeader = SectionHeader("DLC pack (Xenia / Xbox 360)");
            packPanel.Controls.Add(packHeader);
            // Rows from the bottom: hint, actions, Xenia folder, list buttons; the list takes the rest.
            packPanel.Layout += delegate
            {
                int h = packPanel.ClientSize.Height;
                int w = packPanel.ClientSize.Width;
                this.packStatus.Bounds = new Rectangle(8, h - 52, w - 16, 48);
                this.buildXbox.Top = h - 84;
                this.trialPack.Top = this.buildPack.Top = this.removePack.Top = h - 116;
                this.xeniaBox.Bounds = new Rectangle(8, h - 148, w - 54, this.xeniaBox.Height);
                xeniaBrowse.Location = new Point(w - 40, h - 150);
                xeniaLabel.Location = new Point(8, h - 168);
                this.addToPack.Top = this.removeFromPack.Top = this.newPack.Top = h - 204;
                this.packName.Width = w - 66;
                this.packOfferLabel.Width = w - 16;
                this.packList.Bounds = new Rectangle(8, this.packList.Top, w - 16, Math.Max(40, h - 212 - this.packList.Top));
            };
            this.packOffer = (int) Long(AppSettings.Get("CarPort.PackOffer", "0"));

            // Report and review, as tabs under the two areas.
            this.report = new TextBox();
            this.report.Dock = DockStyle.Fill;
            this.report.Multiline = true;
            this.report.ReadOnly = true;
            this.report.ScrollBars = ScrollBars.Both;
            this.report.WordWrap = false;
            this.report.Font = new Font("Consolas", 9f);

            // Review: what the last trial run found not regular, each with a fix to choose. The fixes are
            // kept per car and used by the next trial run and by the install.
            this.reviewPanel = new Panel();
            this.reviewPanel.Dock = DockStyle.Fill;
            this.reviewPanel.Visible = false;
            this.reviewTitle = new Label();
            this.reviewTitle.Dock = DockStyle.Top;
            this.reviewTitle.Height = 24;
            this.reviewTitle.Font = ForzaTheme.Bold;
            this.reviewTitle.Padding = new Padding(6, 5, 0, 0);
            this.review = new DataGridView();
            this.review.Dock = DockStyle.Fill;
            this.review.AllowUserToAddRows = false;
            this.review.AllowUserToDeleteRows = false;
            this.review.RowHeadersVisible = false;
            this.review.SelectionMode = DataGridViewSelectionMode.CellSelect;
            this.review.EditMode = DataGridViewEditMode.EditOnEnter;
            this.review.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            DataGridViewTextBoxColumn where = new DataGridViewTextBoxColumn();
            where.HeaderText = "Where";
            where.ReadOnly = true;
            where.FillWeight = 25;
            DataGridViewTextBoxColumn problem = new DataGridViewTextBoxColumn();
            problem.HeaderText = "Problem";
            problem.ReadOnly = true;
            problem.FillWeight = 45;
            DataGridViewComboBoxColumn fix = new DataGridViewComboBoxColumn();
            fix.HeaderText = "Fix (used by the next trial run and the install)";
            fix.FillWeight = 30;
            fix.FlatStyle = FlatStyle.Flat;
            this.review.Columns.Add(where);
            this.review.Columns.Add(problem);
            this.review.Columns.Add(fix);
            this.review.CellValueChanged += delegate
            {
                this.StoreFixes();
                // A fix of a pack car: the status says whether the trial still matches the fixes.
                if (!this.showingReview && this.reviewCar != null && this.FindEntry(this.reviewCar) != null)
                {
                    this.UpdatePackInfo();
                }
            };
            this.review.CurrentCellDirtyStateChanged += delegate
            {
                if (this.review.IsCurrentCellDirty)
                {
                    this.review.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            };
            this.review.DataError += delegate(object sender, DataGridViewDataErrorEventArgs e) { e.ThrowException = false; };
            this.reviewPanel.Controls.Add(this.review);
            this.reviewPanel.Controls.Add(this.reviewTitle);

            this.bottomTabs = new TabControl();
            TabPage reportPage = new TabPage("Report");
            reportPage.Controls.Add(this.report);
            this.reviewPage = new TabPage("Review");
            this.reviewPage.Controls.Add(this.reviewPanel);
            this.bottomTabs.TabPages.Add(reportPage);
            this.bottomTabs.TabPages.Add(this.reviewPage);

            // Cars | pack above, report / review below.
            SplitContainer top = new SplitContainer();
            top.Dock = DockStyle.Fill;
            top.FixedPanel = FixedPanel.Panel2;
            top.Panel1.Controls.Add(carsArea);
            top.Panel2.Controls.Add(packPanel);
            SplitContainer body = new SplitContainer();
            body.Dock = DockStyle.Fill;
            body.Orientation = Orientation.Horizontal;
            body.FixedPanel = FixedPanel.Panel2;
            body.Panel1.Controls.Add(top);
            body.Panel2.Controls.Add(TabHost(this.bottomTabs));
            this.Load += delegate
            {
                body.SplitterDistance = Math.Max(200, body.Height - 230);
                top.SplitterDistance = Math.Max(400, top.Width - 380);
            };

            this.Controls.Add(body);
            this.Controls.Add(folders);
            this.Controls.Add(status);
            this.Controls.Add(header);
            this.MainMenuStrip = header;
            this.BackColor = ForzaTheme.Background;
            this.ForeColor = ForzaTheme.Text;
            ForzaTheme.Apply(this);
            this.UpdatePackInfo();
        }

        private readonly TabControl bottomTabs;
        private readonly TabPage reviewPage;
        private Dictionary<string, Color> classColors = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);

        private static void FillLastColumn(ListView list)
        {
            // Closing the window resizes the lists once more while they are torn down (their rows are
            // gone from the native list, GetItemRect(0) then throws): nothing to do then.
            if (list.Columns.Count == 0 || !list.IsHandleCreated || list.Disposing || list.IsDisposed || list.FindForm() == null || list.FindForm().Disposing)
            {
                return;
            }
            int others = 0;
            for (int i = 0; i < list.Columns.Count - 1; i++)
            {
                others += list.Columns[i].Width;
            }
            // From the outer width, less the vertical scroll bar when the rows do not fit (right after a
            // refill the list has not shown that bar yet, so ClientSize would still include it).
            int rowHeight = 18;
            try
            {
                if (list.Items.Count > 0)
                {
                    rowHeight = list.GetItemRect(0).Height;
                }
            }
            catch (ArgumentException)
            {
                return;
            }
            bool scrolls = (list.Items.Count * rowHeight) > (list.Height - 28);
            int width = list.Width - (scrolls ? SystemInformation.VerticalScrollBarWidth : 0) - others;
            list.Columns[list.Columns.Count - 1].Width = Math.Max(60, width);
        }

        // An upper-case section title over a thin red rule, like the game's menu headers.
        private static Control SectionHeader(string text)
        {
            Label label = new Label();
            label.Text = text.ToUpperInvariant();
            label.Font = ForzaTheme.Bold;
            label.Dock = DockStyle.Top;
            label.Height = 28;
            label.Padding = new Padding(6, 7, 0, 0);
            label.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (Pen pen = new Pen(ForzaTheme.Red))
                {
                    e.Graphics.DrawLine(pen, 0, label.Height - 2, label.Width, label.Height - 2);
                }
            };
            return label;
        }

        // The game-style tab strip over a TabControl whose own tabs and border are hidden (as the
        // main window's database view).
        private static Control TabHost(TabControl tabs)
        {
            Panel host = new Panel();
            host.Dock = DockStyle.Fill;
            ForzaTabStrip strip = new ForzaTabStrip(tabs);
            strip.Dock = DockStyle.None;
            tabs.Dock = DockStyle.None;
            host.Resize += delegate
            {
                strip.Bounds = new Rectangle(0, 0, host.Width, strip.Height);
                tabs.Bounds = new Rectangle(-4, strip.Height - 4, host.Width + 8, host.Height - strip.Height + 8);
            };
            host.Controls.Add(tabs);
            host.Controls.Add(strip);
            strip.BringToFront();
            return host;
        }

        // Lists drawn like the main window's: upper-case headers over a red rule, alternating dark
        // rows, the selection in Forza red, the FM4 class as the game's badge, dimmed rows kept dim.
        private void StyleList(ListView list)
        {
            list.OwnerDraw = true;
            // The last column fills the row, so no light header area is left at the right.
            list.Resize += delegate { FillLastColumn(list); };
            list.DrawColumnHeader += delegate(object sender, DrawListViewColumnHeaderEventArgs e)
            {
                using (Brush brush = new SolidBrush(ForzaTheme.Background))
                {
                    e.Graphics.FillRectangle(brush, e.Bounds);
                }
                Rectangle text = new Rectangle(e.Bounds.X + 4, e.Bounds.Y, e.Bounds.Width - 6, e.Bounds.Height);
                TextRenderer.DrawText(e.Graphics, e.Header.Text.ToUpperInvariant(), ForzaTheme.Bold, text, ForzaTheme.TextDim, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                using (Pen pen = new Pen(ForzaTheme.Red))
                {
                    e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
                }
            };
            list.DrawItem += delegate(object sender, DrawListViewItemEventArgs e) { };
            list.DrawSubItem += delegate(object sender, DrawListViewSubItemEventArgs e)
            {
                bool selected = e.Item.Selected;
                Color back = selected ? ForzaTheme.Selection : ((e.ItemIndex % 2 == 0) ? ForzaTheme.Surface : Color.FromArgb(31, 31, 34));
                using (Brush brush = new SolidBrush(back))
                {
                    e.Graphics.FillRectangle(brush, e.Bounds);
                }
                string value = e.SubItem.Text;
                if (list.Columns[e.ColumnIndex].Text == "Class" && value.Length > 0)
                {
                    string letter = value;
                    string pi = "";
                    int space = value.IndexOf(' ');
                    if (space > 0)
                    {
                        letter = value.Substring(0, space);
                        pi = value.Substring(space + 1);
                    }
                    Color color;
                    if (!this.classColors.TryGetValue(letter, out color))
                    {
                        color = ForzaTheme.Line;
                    }
                    ForzaTheme.DrawClassBadge(e.Graphics, e.Bounds, letter, pi, color);
                    return;
                }
                Rectangle text = new Rectangle(e.Bounds.X + 4, e.Bounds.Y, e.Bounds.Width - 6, e.Bounds.Height);
                bool dim = e.Item.ForeColor == ForzaTheme.TextDim;
                Color fore = selected ? Color.White : ((e.ColumnIndex == 0 || dim) ? ForzaTheme.TextDim : ForzaTheme.Text);
                TextRenderer.DrawText(e.Graphics, value, ForzaTheme.Regular, text, fore, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            };
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (Directory.Exists(this.sourceBox.Text) && Directory.Exists(this.targetBox.Text))
            {
                this.LoadCars();
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (this.busy)
            {
                e.Cancel = true;
            }
            base.OnFormClosing(e);
        }

        private TextBox FolderRow(Panel parent, string caption, int y, string value)
        {
            Label label = new Label();
            label.Text = caption;
            label.Location = new Point(10, y + 4);
            label.AutoSize = true;
            TextBox box = new TextBox();
            box.Location = new Point(150, y);
            box.Width = parent.Width - 150 - 176;
            box.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            box.Text = value;
            Button browse = new Button();
            browse.Text = "...";
            browse.Size = new Size(32, 24);
            browse.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            browse.Location = new Point(parent.Width - 168, y - 2);
            browse.Click += delegate
            {
                using (FolderBrowserDialog dialog = new FolderBrowserDialog())
                {
                    dialog.Description = caption + " (the one with default.xex and Media)";
                    dialog.ShowNewFolderButton = false;
                    if (Directory.Exists(box.Text))
                    {
                        dialog.SelectedPath = box.Text;
                    }
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                    {
                        box.Text = dialog.SelectedPath;
                    }
                }
            };
            parent.Controls.Add(label);
            parent.Controls.Add(box);
            parent.Controls.Add(browse);
            return box;
        }

        private static Button ActionButton(Panel parent, string text, int x, int y, int width)
        {
            Button button = new Button();
            button.Text = text;
            button.Size = new Size(width, 26);
            button.Location = new Point(x, y - 4);
            parent.Controls.Add(button);
            return button;
        }

        private Car SelectedCar
        {
            get { return (this.cars.SelectedItems.Count == 1) ? (Car) this.cars.SelectedItems[0].Tag : null; }
        }

        private void UpdateButtons()
        {
            Car car = this.SelectedCar;
            bool ready = !this.busy && car != null && (car.State == Status.New || car.State == Status.Ported) && this.donors.SelectedItem != null;
            this.trial.Enabled = ready;
            this.install.Enabled = ready;
            this.load.Enabled = !this.busy;
            this.openTrial.Enabled = !this.busy && this.trialFolder != null && Directory.Exists(this.trialFolder);

            this.addToPack.Enabled = !this.busy && car != null && car.State == Status.New && this.donors.SelectedItem != null && this.FindEntry(car.MediaName) == null;
            this.removeFromPack.Enabled = !this.busy && this.packList.SelectedItems.Count == 1;
            this.newPack.Enabled = !this.busy && this.pack.Count > 0;
            this.trialPack.Enabled = !this.busy && this.pack.Count > 0;
            this.buildPack.Enabled = !this.busy && this.packTrialFolder != null && Directory.Exists(this.packTrialFolder) && this.packName.Text.Trim().Length > 0 && this.XeniaContent() != null;
            this.buildXbox.Enabled = !this.busy && this.packTrialFolder != null && Directory.Exists(this.packTrialFolder) && this.packName.Text.Trim().Length > 0;
            this.removePack.Enabled = !this.busy && this.packOffer > 0 && this.XeniaContent() != null && Directory.Exists(DlcPackage.PackagePath(this.XeniaContent(), this.packOffer));
        }

        private static Button PackButton(Panel parent, string text, int x, int y, int width)
        {
            Button button = new Button();
            button.Text = text;
            button.Size = new Size(width, 26);
            button.Location = new Point(x, y);
            button.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            parent.Controls.Add(button);
            return button;
        }

        #region DLC pack

        // Xenia's FM4 marketplace content folder, or null when the Xenia folder is not set.
        private string XeniaContent()
        {
            string xenia = this.xeniaBox.Text.Trim();
            if (xenia.Length == 0 || !Directory.Exists(xenia))
            {
                return null;
            }
            try
            {
                return DlcPackage.XeniaContent(xenia);
            }
            catch (IOException)
            {
                return null;
            }
        }

        private PackEntry FindEntry(string mediaName)
        {
            foreach (PackEntry entry in this.pack)
            {
                if (string.Equals(entry.Car.MediaName, mediaName, StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }
            }
            return null;
        }

        private PackEntry SelectedEntry
        {
            get { return (this.packList.SelectedItems.Count == 1) ? (PackEntry) this.packList.SelectedItems[0].Tag : null; }
        }

        // The pack changed: the last pack trial no longer matches it.
        private void PackChanged()
        {
            this.packTrialFolder = null;
            List<string> saved = new List<string>();
            foreach (PackEntry entry in this.pack)
            {
                saved.Add(entry.Car.MediaName + "|" + entry.Donor + "|" + (entry.Arcade ? "1" : "0"));
            }
            AppSettings.Set("CarPort.PackCars", string.Join(";", saved.ToArray()));
            AppSettings.Set("CarPort.PackSource", this.sourceBox.Text.Trim());
            this.RefreshPack();
        }

        private void RefreshPack()
        {
            PackEntry selected = this.SelectedEntry;
            this.packList.BeginUpdate();
            this.packList.Items.Clear();
            foreach (PackEntry entry in this.pack)
            {
                ListViewItem item = new ListViewItem(entry.Car.Name);
                item.Tag = entry;
                item.SubItems.Add(entry.Donor);
                item.SubItems.Add((entry.Issues == null) ? "" : (entry.Issues.Count == 0) ? "ok" : entry.Issues.Count + " to see");
                item.Selected = entry == selected;
                this.packList.Items.Add(item);
            }
            this.packList.EndUpdate();
            FillLastColumn(this.packList);
            this.UpdatePackInfo();
        }

        private void UpdatePackInfo()
        {
            string content = this.XeniaContent();
            string offer = (this.packOffer > 0) ? "offer " + this.packOffer + " (" + DlcPackage.OfferId(this.packOffer) + ")" : "offer chosen at the first build";
            bool installed = this.packOffer > 0 && content != null && Directory.Exists(DlcPackage.PackagePath(content, this.packOffer));
            this.packOfferLabel.Text = string.Format("{0} cars, {1}{2}", this.pack.Count, offer, installed ? ", in Xenia" : "");
            if (!this.busy)
            {
                List<string> changed = this.FixesChangedSinceTrial();
                this.packStatus.Text = (this.pack.Count == 0) ? "Select a car on the left, choose its donor, then Add Selected Car."
                    : (this.packTrialFolder == null) ? "Run Trial Pack: every car is converted and checked; nothing is written to the game or to Xenia."
                    : (changed.Count > 0) ? "Fixes changed since the trial (" + string.Join(", ", changed.ToArray()) + "): Trial Pack again applies them; Build DLC Package would use the trial as it is."
                    : (content == null) ? "Set Xenia's folder, then Build DLC Package."
                    : "Trial done. Build DLC Package writes it into Xenia (" + content + ").";
            }
            this.UpdateButtons();
        }

        // The pack cars whose fixes (as chosen now in their review) differ from those the last pack
        // trial used. Opening a review counts too: it stores the suggested fixes.
        private List<string> FixesChangedSinceTrial()
        {
            List<string> changed = new List<string>();
            if (this.packTrialFixes == null)
            {
                return changed;
            }
            foreach (PackEntry entry in this.pack)
            {
                Dictionary<string, object> now;
                Dictionary<string, object> then;
                this.fixes.TryGetValue(entry.Car.MediaName, out now);
                this.packTrialFixes.TryGetValue(entry.Car.MediaName, out then);
                if (!SameFixes(now, then))
                {
                    changed.Add(entry.Car.Name);
                }
            }
            return changed;
        }

        private static bool SameFixes(Dictionary<string, object> a, Dictionary<string, object> b)
        {
            int countA = (a == null) ? 0 : a.Count;
            int countB = (b == null) ? 0 : b.Count;
            if (countA != countB)
            {
                return false;
            }
            if (countA == 0)
            {
                return true;
            }
            foreach (KeyValuePair<string, object> pair in a)
            {
                object other;
                if (!b.TryGetValue(pair.Key, out other) || Convert.ToString(other, CultureInfo.InvariantCulture) != Convert.ToString(pair.Value, CultureInfo.InvariantCulture))
                {
                    return false;
                }
            }
            return true;
        }

        // After the cars are loaded: the pack kept in the settings, when it was made from this source.
        private void RestorePack()
        {
            this.pack.Clear();
            if (string.Equals(AppSettings.Get("CarPort.PackSource", ""), this.sourceBox.Text.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                foreach (string saved in AppSettings.Get("CarPort.PackCars", "").Split(';'))
                {
                    string[] parts = saved.Split('|');
                    if (parts.Length < 3)
                    {
                        continue;
                    }
                    foreach (Car car in this.horizonCars)
                    {
                        if (string.Equals(car.MediaName, parts[0], StringComparison.OrdinalIgnoreCase) && car.State == Status.New)
                        {
                            PackEntry entry = new PackEntry();
                            entry.Car = car;
                            entry.Donor = parts[1];
                            entry.Arcade = parts[2] == "1";
                            this.pack.Add(entry);
                        }
                    }
                }
            }
            this.packTrialFolder = null;
            this.RefreshPack();
        }

        private void AddToPack()
        {
            Car car = this.SelectedCar;
            DonorChoice donor = this.donors.SelectedItem as DonorChoice;
            if (car == null || donor == null || this.FindEntry(car.MediaName) != null)
            {
                return;
            }
            PackEntry entry = new PackEntry();
            entry.Car = car;
            entry.Donor = donor.Car.MediaName;
            entry.Arcade = this.arcade.Checked;
            this.pack.Add(entry);
            this.PackChanged();
        }

        private void RemoveFromPack()
        {
            PackEntry entry = this.SelectedEntry;
            if (entry != null)
            {
                this.pack.Remove(entry);
                this.PackChanged();
            }
        }

        // An empty pack with a new offer (the old package stays in Xenia until removed).
        private void NewPack()
        {
            if (MessageBox.Show(this, "Start a new pack? The car list is emptied and the next build gets a new offer number.\n\nA package already built stays in Xenia (Remove From Xenia takes it away first).",
                this.Text, MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
            {
                return;
            }
            this.pack.Clear();
            this.packOffer = 0;
            AppSettings.Set("CarPort.PackOffer", "0");
            this.PackChanged();
        }

        private void PackSelected()
        {
            PackEntry entry = this.SelectedEntry;
            if (entry != null && entry.Issues != null)
            {
                this.ShowReview(entry.Car.MediaName, entry.Issues, entry.Applied);
            }
            this.UpdateButtons();
        }

        // Every car of the pack ported in turn into one trial folder (CarPort reads what the car before
        // wrote there), with the fixes chosen in its review.
        private void TrialPack()
        {
            string source = this.sourceBox.Text.Trim();
            string target = this.targetBox.Text.Trim();
            string trials = Path.Combine(Path.Combine(Path.GetTempPath(), "ForzaStudio"), "CarPort");
            // A pack trial holds a copy of UI\Textures.zip (436 MB): the earlier pack trials go.
            if (Directory.Exists(trials))
            {
                foreach (string old in Directory.GetDirectories(trials, "Pack_*"))
                {
                    try
                    {
                        Directory.Delete(old, true);
                    }
                    catch (IOException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }
            }
            string folder = Path.Combine(trials, "Pack_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(folder);
            List<PackEntry> entries = new List<PackEntry>(this.pack);
            List<Dictionary<string, object>> chosen = new List<Dictionary<string, object>>();
            Dictionary<string, Dictionary<string, object>> used = new Dictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase);
            foreach (PackEntry entry in entries)
            {
                Dictionary<string, object> fixes;
                chosen.Add(this.fixes.TryGetValue(entry.Car.MediaName, out fixes) ? new Dictionary<string, object>(fixes) : null);
                if (fixes != null)
                {
                    used[entry.Car.MediaName] = new Dictionary<string, object>(fixes);
                }
            }
            this.packTrialFolder = null;
            this.bottomTabs.SelectedIndex = 0;
            this.report.Text = string.Format("Pack trial: {0} cars -> {1}\r\n", entries.Count, folder);
            this.packStatus.Text = "Trial running...";
            this.Background(delegate
            {
                Stopwatch watch = Stopwatch.StartNew();
                System.Text.StringBuilder text = new System.Text.StringBuilder();
                List<CarPort> ports = new List<CarPort>();
                Exception error = null;
                PackEntry failed = null;
                for (int i = 0; i < entries.Count && error == null; i++)
                {
                    CarPort port = new CarPort();
                    port.SourceRoot = source;
                    port.TargetRoot = target;
                    port.OutputRoot = folder;
                    port.MediaName = entries[i].Car.MediaName;
                    port.DonorMediaName = entries[i].Donor;
                    port.Arcade = entries[i].Arcade;
                    if (chosen[i] != null)
                    {
                        port.Fixes = chosen[i];
                    }
                    text.AppendLine().AppendLine("=== " + entries[i].Car.Name + " (" + port.MediaName + "), donor " + port.DonorMediaName);
                    try
                    {
                        port.Run();
                    }
                    catch (Exception ex)
                    {
                        error = ex;
                        failed = entries[i];
                    }
                    text.Append(port.Report.ToString().Replace("\r\n", "\n").Replace("\n", "\r\n"));
                    ports.Add(port);
                }
                return delegate
                {
                    this.report.Text += text.ToString();
                    int issues = 0;
                    for (int i = 0; i < ports.Count; i++)
                    {
                        entries[i].Issues = ports[i].Issues;
                        entries[i].Applied = ports[i].Fixes.Count;
                        issues += ports[i].Issues.Count;
                    }
                    if (error != null)
                    {
                        this.report.Text += "\r\nFAILED (" + failed.Car.MediaName + "): " + error.Message + "\r\n";
                        this.packStatus.Text = failed.Car.Name + ": the trial failed (see the report). Change its donor or take it out of the pack.";
                        this.packTrialFolder = null;
                    }
                    else
                    {
                        this.report.Text += string.Format(CultureInfo.InvariantCulture, "\r\npack trial done in {0:0.0} s; files in {1}\r\n", watch.Elapsed.TotalSeconds, folder);
                        this.packTrialFolder = folder;
                        this.packTrialFixes = used;
                        this.trialFolder = folder;
                    }
                    this.report.SelectionStart = this.report.TextLength;
                    this.report.ScrollToCaret();
                    this.RefreshPack();
                    if (error == null)
                    {
                        this.packStatus.Text = (issues > 0)
                            ? string.Format("Trial done: {0} things to review. Select a car in the pack to see them; a changed fix needs a new trial.", issues)
                            : this.packStatus.Text;
                    }
                };
            });
        }

        private static bool XeniaRunning()
        {
            foreach (string name in new string[] { "xenia_canary", "xenia" })
            {
                if (Process.GetProcessesByName(name).Length > 0)
                {
                    return true;
                }
            }
            return false;
        }

        private void BuildPack()
        {
            string content = this.XeniaContent();
            string trial = this.packTrialFolder;
            string target = this.targetBox.Text.Trim();
            string name = this.packName.Text.Trim();
            if (content == null || trial == null || !this.ReadyToBuild(target))
            {
                return;
            }
            if (XeniaRunning())
            {
                MessageBox.Show(this, "Close Xenia first: the game reads the DLC packages when it starts.", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (this.packOffer == 0)
            {
                this.packOffer = DlcPackage.FreeOffer(target, content);
                AppSettings.Set("CarPort.PackOffer", this.packOffer.ToString(CultureInfo.InvariantCulture));
            }
            int offer = this.packOffer;
            if (Directory.Exists(DlcPackage.PackagePath(content, offer)) && MessageBox.Show(this, string.Format("The pack (offer {0}) is already in Xenia. Replace it with this build?", offer),
                this.Text, MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
            {
                return;
            }
            this.bottomTabs.SelectedIndex = 0;
            this.report.Text = string.Format("Building the DLC package \"{0}\" (offer {1}) into {2}\r\n", name, offer, content);
            this.packStatus.Text = "Building...";
            this.Background(delegate
            {
                System.Text.StringBuilder text = new System.Text.StringBuilder();
                Exception error = null;
                string package = null;
                try
                {
                    DlcPackage.Remove(content, offer);
                    // The target game is the base: in the DLC workflow its files stay untouched.
                    package = DlcPackage.BuildFolder(target, target, trial, content, offer, name, text);
                }
                catch (Exception ex)
                {
                    error = ex;
                    try
                    {
                        DlcPackage.Remove(content, offer);
                    }
                    catch (IOException)
                    {
                    }
                }
                return delegate
                {
                    this.report.Text += text.ToString().Replace("\r\n", "\n").Replace("\n", "\r\n");
                    if (error != null)
                    {
                        this.report.Text += "\r\nFAILED: " + error.Message + "\r\n";
                        this.packStatus.Text = "The build failed (see the report); nothing was left in Xenia.";
                    }
                    else
                    {
                        this.report.Text += "\r\nbuilt " + package + "\r\n";
                        this.UpdatePackInfo();
                        this.packStatus.Text = string.Format("\"{0}\" is in Xenia ({1} cars). Start FM4 from {2}: the cars are with their makes.", name, this.pack.Count, target);
                    }
                    this.report.SelectionStart = this.report.TextLength;
                    this.report.ScrollToCaret();
                };
            });
        }

        // Before a build: fixes changed since the trial (run it again?), and the pack's cars the FM4 folder
        // has as its own zips (they would hide the pack's files). False: do not build now.
        private bool ReadyToBuild(string target)
        {
            List<string> changed = this.FixesChangedSinceTrial();
            if (changed.Count > 0)
            {
                DialogResult answer = MessageBox.Show(this, "The fixes of " + string.Join(", ", changed.ToArray()) + " changed after the trial.\n\nYes: run Trial Pack again now to apply them (then build).\nNo: build the trial as it is.",
                    this.Text, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (answer == DialogResult.Yes)
                {
                    this.TrialPack();
                    return false;
                }
                if (answer == DialogResult.Cancel)
                {
                    return false;
                }
            }
            List<string> names = new List<string>();
            foreach (PackEntry entry in this.pack)
            {
                names.Add(entry.Car.MediaName);
            }
            List<string> shadowed = DlcPackage.ShadowedBy(target, names);
            return shadowed.Count == 0 || MessageBox.Show(this, "The FM4 folder already has these zips of the pack's cars (from an earlier Install In FM4):\n\n" + string.Join("\n", shadowed.ToArray())
                + "\n\nThe game loads them instead of the pack's files, so the pack's changes (material fixes...) would not show. Take them out of the FM4 folder and its zipmanifest.xml first.\n\nBuild anyway?",
                this.Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;
        }

        // The pack as an Xbox 360 package (STFS file) for a modded console: built in a temporary folder
        // like the Xenia one (same offer), then packed into <chosen folder>\Content\0000000000000000\
        // 4D530910\00000002\<content id>, to copy to the console's Hdd1:\ by FTP.
        private void BuildXbox()
        {
            string trial = this.packTrialFolder;
            string target = this.targetBox.Text.Trim();
            string name = this.packName.Text.Trim();
            if (trial == null || !this.ReadyToBuild(target))
            {
                return;
            }
            string output;
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Folder for the Xbox 360 package (a Content folder is made in it, to copy to the console's Hdd1:\\)";
                string last = AppSettings.Get("CarPort.XboxOut", "");
                if (Directory.Exists(last))
                {
                    dialog.SelectedPath = last;
                }
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                output = dialog.SelectedPath;
            }
            AppSettings.Set("CarPort.XboxOut", output);
            if (this.packOffer == 0)
            {
                this.packOffer = DlcPackage.FreeOffer(target, this.XeniaContent() ?? "");
                AppSettings.Set("CarPort.PackOffer", this.packOffer.ToString(CultureInfo.InvariantCulture));
            }
            int offer = this.packOffer;
            byte[] thumbnail = PackThumbnail(this.Icon);
            this.bottomTabs.SelectedIndex = 0;
            this.report.Text = string.Format("Building the Xbox 360 package \"{0}\" (offer {1}) into {2}\r\n", name, offer, output);
            this.packStatus.Text = "Building the Xbox 360 package...";
            this.Background(delegate
            {
                System.Text.StringBuilder text = new System.Text.StringBuilder();
                Exception error = null;
                string file = null;
                string work = Path.Combine(Path.Combine(Path.GetTempPath(), "ForzaStudio"), "Xbox_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture));
                try
                {
                    string content = Path.Combine(work, "00000002");
                    string folder = DlcPackage.BuildFolder(target, target, trial, content, offer, name, text);
                    file = DlcPackage.BuildConsolePackage(folder, output, name, thumbnail, text);
                }
                catch (Exception ex)
                {
                    error = ex;
                }
                finally
                {
                    try
                    {
                        if (Directory.Exists(work))
                        {
                            Directory.Delete(work, true);
                        }
                    }
                    catch (IOException)
                    {
                    }
                }
                return delegate
                {
                    this.report.Text += text.ToString().Replace("\r\n", "\n").Replace("\n", "\r\n");
                    if (error != null)
                    {
                        this.report.Text += "\r\nFAILED: " + error.Message + "\r\n";
                        this.packStatus.Text = "The Xbox 360 package failed (see the report).";
                    }
                    else
                    {
                        this.packStatus.Text = string.Format("Xbox 360 package ready: copy the Content folder in {0} to the console's Hdd1:\\ (FTP).", output);
                        Process.Start("explorer.exe", "/select,\"" + file + "\"");
                    }
                    this.report.SelectionStart = this.report.TextLength;
                    this.report.ScrollToCaret();
                };
            });
        }

        // The package picture the dashboard shows: the program's icon, 64 x 64 PNG.
        private static byte[] PackThumbnail(Icon icon)
        {
            if (icon == null)
            {
                return null;
            }
            using (Bitmap bitmap = new Bitmap(64, 64))
            {
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(ForzaTheme.Background);
                    graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    using (Icon large = new Icon(icon, 64, 64))
                    {
                        graphics.DrawIcon(large, new Rectangle(0, 0, 64, 64));
                    }
                }
                using (MemoryStream png = new MemoryStream())
                {
                    bitmap.Save(png, System.Drawing.Imaging.ImageFormat.Png);
                    return png.ToArray();
                }
            }
        }

        private void RemovePack()
        {
            string content = this.XeniaContent();
            if (content == null || this.packOffer == 0)
            {
                return;
            }
            if (XeniaRunning())
            {
                MessageBox.Show(this, "Close Xenia first.", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (MessageBox.Show(this, string.Format("Remove the pack (offer {0}) from Xenia?\n\n{1}", this.packOffer, DlcPackage.PackagePath(content, this.packOffer)),
                this.Text, MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
            {
                return;
            }
            DlcPackage.Remove(content, this.packOffer);
            this.UpdatePackInfo();
            this.packStatus.Text = "The pack was removed from Xenia.";
        }

        #endregion

        #region Loading

        private void LoadCars()
        {
            string source = this.sourceBox.Text.Trim();
            string target = this.targetBox.Text.Trim();
            ForzaGame sourceGame = GameInfo.Detect(source);
            ForzaGame targetGame = GameInfo.Detect(target);
            if ((sourceGame != ForzaGame.Horizon1 && sourceGame != ForzaGame.Horizon2 && sourceGame != ForzaGame.Motorsport3) || targetGame != ForzaGame.Motorsport4)
            {
                MessageBox.Show(this, string.Format("The first folder must hold Forza Horizon, Forza Horizon 2 or Forza Motorsport 3 and the second Forza Motorsport 4.\n\nFound: {0} and {1}.",
                    GameInfo.Name(sourceGame) ?? "no Forza game", GameInfo.Name(targetGame) ?? "no Forza game"), this.Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            AppSettings.Set("CarPort.Source", source);
            AppSettings.Set("CarPort.Target", target);
            this.details.Text = "Loading the two databases...";
            this.Background(delegate
            {
                List<Car> horizon;
                List<Car> motorsport;
                ReadCars(source, target, out horizon, out motorsport);
                Dictionary<string, Color> colors = ClassColors(target);
                return delegate
                {
                    this.classColors = colors;
                    this.sourceTag = GameInfo.Tag(sourceGame);
                    this.cars.Columns[4].Text = "PI " + this.sourceTag;
                    this.fm4Cars = motorsport;
                    this.items.Clear();
                    foreach (Car car in horizon)
                    {
                        ListViewItem item = new ListViewItem(car.Id.ToString(CultureInfo.InvariantCulture));
                        item.Tag = car;
                        item.SubItems.Add(car.Name);
                        item.SubItems.Add(car.Year.ToString(CultureInfo.InvariantCulture));
                        item.SubItems.Add(car.MediaName);
                        item.SubItems.Add(car.Pi.ToString("0.000", CultureInfo.InvariantCulture));
                        item.SubItems.Add(car.Fm4Pi.ToString("0.000", CultureInfo.InvariantCulture));
                        // The class badge with the in-game PI (PerformanceIndex * 1000 + 100), as in the game.
                        item.SubItems.Add(car.Fm4Class + " " + ((int) Math.Round((car.Fm4Pi * 1000) + 100)).ToString(CultureInfo.InvariantCulture));
                        item.SubItems.Add(car.Note);
                        if (car.State != Status.New && car.State != Status.Ported)
                        {
                            item.ForeColor = ForzaTheme.TextDim;
                        }
                        this.items.Add(item);
                    }
                    this.Filter();
                    this.horizonCars = horizon;
                    this.RestorePack();
                    int portable = 0;
                    foreach (Car car in horizon)
                    {
                        if (car.State == Status.New)
                        {
                            portable++;
                        }
                    }
                    this.details.Text = string.Format("{0} {1} cars, {2} not in FM4. Choose a car.", horizon.Count, this.sourceTag, portable);
                };
            });
        }

        // FM4's class colors (CarClasses.ClassColor by badge letter), for the class badges.
        private static Dictionary<string, Color> ClassColors(string target)
        {
            Dictionary<string, Color> colors = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (GameDatabase db = new GameDatabase(File.ReadAllBytes(Path.Combine(Path.Combine(Path.Combine(target, "Media"), "db"), "gamedb.slt"))))
                {
                    colors = db.ClassColors();
                }
            }
            catch (InvalidOperationException)
            {
            }
            return colors;
        }

        private static void ReadCars(string source, string target, out List<Car> horizon, out List<Car> motorsport)
        {
            SqliteDatabase from = SqliteDatabase.FromBytes(File.ReadAllBytes(Path.Combine(Path.Combine(Path.Combine(source, "Media"), "db"), "gamedb.slt")));
            SqliteDatabase to = SqliteDatabase.FromBytes(File.ReadAllBytes(Path.Combine(Path.Combine(Path.Combine(target, "Media"), "db"), "gamedb.slt")));
            try
            {
                horizon = Cars(from, Names(source));
                motorsport = Cars(to, Names(target));
                List<double[]> pairs = CarPort.PerformanceIndexPairs(from, to, -1, source, target);
                Dictionary<long, Car> byId = new Dictionary<long, Car>();
                Dictionary<string, Car> byName = new Dictionary<string, Car>(StringComparer.OrdinalIgnoreCase);
                foreach (Car car in motorsport)
                {
                    byId[car.Id] = car;
                    byName[car.MediaName] = car;
                }
                string sourceCars = Path.Combine(Path.Combine(source, "Media"), "cars");
                List<Car> ported = new List<Car>();
                foreach (Car car in horizon)
                {
                    car.Fm4Pi = CarPort.MapPerformanceIndex(pairs, car.Pi);
                    car.Fm4Class = CarPort.ClassName(to, CarPort.ClassFor(to, car.Fm4Pi, car.Id));
                    Car same;
                    Car other;
                    if (!File.Exists(Path.Combine(sourceCars, car.MediaName + ".zip")))
                    {
                        car.State = Status.NoFiles;
                        car.Note = "no car zip in the source folder";
                    }
                    else if (byName.TryGetValue(car.MediaName, out same))
                    {
                        if (same.Id == car.Id && CarPort.IsPorted(source, target, car.MediaName))
                        {
                            car.State = Status.Ported;
                            car.Note = "ported earlier (install updates it)";
                            ported.Add(same);
                        }
                        else
                        {
                            car.State = Status.InFm4;
                            car.Note = "an FM4 car (id " + same.Id + ")";
                        }
                    }
                    else if (byId.TryGetValue(car.Id, out other))
                    {
                        car.State = Status.IdTaken;
                        car.Note = "id taken by " + other.MediaName;
                    }
                    else
                    {
                        car.State = Status.New;
                        car.Note = "";
                    }
                }
                // Donors: FM4's own cars only.
                foreach (Car car in ported)
                {
                    motorsport.Remove(car);
                }
                string targetCars = Path.Combine(Path.Combine(target, "Media"), "cars");
                motorsport.RemoveAll(delegate(Car car) { return !File.Exists(Path.Combine(targetCars, car.MediaName + ".zip")); });
            }
            finally
            {
                from.Dispose();
                to.Dispose();
            }
        }

        private static StringTable Names(string root)
        {
            try
            {
                return StringTable.Load(root, "EN");
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static List<Car> Cars(SqliteDatabase db, StringTable names)
        {
            List<Car> cars = new List<Car>();
            foreach (object[] row in db.Query("SELECT Id, MediaName, DisplayName, Year, MakeID, EnginePlacementID, DriveTypeID, CarTypeID, CurbWeight, PerformanceIndex FROM Data_Car ORDER BY Id").Rows)
            {
                if (!(row[1] is string))
                {
                    continue;
                }
                Car car = new Car();
                car.Id = Convert.ToInt64(row[0]);
                car.MediaName = (string) row[1];
                string name = (names != null) ? names.Lookup("Data_Car", "DisplayName", row[2] as string) : null;
                car.Name = string.IsNullOrEmpty(name) ? car.MediaName : name;
                car.Year = Long(row[3]);
                car.Make = Long(row[4]);
                car.Placement = Long(row[5]);
                car.Drive = Long(row[6]);
                car.Type = Long(row[7]);
                car.Weight = Double(row[8]);
                car.Pi = Double(row[9]);
                cars.Add(car);
            }
            return cars;
        }

        private static long Long(object value)
        {
            return (value == null) ? -1 : Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }

        private static double Double(object value)
        {
            return (value == null) ? 0 : Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }

        #endregion

        #region Selection

        private void Filter()
        {
            string find = this.search.Text.Trim();
            this.cars.BeginUpdate();
            this.cars.Items.Clear();
            List<ListViewItem> shown = new List<ListViewItem>();
            foreach (ListViewItem item in this.items)
            {
                bool match = find.Length == 0;
                foreach (ListViewItem.ListViewSubItem sub in item.SubItems)
                {
                    match = match || sub.Text.IndexOf(find, StringComparison.OrdinalIgnoreCase) >= 0;
                }
                Status state = ((Car) item.Tag).State;
                if (match && (!this.onlyNew.Checked || state == Status.New || state == Status.Ported))
                {
                    shown.Add(item);
                }
            }
            this.cars.Items.AddRange(shown.ToArray());
            this.cars.EndUpdate();
            FillLastColumn(this.cars);
        }

        private void CarSelected()
        {
            Car car = this.SelectedCar;
            this.donors.Items.Clear();
            if (car == null || !string.Equals(car.MediaName, this.reviewCar, StringComparison.OrdinalIgnoreCase))
            {
                this.reviewPanel.Visible = false;   // the fixes stay kept for that car
            }
            if (car == null)
            {
                this.UpdateButtons();
                return;
            }
            this.details.Text = string.Format("{0} ({1}, id {2}): {7} PI {3:0.000} -> FM4 PI {4:0.000}, class {5}.  {6}",
                car.Name, car.MediaName, car.Id, car.Pi, car.Fm4Pi, car.Fm4Class,
                (car.State == Status.New) ? "Not in FM4: it will be added." : car.Note + ".", this.sourceTag);
            if (car.State == Status.New || car.State == Status.Ported)
            {
                List<Car> ranked = new List<Car>(this.fm4Cars);
                Dictionary<Car, double> score = new Dictionary<Car, double>();
                foreach (Car donor in ranked)
                {
                    score[donor] = DonorScore(car, donor);
                }
                ranked.Sort(delegate(Car a, Car b) { return score[b].CompareTo(score[a]); });
                for (int i = 0; i < ranked.Count; i++)
                {
                    DonorChoice choice = new DonorChoice();
                    choice.Car = ranked[i];
                    choice.Suggested = i < 5;
                    this.donors.Items.Add(choice);
                }
                if (this.donors.Items.Count > 0)
                {
                    this.donors.SelectedIndex = 0;
                }
            }
            this.UpdateButtons();
        }

        // How close an FM4 car is to the one being ported: the donor gives the brakes fallback, the
        // CarAttribs.xml and versiondata.xml templates and the manifest position (P1 -> MP4-12C,
        // LaFerrari -> Enzo).
        private static double DonorScore(Car car, Car donor)
        {
            double score = 0;
            score += (car.Make == donor.Make) ? 4 : 0;
            score += (car.Placement == donor.Placement) ? 2 : 0;
            score += (car.Drive == donor.Drive) ? 2 : 0;
            score += (car.Type == donor.Type) ? 1 : 0;
            score -= Math.Abs(car.Year - donor.Year) / 15.0;
            score -= (car.Weight > 0) ? 3 * Math.Abs(car.Weight - donor.Weight) / car.Weight : 0;
            score -= 5 * Math.Abs(car.Fm4Pi - donor.Pi);
            return score;
        }

        #endregion

        #region Port

        private void Port(bool installing)
        {
            Car car = this.SelectedCar;
            DonorChoice donor = this.donors.SelectedItem as DonorChoice;
            if (car == null || donor == null)
            {
                return;
            }
            if (installing && MessageBox.Show(this, string.Format("Install {0} into the FM4 folder?\n\n{1}\n\nThe game database, the car's zips, the string tables, the sound and texture zips and zipmanifest.xml are rewritten; every file is backed up first to\n{2}\n\nClose Xenia (or the game) before going on.",
                car.Name, this.targetBox.Text, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bak")), this.Text, MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
            {
                return;
            }
            CarPort port = new CarPort();
            port.SourceRoot = this.sourceBox.Text.Trim();
            port.TargetRoot = this.targetBox.Text.Trim();
            port.MediaName = car.MediaName;
            port.DonorMediaName = donor.Car.MediaName;
            port.Arcade = this.arcade.Checked;
            Dictionary<string, object> chosen;
            if (this.fixes.TryGetValue(car.MediaName, out chosen))
            {
                port.Fixes = new Dictionary<string, object>(chosen);
            }
            string folder = null;
            if (!installing)
            {
                folder = Path.Combine(Path.Combine(Path.Combine(Path.GetTempPath(), "ForzaStudio"), "CarPort"), car.MediaName + "_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture));
                Directory.CreateDirectory(folder);
                port.OutputRoot = folder;
            }
            this.bottomTabs.SelectedIndex = 0;
            this.report.Text = string.Format("{0} {1} -> {2}, donor {3}...\r\n", installing ? "Installing" : "Trial run:", car.MediaName, port.TargetRoot, donor.Car.MediaName);
            this.Background(delegate
            {
                Stopwatch watch = Stopwatch.StartNew();
                Exception error = null;
                try
                {
                    port.Run();
                }
                catch (Exception ex)
                {
                    error = ex;
                }
                string text = port.Report.ToString().Replace("\r\n", "\n").Replace("\n", "\r\n");
                return delegate
                {
                    this.report.Text += text;
                    if (error != null)
                    {
                        this.report.Text += "\r\nFAILED: " + error.Message + "\r\n";
                        this.details.Text = car.Name + ": the " + (installing ? "install" : "trial run") + " failed (see the report).";
                        if (installing)
                        {
                            MessageBox.Show(this, error.Message, this.Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }
                    else
                    {
                        this.report.Text += string.Format(CultureInfo.InvariantCulture, "\r\ndone in {0:0.0} s{1}\r\n", watch.Elapsed.TotalSeconds,
                            installing ? "" : "; trial files in " + folder);
                        if (installing)
                        {
                            this.details.Text = car.Name + " is installed in FM4. Start the game to try it.";
                        }
                        else
                        {
                            this.trialFolder = folder;
                            this.details.Text = car.Name + ": trial run done, nothing in the FM4 folder changed. " + ((port.Issues.Count > 0)
                                ? port.Issues.Count + " things to review below: choose the fixes, run the trial again, then install."
                                : "Read the report, then install.");
                        }
                        this.ShowReview(car.MediaName, port.Issues, port.Fixes.Count);
                    }
                    this.report.SelectionStart = this.report.TextLength;
                    this.report.ScrollToCaret();
                    if (installing && error == null)
                    {
                        car.State = Status.Ported;
                        car.Note = "ported (install updates it)";
                        foreach (ListViewItem item in this.items)
                        {
                            if (item.Tag == car)
                            {
                                item.SubItems[7].Text = car.Note;
                            }
                        }
                    }
                };
            });
        }

        // The issues of the last run of this car, each row preset to the fix chosen before (or the
        // suggested one for a material).
        private void ShowReview(string mediaName, List<CarPort.Issue> issues, int applied)
        {
            this.showingReview = true;
            this.reviewCar = mediaName;
            Dictionary<string, object> chosen;
            if (!this.fixes.TryGetValue(mediaName, out chosen))
            {
                chosen = new Dictionary<string, object>();
                this.fixes[mediaName] = chosen;
            }
            this.review.Rows.Clear();
            foreach (CarPort.Issue issue in issues)
            {
                int index = this.review.Rows.Add();
                DataGridViewRow row = this.review.Rows[index];
                row.Tag = issue;
                row.Cells[0].Value = issue.Where;
                row.Cells[1].Value = issue.Problem;
                DataGridViewComboBoxCell cell = (DataGridViewComboBoxCell) row.Cells[2];
                cell.Items.Add(LeaveAsIs);
                string selected = LeaveAsIs;
                foreach (CarPort.Choice choice in issue.Choices)
                {
                    cell.Items.Add(choice.Label);
                    object value;
                    if ((chosen.TryGetValue(issue.Key, out value) && object.Equals(Convert.ToString(value, CultureInfo.InvariantCulture), Convert.ToString(choice.Value, CultureInfo.InvariantCulture)))
                        || (!chosen.ContainsKey(issue.Key) && issue.Suggested != null && object.Equals(issue.Suggested, choice.Value)))
                    {
                        selected = choice.Label;
                    }
                }
                cell.Value = selected;
            }
            this.reviewTitle.Text = string.Format("Review of {0}: {1} things not regular for FM4{2}", mediaName, issues.Count, (applied > 0) ? string.Format(" ({0} fixes chosen earlier were applied)", applied) : "");
            this.reviewPanel.Visible = issues.Count > 0 || applied > 0;
            this.reviewPage.Text = (issues.Count > 0) ? "Review (" + issues.Count + ")" : "Review";
            if (issues.Count > 0)
            {
                this.bottomTabs.SelectedTab = this.reviewPage;
            }
            this.bottomTabs.Parent.Invalidate(true);   // the tab strip shows the new count
            this.StoreFixes();
            this.showingReview = false;
        }

        // The fixes picked in the grid, kept for the car on review.
        private void StoreFixes()
        {
            if (this.reviewCar == null)
            {
                return;
            }
            Dictionary<string, object> chosen = this.fixes[this.reviewCar];
            foreach (DataGridViewRow row in this.review.Rows)
            {
                CarPort.Issue issue = row.Tag as CarPort.Issue;
                string text = row.Cells[2].Value as string;
                if (issue == null || text == null)
                {
                    continue;
                }
                chosen.Remove(issue.Key);
                foreach (CarPort.Choice choice in issue.Choices)
                {
                    if (choice.Label == text)
                    {
                        chosen[issue.Key] = choice.Value;
                    }
                }
            }
        }

        // Work off the UI thread; the returned action runs back on it.
        private delegate MethodInvoker Work();

        private void Background(Work work)
        {
            this.busy = true;
            this.UseWaitCursor = true;
            this.UpdateButtons();
            Thread thread = new Thread(delegate()
            {
                MethodInvoker done;
                try
                {
                    done = work();
                }
                catch (Exception ex)
                {
                    done = delegate
                    {
                        this.details.Text = "Failed: " + ex.Message;
                        MessageBox.Show(this, ex.Message, this.Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    };
                }
                this.BeginInvoke((MethodInvoker) delegate
                {
                    this.busy = false;
                    this.UseWaitCursor = false;
                    done();
                    this.UpdateButtons();
                });
            });
            thread.IsBackground = true;
            thread.Start();
        }

        #endregion
    }
}
