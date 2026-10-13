namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Drawing;
    using System.Drawing.Design;
    using System.Drawing.Drawing2D;
    using System.Globalization;
    using System.Windows.Forms;
    using System.Windows.Forms.Design;

    // Color fields of the game database: 0xRRGGBB integers (factory paints, stripes, calipers...),
    // or ARGB / flag bits in the top byte (UI colors, special paints). Only R, G and B are edited,
    // the top byte is always kept.
    internal static class GameColor
    {
        public static bool IsColorColumn(GameDatabase.Column column)
        {
            if (column == null || column.Kind != GameDatabase.ValueKind.Integer || column.IsPrimaryKey)
            {
                return false;
            }
            string name = column.Name;
            string lower = name.ToLowerInvariant();
            if (lower.EndsWith("id") || lower.Contains("valid") || lower.Contains("sequence") || lower.StartsWith("has") || (lower.Contains("class") && !lower.EndsWith("color")))
            {
                return false;
            }
            return lower.Contains("rgb") || lower.EndsWith("color") || lower == "color2" || lower == "colorstripe";
        }

        public static Color ToColor(long value)
        {
            int rgb = (int) (value & 0xFFFFFF);
            return Color.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
        }

        // The new RGB with the old value's top byte (alpha or flags) kept.
        public static long WithColor(long original, Color color)
        {
            uint top = (uint) original & 0xFF000000;
            uint rgb = ((uint) color.R << 16) | ((uint) color.G << 8) | color.B;
            return (int) (top | rgb);
        }

        public static string Hex(Color color)
        {
            return string.Format("#{0:X2}{1:X2}{2:X2}", color.R, color.G, color.B);
        }

        public static readonly List<Color> Recent = new List<Color>();

        public static void Remember(Color color)
        {
            Recent.Remove(color);
            Recent.Insert(0, color);
            if (Recent.Count > 10)
            {
                Recent.RemoveAt(Recent.Count - 1);
            }
        }
    }

    // Hue/saturation wheel, brightness bar, R G B sliders, hex, old/new preview, recent colors.
    internal class ForzaColorPicker : UserControl
    {
        private readonly ColorWheel wheel;
        private readonly ChannelBar brightness;
        private readonly ChannelBar[] channels = new ChannelBar[3];
        private readonly NumericUpDown[] numbers = new NumericUpDown[3];
        private readonly TextBox hex;
        private readonly Panel preview;
        private readonly FlowLayoutPanel recent;
        private Color original;
        private Color color;
        private bool updating;

        public event EventHandler Accepted;
        public event EventHandler Cancelled;

        public ForzaColorPicker(Color start)
        {
            this.original = start;
            this.color = start;
            this.Size = new Size(372, 262);
            this.BackColor = ForzaTheme.Surface;
            this.ForeColor = ForzaTheme.Text;
            this.Font = ForzaTheme.Regular;

            this.wheel = new ColorWheel();
            this.wheel.Bounds = new Rectangle(10, 10, 170, 170);
            this.wheel.Changed += delegate { this.FromHsv(); };
            this.brightness = new ChannelBar(true);
            this.brightness.Bounds = new Rectangle(188, 10, 18, 170);
            this.brightness.Changed += delegate { this.FromHsv(); };

            string[] names = { "R", "G", "B" };
            for (int i = 0; i < 3; i++)
            {
                Label label = new Label();
                label.Text = names[i];
                label.Font = ForzaTheme.Bold;
                label.Bounds = new Rectangle(216, 14 + (i * 30), 14, 18);
                this.channels[i] = new ChannelBar(false);
                this.channels[i].Bounds = new Rectangle(232, 12 + (i * 30), 76, 20);
                this.channels[i].Changed += delegate { this.FromChannels(false); };
                this.numbers[i] = new NumericUpDown();
                this.numbers[i].Minimum = 0;
                this.numbers[i].Maximum = 255;
                this.numbers[i].Bounds = new Rectangle(312, 12 + (i * 30), 50, 20);
                this.numbers[i].BackColor = ForzaTheme.Raised;
                this.numbers[i].ForeColor = ForzaTheme.Text;
                this.numbers[i].BorderStyle = BorderStyle.FixedSingle;
                this.numbers[i].ValueChanged += delegate { this.FromChannels(true); };
                this.Controls.Add(label);
                this.Controls.Add(this.channels[i]);
                this.Controls.Add(this.numbers[i]);
            }

            Label hexLabel = new Label();
            hexLabel.Text = "Hex";
            hexLabel.Font = ForzaTheme.Bold;
            hexLabel.Bounds = new Rectangle(216, 106, 30, 18);
            this.hex = new TextBox();
            this.hex.Bounds = new Rectangle(250, 104, 112, 20);
            this.hex.BackColor = ForzaTheme.Raised;
            this.hex.ForeColor = ForzaTheme.Text;
            this.hex.BorderStyle = BorderStyle.FixedSingle;
            this.hex.CharacterCasing = CharacterCasing.Upper;
            this.hex.TextChanged += delegate { this.FromHex(); };

            this.preview = new Panel();
            this.preview.Bounds = new Rectangle(216, 134, 146, 46);
            this.preview.Paint += new PaintEventHandler(this.PaintPreview);
            this.preview.MouseClick += delegate(object sender, MouseEventArgs e)
            {
                if (e.X < this.preview.Width / 2)
                {
                    this.SetColor(this.original);
                }
            };

            this.recent = new FlowLayoutPanel();
            this.recent.Bounds = new Rectangle(10, 190, 352, 26);
            this.recent.BackColor = ForzaTheme.Surface;
            foreach (Color used in GameColor.Recent)
            {
                Color swatch = used;
                Panel chip = new Panel();
                chip.Size = new Size(22, 22);
                chip.Margin = new Padding(0, 0, 4, 0);
                chip.BackColor = swatch;
                chip.Cursor = Cursors.Hand;
                chip.Click += delegate { this.SetColor(swatch); };
                this.recent.Controls.Add(chip);
            }

            Button ok = new Button();
            ok.Text = "OK";
            ok.Bounds = new Rectangle(206, 226, 76, 26);
            ok.Click += delegate { this.Accept(); };
            Button cancel = new Button();
            cancel.Text = "Cancel";
            cancel.Bounds = new Rectangle(286, 226, 76, 26);
            cancel.Click += delegate { this.Cancel(); };
            Label hint = new Label();
            hint.Text = "Left half of the preview: original";
            hint.ForeColor = ForzaTheme.TextDim;
            hint.Bounds = new Rectangle(10, 230, 190, 18);

            this.Controls.Add(this.wheel);
            this.Controls.Add(this.brightness);
            this.Controls.Add(hexLabel);
            this.Controls.Add(this.hex);
            this.Controls.Add(this.preview);
            this.Controls.Add(this.recent);
            this.Controls.Add(ok);
            this.Controls.Add(cancel);
            this.Controls.Add(hint);
            ForzaTheme.Apply(this);
            this.SetColor(start);
        }

        public Color Value
        {
            get { return this.color; }
        }

        public bool IsAccepted { get; private set; }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Enter)
            {
                this.Accept();
                return true;
            }
            if (keyData == Keys.Escape)
            {
                this.Cancel();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void Accept()
        {
            this.IsAccepted = true;
            GameColor.Remember(this.color);
            if (this.Accepted != null)
            {
                this.Accepted(this, EventArgs.Empty);
            }
        }

        private void Cancel()
        {
            this.IsAccepted = false;
            if (this.Cancelled != null)
            {
                this.Cancelled(this, EventArgs.Empty);
            }
        }

        private void SetColor(Color value)
        {
            this.color = Color.FromArgb(255, value);
            this.updating = true;
            float hue = value.GetHue();
            float saturation;
            float brightnessValue;
            ToHsv(value, out saturation, out brightnessValue);
            this.wheel.Hue = hue;
            this.wheel.Saturation = saturation;
            this.wheel.Brightness = brightnessValue;
            this.brightness.Value = brightnessValue;
            this.updating = false;
            this.ShowColor(true, true);
        }

        private void FromHsv()
        {
            if (this.updating)
            {
                return;
            }
            this.wheel.Brightness = this.brightness.Value;
            this.color = FromHsv(this.wheel.Hue, this.wheel.Saturation, this.brightness.Value);
            this.ShowColor(true, true);
        }

        private void FromChannels(bool fromNumbers)
        {
            if (this.updating)
            {
                return;
            }
            int[] v = new int[3];
            for (int i = 0; i < 3; i++)
            {
                v[i] = fromNumbers ? (int) this.numbers[i].Value : (int) Math.Round(this.channels[i].Value * 255);
            }
            Color value = Color.FromArgb(255, v[0], v[1], v[2]);
            this.updating = true;
            float saturation;
            float brightnessValue;
            ToHsv(value, out saturation, out brightnessValue);
            if (saturation > 0)
            {
                this.wheel.Hue = value.GetHue();
            }
            this.wheel.Saturation = saturation;
            this.wheel.Brightness = brightnessValue;
            this.brightness.Value = brightnessValue;
            this.updating = false;
            this.color = value;
            this.ShowColor(!fromNumbers, true);
        }

        private void FromHex()
        {
            if (this.updating)
            {
                return;
            }
            string text = this.hex.Text.Trim().TrimStart('#');
            int rgb;
            if (text.Length == 6 && int.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out rgb))
            {
                Color value = GameColor.ToColor(rgb);
                this.updating = true;
                float saturation;
                float brightnessValue;
                ToHsv(value, out saturation, out brightnessValue);
                this.wheel.Hue = value.GetHue();
                this.wheel.Saturation = saturation;
                this.wheel.Brightness = brightnessValue;
                this.brightness.Value = brightnessValue;
                this.updating = false;
                this.color = value;
                this.ShowColor(true, false);
            }
        }

        // Puts the current color on every control except the one being edited.
        private void ShowColor(bool numbers, bool hexBox)
        {
            this.updating = true;
            int[] v = { this.color.R, this.color.G, this.color.B };
            for (int i = 0; i < 3; i++)
            {
                this.channels[i].Value = v[i] / 255f;
                this.channels[i].Gradient = this.ChannelGradient(i);
                if (numbers)
                {
                    this.numbers[i].Value = v[i];
                }
            }
            this.brightness.Gradient = new Color[] { FromHsv(this.wheel.Hue, this.wheel.Saturation, 1f), Color.Black };
            if (hexBox)
            {
                this.hex.Text = GameColor.Hex(this.color);
            }
            this.updating = false;
            this.wheel.Invalidate();
            this.preview.Invalidate();
        }

        private Color[] ChannelGradient(int channel)
        {
            Color low = Color.FromArgb(channel == 0 ? 0 : this.color.R, channel == 1 ? 0 : this.color.G, channel == 2 ? 0 : this.color.B);
            Color high = Color.FromArgb(channel == 0 ? 255 : this.color.R, channel == 1 ? 255 : this.color.G, channel == 2 ? 255 : this.color.B);
            return new Color[] { low, high };
        }

        private void PaintPreview(object sender, PaintEventArgs e)
        {
            int half = this.preview.Width / 2;
            using (Brush old = new SolidBrush(this.original))
            using (Brush now = new SolidBrush(this.color))
            {
                e.Graphics.FillRectangle(old, 0, 0, half, this.preview.Height);
                e.Graphics.FillRectangle(now, half, 0, this.preview.Width - half, this.preview.Height);
            }
            using (Pen pen = new Pen(ForzaTheme.Line))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, this.preview.Width - 1, this.preview.Height - 1);
            }
        }

        public static void ToHsv(Color color, out float saturation, out float value)
        {
            int max = Math.Max(color.R, Math.Max(color.G, color.B));
            int min = Math.Min(color.R, Math.Min(color.G, color.B));
            value = max / 255f;
            saturation = (max == 0) ? 0 : 1f - ((float) min / max);
        }

        public static Color FromHsv(float hue, float saturation, float value)
        {
            float c = value * saturation;
            float h = (hue % 360f) / 60f;
            float x = c * (1 - Math.Abs((h % 2) - 1));
            float r = 0, g = 0, b = 0;
            if (h < 1) { r = c; g = x; }
            else if (h < 2) { r = x; g = c; }
            else if (h < 3) { g = c; b = x; }
            else if (h < 4) { g = x; b = c; }
            else if (h < 5) { r = x; b = c; }
            else { r = c; b = x; }
            float m = value - c;
            return Color.FromArgb(255, Clamp(r + m), Clamp(g + m), Clamp(b + m));
        }

        private static int Clamp(float value)
        {
            return Math.Max(0, Math.Min(255, (int) Math.Round(value * 255)));
        }

        // Hue around, saturation outwards; the brightness darkens the whole disc.
        private class ColorWheel : Control
        {
            private Bitmap disc;
            public float Hue;
            public float Saturation;
            public float Brightness = 1f;
            public event EventHandler Changed;

            public ColorWheel()
            {
                this.DoubleBuffered = true;
                this.Cursor = Cursors.Cross;
            }

            private void BuildDisc()
            {
                int size = Math.Min(this.Width, this.Height);
                this.disc = new Bitmap(size, size);
                float radius = size / 2f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = x - radius + 0.5f;
                        float dy = y - radius + 0.5f;
                        float distance = (float) Math.Sqrt((dx * dx) + (dy * dy));
                        if (distance <= radius)
                        {
                            float hue = (float) ((Math.Atan2(-dy, dx) * 180 / Math.PI) + 360) % 360;
                            this.disc.SetPixel(x, y, FromHsv(hue, Math.Min(1f, distance / radius), 1f));
                        }
                    }
                }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                if (this.disc == null || this.disc.Width != Math.Min(this.Width, this.Height))
                {
                    this.BuildDisc();
                }
                e.Graphics.Clear(this.Parent.BackColor);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.DrawImage(this.disc, 0, 0);
                int size = this.disc.Width;
                using (Brush dark = new SolidBrush(Color.FromArgb((int) Math.Round((1 - this.Brightness) * 255), 0, 0, 0)))
                {
                    e.Graphics.FillEllipse(dark, 0, 0, size - 1, size - 1);
                }
                double angle = this.Hue * Math.PI / 180;
                float radius = size / 2f;
                float px = radius + (float) (Math.Cos(angle) * this.Saturation * radius);
                float py = radius - (float) (Math.Sin(angle) * this.Saturation * radius);
                using (Pen outer = new Pen(Color.Black, 3f))
                using (Pen inner = new Pen(Color.White, 1.5f))
                {
                    e.Graphics.DrawEllipse(outer, px - 6, py - 6, 12, 12);
                    e.Graphics.DrawEllipse(inner, px - 6, py - 6, 12, 12);
                }
            }

            private void Pick(Point point)
            {
                float radius = Math.Min(this.Width, this.Height) / 2f;
                float dx = point.X - radius;
                float dy = point.Y - radius;
                this.Hue = (float) ((Math.Atan2(-dy, dx) * 180 / Math.PI) + 360) % 360;
                this.Saturation = Math.Min(1f, (float) Math.Sqrt((dx * dx) + (dy * dy)) / radius);
                this.Invalidate();
                if (this.Changed != null)
                {
                    this.Changed(this, EventArgs.Empty);
                }
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                this.Pick(e.Location);
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                if (e.Button == MouseButtons.Left)
                {
                    this.Pick(e.Location);
                }
            }
        }

        // A gradient bar with a thumb (vertical for brightness, horizontal for R, G, B).
        private class ChannelBar : Control
        {
            private readonly bool vertical;
            private float value;
            public Color[] Gradient = { Color.Black, Color.White };
            public event EventHandler Changed;

            public ChannelBar(bool vertical)
            {
                this.vertical = vertical;
                this.DoubleBuffered = true;
                this.Cursor = Cursors.Hand;
            }

            public float Value
            {
                get { return this.value; }
                set
                {
                    this.value = Math.Max(0f, Math.Min(1f, value));
                    this.Invalidate();
                }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(this.Parent.BackColor);
                Rectangle bar = this.vertical ? new Rectangle(3, 0, this.Width - 6, this.Height) : new Rectangle(0, 4, this.Width, this.Height - 8);
                using (LinearGradientBrush brush = new LinearGradientBrush(bar, this.Gradient[0], this.Gradient[1], this.vertical ? 90f : 0f))
                {
                    e.Graphics.FillRectangle(brush, bar);
                }
                using (Pen pen = new Pen(ForzaTheme.Line))
                {
                    e.Graphics.DrawRectangle(pen, bar.X, bar.Y, bar.Width - 1, bar.Height - 1);
                }
                using (Pen thumb = new Pen(ForzaTheme.Red, 3f))
                {
                    if (this.vertical)
                    {
                        int y = (int) Math.Round((1 - this.value) * (this.Height - 1));
                        e.Graphics.DrawLine(thumb, 0, y, this.Width, y);
                    }
                    else
                    {
                        int x = (int) Math.Round(this.value * (this.Width - 1));
                        e.Graphics.DrawLine(thumb, x, 0, x, this.Height);
                    }
                }
            }

            private void Pick(Point point)
            {
                this.Value = this.vertical ? 1f - ((float) point.Y / Math.Max(1, this.Height - 1)) : (float) point.X / Math.Max(1, this.Width - 1);
                if (this.Changed != null)
                {
                    this.Changed(this, EventArgs.Empty);
                }
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                this.Pick(e.Location);
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                if (e.Button == MouseButtons.Left)
                {
                    this.Pick(e.Location);
                }
            }
        }

        // Shows the picker under a control (grid cells) and returns the value when it is accepted.
        public static void Popup(Control owner, Rectangle near, Color start, Action<Color> accepted)
        {
            ForzaColorPicker picker = new ForzaColorPicker(start);
            ToolStripControlHost host = new ToolStripControlHost(picker);
            host.Margin = Padding.Empty;
            host.Padding = Padding.Empty;
            ToolStripDropDown dropDown = new ToolStripDropDown();
            dropDown.Padding = Padding.Empty;
            dropDown.BackColor = ForzaTheme.Surface;
            dropDown.Items.Add(host);
            picker.Accepted += delegate
            {
                dropDown.Close();
                accepted(picker.Value);
            };
            picker.Cancelled += delegate { dropDown.Close(); };
            dropDown.Show(owner, new Point(near.Left, near.Bottom));
            picker.Focus();
        }
    }

    // The PropertyGrid drop-down for color fields, with a swatch in the value cell.
    internal class ColorValueEditor : UITypeEditor
    {
        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
        {
            return UITypeEditorEditStyle.DropDown;
        }

        public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
        {
            IWindowsFormsEditorService service = (provider == null) ? null : (IWindowsFormsEditorService) provider.GetService(typeof(IWindowsFormsEditorService));
            if (service == null)
            {
                return value;
            }
            long original = Parse(value as string);
            ForzaColorPicker picker = new ForzaColorPicker(GameColor.ToColor(original));
            picker.Accepted += delegate { service.CloseDropDown(); };
            picker.Cancelled += delegate { service.CloseDropDown(); };
            service.DropDownControl(picker);
            if (!picker.IsAccepted)
            {
                return value;
            }
            return GameColor.WithColor(original, picker.Value).ToString(CultureInfo.InvariantCulture);
        }

        public override bool GetPaintValueSupported(ITypeDescriptorContext context)
        {
            return true;
        }

        public override void PaintValue(PaintValueEventArgs e)
        {
            using (Brush brush = new SolidBrush(GameColor.ToColor(Parse(e.Value as string))))
            {
                e.Graphics.FillRectangle(brush, e.Bounds);
            }
        }

        // "12237498  #BAB9BA" (or a bare number) -> 12237498
        public static long Parse(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }
            text = text.Trim();
            int end = 0;
            while (end < text.Length && (char.IsDigit(text[end]) || (end == 0 && text[end] == '-')))
            {
                end++;
            }
            long value;
            return (end > 0 && long.TryParse(text.Substring(0, end), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value)) ? value : 0;
        }
    }
}
