namespace ForzaStudio
{
    using System;
    using System.Drawing;
    using System.Globalization;
    using System.Windows.Forms;

    // "Generate curve" of the engine dialog: the engine's peak torque (N.m at rpm), its peak power (kW at rpm) and
    // its rev limiter; Torque() gives the N.m the curve has at any rpm. A smooth curve (cubic Hermite) through
    // idle, the torque peak, the power peak and the limiter, with the slope at the power peak chosen so that the
    // power really peaks there (power = torque x rpm has zero slope where torque falls by torque / rpm per rpm).
    internal sealed class CurveGenerateDialog : Form
    {
        private readonly NumericUpDown peakTorque;
        private readonly NumericUpDown torqueRpm;
        private readonly NumericUpDown torqueRpmTo;
        private readonly NumericUpDown peakPower;
        private readonly NumericUpDown powerRpm;
        private readonly NumericUpDown limiter;
        private readonly double idle;

        public double PeakTorque { get { return (double) this.peakTorque.Value; } }
        public double TorqueRpm { get { return (double) this.torqueRpm.Value; } }
        public double TorqueRpmTo { get { return (double) this.torqueRpmTo.Value; } }
        public double PeakPowerKw { get { return (double) this.peakPower.Value; } }
        public double PowerRpm { get { return (double) this.powerRpm.Value; } }
        public double Limiter { get { return (double) this.limiter.Value; } }

        public CurveGenerateDialog(double idle, double torque, double atTorque, double toTorque, double kilowatts, double atPower, double limit)
        {
            this.idle = idle;
            this.AutoScaleMode = AutoScaleMode.None;
            this.Text = "Generate the torque curve";
            this.ClientSize = new Size(420, 306);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.CenterParent;

            Label hint = new Label();
            hint.SetBounds(12, 8, 396, 52);
            hint.Text = "The engine's real figures, as you want to see them in the menu and in the game: the curve is drawn from them (idle is the one of the engine dialog). Power peaks above the torque peak, and the limiter is the redline.";
            this.Controls.Add(hint);
            this.peakTorque = this.Row(0, "Peak torque (N.m)", torque, 1, 3000, 0);
            this.torqueRpm = this.Row(1, "... from (rpm)", atTorque, 500, 20000, 0);
            this.torqueRpmTo = this.Row(2, "... to (rpm; same = one point)", toTorque, 500, 20000, 0);
            this.peakPower = this.Row(3, "Peak power (kW)", kilowatts, 1, 3000, 0);
            this.powerRpm = this.Row(4, "... at (rpm)", atPower, 500, 20000, 0);
            this.limiter = this.Row(5, "Rev limiter (rpm)", limit, 1000, 20000, 0);

            Button ok = new Button();
            ok.Text = "Generate";
            ok.SetBounds(228, 264, 90, 28);
            ok.Click += delegate { this.Accept(); };
            Button cancel = new Button();
            cancel.Text = "Cancel";
            cancel.DialogResult = DialogResult.Cancel;
            cancel.SetBounds(326, 264, 82, 28);
            this.Controls.Add(ok);
            this.Controls.Add(cancel);
            this.CancelButton = cancel;
            this.AcceptButton = ok;
            ForzaTheme.Apply(this);
        }

        private NumericUpDown Row(int index, string caption, double value, double minimum, double maximum, int places)
        {
            Label label = new Label();
            label.Text = caption;
            label.SetBounds(12, 72 + (index * 30), 200, 20);
            this.Controls.Add(label);
            NumericUpDown number = new NumericUpDown();
            number.DecimalPlaces = places;
            number.Minimum = (decimal) minimum;
            number.Maximum = (decimal) maximum;
            number.Value = Math.Max(number.Minimum, Math.Min(number.Maximum, (decimal) value));
            number.SetBounds(220, 69 + (index * 30), 110, 24);
            this.Controls.Add(number);
            return number;
        }

