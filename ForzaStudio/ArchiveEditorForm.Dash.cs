namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Globalization;
    using System.IO;
    using System.Windows.Forms;

    // Digital dash view (Anark .fbf + .bgf + .bsg of DigitalGauge\Dash_<car>): element tree, 2D preview, properties.
    // Selecting any of the three files shows the whole dash. The edit panel changes position, rotation, scale, pivot,
    // opacity, colour, text, font and texture in place (AnarkDash writes each value where it lives, nothing is added);
    // every edit makes the changed files pending entries of the archive, written with File > Save.
    public partial class ArchiveEditorForm
    {
        private Panel pnlDash;
        private SplitContainer splitDash;
        private TreeView tvDash;
        private AnarkDashCanvas dashCanvas;
        private ListView lvDash;
        private ToolStripLabel lblDashInfo;
        private bool dashSelecting;

        // edit panel
        private TableLayoutPanel tblDashEdit;
        private readonly List<DashRow> dashTransformRows = new List<DashRow>();
        private DashRow dashColorRow, dashTextRow, dashFontRow, dashTextureRow;
        private ForzaZip.Entry dashFbfEntry, dashBgfEntry, dashBsgEntry;
        private AnarkDash.Node dashEditNode;
        private bool dashLoading;

        // One line of the edit panel: caption, editor, and where the value is written (bgf, bsg, fbf; "-" read only).
        private sealed class DashRow
        {
            public Label Caption;
            public Control Editor;
            public Label Source;
            public string Property;                // transform rows: graph property name
            public float Factor = 1;               // shown = stored * Factor (rotation in degrees)

            public void Show(bool visible, string source)
            {
                this.Caption.Visible = this.Editor.Visible = this.Source.Visible = visible;
                this.Editor.Enabled = source != null;
                this.Source.Text = source ?? "-";
            }
        }

        private void BuildDashView()
        {
            this.pnlDash = new Panel();
            this.pnlDash.Dock = DockStyle.Fill;

            SplitContainer outer = this.splitDash = new SplitContainer();
            outer.Dock = DockStyle.Fill;
            outer.FixedPanel = FixedPanel.Panel1;

            this.tvDash = new TreeView();
            this.tvDash.Dock = DockStyle.Fill;
            this.tvDash.HideSelection = false;
            this.tvDash.AfterSelect += delegate(object sender, TreeViewEventArgs e)
            {
                if (this.dashSelecting)
                {
                    return;
                }
                this.dashCanvas.Selected = (e.Node != null) ? e.Node.Tag as AnarkDash.Node : null;
                this.dashCanvas.Invalidate();
                this.ShowDashProperties(this.dashCanvas.Selected);
            };
            outer.Panel1.Controls.Add(this.tvDash);

            SplitContainer inner = new SplitContainer();
            inner.Dock = DockStyle.Fill;
            inner.FixedPanel = FixedPanel.Panel2;

            this.dashCanvas = new AnarkDashCanvas();
            this.dashCanvas.Dock = DockStyle.Fill;
            this.dashCanvas.SelectionChanged += delegate { this.SelectDashNode(this.dashCanvas.Selected); };

            this.lvDash = new ListView();
            this.lvDash.Dock = DockStyle.Fill;
            this.lvDash.View = View.Details;
            this.lvDash.FullRowSelect = true;
            this.lvDash.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            this.lvDash.Columns.Add("Property", 120);
            this.lvDash.Columns.Add("Value", 110);
            this.lvDash.Columns.Add("From", 60);

            ToolStrip bar = new ToolStrip();
            bar.GripStyle = ToolStripGripStyle.Hidden;
            bar.Items.Add(new ToolStripButton("Fit (double-click)", null, delegate { this.dashCanvas.Fit(); }));
            ToolStripButton outlines = new ToolStripButton("Outlines");
            outlines.CheckOnClick = true;
            outlines.CheckedChanged += delegate
            {
                this.dashCanvas.ShowOutlines = outlines.Checked;
                this.dashCanvas.Invalidate();
            };
            bar.Items.Add(outlines);
            this.lblDashInfo = new ToolStripLabel();
            this.lblDashInfo.Alignment = ToolStripItemAlignment.Right;
            bar.Items.Add(this.lblDashInfo);

            this.BuildDashEditor();

            inner.Panel1.Controls.Add(this.dashCanvas);
            inner.Panel2.Controls.Add(this.lvDash);
            inner.Panel2.Controls.Add(this.tblDashEdit);
            outer.Panel2.Controls.Add(inner);
            this.pnlDash.Controls.Add(outer);
            this.pnlDash.Controls.Add(bar);
            this.pnlDash.Resize += delegate
            {
                if (inner.Width > 400)
                {
                    inner.SplitterDistance = inner.Width - 300;
                }
            };
        }

        private void BuildDashEditor()
        {
            TableLayoutPanel table = this.tblDashEdit = new TableLayoutPanel();
            table.Dock = DockStyle.Top;
            table.AutoSize = true;
            table.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            table.ColumnCount = 3;
            table.Padding = new Padding(2, 2, 2, 6);
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34));

            // name, caption, factor, step, minimum, maximum
            object[][] transforms = new object[][]
            {
                new object[] { "position.x", "position x", 1f, 1m, -100000m, 100000m },
                new object[] { "position.y", "position y", 1f, 1m, -100000m, 100000m },
                new object[] { "position.z", "position z", 1f, 1m, -100000m, 100000m },
                new object[] { "rotation.z", "rotation (deg)", (float) (180 / Math.PI), 1m, -100000m, 100000m },
                new object[] { "scale.x", "scale x", 1f, 0.01m, -10000m, 10000m },
                new object[] { "scale.y", "scale y", 1f, 0.01m, -10000m, 10000m },
                new object[] { "pivot.x", "pivot x", 1f, 1m, -100000m, 100000m },
                new object[] { "pivot.y", "pivot y", 1f, 1m, -100000m, 100000m },
                new object[] { "opacity", "opacity", 1f, 1m, 0m, 100m },
            };
            foreach (object[] t in transforms)
            {
                NumericUpDown number = new NumericUpDown();
                number.DecimalPlaces = 3;
                number.Increment = (decimal) t[3];
                number.Minimum = (decimal) t[4];
                number.Maximum = (decimal) t[5];
                DashRow row = this.AddDashEditRow((string) t[1], number);
                row.Property = (string) t[0];
                row.Factor = (float) t[2];
                number.ValueChanged += delegate { this.DashNumberChanged(row); };
                this.dashTransformRows.Add(row);
            }

            Button color = new Button();
            color.FlatStyle = FlatStyle.Flat;
            color.TextAlign = ContentAlignment.MiddleLeft;
            this.dashColorRow = this.AddDashEditRow("colour", color);
            color.Click += delegate { this.DashPickColor(color); };

            TextBox text = new TextBox();
            this.dashTextRow = this.AddDashEditRow("text", text);
            text.TextChanged += delegate
            {
                AnarkDash.Node node = this.dashEditNode;
                if (!this.dashLoading && node != null && text.Text != this.DashString(node, "textstring"))
                {
                    this.DashEdited(this.dashCanvas.Dash.SetText(node, text.Text), "text");
                }
            };

            ComboBox font = new ComboBox();
            font.DropDownStyle = ComboBoxStyle.DropDown;
            this.dashFontRow = this.AddDashEditRow("font", font);
            this.HookDashCombo(font, delegate(string value)
            {
                AnarkDash.Node node = this.dashEditNode;
                if (node != null && value != this.DashString(node, "font"))
                {
                    this.DashEdited(this.dashCanvas.Dash.SetFont(node, value), "font");
                }
            });

            ComboBox texture = new ComboBox();
            texture.DropDownStyle = ComboBoxStyle.DropDown;
            this.dashTextureRow = this.AddDashEditRow("texture", texture);
            this.HookDashCombo(texture, delegate(string value)
            {
                AnarkDash.Node node = this.dashEditNode;
                AnarkDash.Node image = (node != null) ? this.dashCanvas.Dash.TextureNode(node) : null;
                if (image != null && value != image.Record.Path)
                {
                    this.DashEdited(this.dashCanvas.Dash.SetTexture(node, value), "texture");
                }
            });

            this.ShowDashEditor(null);
        }

        private DashRow AddDashEditRow(string caption, Control editor)
        {
            int index = this.tblDashEdit.RowCount++;
            this.tblDashEdit.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            DashRow row = new DashRow();
            row.Caption = new Label();
            row.Caption.Text = caption;
            row.Caption.AutoSize = true;
            row.Caption.Anchor = AnchorStyles.Left;
            row.Editor = editor;
            editor.Dock = DockStyle.Fill;
            editor.Margin = new Padding(1);
            row.Source = new Label();
            row.Source.AutoSize = true;
            row.Source.Anchor = AnchorStyles.Left;
            row.Source.ForeColor = SystemColors.GrayText;
            this.tblDashEdit.Controls.Add(row.Caption, 0, index);
            this.tblDashEdit.Controls.Add(editor, 1, index);
            this.tblDashEdit.Controls.Add(row.Source, 2, index);
            return row;
        }

        // Editable combo: applied when an item is picked, on Enter, or when the focus leaves it.
        private void HookDashCombo(ComboBox combo, Action<string> apply)
        {
            combo.SelectionChangeCommitted += delegate
            {
                if (!this.dashLoading && combo.SelectedItem != null)
                {
                    apply((string) combo.SelectedItem);
                }
            };
            combo.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    if (!this.dashLoading)
                    {
                        apply(combo.Text);
                    }
                }
            };
            combo.Validated += delegate
            {
                if (!this.dashLoading)
                {
                    apply(combo.Text);
                }
            };
        }

        private void ShowDash(ForzaZip.Entry entry, byte[] data)
        {
            string stem = entry.Name.Substring(0, entry.Name.Length - Path.GetExtension(entry.Name).Length);
            ForzaZip.Entry fbfEntry = this.zip.Find(stem + ".fbf"), bgfEntry = this.zip.Find(stem + ".bgf"), bsgEntry = this.zip.Find(stem + ".bsg");
            if (fbfEntry == null || bgfEntry == null)
            {
                this.ShowInfo(entry, "the dash needs both " + Path.GetFileName(stem) + ".fbf and .bgf next to each other.");
                return;
            }
            AnarkFbf fbf = AnarkFbf.Read(fbfEntry == entry ? data : this.zip.Read(fbfEntry));
            AnarkBgf bgf = AnarkBgf.Read(bgfEntry == entry ? data : this.zip.Read(bgfEntry));
            AnarkBsg bsg = (bsgEntry != null) ? AnarkBsg.Read(bsgEntry == entry ? data : this.zip.Read(bsgEntry)) : null;
            AnarkDash dash = new AnarkDash(fbf, bgf, bsg);
            this.dashFbfEntry = fbfEntry;
            this.dashBgfEntry = bgfEntry;
            this.dashBsgEntry = bsgEntry;

            this.ShowView(this.pnlDash);
            this.splitDash.SplitterDistance = Math.Min(260, Math.Max(this.splitDash.Width / 3, 50));
            this.dashCanvas.SetDash(dash, new AnarkDashCanvas.TextureLoader(this.LoadDashTexture));

            this.tvDash.BeginUpdate();
            this.tvDash.Nodes.Clear();
            if (dash.Root != null)
            {
                TreeNode root = this.AddDashNode(this.tvDash.Nodes, dash.Root);
                root.Expand();
                foreach (TreeNode child in root.Nodes)
                {
                    child.Expand();
                }
            }
            this.tvDash.EndUpdate();
            this.ShowDashProperties(null);

            int missing = 0;
            foreach (AnarkDash.Item item in dash.Items)
            {
                if (item.Texture != null && this.dashCanvas.GetTexture(item.Texture) == null)
                {
                    missing++;
                }
            }
            this.lblDashInfo.Text = string.Format("FBF 0x{0:X} ({1})   {2} elements, {3} meshes{4}{5}",
                fbf.Version, fbf.IsFH2 ? "FH2" : "FM4", bgf.Elements.Count, fbf.Meshes.Count,
                (bsg == null) ? ", no .bsg" : "", (missing > 0) ? string.Format(", {0} textures missing", missing) : "");
        }

        private TreeNode AddDashNode(TreeNodeCollection parent, AnarkDash.Node node)
        {
            string type = (node.Record != null) ? node.Record.Type.ToString() : node.Element.Type.ToString();
            TreeNode tree = new TreeNode(string.Format("{0}  [{1}]", node.Name, type));
            tree.Tag = node;
            if (node.Element.Type == AnarkBgf.ElementType.Behaviour)
            {
                tree.ForeColor = SystemColors.GrayText;
            }
            parent.Add(tree);
            foreach (AnarkDash.Node child in node.Children)
            {
                this.AddDashNode(tree.Nodes, child);
            }
            return tree;
        }

        private void SelectDashNode(AnarkDash.Node node)
        {
            this.dashSelecting = true;
            try
            {
                this.tvDash.SelectedNode = (node != null) ? FindDashTreeNode(this.tvDash.Nodes, node) : null;
            }
            finally
            {
                this.dashSelecting = false;
            }
            this.ShowDashProperties(node);
        }

        private static TreeNode FindDashTreeNode(TreeNodeCollection nodes, AnarkDash.Node node)
        {
            foreach (TreeNode tree in nodes)
            {
                if (tree.Tag == node)
                {
                    return tree;
                }
                TreeNode found = FindDashTreeNode(tree.Nodes, node);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }

        // Texture of a dash image: digitalgauge\<name>.xds of this zip, or of the car the path names
        // (a dash taken from another car loads that car's textures, see anark notes).
        private Bitmap LoadDashTexture(string gamePath)
        {
            string car;
            string name = AnarkDash.TextureEntry(gamePath, out car);
            if (name == null)
            {
                return null;
            }
            ForzaZip source = this.zip;
            ForzaZip.Entry texture = this.zip.Find(name);
            if (texture == null && car != null && !this.zip.IsFolder)
            {
                string other = Path.Combine(Path.GetDirectoryName(this.zip.FilePath), car + ".zip");
                if (File.Exists(other))
                {
                    source = new ForzaZip(other);
                    texture = source.Find(name);
                }
            }
            return (texture != null) ? new XdsTexture(source.Read(texture)).GetLevelBitmap(0) : null;
        }

        private void ShowDashProperties(AnarkDash.Node node)
        {
            this.ShowDashEditor(node);
            this.FillDashList(node);
        }

        private void FillDashList(AnarkDash.Node node)
        {
            this.lvDash.BeginUpdate();
            this.lvDash.Items.Clear();
            if (node != null)
            {
                AnarkFbf.Record rec = node.Record;
                this.AddDashRow("element", node.Element.Type + " #" + node.Index, "bgf");
                this.AddDashRow("id", node.Element.Id.ToString(), "bgf");
                if (rec != null)
                {
                    this.AddDashRow("name", rec.Name, "fbf");
                    this.AddDashRow("type", rec.Type.ToString(), "fbf");
                    if (rec.Type == AnarkFbf.RecordType.Model)
                    {
                        this.AddDashRow("mesh", AnarkFbf.ReadU32(rec.Body, 0).ToString(), "fbf");
                    }
                    else if (rec.Type == AnarkFbf.RecordType.Image)
                    {
                        this.AddDashRow("path", rec.Path, "fbf");
                    }
                    else if (rec.Type == AnarkFbf.RecordType.Text)
                    {
                        this.AddDashRow("font", rec.Font, "fbf");
                        if (rec.Text.Length > 0)
                        {
                            this.AddDashRow("text", rec.Text, "fbf");
                        }
                    }
                    else if (rec.Type == AnarkFbf.RecordType.Material)
                    {
                        this.AddDashRow("diffuse", string.Format("{0} {1} {2} {3}", F(AnarkFbf.ReadFloat(rec.Body, 0)), F(AnarkFbf.ReadFloat(rec.Body, 4)), F(AnarkFbf.ReadFloat(rec.Body, 8)), F(AnarkFbf.ReadFloat(rec.Body, 12))), "fbf");
                    }
                }
                AnarkBsg.Node b = node.Base;
                if (b != null)
                {
                    this.AddDashRow("position", V3(b.Values, AnarkBsg.Node.Position), "bsg");
                    this.AddDashRow("rotation", V3(b.Values, AnarkBsg.Node.Rotation), "bsg");
                    this.AddDashRow("scale", V3(b.Values, AnarkBsg.Node.Scale), "bsg");
                    this.AddDashRow("pivot", V3(b.Values, AnarkBsg.Node.Pivot), "bsg");
                    this.AddDashRow("opacity", F(b.Values[AnarkBsg.Node.Opacity]), "bsg");
                }
                foreach (AnarkBgf.Property p in node.Element.Properties)
                {
                    string name = p.Name ?? p.Hash.ToString("X8");
                    string value;
                    switch (p.Kind)
                    {
                        case AnarkBgf.ValueKind.Float:
                            value = F(p.Float);
                            break;
                        case AnarkBgf.ValueKind.Bool:
                            value = (p.Value != 0) ? "true" : "false";
                            break;
                        case AnarkBgf.ValueKind.String:
                            value = "\"" + this.dashCanvas.Dash.Bgf.GetString(p) + "\"";
                            break;
                        default:
                            value = ((int) p.Value).ToString();
                            break;
                    }
                    this.AddDashRow(name, value, p.IsBehaviourParameter ? "behaviour" : "bgf");
                }
            }
            this.lvDash.EndUpdate();
        }

        // Fills the edit panel for a node: only the rows that apply to it, disabled where the value cannot be
        // changed without adding something to the files.
        private void ShowDashEditor(AnarkDash.Node node)
        {
            this.dashEditNode = node;
            AnarkDash dash = this.dashCanvas.Dash;
            this.dashLoading = true;
            this.tblDashEdit.SuspendLayout();
            try
            {
                bool spatial = node != null && (node.Element.Type == AnarkBgf.ElementType.Node || node.Element.Type == AnarkBgf.ElementType.Text || node.Element.Type == AnarkBgf.ElementType.Component);
                foreach (DashRow row in this.dashTransformRows)
                {
                    string source = spatial ? dash.TransformSource(node, row.Property) : null;
                    row.Show(spatial, source);
                    if (spatial)
                    {
                        SetDashNumber((NumericUpDown) row.Editor, dash.GetTransform(node, row.Property) * row.Factor);
                    }
                }

                string colorSource = (node != null) ? dash.ColorSource(node) : null;
                bool colored = node != null && node.Record != null && (node.Record.Type == AnarkFbf.RecordType.Text || node.Record.Type == AnarkFbf.RecordType.Model);
                this.dashColorRow.Show(colored, colorSource);
                if (colored)
                {
                    Color c = dash.GetColor(node);
                    this.dashColorRow.Editor.BackColor = c;
                    this.dashColorRow.Editor.ForeColor = (c.GetBrightness() > 0.5f) ? Color.Black : Color.White;
                    this.dashColorRow.Editor.Text = GameColor.Hex(c);
                }

                bool text = node != null && node.Record != null && node.Record.Type == AnarkFbf.RecordType.Text;
                string textValue = text ? this.DashString(node, "textstring") : null;
                string fontValue = text ? this.DashString(node, "font") : null;
                this.dashTextRow.Show(text, (textValue != null) ? AnarkDash.SourceGraph : null);
                this.dashFontRow.Show(text, (fontValue != null) ? AnarkDash.SourceGraph : null);
                this.dashTextRow.Editor.Text = textValue ?? "";
                ComboBox font = (ComboBox) this.dashFontRow.Editor;
                font.Items.Clear();
                if (text)
                {
                    font.Items.AddRange(this.DashFonts().ToArray());
                }
                font.Text = fontValue ?? "";

                AnarkDash.Node image = (node != null) ? dash.TextureNode(node) : null;
                this.dashTextureRow.Show(image != null, (image != null) ? AnarkDash.SourceScene : null);
                ComboBox texture = (ComboBox) this.dashTextureRow.Editor;
                texture.Items.Clear();
                if (image != null)
                {
                    texture.Items.AddRange(this.DashTextures(image.Record.Path).ToArray());
                }
                texture.Text = (image != null) ? image.Record.Path : "";
            }
            finally
            {
                this.tblDashEdit.ResumeLayout();
                this.dashLoading = false;
            }
        }

        private static void SetDashNumber(NumericUpDown number, float value)
        {
            decimal d = (float.IsNaN(value) || float.IsInfinity(value)) ? 0m : (decimal) Math.Max(-1e9, Math.Min(1e9, value));
            number.Value = Math.Max(number.Minimum, Math.Min(number.Maximum, d));
        }

        // The graph string of a text node, or null when it has none.
        private string DashString(AnarkDash.Node node, string name)
        {
            AnarkBgf.Property p = node.Element.Find(name);
            return (p != null && p.Kind == AnarkBgf.ValueKind.String) ? this.dashCanvas.Dash.Bgf.GetString(p) : null;
        }

        // Fonts used by the texts of this dash (other names must be fonts the game has).
        private List<string> DashFonts()
        {
            List<string> fonts = new List<string>();
            foreach (AnarkDash.Node n in this.dashCanvas.Dash.Nodes)
            {
                if (n.Record != null && n.Record.Type == AnarkFbf.RecordType.Text)
                {
                    string font = this.DashString(n, "font");
                    if (!string.IsNullOrEmpty(font) && !fonts.Contains(font))
                    {
                        fonts.Add(font);
                    }
                }
            }
            fonts.Sort(StringComparer.OrdinalIgnoreCase);
            return fonts;
        }

        // Images of this dash, plus every digitalgauge\<name>.xds of the zip under the folder of the current path.
        private List<string> DashTextures(string current)
        {
            List<string> paths = new List<string>();
            foreach (AnarkDash.Node n in this.dashCanvas.Dash.Nodes)
            {
                if (n.Record != null && n.Record.Type == AnarkFbf.RecordType.Image && !paths.Contains(n.Record.Path))
                {
                    paths.Add(n.Record.Path);
                }
            }
            int slash = current.LastIndexOf('\\');
            if (slash > 0)
            {
                string folder = current.Substring(0, slash + 1);
                foreach (ForzaZip.Entry e in this.zip.Entries)
                {
                    string name = e.Name.Replace('\\', '/');
                    if (name.StartsWith("digitalgauge/", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".xds", StringComparison.OrdinalIgnoreCase)
                        && name.IndexOf('/', "digitalgauge/".Length) < 0)
                    {
                        string stem = name.Substring("digitalgauge/".Length, name.Length - "digitalgauge/".Length - 4);
                        string path = folder + stem.ToUpperInvariant() + ".TGA";
                        if (!paths.Exists(delegate(string p) { return string.Equals(p, path, StringComparison.OrdinalIgnoreCase); }))
                        {
                            paths.Add(path);
                        }
                    }
                }
            }
            paths.Sort(StringComparer.OrdinalIgnoreCase);
            return paths;
        }

        private void DashNumberChanged(DashRow row)
        {
            AnarkDash.Node node = this.dashEditNode;
            if (this.dashLoading || node == null)
            {
                return;
            }
            float value = (float) ((NumericUpDown) row.Editor).Value / row.Factor;
            this.DashEdited(this.dashCanvas.Dash.SetTransform(node, row.Property, value), row.Property);
        }

        private void DashPickColor(Button button)
        {
            AnarkDash.Node node = this.dashEditNode;
            if (node == null)
            {
                return;
            }
            Rectangle near = button.RectangleToScreen(button.ClientRectangle);
            near = this.RectangleToClient(near);
            ForzaColorPicker.Popup(this, near, this.dashCanvas.Dash.GetColor(node), delegate(Color color)
            {
                if (this.dashEditNode != node)
                {
                    return;
                }
                bool done = this.dashCanvas.Dash.SetColor(node, color);
                if (done)
                {
                    button.BackColor = color;
                    button.ForeColor = (color.GetBrightness() > 0.5f) ? Color.Black : Color.White;
                    button.Text = GameColor.Hex(color);
                }
                this.DashEdited(done, "colour");
            });
        }

        // After an edit: preview, property list, and the changed files as pending entries of the archive.
        private void DashEdited(bool done, string what)
        {
            if (!done)
            {
                this.SetStatus("Dash: " + what + " cannot be changed here.");
                return;
            }
            AnarkDash dash = this.dashCanvas.Dash;
            List<string> files = new List<string>();
            if (dash.FbfChanged)
            {
                this.zip.Replace(this.dashFbfEntry, dash.Fbf.Write());
                this.RefreshDashEntry(this.dashFbfEntry, files);
            }
            if (dash.BgfChanged)
            {
                this.zip.Replace(this.dashBgfEntry, dash.Bgf.Write());
                this.RefreshDashEntry(this.dashBgfEntry, files);
            }
            if (dash.BsgChanged && this.dashBsgEntry != null)
            {
                this.zip.Replace(this.dashBsgEntry, dash.Bsg.Write());
                this.RefreshDashEntry(this.dashBsgEntry, files);
            }
            this.dashCanvas.Invalidate();
            this.FillDashList(this.dashEditNode);
            this.SetStatus(string.Format("Dash: {0} of {1} changed in {2} (pending: Save to write the archive)",
                what, this.dashEditNode.Name, string.Join(", ", files.ToArray())));
        }

        private void RefreshDashEntry(ForzaZip.Entry entry, List<string> files)
        {
            files.Add(EntryFileName(entry.Name));
            TreeNode node = this.FindEntryNode(entry);
            if (node != null)
            {
                this.RefreshNode(node);
            }
            else
            {
                this.UpdateTitle();
            }
        }

        private void AddDashRow(string name, string value, string source)
        {
            ListViewItem item = new ListViewItem(name);
            item.SubItems.Add(value);
            item.SubItems.Add(source);
            this.lvDash.Items.Add(item);
        }

        private static string F(float v)
        {
            return v.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string V3(float[] values, int start)
        {
            return F(values[start]) + ", " + F(values[start + 1]) + ", " + F(values[start + 2]);
        }
    }
}
