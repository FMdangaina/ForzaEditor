namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Drawing2D;
    using System.Windows.Forms;

    // UV layout of the selected part(s), in the TEXTURE tab: the triangle edges of the UV set in use
    // (UV1 / UV2) drawn over the 0..1 square, with the preview texture faintly behind when there is one.
    // Drawn into a bitmap when the selection, the UV set or the texture changes, not on every paint.
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
            this.lblUvInfo = SideNote("");
            column.Controls.Add(SideHeading("UV layout", false));
            column.Controls.Add(this.picUv);
            column.Controls.Add(this.lblUvInfo);
        }

        private void RefreshUvView()
        {
            if (this.picUv == null)
            {
                return;
            }
            List<ForzaMesh> meshes = this.SelectedMeshes;
            Bitmap image = new Bitmap(UvViewSize, UvViewSize);
            using (Graphics g = Graphics.FromImage(image))
            {
                g.Clear(this.picUv.BackColor);
                const int margin = 6;
                int side = UvViewSize - 2 * margin;
                Rectangle square = new Rectangle(margin, margin, side, side);
                if (meshes.Count > 0)
                {
                    this.DrawUvBackground(g, square, meshes[0]);
                }
                using (Pen border = new Pen(System.Drawing.Color.FromArgb(120, 120, 120)))
                {
                    g.DrawRectangle(border, square);
                }
                int triangles = 0;
                foreach (ForzaMesh mesh in meshes)
                {
                    triangles += mesh.FaceCount;
                }
                int step = Math.Max(1, (triangles + UvMaxTriangles - 1) / UvMaxTriangles);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.SetClip(new Rectangle(0, 0, UvViewSize, UvViewSize));
                using (Pen pen = new Pen(System.Drawing.Color.FromArgb(200, 255, 70, 60), 1f))
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
                                Microsoft.Xna.Framework.Vector2 uv = mesh.UseSecondUV ? mesh.Vertices[index].texture1 : mesh.Vertices[index].texture0;
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
                this.lblUvInfo.Text = (meshes.Count == 0) ? "Select a part in the tree."
                    : string.Format("{0}, {1} part{2}, {3:N0} faces{4}. The square is UV 0..1; lines outside it are tiled.",
                        (meshes[0].UseSecondUV ? "UV2" : "UV1"), meshes.Count, meshes.Count == 1 ? "" : "s", triangles, step > 1 ? " (every " + step + "th drawn)" : "");
            }
            Image old = this.picUv.Image;
            this.picUv.Image = image;
            if (old != null)
            {
                old.Dispose();
            }
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

        // The texture the first selected part is drawn with, faint behind the lines.
        private void DrawUvBackground(Graphics g, Rectangle square, ForzaMesh mesh)
        {
            string source;
            if (!this.btnTextures.Checked || !this.meshTextures.TryGetValue(mesh, out source) || source == null)
            {
                return;
            }
            Bitmap texture = this.LoadPreview(source, true);
            if (texture == null)
            {
                return;
            }
            try
            {
                using (System.Drawing.Imaging.ImageAttributes faint = new System.Drawing.Imaging.ImageAttributes())
                {
                    faint.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix(new float[][]
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
    }
}