        private void Accept()
        {
            string problem = Check(this.idle, this.PeakTorque, this.TorqueRpm, this.TorqueRpmTo, this.PeakPowerKw, this.PowerRpm, this.Limiter);
            if (problem != null)
            {
                MessageBox.Show(this, problem, this.Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            this.DialogResult = DialogResult.OK;
        }

        // The N.m the peak power asks at its rpm: power / angular speed. The same as BlackBirdOsaka's method (Discord):
        // lb.ft = hp x 5252 / rpm, x 1.3558 for N.m, then the scalar = that / the peak torque (the curve's value at the
        // power peak, since the game's turbos only scale the existing curve).
        public static double TorqueAtPower(double kilowatts, double rpm)
        {
            return kilowatts * 1000.0 / (rpm * 2.0 * Math.PI / 60.0);
        }

        // null when the figures can be a curve, else what is wrong.
        public static string Check(double idle, double torque, double atTorque, double toTorque, double kilowatts, double atPower, double limit)
        {
            if (!(idle < atTorque))
            {
                return "The torque peak must be above the idle rpm (" + idle.ToString("0", CultureInfo.InvariantCulture) + ").";
            }
            if (!(toTorque >= atTorque))
            {
                return "The end of the torque range must not be below its start.";
            }
            if (!(atPower > toTorque))
            {
                return "The power peak must come at a higher rpm than the end of the torque peak.";
            }
            if (!(limit >= atPower))
            {
                return "The rev limiter must be at or above the power peak rpm.";
            }
            double plateauKw = torque * toTorque * 2.0 * Math.PI / 60.0 / 1000.0;
            if (!(plateauKw < kilowatts))
            {
                return string.Format(CultureInfo.InvariantCulture, "{0:0} N.m up to {1:0} rpm is already {2:0.0} kW, above the peak power of {3:0} kW: raise the peak power, or end the torque range lower.", torque, toTorque, plateauKw, kilowatts);
            }
            double atPowerTorque = TorqueAtPower(kilowatts, atPower);
            if (!(atPowerTorque < torque))
            {
                return string.Format(CultureInfo.InvariantCulture, "{0:0} kW at {1:0} rpm is {2:0.0} N.m, which is not below the peak torque ({3:0.0} N.m): raise the peak torque or lower the power.", kilowatts, atPower, atPowerTorque, torque);
            }
            return null;
        }

        // N.m at an rpm. Knots: idle (55% of the peak), the torque peak (flat), the power peak (slope -T/rpm, so power peaks
        // there) and the limiter (just under the power peak's power, so power only falls after it).
        public static double Torque(double rpm, double idle, double torque, double atTorque, double toTorque, double kilowatts, double atPower, double limit)
        {
            double t2 = TorqueAtPower(kilowatts, atPower);
            double tIdle = 0.55 * torque;
            double tLimit = (limit > atPower * 1.001) ? 0.95 * t2 * atPower / limit : 0.95 * t2;
            if (rpm <= idle)
            {
                return tIdle * (0.6 + (0.4 * Math.Max(0, rpm) / idle));
            }
            if (rpm <= atTorque)
            {
                return Hermite(rpm, idle, tIdle, (torque - tIdle) / (atTorque - idle), atTorque, torque, 0.0);
            }
            if (rpm <= toTorque)
            {
                return torque;
            }
            if (rpm <= atPower)
            {
                // From the end of the torque range to the power peak: torque = peak - (peak - T) * u^p, never above the peak. The
                // exponent makes the slope at the power peak -T / rpm (so power really peaks there); a cubic through the same
                // points overshoots the peak torque in between.
                double u = (rpm - toTorque) / (atPower - toTorque);
                double p = Math.Max(1.0, t2 * (atPower - toTorque) / (atPower * (torque - t2)));
                return torque - ((torque - t2) * Math.Pow(Math.Max(0, u), p));
            }
            if (limit <= atPower)
            {
                return t2;
            }
            double chord = (tLimit - t2) / (limit - atPower);
            if (rpm > limit)
            {
                return Math.Max(0, tLimit + (chord * (rpm - limit)));       // the margin past the limiter goes on down the same slope
            }
            return Hermite(rpm, atPower, t2, -t2 / atPower, limit, tLimit, chord);
        }

        private static double Hermite(double x, double x0, double y0, double m0, double x1, double y1, double m1)
        {
            double h = x1 - x0;
            double t = (x - x0) / h;
            double t2 = t * t, t3 = t2 * t;
            return ((2 * t3) - (3 * t2) + 1) * y0 + (t3 - (2 * t2) + t) * h * m0 + ((-2 * t3) + (3 * t2)) * y1 + (t3 - t2) * h * m1;
        }
    }
}
