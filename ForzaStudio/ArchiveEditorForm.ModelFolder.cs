namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.IO;
    using System.Text;
    using System.Windows.Forms;

    // The whole car model as a work folder of OBJs (see CarModelFolder): export every part of the
    // main, lod0 and cockpit files, edit / add / delete OBJs in an editor, import the folder back.
    public partial class ArchiveEditorForm
    {
        // The car's type-2 model files, parsed, by kind; null with a message when there are none.
        private Dictionary<string, CarbinModel> LoadCarModels(string action, out Dictionary<string, ForzaZip.Entry> files)
        {
            files = (this.zip == null) ? new Dictionary<string, ForzaZip.Entry>() : this.CarModelFiles(this.zip, this.ModelFolder);
            if (files.Count == 0)
            {
                MessageBox.Show(this, "Open a car's model (its .carbin) first.", action, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return null;
            }
            Dictionary<string, CarbinModel> models = new Dictionary<string, CarbinModel>();
            foreach (KeyValuePair<string, ForzaZip.Entry> pair in files)
            {
                byte[] data = this.zip.Read(pair.Value);
                if (CarModelFolder.IsMotorsport2(data))
                {
                    MessageBox.Show(this, "Forza Motorsport 2 models can be exported to a folder, but importing them back is not available yet.", action, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return null;
                }
                models[pair.Key] = new CarbinModel(data);
            }
            return models;
        }

        private static string CarbinName(ForzaZip.Entry entry)
        {
            return entry.Name.Substring(entry.Name.LastIndexOf('/') + 1);
        }

        private void ExportModelFolder()
        {
            // Every model file of the car; FM2 ones go through the viewer's reader.
            Dictionary<string, ForzaZip.Entry> files = (this.zip == null) ? new Dictionary<string, ForzaZip.Entry>() : this.CarModelFiles(this.zip, this.ModelFolder);
            if (files.Count == 0)
            {
                MessageBox.Show(this, "Open a car's model (its .carbin) first.", "Export Model", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string parent;
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Where to export: one folder per .carbin is made here (e.g. toy_supra_98\\lod1\\part.obj, toy_supra_98_lod0\\lod0\\part.obj):";
                string start = AppSettings.Get("Dir.ModelFolder", null);
                if (start != null && Directory.Exists(start))
                {
                    dialog.SelectedPath = start;
                }
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                parent = dialog.SelectedPath;
                AppSettings.Set("Dir.ModelFolder", parent);
            }
            List<string> existing = new List<string>();
            foreach (ForzaZip.Entry entry in files.Values)
            {
                string folder = Path.Combine(parent, CarModelFolder.FolderName(CarbinName(entry)));
                if (Directory.Exists(folder))
                {
                    existing.Add(Path.GetFileName(folder));
                }
            }
            if (existing.Count > 0 && MessageBox.Show(this, string.Join(", ", existing.ToArray()) + " already exist in " + parent + ".\n\nOverwrite them? The OBJs in their lodN folders are replaced by a fresh export (other files are left alone).",
                "Export Model", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }
            try
            {
                Cursor.Current = Cursors.WaitCursor;
                int count = 0;
                List<string> made = new List<string>();
                foreach (string kind in new string[] { CarModelFolder.Main, CarModelFolder.Lod0, CarModelFolder.Cockpit })
                {
                    ForzaZip.Entry entry;
                    if (!files.TryGetValue(kind, out entry))
                    {
                        continue;
                    }
                    string folder = Path.Combine(parent, CarModelFolder.FolderName(CarbinName(entry)));
                    byte[] data = this.zip.Read(entry);
                    count += CarModelFolder.IsMotorsport2(data)
                        ? CarModelFolder.ExportLegacy(data, CarbinName(entry), folder, this.SourceName)
                        : CarModelFolder.Export(new CarbinModel(data), CarbinName(entry), folder, this.SourceName);
                    made.Add(Path.GetFileName(folder));
                }
                this.SetStatus(string.Format("{0:N0} OBJs exported to {1}: {2} (edit them, then Import Model From Folder)", count, parent, string.Join(", ", made.ToArray())));
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, "Export Model", MessageBoxButtons.OK, MessageBoxIcon.Hand);
            }
            finally
            {
                Cursor.Current = Cursors.Default;
            }
        }

        // The .carbin folders to import from the folder picked: that folder itself when it is one
        // (e.g. toy_supra_98_lod0), else the car's .carbin folders inside it.
        private static Dictionary<string, string> ModelFoldersIn(string picked, Dictionary<string, ForzaZip.Entry> files)
        {
            Dictionary<string, string> folders = new Dictionary<string, string>();
            foreach (KeyValuePair<string, ForzaZip.Entry> pair in files)
            {
                string name = CarModelFolder.FolderName(CarbinName(pair.Value));
                if (string.Equals(Path.GetFileName(picked.TrimEnd('\\')), name, StringComparison.OrdinalIgnoreCase))
                {
                    folders.Clear();
                    folders[pair.Key] = picked;
                    return folders;
                }
                string inside = Path.Combine(picked, name);
                if (Directory.Exists(inside))
                {
                    folders[pair.Key] = inside;
                }
            }
            return folders;
        }

        private void ImportModelFolder()
        {
            Dictionary<string, ForzaZip.Entry> files;
            Dictionary<string, CarbinModel> models;
            try
            {
                models = this.LoadCarModels("Import Model", out files);
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, "Import Model", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                return;
            }
            if (models == null)
            {
                return;
            }
            List<string> expected = new List<string>();
            foreach (ForzaZip.Entry entry in files.Values)
            {
                expected.Add(CarModelFolder.FolderName(CarbinName(entry)));
            }
            Dictionary<string, string> folders;
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "The folder you exported to (with " + string.Join(", ", expected.ToArray()) + "), or one of those folders to import only that .carbin:";
                dialog.ShowNewFolderButton = false;
                string start = AppSettings.Get("Dir.ModelFolder", null);
                if (start != null && Directory.Exists(start))
                {
                    dialog.SelectedPath = start;
                }
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                folders = ModelFoldersIn(dialog.SelectedPath, files);
                if (folders.Count == 0)
                {
                    MessageBox.Show(this, "No model folder of this car in " + dialog.SelectedPath + ".\n\nExpected: " + string.Join(", ", expected.ToArray()) + " (made by Export Model To Folder).",
                        "Import Model", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                // Remembered: the folder that holds the .carbin folders.
                bool single = false;
                foreach (string folder in folders.Values)
                {
                    single |= string.Equals(folder, dialog.SelectedPath, StringComparison.OrdinalIgnoreCase);
                }
                AppSettings.Set("Dir.ModelFolder", single ? Path.GetDirectoryName(dialog.SelectedPath) : dialog.SelectedPath);
            }

            CarModelFolder.Plan plan;
            try
            {
                Cursor.Current = Cursors.WaitCursor;
                plan = CarModelFolder.Prepare(folders, models);
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, "Import Model", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                return;
            }
            finally
            {
                Cursor.Current = Cursors.Default;
            }
            if (plan.Errors.Count > 0)
            {
                MessageBox.Show(this, "Nothing imported:\n\n" + string.Join("\n", plan.Errors.GetRange(0, Math.Min(20, plan.Errors.Count)).ToArray())
                    + ((plan.Errors.Count > 20) ? string.Format("\n... {0} more", plan.Errors.Count - 20) : ""), "Import Model", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (plan.IsEmpty)
            {
                MessageBox.Show(this, "Every OBJ matches the model: nothing to import." + ((plan.Ignored.Count > 0) ? "\n\nIgnored: " + string.Join(", ", plan.Ignored.ToArray()) : ""),
                    "Import Model", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!this.ConfirmModelPlan(plan))
            {
                return;
            }

            StringBuilder report = new StringBuilder();
            try
            {
                Cursor.Current = Cursors.WaitCursor;
                List<string> changed = CarModelFolder.Apply(plan, models, report);
                foreach (string kind in changed)
                {
                    this.zip.Replace(files[kind], models[kind].ToBytes());
                    this.RefreshEntryNode(files[kind]);
                }
            }
            catch (Exception exception)
            {
                // Nothing was replaced unless every model was rebuilt: the archive is unchanged.
                MessageBox.Show(this, exception.Message + "\n\nThe archive was not changed.", "Import Model", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                return;
            }
            finally
            {
                Cursor.Current = Cursors.Default;
            }
            this.editSection = null;
            this.ReloadModelKeepingView();
            this.SetStatus(string.Format("Model imported: {0} part changes (pending: Save to write the archive)", plan.Parts.Count));
            this.ShowReport("Import Model", report.ToString());
        }

        // The plan in words, with a template choice for each new part. False when cancelled.
        private bool ConfirmModelPlan(CarModelFolder.Plan plan)
        {
            List<CarModelFolder.PartPlan> added = new List<CarModelFolder.PartPlan>();
            StringBuilder text = new StringBuilder();
            foreach (CarModelFolder.PartPlan part in plan.Parts)
            {
                text.AppendLine(part.Describe());
                if (part.IsNew)
                {
                    added.Add(part);
                }
            }
            foreach (string warning in plan.Warnings)
            {
                text.AppendLine();
                text.AppendLine(warning);
            }
            if (plan.Ignored.Count > 0)
            {
                text.AppendLine();
                text.AppendLine("Ignored: " + string.Join(", ", plan.Ignored.ToArray()));
            }

            Panel panel = new Panel();
            int rows = Math.Min(added.Count, 10);
            panel.Size = new Size(640, 300 + ((added.Count > 0) ? 28 + (rows * 28) : 0));
            TextBox summary = new TextBox();
            summary.Multiline = true;
            summary.ReadOnly = true;
            summary.ScrollBars = ScrollBars.Both;
            summary.WordWrap = false;
            summary.Font = new Font(FontFamily.GenericMonospace, 9f);
            summary.Size = new Size(640, 290);
            summary.Text = text.ToString();
            panel.Controls.Add(summary);
            Dictionary<CarModelFolder.PartPlan, ComboBox> templates = new Dictionary<CarModelFolder.PartPlan, ComboBox>();
            if (added.Count > 0)
            {
                Panel list = new Panel();
                list.Location = new Point(0, 300);
                list.Size = new Size(640, 28 + (rows * 28));
                list.AutoScroll = added.Count > rows;
                Label header = new Label();
                header.Text = "New parts take materials, damage data and draw order from a template part (nearest one preselected):";
                header.AutoSize = true;
                header.Location = new Point(0, 4);
                list.Controls.Add(header);
                int y = 28;
                foreach (CarModelFolder.PartPlan part in added)
                {
                    Label name = new Label();
                    name.Text = ((part.Kind == CarModelFolder.Main) ? "main" : part.Kind.Substring(1)) + " " + part.Part;
                    name.AutoSize = false;
                    name.Size = new Size(260, 22);
                    name.TextAlign = ContentAlignment.MiddleLeft;
                    name.Location = new Point(0, y);
                    ComboBox combo = new ComboBox();
                    combo.DropDownStyle = ComboBoxStyle.DropDownList;
                    combo.Width = 300;
                    combo.Location = new Point(266, y);
                    combo.Items.AddRange(part.Templates.ToArray());
                    combo.SelectedItem = part.Template;
                    list.Controls.Add(name);
                    list.Controls.Add(combo);
                    templates[part] = combo;
                    y += 28;
                }
                panel.Controls.Add(list);
            }
            if (!this.ShowOptions(string.Format("Import Model ({0} changes)", plan.Parts.Count), panel))
            {
                return false;
            }
            foreach (KeyValuePair<CarModelFolder.PartPlan, ComboBox> pair in templates)
            {
                if (pair.Value.SelectedItem != null)
                {
                    pair.Key.Template = (string) pair.Value.SelectedItem;
                }
            }
            return true;
        }

        // A read-only text window (import reports).
        private void ShowReport(string title, string text)
        {
            using (Form form = new Form())
            {
                form.Text = title;
                form.StartPosition = FormStartPosition.CenterParent;
                form.ShowInTaskbar = false;
                form.MinimizeBox = false;
                form.Font = ForzaTheme.Regular;
                form.ClientSize = new Size(760, 480);
                TextBox box = new TextBox();
                box.Multiline = true;
                box.ReadOnly = true;
                box.ScrollBars = ScrollBars.Both;
                box.WordWrap = false;
                box.Font = new Font(FontFamily.GenericMonospace, 9f);
                box.Dock = DockStyle.Fill;
                box.Text = text;
                form.Controls.Add(box);
                form.BackColor = ForzaTheme.Background;
                ForzaTheme.Apply(form);
                form.ShowDialog(this);
            }
        }
    }
}
