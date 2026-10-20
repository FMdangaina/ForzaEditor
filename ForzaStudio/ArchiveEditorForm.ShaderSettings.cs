namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Globalization;
    using System.IO;
    using System.Windows.Forms;

    // Shader Settings view of a car's shadersettings.xml: on the left the techniques the car's models
    // use (and the entries no model uses), on the right the parameters of the selected one. Red: an
    // error (a parameter FM4 does not know, or one no FM4 car sets on that technique, such as
    // BrakeColor on lights_glass); yellow: an entry no model uses (harmless, 41 FM4 cars have some).
    // A technique without an entry runs with its shader's defaults (61 FM4 cars have no file at all).
    // Every edit is stored in the archive at once (pending until Save); "Edit as XML" opens the text.
    // "Copy missing from" reads the entries of this file or of another car (Media\cars of the same game
    // folder, or any car zip by Browse...), shows the chosen entry's values and copies what is missing.
    public partial class ArchiveEditorForm
    {
        private Panel pnlShader;
        private ListView lvTechniques;
        private ListView lvParameters;
        private ToolStripLabel lblShaderSummary;
        private Label lblShaderTechnique;
        private ComboBox cmbAddParameter;
        private ComboBox cmbCopyFrom;
        private ComboBox cmbCopyCar;
        private Label lblCopyPreview;
        private Button btnAddParameter;
        private Button btnEditParameter;
        private Button btnRemoveParameter;
        private Button btnCopyFrom;
        private Button btnRemoveEntry;
        private ToolStripButton btnXmlShaderView;

        private ForzaZip.Entry shaderEntry;
        private ShaderSettings shaderData;
        private SortedDictionary<string, List<string>> shaderUsed;
        private bool shaderAsXml;          // "Edit as XML" chosen for the shown file
        private ShaderSettings copySource;    // the settings "Copy missing from" reads (this file or another car)
        private string copySourceName;        // that car's name, null = this file
        private Dictionary<string, string> copyCarPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private bool fillingCopyCars;

        private const string CopyThisFile = "(this file)";
        private const string CopyBrowse = "Browse...";

        private static bool IsShaderSettings(ForzaZip.Entry entry)
        {
            return entry.Name.EndsWith("shadersettings.xml", StringComparison.OrdinalIgnoreCase);
        }

        private void BuildShaderView()
        {
            this.pnlShader = new Panel();
            this.pnlShader.Dock = DockStyle.Fill;

            ToolStrip bar = new ToolStrip();
            bar.GripStyle = ToolStripGripStyle.Hidden;
            bar.Items.Add(new ToolStripButton("Edit as XML", null, delegate
            {
                this.shaderAsXml = true;
                this.ShowXml(this.shaderEntry, this.zip.Read(this.shaderEntry));
            }));
            bar.Items.Add(new ToolStripButton("Remove Unused Entries", null, delegate { this.RemoveUnusedShaderEntries(); }));
            this.lblShaderSummary = new ToolStripLabel();
            this.lblShaderSummary.Alignment = ToolStripItemAlignment.Right;
            bar.Items.Add(this.lblShaderSummary);

            SplitContainer split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.SplitterDistance = 300;

            this.lvTechniques = new ListView();
            this.lvTechniques.Dock = DockStyle.Fill;
            this.lvTechniques.View = View.Details;
            this.lvTechniques.FullRowSelect = true;
            this.lvTechniques.HideSelection = false;
            this.lvTechniques.MultiSelect = false;
            this.lvTechniques.Columns.Add("Technique", 190);
            this.lvTechniques.Columns.Add("Used in", 90);
            this.lvTechniques.ShowItemToolTips = true;
            this.lvTechniques.SelectedIndexChanged += delegate { this.FillShaderParameters(); };
            split.Panel1.Controls.Add(this.lvTechniques);

            this.lvParameters = new ListView();
            this.lvParameters.Dock = DockStyle.Fill;
            this.lvParameters.View = View.Details;
            this.lvParameters.FullRowSelect = true;
            this.lvParameters.HideSelection = false;
            this.lvParameters.MultiSelect = false;
            this.lvParameters.Columns.Add("Parameter", 180);
            this.lvParameters.Columns.Add("Value", 220);
            this.lvParameters.Columns.Add("Check", 320);
            this.lvParameters.DoubleClick += delegate { this.EditShaderParameter(); };
            this.lvParameters.SelectedIndexChanged += delegate { this.EnableShaderButtons(); };

            this.lblShaderTechnique = new Label();
            this.lblShaderTechnique.Dock = DockStyle.Top;
            this.lblShaderTechnique.Height = 40;
            this.lblShaderTechnique.Padding = new Padding(4);

            // Grows with its rows, so a narrow window wraps the buttons instead of hiding them.
            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Bottom;
            buttons.AutoSize = true;
            buttons.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            buttons.Padding = new Padding(2, 2, 2, 6);
            this.cmbAddParameter = new ComboBox();
            this.cmbAddParameter.DropDownStyle = ComboBoxStyle.DropDownList;
            this.cmbAddParameter.Width = 220;
            ForzaTheme.GreenItems(this.cmbAddParameter);
            this.cmbAddParameter.MaxDropDownItems = 24;
            this.btnAddParameter = new Button();
            this.btnAddParameter.Text = "Add";
            this.btnAddParameter.AutoSize = true;
            this.btnAddParameter.Click += delegate { this.AddShaderParameter(); };
            this.btnEditParameter = new Button();
            this.btnEditParameter.Text = "Edit...";
            this.btnEditParameter.AutoSize = true;
            this.btnEditParameter.Click += delegate { this.EditShaderParameter(); };
            this.btnRemoveParameter = new Button();
            this.btnRemoveParameter.Text = "Remove";
            this.btnRemoveParameter.AutoSize = true;
            this.btnRemoveParameter.Click += delegate { this.RemoveShaderParameter(); };
            this.cmbCopyFrom = new ComboBox();
            this.cmbCopyFrom.DropDownStyle = ComboBoxStyle.DropDownList;
            this.cmbCopyFrom.Width = 220;
            this.cmbCopyFrom.MaxDropDownItems = 24;
            this.cmbCopyFrom.SelectedIndexChanged += delegate { this.ShowCopyPreview(); };
            this.cmbCopyCar = new ComboBox();
            this.cmbCopyCar.DropDownStyle = ComboBoxStyle.DropDownList;
            this.cmbCopyCar.Width = 220;
            this.cmbCopyCar.MaxDropDownItems = 24;
            this.cmbCopyCar.SelectedIndexChanged += delegate { this.CopyCarChanged(); };
            this.lblCopyPreview = new Label();
            this.lblCopyPreview.AutoSize = true;
            this.lblCopyPreview.MaximumSize = new Size(700, 0);
            this.lblCopyPreview.Margin = new Padding(156, 2, 3, 3);
            this.lblCopyPreview.ForeColor = ForzaTheme.TextDim;
            this.btnCopyFrom = new Button();
            this.btnCopyFrom.Text = "Copy Missing From";
            this.btnCopyFrom.AutoSize = true;
            this.btnCopyFrom.Click += delegate { this.CopyShaderEntry(); };
            this.btnRemoveEntry = new Button();
            this.btnRemoveEntry.Text = "Remove Settings";
            this.btnRemoveEntry.AutoSize = true;
            this.btnRemoveEntry.Click += delegate { this.RemoveShaderEntry(); };
            // Labels: the add list holds the parameters the technique lacks (green = FM4 sets it on that
            // technique), the copy list the file's other entries with settings.
            Label addLabel = ShaderButtonLabel("Add parameter (green = FM4):");
            Label carLabel = ShaderButtonLabel("Settings of car:");
            Label copyLabel = ShaderButtonLabel("Copy missing from:");
            buttons.Controls.AddRange(new Control[] { addLabel, this.cmbAddParameter, this.btnAddParameter, this.btnEditParameter, this.btnRemoveParameter, this.btnRemoveEntry });
            buttons.SetFlowBreak(this.btnRemoveEntry, true);
            buttons.Controls.AddRange(new Control[] { carLabel, this.cmbCopyCar });
            buttons.SetFlowBreak(this.cmbCopyCar, true);
            buttons.Controls.AddRange(new Control[] { copyLabel, this.cmbCopyFrom, this.btnCopyFrom });
            buttons.SetFlowBreak(this.btnCopyFrom, true);
            buttons.Controls.Add(this.lblCopyPreview);

            split.Panel2.Controls.Add(this.lvParameters);
            split.Panel2.Controls.Add(buttons);
            split.Panel2.Controls.Add(this.lblShaderTechnique);

            this.pnlShader.Controls.Add(split);
            this.pnlShader.Controls.Add(bar);
        }

        // A label in front of a list in the button rows, the same width on both rows so the lists line up.
        private static Label ShaderButtonLabel(string text)
        {
            Label label = new Label();
            label.Text = text;
            label.Width = 150;
            label.TextAlign = ContentAlignment.MiddleRight;
            label.Margin = new Padding(3, 4, 3, 3);
            return label;
        }

        private void ShowShaderSettings(ForzaZip.Entry entry, byte[] data)
        {
            this.shaderEntry = entry;
            this.shaderData = ShaderSettings.Parse(data);
            string folder = entry.Name.Substring(0, entry.Name.LastIndexOf('/') + 1);
            this.shaderUsed = ShaderSettings.Techniques(this.zip, folder);
            this.FillCopyCars();
            this.FillShaderTechniques(null);
            this.ShowView(this.pnlShader);
        }

        private string SelectedTechnique
        {
            get { return (this.lvTechniques.SelectedItems.Count > 0) ? (string) this.lvTechniques.SelectedItems[0].Tag : null; }
        }

        // M main, L lod0, C cockpit, B brakes.
        private string UsedIn(string technique)
        {
            List<string> files;
            if (!this.shaderUsed.TryGetValue(technique, out files))
            {
                return "not used";
            }
            string letters = "";
            foreach (string file in files)
            {
                string kind = file.EndsWith("_lod0.carbin", StringComparison.OrdinalIgnoreCase) && (file.Contains("caliper") || file.Contains("rotor")) ? "B"
                    : file.EndsWith("_lod0.carbin", StringComparison.OrdinalIgnoreCase) ? "L"
                    : file.EndsWith("_cockpit.carbin", StringComparison.OrdinalIgnoreCase) ? "C" : "M";
                if (!letters.Contains(kind))
                {
                    letters += kind;
                }
            }
            return letters;
        }

        private int ErrorsOf(ShaderSettings.Entry entry)
        {
            int errors = 0;
            foreach (ShaderSettings.Parameter p in entry.Parameters)
            {
                errors += (ShaderSettings.Check(entry.Name, p) != ShaderSettings.Problem.None) ? 1 : 0;
            }
            return errors;
        }

        private void FillShaderTechniques(string select)
        {
            select = select ?? this.SelectedTechnique;
            this.lvTechniques.BeginUpdate();
            this.lvTechniques.Items.Clear();
            this.lvTechniques.Groups.Clear();
            ListViewGroup withSettings = new ListViewGroup("With settings");
            ListViewGroup defaults = new ListViewGroup("No settings (shader defaults)");
            this.lvTechniques.Groups.Add(withSettings);
            this.lvTechniques.Groups.Add(defaults);
            int errors = 0;
            int unused = 0;
            foreach (ShaderSettings.Entry entry in this.shaderData.Entries)
            {
                ListViewItem item = new ListViewItem(new string[] { entry.Name, this.UsedIn(entry.Name) }, withSettings);
                item.Tag = entry.Name;
                int bad = this.ErrorsOf(entry);
                if (bad > 0)
                {
                    errors += bad;
                    item.ForeColor = ForzaTheme.Red;
                    item.ToolTipText = bad + " parameter(s) FM4 does not use on " + entry.Name;
                }
                else if (!this.shaderUsed.ContainsKey(entry.Name))
                {
                    unused++;
                    item.ForeColor = ForzaTheme.Yellow;
                    item.ToolTipText = "No model of the car uses " + entry.Name + ": the game ignores it (harmless).";
                }
                this.lvTechniques.Items.Add(item);
            }
            int noSettings = 0;
            foreach (KeyValuePair<string, List<string>> used in this.shaderUsed)
            {
                if (this.shaderData.Find(used.Key) != null)
                {
                    continue;
                }
                noSettings++;
                ListViewItem item = new ListViewItem(new string[] { used.Key, this.UsedIn(used.Key) }, defaults);
                item.Tag = used.Key;
                item.ForeColor = ForzaTheme.TextDim;
                item.ToolTipText = string.Join(", ", used.Value.ToArray());
                this.lvTechniques.Items.Add(item);
            }
            this.lvTechniques.EndUpdate();
            this.lblShaderSummary.Text = string.Format("{0} with settings, {1} without (defaults), {2} unused, {3} error{4}",
                this.shaderData.Entries.Count, noSettings, unused, errors, (errors == 1) ? "" : "s");
            this.lblShaderSummary.ForeColor = (errors > 0) ? ForzaTheme.Red : ForzaTheme.Text;
            foreach (ListViewItem item in this.lvTechniques.Items)
            {
                if ((string) item.Tag == select)
                {
                    item.Selected = true;
                    item.EnsureVisible();
                }
            }
            this.FillShaderParameters();
        }

        private void FillShaderParameters()
        {
            string technique = this.SelectedTechnique;
            this.lvParameters.BeginUpdate();
            this.lvParameters.Items.Clear();
            this.cmbAddParameter.Items.Clear();
            this.cmbCopyFrom.Items.Clear();
            ShaderSettings.Entry entry = (technique != null) ? this.shaderData.Find(technique) : null;
            if (technique == null)
            {
                this.lblShaderTechnique.Text = "Pick a technique on the left.";
            }
            else
            {
                string[] fm4 = ShaderSettings.Fm4Parameters(technique);
                this.lblShaderTechnique.Text = string.Format("{0}  -  used in: {1}{2}\nFM4 cars set on it: {3}", technique, this.UsedIn(technique),
                    (entry == null) ? "  -  no settings: shader defaults" : "",
                    (fm4 != null) ? string.Join(", ", fm4) : "nothing (no FM4 car lists it)");
                if (entry != null)
                {
                    foreach (ShaderSettings.Parameter p in entry.Parameters)
                    {
                        List<string> values = new List<string>();
                        foreach (string[] a in ShaderSettings.Attributes(p.Xml))
                        {
                            values.Add((a[0] == "value") ? a[1] : a[0] + " " + a[1]);
                        }
                        ShaderSettings.Problem problem = ShaderSettings.Check(technique, p);
                        string note = (problem == ShaderSettings.Problem.UnknownParameter) ? "ERROR: FM4 has no such parameter"
                            : (problem == ShaderSettings.Problem.UnusualParameter) ? "ERROR: no FM4 car sets it on " + technique : "";
                        ListViewItem item = new ListViewItem(new string[] { p.Name, string.Join("  ", values.ToArray()), note });
                        item.Tag = p;
                        if (problem != ShaderSettings.Problem.None)
                        {
                            item.ForeColor = ForzaTheme.Red;
                        }
                        this.lvParameters.Items.Add(item);
                    }
                }
                // Add: green = FM4 cars set it on this technique, first; then the other FM4 parameters.
                List<string> names = new List<string>(CarPort.ShaderParameterNames);
                names.Sort(StringComparer.Ordinal);
                foreach (bool starred in new bool[] { true, false })
                {
                    foreach (string name in names)
                    {
                        bool seen = fm4 != null && Array.IndexOf(fm4, name) >= 0;
                        if (seen != starred || ShaderSettings.IsSampler(name) || (entry != null && entry.Parameters.Exists(delegate(ShaderSettings.Parameter x) { return x.Name == name; })))
                        {
                            continue;
                        }
                        this.cmbAddParameter.Items.Add(new ParameterChoice(name, starred));
                    }
                }
                if (this.cmbAddParameter.Items.Count > 0)
                {
                    this.cmbAddParameter.SelectedIndex = 0;
                }
            }
            this.lvParameters.EndUpdate();
            this.FillCopyFrom();
        }

        // The cars "Copy missing from" can read: this file, the cars of Media\cars of the game folder the
        // open file is in, cars picked before with Browse..., and Browse... itself.
        private void FillCopyCars()
        {
            string keep = (this.cmbCopyCar.SelectedItem as string) ?? CopyThisFile;
            string root = this.GameRoot;
            string cars = (root == null) ? null : Path.Combine(Path.Combine(root, "Media"), "cars");
            if (cars != null && Directory.Exists(cars))
            {
                foreach (string path in Directory.GetFiles(cars, "*.zip"))
                {
                    this.copyCarPaths[Path.GetFileNameWithoutExtension(path)] = path;
                }
            }
            List<string> names = new List<string>(this.copyCarPaths.Keys);
            names.Sort(StringComparer.OrdinalIgnoreCase);
            this.fillingCopyCars = true;
            this.cmbCopyCar.Items.Clear();
            this.cmbCopyCar.Items.Add(CopyThisFile);
            foreach (string name in names)
            {
                this.cmbCopyCar.Items.Add(name);
            }
            this.cmbCopyCar.Items.Add(CopyBrowse);
            int index = this.cmbCopyCar.Items.IndexOf(keep);
            this.cmbCopyCar.SelectedIndex = (index >= 0 && keep != CopyBrowse) ? index : 0;
            this.fillingCopyCars = false;
            this.LoadCopySource();
        }

        private void CopyCarChanged()
        {
            if (this.fillingCopyCars)
            {
                return;
            }
            if ((this.cmbCopyCar.SelectedItem as string) == CopyBrowse)
            {
                using (OpenFileDialog dialog = new OpenFileDialog())
                {
                    dialog.Title = "Car to read shadersettings.xml from";
                    dialog.Filter = "Car zip (*.zip)|*.zip|All files (*.*)|*.*";
                    string picked = null;
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                    {
                        picked = Path.GetFileNameWithoutExtension(dialog.FileName);
                        this.copyCarPaths[picked] = dialog.FileName;
                    }
                    this.fillingCopyCars = true;
                    this.cmbCopyCar.SelectedItem = CopyThisFile;
                    this.fillingCopyCars = false;
                    this.FillCopyCars();
                    if (picked != null)
                    {
                        this.cmbCopyCar.SelectedItem = picked;
                    }
                    else
                    {
                        this.FillCopyFrom();
                    }
                }
                return;
            }
            this.LoadCopySource();
            this.FillCopyFrom();
        }

        // Reads the chosen car's shadersettings.xml (any folder in the zip), or uses this file.
        private void LoadCopySource()
        {
            string car = this.cmbCopyCar.SelectedItem as string;
            this.copySource = null;
            this.copySourceName = null;
            if (car == null || car == CopyThisFile || !this.copyCarPaths.ContainsKey(car))
            {
                return;
            }
            this.copySourceName = car;
            try
            {
                ForzaZip other = GamePaths.OpenZip(this.copyCarPaths[car]);
                ForzaZip.Entry entry = other.Entries.Find(delegate(ForzaZip.Entry e) { return IsShaderSettings(e); });
                this.copySource = (entry == null) ? new ShaderSettings() : ShaderSettings.Parse(other.Read(entry));
            }
            catch (Exception exception)
            {
                this.copySource = new ShaderSettings();
                this.SetStatus(car + ": " + exception.Message);
            }
        }

        // The entries with settings of the chosen source; the selected technique's own name first.
        private void FillCopyFrom()
        {
            string technique = this.SelectedTechnique;
            ShaderSettings source = this.copySource ?? this.shaderData;
            this.cmbCopyFrom.Items.Clear();
            if (technique != null)
            {
                List<string> names = new List<string>();
                foreach (ShaderSettings.Entry other in source.Entries)
                {
                    if (other.Parameters.Count > 0 && (this.copySource != null || other.Name != technique))
                    {
                        names.Add(other.Name);
                    }
                }
                names.Sort(StringComparer.OrdinalIgnoreCase);
                if (names.Remove(technique))
                {
                    names.Insert(0, technique);
                }
                foreach (string name in names)
                {
                    this.cmbCopyFrom.Items.Add(name);
                }
                if (this.cmbCopyFrom.Items.Count > 0)
                {
                    this.cmbCopyFrom.SelectedIndex = 0;
                }
            }
            this.ShowCopyPreview();
            this.EnableShaderButtons();
        }

        // The values of the entry about to be copied, so another car's settings can be read before copying.
        private void ShowCopyPreview()
        {
            ShaderSettings source = this.copySource ?? this.shaderData;
            string where = (this.copySourceName != null) ? this.copySourceName : "this file";
            ShaderSettings.Entry from = (this.cmbCopyFrom.SelectedItem != null) ? source.Find((string) this.cmbCopyFrom.SelectedItem) : null;
            if (this.SelectedTechnique == null)
            {
                this.lblCopyPreview.Text = "";
            }
            else if (from == null)
            {
                this.lblCopyPreview.Text = (this.copySourceName != null && source.Entries.Count == 0)
                    ? where + " has no shadersettings.xml (shader defaults)." : "Nothing to copy from " + where + ".";
            }
            else
            {
                List<string> lines = new List<string>();
                foreach (ShaderSettings.Parameter p in from.Parameters)
                {
                    List<string> values = new List<string>();
                    foreach (string[] a in ShaderSettings.Attributes(p.Xml))
                    {
                        values.Add((a[0] == "value") ? a[1] : a[0] + " " + a[1]);
                    }
                    lines.Add(p.Name + " " + string.Join(" ", values.ToArray()));
                }
                this.lblCopyPreview.Text = from.Name + " in " + where + ": " + string.Join(";  ", lines.ToArray());
            }
        }

        private void EnableShaderButtons()
        {
            bool technique = this.SelectedTechnique != null;
            bool entry = technique && this.shaderData.Find(this.SelectedTechnique) != null;
            bool parameter = this.lvParameters.SelectedItems.Count > 0;
            this.cmbAddParameter.Enabled = technique;
            this.btnAddParameter.Enabled = technique && this.cmbAddParameter.Items.Count > 0;
            this.btnEditParameter.Enabled = parameter;
            this.btnRemoveParameter.Enabled = parameter;
            this.cmbCopyFrom.Enabled = technique && this.cmbCopyFrom.Items.Count > 0;
            this.btnCopyFrom.Enabled = this.cmbCopyFrom.Enabled;
            this.btnRemoveEntry.Enabled = entry;
        }

        // Stores the edited settings in the archive (pending until Save) and refreshes the lists.
        private void StoreShaderSettings(string status)
        {
            this.zip.Replace(this.shaderEntry, this.shaderData.ToBytes());
            this.RefreshEntryNode(this.shaderEntry);
            this.FillShaderTechniques(null);
            this.SetStatus(status + " (pending: Save to write the archive)");
        }

        // An entry of the add list: green when FM4 cars set the parameter on this technique.
        private class ParameterChoice : IRecommended
        {
            private readonly string name;
            private readonly bool fm4;

            public ParameterChoice(string name, bool fm4)
            {
                this.name = name;
                this.fm4 = fm4;
            }

            public bool Recommended
            {
                get { return this.fm4; }
            }

            public override string ToString()
            {
                return this.name;
            }
        }

        private void AddShaderParameter()
        {
            string technique = this.SelectedTechnique;
            if (technique == null || this.cmbAddParameter.SelectedItem == null)
            {
                return;
            }
            string name = this.cmbAddParameter.SelectedItem.ToString();
            List<string[]> attributes = new List<string[]>();
            foreach (string a in ShaderSettings.AttributeNames(name))
            {
                attributes.Add(new string[] { a, "" });
            }
            // No value is made up: the user types it.
            if (!this.AskShaderValues(technique, name, attributes))
            {
                return;
            }
            ShaderSettings.Entry entry = this.shaderData.Find(technique);
            if (entry == null)
            {
                entry = new ShaderSettings.Entry();
                entry.Name = technique;
                this.shaderData.Entries.Add(entry);
            }
            entry.Parameters.Add(new ShaderSettings.Parameter { Name = name, Xml = ShaderSettings.BuildXml(name, attributes) });
            this.StoreShaderSettings(technique + ": " + name + " added");
        }

        private void EditShaderParameter()
        {
            string technique = this.SelectedTechnique;
            if (technique == null || this.lvParameters.SelectedItems.Count == 0)
            {
                return;
            }
            ShaderSettings.Parameter p = (ShaderSettings.Parameter) this.lvParameters.SelectedItems[0].Tag;
            List<string[]> attributes = ShaderSettings.Attributes(p.Xml);
            if (attributes.Count == 0)
            {
                MessageBox.Show(this, p.Name + " has no values to edit here: use Edit as XML.", "Shader Settings", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!this.AskShaderValues(technique, p.Name, attributes))
            {
                return;
            }
            p.Xml = ShaderSettings.BuildXml(p.Name, attributes);
            this.StoreShaderSettings(technique + ": " + p.Name + " changed");
        }

        // One text box per attribute; every value must be a number.
        private bool AskShaderValues(string technique, string name, List<string[]> attributes)
        {
            Panel content = new Panel();
            content.Size = new Size(300, 30 + (attributes.Count * 28));
            Label label = new Label();
            label.Text = technique + " / " + name + ":";
            label.AutoSize = true;
            label.Location = new Point(0, 4);
            content.Controls.Add(label);
            List<TextBox> boxes = new List<TextBox>();
            for (int i = 0; i < attributes.Count; i++)
            {
                Label caption = new Label();
                caption.Text = attributes[i][0];
                caption.AutoSize = true;
                caption.Location = new Point(0, 32 + (i * 28));
                TextBox box = new TextBox();
                box.Text = attributes[i][1];
                box.Width = 180;
                box.Location = new Point(60, 28 + (i * 28));
                content.Controls.Add(caption);
                content.Controls.Add(box);
                boxes.Add(box);
            }
            if (!this.ShowOptions("Shader Settings", content))
            {
                return false;
            }
            List<string> values = new List<string>();
            foreach (TextBox box in boxes)
            {
                double number;
                if (!double.TryParse(box.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out number))
                {
                    MessageBox.Show(this, "Every value must be a number, with a point for decimals (e.g. 0.8). Nothing changed.", "Shader Settings", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
                }
                values.Add(box.Text.Trim());
            }
            for (int i = 0; i < attributes.Count; i++)
            {
                attributes[i][1] = values[i];
            }
            return true;
        }

        private void RemoveShaderParameter()
        {
            string technique = this.SelectedTechnique;
            ShaderSettings.Entry entry = (technique != null) ? this.shaderData.Find(technique) : null;
            if (entry == null || this.lvParameters.SelectedItems.Count == 0)
            {
                return;
            }
            ShaderSettings.Parameter p = (ShaderSettings.Parameter) this.lvParameters.SelectedItems[0].Tag;
            entry.Parameters.Remove(p);
            if (entry.Parameters.Count == 0)
            {
                this.shaderData.Entries.Remove(entry);   // no empty elements
            }
            this.StoreShaderSettings(technique + ": " + p.Name + " removed");
        }

        // The parameters of another entry that this technique does not set yet (its own stay).
        private void CopyShaderEntry()
        {
            string technique = this.SelectedTechnique;
            ShaderSettings source = this.copySource ?? this.shaderData;
            ShaderSettings.Entry from = (this.cmbCopyFrom.SelectedItem != null) ? source.Find((string) this.cmbCopyFrom.SelectedItem) : null;
            if (technique == null || from == null || (this.copySource == null && from.Name == technique))
            {
                return;
            }
            ShaderSettings.Entry entry = this.shaderData.Find(technique);
            if (entry == null)
            {
                entry = new ShaderSettings.Entry();
                entry.Name = technique;
                this.shaderData.Entries.Add(entry);
            }
            int added = 0;
            foreach (ShaderSettings.Parameter p in from.Parameters)
            {
                if (!entry.Parameters.Exists(delegate(ShaderSettings.Parameter x) { return x.Name == p.Name; }))
                {
                    entry.Parameters.Add(new ShaderSettings.Parameter { Name = p.Name, Xml = p.Xml });
                    added++;
                }
            }
            if (entry.Parameters.Count == 0)
            {
                this.shaderData.Entries.Remove(entry);
            }
            this.StoreShaderSettings(string.Format("{0}: {1} parameter(s) copied from {2}{3}", technique, added, from.Name,
                (this.copySourceName != null) ? " of " + this.copySourceName : ""));
        }

        private void RemoveShaderEntry()
        {
            string technique = this.SelectedTechnique;
            ShaderSettings.Entry entry = (technique != null) ? this.shaderData.Find(technique) : null;
            if (entry == null)
            {
                return;
            }
            if (MessageBox.Show(this, "Remove all settings of " + technique + "? It then runs with its shader's defaults.", "Shader Settings", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }
            this.shaderData.Entries.Remove(entry);
            this.StoreShaderSettings(technique + ": settings removed");
        }

        private void RemoveUnusedShaderEntries()
        {
            List<string> removed = new List<string>();
            foreach (ShaderSettings.Entry entry in new List<ShaderSettings.Entry>(this.shaderData.Entries))
            {
                if (!this.shaderUsed.ContainsKey(entry.Name))
                {
                    this.shaderData.Entries.Remove(entry);
                    removed.Add(entry.Name);
                }
            }
            if (removed.Count == 0)
            {
                this.SetStatus("No unused entries.");
                return;
            }
            this.StoreShaderSettings("Removed unused: " + string.Join(", ", removed.ToArray()));
        }
    }
}
