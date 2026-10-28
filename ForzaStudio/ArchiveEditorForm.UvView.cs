namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Drawing2D;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Windows.Forms;

    // UV layout of the selected part(s), in the TEXTURE tab: the triangle edges of the UV set in use
    // (UV1 / UV2) drawn over the 0..1 square, with the preview texture faintly behind when there is one.
    // The small preview is redrawn when the selection, the UV set or the texture changes; "Open in
    // window" gives the same drawing in a resizable window with zoom and PNG export at any size (UvForm).
    public partial class ArchiveEditorForm
    {
        private const int UvViewSize = SideWidth;
        private const int UvMaxTriangles = 120000;   // more than this in one selection: every n-th triangle only
        private PictureBox picUv;
        private Label lblUvInfo;

        private void AddUvViewControls(FlowLayoutPanel column)
        {
            this.picUv = new PictureBox();
            this.picUv.Size = new Size(UvViewSize, UvViewSize);
            this.picUv.SizeMode = PictureBoxSizeMode.Normal;
            this.picUv.BackColor = System.Drawing.Color.FromArgb(24, 24, 24);
            this.picUv.Margin = new Padding(0, 2, 0, 2);
            this.picUv.Cursor = Cursors.Hand;
            this.picUv.DoubleClick += delegate { this.OpenUvWindow(); };
            this.lblUvInfo = SideNote("");
            column.Controls.Add(SideHeading("UV layout", false));
            column.Controls.Add(this.picUv);
            column.Controls.Add(SideWideButton("Open In Window...", delegate { this.OpenUvWindow(); }));
            column.Controls.Add(this.lblUvInfo);
        }

        private void OpenUvWindow()
        {
            List<ForzaMesh> meshes = this.SelectedMeshes;
            if (meshes.Count == 0)
            {
                MessageBox.Show(this, "Select a part in the tree.", "UV Layout", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            // own copy of the texture: the preview cache may dispose its bitmaps while the window is open
            Bitmap texture = null;
            string source;
            if (this.meshTextures.TryGetValue(meshes[0], out source) && source != null)
            {
                Bitmap cached = this.LoadPreview(source, true);
                if (cached != null)
                {
                    texture = new Bitmap(cached);
                }
            }
            string title = (this.tvModel.SelectedNode != null) ? this.tvModel.SelectedNode.Text : "";
            UvForm form = new UvForm(title, new List<ForzaMesh>(meshes), meshes[0].UseSecondUV, texture, Path.GetFileNameWithoutExtension(this.currentEntry.Name));
            form.Show(this);
        }

        private void RefreshUvView()
        {
            if (this.picUv == null)
            {
                return;
            }
            List<ForzaMesh> meshes = this.SelectedMeshes;
            Bitmap texture = null;
            string source;
            if (meshes.Count > 0 && this.btnTextures.Checked && this.meshTextures.TryGetValue(meshes[0], out source) && source != null)
            {
                texture = this.LoadPreview(source, true);
            }
            bool second = meshes.Count > 0 && meshes[0].UseSecondUV;
            int step;
            Bitmap image = UvForm.Render(meshes, second, UvViewSize, 6, texture, 1f, false, out step);
            int triangles = 0;
            foreach (ForzaMesh mesh in meshes)
            {
                triangles += mesh.FaceCount;
            }
            this.lblUvInfo.Text = (meshes.Count == 0) ? "Select a part in the tree."
                : string.Format("{0}, {1} part{2}, {3:N0} faces{4}. Double-click the picture to open it larger.",
                    second ? "UV2" : "UV1", meshes.Count, meshes.Count == 1 ? "" : "s", triangles, step > 1 ? " (every " + step + "th drawn)" : "");
            Image old = this.picUv.Image;
            this.picUv.Image = image;
            if (old != null)
            {
                old.Dispose();
            }
        }
    }

    // UV layout window: the selected parts' UV set over the 0..1 square, zoom with the box or Ctrl+wheel,
    // scroll when bigger than the window, export to PNG at the size typed (the zoom is only the view).
    public class UvForm : Form
    {
        private const int Margin = 6;
        private readonly List<ForzaMesh> meshes;
        private readonly Bitmap texture;
        private readonly string baseName;
        private PictureBox picture;
        private Panel scroller;
        private ComboBox cmbSet;
        private CheckBox chkTexture;
        private CheckBox chkTransparent;
        private NumericUpDown numZoom;
        private NumericUpDown numExport;
        private NumericUpDown numLine;
        private Label lblStatus;

        public UvForm(string title, List<ForzaMesh> meshes, bool secondUV, Bitmap texture, string baseName)
        {
            this.meshes = meshes;
            this.texture = texture;
            this.baseName = baseName;
            this.Text = "UV Layout - " + title;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Size = new Size(900, 760);
            this.KeyPreview = true;

            FlowLayoutPanel bar = new FlowLayoutPanel();
            bar.Dock = DockStyle.Top;
            bar.AutoSize = true;
            bar.Padding = new Padding(6, 4, 6, 4);

            this.cmbSet = new ComboBox();
            this.cmbSet.DropDownStyle = ComboBoxStyle.DropDownList;
            this.cmbSet.Items.AddRange(new object[] { "UV1", "UV2" });
            this.cmbSet.SelectedIndex = secondUV ? 1 : 0;
            this.cmbSet.Width = 60;
            this.cmbSet.SelectedIndexChanged += delegate { this.Redraw(); };

            this.chkTexture = new CheckBox();
            this.chkTexture.Text = "Texture";
            this.chkTexture.AutoSize = true;
            this.chkTexture.Checked = texture != null;
            this.chkTexture.Enabled = texture != null;
            this.chkTexture.CheckedChanged += delegate { this.Redraw(); };

            this.numLine = Number(1, 1, 8);
            this.numLine.DecimalPlaces = 0;
            this.numLine.ValueChanged += delegate { this.Redraw(); };

            this.numZoom = Number(600, 100, 8000);
            this.numZoom.Increment = 100;
            this.numZoom.ValueChanged += delegate { this.Redraw(); };

            this.numExport = Number(2048, 64, 16384);
            this.numExport.Increment = 256;

            this.chkTransparent = new CheckBox();
            this.chkTransparent.Text = "Transparent background";
            this.chkTransparent.AutoSize = true;

            Button export = new Button();
            export.Text = "Export PNG...";
            export.AutoSize = true;
            export.Click += delegate { this.Export(); };

            bar.Controls.Add(Caption("UV set"));
            bar.Controls.Add(this.cmbSet);
            bar.Controls.Add(this.chkTexture);
            bar.Controls.Add(Caption("Line"));
            bar.Controls.Add(this.numLine);
            bar.Controls.Add(Caption("View size (px)"));
            bar.Controls.Add(this.numZoom);
            bar.Controls.Add(Caption("Export size (px)"));
            bar.Controls.Add(this.numExport);
            bar.Controls.Add(this.chkTransparent);
            bar.Controls.Add(export);

            this.lblStatus = new Label();
            this.lblStatus.Dock = DockStyle.Bottom;
            this.lblStatus.Height = 20;
            this.lblStatus.ForeColor = SystemColors.GrayText;

            this.picture = new PictureBox();
            this.picture.SizeMode = PictureBoxSizeMode.Normal;
            this.scroller = new Panel();
            this.scroller.Dock = DockStyle.Fill;
            this.scroller.AutoScroll = true;
            this.scroller.BackColor = System.Drawing.Color.FromArgb(40, 40, 40);
            this.scroller.Controls.Add(this.picture);
            this.scroller.MouseWheel += this.OnWheel;
            this.picture.MouseWheel += this.OnWheel;
            this.picture.MouseEnter += delegate { this.picture.Focus(); };

            this.Controls.Add(this.scroller);
            this.Controls.Add(bar);
            this.Controls.Add(this.lblStatus);
            ForzaTheme.Apply(this);
            this.Load += delegate { this.FitView(); this.Redraw(); };
            this.FormClosed += delegate
            {
                if (this.picture.Image != null)
                {
                    this.picture.Image.Dispose();
                }
                if (this.texture != null)
                {
                    this.texture.Dispose();
                }
            };
        }

        private static Label Caption(string text)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.Margin = new Padding(8, 6, 2, 0);
            return label;
        }

        private static NumericUpDown Number(int value, int min, int max)
        {
            NumericUpDown box = new NumericUpDown();
            box.Minimum = min;
            box.Maximum = max;
            box.Value = value;
            box.Width = 70;
            return box;
        }

        // The view starts as large as the window allows while staying square.
        private void FitView()
        {
            int side = Math.Max(Math.Min(this.scroller.ClientSize.Width, this.scroller.ClientSize.Height) - 4, 100);
            this.numZoom.Value = Math.Min(this.numZoom.Maximum, Math.Max(this.numZoom.Minimum, side));
        }

        private void OnWheel(object sender, MouseEventArgs e)
        {
            if ((Control.ModifierKeys & Keys.Control) == 0)
            {
                return;
            }
            decimal value = this.numZoom.Value * (e.Delta > 0 ? 1.15m : 1m / 1.15m);
            this.numZoom.Value = Math.Min(this.numZoom.Maximum, Math.Max(this.numZoom.Minimum, Math.Round(value)));
            ((HandledMouseEventArgs) e).Handled = true;
        }

        private bool Second
        {
            get { return this.cmbSet.SelectedIndex == 1; }
        }

        private Bitmap Make(int size, bool transparent)
        {
            int step;
            Bitmap image = Render(this.meshes, this.Second, size, Margin, this.chkTexture.Checked ? this.texture : null, (float) this.numLine.Value, transparent, out step);
            int faces = 0;
            foreach (ForzaMesh mesh in this.meshes)
            {
                faces += mesh.FaceCount;
            }
            this.lblStatus.Text = string.Format("  {0}, {1} part{2}, {3:N0} faces{4}   Ctrl+wheel: zoom", this.Second ? "UV2" : "UV1", this.meshes.Count, this.meshes.Count == 1 ? "" : "s", faces,
                step > 1 ? ", every " + step + "th drawn" : "");
            return image;
        }

        private void Redraw()
        {
            if (this.picture == null || this.lblStatus == null)
            {
                return;
            }
            Bitmap image = this.Make((int) this.numZoom.Value, false);
            Image old = this.picture.Image;
            this.picture.Image = image;
            this.picture.Size = image.Size;
            if (old != null)
            {
                old.Dispose();
            }
        }

        private void Export()
        {
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Filter = "PNG image (*.png)|*.png";
                dialog.FileName = this.baseName + "_" + (this.Second ? "uv2" : "uv1") + ".png";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                try
                {
                    using (Bitmap image = this.Make((int) this.numExport.Value, this.chkTransparent.Checked))
                    {
                        image.Save(dialog.FileName, ImageFormat.Png);
                    }
                    this.Redraw();
                    this.lblStatus.Text = "  Saved " + dialog.FileName;
                }
                catch (Exception exception)
                {
                    MessageBox.Show(this, exception.Message, "Export UV Layout", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // size x size bitmap: the 0..1 square inside a margin, texture faint behind, triangle edges on top.
        // step > 1 when the selection was thinned to stay fast.
        public static Bitmap Render(List<ForzaMesh> meshes, bool second, int size, int margin, Bitmap texture, float lineWidth, bool transparent, out int step)
        {
            Bitmap image = new Bitmap(size, size);
            int side = size - 2 * margin;
            Rectangle square = new Rectangle(margin, margin, side, side);
            int triangles = 0;
            foreach (ForzaMesh mesh in meshes)
            {
                triangles += mesh.FaceCount;
            }
            step = Math.Max(1, (triangles + 120000 - 1) / 120000);
            using (Graphics g = Graphics.FromImage(image))
            {
                g.Clear(transparent ? System.Drawing.Color.Transparent : System.Drawing.Color.FromArgb(24, 24, 24));
                if (texture != null)
                {
                    try
                    {
                        using (ImageAttributes faint = new ImageAttributes())
                        {
                            faint.SetColorMatrix(new ColorMatrix(new float[][]
                            {
                                new float[] { 1, 0, 0, 0, 0 }, new float[] { 0, 1, 0, 0, 0 }, new float[] { 0, 0, 1, 0, 0 },
                                new float[] { 0, 0, 0, 0.45f, 0 }, new float[] { 0, 0, 0, 0, 1 }
                            }));
                            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                            g.DrawImage(texture, square, 0, 0, texture.Width, texture.Height, GraphicsUnit.Pixel, faint);
                        }
                    }
                    catch (Exception)
                    {
                        // a bitmap disposed under us: no background this time
                    }
                }
                using (Pen border = new Pen(System.Drawing.Color.FromArgb(120, 120, 120)))
                {
                    g.DrawRectangle(border, square);
                }
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (Pen pen = new Pen(System.Drawing.Color.FromArgb(transparent ? 255 : 200, 255, 70, 60), lineWidth))
                {
                    PointF[] tri = new PointF[4];
                    foreach (ForzaMesh mesh in meshes)
                    {
                        if (mesh.Indices == null || mesh.Vertices == null)
                        {
                            continue;
                        }
                        for (int i = 0; i + 2 < mesh.Indices.Length; i += 3 * step)
                        {
                            bool valid = true;
                            for (int k = 0; k < 3 && valid; k++)
                            {
                                int index = mesh.Indices[i + k];
                                if (index < 0 || index >= mesh.Vertices.Length)
                                {
                                    valid = false;
                                    break;
                                }
                                Microsoft.Xna.Framework.Vector2 uv = second ? mesh.Vertices[index].texture1 : mesh.Vertices[index].texture0;
                                // parsed UVs are bottom-up (OBJ convention), the square is drawn top-down
                                tri[k] = new PointF(square.Left + uv.X * side, square.Top + (1f - uv.Y) * side);
                            }
                            tri[3] = tri[0];
                            if (valid && IsFinite(tri))
                            {
                                g.DrawLines(pen, tri);
                            }
                        }
                    }
                }
            }
            return image;
        }

        private static bool IsFinite(PointF[] points)
        {
            foreach (PointF p in points)
            {
                if (float.IsNaN(p.X) || float.IsNaN(p.Y) || Math.Abs(p.X) > 1e5f || Math.Abs(p.Y) > 1e5f)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
