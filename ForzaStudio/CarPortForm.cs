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

    // Tools > Port Car From FH2: pick the FH2 and FM4 folders, a car and an FM4 donor, then a trial
    // run (files written to a temporary folder, the report shown) and the install (CarPort).
    // The car list says what each car would be in FM4: new, ported earlier (the install updates it),
    // already an FM4 car or with its id taken (both refused), or without files.
    internal class CarPortForm : Form
    {
        private readonly TextBox sourceBox;
        private readonly TextBox targetBox;
        private readonly TextBox search;
        private readonly CheckBox onlyNew;
        private readonly ListView cars;
        private readonly ComboBox donors;
        private readonly CheckBox arcade;
        private readonly Label details;
        private readonly TextBox report;
        private readonly Button load;
        private readonly Button trial;
        private readonly Button install;
        private readonly Button openTrial;
        private readonly List<ListViewItem> items = new List<ListViewItem>();

        private List<Car> fm4Cars = new List<Car>();
        private string trialFolder;
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
            public double Fm4Pi;        // FH2 cars: the PI they would get in FM4
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
            this.Text = "Port Car From Forza Horizon 2";
            this.Size = new Size(1000, 760);
            this.MinimumSize = new Size(820, 600);
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = ForzaTheme.Regular;

            Panel folders = new Panel();
            folders.Dock = DockStyle.Top;
            folders.Height = 74;
            folders.Width = this.ClientSize.Width;
            this.sourceBox = FolderRow(folders, "Forza Horizon 2 folder:", 8, AppSettings.Get("CarPort.Source", ""));
            this.targetBox = FolderRow(folders, "Forza Motorsport 4 folder:", 40, AppSettings.Get("CarPort.Target", ""));
            this.load = new Button();
            this.load.Text = "Load Cars";
            this.load.Size = new Size(96, 28);
            this.load.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            this.load.Location = new Point(folders.Width - 106, 6);
            this.load.Click += delegate { this.LoadCars(); };
            folders.Controls.Add(this.load);

            Panel filter = new Panel();
            filter.Dock = DockStyle.Top;
            filter.Height = 32;
            Label find = new Label();
            find.Text = "Search:";
            find.Location = new Point(10, 8);
            find.AutoSize = true;
            this.search = new TextBox();
            this.search.Location = new Point(64, 5);
            this.search.Width = 260;
            this.search.BorderStyle = BorderStyle.FixedSingle;
            this.search.TextChanged += delegate { this.Filter(); };
            this.onlyNew = new CheckBox();
            this.onlyNew.Text = "Only cars FM4 lacks (and earlier ports)";
            this.onlyNew.Checked = true;
            this.onlyNew.Location = new Point(340, 6);
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
            this.cars.Columns.Add("Car", 230);
            this.cars.Columns.Add("Year", 50);
            this.cars.Columns.Add("Media name", 180);
            this.cars.Columns.Add("PI FH2", 60);
            this.cars.Columns.Add("PI FM4", 60);
            this.cars.Columns.Add("Class", 50);
            this.cars.Columns.Add("In FM4", 230);
            this.cars.SelectedIndexChanged += delegate { this.CarSelected(); };

            Panel options = new Panel();
            options.Dock = DockStyle.Bottom;
            options.Height = 104;
            this.details = new Label();
            this.details.Location = new Point(10, 6);
            this.details.Size = new Size(960, 20);
            this.details.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            this.details.Text = "Choose the two game folders and load the cars.";
            Label donorLabel = new Label();
            donorLabel.Text = "FM4 donor (templates; ★ = closest make, layout, drive, year, weight, PI):";
            donorLabel.Location = new Point(10, 30);
            donorLabel.AutoSize = true;
            this.donors = new ComboBox();
            this.donors.DropDownStyle = ComboBoxStyle.DropDownList;
            this.donors.Location = new Point(10, 50);
            this.donors.Width = 460;
            this.donors.MaxDropDownItems = 24;
            this.donors.SelectedIndexChanged += delegate { this.UpdateButtons(); };
            this.arcade = new CheckBox();
            this.arcade.Text = "In arcade (Data_Car.IsArcade)";
            this.arcade.Checked = true;
            this.arcade.Location = new Point(486, 52);
            this.arcade.AutoSize = true;
            this.trial = ActionButton(options, "Trial Run", 10, 78);
            this.trial.Click += delegate { this.Port(false); };
            this.install = ActionButton(options, "Install In FM4...", 118, 78);
            this.install.Click += delegate { this.Port(true); };
            this.openTrial = ActionButton(options, "Open Trial Folder", 226, 78);
            this.openTrial.Width = 130;
            this.openTrial.Click += delegate
            {
                if (this.trialFolder != null && Directory.Exists(this.trialFolder))
                {
                    Process.Start("explorer.exe", "\"" + this.trialFolder + "\"");
                }
            };
            options.Controls.Add(this.details);
            options.Controls.Add(donorLabel);
            options.Controls.Add(this.donors);
            options.Controls.Add(this.arcade);

            this.report = new TextBox();
            this.report.Dock = DockStyle.Bottom;
            this.report.Height = 170;
            this.report.Multiline = true;
            this.report.ReadOnly = true;
            this.report.ScrollBars = ScrollBars.Both;
            this.report.WordWrap = false;
            this.report.Font = new Font("Consolas", 9f);
            this.report.BorderStyle = BorderStyle.FixedSingle;

            this.Controls.Add(this.cars);
            this.Controls.Add(options);
            this.Controls.Add(this.report);
            this.Controls.Add(filter);
            this.Controls.Add(folders);
            this.BackColor = ForzaTheme.Background;
            ForzaTheme.Apply(this);
            this.UpdateButtons();
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
            box.Location = new Point(170, y);
            box.Width = 640;
            box.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            box.BorderStyle = BorderStyle.FixedSingle;
            box.Text = value;
            Button browse = new Button();
            browse.Text = "...";
            browse.Size = new Size(32, 24);
            browse.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            browse.Location = new Point(816, y - 1);
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

        private static Button ActionButton(Panel parent, string text, int x, int y)
        {
            Button button = new Button();
            button.Text = text;
            button.Size = new Size(100, 24);
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
        }

        #region Loading

        private void LoadCars()
        {
            string source = this.sourceBox.Text.Trim();
            string target = this.targetBox.Text.Trim();
            ForzaGame sourceGame = GameInfo.Detect(source);
            ForzaGame targetGame = GameInfo.Detect(target);
            if (sourceGame == ForzaGame.Horizon1)
            {
                MessageBox.Show(this, "Forza Horizon (the first) is not supported yet: only Forza Horizon 2 cars can be ported.", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (sourceGame != ForzaGame.Horizon2 || targetGame != ForzaGame.Motorsport4)
            {
                MessageBox.Show(this, string.Format("The first folder must hold Forza Horizon 2 and the second Forza Motorsport 4.\n\nFound: {0} and {1}.",
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
                return delegate
                {
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
                        item.SubItems.Add(car.Fm4Class);
                        item.SubItems.Add(car.Note);
                        if (car.State != Status.New && car.State != Status.Ported)
                        {
                            item.ForeColor = ForzaTheme.TextDim;
                        }
                        this.items.Add(item);
                    }
                    this.Filter();
                    int portable = 0;
                    foreach (Car car in horizon)
                    {
                        if (car.State == Status.New)
                        {
                            portable++;
                        }
                    }
                    this.details.Text = string.Format("{0} FH2 cars, {1} not in FM4. Choose a car.", horizon.Count, portable);
                };
            });
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
                        car.Note = "no car zip in the FH2 folder";
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
        }

        private void CarSelected()
        {
            Car car = this.SelectedCar;
            this.donors.Items.Clear();
            if (car == null)
            {
                this.UpdateButtons();
                return;
            }
            this.details.Text = string.Format("{0} ({1}, id {2}): FH2 PI {3:0.000} -> FM4 PI {4:0.000}, class {5}.  {6}",
                car.Name, car.MediaName, car.Id, car.Pi, car.Fm4Pi, car.Fm4Class,
                (car.State == Status.New) ? "Not in FM4: it will be added." : car.Note + ".");
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
            string folder = null;
            if (!installing)
            {
                folder = Path.Combine(Path.Combine(Path.Combine(Path.GetTempPath(), "ForzaStudio"), "CarPort"), car.MediaName + "_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture));
                Directory.CreateDirectory(folder);
                port.OutputRoot = folder;
            }
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
                            this.details.Text = car.Name + ": trial run done, nothing in the FM4 folder changed. Read the report, then install.";
                        }
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
