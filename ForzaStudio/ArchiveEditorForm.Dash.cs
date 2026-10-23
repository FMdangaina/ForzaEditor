namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.IO;
    using System.Windows.Forms;

    // Digital dash view (Anark .fbf + .bgf + .bsg of DigitalGauge\Dash_<car>): element tree, 2D preview, property inspector.
    // Selecting any of the three files shows the whole dash. The inspector (a PropertyGrid, see DashInspector) lists the
    // values of the selected element with the editor that fits each: drop-down lists, check boxes, numbers, text, colour.
    // On the preview, a drag moves the element and the handles resize it. AnarkDash writes each value where it lives,
    // nothing is added; every edit makes the changed files pending entries of the archive, written with File > Save.
    public partial class ArchiveEditorForm
    {
        private Panel pnlDash;
        private SplitContainer splitDash;
        private TreeView tvDash;
        private AnarkDashCanvas dashCanvas;
        private PropertyGrid pgDash;
        private ToolStripLabel lblDashInfo;
        private bool dashSelecting;
        private ForzaZip.Entry dashFbfEntry, dashBgfEntry, dashBsgEntry;

        // name, caption, factor (shown = stored * factor: rotation in degrees), minimum, maximum
        private static readonly object[][] DashTransforms = new object[][]
        {
            new object[] { "position.x", "Position X", 1f, -100000f, 100000f },
            new object[] { "position.y", "Position Y", 1f, -100000f, 100000f },
            new object[] { "position.z", "Position Z", 1f, -100000f, 100000f },
            new object[] { "rotation.z", "Rotation (deg)", (float) (180 / Math.PI), -100000f, 100000f },
            new object[] { "scale.x", "Scale X", 1f, -10000f, 10000f },
            new object[] { "scale.y", "Scale Y", 1f, -10000f, 10000f },
            new object[] { "pivot.x", "Pivot X", 1f, -100000f, 100000f },
            new object[] { "pivot.y", "Pivot Y", 1f, -100000f, 100000f },
            new object[] { "opacity", "Opacity (0-100)", 1f, 0f, 100f },
        };

        // Graph properties shown in the first two groups of the inspector, not again in the last one.
        private static readonly string[] DashHandledNames = new string[]
        {
            "position.x", "position.y", "position.z", "rotation.z", "scale.x", "scale.y", "pivot.x", "pivot.y", "opacity",
            "textstring", "font", "textcolor.r", "textcolor.g", "textcolor.b",
        };

        // Names of the values of the integer properties that are choices.
        private static string[] DashEnumLabels(string name)
        {
            switch (name)
            {
                case "horzalign": return new string[] { "Left", "Center", "Right" };
                case "vertalign": return new string[] { "Top", "Middle", "Bottom" };
            }
            return null;
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
            this.dashCanvas.DuplicateRequested += delegate { this.DuplicateDashSelection(); };
            this.dashCanvas.Dragging +=delegate { this.pgDash.Refresh(); };
            this.dashCanvas.Edited += delegate(object sender, DashEditEventArgs e)
            {
                this.DashEdited(true, e.What, true);
            };
            this.dashCanvas.EditBlocked += delegate(object sender, DashEditEventArgs e) { this.SetStatus("Dash: " + e.What + "."); };

            this.pgDash = new PropertyGrid();
            this.pgDash.Dock = DockStyle.Fill;
            this.pgDash.PropertySort = PropertySort.Categorized;
            this.pgDash.ToolbarVisible = false;
            this.pgDash.HelpVisible = true;

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
            ToolStripDropDownButton align = new ToolStripDropDownButton("Align (select several)");
            string[][] aligns = new string[][]
            {
                new string[] { "Left", "left" }, new string[] { "Center horizontally", "hcenter" }, new string[] { "Right", "right" },
                new string[] { "Top", "top" }, new string[] { "Middle", "vcenter" }, new string[] { "Bottom", "bottom" },
                new string[] { "Spread horizontally (3+)", "hspread" }, new string[] { "Spread vertically (3+)", "vspread" },
            };
            foreach (string[] a in aligns)
            {
                string mode = a[1];
                align.DropDownItems.Add(new ToolStripMenuItem(a[0], null, delegate { this.dashCanvas.Align(mode); }));
            }
            bar.Items.Add(align);
            bar.Items.Add(new ToolStripButton("Duplicate (Ctrl+D)", null, delegate { this.DuplicateDashSelection(); }));
            this.lblDashInfo = new ToolStripLabel();
            this.lblDashInfo.Alignment = ToolStripItemAlignment.Right;
            bar.Items.Add(this.lblDashInfo);

            inner.Panel1.Controls.Add(this.dashCanvas);
            inner.Panel2.Controls.Add(this.pgDash);
            outer.Panel2.Controls.Add(inner);
            this.pnlDash.Controls.Add(outer);
            this.pnlDash.Controls.Add(bar);
            this.pnlDash.Resize += delegate
            {
                if (inner.Width > 500)
                {
                    inner.SplitterDistance = inner.Width - 340;
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

            this.FillDashTree(dash);
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

        private void FillDashTree(AnarkDash dash)
        {
            this.dashSelecting = true;
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
            this.dashSelecting = false;
        }

        // Ctrl+D / Duplicate: copies the selected elements with everything under them (see AnarkDash.Duplicate).
        private void DuplicateDashSelection()
        {
            AnarkDash dash = this.dashCanvas.Dash;
            List<AnarkDash.Node> nodes = (dash != null) ? this.dashCanvas.TopSelection() : new List<AnarkDash.Node>();
            if (nodes.Count == 0)
            {
                this.SetStatus("Dash: select what to duplicate.");
                return;
            }
            string name;
            bool sameNames;
            if (!this.AskDashCopy(nodes, out name, out sameNames))
            {
                return;
            }
            List<int> originals = new List<int>();
            foreach (AnarkDash.Node n in nodes)
            {
                originals.Add(n.Index);       // element indexes stay valid: copies are added at the end
            }
            List<int> created = new List<int>();
            foreach (int index in originals)
            {
                AnarkDash.Node original = dash.Nodes[index];
                AnarkDash.Node copy = dash.Duplicate(original, (nodes.Count == 1) ? name : original.Name + name, sameNames);
                if (copy == null)
                {
                    this.SetStatus("Dash: " + dash.Blocked + ".");
                    continue;
                }
                created.Add(copy.Index);
            }
            if (created.Count == 0)
            {
                return;
            }
            foreach (int index in created)
            {
                dash.Nudge(dash.Nodes[index], 20, -20);        // beside the original; stays on it when the position is not editable
            }
            this.FillDashTree(dash);
            this.dashCanvas.Selection.Clear();
            foreach (int index in created)
            {
                this.dashCanvas.Selection.Add(dash.Nodes[index]);
            }
            this.dashCanvas.Invalidate();
            this.SelectDashNode(this.dashCanvas.Selected);
            this.DashEdited(true, (created.Count == 1) ? "duplicate" : created.Count + " duplicates", false);
        }

        private bool AskDashCopy(List<AnarkDash.Node> nodes, out string name, out bool sameNames)
        {
            bool single = nodes.Count == 1;
            using (Form dialog = new Form())
            {
                dialog.Text = single ? "Duplicate " + nodes[0].Name : "Duplicate " + nodes.Count + " elements";
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.MinimizeBox = dialog.MaximizeBox = false;
                dialog.ClientSize = new Size(420, 170);
                Label caption = new Label();
                caption.Text = single ? "Name of the copy:" : "Text added to each name:";
                caption.SetBounds(12, 12, 396, 18);
                TextBox box = new TextBox();
                box.Text = single ? nodes[0].Name + "_2" : "_2";
                box.SetBounds(12, 34, 396, 22);
                CheckBox same = new CheckBox();
                same.Text = "Keep the original names inside (the game may animate or feed the copy like the original)";
                same.SetBounds(12, 66, 396, 36);
                same.CheckedChanged += delegate { box.Enabled = !same.Checked; };
                Label note = new Label();
                note.Text = "The copy shares meshes and textures, is added at the end of the dash and sits 20 units off the original.";
                note.SetBounds(12, 104, 396, 32);
                Button ok = new Button();
                ok.Text = "Duplicate";
                ok.DialogResult = DialogResult.OK;
                ok.SetBounds(236, 136, 84, 26);
                Button cancel = new Button();
                cancel.Text = "Cancel";
                cancel.DialogResult = DialogResult.Cancel;
                cancel.SetBounds(326, 136, 84, 26);
                dialog.Controls.AddRange(new Control[] { caption, box, same, note, ok, cancel });
                dialog.AcceptButton = ok;
                dialog.CancelButton = cancel;
                ForzaTheme.Apply(dialog);
                bool accepted = dialog.ShowDialog(this) == DialogResult.OK && (same.Checked || box.Text.Trim().Length > 0);
                name = box.Text.Trim();
                sameNames = same.Checked;
                return accepted;
            }
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

        // Fills the inspector for a node: only the values the element has; a value that cannot be changed without
        // adding something to the files is shown read only.
        private void ShowDashProperties(AnarkDash.Node node)
        {
            if (node == null)
            {
                this.pgDash.SelectedObject = null;
                return;
            }
            if (this.dashCanvas.Selection.Count > 1)
            {
                this.ShowDashMulti(this.dashCanvas.Selection);
                return;
            }
            AnarkDash dash = this.dashCanvas.Dash;
            AnarkFbf.Record rec = node.Record;
            DashInspector view = new DashInspector(node.Name);

            view.Add("Name", DashInspector.CategoryElement, "Name of the element in the .fbf.", typeof(string), delegate { return node.Name; }, null);
            view.Add("Type", DashInspector.CategoryElement, "Kind of element.", typeof(string),
                delegate { return (rec != null) ? rec.Type.ToString() : node.Element.Type.ToString(); }, null);
            view.Add("Id", DashInspector.CategoryElement, "Record id: joins the .bgf, .bsg and .fbf.", typeof(string), delegate { return node.Element.Id.ToString(); }, null);

            bool spatial = node.Element.Type == AnarkBgf.ElementType.Node || node.Element.Type == AnarkBgf.ElementType.Text || node.Element.Type == AnarkBgf.ElementType.Component;
            if (spatial)
            {
                foreach (object[] t in DashTransforms)
                {
                    string name = (string) t[0];
                    float factor = (float) t[2], min = (float) t[3], max = (float) t[4];
                    string source = dash.TransformSource(node, name);
                    string description = (source != null)
                        ? "Stored in the ." + source + ". Drag on the preview to change position and size."
                        : "This element has no such value, so it cannot be changed without adding one to the files.";
                    view.Add((string) t[1], DashInspector.CategoryTransform, description, typeof(float),
                        delegate { return dash.GetTransform(node, name) * factor; },
                        (source == null) ? null : (DashSet) delegate(object value)
                        {
                            float shown = Math.Max(min, Math.Min(max, Convert.ToSingle(value)));
                            return this.DashApply(dash.SetTransform(node, name, shown / factor), name);
                        });
                }
            }

            bool isText = rec != null && rec.Type == AnarkFbf.RecordType.Text;
            if (rec != null && (isText || rec.Type == AnarkFbf.RecordType.Model))
            {
                string source = dash.ColorSource(node);
                view.Add("Color", DashInspector.CategoryAppearance, (source != null) ? "Stored in the ." + source + ". Click the arrow for the color picker, or type #RRGGBB." : "No editable color on this element.",
                    typeof(string), delegate { return GameColor.Hex(dash.GetColor(node)); },
                    (source == null) ? null : (DashSet) delegate(object value)
                    {
                        Color color;
                        return DashColorEditor.TryParse(value as string, out color) && this.DashApply(dash.SetColor(node, color), "color");
                    }).Color();
            }
            if (isText)
            {
                string textValue = this.DashString(node, "textstring");
                view.Add("Text", DashInspector.CategoryAppearance, "The text shown (a Latin-1 string stored in the .bgf).", typeof(string),
                    delegate { return this.DashString(node, "textstring") ?? rec.Text; },
                    (textValue == null) ? null : (DashSet) delegate(object value)
                    {
                        string text = (string) value ?? "";
                        return text == this.DashString(node, "textstring") || this.DashApply(dash.SetText(node, text), "text");
                    });
                string fontValue = this.DashString(node, "font");
                view.Add("Font", DashInspector.CategoryAppearance, "Font of the text: pick one used by this dash, or type a font the game has.", typeof(string),
                    delegate { return this.DashString(node, "font") ?? rec.Font; },
                    (fontValue == null) ? null : (DashSet) delegate(object value)
                    {
                        string font = (string) value ?? "";
                        return font == this.DashString(node, "font") || this.DashApply(dash.SetFont(node, font), "font");
                    }).Choices(delegate { return this.DashFonts().ToArray(); });
            }
            AnarkDash.Node image = dash.TextureNode(node);
            if (image != null)
            {
                view.Add("Texture", DashInspector.CategoryAppearance, "GAME:\\ path of the image (.fbf). The list holds the images of this dash and the .xds of its digitalgauge folder.", typeof(string),
                    delegate { return image.Record.Path; },
                    delegate(object value)
                    {
                        string path = (string) value ?? "";
                        return path == image.Record.Path || this.DashApply(dash.SetTexture(node, path), "texture");
                    }).Choices(delegate { return this.DashTextures(image.Record.Path).ToArray(); });
            }

            foreach (AnarkBgf.Property p in node.Element.Properties)
            {
                AnarkBgf.Property property = p;
                string name = p.Name;
                if (name != null && Array.IndexOf(DashHandledNames, name) >= 0)
                {
                    continue;
                }
                string caption = name ?? ("hash " + p.NameHash.ToString("X7"));
                string description = (p.IsBehaviourParameter ? "Behaviour parameter" : "Graph property") + " (" + p.Kind + "), stored in the .bgf.";
                string[] labels = (p.Kind == AnarkBgf.ValueKind.Int && name != null) ? DashEnumLabels(name) : null;
                switch (p.Kind)
                {
                    case AnarkBgf.ValueKind.Float:
                        view.Add(caption, DashInspector.CategoryGraph, description, typeof(float), delegate { return property.Float; },
                            delegate(object value) { return this.DashApply(dash.SetFloat(node, property, Convert.ToSingle(value)), caption); });
                        break;
                    case AnarkBgf.ValueKind.Bool:
                        view.Add(caption, DashInspector.CategoryGraph, description, typeof(bool), delegate { return property.Value != 0; },
                            delegate(object value) { return this.DashApply(dash.SetRaw(node, property, ((bool) value) ? 1u : 0u), caption); });
                        break;
                    case AnarkBgf.ValueKind.String:
                        view.Add(caption, DashInspector.CategoryGraph, description, typeof(string), delegate { return dash.Bgf.GetString(property) ?? ""; },
                            delegate(object value) { return this.DashApply(dash.SetStringProperty(node, property, (string) value), caption); });
                        break;
                    default:
                        if (labels != null)
                        {
                            view.Add(caption, DashInspector.CategoryGraph, description, typeof(string),
                                delegate { return (property.Value < labels.Length) ? labels[property.Value] : ((int) property.Value).ToString(); },
                                delegate(object value)
                                {
                                    int index = Array.IndexOf(labels, (string) value);
                                    return index >= 0 && this.DashApply(dash.SetRaw(node, property, (uint) index), caption);
                                }).Choices(labels, true);
                        }
                        else
                        {
                            view.Add(caption, DashInspector.CategoryGraph, description, typeof(int), delegate { return (int) property.Value; },
                                delegate(object value) { return this.DashApply(dash.SetRaw(node, property, (uint) (int) value), caption); });
                        }
                        break;
                }
            }
            this.pgDash.SelectedObject = view;
        }

        // Several elements selected: the values they share. The inspector shows the main (last) element's value;
        // a change is written to every selected element that has that value.
        private void ShowDashMulti(List<AnarkDash.Node> nodes)
        {
            AnarkDash dash = this.dashCanvas.Dash;
            AnarkDash.Node main = nodes[nodes.Count - 1];
            DashInspector view = new DashInspector(nodes.Count + " elements");
            view.Add("Selected", DashInspector.CategoryElement, "Edits below apply to all of them (Ctrl+click adds or removes one).", typeof(string),
                delegate { return nodes.Count + " elements"; }, null);

            foreach (object[] t in DashTransforms)
            {
                string name = (string) t[0];
                float factor = (float) t[2], min = (float) t[3], max = (float) t[4];
                AnarkDash.Node shown = null;
                int count = 0;
                foreach (AnarkDash.Node n in nodes)
                {
                    if (dash.TransformSource(n, name) != null)
                    {
                        count++;
                        if (shown == null || n == main)
                        {
                            shown = n;
                        }
                    }
                }
                if (count == 0)
                {
                    continue;
                }
                AnarkDash.Node reference = shown;
                view.Add((string) t[1], DashInspector.CategoryTransform,
                    string.Format("Shown for {0}; a change is written to the {1} selected elements that have it (position: aligns them).", reference.Name, count),
                    typeof(float), delegate { return dash.GetTransform(reference, name) * factor; },
                    delegate(object value)
                    {
                        float stored = Math.Max(min, Math.Min(max, Convert.ToSingle(value))) / factor;
                        bool any = false;
                        foreach (AnarkDash.Node n in nodes)
                        {
                            any |= dash.SetTransform(n, name, stored);
                        }
                        return this.DashApply(any, name);
                    });
            }

            List<AnarkDash.Node> colored = nodes.FindAll(delegate(AnarkDash.Node n) { return dash.ColorSource(n) != null; });
            if (colored.Count > 0)
            {
                AnarkDash.Node reference = colored.Contains(main) ? main : colored[0];
                view.Add("Color", DashInspector.CategoryAppearance, "Written to the " + colored.Count + " selected elements that have an editable color.", typeof(string),
                    delegate { return GameColor.Hex(dash.GetColor(reference)); },
                    delegate(object value)
                    {
                        Color color;
                        if (!DashColorEditor.TryParse(value as string, out color))
                        {
                            return false;
                        }
                        bool any = false;
                        foreach (AnarkDash.Node n in colored)
                        {
                            any |= dash.SetColor(n, color);
                        }
                        return this.DashApply(any, "color");
                    }).Color();
            }

            foreach (string name in new string[] { "horzalign", "vertalign" })
            {
                string[] labels = DashEnumLabels(name);
                List<AnarkBgf.Property> found = new List<AnarkBgf.Property>();
                AnarkBgf.Property mainProperty = null;
                foreach (AnarkDash.Node n in nodes)
                {
                    AnarkBgf.Property p = n.Element.Find(name);
                    if (p != null && p.Kind == AnarkBgf.ValueKind.Int)
                    {
                        found.Add(p);
                        if (n == main)
                        {
                            mainProperty = p;
                        }
                    }
                }
                if (found.Count == 0)
                {
                    continue;
                }
                AnarkBgf.Property reference = mainProperty ?? found[0];
                view.Add(name, DashInspector.CategoryGraph, "Written to the " + found.Count + " selected elements that have it.", typeof(string),
                    delegate { return (reference.Value < labels.Length) ? labels[reference.Value] : ((int) reference.Value).ToString(); },
                    delegate(object value)
                    {
                        int index = Array.IndexOf(labels, (string) value);
                        if (index < 0)
                        {
                            return false;
                        }
                        bool any = false;
                        foreach (AnarkBgf.Property p in found)
                        {
                            any |= dash.SetRaw(null, p, (uint) index);
                        }
                        return this.DashApply(any, name);
                    }).Choices(labels, true);
            }
            this.pgDash.SelectedObject = view;
        }

        private bool DashApply(bool done, string what)
        {
            this.DashEdited(done, what, false);
            return done;
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


        // After an edit: preview, and the changed files as pending entries of the archive. refresh: the inspector
        // rereads its values (the edit came from the preview, not from the inspector).
        private void DashEdited(bool done, string what, bool refresh)
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
            if (refresh)
            {
                this.pgDash.Refresh();
            }
            AnarkDash.Node node = this.dashCanvas.Selected;
            int count = this.dashCanvas.Selection.Count;
            this.SetStatus(string.Format("Dash: {0} of {1} changed in {2} (pending: Save to write the archive)",
                what, (count > 1) ? count + " elements" : (node != null) ? node.Name : "the dash", string.Join(", ", files.ToArray())));
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
    }
}
