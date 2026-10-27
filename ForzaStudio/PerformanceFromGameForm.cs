namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Globalization;
    using System.Windows.Forms;

    // Tools > Car Performance From Game: a car's PI and five bars as the garage shows them (the game's live numbers) written into
    // Data_Car, so the showroom (which reads these columns) and the garage (which computes them) agree. For a ported car the
    // stored numbers are the source game's, on another scale; the game's own are read off the screen once.
    internal sealed class PerformanceFromGameForm : Form
    {
        private readonly GameDatabase db;
        private readonly ComboBox cars = new ComboBox();
        private readonly NumericUpDown pi = new NumericUpDown();
        private readonly NumericUpDown[] bars = new NumericUpDown[5];
        private readonly string[] columns = { "SpeedRating", "HandlingRating", "AccelerationRating", "LaunchRating", "BrakingRating" };
        private readonly List<long> ids = new List<long>();
        private bool filling;

        public event EventHandler Changed;

        public PerformanceFromGameForm(GameDatabase db, long selectedCar)
        {
            this.db = db;
            this.AutoScaleMode = AutoScaleMode.None;
            this.Text = "Car performance from the game";
            this.ClientSize = new Size(470, 330);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.CenterParent;

            Label hint = new Label();
            hint.SetBounds(12, 8, 446, 48);
            hint.Text = "Look at the car in the GARAGE (the game's own calculation) and type here the PI and the five bars it shows. They are written into the car, so the showroom shows the same.";
            this.Controls.Add(hint);

            this.cars.DropDownStyle = ComboBoxStyle.DropDownList;
            this.cars.MaxDropDownItems = 24;
            int select = 0;
            foreach (object[] row in db.Sql.Query("SELECT Id, MediaName FROM Data_Car ORDER BY Id DESC").Rows)
            {
                long id = Convert.ToInt64(row[0], CultureInfo.InvariantCulture);
                if (id == selectedCar)
                {
                    select = this.ids.Count;
                }
                this.ids.Add(id);
                this.cars.Items.Add(id + "  " + Convert.ToString(row[1], CultureInfo.InvariantCulture));
            }
            this.Row(0, "Car:", this.cars, 330);

            this.pi.Minimum = 100;
            this.pi.Maximum = 999;
            this.Row(1, "PI shown:", this.pi, 80);
            string[] names = { "Speed:", "Handling:", "Acceleration:", "Launch:", "Braking:" };
            for (int i = 0; i < 5; i++)
            {
                this.bars[i] = new NumericUpDown();
                this.bars[i].Minimum = 0;
                this.bars[i].Maximum = 10;
                this.bars[i].DecimalPlaces = 1;
                this.bars[i].Increment = 0.1m;
                this.Row(2 + i, names[i], this.bars[i], 80);
            }

            Label note = new Label();
            note.SetBounds(12, 262, 446, 18);
            note.Text = "The class follows the PI. Save the database afterwards (File > Save).";
            this.Controls.Add(note);

            Button ok = new Button();
            ok.Text = "Write";
            ok.SetBounds(290, 290, 80, 28);
            ok.Click += delegate { this.Write(); };
            Button cancel = new Button();
            cancel.Text = "Close";
            cancel.DialogResult = DialogResult.Cancel;
            cancel.SetBounds(378, 290, 80, 28);
            this.Controls.Add(ok);
            this.Controls.Add(cancel);
            this.CancelButton = cancel;

            this.cars.SelectedIndexChanged += delegate { this.Load(); };
            if (this.cars.Items.Count > 0)
            {
                this.cars.SelectedIndex = select;
            }
            ForzaTheme.Apply(this);
        }

        private void Row(int row, string text, Control control, int width)
        {
            Label label = new Label();
            label.Text = text;
            label.SetBounds(12, 66 + (row * 30) + 3, 100, 20);
            control.SetBounds(120, 66 + (row * 30), width, 24);
            this.Controls.Add(label);
            this.Controls.Add(control);
        }

        // The car's present values (what the showroom shows now).
        private void Load()
        {
            if (this.cars.SelectedIndex < 0)
            {
                return;
            }
            long id = this.ids[this.cars.SelectedIndex];
            SqliteDatabase.Result row = this.db.Sql.Query("SELECT PerformanceIndex, SpeedRating, HandlingRating, AccelerationRating, LaunchRating, BrakingRating FROM Data_Car WHERE Id = ?", id);
            if (row.Rows.Count == 0)
            {
                return;
            }
            this.filling = true;
            object[] r = row.Rows[0];
            this.pi.Value = Math.Max(this.pi.Minimum, Math.Min(this.pi.Maximum, CarPerformance.ShownPi(this.db.Sql, Real(r[0]))));
            for (int i = 0; i < 5; i++)
            {
                this.bars[i].Value = Math.Max(0, Math.Min(10, (decimal) Math.Round(Real(r[1 + i]), 1)));
            }
            this.filling = false;
        }

        private void Write()
        {
            if (this.cars.SelectedIndex < 0 || this.filling)
            {
                return;
            }
            long id = this.ids[this.cars.SelectedIndex];
            double stored = CarPerformance.StoredPi(this.db.Sql, (int) this.pi.Value);
            Dictionary<string, object> values = new Dictionary<string, object>();
            values["PerformanceIndex"] = stored;
            int classId = CarPerformance.ClassOf(this.db.Sql, stored);
            values["ClassID"] = (long) classId;
            for (int i = 0; i < 5; i++)
            {
                values[this.columns[i]] = (double) this.bars[i].Value;
            }
            try
            {
                long rowId = this.db.RowIdOf("Data_Car", id);
                this.db.UpdateColumns("Data_Car", rowId, values, "Performance from the game: car " + id + ", PI " + this.pi.Value);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, this.Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (this.Changed != null)
            {
                this.Changed(this, EventArgs.Empty);
            }
            MessageBox.Show(this, string.Format(CultureInfo.InvariantCulture, "Car {0}: PI {1} (stored {2:0.0000}), class {3}, bars written.", id, this.pi.Value, stored, CarPerformance.ClassLetter(classId)), this.Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static double Real(object value)
        {
            return (value == null || value == DBNull.Value) ? 0 : Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }
    }
}
