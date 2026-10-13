namespace ForzaStudio
{
    using Microsoft.Xna.Framework;
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.IO;
    using System.Windows.Forms;

    // Parts between cars and editors: export a part to OBJ, replace a part's geometry from an OBJ, and
    // copy a part from another car's model (lossless: all LODs, tangent frames and damage data).
    public partial class ArchiveEditorForm
    {
        private void AddPartImportControls(FlowLayoutPanel side)
        {
            side.Controls.Add(SideLabel("Parts from other cars / editors"));
            side.Controls.Add(PartButton("Import Part From Car...", delegate { this.ImportPartFromCar(); }));
            side.Controls.Add(PartButton("Export Part to OBJ...", delegate { this.ExportPartObj(); }));
            side.Controls.Add(PartButton("Import OBJ Into Part...", delegate { this.ImportObjIntoPart(); }));
        }

        // The part OBJ export/import works on: the one in edit, else the part selected in the tree (OBJ
        // work needs only its name). Says why when there is none instead of doing nothing.
        private string PartForObj(string action)
        {
            string section = this.editSection ?? this.SelectedSectionName;
            if (section == null)
            {
                string reason = (this.currentEntry == null || !this.currentEntry.Name.EndsWith(".carbin", StringComparison.OrdinalIgnoreCase))
                    ? "Open the car's .carbin (e.g. toy_suprarz_98.carbin) and select a part (LODx > part) in the model tree."
                    : "Select a part (LODx > part) or one of its materials in the model tree.\n\n" + this.lblEditPart.Text;
                MessageBox.Show(this, reason, action, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            return section;
        }

        private static Button PartButton(string text, EventHandler click)
        {
            Button button = new Button();
            button.Text = text;
            button.Size = new Size(220, 28);
            button.Click += click;
            return button;
        }

        // Model files of the car shown: "" (main), "_lod0", "_cockpit" -> entry.
        private Dictionary<string, ForzaZip.Entry> CarModelFiles(ForzaZip archive, string folder)
        {
            Dictionary<string, ForzaZip.Entry> files = new Dictionary<string, ForzaZip.Entry>();
            // The car is the "<base>.carbin" + "<base>_lod0.carbin" pair; calipers and rotors
            // ("<base>_rotorlf_lod0.carbin", type 1) have no main file and are skipped.
            string mainBase = null;
            foreach (ForzaZip.Entry entry in archive.Entries)
            {
                string name = entry.Name.ToLowerInvariant();
                if (name.StartsWith(folder.ToLowerInvariant()) && name.EndsWith("_lod0.carbin") && name.IndexOf('/', folder.Length) < 0)
                {
                    string candidate = entry.Name.Substring(0, entry.Name.Length - "_lod0.carbin".Length);
                    if (archive.Find(candidate + ".carbin") != null && (mainBase == null || candidate.Length < mainBase.Length))
                    {
                        mainBase = candidate;
                    }
                }
            }
            if (mainBase == null)
            {
                return files;
            }
            foreach (string suffix in new string[] { "", "_lod0", "_cockpit" })
            {
                ForzaZip.Entry entry = archive.Find(mainBase + suffix + ".carbin");
                if (entry != null)
                {
                    files[suffix] = entry;
                }
            }
            return files;
        }

        private void ExportPartObj()
        {
            string part = this.PartForObj("Export Part");
            if (part == null)
            {
                return;
            }
            // The most detailed version: the lod0 file when it has the part.
            ForzaZip.Entry source = this.currentEntry;
            Dictionary<string, ForzaZip.Entry> files = this.CarModelFiles(this.zip, this.ModelFolder);
            CarbinModel model = null;
            ForzaZip.Entry lod0;
            try
            {
                if (files.TryGetValue("_lod0", out lod0))
                {
                    CarbinModel candidate = new CarbinModel(this.zip.Read(lod0));
                    if (candidate.Find(part) != null)
                    {
                        model = candidate;
                        source = lod0;
                    }
                }
                if (model == null)
                {
                    model = new CarbinModel(this.zip.Read(this.currentEntry));
                }
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, "Export Part", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                return;
            }
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Filter = "Wavefront OBJ (*.obj)|*.obj";
                dialog.FileName = Path.GetFileNameWithoutExtension(source.Name) + "_" + part + ".obj";
                if (!ShowRemembering(dialog, this, "Obj"))
                {
                    return;
                }
                try
                {
                    int triangles = ObjMesh.Export(model, part, dialog.FileName, "Forza Studio: " + part + " from " + source.Name);
                    this.SetStatus(string.Format("{0}: {1:N0} triangles exported from {2} to {3}", part, triangles, source.Name, dialog.FileName));
                }
                catch (Exception exception)
                {
                    MessageBox.Show(this, exception.Message, "Export Part", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                }
            }
        }

        // Every material name this car's model uses, sorted.
        private List<string> CarMaterials()
        {
            List<string> names = new List<string>();
            foreach (ForzaZip.Entry entry in this.PartEditTargets())
            {
                try
                {
                    CarbinModel model = new CarbinModel(this.zip.Read(entry));
                    foreach (CarbinModel.Section section in model.Sections)
                    {
                        foreach (CarbinModel.SubSection sub in section.SubSections)
                        {
                            if (!names.Contains(sub.Name))
                            {
                                names.Add(sub.Name);
                            }
                        }
                    }
                }
                catch (NotSupportedException)
                {
                }
            }
            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
        }

        private void ImportObjIntoPart()
        {
            string part = this.PartForObj("Import OBJ");
            if (part == null)
            {
                return;
            }
            using (OpenFileDialog open = new OpenFileDialog())
            {
                open.Filter = "Wavefront OBJ (*.obj)|*.obj";
                open.Title = "Geometry for " + part;
                if (!ShowRemembering(open, this, "Obj"))
                {
                    return;
                }
                this.ImportObjFile(open.FileName);
            }
        }

        private void ImportObjFile(string path)
        {
            string section = this.PartForObj("Import OBJ");
            if (section == null)
            {
                return;
            }
            ObjMesh mesh;
            try
            {
                mesh = ObjMesh.Load(path);
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, "Cannot read the OBJ: " + exception.Message, "Import OBJ", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                return;
            }
            if (mesh.TriangleCount == 0)
            {
                MessageBox.Show(this, "The OBJ has no faces.", "Import OBJ", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Material mapping and UV mode.
            List<string> carMaterials = this.CarMaterials();
            Dictionary<string, ComboBox> choices = new Dictionary<string, ComboBox>();
            Panel extra = new Panel();
            extra.Size = new Size(520, 120 + (mesh.Materials.Count * 28));
            RadioButton replaceTarget = new RadioButton();
            replaceTarget.Text = "Replace " + section;
            replaceTarget.AutoSize = true;
            replaceTarget.Checked = true;
            replaceTarget.Location = new System.Drawing.Point(0, 4);
            RadioButton addTarget = new RadioButton();
            addTarget.Text = "Add as a new part (" + section + " as template) named:";
            addTarget.AutoSize = true;
            addTarget.Location = new System.Drawing.Point(0, 30);
            TextBox newName = new TextBox();
            newName.Width = 140;
            newName.BorderStyle = BorderStyle.FixedSingle;
            newName.Location = new System.Drawing.Point(346, 28);
            newName.Text = this.SuggestPartName(section);
            newName.Enabled = false;
            addTarget.CheckedChanged += delegate { newName.Enabled = addTarget.Checked; };
            extra.Controls.AddRange(new Control[] { replaceTarget, addTarget, newName });
            int y = 64;
            Label header = new Label();
            header.Text = string.Format("{0:N0} triangles. Car material for each OBJ material:", mesh.TriangleCount);
            header.AutoSize = true;
            header.Location = new System.Drawing.Point(0, y);
            extra.Controls.Add(header);
            y += 24;
            foreach (ObjMesh.Material material in mesh.Materials)
            {
                Label name = new Label();
                name.Text = string.Format("{0} ({1})", material.Name, material.Triangles.Count);
                name.AutoSize = false;
                name.Size = new Size(220, 22);
                name.TextAlign = ContentAlignment.MiddleLeft;
                name.Location = new System.Drawing.Point(0, y);
                ComboBox combo = new ComboBox();
                combo.DropDownStyle = ComboBoxStyle.DropDownList;
                combo.Width = 260;
                combo.Location = new System.Drawing.Point(226, y);
                combo.Items.Add("(skip)");
                combo.Items.AddRange(carMaterials.ToArray());
                int index = carMaterials.IndexOf(material.Name);
                combo.SelectedIndex = (index >= 0) ? index + 1 : 0;
                extra.Controls.Add(name);
                extra.Controls.Add(combo);
                choices[material.Name] = combo;
                y += 28;
            }
            CheckBox fit = new CheckBox();
            fit.Text = "Fit UVs into this car's material regions (part from another car)";
            fit.AutoSize = true;
            fit.Location = new System.Drawing.Point(0, y + 4);
            extra.Controls.Add(fit);

            if (!this.ShowOptions("Import OBJ Into " + section, extra))
            {
                return;
            }
            string target = addTarget.Checked ? newName.Text.Trim() : section;
            if (target.Length == 0 || target.IndexOfAny(new char[] { ' ', '/', '\\' }) >= 0)
            {
                MessageBox.Show(this, "Give the new part a name without spaces.", "Import OBJ", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (target != section && this.PartExists(target) && MessageBox.Show(this, string.Format("This car already has a part named {0}.\n\nReplace it with the OBJ ({1} as template)?", target, section),
                "Import OBJ", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }
            Dictionary<string, string> map = new Dictionary<string, string>();
            foreach (KeyValuePair<string, ComboBox> pair in choices)
            {
                map[pair.Key] = (pair.Value.SelectedIndex > 0) ? (string) pair.Value.SelectedItem : null;
            }

            List<string> report = new List<string>();
            try
            {
                Cursor.Current = Cursors.WaitCursor;
                foreach (ForzaZip.Entry entry in this.PartEditTargets(target != section || this.chkAllModelFiles.Checked))
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
                    if (model.Find(section) == null)
                    {
                        continue;
                    }
                    if (target != section)
                    {
                        if (model.Find(target) != null)
                        {
                            model.RemoveSection(target);
                        }
                        model.DuplicateSection(section, target);
                    }
                    string done = model.ReplaceGeometry(target, mesh, map, fit.Checked);
                    if (done == null)
                    {
                        continue;
                    }
                    this.zip.Replace(entry, model.ToBytes());
                    this.RefreshEntryNode(entry);
                    report.Add(entry.Name.Substring(entry.Name.LastIndexOf('/') + 1));
                }
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, "Import OBJ", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                return;
            }
            finally
            {
                Cursor.Current = Cursors.Default;
            }
            this.editSection = null;
            this.ReloadModelKeepingView();
            this.SetStatus(string.Format((target == section) ? "{0}: geometry replaced from OBJ in {2} (pending: Save to write the archive)"
                : "{0}: added from OBJ ({1} as template) in {2} (pending: Save to write the archive)", target, section, string.Join(", ", report.ToArray())));
        }

        // Whether any model file the edit touches already has a part with this name.
        private bool PartExists(string name)
        {
            foreach (ForzaZip.Entry entry in this.PartEditTargets(true))
            {
                try
                {
                    if (new CarbinModel(this.zip.Read(entry)).Find(name) != null)
                    {
                        return true;
                    }
                }
                catch (NotSupportedException)
                {
                }
            }
            return false;
        }

        // A free name for a sibling part: exhausta -> exhaustb (next variant letter), else name_2, name_3...
        private string SuggestPartName(string name)
        {
            char last = name[name.Length - 1];
            if (last >= 'a' && last < 'z')
            {
                for (char c = (char) (last + 1); c <= 'z'; c++)
                {
                    string candidate = name.Substring(0, name.Length - 1) + c;
                    if (!this.PartExists(candidate))
                    {
                        return candidate;
                    }
                }
            }
            for (int i = 2; ; i++)
            {
                string candidate = name + "_" + i;
                if (!this.PartExists(candidate))
                {
                    return candidate;
                }
            }
        }

        // OK / Cancel window around an options panel.
        private bool ShowOptions(string title, Control content)
        {
            using (Form form = new Form())
            {
                form.Text = title;
                form.StartPosition = FormStartPosition.CenterParent;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MinimizeBox = false;
                form.MaximizeBox = false;
                form.ShowInTaskbar = false;
                form.Font = ForzaTheme.Regular;
                form.ClientSize = new Size(content.Width + 24, content.Height + 60);
                content.Location = new System.Drawing.Point(12, 12);
                form.Controls.Add(content);
                Button ok = new Button();
                ok.Text = "OK";
                ok.Size = new Size(84, 28);
                ok.Location = new System.Drawing.Point(form.ClientSize.Width - 186, form.ClientSize.Height - 40);
                ok.DialogResult = DialogResult.OK;
                Button cancel = new Button();
                cancel.Text = "Cancel";
                cancel.Size = new Size(84, 28);
                cancel.Location = new System.Drawing.Point(form.ClientSize.Width - 96, form.ClientSize.Height - 40);
                cancel.DialogResult = DialogResult.Cancel;
                form.Controls.Add(ok);
                form.Controls.Add(cancel);
                form.AcceptButton = ok;
                form.CancelButton = cancel;
                form.BackColor = ForzaTheme.Background;
                ForzaTheme.Apply(form);
                bool accepted = form.ShowDialog(this) == DialogResult.OK;
                // Detached so the caller can still read its fields after the window is disposed.
                form.Controls.Remove(content);
                return accepted;
            }
        }

        private void ImportPartFromCar()
        {
            string selected = this.editSection;
            ForzaZip source;
            string sourceFolder = "";
            using (OpenFileDialog open = new OpenFileDialog())
            {
                open.Filter = "Forza car (*.zip; *_lod0.carbin)|*.zip;*_lod0.carbin";
                open.Title = "Car to take a part from (its .zip, or the _lod0.carbin in an uncompressed car folder)";
                if (!ShowRemembering(open, this, "SourceCar"))
                {
                    return;
                }
                try
                {
                    if (open.FileName.EndsWith(".carbin", StringComparison.OrdinalIgnoreCase))
                    {
                        source = ForzaZip.OpenFolder(Path.GetDirectoryName(open.FileName));
                    }
                    else
                    {
                        source = new ForzaZip(open.FileName);
                    }
                }
                catch (Exception exception)
                {
                    MessageBox.Show(this, exception.Message, "Import Part", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                    return;
                }
            }
            Dictionary<string, ForzaZip.Entry> sourceFiles = this.CarModelFiles(source, sourceFolder);
            Dictionary<string, ForzaZip.Entry> targetFiles = this.CarModelFiles(this.zip, this.ModelFolder);
            if (!sourceFiles.ContainsKey("") || !targetFiles.ContainsKey(""))
            {
                MessageBox.Show(this, "Both cars need their main model (.carbin next to the _lod0.carbin).", "Import Part", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            CarbinModel sourceMain;
            CarbinModel targetMain;
            try
            {
                sourceMain = new CarbinModel(source.Read(sourceFiles[""]));
                targetMain = new CarbinModel(this.zip.Read(targetFiles[""]));
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, "Import Part", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                return;
            }

            List<object[]> rows = new List<object[]>();
            foreach (CarbinModel.Section section in sourceMain.Sections)
            {
                List<string> materials = new List<string>();
                foreach (CarbinModel.SubSection sub in section.SubSections)
                {
                    if (!materials.Contains(sub.Name))
                    {
                        materials.Add(sub.Name);
                    }
                }
                rows.Add(new object[] { section.Name, string.Join(", ", materials.ToArray()), section.IsDeformable ? "deformable" : "rigid" });
            }

            // Options under the part list.
            Panel extra = new Panel();
            extra.Size = new Size(520, 104);
            Label replaceLabel = new Label();
            replaceLabel.Text = "Replace in this car:";
            replaceLabel.AutoSize = true;
            replaceLabel.Location = new System.Drawing.Point(0, 6);
            ComboBox replace = new ComboBox();
            replace.DropDownStyle = ComboBoxStyle.DropDownList;
            replace.Location = new System.Drawing.Point(130, 2);
            replace.Width = 200;
            replace.Items.Add("(nothing: add as a new part)");
            List<string> targetNames = new List<string>();
            foreach (CarbinModel.Section section in targetMain.Sections)
            {
                targetNames.Add(section.Name);
            }
            targetNames.Sort(StringComparer.OrdinalIgnoreCase);
            replace.Items.AddRange(targetNames.ToArray());
            replace.SelectedIndex = (selected != null && targetNames.Contains(selected)) ? targetNames.IndexOf(selected) + 1 : 0;
            Label nameLabel = new Label();
            nameLabel.Text = "Name in this car:";
            nameLabel.AutoSize = true;
            nameLabel.Location = new System.Drawing.Point(0, 36);
            TextBox name = new TextBox();
            name.Location = new System.Drawing.Point(130, 33);
            name.Width = 200;
            name.BorderStyle = BorderStyle.FixedSingle;
            name.Text = (replace.SelectedIndex > 0) ? (string) replace.SelectedItem : "";
            replace.SelectedIndexChanged += delegate
            {
                if (replace.SelectedIndex > 0)
                {
                    name.Text = (string) replace.SelectedItem;
                }
            };
            CheckBox align = new CheckBox();
            align.Text = "Place it at the replaced part's center";
            align.Checked = true;
            align.AutoSize = true;
            align.Location = new System.Drawing.Point(0, 62);
            CheckBox regions = new CheckBox();
            regions.Text = "Use this car's texture regions for its materials";
            regions.Checked = true;
            regions.AutoSize = true;
            regions.Location = new System.Drawing.Point(0, 84);
            extra.Controls.AddRange(new Control[] { replaceLabel, replace, nameLabel, name, align, regions });

            string part;
            string newName;
            string replaced;
            bool alignChecked;
            bool regionsChecked;
            using (PickDialog pick = new PickDialog("Import Part From " + Path.GetFileName(source.FilePath ?? "car"), new string[] { "Part", "Materials", "Damage" }, new int[] { 160, 260, 90 }, rows, extra))
            {
                if (pick.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                part = (string) pick.SelectedKey;
                newName = name.Text.Trim();
                replaced = (replace.SelectedIndex > 0) ? (string) replace.SelectedItem : null;
                alignChecked = align.Checked;
                regionsChecked = regions.Checked;
            }
            if (newName.Length == 0)
            {
                newName = part;
            }
            if (newName != replaced && targetNames.Contains(newName))
            {
                MessageBox.Show(this, "This car already has a part named " + newName + ": choose it in \"Replace\" or use another name.", "Import Part", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // One placement for every file: source part center -> replaced part center (main files).
            Matrix move = Matrix.Identity;
            CarbinModel.Section from = sourceMain.Find(part);
            CarbinModel.Section to = (replaced != null) ? targetMain.Find(replaced) : null;
            if (alignChecked && to != null)
            {
                move = Matrix.CreateTranslation(new Vector3(to.Bounds[0] - from.Bounds[0], to.Bounds[1] - from.Bounds[1], to.Bounds[2] - from.Bounds[2]));
            }

            List<string> report = new List<string>();
            try
            {
                Cursor.Current = Cursors.WaitCursor;
                foreach (KeyValuePair<string, ForzaZip.Entry> pair in targetFiles)
                {
                    ForzaZip.Entry sourceEntry;
                    if (!sourceFiles.TryGetValue(pair.Key, out sourceEntry))
                    {
                        continue;
                    }
                    CarbinModel sourceModel = new CarbinModel(source.Read(sourceEntry));
                    if (sourceModel.Find(part) == null)
                    {
                        continue;
                    }
                    CarbinModel model = new CarbinModel(this.zip.Read(pair.Value));
                    if (replaced != null && model.Find(replaced) != null)
                    {
                        model.RemoveSection(replaced);
                    }
                    model.ImportSection(sourceModel, part, newName, regionsChecked);
                    if (move != Matrix.Identity)
                    {
                        model.TransformSection(newName, move);
                    }
                    this.zip.Replace(pair.Value, model.ToBytes());
                    this.RefreshEntryNode(pair.Value);
                    report.Add(pair.Value.Name.Substring(pair.Value.Name.LastIndexOf('/') + 1));
                }
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, "Import Part", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                return;
            }
            finally
            {
                Cursor.Current = Cursors.Default;
            }
            this.editSection = null;
            this.ReloadModelKeepingView();
            this.SetStatus(string.Format("{0} from {1} -> {2} in {3} (pending: Save to write the archive)", part, Path.GetFileName(source.FilePath ?? ""), newName, string.Join(", ", report.ToArray())));
        }
    }
}
