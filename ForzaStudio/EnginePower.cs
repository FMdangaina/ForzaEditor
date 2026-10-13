namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Drawing2D;
    using System.Globalization;
    using System.Windows.Forms;

    // FM4 engine model, as far as the data shows it. The camshaft level picks the full-throttle torque
    // curve (List_TorqueCurve: NumTorqueValues points, 0 to TorqueCurveMaxRPM, normalized values times
    // TorqueScale in N·m) and the redline; the other power parts multiply the torque (TorqueScale);
    // turbos multiply it by MaxScale at full boost, superchargers from ZeroRPMScale to RedlineRPMScale.
    // Checked on the Supra RZ: the peaks fall at the rpm of Data_Car.SimPeakTorqueAngVel/SimPeakAngVel,
    // and stock 323.9 N·m x 1.39 = 450 N·m, about 336 CV, close to the real 2JZ-GTE.
    public class EnginePower
    {
        public class Part
        {
            public string Table;
            public string Label;
            public string Factor;   // the column that scales the torque
            public bool Aspiration;

            public Part(string table, string label, string factor, bool aspiration)
            {
                this.Table = table;
                this.Label = label;
                this.Factor = factor;
                this.Aspiration = aspiration;
            }
        }

        public static readonly Part[] Parts =
        {
            new Part("List_UpgradeEngineCamshaft", "Camshaft (curve, redline)", null, false),
            new Part("List_UpgradeEngineIntake", "Intake", "TorqueScale", false),
            new Part("List_UpgradeEngineFuelSystem", "Fuel system", "TorqueScale", false),
            new Part("List_UpgradeEngineIgnition", "Ignition", "TorqueScale", false),
            new Part("List_UpgradeEngineExhaust", "Exhaust", "TorqueScale", false),
            new Part("List_UpgradeEngineValves", "Valves", "TorqueScale", false),
            new Part("List_UpgradeEnginePistonsCompression", "Pistons / compression", "TorqueScale", false),
            new Part("List_UpgradeEngineDisplacement", "Displacement", "TorqueScale", false),
            new Part("List_UpgradeEngineManifold", "Manifold", "TorqueScale", false),
            new Part("List_UpgradeEngineOilCooling", "Oil cooling", "TorqueScale", false),
            new Part("List_UpgradeEngineRestrictorPlate", "Restrictor plate", "TorqueScale", false),
            new Part("List_UpgradeEngineTurboSingle", "Single turbo", "MaxScale", true),
            new Part("List_UpgradeEngineTurboTwin", "Twin turbo", "MaxScale", true),
            new Part("List_UpgradeEngineTurboQuad", "Quad turbo", "MaxScale", true),
            new Part("List_UpgradeEngineCSC", "Centrifugal supercharger", "RedlineRPMScale", true),
            new Part("List_UpgradeEngineDSC", "Positive displacement supercharger", "RedlineRPMScale", true),
            new Part("List_UpgradeEngineIntercooler", "Intercooler", "MaxScaleScale", false),
        };

        public double[] Rpm = new double[0];
        public double[] BaseTorque = new double[0];  // the camshaft's curve
        public double[] Torque = new double[0];      // with every chosen part
        public double[] Normalized = new double[0];  // the curve's own values (what is edited)
        public double CurveScale;
        public long CurveId = -1;
        public double Redline;

        // chosen: part table -> rowid of the level (absent = not fitted).
        public static EnginePower Compute(GameDatabase db, Dictionary<string, long> chosen)
        {
            EnginePower power = new EnginePower();
            long camshaft;
            if (!chosen.TryGetValue("List_UpgradeEngineCamshaft", out camshaft) || !db.HasTable("List_TorqueCurve"))
            {
                return power;
            }
            SqliteDatabase.Result cam = db.Sql.Query("SELECT RedlineRPM, TorqueCurveMaxRPM, NumRPMEntriesArray, TorqueCurveFullThrottleID FROM List_UpgradeEngineCamshaft WHERE rowid = ?", camshaft);
            if (cam.Rows.Count == 0)
            {
                return power;
            }
            power.Redline = Number(cam.Rows[0][0]);
            double maxRpm = Number(cam.Rows[0][1]);
            power.CurveId = (long) Number(cam.Rows[0][3]);
            SqliteDatabase.Result curve = db.Sql.Query("SELECT * FROM List_TorqueCurve WHERE TorqueCurveID = ?", power.CurveId);
            if (curve.Rows.Count == 0)
            {
                return power;
            }
            object[] row = curve.Rows[0];
            power.CurveScale = Number(row[1]);
            int count = Math.Min((int) Number(row[2]), row.Length - 3);
            if (count < 2)
            {
                return power;
            }
            power.Rpm = new double[count];
            power.Normalized = new double[count];
            power.BaseTorque = new double[count];
            power.Torque = new double[count];
            for (int i = 0; i < count; i++)
            {
                power.Rpm[i] = i * maxRpm / (count - 1);
                power.Normalized[i] = Number(row[3 + i]);
                power.BaseTorque[i] = power.Normalized[i] * power.CurveScale;
            }

            double scale = 1;
            double boostAtZero = 1;
            double boostAtRedline = 1;
            double intercooler = 1;
            foreach (Part part in Parts)
            {
                long rowId;
                if (part.Factor == null || !chosen.TryGetValue(part.Table, out rowId) || !db.HasTable(part.Table))
                {
                    continue;
                }
                if (part.Table.EndsWith("CSC") || part.Table.EndsWith("DSC"))
                {
                    SqliteDatabase.Result sc = db.Sql.Query("SELECT ZeroRPMScale, RedlineRPMScale FROM " + part.Table + " WHERE rowid = ?", rowId);
                    if (sc.Rows.Count > 0)
                    {
                        boostAtZero = Factor(sc.Rows[0][0]);
                        boostAtRedline = Factor(sc.Rows[0][1]);
                    }
                    continue;
                }
                double factor = Factor(db.Sql.Scalar("SELECT " + SqliteDatabase.Quote(part.Factor) + " FROM " + part.Table + " WHERE rowid = ?", rowId));
                if (part.Aspiration)
                {
                    boostAtZero = factor;
                    boostAtRedline = factor;
                }
                else if (part.Factor == "MaxScaleScale")
                {
                    intercooler = factor;
                }
                else
                {
                    scale *= factor;
                }
            }
            for (int i = 0; i < count; i++)
            {
                double t = (power.Redline > 0) ? Math.Min(1, power.Rpm[i] / power.Redline) : 0;
                double boost = boostAtZero + ((boostAtRedline - boostAtZero) * t);
                if (boost != 1)
                {
                    boost = 1 + ((boost - 1) * intercooler);
                }
                power.Torque[i] = power.BaseTorque[i] * scale * boost;
            }
            return power;
        }

        private static double Number(object value)
        {
            return (value is double) ? (double) value : (value is long ? (long) value : 0);
        }

        private static double Factor(object value)
        {
            double number = Number(value);
            return (number == 0) ? 1 : number;
        }

        // N·m and rpm -> kW
        public static double Kilowatts(double torque, double rpm)
        {
            return torque * rpm * 2 * Math.PI / 60000;
        }

        public static double Cv(double kilowatts)
        {
            return kilowatts * 1000 / 735.5;
        }

        public static double Hp(double kilowatts)
        {
            return kilowatts * 1000 / 745.7;
        }
    }

    // Torque (Forza red) and power (white) against rpm; the camshaft's own curve dashed. With editing
    // on, dragging reshapes the curve smoothly around the point under the mouse.
    internal class PowerGraph : Control
    {
        private EnginePower power = new EnginePower();
        private int hover = -1;
        private bool dragging;
        private double[] edited;

        public bool Editable { get; set; }
        public event EventHandler CurveEdited;

        public PowerGraph()
        {
            this.DoubleBuffered = true;
            this.BackColor = ForzaTheme.Background;
            this.ForeColor = ForzaTheme.Text;
            this.Font = ForzaTheme.Regular;
        }

        public EnginePower Power
        {
            get { return this.power; }
            set
            {
                this.power = value ?? new EnginePower();
                this.edited = null;
                this.Invalidate();
            }
        }

        // The edited curve (normalized values), once a drag ends.
        public double[] EditedCurve
        {
            get { return this.edited; }
        }

        private Rectangle Plot
        {
            get { return new Rectangle(56, 58, Math.Max(10, this.Width - 112), Math.Max(10, this.Height - 90)); }
        }

        private double MaxRpm
        {
            get { return (this.power.Rpm.Length > 0) ? this.power.Rpm[this.power.Rpm.Length - 1] : 8000; }
        }

        private double MaxTorque
        {
            get
            {
                double max = 100;
                for (int i = 0; i < this.power.Torque.Length; i++)
                {
                    max = Math.Max(max, Math.Max(this.power.Torque[i], this.Base(i)));
                }
                return Nice(max * 1.1);
            }
        }

        private double MaxPower
        {
            get
            {
                double max = 50;
                for (int i = 0; i < this.power.Torque.Length; i++)
                {
                    max = Math.Max(max, EnginePower.Cv(EnginePower.Kilowatts(this.power.Torque[i], this.power.Rpm[i])));
                }
                return Nice(max * 1.1);
            }
        }

        private double Base(int i)
        {
            return (this.edited != null) ? this.edited[i] * this.power.CurveScale : this.power.BaseTorque[i];
        }

        private static double Nice(double value)
        {
            double step = Math.Pow(10, Math.Floor(Math.Log10(value)));
            foreach (double m in new double[] { 1, 2, 2.5, 5, 10 })
            {
                if (m * step >= value)
                {
                    return m * step;
                }
            }
            return value;
        }

        private float X(double rpm)
        {
            Rectangle p = this.Plot;
            return p.Left + (float) (rpm / this.MaxRpm * p.Width);
        }

        private float Y(double value, double max)
        {
            Rectangle p = this.Plot;
            return p.Bottom - (float) (value / max * p.Height);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(this.BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle plot = this.Plot;
            int count = this.power.Rpm.Length;
            if (count < 2)
            {
                TextRenderer.DrawText(g, "No torque curve for this engine.", this.Font, this.ClientRectangle, ForzaTheme.TextDim, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }
            double maxTorque = this.MaxTorque;
            double maxPower = this.MaxPower;

            // Grid, axes and the redline zone.
            using (Pen grid = new Pen(Color.FromArgb(40, 40, 44)))
            using (Brush red = new SolidBrush(Color.FromArgb(40, ForzaTheme.Red)))
            {
                if (this.power.Redline > 0 && this.power.Redline < this.MaxRpm)
                {
                    float x = this.X(this.power.Redline);
                    g.FillRectangle(red, x, plot.Top, plot.Right - x, plot.Height);
                }
                for (double rpm = 0; rpm <= this.MaxRpm + 1; rpm += 1000)
                {
                    float x = this.X(rpm);
                    g.DrawLine(grid, x, plot.Top, x, plot.Bottom);
                    TextRenderer.DrawText(g, (rpm / 1000).ToString(CultureInfo.InvariantCulture), this.Font, new Point((int) x - 6, plot.Bottom + 4), ForzaTheme.TextDim);
                }
                for (int i = 0; i <= 5; i++)
                {
                    float y = plot.Bottom - (plot.Height * i / 5f);
                    g.DrawLine(grid, plot.Left, y, plot.Right, y);
                    TextRenderer.DrawText(g, (maxTorque * i / 5).ToString("0", CultureInfo.InvariantCulture), this.Font, new Rectangle(0, (int) y - 8, plot.Left - 6, 16), ForzaTheme.Red, TextFormatFlags.Right);
                    TextRenderer.DrawText(g, (maxPower * i / 5).ToString("0", CultureInfo.InvariantCulture), this.Font, new Point(plot.Right + 6, (int) y - 8), ForzaTheme.Text);
                }
            }
            TextRenderer.DrawText(g, "rpm x1000", this.Font, new Point(plot.Right - 60, plot.Bottom + 16), ForzaTheme.TextDim);
            TextRenderer.DrawText(g, "N·m", ForzaTheme.Bold, new Point(plot.Left - 40, plot.Top - 20), ForzaTheme.Red);
            TextRenderer.DrawText(g, "CV", ForzaTheme.Bold, new Point(plot.Right + 6, plot.Top - 20), ForzaTheme.Text);

            PointF[] baseLine = new PointF[count];
            PointF[] torqueLine = new PointF[count];
            PointF[] powerLine = new PointF[count];
            int peakTorque = 0;
            int peakPower = 0;
            for (int i = 0; i < count; i++)
            {
                double torque = this.Torque(i);
                double cv = EnginePower.Cv(EnginePower.Kilowatts(torque, this.power.Rpm[i]));
                baseLine[i] = new PointF(this.X(this.power.Rpm[i]), this.Y(this.Base(i), maxTorque));
                torqueLine[i] = new PointF(this.X(this.power.Rpm[i]), this.Y(torque, maxTorque));
                powerLine[i] = new PointF(this.X(this.power.Rpm[i]), this.Y(cv, maxPower));
                bool inRange = this.power.Redline <= 0 || this.power.Rpm[i] <= this.power.Redline;
                if (inRange && torque > this.Torque(peakTorque))
                {
                    peakTorque = i;
                }
                if (inRange && cv > EnginePower.Cv(EnginePower.Kilowatts(this.Torque(peakPower), this.power.Rpm[peakPower])))
                {
                    peakPower = i;
                }
            }
            using (Pen dashed = new Pen(Color.FromArgb(150, ForzaTheme.Red), this.Editable ? 2f : 1f))
            using (Pen torquePen = new Pen(ForzaTheme.Red, 2.5f))
            using (Pen powerPen = new Pen(Color.White, 2.5f))
            {
                dashed.DashStyle = DashStyle.Dash;
                g.DrawLines(dashed, baseLine);
                g.DrawLines(torquePen, torqueLine);
                g.DrawLines(powerPen, powerLine);
            }

            double peakKw = EnginePower.Kilowatts(this.Torque(peakPower), this.power.Rpm[peakPower]);
            string torqueText = string.Format("TORQUE  {0:0} N·m @ {1:0} rpm      REDLINE  {2:0} rpm", this.Torque(peakTorque), this.power.Rpm[peakTorque], this.power.Redline);
            string powerText = string.Format("POWER  {0:0} CV  ({1:0} hp, {2:0} kW) @ {3:0} rpm", EnginePower.Cv(peakKw), EnginePower.Hp(peakKw), peakKw, this.power.Rpm[peakPower]);
            TextRenderer.DrawText(g, torqueText, ForzaTheme.Bold, new Point(plot.Left, 4), ForzaTheme.Red);
            TextRenderer.DrawText(g, powerText, ForzaTheme.Bold, new Point(plot.Left, 22), Color.White);

            if (this.hover >= 0 && this.hover < count)
            {
                float x = this.X(this.power.Rpm[this.hover]);
                using (Pen cursor = new Pen(Color.FromArgb(120, 255, 255, 255)))
                {
                    g.DrawLine(cursor, x, plot.Top, x, plot.Bottom);
                }
                double torque = this.Torque(this.hover);
                double kw = EnginePower.Kilowatts(torque, this.power.Rpm[this.hover]);
                string text = string.Format("{0:0} rpm\n{1:0} N·m\n{2:0} CV", this.power.Rpm[this.hover], torque, EnginePower.Cv(kw));
                Size size = TextRenderer.MeasureText(text, this.Font);
                Rectangle box = new Rectangle((int) x + 8, plot.Top + 8, size.Width + 10, size.Height + 6);
                if (box.Right > plot.Right)
                {
                    box.X = (int) x - box.Width - 8;
                }
                using (Brush back = new SolidBrush(Color.FromArgb(220, ForzaTheme.Raised)))
                {
                    g.FillRectangle(back, box);
                }
                TextRenderer.DrawText(g, text, this.Font, new Point(box.X + 5, box.Y + 3), ForzaTheme.Text);
            }
            using (Pen line = new Pen(ForzaTheme.Red))
            {
                g.DrawLine(line, plot.Left, plot.Bottom, plot.Right, plot.Bottom);
            }
        }

        // Torque with the parts, following the edited curve while dragging.
        private double Torque(int i)
        {
            double ratio = (this.power.BaseTorque[i] > 0) ? this.power.Torque[i] / this.power.BaseTorque[i] : 1;
            return this.Base(i) * ratio;
        }

        private int Index(int x)
        {
            Rectangle plot = this.Plot;
            int count = this.power.Rpm.Length;
            if (count < 2)
            {
                return -1;
            }
            double rpm = (double) (x - plot.Left) / plot.Width * this.MaxRpm;
            return Math.Max(0, Math.Min(count - 1, (int) Math.Round(rpm / this.MaxRpm * (count - 1))));
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            this.hover = this.Index(e.X);
            if (this.dragging)
            {
                this.Reshape(this.hover, e.Y);
            }
            this.Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            this.hover = -1;
            this.Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!this.Editable || e.Button != MouseButtons.Left || this.power.Normalized.Length < 2 || this.power.CurveScale <= 0)
            {
                return;
            }
            this.dragging = true;
            this.edited = (double[]) this.power.Normalized.Clone();
            this.Reshape(this.Index(e.X), e.Y);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!this.dragging)
            {
                return;
            }
            this.dragging = false;
            if (this.CurveEdited != null)
            {
                this.CurveEdited(this, EventArgs.Empty);
            }
        }

        // Moves the base curve at index towards the mouse, with a smooth falloff on the neighbours.
        private void Reshape(int index, int y)
        {
            if (index < 0 || this.edited == null)
            {
                return;
            }
            // The red line (with the parts) follows the mouse: the curve gets the torque without them.
            Rectangle plot = this.Plot;
            double ratio = (this.power.BaseTorque[index] > 0) ? this.power.Torque[index] / this.power.BaseTorque[index] : 1;
            double target = Math.Max(0, (plot.Bottom - y) / (double) plot.Height * this.MaxTorque) / ratio / this.power.CurveScale;
            double delta = target - this.edited[index];
            double sigma = Math.Max(1.5, this.edited.Length / 24.0);
            for (int i = 0; i < this.edited.Length; i++)
            {
                double weight = Math.Exp(-((i - index) * (i - index)) / (2 * sigma * sigma));
                this.edited[i] = Math.Max(0, this.edited[i] + (delta * weight));
            }
        }
    }
}
