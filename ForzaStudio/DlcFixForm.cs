namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Windows.Forms;

    // The list of what Fix... can change in a DLC package (DlcFixer.Items), each one ticked or not, and the name the pack
    // gets when its name is empty. OK = do the ticked ones.
    internal sealed class DlcFixForm : Form
    {
        private readonly DlcFixer fixer;
        private readonly CheckedListBox list = new CheckedListBox();
        private readonly TextBox name = new TextBox();
        private readonly TextBox description = new TextBox();
        private readonly TextBox notes = new TextBox();

        public List<DlcFixer.Item> Chosen = new List<DlcFixer.Item>();

        public DlcFixForm(DlcFixer fixer)
        {
            this.fixer = fixer;
            this.AutoScaleMode = AutoScaleMode.None;
            this.Text = "Fix DLC package";
            this.ClientSize = new Size(900, 560);
            this.MinimumSize = new Size(700, 440);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.CenterParent;

            Label head = new Label();
            head.Text = fixer.Items.Count == 0 ? "Nothing can be fixed automatically." : "Tick what to change. The original package is kept in bak\\ first and the package is checked again after.";
            head.SetBounds(12, 10, 870, 20);
            this.list.SetBounds(12, 34, 876, 260);
            this.list.CheckOnClick = true;
            this.list.HorizontalScrollbar = true;
            this.list.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            foreach (DlcFixer.Item item in fixer.Items)
            {
                this.list.Items.Add("[" + item.Kind + "] " + item.Text, item.Checked);
            }

            Label nameLabel = new Label();
            nameLabel.Text = "Pack name:";
            nameLabel.SetBounds(12, 304, 100, 20);
            nameLabel.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
            this.name.Text = fixer.PackName;
            this.name.Enabled = fixer.NeedsName;
            this.name.SetBounds(120, 302, 400, 24);
            this.name.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
            Label descriptionLabel = new Label();
            descriptionLabel.Text = "Description:";
            descriptionLabel.SetBounds(12, 334, 100, 20);
            descriptionLabel.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
            this.description.Text = fixer.PackDescription;
            this.description.Enabled = fixer.NeedsName;
            this.description.SetBounds(120, 332, 768, 24);
            this.description.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;

            this.notes.Multiline = true;
            this.notes.ReadOnly = true;
            this.notes.ScrollBars = ScrollBars.Vertical;
            this.notes.Text = "Not changed by Fix:\r\n" + fixer.Log.ToString().Replace("\r\n", "\n").Replace("\n", "\r\n");
            this.notes.SetBounds(12, 366, 876, 140);
            this.notes.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;

            Button ok = new Button();
            ok.Text = "Fix";
            ok.SetBounds(708, 518, 86, 30);
            ok.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            ok.Enabled = fixer.Items.Count > 0;
            ok.Click += delegate { this.Accept(); };
            Button cancel = new Button();
            cancel.Text = "Cancel";
            cancel.DialogResult = DialogResult.Cancel;
            cancel.SetBounds(802, 518, 86, 30);
            cancel.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            this.CancelButton = cancel;
            this.Controls.AddRange(new Control[] { head, this.list, nameLabel, this.name, descriptionLabel, this.description, this.notes, ok, cancel });
            ForzaTheme.Apply(this);
        }

        private void Accept()
        {
            this.fixer.PackName = this.name.Text;
            this.fixer.PackDescription = this.description.Text;
            this.Chosen = new List<DlcFixer.Item>();
            for (int i = 0; i < this.fixer.Items.Count; i++)
            {
                if (this.list.GetItemChecked(i))
                {
                    this.Chosen.Add(this.fixer.Items[i]);
                }
            }
            this.DialogResult = DialogResult.OK;
        }
    }
}
