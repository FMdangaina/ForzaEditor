using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ForzaStudio
{
    public partial class ArchiveEditorForm
    {
        // Model > Export UV Map to TGA: the UV layout (LOD 0, first UV set) of the chosen materials of the whole car model
        // as a TGA, e.g. the digital dash screen, to draw the dash picture on. The window lists every material of the
        // main, lod0 and cockpit files; the ones that look like a dash / gauge are checked. Image y runs top-down as in the
        // texture file (v is flipped the way the OBJ export does it).
        private void ExportUvMap()
        {
            Dictionary<string, ForzaZip.Entry> files;
            Dictionary<string, CarbinModel> models = this.LoadCarModels("Export UV Map", out files);
            if (models == null)
            {
                return;
            }
            // material -> triangles (3 UV points each) and the files it is in
            Dictionary<string, List<PointF[]>> triangles = new Dictionary<string, List<PointF[]>>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, List<string>> where = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, CarbinModel> pair in models)
            {
                foreach (CarbinModel.Section section in pair.Value.Sections)
                {
                    ObjMesh mesh;
                    try
                    {
                        mesh = ObjMesh.FromLod(section, 0);
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                    foreach (ObjMesh.Material material in mesh.Materials)
                    {
                        List<PointF[]> list;
                        if (!triangles.TryGetValue(material.Name, out list))
                        {
                            list = new List<PointF[]>();
                            triangles[material.Name] = list;
                            where[material.Name] = new List<string>();
                        }
                        string place = pair.Key + ":" + section.Name;
                        if (!where[material.Name].Contains(place))
                        {
                            where[material.Name].Add(place);
                        }
                        foreach (ObjMesh.Corner[] t in material.Triangles)
                        {
                            list.Add(new PointF[] { new PointF(t[0].Uv.X, t[0].Uv.Y), new PointF(t[1].Uv.X, t[1].Uv.Y), new PointF(t[2].Uv.X, t[2].Uv.Y) });
                        }
                    }
                }
            }
            if (triangles.Count == 0)
            {
                MessageBox.Show(this, "The car model has no triangles.", "Export UV Map", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            List<string> names = new List<string>(triangles.Keys);
            names.Sort(StringComparer.OrdinalIgnoreCase);

            List<string> chosen = new List<string>();
            int width, height;
            using (Form dialog = new Form())
            {
                dialog.Text = "Export UV Map to TGA";
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.MinimizeBox = dialog.MaximizeBox = false;
                dialog.ClientSize = new Size(452, 380);
                Label l1 = new Label();
                l1.Text = "Materials to draw (LOD 0, all model files of the car):";
                l1.SetBounds(12, 10, 428, 18);
                CheckedListBox list = new CheckedListBox();
                list.CheckOnClick = true;
                list.SetBounds(12, 30, 428, 250);
                foreach (string name in names)
                {
                    string lower = name.ToLowerInvariant();
                    bool dash = lower.Contains("dash") || lower.Contains("digital") || lower.Contains("gauge") || lower.Contains("screen") || lower.Contains("display");
                    list.Items.Add(string.Format("{0}  ({1} triangles; {2})", name, triangles[name].Count, string.Join(", ", where[name].ToArray())), dash);
                }
                Label l2 = new Label();
                l2.Text = "Size:";
                l2.SetBounds(12, 292, 40, 18);
                NumericUpDown w = new NumericUpDown();
                w.Minimum = 16;
                w.Maximum = 8192;
                w.Value = 1024;
                w.SetBounds(56, 289, 70, 22);
                Label x = new Label();
                x.Text = "x";
                x.SetBounds(130, 292, 12, 18);
                NumericUpDown h = new NumericUpDown();
                h.Minimum = 16;
                h.Maximum = 8192;
                h.Value = 1024;
                h.SetBounds(144, 289, 70, 22);
                Label note = new Label();
                note.Text = "Black background, each material in its own colour (fill + edges). Set the size to the dash texture's.";
                note.SetBounds(12, 320, 428, 34);
                Button ok = new Button();
                ok.Text = "Export...";
                ok.DialogResult = DialogResult.OK;
                ok.SetBounds(266, 348, 84, 26);
                Button cancel = new Button();
                cancel.Text = "Cancel";
                cancel.DialogResult = DialogResult.Cancel;
                cancel.SetBounds(356, 348, 84, 26);
                dialog.Controls.AddRange(new Control[] { l1, list, l2, w, x, h, note, ok, cancel });
                dialog.AcceptButton = ok;
                dialog.CancelButton = cancel;
                ForzaTheme.Apply(dialog);
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                for (int i = 0; i < names.Count; i++)
                {
                    if (list.GetItemChecked(i))
                    {
                        chosen.Add(names[i]);
                    }
                }
                width = (int) w.Value;
                height = (int) h.Value;
            }
            if (chosen.Count == 0)
            {
                this.SetStatus("UV map: no material checked.");
                return;
            }
            string target;
            using (SaveFileDialog save = new SaveFileDialog())
            {
                save.Filter = "TGA image (*.tga)|*.tga";
                string car = (this.zip != null && !string.IsNullOrEmpty(this.zip.FilePath)) ? Path.GetFileNameWithoutExtension(this.zip.FilePath.TrimEnd('\\')) : "car";
                save.FileName = car + "_uvmap.tga";
                if (save.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                target = save.FileName;
            }
            int count = 0;
            byte[] bgra;
            using (Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.Clear(Color.Black);
                    g.SmoothingMode = SmoothingMode.None;
                    float sx = width, sy = height;
                    for (int m = 0; m < chosen.Count; m++)
                    {
                        Color color = Color.FromArgb(0, 255, 0);
                        if (chosen.Count > 1)
                        {
                            color = ColorFromHue((m * 0.618034f) % 1f);
                        }
                        using (SolidBrush fill = new SolidBrush(Color.FromArgb(70, color.R, color.G, color.B)))
                        using (Pen edge = new Pen(color, 1f))
                        {
                            foreach (PointF[] t in triangles[chosen[m]])
                            {
                                PointF[] p = new PointF[] { new PointF(t[0].X * sx, t[0].Y * sy), new PointF(t[1].X * sx, t[1].Y * sy), new PointF(t[2].X * sx, t[2].Y * sy) };
                                bool bad = false;
                                foreach (PointF q in p)
                                {
                                    if (float.IsNaN(q.X) || float.IsNaN(q.Y) || Math.Abs(q.X) > 1e6f || Math.Abs(q.Y) > 1e6f)
                                    {
                                        bad = true;
                                    }
                                }
                                if (bad)
                                {
                                    continue;
                                }
                                g.FillPolygon(fill, p);
                                g.DrawPolygon(edge, p);
                                count++;
                            }
                        }
                    }
                }
                BitmapData data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                bgra = new byte[width * height * 4];
                for (int row = 0; row < height; row++)
                {
                    Marshal.Copy(new IntPtr(data.Scan0.ToInt64() + ((long) row * data.Stride)), bgra, row * width * 4, width * 4);
                }
                bitmap.UnlockBits(data);
            }
            for (int i = 3; i < bgra.Length; i += 4)
            {
                bgra[i] = 255;
            }
            try
            {
                File.WriteAllBytes(target, TgaImage.Encode32(width, height, bgra));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Export UV Map", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                return;
            }
            this.SetStatus(string.Format("UV map: {0} triangles of {1} to {2} ({3}x{4})", count, string.Join(", ", chosen.ToArray()), target, width, height));
        }

        private static Color ColorFromHue(float hue)
        {
            float h = hue * 6f;
            int i = (int) Math.Floor(h);
            float f = h - i;
            int up = (int) (255 * f), down = 255 - up;
            switch (i % 6)
            {
                case 0: return Color.FromArgb(255, up, 80);
                case 1: return Color.FromArgb(down, 255, 80);
                case 2: return Color.FromArgb(80, 255, up);
                case 3: return Color.FromArgb(80, down, 255);
                case 4: return Color.FromArgb(up, 80, 255);
                default: return Color.FromArgb(255, 80, down);
            }
        }
    }
}
