namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.IO;
    using System.Windows.Forms;

    // New files: add any file to the archive (asking its name and extension), turning an image into
    // an .xds on the way when asked; create a standalone .xds from an image.
    public partial class ArchiveEditorForm
    {
        private static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff", ".tga", ".dds" };

        private static bool IsImage(string path)
        {
            return Array.IndexOf(ImageExtensions, Path.GetExtension(path).ToLowerInvariant()) >= 0;
        }

        // The archive folder of the selected tree node ("" = root, else "a/b/").
        private string SelectedArchiveFolder()
        {
            TreeNode node = this.tvArchive.SelectedNode;
            if (node == null)
            {
                return "";
            }
            ForzaZip.Entry entry = node.Tag as ForzaZip.Entry;
            if (entry != null)
            {
                return entry.Name.Substring(0, entry.Name.LastIndexOf('/') + 1);
            }
            if (node.Tag is string)
            {
                return (string) node.Tag;
            }
            // Zip mode: folder nodes are named by their path parts below the root node.
            List<string> parts = new List<string>();
            for (TreeNode n = node; n != null && n.Parent != null; n = n.Parent)
            {
                parts.Insert(0, n.Text);
            }
            return (parts.Count == 0) ? "" : string.Join("/", parts.ToArray()) + "/";
        }

        // Extensions already in the archive, most used first.
        private List<string> ArchiveExtensions()
        {
            Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (ForzaZip.Entry entry in this.zip.Entries)
            {
                string extension = Path.GetExtension(entry.Name).ToLowerInvariant();
                if (extension.Length > 0)
                {
                    int count;
                    counts.TryGetValue(extension, out count);
                    counts[extension] = count + 1;
                }
            }
            List<string> list = new List<string>(counts.Keys);
            list.Sort(delegate(string a, string b) { return (counts[a] != counts[b]) ? counts[b].CompareTo(counts[a]) : string.CompareOrdinal(a, b); });
            return list;
        }

        private void AddFileToArchive()
        {
            if (this.zip == null)
            {
                MessageBox.Show(this, "Open an archive or a folder first.", "Add File", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string source;
            using (OpenFileDialog open = new OpenFileDialog())
            {
                open.Title = "File to add to " + this.SourceName;
                open.Filter = "All files (*.*)|*.*|Images (*.png;*.jpg;*.bmp;*.tif;*.tga;*.dds)|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff;*.tga;*.dds";
                if (!ShowRemembering(open, this, "AddFile"))
                {
                    return;
                }
                source = open.FileName;
            }
            this.AddFileToArchive(source);
        }

        private void AddFileToArchive(string source)
        {
            string sourceExtension = Path.GetExtension(source).ToLowerInvariant();
            bool image = IsImage(source) && sourceExtension != ".dds";

            // Name, folder and extension in the archive.
            Panel content = new Panel();
            content.Size = new Size(470, 96);
            Label folderLabel = OptionLabel("Folder in the archive:", 0, 4);
            TextBox folder = OptionText(this.SelectedArchiveFolder(), 150, 0, 310);
            Label nameLabel = OptionLabel("Name:", 0, 34);
            TextBox name = OptionText(Path.GetFileNameWithoutExtension(source), 150, 30, 310);
            Label extensionLabel = OptionLabel("Extension:", 0, 64);
            ComboBox extension = new ComboBox();
            extension.DropDownStyle = ComboBoxStyle.DropDown;
            extension.Location = new Point(150, 60);
            extension.Width = 120;
            List<string> extensions = this.ArchiveExtensions();
            if (!extensions.Contains(".xds"))
            {
                extensions.Add(".xds");
            }
            if (sourceExtension.Length > 0 && !extensions.Contains(sourceExtension))
            {
                extensions.Insert(0, sourceExtension);
            }
            extension.Items.AddRange(extensions.ToArray());
            // Images the game has no use for as such (png, jpg, bmp, tif) become textures by default.
            extension.Text = (image && sourceExtension != ".tga") ? ".xds" : sourceExtension;
            Label note = OptionLabel("", 280, 64);
            note.ForeColor = SystemColors.GrayText;
            content.Controls.AddRange(new Control[] { folderLabel, folder, nameLabel, name, extensionLabel, extension, note });

            XdsOptions xds = null;
            if (image)
            {
                try
                {
                    xds = new XdsOptions(source);
                }
                catch (Exception exception)
                {
                    MessageBox.Show(this, "Cannot read the image: " + exception.Message, "Add File", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                    return;
                }
                xds.Panel.Location = new Point(0, 100);
                content.Height = 100 + xds.Panel.Height;
                content.Controls.Add(xds.Panel);
                EventHandler toggle = delegate
                {
                    bool toXds = NormalizeExtension(extension.Text) == ".xds";
                    xds.Panel.Enabled = toXds;
                    note.Text = toXds ? "converted to a texture" : "copied as it is";
                };
                extension.TextChanged += toggle;
                toggle(null, EventArgs.Empty);
            }
            if (!this.ShowOptions("Add File: " + Path.GetFileName(source), content))
            {
                return;
            }

            string ext = NormalizeExtension(extension.Text);
            string baseName = name.Text.Trim();
            string dir = folder.Text.Trim().Replace('\\', '/').Trim('/');
            if (baseName.Length == 0 || baseName.IndexOfAny(new char[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|' }) >= 0)
            {
                MessageBox.Show(this, "Give the file a name (no / \\ : * ? \" < > |).", "Add File", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (ext.Length > 0 && baseName.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
            {
                baseName = baseName.Substring(0, baseName.Length - ext.Length);
            }
            string entryName = ((dir.Length > 0) ? dir + "/" : "") + baseName + ext;

            byte[] data;
            string how;
            try
            {
                Cursor.Current = Cursors.WaitCursor;
                if (ext == ".xds" && sourceExtension != ".xds")
                {
                    if (xds == null)
                    {
                        throw new InvalidDataException("Only images can be turned into an .xds texture.");
                    }
                    this.SetStatus("Encoding " + Path.GetFileName(source) + "...");
                    XdsTexture texture = xds.Encode(source);
                    data = texture.ToBytes();
                    how = "texture " + texture;
                }
                else
                {
                    data = File.ReadAllBytes(source);
                    if (ext == ".xds")
                    {
                        new XdsTexture(data);
                    }
                    how = string.Format("{0:N0} bytes", data.Length);
                }
            }
            catch (Exception exception)
            {
                this.SetStatus("Add failed");
                MessageBox.Show(this, exception.Message, "Add File", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                return;
            }
            finally
            {
                Cursor.Current = Cursors.Default;
            }

            ForzaZip.Entry existing = this.zip.Find(entryName);
            if (existing != null)
            {
                if (MessageBox.Show(this, entryName + " is already in the archive.\n\nReplace it?", "Add File", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                {
                    return;
                }
                this.ClearPreviewCache();
                this.zip.Replace(existing, data);
            }
            else
            {
                this.zip.Add(entryName, data);
            }
            this.ClearPreviewCache();
            this.currentEntry = null;
            this.PopulateTree();
            this.SelectEntry(entryName);
            this.UpdateTitle();
            this.SetStatus(string.Format("{0} {1} ({2}) - save to write it", (existing != null) ? "Replaced" : "Added", entryName, how));
        }

        private static string NormalizeExtension(string text)
        {
            string extension = text.Trim().ToLowerInvariant();
            if (extension.Length > 0 && !extension.StartsWith("."))
            {
                extension = "." + extension;
            }
            return extension;
        }

        // A standalone .xds next to the image (or where chosen), then opened for viewing / editing.
        private void NewXdsFromImage()
        {
            string source;
            using (OpenFileDialog open = new OpenFileDialog())
            {
                open.Title = "Image to turn into an .xds texture";
                open.Filter = "Images (*.png;*.jpg;*.bmp;*.tif;*.tga)|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff;*.tga";
                if (!ShowRemembering(open, this, "AddFile"))
                {
                    return;
                }
                source = open.FileName;
            }
            XdsOptions xds;
            try
            {
                xds = new XdsOptions(source);
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, "Cannot read the image: " + exception.Message, "New XDS", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                return;
            }
            if (!this.ShowOptions("New XDS From " + Path.GetFileName(source), xds.Panel))
            {
                return;
            }
            string target;
            using (SaveFileDialog save = new SaveFileDialog())
            {
                save.Filter = "Forza texture (*.xds)|*.xds";
                save.InitialDirectory = Path.GetDirectoryName(source);
                save.FileName = Path.GetFileNameWithoutExtension(source) + ".xds";
                if (save.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                target = save.FileName;
            }
            if (!this.ConfirmDiscard())
            {
                return;
            }
            try
            {
                Cursor.Current = Cursors.WaitCursor;
                this.SetStatus("Encoding " + Path.GetFileName(source) + "...");
                XdsTexture texture = xds.Encode(source);
                File.WriteAllBytes(target, texture.ToBytes());
                this.OpenSingleFile(target);
                this.SetStatus(string.Format("Created {0}: {1}", target, texture));
            }
            catch (Exception exception)
            {
                this.SetStatus("New XDS failed");
                MessageBox.Show(this, exception.Message, "New XDS", MessageBoxButtons.OK, MessageBoxIcon.Hand);
            }
            finally
            {
                Cursor.Current = Cursors.Default;
            }
        }

        // A loose file (.xds and the like) on its own: viewed and edited like an archive entry, saved
        // in place (with the usual backup).
        public void OpenSingleFile(string path)
        {
            try
            {
                ForzaZip opened = ForzaZip.OpenFile(path);
                this.CloseArchiveCore();
                this.zip = opened;
                this.PopulateTree();
                this.SelectEntry(Path.GetFileName(path));
                this.SetStatus("Opened " + path);
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, "Open", MessageBoxButtons.OK, MessageBoxIcon.Hand);
            }
            finally
            {
                this.UpdateTitle();
            }
        }

        private static Label OptionLabel(string text, int x, int y)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.Location = new Point(x, y);
            return label;
        }

        private static TextBox OptionText(string text, int x, int y, int width)
        {
            TextBox box = new TextBox();
            box.Text = text;
            box.Location = new Point(x, y);
            box.Width = width;
            box.BorderStyle = BorderStyle.FixedSingle;
            return box;
        }

        // Format, color space and mips of a texture made from an image; defaults from the image
        // (alpha in use -> DXT5, else DXT1; gamma color; full mip chain).
        private class XdsOptions
        {
            public Panel Panel = new Panel();
            private ComboBox format = new ComboBox();
            private CheckBox gamma = new CheckBox();
            private ComboBox mips = new ComboBox();
            private int width;
            private int height;

            public XdsOptions(string imagePath)
            {
                byte[] pixels = TgaImage.LoadPixels(imagePath, out this.width, out this.height);
                bool alpha = false;
                for (int i = 3; i < pixels.Length && !alpha; i += 4)
                {
                    alpha = pixels[i] != 255;
                }
                this.Panel.Size = new Size(470, 120);
                Label size = OptionLabel(string.Format("Image: {0} x {1}{2}{3}", this.width, this.height, alpha ? ", with alpha" : ", opaque",
                    (IsPowerOfTwo(this.width) && IsPowerOfTwo(this.height)) ? "" : " (not a power of two: allowed, the game has such textures)"), 0, 0);
                Label formatLabel = OptionLabel("Texture format:", 0, 30);
                this.format.DropDownStyle = ComboBoxStyle.DropDownList;
                this.format.Location = new Point(150, 26);
                this.format.Width = 230;
                foreach (DxtFormat codec in XdsTexture.CreatableFormats)
                {
                    this.format.Items.Add(new FormatItem(codec));
                }
                this.format.SelectedIndex = Array.IndexOf(XdsTexture.CreatableFormats, alpha ? DxtFormat.Dxt5 : DxtFormat.Dxt1);
                this.gamma.Text = "Gamma color (color maps; off for normal maps and masks)";
                this.gamma.AutoSize = true;
                this.gamma.Checked = true;
                this.gamma.Location = new Point(0, 58);
                this.format.SelectedIndexChanged += delegate
                {
                    DxtFormat codec = ((FormatItem) this.format.SelectedItem).Codec;
                    this.gamma.Checked = codec != DxtFormat.Dxn && codec != DxtFormat.Dxt5A;
                };
                Label mipsLabel = OptionLabel("Mip levels:", 0, 90);
                this.mips.DropDownStyle = ComboBoxStyle.DropDownList;
                this.mips.Location = new Point(150, 86);
                this.mips.Width = 230;
                int full = XdsTexture.Log2Floor(Math.Max(this.width, this.height)) + 1;
                this.mips.Items.Add(string.Format("Full chain ({0} levels)", full));
                this.mips.Items.Add("None (1 level)");
                this.mips.SelectedIndex = 0;
                this.Panel.Controls.AddRange(new Control[] { size, formatLabel, this.format, this.gamma, mipsLabel, this.mips });
            }

            private static bool IsPowerOfTwo(int value)
            {
                return (value & (value - 1)) == 0;
            }

            public XdsTexture Encode(string imagePath)
            {
                DxtFormat codec = ((FormatItem) this.format.SelectedItem).Codec;
                int levels = (this.mips.SelectedIndex == 0) ? int.MaxValue : 1;
                XdsTexture texture = XdsTexture.Create(this.width, this.height, codec, this.gamma.Checked, levels);
                texture.ImportImage(imagePath, Path.Combine(Application.StartupPath, "texconv.exe"));
                return texture;
            }

            private class FormatItem
            {
                public DxtFormat Codec;

                public FormatItem(DxtFormat codec)
                {
                    this.Codec = codec;
                }

                public override string ToString()
                {
                    return XdsTexture.FormatLabel(this.Codec);
                }
            }
        }
    }
}
