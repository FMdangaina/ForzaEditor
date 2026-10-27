namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Globalization;
    using System.Windows.Forms;

    // Tools > Change Car Sound: the user picks a car and the car whose engine sound it should get. The engine sound is
    // Data_Engine.MediaName (the name of the engine tuning, <MediaName>_ET.xml, with its harmonic tunings and sound
    // banks), so changing it is one value of the car's stock engine, as the Sound box of Duplicate Car does. The donor
    // list can be filtered by cylinders, layout and aspiration (naturally aspirated, turbo, DSC / CSC supercharger) and
    // follows the car's own engine by default, so the first cars shown are the similar ones. Works on the database open in
    // the database view: the game's gamedb.slt, or a DLC package's merge database (the game's gamedb.slt is then its reference).
    internal sealed class ChangeSoundForm : Form
    {
        private sealed class CarRow
        {
            public long Id;
            public string Name;
            public long EngineId;
            public string Sound;
            public string Layout;
            public long Cylinders;
            public long Aspiration;
            public string AspirationName;
            public bool Own;           // the engine row is in the database that is edited (not in the reference one)

            public int Group
            {
                get { return AspirationGroup(this.Aspiration); }
            }
        }

        private static readonly string[] Groups = { "Any aspiration", "Naturally aspirated", "Turbo (single, twin, quad)", "Supercharger DSC", "Supercharger CSC" };

        private readonly GameDatabase db;
        private readonly GameDatabase reference;   // the game's own gamedb.slt when the open database is a partial one (a DLC pack's)
        private readonly List<CarRow> cars = new List<CarRow>();
        private readonly ListView targets = new ListView();
        private readonly ListView donors = new ListView();
        private readonly TextBox targetFilter = new TextBox();
        private readonly TextBox donorFilter = new TextBox();
        private readonly ComboBox layout = new ComboBox();
        private readonly ComboBox cylinders = new ComboBox();
        private readonly ComboBox aspiration = new ComboBox();
        private readonly CheckBox follow = new CheckBox();
        private readonly Label info = new Label();
        private readonly Button change = new Button();
        private bool filling;

        public event EventHandler Changed;

        public ChangeSoundForm(GameDatabase db, string gameRoot)
        {
            this.db = db;
            this.reference = db.Reference ?? OpenGameDb(gameRoot, db);
            this.FormClosed += delegate { if (this.reference != null && this.reference != this.db.Reference) { this.reference.Dispose(); } };
            this.AutoScaleMode = AutoScaleMode.None;
            this.Text = "Change car sound";
            this.ClientSize = new Size(1000, 640);
            this.MinimumSize = new Size(800, 480);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.CenterParent;

            this.LoadCars();

            Label left = new Label();
            left.Text = "1. The car to change:";
            left.SetBounds(12, 10, 300, 20);
            this.targetFilter.SetBounds(12, 34, 470, 24);
            this.SetupList(this.targets, 12, 64, 470, 420);
            this.targetFilter.TextChanged += delegate { this.FillTargets(); };
            this.targets.SelectedIndexChanged += delegate { this.OnTargetChanged(); };

            Label right = new Label();
            right.Text = "2. Take the engine sound of:";
            right.SetBounds(500, 10, 300, 20);
            this.donorFilter.SetBounds(500, 34, 200, 24);
            this.layout.SetBounds(708, 34, 90, 24);
            this.cylinders.SetBounds(804, 34, 90, 24);
            this.aspiration.SetBounds(500, 64, 200, 24);
            this.follow.Text = "Same engine as the car";
            this.follow.Checked = true;
            this.follow.SetBounds(708, 64, 200, 24);
            foreach (ComboBox box in new ComboBox[] { this.layout, this.cylinders, this.aspiration })
            {
                box.DropDownStyle = ComboBoxStyle.DropDownList;
                box.MaxDropDownItems = 24;
            }
            this.FillFilters();
            this.SetupList(this.donors, 500, 94, 488, 390);
            this.donorFilter.TextChanged += delegate { this.FillDonors(); };
            this.layout.SelectedIndexChanged += delegate { this.OnFilterChosen(); };
            this.cylinders.SelectedIndexChanged += delegate { this.OnFilterChosen(); };
            this.aspiration.SelectedIndexChanged += delegate { this.OnFilterChosen(); };
            this.follow.CheckedChanged += delegate { if (this.follow.Checked) { this.OnTargetChanged(); } };
            this.donors.SelectedIndexChanged += delegate { this.UpdateInfo(); };

            this.info.SetBounds(12, 494, 976, 90);
            this.info.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;

            this.change.Text = "Change sound";
            this.change.SetBounds(792, 596, 110, 32);
            this.change.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            this.change.Click += delegate { this.Apply(); };
            Button close = new Button();
            close.Text = "Close";
            close.DialogResult = DialogResult.Cancel;
            close.SetBounds(910, 596, 78, 32);
            close.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            this.CancelButton = close;

            this.targets.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom;
            this.donors.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            this.Controls.AddRange(new Control[] { left, this.targetFilter, this.targets, right, this.donorFilter, this.layout, this.cylinders, this.aspiration, this.follow, this.donors, this.info, this.change, close });
            ForzaTheme.Apply(this);
            this.FillTargets();
            this.FillDonors();
            this.UpdateInfo();
        }

        // The game's gamedb.slt of the FM4 folder set in the ini, for a partial database (a DLC pack's merge database: it has
        // its own cars but not the lists, nor the game's engines to take the sound of). Null when db is the full one.
        private static GameDatabase OpenGameDb(string gameRoot, GameDatabase db)
        {
            if (db.HasTable("List_EngineConfig") || string.IsNullOrEmpty(gameRoot))
            {
                return null;
            }
            string path = System.IO.Path.Combine(System.IO.Path.Combine(System.IO.Path.Combine(gameRoot, "Media"), "db"), "gamedb.slt");
            try
            {
                if (!System.IO.File.Exists(path))
                {
                    return null;
                }
                GameDatabase game = new GameDatabase(System.IO.File.ReadAllBytes(path));
                game.Strings = db.Strings;
                return game.HasTable("Data_Car") ? game : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void SetupList(ListView list, int x, int y, int width, int height)
        {
            list.SetBounds(x, y, width, height);
            list.View = View.Details;
            list.FullRowSelect = true;
            list.MultiSelect = false;
            list.HideSelection = false;
            list.Columns.Add("Car", 230);
            list.Columns.Add("Sound", 130);
            list.Columns.Add("Layout", 55);
            list.Columns.Add("Cyl", 40);
            list.Columns.Add("Aspiration", 110);
        }

        private static int AspirationGroup(long id)
        {
            return (id <= 1) ? 1 : (id <= 4) ? 2 : (id == 5) ? 3 : (id == 6) ? 4 : 0;
        }

        private static long Number(object value)
        {
            return (value == null || value == DBNull.Value) ? 0 : Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }

        private static string Str(object value)
        {
            return (value == null || value == DBNull.Value) ? "" : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        // Every car with its stock engine: this database's cars first, then the reference database's (the game's own,
        // when this one is a DLC pack's merge database). A car in both is the pack's.
        private void LoadCars()
        {
            Dictionary<long, string> layouts = new Dictionary<long, string>();
            Dictionary<long, long> cylinderCounts = new Dictionary<long, long>();
            Dictionary<long, string> aspirations = new Dictionary<long, string>();
            Dictionary<long, bool> seen = new Dictionary<long, bool>();
            foreach (GameDatabase source in new GameDatabase[] { this.db, this.reference })
            {
                if (source == null)
                {
                    continue;
                }
                if (source.HasTable("List_EngineConfig"))
                {
                    foreach (object[] row in source.Sql.Query("SELECT ConfigID, EngineConfig FROM List_EngineConfig").Rows)
                    {
                        layouts[Number(row[0])] = Str(row[1]);
                    }
                }
                if (source.HasTable("List_Cylinders"))
                {
                    foreach (object[] row in source.Sql.Query("SELECT CylinderID, Number FROM List_Cylinders").Rows)
                    {
                        cylinderCounts[Number(row[0])] = Number(row[1]);
                    }
                }
                if (source.HasTable("List_Aspiration"))
                {
                    foreach (object[] row in source.Sql.Query("SELECT AspirationID, Aspiration FROM List_Aspiration").Rows)
                    {
                        aspirations[Number(row[0])] = Str(row[1]);
                    }
                }
            }
            foreach (GameDatabase source in new GameDatabase[] { this.db, this.reference })
            {
                if (source == null || !source.HasTable("Data_Car") || !source.HasTable("Data_Engine") || !source.HasTable("List_UpgradeEngine"))
                {
                    continue;
                }
                SqliteDatabase.Result result = source.Sql.Query(
                    "SELECT c.Id, c.MediaName, e.EngineID, e.MediaName, e.ConfigID, e.CylinderID, e.AspirationID_Stock FROM Data_Car c "
                    + "JOIN List_UpgradeEngine u ON u.Ordinal = c.Id AND u.IsStock = 1 JOIN Data_Engine e ON e.EngineID = u.EngineID ORDER BY c.Id");
                foreach (object[] row in result.Rows)
                {
                    long id = Number(row[0]);
                    if (seen.ContainsKey(id) || Str(row[3]).Length == 0)
                    {
                        continue;
                    }
                    seen[id] = true;
                    CarRow car = new CarRow();
                    car.Id = id;
                    car.Name = source.LookupName("car", id) ?? Str(row[1]);
                    car.EngineId = Number(row[2]);
                    car.Sound = Str(row[3]);
                    long config = Number(row[4]);
                    long cylinder = Number(row[5]);
                    car.Layout = layouts.ContainsKey(config) ? layouts[config] : config.ToString(CultureInfo.InvariantCulture);
                    car.Cylinders = cylinderCounts.ContainsKey(cylinder) ? cylinderCounts[cylinder] : 0;
                    car.Aspiration = Number(row[6]);
                    car.AspirationName = aspirations.ContainsKey(car.Aspiration) ? aspirations[car.Aspiration] : car.Aspiration.ToString(CultureInfo.InvariantCulture);
                    car.Own = (source == this.db);
                    this.cars.Add(car);
                }
            }
            this.cars.Sort(delegate(CarRow a, CarRow b) { return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase); });
        }

        private void FillFilters()
        {
            this.filling = true;
            SortedDictionary<string, bool> layouts = new SortedDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            SortedDictionary<long, bool> counts = new SortedDictionary<long, bool>();
            foreach (CarRow car in this.cars)
            {
                layouts[car.Layout] = true;
                counts[car.Cylinders] = true;
            }
            this.layout.Items.Add("Any layout");
            foreach (string name in layouts.Keys)
            {
                this.layout.Items.Add(name);
            }
            this.cylinders.Items.Add("Any cyl.");
            foreach (long count in counts.Keys)
            {
                this.cylinders.Items.Add(count.ToString(CultureInfo.InvariantCulture));
            }
            this.aspiration.Items.AddRange(Groups);
            this.layout.SelectedIndex = 0;
            this.cylinders.SelectedIndex = 0;
            this.aspiration.SelectedIndex = 0;
            this.filling = false;
        }

        private static ListViewItem Item(CarRow car)
        {
            ListViewItem item = new ListViewItem(car.Name);
            item.SubItems.Add(car.Sound);
            item.SubItems.Add(car.Layout);
            item.SubItems.Add(car.Cylinders.ToString(CultureInfo.InvariantCulture));
            item.SubItems.Add(car.AspirationName);
            item.Tag = car;
            return item;
        }

        private static bool Contains(CarRow car, string text)
        {
            return text.Length == 0 || car.Name.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0 || car.Sound.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private CarRow SelectedTarget()
        {
            return (this.targets.SelectedItems.Count == 0) ? null : (CarRow) this.targets.SelectedItems[0].Tag;
        }

        private CarRow SelectedDonor()
        {
            return (this.donors.SelectedItems.Count == 0) ? null : (CarRow) this.donors.SelectedItems[0].Tag;
        }

        private void FillTargets()
        {
            CarRow keep = this.SelectedTarget();
            string text = this.targetFilter.Text.Trim();
            this.targets.BeginUpdate();
            this.targets.Items.Clear();
            foreach (CarRow car in this.cars)
            {
                if (car.Own && Contains(car, text))
                {
                    ListViewItem item = Item(car);
                    this.targets.Items.Add(item);
                    if (keep != null && keep.Id == car.Id)
                    {
                        item.Selected = true;
                    }
                }
            }
            this.targets.EndUpdate();
        }

        private void FillDonors()
        {
            CarRow keep = this.SelectedDonor();
            CarRow target = this.SelectedTarget();
            string text = this.donorFilter.Text.Trim();
            string layoutName = (this.layout.SelectedIndex > 0) ? (string) this.layout.SelectedItem : null;
            long count = (this.cylinders.SelectedIndex > 0) ? long.Parse((string) this.cylinders.SelectedItem, CultureInfo.InvariantCulture) : -1;
            int group = this.aspiration.SelectedIndex;
            this.donors.BeginUpdate();
            this.donors.Items.Clear();
            foreach (CarRow car in this.cars)
            {
                if ((target != null && car.Id == target.Id) || !Contains(car, text)
                    || (layoutName != null && !string.Equals(car.Layout, layoutName, StringComparison.OrdinalIgnoreCase))
                    || (count >= 0 && car.Cylinders != count)
                    || (group > 0 && car.Group != group))
                {
                    continue;
                }
                ListViewItem item = Item(car);
                this.donors.Items.Add(item);
                if (keep != null && keep.Id == car.Id)
                {
                    item.Selected = true;
                }
            }
            this.donors.EndUpdate();
            this.UpdateInfo();
        }

        // The filters take the engine of the selected car (Same engine as the car).
        private void OnTargetChanged()
        {
            CarRow target = this.SelectedTarget();
            if (target != null && this.follow.Checked)
            {
                this.filling = true;
                this.layout.SelectedItem = target.Layout;
                this.cylinders.SelectedItem = target.Cylinders.ToString(CultureInfo.InvariantCulture);
                this.aspiration.SelectedIndex = Math.Max(0, target.Group);
                this.filling = false;
            }
            this.FillDonors();
        }

        // A filter chosen by hand stops the following.
        private void OnFilterChosen()
        {
            if (this.filling)
            {
                return;
            }
            this.follow.Checked = false;
            this.FillDonors();
        }

        private void UpdateInfo()
        {
            CarRow target = this.SelectedTarget();
            CarRow donor = this.SelectedDonor();
            this.change.Enabled = target != null && donor != null;
            if (target == null)
            {
                this.info.Text = string.Format(CultureInfo.InvariantCulture, "{0} cars. Pick the car whose sound you want to change; the right list then shows the cars with a similar engine (cylinders, layout, aspiration).", this.cars.Count);
                return;
            }
            string text = string.Format(CultureInfo.InvariantCulture, "{0}: sound {1}, {2} {3} cyl, {4}.", target.Name, target.Sound, target.Layout, target.Cylinders, target.AspirationName);
            if (!target.Own)
            {
                text += "\nThis car's engine is in the game's own database, not in the one open here: it cannot be changed from here.";
            }
            else if (donor != null)
            {
                text += string.Format(CultureInfo.InvariantCulture, "\nIt will play the sound {0} of {1}. {2}", donor.Sound, donor.Name, string.Equals(donor.Sound, target.Sound, StringComparison.OrdinalIgnoreCase) ? "(it is the same sound already)" : "");
            }
            this.info.Text = text;
        }

        private void Apply()
        {
            CarRow target = this.SelectedTarget();
            CarRow donor = this.SelectedDonor();
            if (target == null || donor == null || this.filling)
            {
                return;
            }
            if (this.db.ReadOnly)
            {
                MessageBox.Show(this, "This view holds the game plus its installed DLC packages and is read-only. Turn the DLC button off in the toolbar first.", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!target.Own)
            {
                MessageBox.Show(this, "The engine of this car is in the game's own database. Open that database (Media\\db\\gamedb.slt) to change it.", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            List<string> users = this.db.EngineUsers(target.EngineId);
            if (users.Count > 1
                && MessageBox.Show(this, "The engine " + target.Sound + " is also the stock engine of " + (users.Count - 1) + " other car(s): " + string.Join(", ", users.GetRange(0, Math.Min(6, users.Count)).ToArray()) + (users.Count > 6 ? ", ..." : "") + ".\nTheir sound changes too. Continue?", this.Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }
            try
            {
                long rowId = this.db.RowIdOf("Data_Engine", target.EngineId);
                if (rowId < 0)
                {
                    throw new InvalidOperationException("The engine " + target.EngineId + " has no row in this database.");
                }
                Dictionary<string, object> values = new Dictionary<string, object>();
                values["MediaName"] = donor.Sound;
                this.db.UpdateColumns("Data_Engine", rowId, values, "Change sound: " + target.Name + " " + target.Sound + " -> " + donor.Sound + " (" + donor.Name + ")");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, this.Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            string previous = target.Sound;
            foreach (CarRow car in this.cars)
            {
                if (car.Own && car.EngineId == target.EngineId)
                {
                    car.Sound = donor.Sound;
                }
            }
            this.FillTargets();
            this.FillDonors();
            if (this.Changed != null)
            {
                this.Changed(this, EventArgs.Empty);
            }
            this.info.Text = target.Name + ": sound " + previous + " -> " + donor.Sound + ". Save the database afterwards (File > Save).";
        }
    }
}
