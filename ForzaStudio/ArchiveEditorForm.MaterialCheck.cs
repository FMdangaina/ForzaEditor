namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
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
            Panel content = new Panel();
            content.Size = new Size(380, 150);
            Label label = new Label();
            label.Text = string.Format("Rename the material {0} of {1} to:", material, section);
            label.AutoSize = true;
            label.Location = new Point(0, 4);
            ComboBox name = new ComboBox();
            name.DropDownStyle = ComboBoxStyle.DropDown;
            name.Width = 260;
            name.Location = new Point(0, 26);
            name.Items.AddRange(CarPort.Fm4Materials(this.modelType));
            name.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            name.AutoCompleteSource = AutoCompleteSource.ListItems;
            string suggestion = CarPort.SuggestMaterial(material, this.currentEntry.Name);
            name.Text = suggestion ?? "";
            CheckBox everyPart = new CheckBox();
            everyPart.Text = "In every part with this material";
            everyPart.Checked = true;
            everyPart.AutoSize = true;
            everyPart.Location = new Point(0, 60);
            CheckBox everyFile = new CheckBox();
            everyFile.Text = "In every model file of the car (main, lod0, cockpit)";
            everyFile.Checked = true;
            everyFile.AutoSize = true;
            everyFile.Location = new Point(0, 84);
            Label note = new Label();
            note.Text = (suggestion != null) ? "Suggested by the FH2 -> FM4 material table." : "The list holds the materials FM4 cars use.";
            note.ForeColor = SystemColors.GrayText;
            note.AutoSize = true;
            note.Location = new Point(0, 114);
            content.Controls.AddRange(new Control[] { label, name, everyPart, everyFile, note });
            if (!this.ShowOptions("Rename Material", content))
            {
                return;
            }
            string newName = name.Text.Trim();
            if (newName.Length == 0 || newName.IndexOfAny(new char[] { ' ', '/', '\\', '<', '>' }) >= 0 || newName == material)
            {
                return;
            }
            List<string> report = new List<string>();
            try
            {
                Cursor.Current = Cursors.WaitCursor;
                List<string> renamedIn = new List<string>();   // "cockpit_" or "" per renamed model, for shadersettings.xml
                foreach (ForzaZip.Entry entry in this.PartEditTargets(everyFile.Checked))
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
                    foreach (CarbinModel.Section part in model.Sections)
                    {
                        if (!everyPart.Checked && part.Name != section)
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
                    string prefix = entry.Name.EndsWith("_cockpit.carbin", StringComparison.OrdinalIgnoreCase) ? "cockpit_" : "";
                    if (!renamedIn.Contains(prefix + to))
                    {
                        renamedIn.Add(prefix + to);
                        if (this.RenameShaderSetting(prefix + material, prefix + to))
                        {
                            report.Add("shadersettings.xml: " + prefix + material + " -> " + prefix + to);
                        }
                    }
                }
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

        // Renames the <from> element of the car's shadersettings.xml to <to> when it has no <to> yet.
        private bool RenameShaderSetting(string from, string to)
        {
            ForzaZip.Entry entry = this.zip.Find(this.ModelFolder + "shadersettings.xml");
            if (entry == null)
            {
                return false;
            }
            byte[] data = this.zip.Read(entry);
            string xml = System.Text.Encoding.UTF8.GetString(data);
            bool bom = xml.Length > 0 && xml[0] == '﻿';
            if (Regex.IsMatch(xml, "<" + Regex.Escape(to) + ">") || !Regex.IsMatch(xml, "<" + Regex.Escape(from) + ">"))
            {
                return false;
            }
            xml = Regex.Replace(xml, "<(/?)" + Regex.Escape(from) + ">", "<$1" + to + ">");
            byte[] text = System.Text.Encoding.UTF8.GetBytes(bom ? xml.Substring(1) : xml);
            if (bom)
            {
                byte[] withBom = new byte[text.Length + 3];
                withBom[0] = 0xEF;
                withBom[1] = 0xBB;
                withBom[2] = 0xBF;
                Buffer.BlockCopy(text, 0, withBom, 3, text.Length);
                text = withBom;
            }
            this.zip.Replace(entry, text);
            this.RefreshEntryNode(entry);
            return true;
        }
    }
}
