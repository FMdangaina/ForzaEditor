namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Globalization;
    using System.Windows.Forms;

    // Pick one item from a searchable list (engines, cars, tracks...), with optional extra fields.
    internal class PickDialog : Form
    {
        private readonly ListView list;
        private readonly TextBox search;
        private readonly List<ListViewItem> items = new List<ListViewItem>();

        public PickDialog(string title, string[] columns, int[] widths, List<object[]> rows, Control extra)
        {
            this.Text = title;
            this.Size = new Size(Math.Max(560, Sum(widths) + 60), 560);
            this.StartPosition = FormStartPosition.CenterParent;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.Font = ForzaTheme.Regular;

            Panel top = new Panel();
            top.Dock = DockStyle.Top;
            top.Height = 34;
            Label label = new Label();
            label.Text = "Search:";
            label.Location = new Point(10, 10);
            label.AutoSize = true;
            this.search = new TextBox();
            this.search.Location = new Point(64, 7);
            this.search.Width = 260;
            this.search.BorderStyle = BorderStyle.FixedSingle;
            this.search.TextChanged += delegate { this.Filter(); };
            this.search.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Down && this.list.Items.Count > 0)
                {
                    this.list.Focus();
                    this.list.Items[0].Selected = true;
                    e.Handled = true;
                }
            };
            top.Controls.Add(label);
            top.Controls.Add(this.search);

            this.list = new ListView();
            this.list.Dock = DockStyle.Fill;
            this.list.View = View.Details;
            this.list.FullRowSelect = true;
            this.list.MultiSelect = false;
            this.list.HideSelection = false;
            for (int i = 0; i < columns.Length; i++)
            {
                this.list.Columns.Add(columns[i], widths[i]);
            }
            foreach (object[] row in rows)
            {
                ListViewItem item = new ListViewItem(GameDatabase.FormatValue(row[0]));
                item.Tag = row[0];
                for (int i = 1; i < row.Length; i++)
                {
                    item.SubItems.Add((row[i] == null) ? "" : GameDatabase.FormatValue(row[i]));
                }
                this.items.Add(item);
            }
            this.list.DoubleClick += delegate { this.Accept(); };

            Panel bottom = new Panel();
            bottom.Dock = DockStyle.Bottom;
            bottom.Height = 44 + ((extra != null) ? extra.Height : 0);
            if (extra != null)
            {
                extra.Location = new Point(8, 6);
                bottom.Controls.Add(extra);
            }
            Button ok = new Button();
            ok.Text = "OK";
            ok.Size = new Size(84, 28);
            ok.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            ok.Location = new Point(this.ClientSize.Width - 186, bottom.Height - 36);
            ok.Click += delegate { this.Accept(); };
            Button cancel = new Button();
            cancel.Text = "Cancel";
            cancel.Size = new Size(84, 28);
            cancel.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            cancel.Location = new Point(this.ClientSize.Width - 96, bottom.Height - 36);
            cancel.DialogResult = DialogResult.Cancel;
            bottom.Controls.Add(ok);
            bottom.Controls.Add(cancel);
            this.CancelButton = cancel;

            this.Controls.Add(this.list);
            this.Controls.Add(bottom);
            this.Controls.Add(top);
            this.BackColor = ForzaTheme.Background;
            ForzaTheme.Apply(this);
            this.Filter();
        }

        private static int Sum(int[] values)
        {
            int total = 0;
            foreach (int value in values)
            {
                total += value;
            }
            return total;
        }

        public object SelectedKey { get; private set; }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            this.search.Focus();
        }

        private void Filter()
        {
            string find = this.search.Text.Trim();
            this.list.BeginUpdate();
            this.list.Items.Clear();
            List<ListViewItem> shown = new List<ListViewItem>();
            foreach (ListViewItem item in this.items)
            {
                bool match = find.Length == 0;
                foreach (ListViewItem.ListViewSubItem sub in item.SubItems)
                {
                    match = match || sub.Text.IndexOf(find, StringComparison.OrdinalIgnoreCase) >= 0;
                }
                if (match)
                {
                    shown.Add(item);
                }
            }
            this.list.Items.AddRange(shown.ToArray());
            this.list.EndUpdate();
        }

        private void Accept()
        {
            if (this.list.SelectedItems.Count != 1)
            {
                return;
            }
            this.SelectedKey = this.list.SelectedItems[0].Tag;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        // A labelled number field for the extra panel.
        public static NumericUpDown NumberField(Control parent, string caption, int x, decimal value, decimal maximum, int decimals)
        {
            Label label = new Label();
            label.Text = caption;
            label.Location = new Point(x, 4);
            label.AutoSize = true;
            NumericUpDown number = new NumericUpDown();
            number.Location = new Point(x, 22);
            number.Width = 110;
            number.Maximum = maximum;
            number.Minimum = -maximum;
            number.DecimalPlaces = decimals;
            number.Value = value;
            number.BorderStyle = BorderStyle.FixedSingle;
            parent.Controls.Add(label);
            parent.Controls.Add(number);
            return number;
        }

        public static ComboBox ListField(Control parent, string caption, int x, int width, GameDatabase.Lookup lookup, long selected)
        {
            Label label = new Label();
            label.Text = caption;
            label.Location = new Point(x, 4);
            label.AutoSize = true;
            ComboBox combo = new ComboBox();
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            combo.Location = new Point(x, 22);
            combo.Width = width;
            int index = 0;
            foreach (KeyValuePair<long, string> item in lookup.Items)
            {
                combo.Items.Add(new LookupChoice(item.Key, lookup.Describe(item.Key)));
                if (item.Key == selected)
                {
                    index = combo.Items.Count - 1;
                }
            }
            if (combo.Items.Count > 0)
            {
                combo.SelectedIndex = index;
            }
            parent.Controls.Add(label);
            parent.Controls.Add(combo);
            return combo;
        }

        public class LookupChoice
        {
            public readonly long Id;
            private readonly string text;

            public LookupChoice(long id, string text)
            {
                this.Id = id;
                this.text = text;
            }

            public override string ToString()
            {
                return this.text;
            }
        }
    }
}
