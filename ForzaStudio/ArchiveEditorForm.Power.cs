namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Globalization;
    using System.Windows.Forms;

    // POWER tab of a car: the engine's torque and power with the chosen level of each power part,
    // the torque factor of each level editable, new levels, and the torque curve reshaped by dragging.
    public partial class ArchiveEditorForm
    {
        private Panel pnlPower;
        private PowerGraph powerGraph;
        private FlowLayoutPanel powerParts;
        private Label lblPowerEngine;
        private CheckBox chkEditCurve;
        private ComboBox cmbNewLevel;
        private readonly Dictionary<string, ComboBox> powerCombos = new Dictionary<string, ComboBox>();
        private readonly Dictionary<string, NumericUpDown> powerFactors = new Dictionary<string, NumericUpDown>();
        private long powerEngine = -1;
        private bool powerFilling;

        private class LevelChoice
        {
            public long RowId = -1;     // -1: not fitted
            public long Level;
            public string Text;

            public override string ToString()
            {
                return this.Text;
            }
        }

        private Control BuildPowerTab()
        {
            this.pnlPower = new Panel();
            this.pnlPower.Dock = DockStyle.Fill;

            Panel top = new Panel();
            top.Dock = DockStyle.Top;
            top.Height = 36;
            this.lblPowerEngine = new Label();
            this.lblPowerEngine.Location = new Point(8, 10);
            this.lblPowerEngine.AutoSize = false;
            this.lblPowerEngine.AutoEllipsis = true;
            this.lblPowerEngine.Size = new Size(300, 18);
            this.chkEditCurve = new CheckBox();
            this.chkEditCurve.Text = "Edit curve (drag the red line)";
            this.chkEditCurve.AutoSize = true;
            this.chkEditCurve.CheckedChanged += delegate { this.powerGraph.Editable = this.chkEditCurve.Checked; this.powerGraph.Cursor = this.chkEditCurve.Checked ? Cursors.SizeNS : Cursors.Default; };
            this.cmbNewLevel = new ComboBox();
            this.cmbNewLevel.DropDownStyle = ComboBoxStyle.DropDownList;
            this.cmbNewLevel.Width = 200;
            Button create = new Button();
            create.Text = "New Level";
            create.AutoSize = true;
            create.Click += delegate { this.CreatePartLevel(); };
            top.Controls.Add(this.lblPowerEngine);
            top.Controls.Add(this.chkEditCurve);
            top.Controls.Add(this.cmbNewLevel);
            top.Controls.Add(create);
            top.Resize += delegate
            {
                create.Location = new Point(top.Width - create.Width - 8, 5);
                this.cmbNewLevel.Location = new Point(create.Left - this.cmbNewLevel.Width - 6, 7);
                this.chkEditCurve.Location = new Point(this.cmbNewLevel.Left - this.chkEditCurve.Width - 16, 9);
                this.lblPowerEngine.Width = Math.Max(60, this.chkEditCurve.Left - 16);
            };

            this.powerParts = new FlowLayoutPanel();
            this.powerParts.Dock = DockStyle.Left;
            this.powerParts.Width = 290;
            this.powerParts.FlowDirection = FlowDirection.TopDown;
            this.powerParts.WrapContents = false;
            this.powerParts.AutoScroll = true;
            this.powerParts.Padding = new Padding(6, 4, 0, 0);

            this.powerGraph = new PowerGraph();
            this.powerGraph.Dock = DockStyle.Fill;
            this.powerGraph.CurveEdited += delegate { this.SaveEditedCurve(); };

            this.pnlPower.Controls.Add(this.powerGraph);
            this.pnlPower.Controls.Add(this.powerParts);
            this.pnlPower.Controls.Add(top);
            return this.pnlPower;
        }

        // The selectors for every power part this engine has, set to the stock levels.
        private void ShowPower(long engine)
        {
            this.powerEngine = engine;
            this.powerFilling = true;
            this.powerParts.SuspendLayout();
            this.powerParts.Controls.Clear();
            this.powerCombos.Clear();
            this.powerFactors.Clear();
            this.cmbNewLevel.Items.Clear();
            string engineKey = engine.ToString(CultureInfo.InvariantCulture);
            object name = this.gameDb.Sql.Scalar("SELECT coalesce(EngineName, MediaName) FROM Data_Engine WHERE EngineID = ?", engine);
            List<string> users = this.gameDb.EngineUsers(engine);
            this.lblPowerEngine.Text = string.Format("{0}{1}", name, (users.Count > 1) ? string.Format("   ·   shared by {0} cars", users.Count) : "");
            this.lblPowerEngine.ForeColor = (users.Count > 1) ? ForzaTheme.Red : ForzaTheme.Text;

            foreach (EnginePower.Part part in EnginePower.Parts)
            {
                if (!this.gameDb.HasTable(part.Table))
                {
                    continue;
                }
                string factorColumn = part.Factor ?? "RedlineRPM";
                SqliteDatabase.Result rows = this.gameDb.Sql.Query("SELECT rowid, Level, IsStock, " + SqliteDatabase.Quote(factorColumn) + ", Price FROM " + part.Table + " WHERE EngineID = ? ORDER BY Level, rowid", engine);
                if (rows.Rows.Count == 0)
                {
                    continue;
                }
                Label label = new Label();
                label.Text = part.Label.ToUpperInvariant();
                label.Font = ForzaTheme.Bold;
                label.ForeColor = ForzaTheme.TextDim;
                label.AutoSize = true;
                label.Margin = new Padding(0, 6, 0, 1);
                ComboBox combo = new ComboBox();
                combo.DropDownStyle = ComboBoxStyle.DropDownList;
                combo.Width = 190;
                combo.Tag = part;
                NumericUpDown factor = new NumericUpDown();
                factor.Width = 72;
                factor.DecimalPlaces = (part.Factor == null) ? 0 : 4;
                factor.Increment = (part.Factor == null) ? 100 : 0.01m;
                factor.Maximum = 100000;
                factor.Tag = part;
                factor.BorderStyle = BorderStyle.FixedSingle;
                bool hasStock = false;
                if (part.Factor != null)
                {
                    combo.Items.Add(new LevelChoice { Text = "Not fitted" });
                }
                int selected = 0;
                foreach (object[] row in rows.Rows)
                {
                    bool stock = row[2] is long && (long) row[2] != 0;
                    LevelChoice choice = new LevelChoice();
                    choice.RowId = (long) row[0];
                    choice.Level = (row[1] is long) ? (long) row[1] : 0;
                    choice.Text = string.Format("Level {0}{1}  ·  {2}{3}", choice.Level, stock ? " (stock)" : "", (part.Factor == null) ? "redline " : "x", GameDatabase.FormatValue(row[3]));
                    combo.Items.Add(choice);
                    if (stock && !hasStock)
                    {
                        hasStock = true;
                        selected = combo.Items.Count - 1;
                    }
                }
                combo.SelectedIndex = (part.Factor == null && !hasStock) ? 0 : selected;
                combo.SelectedIndexChanged += delegate { this.PowerSelectionChanged((ComboBox) combo); };
                factor.ValueChanged += delegate { this.PowerFactorChanged(part, factor); };
                FlowLayoutPanel line = new FlowLayoutPanel();
                line.FlowDirection = FlowDirection.LeftToRight;
                line.WrapContents = false;
                line.AutoSize = true;
                line.Margin = Padding.Empty;
                line.Controls.Add(combo);
                line.Controls.Add(factor);
                this.powerParts.Controls.Add(label);
                this.powerParts.Controls.Add(line);
                this.powerCombos[part.Table] = combo;
                this.powerFactors[part.Table] = factor;
                this.cmbNewLevel.Items.Add(new PickDialog.LookupChoice(Array.IndexOf(EnginePower.Parts, part), part.Label));
            }
            if (this.cmbNewLevel.Items.Count > 0)
            {
                this.cmbNewLevel.SelectedIndex = 0;
            }
            ForzaTheme.Apply(this.powerParts);
            ForzaTheme.Apply(this.cmbNewLevel);
            this.powerParts.ResumeLayout();
            this.powerFilling = false;
            this.UpdatePowerFactors();
            this.RecomputePower();
        }

        // Only one forced induction at a time.
        private void PowerSelectionChanged(ComboBox combo)
        {
            if (this.powerFilling)
            {
                return;
            }
            EnginePower.Part part = (EnginePower.Part) combo.Tag;
            LevelChoice choice = combo.SelectedItem as LevelChoice;
            if (part.Aspiration && choice != null && choice.RowId >= 0)
            {
                this.powerFilling = true;
                foreach (ComboBox other in this.powerCombos.Values)
                {
                    if (other != combo && ((EnginePower.Part) other.Tag).Aspiration)
                    {
                        other.SelectedIndex = 0;
                    }
                }
                this.powerFilling = false;
            }
            this.UpdatePowerFactors();
            this.RecomputePower();
        }

        private void UpdatePowerFactors()
        {
            this.powerFilling = true;
            foreach (KeyValuePair<string, ComboBox> pair in this.powerCombos)
            {
                EnginePower.Part part = (EnginePower.Part) pair.Value.Tag;
                LevelChoice choice = pair.Value.SelectedItem as LevelChoice;
                NumericUpDown factor = this.powerFactors[pair.Key];
                factor.Enabled = choice != null && choice.RowId >= 0;
                if (factor.Enabled)
                {
                    object value = this.gameDb.Sql.Scalar("SELECT " + SqliteDatabase.Quote(part.Factor ?? "RedlineRPM") + " FROM " + part.Table + " WHERE rowid = ?", choice.RowId);
                    double number = (value is double) ? (double) value : (value is long ? (long) value : 0);
                    factor.Value = (decimal) Math.Max((double) factor.Minimum, Math.Min((double) factor.Maximum, number));
                }
            }
            this.powerFilling = false;
        }

        // A level's torque factor (or the camshaft's redline) written as it is changed.
        private void PowerFactorChanged(EnginePower.Part part, NumericUpDown factor)
        {
            if (this.powerFilling)
            {
                return;
            }
            LevelChoice choice = this.powerCombos[part.Table].SelectedItem as LevelChoice;
            if (choice == null || choice.RowId < 0)
            {
                return;
            }
            this.gameDb.Update(part.Table, choice.RowId, part.Factor ?? "RedlineRPM", (double) factor.Value);
            this.OnGameDbEdited(null, EventArgs.Empty);
            this.RecomputePower();
        }

        private Dictionary<string, long> PowerChoices()
        {
            Dictionary<string, long> chosen = new Dictionary<string, long>();
            foreach (KeyValuePair<string, ComboBox> pair in this.powerCombos)
            {
                LevelChoice choice = pair.Value.SelectedItem as LevelChoice;
                if (choice != null && choice.RowId >= 0)
                {
                    chosen[pair.Key] = choice.RowId;
                }
            }
            return chosen;
        }

        private void RecomputePower()
        {
            this.powerGraph.Power = EnginePower.Compute(this.gameDb, this.PowerChoices());
        }

        private void SaveEditedCurve()
        {
            double[] curve = this.powerGraph.EditedCurve;
            EnginePower power = this.powerGraph.Power;
            if (curve == null || power.CurveId < 0)
            {
                return;
            }
            long rowId = this.gameDb.RowIdOf("List_TorqueCurve", power.CurveId);
            Dictionary<string, object> values = new Dictionary<string, object>();
            for (int i = 0; i < curve.Length; i++)
            {
                values["v" + i.ToString(CultureInfo.InvariantCulture)] = Math.Round(curve[i], 7);
            }
            long shared = this.gameDb.Count("List_UpgradeEngineCamshaft", "TorqueCurveFullThrottleID = " + power.CurveId.ToString(CultureInfo.InvariantCulture));
            this.gameDb.UpdateColumns("List_TorqueCurve", rowId, values, "torque curve " + power.CurveId + " reshaped");
            this.OnGameDbEdited(null, EventArgs.Empty);
            this.RecomputePower();
            if (shared > 1)
            {
                this.SetStatus(string.Format("Torque curve {0} reshaped - it is used by {1} camshaft levels.", power.CurveId, shared));
            }
        }

        // A new level after the highest one: Id = EngineID * 1000 + Level like the game's, a camshaft
        // also gets its own copy of the torque curve so it can be reshaped alone.
        private void CreatePartLevel()
        {
            PickDialog.LookupChoice picked = this.cmbNewLevel.SelectedItem as PickDialog.LookupChoice;
            if (picked == null || this.powerEngine < 0)
            {
                return;
            }
            EnginePower.Part part = EnginePower.Parts[picked.Id];
            string engineKey = this.powerEngine.ToString(CultureInfo.InvariantCulture);
            string where = "EngineID = " + engineKey;
            long template = this.RowIdWhere(part.Table, where, "Level DESC, rowid DESC");
            if (template < 0)
            {
                return;
            }
            long level = this.gameDb.Max(part.Table, "Level", where, 0) + 1;
            long id = (this.powerEngine * 1000) + level;
            if (this.gameDb.Count(part.Table, "Id = " + id.ToString(CultureInfo.InvariantCulture)) > 0)
            {
                id = this.gameDb.Max(part.Table, "Id", null, 0) + 1;
            }
            object price = this.gameDb.Sql.Scalar("SELECT Price FROM " + part.Table + " WHERE rowid = ?", template);
            long newPrice = (price is long && (long) price > 0) ? (long) Math.Round((long) price * 1.4 / 100) * 100 : 1000;
            Dictionary<string, object> values = new Dictionary<string, object>();
            values["Id"] = id;
            values["Level"] = level;
            values["IsStock"] = 0L;
            values["Price"] = newPrice;
            if (part.Factor != null && part.Factor != "RedlineRPMScale")
            {
                object factor = this.gameDb.Sql.Scalar("SELECT " + SqliteDatabase.Quote(part.Factor) + " FROM " + part.Table + " WHERE rowid = ?", template);
                double number = (factor is double) ? (double) factor : (factor is long ? (long) factor : 1);
                values[part.Factor] = Math.Round(((number == 0) ? 1 : number) * 1.05, 4);
            }
            if (part.Factor == null)
            {
                object curve = this.gameDb.Sql.Scalar("SELECT TorqueCurveFullThrottleID FROM " + part.Table + " WHERE rowid = ?", template);
                long curveRow = (curve is long) ? this.gameDb.RowIdOf("List_TorqueCurve", (long) curve) : -1;
                if (curveRow >= 0)
                {
                    long newCurve = this.gameDb.Max("List_TorqueCurve", "TorqueCurveID", null, 0) + 1;
                    Dictionary<string, object> curveValues = new Dictionary<string, object>();
                    curveValues["TorqueCurveID"] = newCurve;
                    this.gameDb.CopyRow("List_TorqueCurve", curveRow, curveValues, "torque curve " + newCurve + " (copy of " + curve + ")");
                    values["TorqueCurveFullThrottleID"] = newCurve;
                }
            }
            this.gameDb.CopyRow(part.Table, template, values, part.Label + " level " + level + " for engine " + this.powerEngine);
            this.OnGameDbEdited(null, EventArgs.Empty);
            this.ShowPower(this.powerEngine);
            ComboBox combo;
            if (this.powerCombos.TryGetValue(part.Table, out combo))
            {
                combo.SelectedIndex = combo.Items.Count - 1;
            }
            if (part.Factor == null)
            {
                this.chkEditCurve.Checked = true;
            }
            this.SetStatus(string.Format("{0}: level {1} created (Id {2}, {3} credits). Tune it here or in the Upgrades tab.", part.Label, level, id, newPrice));
        }
    }
}
