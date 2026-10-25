namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Globalization;
    using System.IO;
    using System.Windows.Forms;

    // Every section and key of ForzaStudio.ini as a list of sections and a grid of keys (Tools > Settings > ALL): the value
    // is edited in place, the key and the file's note beside it are read-only. [App] is read by AppIni (its values may
    // hold semicolons), the other sections by IniFile. Nothing is written before Apply.
    internal sealed class IniEditorPanel : Panel
    {
        private readonly ListBox sections = new ListBox();
        private readonly DataGridView grid = new DataGridView();
        private readonly Label comment = new Label();
        private List<IniFile.Section> layout = new List<IniFile.Section>();
        private readonly Dictionary<string, string> edits = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);   // "section|key" -> value
        private bool filling;

        public IniEditorPanel()
        {
            this.Dock = DockStyle.Fill;
            this.comment.Dock = DockStyle.Top;
            this.comment.Height = 40;
            this.comment.ForeColor = ForzaTheme.Text;
            this.sections.Dock = DockStyle.Left;
            this.sections.Width = 230;
            this.sections.IntegralHeight = false;
            this.sections.SelectedIndexChanged += delegate { this.ShowSection(); };
            Panel gap = new Panel();
            gap.Dock = DockStyle.Left;
            gap.Width = 8;
            this.grid.Dock = DockStyle.Fill;
            this.grid.AllowUserToAddRows = false;
            this.grid.AllowUserToDeleteRows = false;
            this.grid.AllowUserToResizeRows = false;
            this.grid.RowHeadersVisible = false;
            this.grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
            this.grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            this.grid.Columns.Add("Key", "Key");
            this.grid.Columns.Add("Value", "Value");
            this.grid.Columns.Add("Note", "Note");
            this.grid.Columns[0].ReadOnly = true;
            this.grid.Columns[0].Width = 190;
            this.grid.Columns[1].Width = 260;
            this.grid.Columns[2].ReadOnly = true;
            this.grid.Columns[2].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            this.grid.Columns[2].DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            this.grid.Columns[2].DefaultCellStyle.ForeColor = ForzaTheme.TextDim;
            this.grid.EditMode = DataGridViewEditMode.EditOnEnter;
            foreach (int readOnly in new int[] { 0, 2 })
            {
                DataGridViewCellStyle style = this.grid.Columns[readOnly].DefaultCellStyle;
                style.SelectionBackColor = ForzaTheme.Surface;
                style.SelectionForeColor = (readOnly == 2) ? ForzaTheme.TextDim : ForzaTheme.Text;
            }
            foreach (DataGridViewColumn column in this.grid.Columns)
            {
                column.SortMode = DataGridViewColumnSortMode.NotSortable;
            }
            this.grid.VisibleChanged += delegate
            {
                if (this.grid.Visible)
                {
                    this.grid.CurrentCell = null;
                    this.grid.ClearSelection();
                }
            };
            this.grid.CellValueChanged += delegate(object sender, DataGridViewCellEventArgs e)
            {
                if (this.filling || e.RowIndex < 0 || e.ColumnIndex != 1 || this.sections.SelectedItem == null)
                {
                    return;
                }
                DataGridViewRow row = this.grid.Rows[e.RowIndex];
                this.edits[(string) this.sections.SelectedItem + "|" + (string) row.Cells[0].Value] = Convert.ToString(row.Cells[1].Value, CultureInfo.InvariantCulture).Trim();
            };
            this.Controls.Add(this.grid);
            this.Controls.Add(gap);
            this.Controls.Add(this.sections);
            this.Controls.Add(this.comment);
            this.Reload();
        }

        // The file as it is now; edits not applied yet are dropped.
        public void Reload()
        {
            AppIni.Ensure();
            this.layout = IniFile.Layout(File.Exists(AppIni.Path) ? File.ReadAllText(AppIni.Path) : "");
            foreach (IniFile.Section section in this.layout)
            {
                if (string.Equals(section.Name, AppIni.AppSection, StringComparison.OrdinalIgnoreCase))
                {
                    section.Entries = AppIni.AppEntries();
                    section.Comment = "Folders, music, viewer and the last places used; the port window's memory (CarPort.*).";
                }
            }
            this.edits.Clear();
            string selected = this.sections.SelectedItem as string;
            this.sections.Items.Clear();
            foreach (IniFile.Section section in this.layout)
            {
                this.sections.Items.Add(section.Name);
            }
            if (this.sections.Items.Count > 0)
            {
                int index = (selected != null) ? this.sections.Items.IndexOf(selected) : 0;
                this.sections.SelectedIndex = Math.Max(0, index);
            }
        }

        private void ShowSection()
        {
            this.grid.EndEdit();
            this.filling = true;
            this.grid.Rows.Clear();
            IniFile.Section section = this.layout.Find(delegate(IniFile.Section s) { return s.Name == this.sections.SelectedItem as string; });
            this.comment.Text = (section != null) ? section.Comment : "";
            if (section != null)
            {
                foreach (string[] entry in section.Entries)
                {
                    string edited;
                    string value = this.edits.TryGetValue(section.Name + "|" + entry[0], out edited) ? edited : entry[1];
                    this.grid.Rows.Add(entry[0], value, entry[2]);
                }
            }
            this.grid.CurrentCell = null;
            this.grid.ClearSelection();
            this.filling = false;
        }

        // Writes the values changed (only those): [App] through AppIni, the others in place in their section.
        public void Apply()
        {
            this.grid.EndEdit();
            foreach (KeyValuePair<string, string> edit in this.edits)
            {
                int bar = edit.Key.IndexOf('|');
                string name = edit.Key.Substring(0, bar);
                string key = edit.Key.Substring(bar + 1);
                IniFile.Section section = this.layout.Find(delegate(IniFile.Section s) { return s.Name == name; });
                string[] entry = (section == null) ? null : section.Entries.Find(delegate(string[] e) { return e[0] == key; });
                if (entry == null || entry[1] == edit.Value)
                {
                    continue;
                }
                if (string.Equals(name, AppIni.AppSection, StringComparison.OrdinalIgnoreCase))
                {
                    AppIni.SetApp(key, edit.Value);
                }
                else
                {
                    IniFile.Set(AppIni.Path, PortSettings.DefaultIni, name, key, edit.Value);
                }
            }
        }
    }
}
