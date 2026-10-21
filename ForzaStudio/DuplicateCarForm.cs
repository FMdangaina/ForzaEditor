namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Windows.Forms;

    // Tools > Duplicate Car (ROADMAP 18.3 / 18.4): a four-step wizard around CarDuplicate.
    //   1 Car      the car to copy and the new MediaName
    //   2 Names    DisplayName, ModelShort, year, make
    //   3 Data     the main Data_Car numbers, editable (the copy keeps the source's engine, wheel and paint
    //              colours; its own engine is a later step)
    //   4 Summary  the plan (nothing written), then Create
    internal sealed class DuplicateCarForm : Form
    {
        private sealed class CarItem
        {
            public string MediaName;
            public string Text;

            public override string ToString()
            {
                return this.Text;
            }
        }

        private sealed class MakeItem
        {
            public long Id;
            public string Text;

            public override string ToString()
            {
                return this.Text;
            }
        }

        // The Data_Car numbers worth changing on a copy (the others follow from the engine and the model).
        private static readonly string[] DataColumns = { "CurbWeight", "WeightDistribution", "BaseCost", "BaseRarity", "FrontTireWidthMM", "RearTireWidthMM", "TopSpeed-mph", "Time:0-60-sec", "IsUnicorn", "IsArcade" };

        private readonly string target;
        private readonly string xeniaContent;
        private readonly Panel[] pages = new Panel[4];
        private readonly ComboBox car;
        private readonly TextBox newName;
        private readonly Label problem;
        private readonly TextBox displayName;
        private readonly TextBox modelShort;
        private readonly NumericUpDown year;
        private readonly ComboBox make;
        private readonly DataGridView grid;
        private readonly TextBox summary;
        private readonly Button back;
        private readonly Button next;
        private readonly Button cancel;
        private readonly Dictionary<string, object> original = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, object> carRow = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        private int page;
        private List<string> taken = new List<string>();
        private StringTable names;

        public string CreatedName;

        public DuplicateCarForm(string target, string xeniaContent)
        {
            this.target = target;
            this.xeniaContent = xeniaContent;
            this.AutoScaleMode = AutoScaleMode.None;
            this.Text = "Duplicate Car";
            this.ClientSize = new Size(600, 420);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.CenterParent;

            try
            {
                this.names = StringTable.Load(target, "EN");
            }
            catch (Exception)
            {
            }
            List<CarItem> cars = new List<CarItem>();
            List<MakeItem> makes = new List<MakeItem>();
            using (SqliteDatabase db = SqliteDatabase.FromBytes(File.ReadAllBytes(CarMake.DatabasePath(target))))
            {
                foreach (object[] row in db.Query("SELECT MediaName, DisplayName, ModelShort, Year FROM Data_Car ORDER BY MediaName").Rows)
                {
                    CarItem item = new CarItem();
                    item.MediaName = (string) row[0];
                    string shown = this.Lookup("Data_Car", "ModelShort", row[2] as string);
                    item.Text = item.MediaName + (string.IsNullOrEmpty(shown) ? "" : "   " + shown);
                    cars.Add(item);
                    this.taken.Add(item.MediaName);
                }
                foreach (object[] row in db.Query("SELECT ID, DisplayName FROM List_CarMake ORDER BY ID").Rows)
                {
                    MakeItem item = new MakeItem();
                    item.Id = Convert.ToInt64(row[0], CultureInfo.InvariantCulture);
                    string shown = this.Lookup("List_CarMake", "DisplayName", row[1] as string);
                    item.Text = string.IsNullOrEmpty(shown) ? "make " + item.Id : shown;
                    makes.Add(item);
                }
            }
            makes.Sort(delegate(MakeItem a, MakeItem b) { return string.Compare(a.Text, b.Text, StringComparison.OrdinalIgnoreCase); });

            for (int i = 0; i < this.pages.Length; i++)
            {
                this.pages[i] = new Panel();
                this.pages[i].SetBounds(0, 0, 600, 370);
                this.Controls.Add(this.pages[i]);
            }

            // 1 Car
            this.AddLabel(0, "The car to copy and the name of the copy. The copy gets the next free car Id (above every car of FM4 and its DLC) and its own strings.", 12, 12, 576, 36);
            this.AddLabel(0, "Car:", 12, 64, 90, 20);
            this.car = new ComboBox();
            this.car.DropDownStyle = ComboBoxStyle.DropDownList;
            this.car.SetBounds(106, 61, 480, 24);
            this.car.MaxDropDownItems = 20;
            this.car.Items.AddRange(cars.ToArray());
            this.car.SelectedIndexChanged += delegate { this.CarChanged(); };
            this.pages[0].Controls.Add(this.car);
            this.AddLabel(0, "New MediaName:", 12, 100, 94, 20);
            this.newName = new TextBox();
            this.newName.SetBounds(106, 97, 240, 24);
            this.newName.MaxLength = 40;
            this.newName.TextChanged += delegate { this.Recheck(); };
            new ToolTip().SetToolTip(this.newName, "The car's file name in the game (like ACU_NSX_97): letters, digits and underscores; the zip is Media\\cars\\<name>.zip");
            this.pages[0].Controls.Add(this.newName);
            this.problem = this.AddLabel(0, "", 12, 130, 576, 40);

            // 2 Names
            this.AddLabel(1, "The names the game shows, the same in every language (change a language later in the string tables).", 12, 12, 576, 36);
            this.AddLabel(1, "Display name:", 12, 64, 90, 20);
            this.displayName = new TextBox();
            this.displayName.SetBounds(106, 61, 300, 24);
            this.pages[1].Controls.Add(this.displayName);
            this.AddLabel(1, "Model name:", 12, 98, 90, 20);
            this.modelShort = new TextBox();
            this.modelShort.SetBounds(106, 95, 300, 24);
            this.pages[1].Controls.Add(this.modelShort);
            this.AddLabel(1, "Year:", 12, 132, 90, 20);
            this.year = new NumericUpDown();
            this.year.Minimum = 1900;
            this.year.Maximum = 2100;
            this.year.Value = 2000;
            this.year.SetBounds(106, 129, 80, 24);
            this.pages[1].Controls.Add(this.year);
            this.AddLabel(1, "Make:", 12, 166, 90, 20);
            this.make = new ComboBox();
            this.make.DropDownStyle = ComboBoxStyle.DropDownList;
            this.make.SetBounds(106, 163, 300, 24);
            this.make.MaxDropDownItems = 20;
            this.make.Items.AddRange(makes.ToArray());
            this.pages[1].Controls.Add(this.make);

            // 3 Data
            this.AddLabel(2, "Data_Car values of the copy. Change what the copy needs; leave the rest. Paint colours and upgrades are copied with the car; the engine, drivetrain and wheel stay shared with the source.", 12, 12, 576, 48);
            this.grid = new DataGridView();
            this.grid.SetBounds(12, 66, 576, 290);
            this.grid.AllowUserToAddRows = false;
            this.grid.AllowUserToDeleteRows = false;
            this.grid.RowHeadersVisible = false;
            this.grid.Columns.Add("column", "Column");
            this.grid.Columns.Add("value", "Value");
            this.grid.Columns.Add("source", "Source car");
            this.grid.Columns[0].ReadOnly = true;
            this.grid.Columns[2].ReadOnly = true;
            this.grid.Columns[0].Width = 200;
            this.grid.Columns[1].Width = 170;
            this.grid.Columns[2].Width = 180;
            this.pages[2].Controls.Add(this.grid);

            // 4 Summary
            this.AddLabel(3, "What will be done (nothing is written until Create). Every file is backed up first (bak folder).", 12, 12, 576, 20);
            this.summary = new TextBox();
            this.summary.Multiline = true;
            this.summary.ReadOnly = true;
            this.summary.ScrollBars = ScrollBars.Vertical;
            this.summary.SetBounds(12, 40, 576, 316);
            this.pages[3].Controls.Add(this.summary);

            this.back = new Button();
            this.back.Text = "< Back";
            this.back.SetBounds(336, 382, 80, 28);
            this.back.Click += delegate { this.Go(this.page - 1); };
            this.next = new Button();
            this.next.Text = "Next >";
            this.next.SetBounds(424, 382, 80, 28);
            this.next.Click += delegate { this.NextClicked(); };
            this.cancel = new Button();
            this.cancel.Text = "Cancel";
            this.cancel.DialogResult = DialogResult.Cancel;
            this.cancel.SetBounds(512, 382, 80, 28);
            this.Controls.Add(this.back);
            this.Controls.Add(this.next);
            this.Controls.Add(this.cancel);
            this.CancelButton = this.cancel;

            ForzaTheme.Apply(this);
            if (this.car.Items.Count > 0)
            {
                this.car.SelectedIndex = 0;
            }
            this.Go(0);
        }

        private string Lookup(string table, string column, string value)
        {
            return (this.names == null || value == null) ? null : this.names.Lookup(table, column, value);
        }

        private Label AddLabel(int pageIndex, string text, int x, int y, int width, int height)
        {
            Label label = new Label();
            label.Text = text;
            label.SetBounds(x, y, width, height);
            this.pages[pageIndex].Controls.Add(label);
            return label;
        }

        private CarItem SelectedCar
        {
            get { return this.car.SelectedItem as CarItem; }
        }

        // The source car's current values into the name, year, make and data pages.
        private void CarChanged()
        {
            CarItem item = this.SelectedCar;
            if (item == null)
            {
                return;
            }
            using (SqliteDatabase db = SqliteDatabase.FromBytes(File.ReadAllBytes(CarMake.DatabasePath(this.target))))
            {
                SqliteDatabase.Result row = db.Query("SELECT * FROM Data_Car WHERE MediaName = ? COLLATE NOCASE", item.MediaName);
                this.carRow.Clear();
                for (int i = 0; i < row.Columns.Length; i++)
                {
                    this.carRow[row.Columns[i]] = row.Rows[0][i];
                }
            }
            this.newName.Text = this.FreeName(item.MediaName);
            this.displayName.Text = this.Lookup("Data_Car", "DisplayName", this.carRow["DisplayName"] as string) ?? "";
            this.modelShort.Text = this.Lookup("Data_Car", "ModelShort", this.carRow["ModelShort"] as string) ?? "";
            this.year.Value = Math.Max(this.year.Minimum, Math.Min(this.year.Maximum, Convert.ToDecimal(this.carRow["Year"], CultureInfo.InvariantCulture)));
            long makeId = Convert.ToInt64(this.carRow["MakeID"], CultureInfo.InvariantCulture);
            for (int i = 0; i < this.make.Items.Count; i++)
            {
                if (((MakeItem) this.make.Items[i]).Id == makeId)
                {
                    this.make.SelectedIndex = i;
                }
            }
            this.grid.Rows.Clear();
            foreach (string column in DataColumns)
            {
                object value;
                if (this.carRow.TryGetValue(column, out value))
                {
                    this.grid.Rows.Add(column, Convert.ToString(value, CultureInfo.InvariantCulture), Convert.ToString(value, CultureInfo.InvariantCulture));
                }
            }
            this.Recheck();
        }

        private string FreeName(string source)
        {
            for (int i = 2; i < 1000; i++)
            {
                string candidate = source + "_" + i.ToString(CultureInfo.InvariantCulture);
                if (candidate.Length <= 40 && !this.taken.Exists(delegate(string t) { return string.Equals(t, candidate, StringComparison.OrdinalIgnoreCase); }))
                {
                    return candidate;
                }
            }
            return source;
        }

        private bool Recheck()
        {
            string text = this.newName.Text.Trim();
            string message = null;
            if (this.SelectedCar == null)
            {
                message = "Choose the car to copy.";
            }
            else if (!CarDuplicate.IsValidName(text))
            {
                message = "The MediaName must be 3-40 letters, digits or underscores.";
            }
            else if (this.taken.Exists(delegate(string t) { return string.Equals(t, text, StringComparison.OrdinalIgnoreCase); }))
            {
                message = text + " is already a car of this game.";
            }
            this.problem.ForeColor = ForzaTheme.Yellow;
            this.problem.Text = message ?? "";
            return message == null;
        }

        private void Go(int index)
        {
            this.page = Math.Max(0, Math.Min(this.pages.Length - 1, index));
            for (int i = 0; i < this.pages.Length; i++)
            {
                this.pages[i].Visible = i == this.page;
            }
            this.back.Enabled = this.page > 0;
            this.next.Text = (this.page == this.pages.Length - 1) ? "Create" : "Next >";
            if (this.page == this.pages.Length - 1)
            {
                this.ShowPlan();
            }
        }

        private void NextClicked()
        {
            if (this.page == 0 && !this.Recheck())
            {
                return;
            }
            if (this.page < this.pages.Length - 1)
            {
                this.Go(this.page + 1);
                return;
            }
            this.Create();
        }

        private CarDuplicate Build()
        {
            CarDuplicate duplicate = new CarDuplicate();
            duplicate.TargetRoot = this.target;
            duplicate.XeniaContent = this.xeniaContent;
            duplicate.SourceMediaName = this.SelectedCar.MediaName;
            duplicate.NewMediaName = this.newName.Text.Trim();
            duplicate.DisplayName = this.displayName.Text.Trim();
            duplicate.ModelShort = this.modelShort.Text.Trim();
            duplicate.Year = (long) this.year.Value;
            MakeItem makeItem = this.make.SelectedItem as MakeItem;
            if (makeItem != null && makeItem.Id != Convert.ToInt64(this.carRow["MakeID"], CultureInfo.InvariantCulture))
            {
                duplicate.MakeId = makeItem.Id;
            }
            foreach (DataGridViewRow row in this.grid.Rows)
            {
                string column = (string) row.Cells[0].Value;
                string value = Convert.ToString(row.Cells[1].Value, CultureInfo.InvariantCulture);
                if (value == Convert.ToString(row.Cells[2].Value, CultureInfo.InvariantCulture))
                {
                    continue;
                }
                object old = this.carRow[column];
                if (old is long)
                {
                    long whole;
                    if (!long.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out whole))
                    {
                        throw new InvalidOperationException(column + ": " + value + " is not a whole number.");
                    }
                    duplicate.Data[column] = whole;
                }
                else
                {
                    double real;
                    if (!double.TryParse(value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out real))
                    {
                        throw new InvalidOperationException(column + ": " + value + " is not a number.");
                    }
                    duplicate.Data[column] = real;
                }
            }
            return duplicate;
        }

        private void ShowPlan()
        {
            try
            {
                CarDuplicate duplicate = this.Build();
                this.summary.Text = string.Join("\r\n", duplicate.Plan().ToArray());
                this.next.Enabled = true;
            }
            catch (Exception ex)
            {
                this.summary.Text = "Cannot duplicate: " + ex.Message;
                this.next.Enabled = false;
            }
        }

        private void Create()
        {
            if (MessageBox.Show(this, "Write the copy " + this.newName.Text.Trim() + " into the game folder now? The database, the string tables, Textures.zip and zipmanifest.xml are backed up first.", this.Text, MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
            {
                return;
            }
            try
            {
                CarDuplicate duplicate = this.Build();
                duplicate.Run();
                this.CreatedName = duplicate.NewMediaName;
                this.summary.Text = duplicate.Report.ToString().Replace("\n", "\r\n");
                this.next.Enabled = false;
                this.back.Enabled = false;
                this.cancel.Text = "Close";
                MessageBox.Show(this, duplicate.NewMediaName + " created (car Id " + duplicate.NewId + ").", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, this.Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
