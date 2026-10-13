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
                models[pair.Key] = new CarbinModel(this.zip.Read(pair.Value));
            }
            return models;
        }

        private static string CarBaseName(Dictionary<string, ForzaZip.Entry> files)
        {
            string name = files[files.ContainsKey(CarModelFolder.Main) ? CarModelFolder.Main : CarModelFolder.Lod0].Name;
            name = name.Substring(name.LastIndexOf('/') + 1);
            name = name.Substring(0, name.Length - ".carbin".Length);
            return name.EndsWith("_lod0", StringComparison.OrdinalIgnoreCase) ? name.Substring(0, name.Length - 5) : name;
        }

        private void ExportModelFolder()
        {
            Dictionary<string, ForzaZip.Entry> files;
            Dictionary<string, CarbinModel> models;
            try
            {
                models = this.LoadCarModels("Export Model", out files);
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, "Export Model", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                return;
            }
            if (models == null)
            {
                return;
            }
            string parent;
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Folder to create the model's work folder in (a subfolder named after the car is made):";
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
            string folder = Path.Combine(parent, CarBaseName(files));
            if (Directory.Exists(folder) && Directory.GetFiles(folder, "*.obj").Length > 0
                && MessageBox.Show(this, folder + " already holds OBJs.\n\nOverwrite it? Its model OBJs are replaced by a fresh export (other files are left alone).",
                    "Export Model", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }
            Dictionary<string, string> names = new Dictionary<string, string>();
            foreach (KeyValuePair<string, ForzaZip.Entry> pair in files)
            {
                names[pair.Key] = pair.Value.Name.Substring(pair.Value.Name.LastIndexOf('/') + 1);
            }
            try
            {
                Cursor.Current = Cursors.WaitCursor;
                int count = CarModelFolder.Export(models, names, folder, this.SourceName);
                this.SetStatus(string.Format("{0:N0} OBJs exported to {1} (main, lod0 and cockpit; edit them and use Import Model From Folder)", count, folder));
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
            string folder;
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "The car's model work folder (the one with " + CarModelFolder.ManifestName + " and the OBJs):";
                dialog.ShowNewFolderButton = false;
                string start = AppSettings.Get("Dir.ModelFolder", null);
                string own = (start != null) ? Path.Combine(start, CarBaseName(files)) : null;
                if (own != null && Directory.Exists(own))
                {
                    dialog.SelectedPath = own;
                }
                else if (start != null && Directory.Exists(start))
                {
                    dialog.SelectedPath = start;
                }
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                folder = dialog.SelectedPath;
            }

            CarModelFolder.Plan plan;
            try
            {
                Cursor.Current = Cursors.WaitCursor;
                plan = CarModelFolder.Prepare(folder, models);
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
                MessageBox.Show(this, "Every OBJ in the folder matches the model: nothing to import." + ((plan.Ignored.Count > 0) ? "\n\nIgnored (name not <lod>-<part>-<material>.obj): " + string.Join(", ", plan.Ignored.ToArray()) : ""),
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
            this.SetStatus(string.Format("Model imported from {0}: {1} part changes (pending: Save to write the archive)", folder, plan.Parts.Count));
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
                text.AppendLine("Ignored (name not <lod>-<part>-<material>.obj): " + string.Join(", ", plan.Ignored.ToArray()));
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
            if (!this.ShowOptions(string.Format("Import Model From {0} ({1} changes)", Path.GetFileName(plan.Folder), plan.Parts.Count), panel))
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
