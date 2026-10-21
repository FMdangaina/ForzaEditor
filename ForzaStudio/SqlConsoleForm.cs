namespace ForzaStudio
{
    using System;
    using System.Drawing;
    using System.Windows.Forms;

    // Tools > Database SQL (ROADMAP 18.5): one statement at a time on the open gamedb.slt, SELECT to look,
    // INSERT / UPDATE / DELETE to change any table. Writes go through SqlGuard (the Id rule) and are listed
    // with the database changes; File > Save writes them. Ctrl+Enter runs.
    internal sealed class SqlConsoleForm : Form
    {
        private readonly GameDatabase database;
        private readonly TextBox sql;
        private readonly Label message;
        private readonly DataGridView grid;

        public event EventHandler Changed;

        public SqlConsoleForm(GameDatabase database)
        {
            this.database = database;
            this.AutoScaleMode = AutoScaleMode.None;
            this.Text = "Database SQL";
            this.ClientSize = new Size(760, 520);
            this.MinimumSize = new Size(520, 360);
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowInTaskbar = false;

            this.sql = new TextBox();
            this.sql.Multiline = true;
            this.sql.AcceptsReturn = true;
            this.sql.ScrollBars = ScrollBars.Vertical;
            this.sql.Font = new Font("Consolas", 10f);
            this.sql.SetBounds(12, 12, 736, 130);
            this.sql.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            this.sql.Text = "SELECT * FROM Data_Car WHERE MediaName LIKE 'ACU%'";
            this.sql.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Control && e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    this.Execute();
                }
            };
            Button run = new Button();
            run.Text = "Run (Ctrl+Enter)";
            run.SetBounds(12, 150, 130, 28);
            run.Click += delegate { this.Execute(); };
            this.message = new Label();
            this.message.SetBounds(152, 154, 596, 40);
            this.message.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            this.message.Text = "One statement: SELECT, INSERT, UPDATE or DELETE. New keys must be above FM4's largest Id.";
            this.grid = new DataGridView();
            this.grid.SetBounds(12, 200, 736, 308);
            this.grid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            this.grid.ReadOnly = true;
            this.grid.AllowUserToAddRows = false;
            this.grid.AllowUserToDeleteRows = false;
            this.grid.RowHeadersVisible = false;
            this.grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
            this.Controls.Add(this.sql);
            this.Controls.Add(run);
            this.Controls.Add(this.message);
            this.Controls.Add(this.grid);
            ForzaTheme.Apply(this);
        }

        private void Execute()
        {
            try
            {
                SqlGuard.Outcome outcome = this.database.RunSql(this.sql.Text);
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
                    this.Changed(this, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                this.message.ForeColor = ForzaTheme.Yellow;
                this.message.Text = ex.Message;
            }
        }
    }
}
