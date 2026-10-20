namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.IO;
    using System.Text.RegularExpressions;
    using System.Windows.Forms;

    // FM4 models whose materials no FM4 car uses (an FH2 port: fullbeam, hidhead, ...) render them
    // black in game. They are shown in red in the model tree and can be renamed to an FM4 material.
    public partial class ArchiveEditorForm
    {
        private ToolStripLabel lblMaterialWarning;
        private int modelType;                                   // carbin type of the shown model, 0 if not checked
        private readonly List<string> unknownMaterials = new List<string>();

        private ToolStripLabel BuildMaterialWarning()
        {
            this.lblMaterialWarning = new ToolStripLabel();
            this.lblMaterialWarning.ForeColor = ForzaTheme.Red;
            this.lblMaterialWarning.Visible = false;
            // A link: a click selects the first one, ready for Rename Material.
            this.lblMaterialWarning.IsLink = true;
            this.lblMaterialWarning.LinkColor = ForzaTheme.Red;
            this.lblMaterialWarning.ActiveLinkColor = ForzaTheme.Red;
            this.lblMaterialWarning.VisitedLinkColor = ForzaTheme.Red;
            this.lblMaterialWarning.LinkBehavior = LinkBehavior.HoverUnderline;
            this.lblMaterialWarning.Click += delegate { this.SelectUnknownMaterial(); };
            return this.lblMaterialWarning;
        }

        // Colors the mesh nodes (and their part and LOD) whose material no FM4 car uses. Only FM4-format
        // files are checked: in an FH or FM3 file every game-specific name would show.
        private void MarkUnknownMaterials(byte[] data, TreeNode root)
        {
            this.unknownMaterials.Clear();
            this.modelType = 0;
            this.lblMaterialWarning.Visible = false;
            CarbinModel model;
            try
            {
                model = new CarbinModel(data);
            }
            catch (Exception)
            {
                return;
            }
            if (model.IsHorizon)
            {
                return;
            }
            this.modelType = (int) model.Type;
            this.MarkUnknownMaterials(root.Nodes);
            if (this.unknownMaterials.Count > 0)
            {
                this.lblMaterialWarning.Text = string.Format("{0} material{1} not in FM4: click to fix", this.unknownMaterials.Count, (this.unknownMaterials.Count == 1) ? "" : "s");
                this.lblMaterialWarning.ToolTipText = "No FM4 car uses " + string.Join(", ", this.unknownMaterials.ToArray())
                    + ": they may render black in game.\nRed in the tree. Click here to select the first one, then Rename Material in the Part tab.";
                this.lblMaterialWarning.Visible = true;
            }
        }

        private void MarkUnknownMaterials(TreeNodeCollection nodes)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.Tag is ForzaMesh)
                {
                    if (!CarPort.IsFm4Material(node.Text, this.modelType))
                    {
                        if (!this.unknownMaterials.Contains(node.Text))
                        {
                            this.unknownMaterials.Add(node.Text);
                        }
                        for (TreeNode marked = node; marked != null; marked = marked.Parent)
                        {
                            marked.ForeColor = ForzaTheme.Red;
                        }
                    }
                    continue;
                }
                this.MarkUnknownMaterials(node.Nodes);
            }
        }

        private bool IsUnknownMaterial(string material)
        {
            return this.unknownMaterials.Contains(material);
        }

        private void SelectUnknownMaterial()
        {
            TreeNode found = FindUnknownMaterial(this.tvModel.Nodes, this.unknownMaterials);
            if (found != null)
            {
                found.EnsureVisible();
                this.tvModel.SelectedNode = found;
                this.tvModel.Focus();
            }
        }

        private static TreeNode FindUnknownMaterial(TreeNodeCollection nodes, List<string> names)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.Tag is ForzaMesh && names.Contains(node.Text))
                {
                    return node;
                }
                TreeNode found = FindUnknownMaterial(node.Nodes, names);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }

        // Renames the selected material (subsection name) to an FM4 one, in the selected part or in every
        // part, in this file or every model file of the car, under each file's own variant (CarPort.LodVariant:
        // tail_light_lod0 in lod0 and cockpit). shadersettings.xml follows when it has the old name only.
        private void RenameSelectedMaterial()
        {
            string section = this.SelectedSectionName;
            string material = this.SelectedMaterialName;
            if (section == null || material == null)
            {
                return;
            }
            ForzaMesh selected = this.tvModel.SelectedNode.Tag as ForzaMesh;
            int sharing = 0;   // meshes of the selected part (all LODs) with this material
            foreach (TreeNode lodNode in this.tvModel.SelectedNode.Parent.Parent.Parent.Nodes)
            {
                foreach (TreeNode partNode in lodNode.Nodes)
                {
                    if (partNode.Text != section)
                    {
                        continue;
                    }
                    foreach (TreeNode meshNode in partNode.Nodes)
                    {
                        sharing += (meshNode.Tag is ForzaMesh && meshNode.Text == material) ? 1 : 0;
                    }
                }
            }
            Panel content = new Panel();
            content.Size = new Size(440, 192);
            Label label = new Label();
            label.Text = string.Format("Rename the material {0} of {1} to:", material, section);
            label.AutoSize = true;
            label.Location = new Point(0, 4);
            // Green: recommended (port table, name, most similar shaders), then the common FM4 materials, then
            // ~ the unusual ones (an FM4 shader no or few FM4 cars use: for something different on purpose).
            List<MaterialSuggestions.Entry> choices = MaterialSuggestions.For(material, this.currentEntry.Name, this.modelType);
            ComboBox name = new ComboBox();
            name.DropDownStyle = ComboBoxStyle.DropDown;
            name.Width = 420;
            name.MaxDropDownItems = 20;
            ForzaTheme.GreenItems(name);
            name.Location = new Point(0, 26);
            AutoCompleteStringCollection names = new AutoCompleteStringCollection();
            foreach (MaterialSuggestions.Entry choice in choices)
            {
                name.Items.Add(choice);
                names.Add(choice.Name);
            }
            name.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            name.AutoCompleteSource = AutoCompleteSource.CustomSource;
            name.AutoCompleteCustomSource = names;
            bool recommended = choices.Count > 0 && choices[0].Group == MaterialSuggestions.Group.Recommended;
            if (recommended)
            {
                name.SelectedIndex = 0;
            }
            CheckBox everyPart = new CheckBox();
            everyPart.Text = "In every part with this material";
            everyPart.Checked = false;   // nothing preset: the user picks the scope
            everyPart.AutoSize = true;
            everyPart.Location = new Point(0, 60);
            CheckBox everyFile = new CheckBox();
            everyFile.Text = "In every model file of the car (main, lod0, cockpit)";
            everyFile.Checked = false;
            everyFile.AutoSize = true;
            everyFile.Location = new Point(0, 84);
            // FH traffic parts merge several pieces (both tail lights in one part): one mesh renamed alone.
            CheckBox onlyThis = new CheckBox();
            onlyThis.Text = string.Format("Only this mesh ({0} meshes of {1} use {2})", sharing, section, material);
            onlyThis.AutoSize = true;
            onlyThis.Location = new Point(0, 108);
            onlyThis.Enabled = selected != null && selected.SubIndex >= 0;
            onlyThis.CheckedChanged += delegate
            {
                everyPart.Enabled = !onlyThis.Checked;
                everyFile.Enabled = !onlyThis.Checked;
            };
            Label note = new Label();
            note.Text = "green = recommended   ~ unusual: an FM4 shader few or no FM4 cars use\n\"shader N\": how close the FM4 shader is to the source game's (0-100).";
            note.ForeColor = SystemColors.GrayText;
            note.AutoSize = true;
            note.Location = new Point(0, 138);
            content.Controls.AddRange(new Control[] { label, name, everyPart, everyFile, onlyThis, note });
            if (!this.ShowOptions("Rename Material", content))
            {
                return;
            }
            string newName = MaterialSuggestions.NameOf((name.SelectedItem != null && name.Text == name.SelectedItem.ToString()) ? name.SelectedItem : name.Text);
            if (newName.Length == 0 || newName.IndexOfAny(new char[] { ' ', '/', '\\', '<', '>' }) >= 0 || newName == material)
            {
                return;
            }
            List<string> report = new List<string>();
            try
            {
                Cursor.Current = Cursors.WaitCursor;
                List<string[]> renamed = new List<string[]>();   // technique from -> to, for shadersettings.xml
                foreach (ForzaZip.Entry entry in this.PartEditTargets(everyFile.Checked && !onlyThis.Checked))
                {
                    CarbinModel model;
                    try
                    {
                        model = new CarbinModel(this.zip.Read(entry));
                    }
                    catch (NotSupportedException)
                    {
                        continue;
                    }
                    string to = CarPort.LodVariant(newName, entry.Name);
                    int count = 0;
                    if (onlyThis.Checked)
                    {
                        // The tree reads the file in the same order as CarbinModel; checked by name anyway.
                        CarbinModel.SubSection sub = (selected.SectionIndex < model.Sections.Count && selected.SubIndex < model.Sections[selected.SectionIndex].SubSections.Count)
                            ? model.Sections[selected.SectionIndex].SubSections[selected.SubIndex] : null;
                        if (sub == null || model.Sections[selected.SectionIndex].Name != section || sub.Name != material)
                        {
                            throw new InvalidDataException("The selected mesh could not be found in the file: nothing renamed.");
                        }
                        sub.Name = to;
                        count = 1;
                    }
                    foreach (CarbinModel.Section part in model.Sections)
                    {
                        if (onlyThis.Checked || (!everyPart.Checked && part.Name != section))
                        {
                            continue;
                        }
                        foreach (CarbinModel.SubSection sub in part.SubSections)
                        {
                            if (sub.Name == material)
                            {
                                sub.Name = to;
                                count++;
                            }
                        }
                    }
                    if (count == 0)
                    {
                        continue;
                    }
                    this.zip.Replace(entry, model.ToBytes());
                    this.RefreshEntryNode(entry);
                    string file = entry.Name.Substring(entry.Name.LastIndexOf('/') + 1);
                    report.Add(string.Format("{0}: {1} -> {2} ({3})", file, material, to, count));
                    string[] pair = new string[] { ShaderSettings.TechniqueName(material, entry.Name), ShaderSettings.TechniqueName(to, entry.Name) };
                    if (!renamed.Exists(delegate(string[] p) { return p[0] == pair[0] && p[1] == pair[1]; }))
                    {
                        renamed.Add(pair);
                    }
                }
                this.FollowShaderSettings(renamed, report);
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, "Rename Material", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                return;
            }
            finally
            {
                Cursor.Current = Cursors.Default;
            }
            this.editSection = null;
            this.ReloadModelKeepingView();
            this.SetStatus((report.Count > 0) ? string.Join("; ", report.ToArray()) + " (pending: Save to write the archive)" : "Nothing to rename.");
        }

        // After materials were renamed in the car's models (technique pairs from -> to): the car's
        // shadersettings.xml follows (ShaderSettings.FollowRename). The models are read again, so a
        // technique still used elsewhere (other part, other file, one mesh renamed) keeps its entry and
        // the new name gets a copy; otherwise the entry is renamed. Before, the entry was always renamed,
        // which left the meshes still using the old name without their settings.
        private void FollowShaderSettings(List<string[]> renamed, List<string> report)
        {
            ForzaZip.Entry entry = this.zip.Find(this.ModelFolder + "shadersettings.xml");
            if (entry == null || renamed.Count == 0)
            {
                return;
            }
            byte[] original = this.zip.Read(entry);
            ShaderSettings settings = ShaderSettings.Parse(original);
            string before = Convert.ToBase64String(settings.ToBytes());
            SortedDictionary<string, List<string>> used = ShaderSettings.Techniques(this.zip, this.ModelFolder);
            foreach (string[] pair in renamed)
            {
                string done = settings.FollowRename(pair[0], pair[1], used.ContainsKey(pair[0]));
                if (done != null)
                {
                    report.Add("shadersettings.xml: " + done);
                }
            }
            if (Convert.ToBase64String(settings.ToBytes()) != before)
            {
                this.zip.Replace(entry, settings.ToBytes());
                this.RefreshEntryNode(entry);
            }
        }
    }
}
