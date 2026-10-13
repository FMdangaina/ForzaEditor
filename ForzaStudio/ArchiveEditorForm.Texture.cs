namespace ForzaStudio
{
    using System;
    using System.Drawing;
    using System.Drawing.Drawing2D;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Windows.Forms;

    public partial class ArchiveEditorForm
    {
        private Panel pnlTexture;
        private BufferedPanel pnlImage;
        private Label lblTextureInfo;
        private ComboBox cmbMipLevel;
        private ComboBox cmbChannels;
        private XdsTexture texture;
        private TgaImage tga;
        private Label lblImportNote;
        private Bitmap textureBitmap;
        private byte[] xdcSource;   // the .xdc file when the texture shown is a compressed one

        private class BufferedPanel : Panel
        {
            public BufferedPanel()
            {
                this.DoubleBuffered = true;
                this.ResizeRedraw = true;
            }
        }

        private void BuildTextureView()
        {
            this.pnlTexture = new Panel();
            this.pnlTexture.Dock = DockStyle.Fill;

            this.pnlImage = new BufferedPanel();
            this.pnlImage.Dock = DockStyle.Fill;
            this.pnlImage.BackColor = System.Drawing.Color.FromArgb(40, 40, 40);
            this.pnlImage.Paint += new PaintEventHandler(this.pnlImage_Paint);

            FlowLayoutPanel side = new FlowLayoutPanel();
            side.Dock = DockStyle.Right;
            side.Width = 230;
            side.FlowDirection = FlowDirection.TopDown;
            side.WrapContents = false;
            side.Padding = new Padding(8);

            this.lblTextureInfo = new Label();
            this.lblTextureInfo.AutoSize = false;
            this.lblTextureInfo.Size = new Size(210, 120);

            this.cmbMipLevel = new ComboBox();
            this.cmbMipLevel.DropDownStyle = ComboBoxStyle.DropDownList;
            this.cmbMipLevel.Width = 210;
            this.cmbMipLevel.SelectedIndexChanged += delegate { this.UpdateTextureBitmap(); };

            this.cmbChannels = new ComboBox();
            this.cmbChannels.DropDownStyle = ComboBoxStyle.DropDownList;
            this.cmbChannels.Width = 210;
            this.cmbChannels.Items.AddRange(new object[] { "RGB + Alpha", "RGB only", "Alpha only" });
            this.cmbChannels.SelectedIndex = 0;
            this.cmbChannels.SelectedIndexChanged += delegate { this.UpdateTextureBitmap(); };

            side.Controls.Add(this.lblTextureInfo);
            side.Controls.Add(SideLabel("Mip level"));
            side.Controls.Add(this.cmbMipLevel);
            side.Controls.Add(SideLabel("Channels"));
            side.Controls.Add(this.cmbChannels);
            side.Controls.Add(SideLabel(""));
            side.Controls.Add(SideButton("Export Image...", delegate { this.ExportTexturePng(); }));
            side.Controls.Add(SideButton("Import Image...", delegate { this.ImportTextureImage(); }));
            this.lblImportNote = SideLabel("");
            this.lblImportNote.Size = new Size(210, 110);
            this.lblImportNote.ForeColor = SystemColors.GrayText;
            side.Controls.Add(this.lblImportNote);

            this.pnlTexture.Controls.Add(this.pnlImage);
            this.pnlTexture.Controls.Add(side);
        }

        private static Label SideLabel(string text)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = false;
            label.Size = new Size(210, 20);
            label.TextAlign = ContentAlignment.BottomLeft;
            return label;
        }

        private static Button SideButton(string text, EventHandler click)
        {
            Button button = new Button();
            button.Text = text;
            button.Size = new Size(210, 30);
            button.Click += click;
            return button;
        }

        private void ShowTexture(byte[] data)
        {
            this.ClearTexture();
            this.texture = new XdsTexture(data);
            this.lblTextureInfo.Text = string.Format("{0}\n\n{1} x {2}\nFormat: {3}\nColor space: {4}\nMip levels: {5}",
                Path.GetFileName(this.currentEntry.Name), this.texture.Width, this.texture.Height, this.texture.Codec,
                this.texture.IsGamma ? "gamma (sRGB-like)" : "linear", this.texture.LevelCount);
            if (this.xdcSource != null)
            {
                this.lblTextureInfo.Text += "\nStored compressed (.xdc)";
            }
            this.cmbMipLevel.BeginUpdate();
            this.cmbMipLevel.Items.Clear();
            for (int level = 0; level < this.texture.LevelCount; level++)
            {
                XdsTexture.Level info = this.texture.GetLevel(level);
                this.cmbMipLevel.Items.Add(string.Format("{0}: {1} x {2}", level, info.Width, info.Height));
            }
            this.lblImportNote.Text = "Import accepts PNG, TGA, BMP, TIF or DDS with the same size as the texture. It is compressed to the original format and mips are regenerated. Changes are written when the archive is saved.";
            this.cmbMipLevel.EndUpdate();
            this.ShowView(this.pnlTexture);
            this.cmbMipLevel.SelectedIndex = 0;
        }

        private void ClearTexture()
        {
            this.texture = null;
            this.tga = null;
            if (this.textureBitmap != null)
            {
                this.textureBitmap.Dispose();
                this.textureBitmap = null;
            }
        }

        private void UpdateTextureBitmap()
        {
            if ((this.texture == null && this.tga == null) || this.cmbMipLevel.SelectedIndex < 0)
            {
                return;
            }
            if (this.textureBitmap != null)
            {
                this.textureBitmap.Dispose();
            }
            Bitmap bitmap = (this.texture != null) ? this.texture.GetLevelBitmap(this.cmbMipLevel.SelectedIndex) : this.tga.ToBitmap();
            int mode = this.cmbChannels.SelectedIndex;
            if (mode > 0)
            {
                BitmapData data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
                byte[] pixels = new byte[data.Stride * bitmap.Height];
                Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
                for (int i = 0; i < pixels.Length; i += 4)
                {
                    if (mode == 2)
                    {
                        pixels[i] = pixels[i + 1] = pixels[i + 2] = pixels[i + 3];
                    }
                    pixels[i + 3] = 255;
                }
                Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
                bitmap.UnlockBits(data);
            }
            this.textureBitmap = bitmap;
            this.pnlImage.Invalidate();
        }

        private void pnlImage_Paint(object sender, PaintEventArgs e)
        {
            if (this.textureBitmap == null)
            {
                return;
            }
            Size area = this.pnlImage.ClientSize;
            float scale = Math.Min((area.Width - 16) / (float) this.textureBitmap.Width, (area.Height - 16) / (float) this.textureBitmap.Height);
            if (scale <= 0f)
            {
                return;
            }
            int width = (int) (this.textureBitmap.Width * scale);
            int height = (int) (this.textureBitmap.Height * scale);
            Rectangle target = new Rectangle((area.Width - width) / 2, (area.Height - height) / 2, width, height);

            using (HatchBrush checker = new HatchBrush(HatchStyle.LargeCheckerBoard, System.Drawing.Color.FromArgb(200, 200, 200), System.Drawing.Color.White))
            {
                e.Graphics.FillRectangle(checker, target);
            }
            e.Graphics.InterpolationMode = (scale >= 1f) ? InterpolationMode.NearestNeighbor : InterpolationMode.HighQualityBicubic;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
            e.Graphics.DrawImage(this.textureBitmap, target);
        }


        private void ShowTga(byte[] data)
        {
            this.ClearTexture();
            this.tga = new TgaImage(data);
            this.lblTextureInfo.Text = string.Format("{0}\n\n{1} x {2}\nFormat: TGA {3}\nAlpha bits declared: {4}",
                Path.GetFileName(this.currentEntry.Name), this.tga.Width, this.tga.Height, this.tga.Description, this.tga.AlphaBits);
            this.cmbMipLevel.BeginUpdate();
            this.cmbMipLevel.Items.Clear();
            this.cmbMipLevel.Items.Add(string.Format("0: {0} x {1}", this.tga.Width, this.tga.Height));
            this.cmbMipLevel.EndUpdate();
            this.lblImportNote.Text = "Import accepts PNG, TGA, BMP or TIF with the same size. It is saved in the original TGA format (same compression and bit depth), keeping RGB and alpha as separate data. Changes are written when the archive is saved.";
            this.ShowView(this.pnlTexture);
            this.cmbMipLevel.SelectedIndex = 0;
        }

        private void ExportTexturePng()
        {
            if (this.texture == null && this.tga == null)
            {
                return;
            }
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Filter = "PNG (*.png)|*.png|TGA 32 bit, alpha as separate channel (*.tga)|*.tga";
                dialog.FileName = Path.GetFileNameWithoutExtension(this.currentEntry.Name);
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                using (Bitmap bitmap = (this.texture != null) ? this.texture.GetLevelBitmap(0) : this.tga.ToBitmap())
                {
                    if (dialog.FileName.EndsWith(".tga", StringComparison.OrdinalIgnoreCase))
                    {
                        byte[] pixels = (this.texture != null) ? this.texture.DecodeLevel(0) : this.tga.Pixels;
                        File.WriteAllBytes(dialog.FileName, TgaImage.Encode32(bitmap.Width, bitmap.Height, pixels));
                    }
                    else
                    {
                        bitmap.Save(dialog.FileName, ImageFormat.Png);
                    }
                }
                this.SetStatus("Exported " + dialog.FileName);
            }
        }

        private void ImportTextureImage()
        {
            if (this.texture == null && this.tga == null)
            {
                return;
            }
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                if (this.texture != null)
                {
                    dialog.Title = string.Format("Import into {0} ({1} x {2} {3})", Path.GetFileName(this.currentEntry.Name), this.texture.Width, this.texture.Height, this.texture.Codec);
                    dialog.Filter = "Images (*.png;*.tga;*.bmp;*.tif;*.dds)|*.png;*.tga;*.bmp;*.tif;*.tiff;*.dds";
                }
                else
                {
                    dialog.Title = string.Format("Import into {0} ({1} x {2} TGA)", Path.GetFileName(this.currentEntry.Name), this.tga.Width, this.tga.Height);
                    dialog.Filter = "Images (*.png;*.tga;*.bmp;*.tif)|*.png;*.tga;*.bmp;*.tif;*.tiff";
                }
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                try
                {
                    Cursor.Current = Cursors.WaitCursor;
                    this.SetStatus("Encoding " + Path.GetFileName(dialog.FileName) + "...");
                    int level = this.cmbMipLevel.SelectedIndex;
                    byte[] data;
                    if (this.texture != null)
                    {
                        XdsTexture updated = new XdsTexture(this.texture.ToBytes());
                        updated.ImportImage(dialog.FileName, Path.Combine(Application.StartupPath, "texconv.exe"));
                        data = updated.ToBytes();
                        if (this.xdcSource != null)
                        {
                            this.xdcSource = XdcFile.Pack(data, this.xdcSource);
                            this.ReplaceCurrent(this.xdcSource);
                        }
                        else
                        {
                            this.ReplaceCurrent(data);
                        }
                        this.ShowTexture(data);
                    }
                    else
                    {
                        int width;
                        int height;
                        byte[] pixels = TgaImage.LoadPixels(dialog.FileName, out width, out height);
                        if (width != this.tga.Width || height != this.tga.Height)
                        {
                            throw new InvalidDataException(string.Format("The image is {0}x{1}, the TGA is {2}x{3}.", width, height, this.tga.Width, this.tga.Height));
                        }
                        TgaImage updated = new TgaImage(this.tga.ToBytes());
                        updated.SetPixels(pixels);
                        data = updated.ToBytes();
                        this.ReplaceCurrent(data);
                        this.ShowTga(data);
                    }
                    this.cmbMipLevel.SelectedIndex = Math.Min(level, this.cmbMipLevel.Items.Count - 1);
                    this.SetStatus(string.Format("Imported {0} into {1} - save the archive to keep it", Path.GetFileName(dialog.FileName), this.currentEntry.Name));
                }
                catch (Exception exception)
                {
                    this.SetStatus("Import failed");
                    MessageBox.Show(this, exception.Message, "Import Image", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                }
                finally
                {
                    Cursor.Current = Cursors.Default;
                }
            }
        }
    }
}
