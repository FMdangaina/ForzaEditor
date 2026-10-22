namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Globalization;
    using System.IO;
    using System.Windows.Forms;

    // The engine of a duplicated car (Duplicate Car, ROADMAP 2.3.2): name, sound, layout, cylinders, aspiration,
    // boost, compression, mass, displacement, idle and redline rpm, torque, and the shape of the torque curve,
    // drawn with the mouse on the power graph. Starts from the source car's stock engine; Result is the
    // EngineSpec (only what differs) that CarDuplicate hands to CarEngine.Clone.
    internal sealed class EngineDialog : Form
    {
        private sealed class Choice
        {
            public long Id;
            public string Media;
            public string Text;

            public override string ToString()
            {
                return this.Text;
            }
        }

        private readonly StringTable names;
        private readonly TextBox name;
        private readonly ComboBox sound;
        private readonly ComboBox layout;
        private readonly ComboBox cylinders;
        private readonly ComboBox aspiration;
        private readonly NumericUpDown boost;
        private readonly NumericUpDown compression;
        private readonly NumericUpDown mass;
        private readonly NumericUpDown displacement;
        private readonly NumericUpDown torque;
        private readonly NumericUpDown idle;
        private readonly NumericUpDown redline;
        private readonly CheckBox diesel;
        private readonly CheckBox carbureted;
        private readonly CheckBox rotary;
        private readonly PowerGraph graph;
        private readonly DataGridView table;
        private bool refreshing;
        private EnginePower current = new EnginePower();   // the curve the graph and the table show
        private double[] knotTorque = new double[0];       // N.m shown in the table, one per Knot rpm
        private const double Knot = 250;                   // the table's step in rpm; the curve's 96 points between are interpolated
        private readonly Dictionary<string, object> source = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        private double[] stockShape = new double[0];    // the source's stock curve, normalised
        private double[] shape = new double[0];         // the curve being designed
        private double stockScale;                      // List_TorqueCurve.TorqueScale of the stock curve
        private double stockMaxRpm;
        private double stockRedline;
        private double stockDisplacement;
        private double stockIdle;
        private bool filling;

        public EngineSpec Result;

        public EngineDialog(string target, StringTable names, long carId)
        {
            this.names = names;
            this.AutoScaleMode = AutoScaleMode.None;
            this.Text = "Engine of the copy";
            this.ClientSize = new Size(780, 690);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.CenterParent;

            List<Choice> layouts = new List<Choice>();
            List<Choice> cylinderChoices = new List<Choice>();
            List<Choice> aspirations = new List<Choice>();
            List<Choice> sounds = new List<Choice>();
            using (SqliteDatabase db = SqliteDatabase.FromBytes(File.ReadAllBytes(CarMake.DatabasePath(target))))
            {
                foreach (object[] row in db.Query("SELECT ConfigID, EngineConfig, DisplayName FROM List_EngineConfig ORDER BY ConfigID").Rows)
                {
                    layouts.Add(this.Item(row[0], this.Lookup("List_EngineConfig", "DisplayName", row[2] as string) ?? (row[1] as string), null));
                }
                foreach (object[] row in db.Query("SELECT CylinderID, Number, Description, Rotors FROM List_Cylinders ORDER BY CylinderID").Rows)
                {
                    string shown = this.Lookup("List_Cylinders", "Description", row[2] as string);
                    bool rotors = Convert.ToInt64(row[3], CultureInfo.InvariantCulture) > 0;
                    cylinderChoices.Add(this.Item(row[0], string.IsNullOrEmpty(shown) ? (Convert.ToString(row[1], CultureInfo.InvariantCulture) + (rotors ? " rotors" : " cylinders")) : shown, null));
                }
                foreach (object[] row in db.Query("SELECT AspirationID, Aspiration, DisplayName FROM List_Aspiration ORDER BY AspirationID").Rows)
                {
                    aspirations.Add(this.Item(row[0], this.Lookup("List_Aspiration", "DisplayName", row[2] as string) ?? (row[1] as string), null));
                }
                foreach (object[] row in db.Query("SELECT EngineID, MediaName, EngineName FROM Data_Engine ORDER BY EngineName").Rows)
                {
                    sounds.Add(this.Item(row[0], Convert.ToString(row[2], CultureInfo.InvariantCulture).Replace("   ", " ") + "   [" + row[1] + "]", row[1] as string));
                }
                this.ReadSource(db, carId);
            }

            Label hint = new Label();
            hint.SetBounds(12, 8, 756, 34);
            hint.Text = "A copy of the source car's stock engine with the values below. The upgrade levels follow the displacement, the redline and the torque; drag on the graph to draw the torque curve.";
            this.Controls.Add(hint);
            this.name = new TextBox();
            this.Field(0, "Name:", 0, this.name, 420);
            this.sound = new ComboBox();
            this.sound.DropDownStyle = ComboBoxStyle.DropDownList;
            this.sound.MaxDropDownItems = 24;
            Choice own = new Choice();
            own.Text = "(the source engine's sound)";
            this.sound.Items.Add(own);
            this.sound.Items.AddRange(sounds.ToArray());
            this.sound.SelectedIndex = 0;
            this.Field(1, "Sound:", 0, this.sound, 560);
            this.layout = this.Combo(2, "Layout:", 0, layouts);
            this.cylinders = this.Combo(2, "Cylinders:", 1, cylinderChoices);
            this.aspiration = this.Combo(3, "Aspiration:", 0, aspirations);
            this.boost = this.Number(3, "Turbo boost (bar):", 1, 0, 6, 2, 0.05m);
            this.compression = this.Number(4, "Compression:", 0, 4, 20, 1, 0.1m);
            this.mass = this.Number(4, "Mass (kg):", 1, 20, 1500, 0, 5m);
            this.displacement = this.Number(5, "Displacement (cc):", 0, 0, 20000, 0, 50m);
            this.torque = this.Number(5, "Torque x:", 1, 0.05m, 5, 3, 0.05m);
            this.idle = this.Number(6, "Idle (rpm):", 0, 100, 3000, 0, 50m);
            this.redline = this.Number(6, "Redline (rpm):", 1, 1000, 20000, 0, 100m);
            this.diesel = new CheckBox();
            this.diesel.Text = "Diesel";
            this.diesel.SetBounds(124, 12 + (7 * 30), 90, 22);
            this.carbureted = new CheckBox();
            this.carbureted.Text = "Carburettor";
            this.carbureted.SetBounds(224, 12 + (7 * 30), 110, 22);
            this.rotary = new CheckBox();
            this.rotary.Text = "Rotary";
            this.rotary.SetBounds(344, 12 + (7 * 30), 90, 22);
            this.Controls.Add(this.diesel);
            this.Controls.Add(this.carbureted);
            this.Controls.Add(this.rotary);

            this.graph = new PowerGraph();
            this.graph.Editable = true;
            this.graph.SetBounds(12, 262, 506, 360);
            this.graph.CurveEdited += delegate
            {
                double[] edited = this.graph.EditedCurve;
                if (edited != null && edited.Length == this.shape.Length)
                {
                    this.shape = (double[]) edited.Clone();
                }
                this.Rebuild();
            };
            this.Controls.Add(this.graph);

            // The same curve as numbers, one row per point: the torque column can be typed in (or a column pasted).
            this.table = new DataGridView();
            this.table.SetBounds(526, 262, 242, 360);
            this.table.AllowUserToAddRows = false;
            this.table.AllowUserToDeleteRows = false;
            this.table.AllowUserToResizeRows = false;
            this.table.RowHeadersVisible = false;
            this.table.Columns.Add("rpm", "rpm");
            this.table.Columns.Add("torque", "N.m");
            this.table.Columns.Add("power", "CV");
            this.table.Columns[0].ReadOnly = true;
            this.table.Columns[2].ReadOnly = true;
            foreach (DataGridViewColumn column in this.table.Columns)
            {
                column.Width = 68;
                column.SortMode = DataGridViewColumnSortMode.NotSortable;
                column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            }
            this.table.CellValueChanged += delegate(object sender, DataGridViewCellEventArgs e) { this.TableEdited(e.RowIndex, e.ColumnIndex); };
            this.table.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Control && e.KeyCode == Keys.V)
                {
                    e.SuppressKeyPress = true;
                    this.PasteTorque();
                }
            };
            this.Controls.Add(this.table);
            Button reset = new Button();
            reset.Text = "Reset curve";
            reset.SetBounds(12, 632, 110, 28);
            reset.Click += delegate
            {
                this.shape = (double[]) this.stockShape.Clone();
                this.Rebuild();
            };
            this.Controls.Add(reset);
            Label note = new Label();
            note.SetBounds(130, 628, 380, 40);
            note.Text = "Drag on the graph, or type the N.m of a row of the table (every 250 rpm; the values between are interpolated). Ctrl+V pastes two columns from Excel (rpm, N.m at any rpm) or one column of N.m from the selected row. Rows past the redline are grey. Idle is the stall rpm; the sound is the FM4 engine you pick.";
            this.Controls.Add(note);
            Button ok = new Button();
            ok.Text = "OK";
            ok.SetBounds(596, 650, 80, 28);
            ok.Click += delegate { this.Accept(); };
            Button cancel = new Button();
            cancel.Text = "Cancel";
            cancel.DialogResult = DialogResult.Cancel;
            cancel.SetBounds(688, 650, 80, 28);
            this.Controls.Add(ok);
            this.Controls.Add(cancel);
            this.CancelButton = cancel;
            this.AcceptButton = ok;

            this.Fill();
            this.torque.ValueChanged += delegate { this.Rebuild(); };
            this.redline.ValueChanged += delegate { this.Rebuild(); };
            this.displacement.ValueChanged += delegate
            {
                if (!this.filling && this.stockDisplacement > 0 && this.displacement.Value > 0)
                {
                    this.torque.Value = Math.Max(this.torque.Minimum, Math.Min(this.torque.Maximum, (decimal) (((double) this.displacement.Value) / this.stockDisplacement)));
                }
            };
            ForzaTheme.Apply(this);
        }

        private string Lookup(string table, string column, string value)
        {
            return (this.names == null || value == null) ? null : this.names.Lookup(table, column, value);
        }

        private Choice Item(object id, string text, string media)
        {
            Choice item = new Choice();
            item.Id = Convert.ToInt64(id, CultureInfo.InvariantCulture);
            item.Media = media;
            item.Text = (media == null ? item.Id + "  " : "") + text;
            return item;
        }

        private void Field(int row, string text, int column, Control control, int width)
        {
            Label label = new Label();
            label.Text = text;
            label.SetBounds(12 + (column * 380), 15 + (row * 30), 108, 20);
            control.SetBounds(124 + (column * 380), 12 + (row * 30), width, 24);
            this.Controls.Add(label);
            this.Controls.Add(control);
        }

        private ComboBox Combo(int row, string text, int column, List<Choice> items)
        {
            ComboBox combo = new ComboBox();
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            combo.Items.AddRange(items.ToArray());
            this.Field(row, text, column, combo, 230);
            return combo;
        }

        private NumericUpDown Number(int row, string text, int column, decimal min, decimal max, int decimals, decimal step)
        {
            NumericUpDown number = new NumericUpDown();
            number.Minimum = min;
            number.Maximum = max;
            number.DecimalPlaces = decimals;
            number.Increment = step;
            this.Field(row, text, column, number, 110);
            return number;
        }

        private static void Select(ComboBox combo, long id)
        {
            for (int i = 0; i < combo.Items.Count; i++)
            {
                Choice item = combo.Items[i] as Choice;
                if (item != null && item.Id == id && item.Media == null)
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
            combo.SelectedIndex = -1;
        }

        private static void Set(NumericUpDown number, double value)
        {
            number.Value = Math.Max(number.Minimum, Math.Min(number.Maximum, (decimal) value));
        }

        private static double Real(object value)
        {
            return (value == null || value is DBNull) ? 0 : Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }

        // The source car's stock engine, its stock displacement, redline and torque curve.
        private void ReadSource(SqliteDatabase db, long carId)
        {
            long engine = CarEngine.StockEngine(db, carId);
            if (engine < 0)
            {
                throw new InvalidOperationException("The car has no stock engine.");
            }
            SqliteDatabase.Result row = db.Query("SELECT * FROM Data_Engine WHERE EngineID = ?", engine);
            for (int i = 0; i < row.Columns.Length; i++)
            {
                this.source[row.Columns[i]] = row.Rows[0][i];
            }
            if (DbIds.HasTable(db, "List_UpgradeEngineDisplacement"))
            {
                this.stockDisplacement = Real(db.Scalar("SELECT Disp FROM List_UpgradeEngineDisplacement WHERE EngineID = ? AND IsStock = 1 ORDER BY Level LIMIT 1", engine));
            }
            if (DbIds.HasTable(db, "List_UpgradeEngineCamshaft"))
            {
                SqliteDatabase.Result cam = db.Query("SELECT RedlineRPM, TorqueCurveMaxRPM, StallRPM, TorqueCurveFullThrottleID FROM List_UpgradeEngineCamshaft WHERE EngineID = ? AND IsStock = 1 ORDER BY Level LIMIT 1", engine);
                if (cam.Rows.Count > 0)
                {
                    this.stockRedline = Real(cam.Rows[0][0]);
                    this.stockMaxRpm = Real(cam.Rows[0][1]);
                    this.stockIdle = Real(cam.Rows[0][2]);
                    SqliteDatabase.Result curve = db.Query("SELECT * FROM List_TorqueCurve WHERE TorqueCurveID = ?", Convert.ToInt64(cam.Rows[0][3], CultureInfo.InvariantCulture));
                    if (curve.Rows.Count > 0)
                    {
                        object[] values = curve.Rows[0];
                        this.stockScale = Real(values[1]);
                        int count = Math.Min((int) Real(values[2]), values.Length - 3);
                        this.stockShape = new double[Math.Max(0, count)];
                        for (int i = 0; i < this.stockShape.Length; i++)
                        {
                            this.stockShape[i] = Real(values[3 + i]);
                        }
                    }
                }
            }
            this.shape = (double[]) this.stockShape.Clone();
        }

        private void Fill()
        {
            this.filling = true;
            this.name.Text = Convert.ToString(this.source["EngineName"], CultureInfo.InvariantCulture);
            Select(this.layout, Convert.ToInt64(this.source["ConfigID"], CultureInfo.InvariantCulture));
            Select(this.cylinders, Convert.ToInt64(this.source["CylinderID"], CultureInfo.InvariantCulture));
            Select(this.aspiration, Convert.ToInt64(this.source["AspirationID_Stock"], CultureInfo.InvariantCulture));
            Set(this.boost, Real(this.source["StockBoost-bar"]));
            Set(this.compression, Real(this.source["Compression"]));
            Set(this.mass, Real(this.source["EngineMass-kg"]));
            Set(this.displacement, this.stockDisplacement);
            Set(this.idle, this.stockIdle);
            Set(this.redline, this.stockRedline);
            this.torque.Value = 1;
            this.diesel.Checked = Convert.ToInt64(this.source["Diesel"], CultureInfo.InvariantCulture) != 0;
            this.carbureted.Checked = Convert.ToInt64(this.source["Carbureted"], CultureInfo.InvariantCulture) != 0;
            this.rotary.Checked = Convert.ToInt64(this.source["Rotary"], CultureInfo.InvariantCulture) != 0;
            this.filling = false;
            this.Rebuild();
        }

        // The graph from the curve shape, the torque factor and the redline (the curve's rpm stretch with it).
        private void Rebuild()
        {
            if (this.filling)
            {
                return;
            }
            EnginePower power = new EnginePower();
            int count = this.shape.Length;
            if (count >= 2)
            {
                double ratio = (this.stockRedline > 0) ? (double) this.redline.Value / this.stockRedline : 1.0;
                power.CurveScale = this.stockScale * (double) this.torque.Value;
                power.Redline = (double) this.redline.Value;
                power.Rpm = new double[count];
                power.Normalized = (double[]) this.shape.Clone();
                power.BaseTorque = new double[count];
                power.Torque = new double[count];
                for (int i = 0; i < count; i++)
                {
                    power.Rpm[i] = i * this.stockMaxRpm * ratio / (count - 1);
                    power.BaseTorque[i] = this.shape[i] * power.CurveScale;
                    power.Torque[i] = power.BaseTorque[i];
                }
            }
            this.graph.Power = power;
            this.RefreshTable(power);
        }

        // The table: the curve every 250 rpm (the torque between its 96 points, interpolated).
        private void RefreshTable(EnginePower power)
        {
            this.current = power;
            this.refreshing = true;
            int count = power.Rpm.Length;
            int rows = (count < 2) ? 0 : (int) Math.Floor(power.Rpm[count - 1] / Knot) + 1;
            while (this.table.Rows.Count < rows)
            {
                this.table.Rows.Add();
            }
            while (this.table.Rows.Count > rows)
            {
                this.table.Rows.RemoveAt(this.table.Rows.Count - 1);
            }
            this.knotTorque = new double[rows];
            for (int k = 0; k < rows; k++)
            {
                double rpm = k * Knot;
                double torque = At(power, rpm);
                this.knotTorque[k] = torque;
                this.table.Rows[k].Cells[0].Value = rpm.ToString("0", CultureInfo.InvariantCulture);
                this.table.Rows[k].Cells[1].Value = torque.ToString("0.0", CultureInfo.InvariantCulture);
                this.table.Rows[k].Cells[2].Value = EnginePower.Cv(EnginePower.Kilowatts(torque, rpm)).ToString("0", CultureInfo.InvariantCulture);
                this.table.Rows[k].DefaultCellStyle.ForeColor = (power.Redline > 0 && rpm > power.Redline) ? ForzaTheme.TextDim : ForzaTheme.Text;
            }
            this.refreshing = false;
        }

        // N.m of the curve at an rpm: linear between its points.
        private static double At(EnginePower power, double rpm)
        {
            int count = power.Rpm.Length;
            double step = power.Rpm[count - 1] / (count - 1);
            int i = Math.Min(count - 2, Math.Max(0, (int) Math.Floor(rpm / step)));
            double t = (rpm - power.Rpm[i]) / step;
            return power.Torque[i] + ((power.Torque[i + 1] - power.Torque[i]) * t);
        }

        // Moves the table's torque to new values (delta = the change of every row, mostly 0): a tent of the curve's points
        // around each row, one Knot wide each way, so what lies between the rows is interpolated linearly. The tents'
        // heights are solved (each row's value is the curve's linear interpolation between its two points, so the tents
        // of the neighbours leak into it) so that every row shows exactly what was typed and the others keep theirs.
        private void ApplyDeltas(double[] delta)
        {
            double scale = this.stockScale * (double) this.torque.Value;
            int count = this.current.Rpm.Length;
            int rows = delta.Length;
            if (scale <= 0 || count < 2)
            {
                return;
            }
            double[] height = new double[rows];
            for (int pass = 0; pass < 40; pass++)
            {
                for (int j = 0; j < rows; j++)
                {
                    double sum = delta[j];
                    if (j > 0)
                    {
                        sum -= this.Seen(j, j - 1) * height[j - 1];
                    }
                    if (j + 1 < rows)
                    {
                        sum -= this.Seen(j, j + 1) * height[j + 1];
                    }
                    height[j] = sum / Math.Max(0.2, this.Seen(j, j));
                }
            }
            for (int i = 0; i < count; i++)
            {
                double add = 0;
                for (int k = 0; k < rows; k++)
                {
                    add += height[k] * Tent(this.current.Rpm[i], k * Knot);
                }
                this.shape[i] += add / scale;
            }
        }

        // The tent of row k as the curve's interpolation sees it at row j's rpm.
        private double Seen(int j, int k)
        {
            int count = this.current.Rpm.Length;
            double step = this.current.Rpm[count - 1] / (count - 1);
            double rpm = j * Knot;
            int i = Math.Min(count - 2, Math.Max(0, (int) Math.Floor(rpm / step)));
            double t = (rpm - this.current.Rpm[i]) / step;
            return (Tent(this.current.Rpm[i], k * Knot) * (1 - t)) + (Tent(this.current.Rpm[i + 1], k * Knot) * t);
        }

        private static double Tent(double rpm, double center)
        {
            return Math.Max(0, 1 - (Math.Abs(rpm - center) / Knot));
        }

        // A torque typed in the table (N.m at that rpm).
        private void TableEdited(int row, int column)
        {
            if (this.refreshing || column != 1 || row < 0 || row >= this.knotTorque.Length)
            {
                return;
            }
            double value;
            string text = Convert.ToString(this.table.Rows[row].Cells[1].Value, CultureInfo.InvariantCulture) ?? "";
            if (double.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                double[] delta = new double[this.knotTorque.Length];
                delta[row] = value - this.knotTorque[row];
                this.ApplyDeltas(delta);
            }
            this.Rebuild();
        }

        // Ctrl+V from Excel/CSV: two columns rpm + N.m at any rpm (the curve between is interpolated onto the table's rows;
        // rows outside the pasted rpm range stay), or one column of N.m from the selected row down. A header line is skipped.
        private void PasteTorque()
        {
            if (!Clipboard.ContainsText() || this.table.CurrentRow == null)
            {
                return;
            }
            List<double[]> pairs = new List<double[]>();
            List<double> column = new List<double>();
            foreach (string line in Clipboard.GetText().Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                List<double> numbers = new List<double>();
                foreach (string field in line.Split(new char[] { '\t', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    double number;
                    if (double.TryParse(field.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out number))
                    {
                        numbers.Add(number);
                    }
                }
                if (numbers.Count >= 2)
                {
                    pairs.Add(new double[] { numbers[0], numbers[1] });
                }
                else if (numbers.Count == 1)
                {
                    column.Add(numbers[0]);
                }
            }
            double[] delta = new double[this.knotTorque.Length];
            bool any = false;
            if (pairs.Count >= 2)
            {
                pairs.Sort(delegate(double[] a, double[] b) { return a[0].CompareTo(b[0]); });
                for (int k = 0; k < delta.Length; k++)
                {
                    double rpm = k * Knot;
                    if (rpm < pairs[0][0] || rpm > pairs[pairs.Count - 1][0])
                    {
                        continue;
                    }
                    int i = 0;
                    while (i + 2 < pairs.Count && pairs[i + 1][0] < rpm)
                    {
                        i++;
                    }
                    double span = pairs[i + 1][0] - pairs[i][0];
                    double t = (span > 0) ? (rpm - pairs[i][0]) / span : 0;
                    delta[k] = (pairs[i][1] + ((pairs[i + 1][1] - pairs[i][1]) * t)) - this.knotTorque[k];
                    any = true;
                }
            }
            else
            {
                int row = this.table.CurrentRow.Index;
                for (int n = 0; n < column.Count && row + n < delta.Length; n++)
                {
                    delta[row + n] = column[n] - this.knotTorque[row + n];
                    any = true;
                }
            }
            if (any)
            {
                this.ApplyDeltas(delta);
                this.Rebuild();
            }
        }

        private void Accept()
        {
            EngineSpec spec = new EngineSpec();
            string text = this.name.Text.Trim();
            if (text.Length == 0)
            {
                MessageBox.Show(this, "Give the engine a name.", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (text != Convert.ToString(this.source["EngineName"], CultureInfo.InvariantCulture))
            {
                spec.Name = text;
            }
            Choice picked = this.sound.SelectedItem as Choice;
            if (picked != null && picked.Media != null && picked.Media != Convert.ToString(this.source["MediaName"], CultureInfo.InvariantCulture))
            {
                spec.Data["MediaName"] = picked.Media;
            }
            this.Id(spec, "ConfigID", this.layout);
            this.Id(spec, "CylinderID", this.cylinders);
            this.Id(spec, "AspirationID_Stock", this.aspiration);
            this.Real(spec, "StockBoost-bar", this.boost);
            this.Real(spec, "Compression", this.compression);
            this.Real(spec, "EngineMass-kg", this.mass);
            this.Flag(spec, "Diesel", this.diesel);
            this.Flag(spec, "Carbureted", this.carbureted);
            this.Flag(spec, "Rotary", this.rotary);
            if (this.stockDisplacement > 0 && this.displacement.Value > 0 && (double) this.displacement.Value != Math.Round(this.stockDisplacement))
            {
                spec.DisplacementCc = (double) this.displacement.Value;
            }
            if (this.stockRedline > 0 && (double) this.redline.Value != Math.Round(this.stockRedline))
            {
                spec.RedlineRpm = (double) this.redline.Value;
            }
            if (this.stockIdle > 0 && (double) this.idle.Value != Math.Round(this.stockIdle))
            {
                spec.IdleRpm = (double) this.idle.Value;
            }
            spec.TorqueScale = (double) this.torque.Value;
            for (int i = 0; i < this.shape.Length; i++)
            {
                if (Math.Abs(this.shape[i] - this.stockShape[i]) > 1e-9)
                {
                    spec.Shape = (double[]) this.shape.Clone();
                    break;
                }
            }
            this.Result = spec;
            this.DialogResult = DialogResult.OK;
        }

        private void Id(EngineSpec spec, string column, ComboBox combo)
        {
            Choice item = combo.SelectedItem as Choice;
            if (item != null && item.Id != Convert.ToInt64(this.source[column], CultureInfo.InvariantCulture))
            {
                spec.Data[column] = item.Id;
            }
        }

        private void Real(EngineSpec spec, string column, NumericUpDown number)
        {
            if (Math.Abs((double) number.Value - Real(this.source[column])) > Math.Pow(10, -number.DecimalPlaces) / 2)
            {
                spec.Data[column] = (double) number.Value;
            }
        }

        private void Flag(EngineSpec spec, string column, CheckBox box)
        {
            if ((Convert.ToInt64(this.source[column], CultureInfo.InvariantCulture) != 0) != box.Checked)
            {
                spec.Data[column] = box.Checked ? 1L : 0L;
            }
        }

        // One line for the wizard page.
        public static string Describe(EngineSpec spec)
        {
            List<string> parts = new List<string>();
            if (spec.Name != null)
            {
                parts.Add("\"" + spec.Name + "\"");
            }
            object sound;
            if (spec.Data.TryGetValue("MediaName", out sound))
            {
                parts.Add("sound of " + sound);
            }
            if (spec.DisplacementCc.HasValue)
            {
                parts.Add(spec.DisplacementCc.Value.ToString("0", CultureInfo.InvariantCulture) + " cc");
            }
            if (spec.RedlineRpm.HasValue)
            {
                parts.Add("redline " + spec.RedlineRpm.Value.ToString("0", CultureInfo.InvariantCulture));
            }
            if (spec.IdleRpm.HasValue)
            {
                parts.Add("idle " + spec.IdleRpm.Value.ToString("0", CultureInfo.InvariantCulture));
            }
            if (spec.TorqueScale != 1.0)
            {
                parts.Add("torque x" + spec.TorqueScale.ToString("0.###", CultureInfo.InvariantCulture));
            }
            if (spec.Shape != null)
            {
                parts.Add("own torque curve");
            }
            parts.Add(spec.Data.Count + " other values changed");
            return string.Join(", ", parts.ToArray());
        }
    }
}
