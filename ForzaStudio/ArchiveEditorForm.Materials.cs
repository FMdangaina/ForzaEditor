namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Windows.Forms;

    // Mesh information and preview texture assignment. The .carbin only names the material
    // (shader) of each part; which .xds it samples is decided by the game, so textures are
    // assigned by hand, by default to every part sharing the material.
    public partial class ArchiveEditorForm
    {
        private const string ArchivePrefix = "zip:";
        private static readonly System.Drawing.Color BaseColor = System.Drawing.Color.FromArgb(150, 150, 150);

        private Label lblMeshInfo;
        private ComboBox cmbMeshTexture;
        private ComboBox cmbMeshUV;
        private CheckBox chkSameMaterial;
        private CheckBox chkAlphaMask;
        private bool updatingMeshPanel;

        // Material name -> texture source ("zip:<entry>" or a file path) and UV set, kept across models.
        private Dictionary<string, string> materialTextures = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, bool> materialSecondUV = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<ForzaMesh, string> meshTextures = new Dictionary<ForzaMesh, string>();
        private Dictionary<string, Bitmap> previewCache = new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);

        private Panel BuildMeshInfoPanel()
        {
            FlowLayoutPanel side = new FlowLayoutPanel();
            side.Dock = DockStyle.Right;
            side.Width = 268;
            side.FlowDirection = FlowDirection.TopDown;
            side.WrapContents = false;
            side.AutoScroll = true;
            side.Padding = new Padding(8);

            this.lblMeshInfo = new Label();
            this.lblMeshInfo.AutoSize = false;
            this.lblMeshInfo.Size = new Size(220, 175);
            this.lblMeshInfo.Text = "Select a part in the tree.";

            this.cmbMeshTexture = new ComboBox();
            this.cmbMeshTexture.DropDownStyle = ComboBoxStyle.DropDownList;
            this.cmbMeshTexture.Width = 220;
            this.cmbMeshTexture.SelectedIndexChanged += delegate
            {
                if (!this.updatingMeshPanel && this.cmbMeshTexture.SelectedIndex >= 0)
                {
                    string item = (string) this.cmbMeshTexture.SelectedItem;
                    this.AssignTexture(item == "(none)" ? null : ArchivePrefix + item);
                }
            };

            Button browse = new Button();
            browse.Text = "Texture From Folder...";
            browse.Size = new Size(220, 28);
            browse.Click += delegate { this.AssignTextureFromFile(); };

            this.cmbMeshUV = new ComboBox();
            this.cmbMeshUV.DropDownStyle = ComboBoxStyle.DropDownList;
            this.cmbMeshUV.Width = 220;
            this.cmbMeshUV.Items.AddRange(new object[] { "UV1", "UV2" });
            this.cmbMeshUV.SelectedIndex = 0;
            this.cmbMeshUV.SelectedIndexChanged += delegate
            {
                if (!this.updatingMeshPanel)
                {
                    this.SetSecondUV(this.cmbMeshUV.SelectedIndex == 1);
                }
            };

            this.chkSameMaterial = new CheckBox();
            this.chkSameMaterial.Text = "Apply to all parts with this material";
            this.chkSameMaterial.Checked = true;
            this.chkSameMaterial.Size = new Size(220, 36);

            this.chkAlphaMask = new CheckBox();
            this.chkAlphaMask.Text = "Alpha as decal mask over base color";
            this.chkAlphaMask.Checked = true;
            this.chkAlphaMask.Size = new Size(220, 36);
            this.chkAlphaMask.CheckedChanged += delegate
            {
                this.ClearPreviewCache();
                this.ApplyTextures();
            };

            side.Controls.Add(this.lblMeshInfo);
            this.AddPartEditControls(side);
            side.Controls.Add(SideLabel("PREVIEW TEXTURE"));
            side.Controls.Add(this.cmbMeshTexture);
            side.Controls.Add(browse);
            side.Controls.Add(SideLabel("UV set"));
            side.Controls.Add(this.cmbMeshUV);
            side.Controls.Add(this.chkSameMaterial);
            side.Controls.Add(this.chkAlphaMask);
            Label note = SideLabel("The .carbin names the material of each part, not its texture file: pick the .xds to preview. Assignments are remembered per material.");
            note.Size = new Size(220, 90);
            note.ForeColor = SystemColors.GrayText;
            side.Controls.Add(note);

            this.tvModel.AfterSelect += delegate { this.UpdateMeshPanel(); };
            return side;
        }

        private List<ForzaMesh> SelectedMeshes
        {
            get
            {
                List<ForzaMesh> meshes = new List<ForzaMesh>();
                TreeNode node = this.tvModel.SelectedNode;
                if (node != null)
                {
                    ForzaMesh mesh = node.Tag as ForzaMesh;
                    if (mesh != null)
                    {
                        meshes.Add(mesh);
                    }
                    else
                    {
                        meshes.AddRange(GetMeshes(node.Nodes, false));
                    }
                }
                return meshes;
            }
        }

        // Called when a model has been loaded: refresh the texture list and reapply remembered materials.
        private void OnModelLoaded()
        {
            this.updatingMeshPanel = true;
            this.cmbMeshTexture.Items.Clear();
            this.cmbMeshTexture.Items.Add("(none)");
            // Textures next to the model and below it: the whole zip, or the car's folder in folder mode.
            string folder = this.ModelFolder;
            foreach (ForzaZip.Entry entry in this.zip.Entries)
            {
                if (!entry.Name.StartsWith(folder, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (entry.Name.EndsWith(".xds", StringComparison.OrdinalIgnoreCase) || entry.Name.EndsWith(".tga", StringComparison.OrdinalIgnoreCase))
                {
                    this.cmbMeshTexture.Items.Add(entry.Name.Substring(folder.Length));
                }
            }
            this.updatingMeshPanel = false;

            this.meshTextures.Clear();
            foreach (ForzaMesh mesh in GetMeshes(this.tvModel.Nodes, false))
            {
                string source;
                if (this.materialTextures.TryGetValue(mesh.MaterialName, out source) && this.SourceExists(source))
                {
                    this.meshTextures[mesh] = source;
                }
                bool second;
                mesh.UseSecondUV = this.materialSecondUV.TryGetValue(mesh.MaterialName, out second) && second;
            }
            this.ApplyTextures();
            this.UpdateMeshPanel();
        }

        private void UpdateMeshPanel()
        {
            List<ForzaMesh> meshes = this.SelectedMeshes;
            this.updatingMeshPanel = true;
            try
            {
                if (meshes.Count == 0)
                {
                    this.lblMeshInfo.Text = "Select a part in the tree.";
                    this.cmbMeshTexture.SelectedIndex = -1;
                    return;
                }
                Dictionary<string, int> materials = new Dictionary<string, int>();
                int vertices = 0;
                int faces = 0;
                foreach (ForzaMesh mesh in meshes)
                {
                    vertices += mesh.VertexCount;
                    faces += mesh.FaceCount;
                    int count;
                    materials.TryGetValue(mesh.MaterialName, out count);
                    materials[mesh.MaterialName] = count + 1;
                }
                ForzaMesh first = meshes[0];
                string texture;
                this.meshTextures.TryGetValue(first, out texture);
                TreeNode node = this.tvModel.SelectedNode;
                if (node.Tag is ForzaMesh)
                {
                    string part = (node.Parent != null) ? node.Parent.Text : "";
                    string lod = (node.Parent != null && node.Parent.Parent != null) ? node.Parent.Parent.Text : "";
                    this.lblMeshInfo.Text = string.Format("Part: {0}\nLOD: {1}\nMaterial: {2}\nDamage: {7}\n\nVertices: {3:N0}\nFaces: {4:N0}\nParts with this material: {5}\n\nTexture: {6}",
                        part, lod, first.MaterialName, vertices, faces, this.CountMaterial(first.MaterialName), DisplaySource(texture), this.DescribeDamage(part));
                }
                else
                {
                    List<string> names = new List<string>(materials.Keys);
                    names.Sort();
                    this.lblMeshInfo.Text = string.Format("{0}\n{1} parts, {2:N0} vertices, {3:N0} faces\n{6}\n\nMaterials ({4}):\n{5}",
                        this.tvModel.SelectedNode.Text, meshes.Count, vertices, faces, names.Count, string.Join(", ", names.ToArray()), this.DescribeDamage(node));
                }
                string entry = (texture != null && texture.StartsWith(ArchivePrefix)) ? texture.Substring(ArchivePrefix.Length) : null;
                int index = (texture == null) ? 0 : ((entry != null) ? this.cmbMeshTexture.Items.IndexOf(entry) : -1);
                this.cmbMeshTexture.SelectedIndex = index;
                this.cmbMeshUV.SelectedIndex = first.UseSecondUV ? 1 : 0;
            }
            finally
            {
                this.updatingMeshPanel = false;
            }
        }

        // Section name -> has damage deformation data, for the model being shown (null if unknown).
        private Dictionary<string, bool> sectionDeformable;

        private void LoadDamageInfo(byte[] data)
        {
            this.sectionDeformable = null;
            try
            {
                CarbinFile carbin = new CarbinFile(data);
                this.sectionDeformable = new Dictionary<string, bool>();
                foreach (CarbinFile.Section section in carbin.Sections)
                {
                    this.sectionDeformable[section.Name] = section.IsDeformable;
                }
            }
            catch (Exception)
            {
            }
        }

        private string DescribeDamage(string section)
        {
            bool deformable;
            if (this.sectionDeformable == null || !this.sectionDeformable.TryGetValue(section, out deformable))
            {
                return "unknown";
            }
            return deformable ? "deformable (has damage data)" : "rigid";
        }

        // Group nodes: how many of the parts below are deformable.
        private string DescribeDamage(TreeNode node)
        {
            if (this.sectionDeformable == null)
            {
                return "";
            }
            if (this.sectionDeformable.ContainsKey(node.Text) && node.Parent != null && node.Parent.Parent != null)
            {
                return "Damage: " + this.DescribeDamage(node.Text);
            }
            Dictionary<string, bool> parts = new Dictionary<string, bool>();
            this.CollectParts(node, parts);
            int deformable = 0;
            foreach (bool value in parts.Values)
            {
                if (value)
                {
                    deformable++;
                }
            }
            return string.Format("Deformable parts: {0} of {1}", deformable, parts.Count);
        }

        private void CollectParts(TreeNode node, Dictionary<string, bool> parts)
        {
            foreach (TreeNode child in node.Nodes)
            {
                bool deformable;
                if (child.Tag is ForzaMesh && this.sectionDeformable.TryGetValue(node.Text, out deformable))
                {
                    parts[node.Text] = deformable;
                }
                this.CollectParts(child, parts);
            }
        }

        private int CountMaterial(string material)
        {
            int count = 0;
            foreach (ForzaMesh mesh in GetMeshes(this.tvModel.Nodes, false))
            {
                if (mesh.MaterialName == material)
                {
                    count++;
                }
            }
            return count;
        }

        private static string DisplaySource(string source)
        {
            if (source == null)
            {
                return "(none)";
            }
            return source.StartsWith(ArchivePrefix) ? source.Substring(ArchivePrefix.Length) : Path.GetFileName(source) + " (folder)";
        }

        // Targets are the selected parts, widened to every part sharing their materials when requested.
        private List<ForzaMesh> TargetMeshes(out List<string> materials)
        {
            List<ForzaMesh> selected = this.SelectedMeshes;
            materials = new List<string>();
            foreach (ForzaMesh mesh in selected)
            {
                if (!materials.Contains(mesh.MaterialName))
                {
                    materials.Add(mesh.MaterialName);
                }
            }
            if (!this.chkSameMaterial.Checked)
            {
                return selected;
            }
            List<ForzaMesh> targets = new List<ForzaMesh>();
            foreach (ForzaMesh mesh in GetMeshes(this.tvModel.Nodes, false))
            {
                if (materials.Contains(mesh.MaterialName))
                {
                    targets.Add(mesh);
                }
            }
            return targets;
        }

        private void AssignTexture(string source)
        {
            List<string> materials;
            List<ForzaMesh> targets = this.TargetMeshes(out materials);
            if (targets.Count == 0)
            {
                return;
            }
            if (source != null && this.LoadPreview(source) == null)
            {
                return;
            }
            foreach (ForzaMesh mesh in targets)
            {
                if (source == null)
                {
                    this.meshTextures.Remove(mesh);
                }
                else
                {
                    this.meshTextures[mesh] = source;
                }
            }
            if (this.chkSameMaterial.Checked)
            {
                foreach (string material in materials)
                {
                    if (source == null)
                    {
                        this.materialTextures.Remove(material);
                    }
                    else
                    {
                        this.materialTextures[material] = source;
                    }
                }
            }
            this.ApplyTextures();
            this.UpdateMeshPanel();
            this.SetStatus(string.Format("{0} -> {1} ({2} parts)", string.Join(", ", materials.ToArray()), DisplaySource(source), targets.Count));
        }

        private void AssignTextureFromFile()
        {
            if (this.SelectedMeshes.Count == 0)
            {
                return;
            }
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "Textures (*.xds;*.tga)|*.xds;*.tga|Images (*.png;*.tga;*.bmp;*.jpg)|*.png;*.tga;*.bmp;*.jpg";
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    this.AssignTexture(dialog.FileName);
                }
            }
        }

        private void SetSecondUV(bool second)
        {
            List<string> materials;
            foreach (ForzaMesh mesh in this.TargetMeshes(out materials))
            {
                mesh.UseSecondUV = second;
            }
            if (this.chkSameMaterial.Checked)
            {
                foreach (string material in materials)
                {
                    this.materialSecondUV[material] = second;
                }
            }
            if (this.scene != null)
            {
                this.scene.Update(true);
            }
        }

        // Folder of the model being shown ("" for a zip root); archive texture sources are relative to it,
        // so the same assignments work in the zip and in the uncompressed car folder.
        private string ModelFolder
        {
            get { return (this.currentEntry == null) ? "" : this.currentEntry.Name.Substring(0, this.currentEntry.Name.LastIndexOf('/') + 1); }
        }

        private ForzaZip.Entry FindArchiveSource(string source)
        {
            return (this.zip == null) ? null : this.zip.Find(this.ModelFolder + source.Substring(ArchivePrefix.Length));
        }

        private bool SourceExists(string source)
        {
            if (source.StartsWith(ArchivePrefix))
            {
                return this.FindArchiveSource(source) != null;
            }
            return File.Exists(source);
        }

        private void ApplyTextures()
        {
            if (this.scene == null)
            {
                return;
            }
            this.scene.ClearTextures();
            foreach (KeyValuePair<ForzaMesh, string> pair in this.meshTextures)
            {
                this.scene.SetMeshTexture(pair.Key, this.LoadPreview(pair.Value));
            }
            this.scene.Update(true);
        }

        private void ClearPreviewCache()
        {
            if (this.scene != null)
            {
                this.scene.ClearTextures();
                this.scene.ReleaseGpuTextures();
            }
            foreach (Bitmap bitmap in this.previewCache.Values)
            {
                bitmap.Dispose();
            }
            this.previewCache.Clear();
        }

        // Decodes a texture source into the bitmap shown on the model (null if it cannot be read).
        private Bitmap LoadPreview(string source)
        {
            Bitmap preview;
            string key = source.StartsWith(ArchivePrefix) ? (ArchivePrefix + this.ModelFolder + source.Substring(ArchivePrefix.Length)) : source;
            if (this.previewCache.TryGetValue(key, out preview))
            {
                return preview;
            }
            try
            {
                Bitmap image;
                if (source.StartsWith(ArchivePrefix))
                {
                    ForzaZip.Entry entry = this.FindArchiveSource(source);
                    byte[] data = this.zip.Read(entry);
                    image = entry.Name.EndsWith(".tga", StringComparison.OrdinalIgnoreCase) ? new TgaImage(data).ToBitmap() : new XdsTexture(data).GetLevelBitmap(0);
                }
                else if (source.EndsWith(".xds", StringComparison.OrdinalIgnoreCase))
                {
                    image = XdsTexture.FromFile(source).GetLevelBitmap(0);
                }
                else if (source.EndsWith(".tga", StringComparison.OrdinalIgnoreCase))
                {
                    image = TgaImage.FromFile(source).ToBitmap();
                }
                else
                {
                    using (Image file = Image.FromFile(source))
                    {
                        image = new Bitmap(file);
                    }
                }
                preview = this.chkAlphaMask.Checked ? CompositeOver(image, BaseColor) : image;
                if (preview != image)
                {
                    image.Dispose();
                }
                this.previewCache[key] = preview;
                return preview;
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, "Cannot load " + DisplaySource(source) + ":\n" + exception.Message, "Preview Texture", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                return null;
            }
        }

        // Forza car textures use alpha to blend decals over the paint: preview them over a flat base color.
        private static Bitmap CompositeOver(Bitmap image, System.Drawing.Color baseColor)
        {
            Bitmap result = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb);
            Rectangle rect = new Rectangle(0, 0, image.Width, image.Height);
            BitmapData src = image.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            BitmapData dst = result.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            byte[] row = new byte[image.Width * 4];
            for (int y = 0; y < image.Height; y++)
            {
                Marshal.Copy(new IntPtr(src.Scan0.ToInt64() + ((long) y * src.Stride)), row, 0, row.Length);
                for (int i = 0; i < row.Length; i += 4)
                {
                    int a = row[i + 3];
                    row[i] = (byte) (((row[i] * a) + (baseColor.B * (255 - a))) / 255);
                    row[i + 1] = (byte) (((row[i + 1] * a) + (baseColor.G * (255 - a))) / 255);
                    row[i + 2] = (byte) (((row[i + 2] * a) + (baseColor.R * (255 - a))) / 255);
                    row[i + 3] = 255;
                }
                Marshal.Copy(row, 0, new IntPtr(dst.Scan0.ToInt64() + ((long) y * dst.Stride)), row.Length);
            }
            image.UnlockBits(src);
            result.UnlockBits(dst);
            return result;
        }
    }
}
