namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Globalization;
    using System.IO;
    using System.Windows.Forms;

    // Digital dash view (Anark .fbf + .bgf + .bsg of DigitalGauge\Dash_<car>): element tree, 2D preview, properties.
    // Read only for now. Selecting any of the three files shows the whole dash.
    public partial class ArchiveEditorForm
    {
        private Panel pnlDash;
        private SplitContainer splitDash;
        private TreeView tvDash;
        private AnarkDashCanvas dashCanvas;
        private ListView lvDash;
        private ToolStripLabel lblDashInfo;
        private bool dashSelecting;

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

            inner.Panel1.Controls.Add(this.dashCanvas);
            inner.Panel2.Controls.Add(this.lvDash);
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
