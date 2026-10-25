namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Globalization;
    using System.Windows.Forms;

    // TRANSMISSION tab of a car (Game Database): the gearbox levels of the car's drivetrains
    // (List_UpgradeDrivetrainTransmission), one at a time, with the ratios as a plain summary above the fields.
    public partial class ArchiveEditorForm
    {
        private PropertyGrid pgTransmission;
        private ToolStripComboBox cmbTransmission;
        private ToolStripLabel lblTransmissionUsage;
        private Label lblGears;

        private Control BuildTransmissionTab()
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Fill;
            this.pgTransmission = this.NewPropertyGrid();
            this.pgTransmission.PropertyValueChanged += delegate { this.ShowGearSummary(); };
            this.lblGears = new Label();
            this.lblGears.Dock = DockStyle.Top;
            this.lblGears.Height = 74;
            this.lblGears.Padding = new Padding(8, 6, 8, 0);
            ToolStrip bar = new ToolStrip();
            bar.GripStyle = ToolStripGripStyle.Hidden;
            bar.Items.Add(new ToolStripLabel("Gearbox:"));
            this.cmbTransmission = new ToolStripComboBox();
            this.cmbTransmission.DropDownStyle = ComboBoxStyle.DropDownList;
            this.cmbTransmission.AutoSize = false;
            this.cmbTransmission.Width = 420;
            this.cmbTransmission.SelectedIndexChanged += delegate { this.ShowTransmission(); };
            bar.Items.Add(this.cmbTransmission);
            this.lblTransmissionUsage = new ToolStripLabel();
            bar.Items.Add(this.lblTransmissionUsage);
            panel.Controls.Add(this.pgTransmission);
            panel.Controls.Add(this.lblGears);
            panel.Controls.Add(bar);
            return panel;
        }

        private class TransmissionChoice
        {
            public readonly long Id;
            public readonly long Drivetrain;
            private readonly string text;

            public TransmissionChoice(long id, long drivetrain, string text)
            {
                this.Id = id;
                this.Drivetrain = drivetrain;
                this.text = text;
            }

            public override string ToString()
            {
                return this.text;
            }
        }

        // The gearbox levels of every drivetrain the car has (stock, conversions), stock drivetrain first.
        private void ShowCarTransmissions(long carId)
        {
            this.cmbTransmission.Items.Clear();
            this.pgTransmission.SelectedObject = null;
            this.lblGears.Text = "";
            this.lblTransmissionUsage.Text = "";
            if (!this.gameDb.HasTable("List_UpgradeDrivetrainTransmission") || !this.gameDb.HasTable("List_UpgradeDrivetrain"))
            {
                this.lblTransmissionUsage.Text = "This database has no gearbox table.";
                return;
            }
            SqliteDatabase.Result rows = this.gameDb.Sql.Query(
                "SELECT t.Id, t.DrivetrainID, t.Level, t.IsStock, t.NumGears, u.IsStock FROM List_UpgradeDrivetrainTransmission t JOIN (SELECT DrivetrainID, max(IsStock) IsStock FROM List_UpgradeDrivetrain WHERE Ordinal = ? GROUP BY DrivetrainID) u ON u.DrivetrainID = t.DrivetrainID ORDER BY u.IsStock DESC, t.DrivetrainID, t.Level", carId);
            foreach (object[] row in rows.Rows)
            {
                bool stockDrivetrain = Convert.ToInt64(row[5], CultureInfo.InvariantCulture) != 0;
                this.cmbTransmission.Items.Add(new TransmissionChoice(Convert.ToInt64(row[0], CultureInfo.InvariantCulture), Convert.ToInt64(row[1], CultureInfo.InvariantCulture),
                    string.Format(CultureInfo.InvariantCulture, "{0} - level {1}{2}, {3} gears{4}", row[0], row[2], (Convert.ToInt64(row[3], CultureInfo.InvariantCulture) != 0) ? " (stock)" : "", Math.Max(0, Convert.ToInt64(row[4], CultureInfo.InvariantCulture) - 1), stockDrivetrain ? "" : "  (drivetrain swap)")));
            }
            if (this.cmbTransmission.Items.Count > 0)
            {
                this.cmbTransmission.SelectedIndex = 0;
            }
            else
            {
                this.lblTransmissionUsage.Text = "No gearbox rows for this car.";
            }
        }

        private void ShowTransmission()
        {
            TransmissionChoice choice = this.cmbTransmission.SelectedItem as TransmissionChoice;
            if (choice == null || this.gameDb == null)
            {
                return;
            }
            this.pgTransmission.SelectedObject = this.Record("List_UpgradeDrivetrainTransmission", choice.Id);
            this.ShowGearSummary();
            long cars = Convert.ToInt64(this.gameDb.Sql.Scalar("SELECT count(DISTINCT Ordinal) FROM List_UpgradeDrivetrain WHERE DrivetrainID = ?", choice.Drivetrain), CultureInfo.InvariantCulture);
            if (cars <= 1)
            {
                this.lblTransmissionUsage.Text = "Used only by this car.";
                this.lblTransmissionUsage.ForeColor = ForzaTheme.Text;
            }
            else
            {
                this.lblTransmissionUsage.Text = string.Format(CultureInfo.InvariantCulture, "Shared by {0} cars - edits change all of them", cars);
                this.lblTransmissionUsage.ForeColor = ForzaTheme.Red;
            }
        }

        // The ratios in words: reverse, the forward gears with their overall ratio (gear x final drive), final drive, shift time.
        private void ShowGearSummary()
        {
            TransmissionChoice choice = this.cmbTransmission.SelectedItem as TransmissionChoice;
            if (choice == null)
            {
                this.lblGears.Text = "";
                return;
            }
            SqliteDatabase.Result r = this.gameDb.Sql.Query("SELECT FinalDriveRatio, GearShiftTime, GearRatio0, GearRatio1, GearRatio2, GearRatio3, GearRatio4, GearRatio5, GearRatio6, GearRatio7, GearRatio8 FROM List_UpgradeDrivetrainTransmission WHERE Id = ?", choice.Id);
            if (r.Rows.Count == 0)
            {
                this.lblGears.Text = "";
                return;
            }
            object[] row = r.Rows[0];
            double finalDrive = ToDouble(row[0]);
            List<string> gears = new List<string>();
            for (int i = 3; i < 11; i++)
            {
                double ratio = ToDouble(row[i]);
                if (ratio > 0)
                {
                    gears.Add(string.Format(CultureInfo.InvariantCulture, "{0}: {1:0.###} ({2:0.00})", i - 2, ratio, ratio * finalDrive));
                }
            }
            this.lblGears.Text = string.Format(CultureInfo.InvariantCulture, "{0} forward gears (gear ratio, overall with the final drive in brackets)\r\n{1}\r\nReverse {2:0.###}   Final drive {3:0.###}   Shift time {4:0.###} s",
                gears.Count, string.Join("   ", gears.ToArray()), Math.Abs(ToDouble(row[2])), finalDrive, ToDouble(row[1]));
        }

    }
}
