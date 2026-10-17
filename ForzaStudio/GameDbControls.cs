namespace ForzaStudio
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Drawing;
    using System.Globalization;
    using System.Windows.Forms;

    // One database row shown in a PropertyGrid: a property per column, grouped in categories, with
    // a drop-down list for id columns. Every edit is written to the database straight away.
    internal class DbRecord : ICustomTypeDescriptor
    {
        public readonly GameDatabase Database;
        public readonly GameDatabase.Table Table;
        public long RowId;
        public object[] Values;
        public event EventHandler Changed;

        public DbRecord(GameDatabase database, string table, long rowId)
        {
            this.Database = database;
            this.Table = database.GetTable(table);
            this.RowId = rowId;
            this.Values = database.ReadRow(table, rowId);
        }

        public bool Exists
        {
            get { return this.Values != null; }
        }

        internal void Write(GameDatabase.Column column, string text)
        {
            object value = GameDatabase.ParseValue(column, text);
            this.RowId = this.Database.Update(this.Table.Name, this.RowId, column.Name, value);
            this.Values = this.Database.ReadRow(this.Table.Name, this.RowId);
            if (this.Changed != null)
            {
                this.Changed(this, EventArgs.Empty);
            }
        }

        public PropertyDescriptorCollection GetProperties(Attribute[] attributes)
        {
            List<PropertyDescriptor> properties = new List<PropertyDescriptor>();
            if (this.Values != null)
            {
                for (int i = 0; i < this.Table.Columns.Count; i++)
                {
                    properties.Add(new DbProperty(this, this.Table.Columns[i], i));
                }
            }
            return new PropertyDescriptorCollection(properties.ToArray());
        }

        public PropertyDescriptorCollection GetProperties()
        {
            return this.GetProperties(null);
        }

        public AttributeCollection GetAttributes() { return AttributeCollection.Empty; }
        public string GetClassName() { return this.Table.Name; }
        public string GetComponentName() { return null; }
        public TypeConverter GetConverter() { return new TypeConverter(); }
        public EventDescriptor GetDefaultEvent() { return null; }
        public PropertyDescriptor GetDefaultProperty() { return null; }
        public object GetEditor(Type editorBaseType) { return null; }
        public EventDescriptorCollection GetEvents(Attribute[] attributes) { return EventDescriptorCollection.Empty; }
        public EventDescriptorCollection GetEvents() { return EventDescriptorCollection.Empty; }
        public object GetPropertyOwner(PropertyDescriptor pd) { return this; }
    }

    internal class DbProperty : PropertyDescriptor
    {
        private readonly DbRecord record;
        private readonly GameDatabase.Column column;
        private readonly int index;
        private readonly GameDatabase.Lookup lookup;

        public DbProperty(DbRecord record, GameDatabase.Column column, int index)
            : base(column.Name, null)
        {
            this.record = record;
            this.column = column;
            this.index = index;
            this.lookup = column.IsPrimaryKey ? null : record.Database.GetLookup(record.Table.Name, column.Name);
        }

        public override AttributeCollection Attributes
        {
            get
            {
                string category = (this.record.Table.Name == "Data_Car") ? GameDatabase.CarCategory(this.column.Name) : GameDatabase.GenericCategory(this.column, this.lookup);
                string description = string.Format("{0} ({1}{2})", this.column.Name, this.column.DeclaredType,
                    this.column.IsPrimaryKey ? ", row id: read only" : (this.lookup != null ? ", pick from the list or type the number" : ""));
                GameDbSchema.Relation relation = this.column.IsPrimaryKey ? null : this.record.Database.RelationOf(this.record.Table.Name, this.column.Name);
                if (relation != null)
                {
                    description += string.Format("\nId of a row in {0} ({1}). Right-click: Go to that row.", relation.Target, relation.TargetColumn);
                }
                return new AttributeCollection(new Attribute[] { new CategoryAttribute(category), new DescriptionAttribute(description) });
            }
        }

        public override TypeConverter Converter
        {
            get { return (this.lookup != null) ? new LookupConverter(this.lookup) : base.Converter; }
        }

        // Color fields get the color picker drop-down and a swatch.
        public override object GetEditor(Type editorBaseType)
        {
            if (editorBaseType == typeof(System.Drawing.Design.UITypeEditor) && GameColor.IsColorColumn(this.column))
            {
                return new ColorValueEditor();
            }
            return base.GetEditor(editorBaseType);
        }

        public override Type ComponentType { get { return typeof(DbRecord); } }
        public override bool IsReadOnly { get { return this.column.IsPrimaryKey; } }
        public override Type PropertyType { get { return typeof(string); } }
        public override bool CanResetValue(object component) { return false; }
        public override void ResetValue(object component) { }
        public override bool ShouldSerializeValue(object component) { return false; }

        public override object GetValue(object component)
        {
            object value = this.record.Values[this.index];
            if (this.lookup != null && value is long)
            {
                return this.lookup.Describe((long) value);
            }
            if (value is long && GameColor.IsColorColumn(this.column))
            {
                return GameDatabase.FormatValue(value) + "  " + GameColor.Hex(GameColor.ToColor((long) value));
            }
            return this.record.Database.DisplayText(this.record.Table.Name, this.column.Name, value) ?? GameDatabase.FormatValue(value);
        }

        public override void SetValue(object component, object value)
        {
            string text = Convert.ToString(value, CultureInfo.InvariantCulture);
            if (GameColor.IsColorColumn(this.column) && text.Trim().StartsWith("#"))
            {
                // "#RRGGBB" typed by hand: the top byte (alpha or flags) stays.
                int rgb;
                if (!int.TryParse(text.Trim().Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out rgb))
                {
                    throw new FormatException(this.column.Name + " needs a color like #C80A0A.");
                }
                object current = this.record.Values[this.index];
                long original = (current is long) ? (long) current : 0;
                text = GameColor.WithColor(original, GameColor.ToColor(rgb)).ToString(CultureInfo.InvariantCulture);
            }
            this.record.Write(this.column, text);
        }
    }

    // Drop-down list of "id - name" for an id column; any other number can still be typed.
    internal class LookupConverter : StringConverter
    {
        private readonly GameDatabase.Lookup lookup;

        public LookupConverter(GameDatabase.Lookup lookup)
        {
            this.lookup = lookup;
        }

        public override bool GetStandardValuesSupported(ITypeDescriptorContext context) { return true; }
        public override bool GetStandardValuesExclusive(ITypeDescriptorContext context) { return false; }

        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
        {
            List<string> values = new List<string>();
            foreach (KeyValuePair<long, string> item in this.lookup.Items)
            {
                values.Add(this.lookup.Describe(item.Key));
            }
            return new StandardValuesCollection(values);
        }
    }

    // Editable rows of one table (optionally filtered), with the same typing rules as the record view.
    internal class RecordGrid : Panel
    {
        private static readonly string[] ColorColumns = { "RGB", "Color2", "ColorStripe", "PriColorRGB", "SecColorRGB", "PrimaryColor", "rgb" };

        private readonly ToolStripLabel lblTitle;
        private readonly ToolStripTextBox txtFilter;
        private readonly DataGridView grid;
        private GameDatabase database;
        private GameDatabase.Table table;
        private string where;
        private bool loading;

        public event EventHandler Changed;
        public event GamePathHandler GamePathActivated;
        public event GamePathHandler GamePathFocused;
        public event DbJumpHandler Jump;

        public string TableName
        {
            get { return (this.table == null) ? null : this.table.Name; }
        }

        public string Filter
        {
            get { return this.where; }
        }

        // Current cell as (row, column), or (-1, -1).
        public Point CellPosition
        {
            get { return (this.grid.CurrentCell == null) ? new Point(-1, -1) : new Point(this.grid.CurrentCell.RowIndex, this.grid.CurrentCell.ColumnIndex); }
            set
            {
                if (value.X >= 0 && value.X < this.grid.Rows.Count && value.Y >= 0 && value.Y < this.grid.Columns.Count && this.grid.Columns[value.Y].Visible)
                {
                    this.grid.CurrentCell = this.grid.Rows[value.X].Cells[value.Y];
                }
            }
        }

        public void FocusGrid()
        {
            this.grid.Focus();
        }

        public RecordGrid(bool showFilter)
        {
            this.grid = new DataGridView();
            this.grid.Dock = DockStyle.Fill;
            this.grid.AllowUserToAddRows = false;
            this.grid.AllowUserToDeleteRows = false;
            this.grid.AllowUserToResizeRows = false;
            this.grid.RowHeadersWidth = 24;
            this.grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
            ForzaTheme.StyleGrid(this.grid);
            this.grid.BorderStyle = BorderStyle.None;
            this.grid.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
            this.grid.CellFormatting += new DataGridViewCellFormattingEventHandler(this.grid_CellFormatting);
            this.grid.CellParsing += new DataGridViewCellParsingEventHandler(this.grid_CellParsing);
            this.grid.CellValueChanged += new DataGridViewCellEventHandler(this.grid_CellValueChanged);
            this.grid.DataError += delegate(object sender, DataGridViewDataErrorEventArgs e) { e.ThrowException = false; };
            // GAME:\ paths open the file they point to (editing a cell is F2 or typing).
            this.grid.CellDoubleClick += delegate(object sender, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && this.IsColorCell(e.ColumnIndex))
                {
                    this.PickColor(e.RowIndex, e.ColumnIndex);
                    return;
                }
                this.ActivateGamePath(e.RowIndex, e.ColumnIndex);
            };
            ContextMenuStrip menu = new ContextMenuStrip();
            ToolStripMenuItem open = new ToolStripMenuItem("Open Linked File", null, delegate
            {
                if (this.grid.CurrentCell != null)
                {
                    this.ActivateGamePath(this.grid.CurrentCell.RowIndex, this.grid.CurrentCell.ColumnIndex);
                }
            });
            ToolStripMenuItem pick = new ToolStripMenuItem("Pick Color...", null, delegate
            {
                if (this.grid.CurrentCell != null)
                {
                    this.PickColor(this.grid.CurrentCell.RowIndex, this.grid.CurrentCell.ColumnIndex);
                }
            });
            menu.Items.Add(open);
            menu.Items.Add(pick);
            menu.Opening += delegate(object sender, CancelEventArgs e)
            {
                DataGridViewCell cell = this.grid.CurrentCell;
                open.Visible = cell != null && GamePaths.IsGamePath(cell.Value as string);
                pick.Visible = cell != null && this.IsColorCell(cell.ColumnIndex);
                object[] row = (cell == null || this.table == null) ? null : this.database.ReadRow(this.table.Name, (long) this.grid.Rows[cell.RowIndex].Tag);
                string column = (cell == null) ? null : ((GameDatabase.Column) this.grid.Columns[cell.ColumnIndex].Tag).Name;
                DbJumpMenu.Fill(menu, this.database, (this.table == null) ? null : this.table.Name, row, column, delegate(string table, string where, string key, object value)
                {
                    if (this.Jump != null)
                    {
                        this.Jump(table, where, key, value);
                    }
                });
                e.Cancel = !DbJumpMenu.HasVisibleItems(menu);
            };
            menu.Renderer = ForzaTheme.Renderer;
            this.grid.CellMouseDown += delegate(object sender, DataGridViewCellMouseEventArgs e)
            {
                if (e.Button == MouseButtons.Right && e.RowIndex >= 0 && e.ColumnIndex >= 0)
                {
                    this.grid.CurrentCell = this.grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
                }
            };
            this.grid.ContextMenuStrip = menu;
            this.grid.CurrentCellChanged += delegate
            {
                if (this.GamePathFocused != null)
                {
                    this.GamePathFocused((this.grid.CurrentCell == null) ? null : this.grid.CurrentCell.Value as string);
                }
            };
            this.grid.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                // Enter on a GAME:\ path opens the file (anywhere else it moves down as usual).
                if (e.KeyCode == Keys.Enter && this.grid.CurrentCell != null && GamePaths.IsGamePath(this.grid.CurrentCell.Value as string))
                {
                    e.Handled = true;
                    this.ActivateGamePath(this.grid.CurrentCell.RowIndex, this.grid.CurrentCell.ColumnIndex);
                }
            };
            this.grid.CurrentCellDirtyStateChanged += delegate
            {
                // Lists commit as soon as an item is picked.
                if (this.grid.IsCurrentCellDirty && this.grid.CurrentCell is DataGridViewComboBoxCell)
                {
                    this.grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            };

            ToolStrip bar = new ToolStrip();
            bar.GripStyle = ToolStripGripStyle.Hidden;
            this.lblTitle = new ToolStripLabel();
            bar.Items.Add(this.lblTitle);
            bar.Items.Add(new ToolStripSeparator());
            bar.Items.Add(new ToolStripButton("Duplicate Row", null, delegate { this.DuplicateRow(); }));
            bar.Items.Add(new ToolStripButton("Delete Row", null, delegate { this.DeleteRow(); }));
            // The right-click menu of the selected cell, from the toolbar.
            ToolStripButton links = new ToolStripButton("Go to / Used by ▾");
            links.ToolTipText = "The row the selected id points at, and the rows of other tables that point at this row (also on right-click)";
            links.Click += delegate
            {
                if (this.grid.CurrentCell != null)
                {
                    menu.Show(bar, new Point(links.Bounds.Left, links.Bounds.Bottom));
                }
            };
            bar.Items.Add(links);
            if (showFilter)
            {
                bar.Items.Add(new ToolStripSeparator());
                bar.Items.Add(new ToolStripLabel("Filter:"));
                this.txtFilter = new ToolStripTextBox();
                this.txtFilter.Width = 260;
                this.txtFilter.ToolTipText = "An SQL WHERE condition, for example: MediaName LIKE '%supra%'   (Enter to apply, empty for all rows)";
                this.txtFilter.KeyDown += delegate(object sender, KeyEventArgs e)
                {
                    if (e.KeyCode == Keys.Enter)
                    {
                        e.SuppressKeyPress = true;
                        this.ShowRows(this.database, this.table.Name, this.txtFilter.Text.Trim());
                    }
                };
                bar.Items.Add(this.txtFilter);
            }

            this.Controls.Add(this.grid);
            this.Controls.Add(bar);
        }

        public void Clear()
        {
            this.grid.Rows.Clear();
            this.grid.Columns.Clear();
            this.lblTitle.Text = "";
            this.table = null;
        }

        public void ShowRows(GameDatabase database, string tableName, string filter)
        {
            this.database = database;
            this.table = database.GetTable(tableName);
            this.where = filter;
            this.loading = true;
            this.grid.SuspendLayout();
            try
            {
                this.grid.Rows.Clear();
                this.grid.Columns.Clear();
                string sql = "SELECT rowid, * FROM " + SqliteDatabase.Quote(this.table.Name) + (string.IsNullOrEmpty(filter) ? "" : " WHERE " + filter) + " ORDER BY rowid";
                SqliteDatabase.Result result;
                try
                {
                    result = database.Sql.Query(sql);
                }
                catch (InvalidOperationException exception)
                {
                    this.lblTitle.Text = this.table.Name + ": " + exception.Message;
                    return;
                }
                foreach (GameDatabase.Column column in this.table.Columns)
                {
                    this.grid.Columns.Add(this.CreateColumn(column, result));
                }
                if (this.grid.Columns.Count > 0)
                {
                    this.grid.Columns[0].Frozen = true;
                }
                foreach (object[] row in result.Rows)
                {
                    object[] cells = new object[row.Length - 1];
                    for (int i = 1; i < row.Length; i++)
                    {
                        cells[i - 1] = row[i] ?? DBNull.Value;
                    }
                    int index = this.grid.Rows.Add(cells);
                    this.grid.Rows[index].Tag = row[0];
                }
                // Columns as wide as their content (measuring every cell of big tables is too slow).
                if (result.Rows.Count <= 400)
                {
                    this.grid.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.AllCells);
                    foreach (DataGridViewColumn gridColumn in this.grid.Columns)
                    {
                        gridColumn.Width = Math.Min(Math.Max(gridColumn.Width + 8, 40), 260);
                    }
                }
                this.UpdateTitle();
                if (this.txtFilter != null)
                {
                    this.txtFilter.Text = filter ?? "";
                }
            }
            finally
            {
                this.grid.ResumeLayout();
                this.loading = false;
            }
        }

        private void UpdateTitle()
        {
            this.lblTitle.Text = string.Format("{0}: {1} rows", this.table.Name, this.grid.Rows.Count);
        }

        private DataGridViewColumn CreateColumn(GameDatabase.Column column, SqliteDatabase.Result result)
        {
            GameDatabase.Lookup lookup = column.IsPrimaryKey ? null : this.database.GetLookup(this.table.Name, column.Name);
            DataGridViewColumn gridColumn;
            if (lookup != null)
            {
                // The list holds every value present in the rows, so unknown ids still show.
                List<LookupItem> items = new List<LookupItem>();
                Dictionary<long, bool> seen = new Dictionary<long, bool>();
                foreach (KeyValuePair<long, string> item in lookup.Items)
                {
                    items.Add(new LookupItem(item.Key, lookup.Describe(item.Key)));
                    seen[item.Key] = true;
                }
                int index = this.table.Columns.IndexOf(column) + 1;
                foreach (object[] row in result.Rows)
                {
                    if (row[index] is long && !seen.ContainsKey((long) row[index]))
                    {
                        seen[(long) row[index]] = true;
                        items.Add(new LookupItem((long) row[index], ((long) row[index]).ToString(CultureInfo.InvariantCulture)));
                    }
                }
                DataGridViewComboBoxColumn combo = new DataGridViewComboBoxColumn();
                combo.DataSource = items;
                combo.ValueMember = "Value";
                combo.DisplayMember = "Name";
                combo.DisplayStyle = DataGridViewComboBoxDisplayStyle.Nothing;
                combo.FlatStyle = FlatStyle.Flat;
                combo.DropDownWidth = 260;
                gridColumn = combo;
            }
            else
            {
                gridColumn = new DataGridViewTextBoxColumn();
            }
            gridColumn.Name = column.Name;
            gridColumn.HeaderText = column.Name;
            gridColumn.Tag = column;
            gridColumn.SortMode = DataGridViewColumnSortMode.NotSortable;
            gridColumn.ReadOnly = false;
            gridColumn.Width = (lookup != null) ? 150 : Math.Max(60, Math.Min(180, (column.Name.Length * 7) + 16));
            if (column.IsPrimaryKey)
            {
                gridColumn.DefaultCellStyle.ForeColor = ForzaTheme.TextDim;
            }
            return gridColumn;
        }

        private class LookupItem
        {
            private readonly long value;
            private readonly string name;

            public LookupItem(long value, string name)
            {
                this.value = value;
                this.name = name;
            }

            public long Value { get { return this.value; } }
            public string Name { get { return this.name; } }
        }

        private bool IsColorCell(int column)
        {
            return column >= 0 && column < this.grid.Columns.Count && GameColor.IsColorColumn(this.grid.Columns[column].Tag as GameDatabase.Column);
        }

        // The color picker under the cell; the new RGB keeps the value's top byte.
        private void PickColor(int row, int column)
        {
            if (row < 0 || !this.IsColorCell(column))
            {
                return;
            }
            DataGridViewCell cell = this.grid.Rows[row].Cells[column];
            long original = (cell.Value is long) ? (long) cell.Value : 0;
            Rectangle bounds = this.grid.GetCellDisplayRectangle(column, row, false);
            ForzaColorPicker.Popup(this.grid, bounds, GameColor.ToColor(original), delegate(Color picked)
            {
                long value = GameColor.WithColor(original, picked);
                if (value != original)
                {
                    cell.Value = value;
                }
            });
        }

        private void ActivateGamePath(int row, int column)
        {
            if (row < 0 || column < 0)
            {
                return;
            }
            string value = this.grid.Rows[row].Cells[column].Value as string;
            if (GamePaths.IsGamePath(value) && this.GamePathActivated != null)
            {
                this.GamePathActivated(value);
            }
        }

        private void grid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex >= 0 && GamePaths.IsGamePath(e.Value as string))
            {
                e.CellStyle.ForeColor = Color.FromArgb(110, 170, 255);
            }
            if (e.RowIndex < 0 || this.grid.Columns[e.ColumnIndex] is DataGridViewComboBoxColumn)
            {
                return;
            }
            object value = e.Value;
            string localized = (value is string) ? this.database.DisplayText(this.table.Name, this.grid.Columns[e.ColumnIndex].Name, value) : null;
            if (localized != null)
            {
                e.Value = localized;
                e.FormattingApplied = true;
            }
            else if (value is double)
            {
                e.Value = GameDatabase.FormatValue(value);
                e.FormattingApplied = true;
            }
            else if (value == null || value is DBNull)
            {
                e.Value = "NULL";
                e.CellStyle.ForeColor = ForzaTheme.TextDim;
                e.FormattingApplied = true;
            }
            if (value is long && this.IsColorCell(e.ColumnIndex))
            {
                long rgb = (long) value;
                Color color = Color.FromArgb((int) ((rgb >> 16) & 0xFF), (int) ((rgb >> 8) & 0xFF), (int) (rgb & 0xFF));
                e.CellStyle.BackColor = color;
                e.CellStyle.ForeColor = (((color.R * 299) + (color.G * 587) + (color.B * 114)) / 1000 > 128) ? Color.Black : Color.White;
                e.CellStyle.SelectionBackColor = color;
                e.CellStyle.SelectionForeColor = e.CellStyle.ForeColor;
            }
        }

        private void grid_CellParsing(object sender, DataGridViewCellParsingEventArgs e)
        {
            GameDatabase.Column column = this.grid.Columns[e.ColumnIndex].Tag as GameDatabase.Column;
            if (column == null || this.grid.Columns[e.ColumnIndex] is DataGridViewComboBoxColumn)
            {
                return;
            }
            try
            {
                object value = GameDatabase.ParseValue(column, Convert.ToString(e.Value, CultureInfo.CurrentCulture));
                e.Value = value ?? DBNull.Value;
                e.ParsingApplied = true;
            }
            catch (FormatException exception)
            {
                MessageBox.Show(this, exception.Message, "Game Database", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                e.Value = this.grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value;
                e.ParsingApplied = true;
            }
        }

        private void grid_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (this.loading || e.RowIndex < 0)
            {
                return;
            }
            DataGridViewRow row = this.grid.Rows[e.RowIndex];
            GameDatabase.Column column = (GameDatabase.Column) this.grid.Columns[e.ColumnIndex].Tag;
            object value = row.Cells[e.ColumnIndex].Value;
            try
            {
                row.Tag = this.database.Update(this.table.Name, (long) row.Tag, column.Name, (value is DBNull) ? null : value);
                if (this.Changed != null)
                {
                    this.Changed(this, EventArgs.Empty);
                }
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, "Game Database", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                this.ReloadRow(row);
            }
        }

        private void ReloadRow(DataGridViewRow row)
        {
            object[] values = this.database.ReadRow(this.table.Name, (long) row.Tag);
            if (values == null)
            {
                return;
            }
            this.loading = true;
            for (int i = 0; i < values.Length; i++)
            {
                row.Cells[i].Value = values[i] ?? DBNull.Value;
            }
            this.loading = false;
        }

        private void DuplicateRow()
        {
            if (this.table == null || this.grid.CurrentRow == null)
            {
                return;
            }
            try
            {
                long copy = this.database.DuplicateRow(this.table.Name, (long) this.grid.CurrentRow.Tag);
                int column = this.grid.CurrentCell.ColumnIndex;
                // A copied row may not match the filter (new id): show it anyway.
                string filter = string.IsNullOrEmpty(this.where) ? null : "(" + this.where + ") OR rowid = " + copy.ToString(CultureInfo.InvariantCulture);
                this.ShowRows(this.database, this.table.Name, filter);
                foreach (DataGridViewRow row in this.grid.Rows)
                {
                    if ((long) row.Tag == copy)
                    {
                        this.grid.CurrentCell = row.Cells[column];
                        break;
                    }
                }
                this.where = filter;
                if (this.Changed != null)
                {
                    this.Changed(this, EventArgs.Empty);
                }
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, "Duplicate Row", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }

        private void DeleteRow()
        {
            if (this.table == null || this.grid.CurrentRow == null)
            {
                return;
            }
            DataGridViewRow row = this.grid.CurrentRow;
            string question = string.Format("Delete row #{0} from {1}?", row.Tag, this.table.Name);
            List<GameDatabase.Related> users = this.database.ReferencesTo(this.table.Name, this.database.ReadRow(this.table.Name, (long) row.Tag));
            if (users.Count > 0)
            {
                // The game has no foreign keys: rows left pointing at a deleted one can break it (a car without body data has NaN physics).
                List<string> lines = new List<string>();
                foreach (GameDatabase.Related user in users)
                {
                    lines.Add("   " + user);
                }
                question += "\n\nThese rows still point at it and would be left dangling:\n" + string.Join("\n", lines.ToArray()) + "\n\nGo to / Used by (toolbar or right-click) shows them.";
            }
            if (MessageBox.Show(this, question, "Delete Row", MessageBoxButtons.YesNo, (users.Count > 0) ? MessageBoxIcon.Warning : MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            {
                return;
            }
            try
            {
                this.database.DeleteRow(this.table.Name, (long) row.Tag);
                this.loading = true;
                this.grid.Rows.Remove(row);
                this.loading = false;
                this.UpdateTitle();
                if (this.Changed != null)
                {
                    this.Changed(this, EventArgs.Empty);
                }
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, "Delete Row", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }
    }

    internal delegate void GamePathHandler(string gamePath);

    // A jump to rows of a table: key and value are set when it is one row by its id.
    internal delegate void DbJumpHandler(string table, string where, string key, object value);

    // Right-click items: "Go to" the row an id points at, and "Used by": the rows of other tables
    // that point at this one (GameDbSchema).
    internal static class DbJumpMenu
    {
        private static readonly object Marker = new object();

        public static void Fill(ContextMenuStrip menu, GameDatabase database, string tableName, object[] row, string columnName, DbJumpHandler jump)
        {
            for (int i = menu.Items.Count - 1; i >= 0; i--)
            {
                if (menu.Items[i].Tag == Marker)
                {
                    menu.Items.RemoveAt(i);
                }
            }
            GameDatabase.Table table = (database == null || tableName == null) ? null : database.GetTable(tableName);
            if (table == null || row == null)
            {
                return;
            }
            List<ToolStripItem> items = new List<ToolStripItem>();
            GameDatabase.Column column = (columnName == null) ? null : table.Find(columnName);
            GameDbSchema.Relation relation = (column == null || column.IsPrimaryKey) ? null : database.RelationOf(table.Name, column.Name);
            object value = (column == null) ? null : row[table.Columns.IndexOf(column)];
            if (relation != null && !relation.IsComposite && value != null && !(value is long && GameDbSchema.IsNone((long) value)) && !"".Equals(value))
            {
                GameDatabase.Lookup lookup = database.GetLookup(table.Name, column.Name);
                string name = (lookup != null && value is long) ? lookup.Name((long) value) : null;
                bool here = database.HasTable(relation.Target);
                string where = SqliteDatabase.Quote(relation.TargetColumn) + " = " + GameDatabase.SqlLiteral(value);
                bool exists = here && database.Count(relation.Target, where) > 0;
                string text = string.Format("Go to {0} {1}{2}", relation.Target, GameDatabase.FormatValue(value), string.IsNullOrEmpty(name) ? "" : " - " + name);
                ToolStripMenuItem go = new ToolStripMenuItem(text + (exists ? "" : here ? "   (missing: dangling id)" : "   (not in this database)"), null, delegate
                {
                    jump(relation.Target, where, relation.TargetColumn, value);
                });
                go.Enabled = exists;
                items.Add(go);
            }
            if (GameDbSchema.Into(table.Name).Count > 0)
            {
                List<GameDatabase.Related> users = database.ReferencesTo(table.Name, row);
                ToolStripMenuItem used = new ToolStripMenuItem((users.Count == 0) ? "Not used by other tables" : "Used by");
                used.Enabled = users.Count > 0;
                foreach (GameDatabase.Related user in users)
                {
                    GameDatabase.Related target = user;
                    used.DropDownItems.Add(new ToolStripMenuItem(user.ToString(), null, delegate { jump(target.Table, target.Where, null, null); }));
                }
                items.Add(used);
            }
            if (items.Count == 0)
            {
                return;
            }
            if (HasVisibleItems(menu))
            {
                items.Insert(0, new ToolStripSeparator());
            }
            foreach (ToolStripItem item in items)
            {
                item.Tag = Marker;
                menu.Items.Add(item);
            }
        }

        // Visible before the menu is shown (Visible is false until then, Available is not).
        public static bool HasVisibleItems(ContextMenuStrip menu)
        {
            foreach (ToolStripItem item in menu.Items)
            {
                if (item.Available && !(item is ToolStripSeparator))
                {
                    return true;
                }
            }
            return false;
        }
    }

    // A list of related tables on the left (upgrades of a car, races of an event...) and their rows.
    internal class RelatedPanel : SplitContainer
    {
        private readonly ListBox list;
        private readonly RecordGrid grid;
        private GameDatabase database;

        public event EventHandler Changed;
        public event GamePathHandler GamePathActivated;
        public event GamePathHandler GamePathFocused;
        public event DbJumpHandler Jump;

        public RecordGrid Grid
        {
            get { return this.grid; }
        }

        // The list shown on the right, by its label.
        public string SelectedLabel
        {
            get { return (this.list.SelectedItem is GameDatabase.Related) ? ((GameDatabase.Related) this.list.SelectedItem).Label : null; }
            set
            {
                for (int i = 0; i < this.list.Items.Count; i++)
                {
                    if (((GameDatabase.Related) this.list.Items[i]).Label == value)
                    {
                        this.list.SelectedIndex = i;
                    }
                }
            }
        }

        public RelatedPanel()
        {
            this.Dock = DockStyle.Fill;
            this.FixedPanel = FixedPanel.Panel1;
            this.list = new ListBox();
            this.list.Dock = DockStyle.Fill;
            this.list.IntegralHeight = false;
            this.list.BorderStyle = BorderStyle.None;
            this.list.SelectedIndexChanged += delegate
            {
                GameDatabase.Related related = this.list.SelectedItem as GameDatabase.Related;
                if (related != null)
                {
                    this.grid.ShowRows(this.database, related.Table, related.Where);
                }
            };
            this.grid = new RecordGrid(false);
            this.grid.Dock = DockStyle.Fill;
            this.grid.Changed += delegate(object sender, EventArgs e)
            {
                if (this.Changed != null)
                {
                    this.Changed(this, e);
                }
            };
            this.grid.GamePathActivated += delegate(string path)
            {
                if (this.GamePathActivated != null)
                {
                    this.GamePathActivated(path);
                }
            };
            this.grid.GamePathFocused += delegate(string path)
            {
                if (this.GamePathFocused != null)
                {
                    this.GamePathFocused(path);
                }
            };
            this.grid.Jump += delegate(string table, string where, string key, object value)
            {
                if (this.Jump != null)
                {
                    this.Jump(table, where, key, value);
                }
            };
            this.Panel1.Controls.Add(this.list);
            this.Panel2.Controls.Add(this.grid);
        }

        private bool sized;

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (!this.sized && this.Width > 500)
            {
                this.sized = true;
                this.SplitterDistance = 240;
            }
        }

        public void ShowItems(GameDatabase database, List<GameDatabase.Related> items)
        {
            this.database = database;
            string selected = (this.list.SelectedItem is GameDatabase.Related) ? ((GameDatabase.Related) this.list.SelectedItem).Label : null;
            this.list.BeginUpdate();
            this.list.Items.Clear();
            foreach (GameDatabase.Related item in items)
            {
                this.list.Items.Add(item);
            }
            this.list.EndUpdate();
            this.grid.Clear();
            int index = 0;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Label == selected)
                {
                    index = i;
                }
            }
            if (items.Count > 0)
            {
                this.list.SelectedIndex = index;
            }
        }
    }
}
