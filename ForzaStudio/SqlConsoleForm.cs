namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Windows.Forms;

    // Tools > Database SQL (ROADMAP 18.5, 1.4): one statement at a time on the open gamedb.slt, SELECT to
    // look, INSERT / UPDATE / DELETE to change any table. A write is planned first (SqlGuard.Plan: the
    // integrity rules of DbIntegrity, nothing is touched), the plan is shown per table and the user
    // confirms; then it is written in a transaction and listed with the database changes (File > Save
    // writes them). Ctrl+Enter runs (the selected text when there is a selection), Ctrl+Up / Ctrl+Down
    // walk the statements already run. "Diagnostics" are ready-made SELECTs that change nothing; "Table"
    // starts a SELECT on a table.
    internal sealed class SqlConsoleForm : Form
    {
        private readonly GameDatabase database;
        private readonly TextBox sql;
        private readonly Label message;
        private readonly DataGridView grid;
        private readonly ComboBox diagnostics;
        private readonly ComboBox tables;
        private readonly List<string> history = new List<string>();
        private int historyAt;
        private SqlDiagnostics.Query built;     // the diagnostic whose text is in the editor (checked unchanged on Run)

        public event EventHandler Changed;

        public SqlConsoleForm(GameDatabase database)
        {
            this.database = database;
            this.AutoScaleMode = AutoScaleMode.None;
            this.Text = "Database SQL";
            this.ClientSize = new Size(760, 560);
            this.MinimumSize = new Size(520, 400);
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowInTaskbar = false;

            Label diagnosticsLabel = new Label();
            diagnosticsLabel.Text = "Diagnostics:";
            diagnosticsLabel.SetBounds(12, 16, 72, 20);
            this.diagnostics = new ComboBox();
            this.diagnostics.DropDownStyle = ComboBoxStyle.DropDownList;
            this.diagnostics.SetBounds(86, 12, 420, 24);
            this.diagnostics.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            this.diagnostics.MaxDropDownItems = 20;
            try
            {
                this.diagnostics.Items.Add("(ready-made queries, they change nothing)");
                foreach (SqlDiagnostics.Query query in database.Diagnostics())
                {
                    this.diagnostics.Items.Add(query);
                }
            }
            catch (Exception ex)
            {
                this.diagnostics.Items.Add("(not available: " + ex.Message + ")");
            }
            this.diagnostics.SelectedIndex = 0;
            this.diagnostics.SelectedIndexChanged += delegate
            {
                SqlDiagnostics.Query query = this.diagnostics.SelectedItem as SqlDiagnostics.Query;
                if (query != null)
                {
                    this.built = query;
                    this.sql.Text = query.Sql;
                    this.message.ForeColor = ForzaTheme.Green;
                    this.message.Text = query.Name + ". Press Run.";
                }
            };
            Label tablesLabel = new Label();
            tablesLabel.Text = "Table:";
            tablesLabel.SetBounds(520, 16, 40, 20);
            tablesLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            this.tables = new ComboBox();
            this.tables.DropDownStyle = ComboBoxStyle.DropDownList;
            this.tables.SetBounds(562, 12, 186, 24);
            this.tables.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            this.tables.MaxDropDownItems = 25;
            this.tables.Items.Add("(SELECT on a table)");
            List<string> names = new List<string>(database.TableNames);
            names.Sort(StringComparer.OrdinalIgnoreCase);
            this.tables.Items.AddRange(names.ToArray());
            this.tables.SelectedIndex = 0;
            this.tables.SelectedIndexChanged += delegate
            {
                if (this.tables.SelectedIndex > 0)
                {
                    this.sql.Text = "SELECT * FROM " + SqliteDatabase.Quote((string) this.tables.SelectedItem) + " LIMIT 200";
                    this.message.ForeColor = ForzaTheme.Green;
                    this.message.Text = "The columns of the table are the headings of the result.";
                }
            };

            this.sql = new TextBox();
            this.sql.Multiline = true;
            this.sql.AcceptsReturn = true;
            this.sql.ScrollBars = ScrollBars.Vertical;
            this.sql.Font = new Font("Consolas", 10f);
            this.sql.SetBounds(12, 44, 736, 120);
            this.sql.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            this.sql.Text = "SELECT * FROM Data_Car WHERE MediaName LIKE 'ACU%'";
            this.sql.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Control && e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    this.Execute();
                }
                else if (e.Control && (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down))
                {
                    e.SuppressKeyPress = true;
                    this.Walk(e.KeyCode == Keys.Up ? -1 : 1);
                }
            };
            Button run = new Button();
            run.Text = "Run (Ctrl+Enter)";
            run.SetBounds(12, 172, 130, 28);
            run.Click += delegate { this.Execute(); };
            Button copy = new Button();
            copy.Text = "Copy result";
            copy.SetBounds(150, 172, 90, 28);
            copy.Click += delegate { this.CopyResult(); };
            this.message = new Label();
            this.message.SetBounds(250, 172, 498, 44);
            this.message.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            this.message.Text = "One statement: SELECT, INSERT, UPDATE or DELETE. A write is checked against the database rules and shown first.";
            this.grid = new DataGridView();
            this.grid.SetBounds(12, 220, 736, 328);
            this.grid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            this.grid.ReadOnly = true;
            this.grid.AllowUserToAddRows = false;
            this.grid.AllowUserToDeleteRows = false;
            this.grid.RowHeadersVisible = false;
            this.grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
            this.Controls.Add(diagnosticsLabel);
            this.Controls.Add(this.diagnostics);
            this.Controls.Add(tablesLabel);
            this.Controls.Add(this.tables);
            this.Controls.Add(this.sql);
            this.Controls.Add(run);
            this.Controls.Add(copy);
            this.Controls.Add(this.message);
            this.Controls.Add(this.grid);
            ForzaTheme.Apply(this);
        }

        // The statement to run: the selection when there is one, else the whole text.
        private string Statement()
        {
            return (this.sql.SelectionLength > 0) ? this.sql.SelectedText : this.sql.Text;
        }

        private void Walk(int step)
        {
            if (this.history.Count == 0)
            {
                return;
            }
            this.historyAt = Math.Max(0, Math.Min(this.history.Count - 1, this.historyAt + step));
            this.sql.Text = this.history[this.historyAt];
            this.sql.SelectionStart = this.sql.TextLength;
        }

        private void CopyResult()
        {
            if (this.grid.Rows.Count == 0)
            {
                return;
            }
            this.grid.SelectAll();
            this.grid.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithAutoHeaderText;
            Clipboard.SetDataObject(this.grid.GetClipboardContent());
            this.grid.ClearSelection();
            this.message.ForeColor = ForzaTheme.Green;
            this.message.Text = "The result is on the clipboard (tab separated).";
        }

        private void Execute()
        {
            string text = this.Statement();
            try
            {
                if (this.built != null && this.built.Build != null && text == this.built.Sql)
                {
                    this.Fill(this.database.Build(this.built.Build));
                    this.message.ForeColor = ForzaTheme.Green;
                    this.message.Text = this.built.Name + ": " + this.grid.Rows.Count + " rows.";
                    return;
                }
                SqlGuard.Outcome plan = this.database.PlanSql(text);
                if (plan.Table != null)
                {
                    string summary = string.Join("\n", plan.Summary.ToArray());
                    if (MessageBox.Show(this, "This statement would change the database:\n\n" + summary + "\n\nThe rules of the database are met. Write it?", this.Text, MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
                    {
                        this.message.ForeColor = ForzaTheme.Yellow;
                        this.message.Text = "Not written.";
                        return;
                    }
                }
                SqlGuard.Outcome outcome = (plan.Table != null) ? this.database.RunSql(text) : plan;
                this.Remember(text);
                this.message.ForeColor = ForzaTheme.Green;
                this.message.Text = outcome.Message;
                this.grid.Columns.Clear();
                if (outcome.Rows != null)
                {
                    foreach (string column in outcome.Rows.Columns)
                    {
                        this.grid.Columns.Add(column, column);
                    }
                    int shown = 0;
                    foreach (object[] row in outcome.Rows.Rows)
                    {
                        if (shown++ >= 2000)
                        {
                            this.message.Text += " (the first 2000 are shown)";
                            break;
                        }
                        this.grid.Rows.Add(row);
                    }
                }
                else if (this.Changed != null)
                {
                    this.message.Text += " " + string.Join("; ", outcome.Summary.ToArray());
                    this.Changed(this, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                this.message.ForeColor = ForzaTheme.Yellow;
                this.message.Text = ex.Message;
            }
        }

        private void Fill(SqliteDatabase.Result rows)
        {
            this.grid.Columns.Clear();
            foreach (string column in rows.Columns)
            {
                this.grid.Columns.Add(column, column);
            }
            int shown = 0;
            foreach (object[] row in rows.Rows)
            {
                if (shown++ >= 2000)
                {
                    break;
                }
                this.grid.Rows.Add(row);
            }
        }

        private void Remember(string text)
        {
            text = text.Trim();
            if (text.Length > 0 && (this.history.Count == 0 || this.history[this.history.Count - 1] != text))
            {
                this.history.Add(text);
            }
            this.historyAt = this.history.Count;
        }
    }
}
