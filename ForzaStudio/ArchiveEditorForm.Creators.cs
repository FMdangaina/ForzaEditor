namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Globalization;
    using System.Windows.Forms;

    // Track and event headers, and the tools that create game content: engine swaps, events (a full
    // copy of an existing one), races and AI competitors. New rows copy a real game row and only
    // change what makes them new, so every other value stays one the game knows.
    public partial class ArchiveEditorForm
    {
        private Panel trackHeader;
        private PictureBox picTrackLogo;
        private PictureBox picTrackRibbon;
        private Label lblTrackInfo;
        private Panel eventHeader;
        private Label lblEventInfo;
        private long dbTrackId = -1;
        private long dbEventId = -1;

        private void BuildTrackAndEventHeaders()
        {
            this.trackHeader = new Panel();
            this.trackHeader.Height = 86;
            this.picTrackLogo = HeaderPicture(4, 156);
            this.picTrackRibbon = HeaderPicture(166, 156);
            this.lblTrackInfo = new Label();
            this.lblTrackInfo.Location = new Point(330, 8);
            this.lblTrackInfo.Size = new Size(300, 70);
            this.lblTrackInfo.AutoEllipsis = true;
            this.trackHeader.Controls.Add(this.picTrackLogo);
            this.trackHeader.Controls.Add(this.picTrackRibbon);
            this.trackHeader.Controls.Add(this.lblTrackInfo);
            this.trackHeader.Resize += delegate { this.lblTrackInfo.Width = Math.Max(100, this.trackHeader.Width - this.lblTrackInfo.Left - 6); };

            this.eventHeader = new Panel();
            this.eventHeader.Height = 70;
            this.lblEventInfo = new Label();
            this.lblEventInfo.Location = new Point(8, 6);
            this.lblEventInfo.Size = new Size(600, 22);
            this.lblEventInfo.Font = ForzaTheme.Heading;
            this.lblEventInfo.AutoEllipsis = true;
            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Location = new Point(4, 32);
            buttons.Size = new Size(600, 34);
            buttons.Controls.Add(HeaderButton("New Event (copy of this)", delegate { this.CopyEvent(); }));
            buttons.Controls.Add(HeaderButton("Add Race...", delegate { this.AddRace(); }));
            buttons.Controls.Add(HeaderButton("Add Competitor...", delegate { this.AddCompetitor(); }));
            this.eventHeader.Controls.Add(this.lblEventInfo);
            this.eventHeader.Controls.Add(buttons);
            this.eventHeader.Resize += delegate { this.lblEventInfo.Width = Math.Max(100, this.eventHeader.Width - 16); };
        }

        private static PictureBox HeaderPicture(int x, int width)
        {
            PictureBox picture = new PictureBox();
            picture.Location = new Point(x, 4);
            picture.Size = new Size(width, 78);
            picture.SizeMode = PictureBoxSizeMode.Zoom;
            picture.BorderStyle = BorderStyle.FixedSingle;
            return picture;
        }

        private static Button HeaderButton(string text, EventHandler click)
        {
            Button button = new Button();
            button.Text = text;
            button.AutoSize = true;
            button.Click += click;
            return button;
        }

        private void ShowTrack(long id)
        {
            this.dbTrackId = id;
            this.pgTrack.SelectedObject = this.Record("Tracks", id);
            this.relTrack.ShowItems(this.gameDb, this.gameDb.TrackRelated(id));
            SqliteDatabase.Result result = this.gameDb.Sql.Query("SELECT Logo, MiniRibbonPath, ShortDisplayName, DisplayName, MediaName, Length, IsReverse FROM Tracks WHERE id = ?", id);
            object[] row = (result.Rows.Count > 0) ? result.Rows[0] : new object[7];
            SetPicture(this.picTrackLogo, this.LoadGameImage(row[0] as string));
            SetPicture(this.picTrackRibbon, this.LoadGameImage(row[1] as string));
            string name = this.gameDb.Text("Tracks", "ShortDisplayName", row[2]) ?? GameDatabase.FormatValue(row[4]);
            string layout = this.gameDb.Text("Tracks", "DisplayName", row[3]) ?? "";
            this.lblTrackInfo.Text = string.Format("{0}\n{1}\n{2} m{3}  ·  {4}", name, layout, GameDatabase.FormatValue(row[5]), (row[6] is long && (long) row[6] != 0) ? ", reverse" : "", GameDatabase.FormatValue(row[4]));
            this.ShowDbContent(this.tabTrack);
        }

        private void ShowEvent(long id)
        {
            this.dbEventId = id;
            this.pgEvent.SelectedObject = this.Record("Events", id);
            this.relEvent.ShowItems(this.gameDb, this.gameDb.EventRelated(id));
            object name = this.gameDb.Sql.Scalar("SELECT Name FROM Events WHERE Id = ?", id);
            long races = this.gameDb.Count("Races", "EventId = " + id.ToString(CultureInfo.InvariantCulture));
            long cars = this.gameDb.Count("EventParticipants", "EventID = " + id.ToString(CultureInfo.InvariantCulture));
            this.lblEventInfo.Text = string.Format("{0}   ·   {1} races, {2} AI competitors", this.gameDb.Text("Events", "Name", name) ?? ("Event " + id), races, cars);
            this.ShowDbContent(this.tabEvent);
        }

        private void SelectDbItem(DbSection section, long id)
        {
            this.SelectDbSection(section);
            this.ClearDbSearch();
            string key = id.ToString(CultureInfo.InvariantCulture);
            foreach (ListViewItem item in this.lvDb.Items)
            {
                if (item.Text == key)
                {
                    item.Selected = true;
                    item.Focused = true;
                    item.EnsureVisible();
                    break;
                }
            }
        }

        private long RowIdWhere(string table, string where, string order)
        {
            object rowId = this.gameDb.Sql.Scalar("SELECT rowid FROM " + SqliteDatabase.Quote(table) + " WHERE " + where + " ORDER BY " + order + " LIMIT 1");
            return (rowId is long) ? (long) rowId : -1;
        }

        #region Engine swap

        // A new engine for the car: Id = car * 1000 + Level, the mass and weight distribution changes
        // computed the way the game's own swaps are.
        private void AddEngineSwap()
        {
            if (this.gameDb == null || this.dbCarId < 0 || !this.gameDb.HasTable("List_UpgradeEngine") || !this.gameDb.HasTable("Data_Engine"))
            {
                return;
            }
            long car = this.dbCarId;
            long stockRow = this.RowIdWhere("List_UpgradeEngine", "Ordinal = " + car.ToString(CultureInfo.InvariantCulture), "Level");
            if (stockRow < 0)
            {
                MessageBox.Show(this, "This car has no engine rows to start from.", "Add Engine Swap", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            List<object[]> rows = new List<object[]>();
            foreach (object[] engine in this.gameDb.Sql.Query("SELECT e.EngineID, e.EngineName, e.MediaName, e.[EngineMass-kg], (SELECT count(DISTINCT Ordinal) FROM List_UpgradeEngine u WHERE u.EngineID = e.EngineID) FROM Data_Engine e ORDER BY e.EngineName").Rows)
            {
                rows.Add(engine);
            }
            Panel extra = new Panel();
            extra.Size = new Size(520, 50);
            NumericUpDown price = PickDialog.NumberField(extra, "Price (credits)", 0, 7500, 10000000, 0);
            ComboBox maker = PickDialog.ListField(extra, "Part maker", 130, 260, this.gameDb.GetLookup("List_UpgradeEngine", "ManufacturerID"), 9);
            using (PickDialog dialog = new PickDialog("Add Engine Swap - " + this.CarTitle(car), new string[] { "Id", "Engine", "Media name", "Mass (kg)", "Cars" }, new int[] { 50, 300, 150, 70, 45 }, rows, extra))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                long engine = Convert.ToInt64(dialog.SelectedKey);
                foreach (long[] existing in this.gameDb.CarEngineIds(car))
                {
                    if (existing[0] == engine)
                    {
                        MessageBox.Show(this, "This car already has that engine.", "Add Engine Swap", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                }
                string where = "Ordinal = " + car.ToString(CultureInfo.InvariantCulture);
                long level = this.gameDb.Max("List_UpgradeEngine", "Level", where, 0) + 1;
                long id = (car * 1000) + level;
                if (this.gameDb.Count("List_UpgradeEngine", "Id = " + id.ToString(CultureInfo.InvariantCulture)) > 0)
                {
                    id = this.gameDb.NextId("List_UpgradeEngine", "Id");
                }
                object stockEngine = this.gameDb.Sql.Scalar("SELECT EngineID FROM List_UpgradeEngine WHERE rowid = ?", stockRow);
                double stockMass = ToDouble(this.gameDb.Sql.Scalar("SELECT [EngineMass-kg] FROM Data_Engine WHERE EngineID = ?", stockEngine));
                double newMass = ToDouble(this.gameDb.Sql.Scalar("SELECT [EngineMass-kg] FROM Data_Engine WHERE EngineID = ?", engine));
                double curb = ToDouble(this.gameDb.Sql.Scalar("SELECT CurbWeight FROM Data_Car WHERE Id = ?", car));
                object placement = this.gameDb.Sql.Scalar("SELECT EnginePlacementID FROM Data_Car WHERE Id = ?", car);
                double massDiff = Math.Round(newMass - stockMass, 3);
                double side = (placement is long && (long) placement == 1) ? 0.3 : -0.3;
                double weightDiff = (curb > 0) ? massDiff * side / (curb * 100) : 0;

                Dictionary<string, object> values = new Dictionary<string, object>();
                values["Id"] = id;
                values["Level"] = level;
                values["EngineID"] = engine;
                values["IsStock"] = 0L;
                values["ManufacturerID"] = (maker.SelectedItem is PickDialog.LookupChoice) ? ((PickDialog.LookupChoice) maker.SelectedItem).Id : 0L;
                values["Price"] = (long) price.Value;
                values["MassDiff"] = massDiff;
                values["WeightDistDiff"] = weightDiff;
                values["DragScale"] = 1.0;
                values["WindInstabilityScale"] = 1.0;
                this.gameDb.CopyRow("List_UpgradeEngine", stockRow, values, "engine swap " + engine + " for car " + car);
                this.OnGameDbEdited(null, EventArgs.Empty);
                this.ShowCar(car);
                foreach (object item in this.cmbEngine.Items)
                {
                    if (item is EngineChoice && ((EngineChoice) item).Id == engine)
                    {
                        this.cmbEngine.SelectedItem = item;
                    }
                }
                this.SetStatus(string.Format("Engine swap added: level {0}, mass {1:+0.#;-0.#;0} kg. Its upgrade parts come with the engine.", level, massDiff));
            }
        }

        private static double ToDouble(object value)
        {
            return (value is double) ? (double) value : (value is long ? (long) value : 0);
        }

        private string CarTitle(long car)
        {
            SqliteDatabase.Result result = this.gameDb.Sql.Query("SELECT ModelShort, MediaName FROM Data_Car WHERE Id = ?", car);
            return (result.Rows.Count == 0) ? car.ToString(CultureInfo.InvariantCulture) : (this.gameDb.Text("Data_Car", "ModelShort", result.Rows[0][0]) ?? GameDatabase.FormatValue(result.Rows[0][1]));
        }

        #endregion

        #region Events

        // A complete copy of the event: its races (and special targets), AI competitors and restrictions.
        private void CopyEvent()
        {
            if (this.gameDb == null || this.dbEventId < 0)
            {
                return;
            }
            long source = this.dbEventId;
            string sourceKey = source.ToString(CultureInfo.InvariantCulture);
            long eventRow = this.gameDb.RowIdOf("Events", source);
            long newEvent = this.gameDb.NextId("Events", "Id");
            Dictionary<string, object> values = new Dictionary<string, object>();
            values["Id"] = newEvent;
            this.gameDb.CopyRow("Events", eventRow, values, "new event " + newEvent + " (copy of " + source + ")");

            if (this.gameDb.HasTable("Races"))
            {
                foreach (object[] race in this.gameDb.Sql.Query("SELECT rowid, Id FROM Races WHERE EventId = ? ORDER BY RaceNumber", source).Rows)
                {
                    long newRace = this.gameDb.NextId("Races", "Id");
                    Dictionary<string, object> raceValues = new Dictionary<string, object>();
                    raceValues["Id"] = newRace;
                    raceValues["EventId"] = newEvent;
                    this.gameDb.CopyRow("Races", (long) race[0], raceValues, "race " + newRace + " of event " + newEvent);
                    if (this.gameDb.HasTable("RacesSpecial"))
                    {
                        long special = this.RowIdWhere("RacesSpecial", "RaceId = " + Convert.ToInt64(race[1]).ToString(CultureInfo.InvariantCulture), "rowid");
                        if (special >= 0)
                        {
                            Dictionary<string, object> specialValues = new Dictionary<string, object>();
                            specialValues["RaceId"] = newRace;
                            this.gameDb.CopyRow("RacesSpecial", special, specialValues, "race target " + newRace);
                        }
                    }
                }
            }
            foreach (string table in new string[] { "EventParticipants", "EventRestrictions" })
            {
                if (!this.gameDb.HasTable(table))
                {
                    continue;
                }
                foreach (object[] row in this.gameDb.Sql.Query("SELECT rowid FROM " + table + " WHERE EventID = " + sourceKey).Rows)
                {
                    Dictionary<string, object> rowValues = new Dictionary<string, object>();
                    rowValues["ID"] = this.gameDb.NextId(table, "ID");
                    rowValues["EventID"] = newEvent;
                    this.gameDb.CopyRow(table, (long) row[0], rowValues, table + " for event " + newEvent);
                }
            }
            this.OnGameDbEdited(null, EventArgs.Empty);
            this.SelectDbItem(DbSection.Events, newEvent);
            this.SetStatus(string.Format("Event {0} created as a copy of {1}. Its name and description still use the original's text ids.", newEvent, source));
        }

        private void AddRace()
        {
            if (this.gameDb == null || this.dbEventId < 0 || !this.gameDb.HasTable("Races") || !this.gameDb.HasTable("Tracks"))
            {
                return;
            }
            List<object[]> rows = new List<object[]>();
            foreach (object[] track in this.gameDb.Sql.Query("SELECT id, ShortDisplayName, DisplayName, MediaName, Length FROM Tracks ORDER BY MediaName, id").Rows)
            {
                rows.Add(new object[] { track[0], this.gameDb.Text("Tracks", "ShortDisplayName", track[1]) ?? GameDatabase.FormatValue(track[3]), this.gameDb.Text("Tracks", "DisplayName", track[2]) ?? "", track[3], track[4] });
            }
            Panel extra = new Panel();
            extra.Size = new Size(300, 50);
            NumericUpDown laps = PickDialog.NumberField(extra, "Laps", 0, 3, 200, 0);
            using (PickDialog dialog = new PickDialog("Add Race", new string[] { "Id", "Track", "Layout", "Media name", "Length (m)" }, new int[] { 45, 170, 170, 100, 70 }, rows, extra))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                string where = "EventId = " + this.dbEventId.ToString(CultureInfo.InvariantCulture);
                long template = this.RowIdWhere("Races", where, "RaceNumber DESC");
                if (template < 0)
                {
                    template = this.RowIdWhere("Races", "1 = 1", "rowid");
                }
                Dictionary<string, object> values = new Dictionary<string, object>();
                values["Id"] = this.gameDb.NextId("Races", "Id");
                values["EventId"] = this.dbEventId;
                values["RaceNumber"] = this.gameDb.Max("Races", "RaceNumber", where, 0) + 1;
                values["TrackId"] = Convert.ToInt64(dialog.SelectedKey);
                values["NumLaps"] = (long) laps.Value;
                this.gameDb.CopyRow("Races", template, values, "race for event " + this.dbEventId);
                this.gameDb.Update("Events", this.gameDb.RowIdOf("Events", this.dbEventId), "NumberOfRaces", this.gameDb.Count("Races", where));
                this.OnGameDbEdited(null, EventArgs.Empty);
                this.ShowEvent(this.dbEventId);
            }
        }

        // An AI car in the event: a new EventParticipants row after the last one.
        private void AddCompetitor()
        {
            if (this.gameDb == null || this.dbEventId < 0 || !this.gameDb.HasTable("EventParticipants") || !this.gameDb.HasTable("Data_Car"))
            {
                return;
            }
            List<object[]> rows = new List<object[]>();
            foreach (object[] car in this.gameDb.Sql.Query(CarListSql).Rows)
            {
                ListViewItem item = this.CarItem(car);
                rows.Add(new object[] { car[0], item.SubItems[1].Text, item.SubItems[2].Text, item.SubItems[3].Text, item.SubItems[4].Text, item.SubItems[5].Text });
            }
            rows.Sort(delegate(object[] a, object[] b) { return string.Compare((string) a[1] + a[2], (string) b[1] + b[2], StringComparison.OrdinalIgnoreCase); });
            using (PickDialog dialog = new PickDialog("Add Competitor", new string[] { "Id", "Make", "Model", "Media name", "Year", "Class" }, new int[] { 45, 100, 180, 160, 45, 60 }, rows, null))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                string where = "EventID = " + this.dbEventId.ToString(CultureInfo.InvariantCulture);
                long template = this.RowIdWhere("EventParticipants", where, "Priority DESC");
                if (template < 0)
                {
                    template = this.RowIdWhere("EventParticipants", "1 = 1", "rowid");
                }
                Dictionary<string, object> values = new Dictionary<string, object>();
                values["ID"] = this.gameDb.NextId("EventParticipants", "ID");
                values["EventID"] = this.dbEventId;
                values["Priority"] = this.gameDb.Max("EventParticipants", "Priority", where, 0) + 1;
                values["Ordinal"] = Convert.ToInt64(dialog.SelectedKey);
                this.gameDb.CopyRow("EventParticipants", template, values, "AI competitor for event " + this.dbEventId);
                this.OnGameDbEdited(null, EventArgs.Empty);
                this.ShowEvent(this.dbEventId);
                this.relEvent.SelectedLabel = "AI participants";
            }
        }

        #endregion
    }
}
