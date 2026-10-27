using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace ForzaStudio
{
    public class DashEditEventArgs : EventArgs
    {
        public readonly string What;

        public DashEditEventArgs(string what)
        {
            this.What = what;
        }
    }

    // Draws an AnarkDash: textured or coloured rectangles and texts, far to near.
    // Wheel = zoom at the cursor, click = select (SelectionChanged), double-click = fit.
    // Drag on an element = move it, drag a handle of the selected one = resize (Shift keeps the shape), arrows nudge
    // (Shift = 10); a drag on the background, or with the middle / right button, pans. The edits go to the AnarkDash
    // and are announced by Edited when the mouse is released.
    public class AnarkDashCanvas : Control
    {
        public delegate Bitmap TextureLoader(string gamePath);

        private AnarkDash dash;
        private TextureLoader loader;
        private readonly Dictionary<string, Bitmap> textures = new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);
        private float zoom = 1;
        private PointF center;                     // world point at the middle of the control
        private Point dragStart;
        private PointF dragCenter;
        private bool dragging;
        private bool dragged;
        private List<AnarkDash.Drag> edits;        // move (one drag per selected node) or resize (one) in progress; null otherwise
        private bool pickedForEdit;                // the mouse went down on an element or a handle
        private bool banding;                      // a rubber band is being drawn on the background
        private Rectangle band;                    // in screen pixels
        private bool bandAdds;

        private const int HandleSize = 7;

        // The selection: the last node is the main one (shown in the inspector, resized by the handles).
        public readonly List<AnarkDash.Node> Selection = new List<AnarkDash.Node>();

        public AnarkDash.Node Selected
        {
            get { return (this.Selection.Count > 0) ? this.Selection[this.Selection.Count - 1] : null; }
            set
            {
                this.Selection.Clear();
                if (value != null)
                {
                    this.Selection.Add(value);
                }
            }
        }

        private bool IsSelected(AnarkDash.Node node)
        {
            foreach (AnarkDash.Node s in this.Selection)
            {
                if (node == s || IsInside(node, s))
                {
                    return true;
                }
            }
            return false;
        }

        private void RaiseSelectionChanged()
        {
            this.Invalidate();
            if (this.SelectionChanged != null)
            {
                this.SelectionChanged(this, EventArgs.Empty);
            }
        }
        public bool ShowOutlines;
        public event EventHandler SelectionChanged;
        public event EventHandler DuplicateRequested;         // Ctrl+D
        public event EventHandler DeleteRequested;            // Del
        public event EventHandler Dragging;                  // while a move / resize is going on
        public event EventHandler<DashEditEventArgs> Edited;  // after one (What: "position", "size")
        public event EventHandler<DashEditEventArgs> EditBlocked;

        public AnarkDashCanvas()
        {
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            this.BackColor = Color.FromArgb(8, 8, 9);
        }

        public AnarkDash Dash
        {
            get { return this.dash; }
        }

        public void SetDash(AnarkDash dash, TextureLoader loader)
        {
            foreach (Bitmap bitmap in this.textures.Values)
            {
                if (bitmap != null)
                {
                    bitmap.Dispose();
                }
            }
            this.textures.Clear();
            this.dash = dash;
            this.loader = loader;
            this.Selected = null;
            this.Fit();
        }

        // null when the texture is missing from the zip (drawn as a hatched rectangle)
        public Bitmap GetTexture(string path)
        {
            Bitmap bitmap;
            if (!this.textures.TryGetValue(path, out bitmap))
            {
                try
                {
                    bitmap = (this.loader != null) ? this.loader(path) : null;
                }
                catch
                {
                    bitmap = null;
                }
                this.textures[path] = bitmap;
            }
            return bitmap;
        }

        public RectangleF ItemBounds(AnarkDash.Item item)
        {
            if (item.Kind == AnarkDash.ItemKind.Text)
            {
                SizeF size = this.TextSize(item);
                PointF[] box = this.TextBox(item, size);
                return Enclose(box);
            }
            return Enclose(item.Corners);
        }

        public void Fit()
        {
            if (this.dash == null || this.dash.Items.Count == 0)
            {
                this.zoom = 1;
                this.center = PointF.Empty;
                this.Invalidate();
                return;
            }
            RectangleF all = RectangleF.Empty;
            foreach (AnarkDash.Item item in this.dash.Items)
            {
                RectangleF r = this.ItemBounds(item);
                all = all.IsEmpty ? r : RectangleF.Union(all, r);
            }
            this.center = new PointF(all.X + all.Width / 2, all.Y + all.Height / 2);
            float w = Math.Max(1, this.ClientSize.Width - 40), h = Math.Max(1, this.ClientSize.Height - 40);
            this.zoom = Math.Min(w / Math.Max(1, all.Width), h / Math.Max(1, all.Height));
            this.Invalidate();
        }

        public PointF ToScreen(PointF world, Size client)
        {
            return new PointF(client.Width / 2f + (world.X - this.center.X) * this.zoom, client.Height / 2f - (world.Y - this.center.Y) * this.zoom);
        }

        public PointF ToWorld(Point screen)
        {
            return new PointF((screen.X - this.ClientSize.Width / 2f) / this.zoom + this.center.X, -(screen.Y - this.ClientSize.Height / 2f) / this.zoom + this.center.Y);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            this.Render(e.Graphics, this.ClientSize);
        }

        public void Render(Graphics g, Size client)
        {
            g.Clear(this.BackColor);
            if (this.dash == null)
            {
                return;
            }
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            using (Pen axis = new Pen(Color.FromArgb(40, 40, 44)))
            {
                PointF o = this.ToScreen(PointF.Empty, client);
                g.DrawLine(axis, 0, o.Y, client.Width, o.Y);
                g.DrawLine(axis, o.X, 0, o.X, client.Height);
            }
            foreach (AnarkDash.Item item in this.dash.Items)
            {
                if (item.Kind == AnarkDash.ItemKind.Rectangle)
                {
                    this.DrawRectangle(g, item, client);
                }
                else
                {
                    this.DrawText(g, item, client);
                }
            }
            foreach (AnarkDash.Item item in this.dash.Items)
            {
                bool selected = this.IsSelected(item.Node);
                if (!selected && !this.ShowOutlines)
                {
                    continue;
                }
                PointF[] outline = (item.Kind == AnarkDash.ItemKind.Text) ? this.TextBox(item, this.TextSize(item)) : item.Corners;
                PointF[] screen = new PointF[outline.Length];
                for (int k = 0; k < outline.Length; k++)
                {
                    screen[k] = this.ToScreen(outline[k], client);
                }
                using (Pen pen = new Pen(selected ? Color.FromArgb(226, 38, 30) : Color.FromArgb(90, 120, 160, 220), selected ? 2 : 1))
                {
                    g.DrawPolygon(pen, screen);
                }
            }
            this.DrawHandles(g, client);
            if (this.banding)
            {
                using (Pen pen = new Pen(Color.FromArgb(200, 120, 160, 220)))
                using (SolidBrush fill = new SolidBrush(Color.FromArgb(40, 120, 160, 220)))
                {
                    g.FillRectangle(fill, this.band);
                    g.DrawRectangle(pen, this.band);
                }
            }
        }

        // The 8 handles (corners and edge middles) of the selected rectangle, as fractions of its mesh rectangle.
        private static readonly float[][] Handles = new float[][]
        {
            new float[] { 0, 0 }, new float[] { 0.5f, 0 }, new float[] { 1, 0 }, new float[] { 1, 0.5f },
            new float[] { 1, 1 }, new float[] { 0.5f, 1 }, new float[] { 0, 1 }, new float[] { 0, 0.5f },
        };

        private AnarkDash.Item SelectedRectangle()
        {
            AnarkDash.Item item = (this.dash != null && this.Selection.Count == 1) ? this.dash.ItemOf(this.Selected) : null;
            return (item != null && item.Kind == AnarkDash.ItemKind.Rectangle) ? item : null;
        }

        private PointF HandleWorld(AnarkDash.Item item, float[] h)
        {
            RectangleF r = item.LocalRect;
            return item.Node.Transform(r.Left + h[0] * r.Width, r.Top + h[1] * r.Height);
        }

        // index in Handles under a screen point, or -1
        private int HandleAt(Point screen)
        {
            AnarkDash.Item item = this.SelectedRectangle();
            if (item == null)
            {
                return -1;
            }
            for (int k = 0; k < Handles.Length; k++)
            {
                PointF p = this.ToScreen(this.HandleWorld(item, Handles[k]), this.ClientSize);
                if (Math.Abs(p.X - screen.X) <= HandleSize && Math.Abs(p.Y - screen.Y) <= HandleSize)
                {
                    return k;
                }
            }
            return -1;
        }

        private void DrawHandles(Graphics g, Size client)
        {
            AnarkDash.Item item = this.SelectedRectangle();
            if (item == null)
            {
                return;
            }
            using (Pen pen = new Pen(Color.FromArgb(226, 38, 30)))
            {
                foreach (float[] h in Handles)
                {
                    PointF p = this.ToScreen(this.HandleWorld(item, h), client);
                    RectangleF box = new RectangleF(p.X - HandleSize / 2f, p.Y - HandleSize / 2f, HandleSize, HandleSize);
                    g.FillRectangle(Brushes.White, box);
                    g.DrawRectangle(pen, box.X, box.Y, box.Width, box.Height);
                }
            }
        }

        private static Cursor HandleCursor(int k)
        {
            switch (k)
            {
                case 0:
                case 4:
                    return Cursors.SizeNESW;
                case 2:
                case 6:
                    return Cursors.SizeNWSE;
                case 1:
                case 5:
                    return Cursors.SizeNS;
                default:
                    return Cursors.SizeWE;
            }
        }

        private static bool IsInside(AnarkDash.Node node, AnarkDash.Node ancestor)
        {
            for (AnarkDash.Node p = node.Parent; p != null; p = p.Parent)
            {
                if (p == ancestor)
                {
                    return true;
                }
            }
            return false;
        }

        private void DrawRectangle(Graphics g, AnarkDash.Item item, Size client)
        {
            if (item.Color.A == 0)
            {
                return;
            }
            PointF bl = this.ToScreen(item.Corners[0], client), br = this.ToScreen(item.Corners[1], client);
            PointF tr = this.ToScreen(item.Corners[2], client), tl = this.ToScreen(item.Corners[3], client);
            Bitmap bitmap = (item.Texture != null) ? this.GetTexture(item.Texture) : null;
            if (bitmap != null)
            {
                using (ImageAttributes attributes = new ImageAttributes())
                {
                    ColorMatrix matrix = new ColorMatrix();
                    matrix.Matrix00 = item.Color.R / 255f;
                    matrix.Matrix11 = item.Color.G / 255f;
                    matrix.Matrix22 = item.Color.B / 255f;
                    matrix.Matrix33 = item.Color.A / 255f;
                    attributes.SetColorMatrix(matrix);
                    attributes.SetWrapMode(WrapMode.TileFlipXY);
                    g.DrawImage(bitmap, new PointF[] { tl, tr, bl }, new RectangleF(0, 0, bitmap.Width, bitmap.Height), GraphicsUnit.Pixel, attributes);
                }
                return;
            }
            PointF[] quad = new PointF[] { bl, br, tr, tl };
            if (item.Texture != null)
            {
                using (HatchBrush hatch = new HatchBrush(HatchStyle.BackwardDiagonal, Color.FromArgb(item.Color.A, 200, 60, 200), Color.Transparent))
                {
                    g.FillPolygon(hatch, quad);
                }
                return;
            }
            using (SolidBrush brush = new SolidBrush(item.Color))
            {
                g.FillPolygon(brush, quad);
            }
        }

        private static Font MakeFont(AnarkDash.Item item, float size)
        {
            string name = (item.Font ?? "").ToLowerInvariant();
            FontStyle style = name.Contains("bold") ? FontStyle.Bold : FontStyle.Regular;
            string family = (name.Contains("lcd") || name.Contains("quartz") || name.Contains("seven")) ? "Consolas" : "Arial";
            return new Font(family, Math.Max(1, size), style, GraphicsUnit.Pixel);
        }

        private SizeF TextSize(AnarkDash.Item item)
        {
            using (Bitmap b = new Bitmap(1, 1))
            using (Graphics g = Graphics.FromImage(b))
            using (Font font = MakeFont(item, item.Size))
            {
                return g.MeasureString(item.Text, font, PointF.Empty, StringFormat.GenericTypographic);
            }
        }

        // text box corners in the world, from the alignment around the node origin
        private PointF[] TextBox(AnarkDash.Item item, SizeF size)
        {
            float left = (item.HorzAlign == 1) ? -size.Width / 2 : (item.HorzAlign == 2) ? -size.Width : 0;
            float top = (item.VertAlign == 0) ? 0 : (item.VertAlign == 2) ? size.Height : size.Height / 2;
            AnarkDash.Node n = item.Node;
            return new PointF[] { n.Transform(left, top - size.Height), n.Transform(left + size.Width, top - size.Height), n.Transform(left + size.Width, top), n.Transform(left, top) };
        }

        private void DrawText(Graphics g, AnarkDash.Item item, Size client)
        {
            if (string.IsNullOrEmpty(item.Text) || item.Color.A == 0)
            {
                return;
            }
            AnarkDash.Node n = item.Node;
            PointF origin = this.ToScreen(new PointF(n.X, n.Y), client);
            GraphicsState state = g.Save();
            // text space: u right, v down; world y = -v
            g.Transform = new Matrix(this.zoom * n.A, -this.zoom * n.B, -this.zoom * n.C, this.zoom * n.D, origin.X, origin.Y);
            using (Font font = MakeFont(item, item.Size))
            using (SolidBrush brush = new SolidBrush(item.Color))
            using (StringFormat format = new StringFormat(StringFormat.GenericTypographic))
            {
                format.Alignment = (item.HorzAlign == 1) ? StringAlignment.Center : (item.HorzAlign == 2) ? StringAlignment.Far : StringAlignment.Near;
                format.LineAlignment = (item.VertAlign == 0) ? StringAlignment.Near : (item.VertAlign == 2) ? StringAlignment.Far : StringAlignment.Center;
                format.FormatFlags |= StringFormatFlags.NoWrap | StringFormatFlags.NoClip;
                g.DrawString(item.Text, font, brush, PointF.Empty, format);
            }
            g.Restore(state);
        }

        private static RectangleF Enclose(PointF[] points)
        {
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            foreach (PointF p in points)
            {
                x0 = Math.Min(x0, p.X);
                y0 = Math.Min(y0, p.Y);
                x1 = Math.Max(x1, p.X);
                y1 = Math.Max(y1, p.Y);
            }
            return RectangleF.FromLTRB(x0, y0, x1, y1);
        }

        private static bool Contains(PointF[] polygon, PointF p)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                if ((polygon[i].Y > p.Y) != (polygon[j].Y > p.Y)
                    && p.X < (polygon[j].X - polygon[i].X) * (p.Y - polygon[i].Y) / (polygon[j].Y - polygon[i].Y) + polygon[i].X)
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        // the nearest drawn item under a world point
        public AnarkDash.Node Pick(PointF world)
        {
            if (this.dash == null)
            {
                return null;
            }
            for (int k = this.dash.Items.Count - 1; k >= 0; k--)
            {
                AnarkDash.Item item = this.dash.Items[k];
                PointF[] shape = (item.Kind == AnarkDash.ItemKind.Text) ? this.TextBox(item, this.TextSize(item)) : item.Corners;
                if (item.Color.A > 0 && Contains(shape, world))
                {
                    return item.Node;
                }
            }
            return null;
        }

        private bool panning;                     // the drag pans (middle / right button)

        // Selected nodes that move: the ones not inside another selected node (those follow their parent).
        public List<AnarkDash.Node> TopSelection()
        {
            List<AnarkDash.Node> top = new List<AnarkDash.Node>();
            foreach (AnarkDash.Node node in this.Selection)
            {
                bool inside = false;
                for (AnarkDash.Node p = node.Parent; p != null && !inside; p = p.Parent)
                {
                    inside = this.Selection.Contains(p);
                }
                if (!inside)
                {
                    top.Add(node);
                }
            }
            return top;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            this.Focus();
            this.dragging = true;
            this.dragged = false;
            this.edits = null;
            this.pickedForEdit = false;
            this.banding = false;
            this.bandAdds = false;
            this.panning = this.dash == null || e.Button != MouseButtons.Left;
            this.dragStart = e.Location;
            this.dragCenter = this.center;
            if (this.panning)
            {
                return;
            }
            bool ctrl = (Control.ModifierKeys & Keys.Control) != 0;
            int handle = this.HandleAt(e.Location);
            if (handle >= 0)
            {
                this.pickedForEdit = true;
                AnarkDash.Drag resize = this.dash.BeginResize(this.Selected, Handles[handle][0], Handles[handle][1]);
                if (resize != null)
                {
                    this.edits = new List<AnarkDash.Drag>();
                    this.edits.Add(resize);
                }
                else
                {
                    this.ReportBlocked(this.dash.Blocked);
                }
                return;
            }
            PointF world = this.ToWorld(e.Location);
            AnarkDash.Node hit = this.Pick(world);
            if (hit == null)
            {
                this.bandAdds = ctrl;              // background: a rubber band (Ctrl adds to the selection)
                return;
            }
            this.pickedForEdit = true;
            if (ctrl)
            {
                if (!this.Selection.Remove(hit))
                {
                    this.Selection.Add(hit);
                }
                this.RaiseSelectionChanged();
                return;
            }
            // a drag on a selected element (or inside a selected group) moves the whole selection
            if (!this.IsSelected(hit))
            {
                this.Selected = hit;
                this.RaiseSelectionChanged();
            }
            List<AnarkDash.Drag> moves = new List<AnarkDash.Drag>();
            string blocked = null;
            foreach (AnarkDash.Node node in this.TopSelection())
            {
                AnarkDash.Drag move = this.dash.BeginMove(node, world);
                if (move != null)
                {
                    moves.Add(move);
                }
                else if (this.dash.Blocked != null)
                {
                    blocked = this.dash.Blocked;
                }
            }
            if (moves.Count > 0)
            {
                this.edits = moves;
            }
            else
            {
                this.ReportBlocked(blocked);
            }
        }

        private void ReportBlocked(string message)
        {
            if (message != null && this.EditBlocked != null)
            {
                this.EditBlocked(this, new DashEditEventArgs(message));
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!this.dragging)
            {
                int handle = this.HandleAt(e.Location);
                this.Cursor = (handle >= 0) ? HandleCursor(handle) : Cursors.Default;
                return;
            }
            if (!this.dragged && Math.Abs(e.X - this.dragStart.X) <= 3 && Math.Abs(e.Y - this.dragStart.Y) <= 3)
            {
                return;
            }
            this.dragged = true;
            if (this.edits != null)
            {
                PointF world = this.ToWorld(e.Location);
                bool keep = (Control.ModifierKeys & Keys.Shift) != 0;
                foreach (AnarkDash.Drag d in this.edits)
                {
                    this.dash.ApplyDrag(d, world, keep);
                }
                this.Invalidate();
                if (this.Dragging != null)
                {
                    this.Dragging(this, EventArgs.Empty);
                }
            }
            else if (this.panning)
            {
                this.center = new PointF(this.dragCenter.X - (e.X - this.dragStart.X) / this.zoom, this.dragCenter.Y + (e.Y - this.dragStart.Y) / this.zoom);
                this.Invalidate();
            }
            else if (!this.pickedForEdit)
            {
                this.banding = true;
                this.band = Rectangle.FromLTRB(Math.Min(this.dragStart.X, e.X), Math.Min(this.dragStart.Y, e.Y), Math.Max(this.dragStart.X, e.X), Math.Max(this.dragStart.Y, e.Y));
                this.Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            this.dragging = false;
            List<AnarkDash.Drag> finished = this.edits;
            this.edits = null;
            if (this.banding)
            {
                this.banding = false;
                this.SelectInBand();
                return;
            }
            if (finished != null && this.dragged)
            {
                if (this.Edited != null)
                {
                    this.Edited(this, new DashEditEventArgs(finished[0].What));
                }
                return;
            }
            if (!this.dragged && e.Button == MouseButtons.Left && !this.pickedForEdit && !this.bandAdds && this.Selection.Count > 0)
            {
                this.Selection.Clear();
                this.RaiseSelectionChanged();
            }
        }

        // Selects every drawn element the rubber band touches.
        private void SelectInBand()
        {
            PointF a = this.ToWorld(new Point(this.band.Left, this.band.Bottom)), b = this.ToWorld(new Point(this.band.Right, this.band.Top));
            RectangleF area = RectangleF.FromLTRB(a.X, a.Y, b.X, b.Y);
            if (!this.bandAdds)
            {
                this.Selection.Clear();
            }
            foreach (AnarkDash.Item item in this.dash.Items)
            {
                if (item.Color.A > 0 && area.IntersectsWith(this.ItemBounds(item)) && !this.Selection.Contains(item.Node))
                {
                    this.Selection.Add(item.Node);
                }
            }
            this.RaiseSelectionChanged();
        }

        public void SelectAll()
        {
            if (this.dash == null)
            {
                return;
            }
            this.Selection.Clear();
            foreach (AnarkDash.Item item in this.dash.Items)
            {
                if (!this.Selection.Contains(item.Node))
                {
                    this.Selection.Add(item.Node);
                }
            }
            this.RaiseSelectionChanged();
        }

        // world bounds of a node: its own item and the items under it; null when it draws nothing
        private RectangleF? NodeBounds(AnarkDash.Node node)
        {
            RectangleF? all = null;
            foreach (AnarkDash.Item item in this.dash.Items)
            {
                if (item.Node == node || IsInside(item.Node, node))
                {
                    RectangleF r = this.ItemBounds(item);
                    all = all.HasValue ? RectangleF.Union(all.Value, r) : r;
                }
            }
            return all;
        }

        // Aligns the selection to its outer edges / centre, or spreads it evenly:
        // left, hcenter, right, top, vcenter, bottom, hspread, vspread.
        public void Align(string mode)
        {
            if (this.dash == null)
            {
                return;
            }
            List<AnarkDash.Node> nodes = new List<AnarkDash.Node>();
            List<RectangleF> boxes = new List<RectangleF>();
            foreach (AnarkDash.Node node in this.TopSelection())
            {
                RectangleF? box = this.NodeBounds(node);
                if (box.HasValue)
                {
                    nodes.Add(node);
                    boxes.Add(box.Value);
                }
            }
            bool spread = mode == "hspread" || mode == "vspread";
            if (nodes.Count < (spread ? 3 : 2))
            {
                this.ReportBlocked(spread ? "select at least 3 elements to spread them" : "select at least 2 elements to align them");
                return;
            }
            RectangleF all = boxes[0];
            foreach (RectangleF box in boxes)
            {
                all = RectangleF.Union(all, box);
            }
            float[] dx = new float[nodes.Count], dy = new float[nodes.Count];
            for (int k = 0; k < nodes.Count; k++)
            {
                RectangleF b = boxes[k];
                switch (mode)
                {
                    case "left": dx[k] = all.Left - b.Left; break;
                    case "right": dx[k] = all.Right - b.Right; break;
                    case "hcenter": dx[k] = (all.Left + all.Width / 2) - (b.Left + b.Width / 2); break;
                    case "top": dy[k] = all.Bottom - b.Bottom; break;         // world Y points up: Bottom is the largest Y
                    case "bottom": dy[k] = all.Top - b.Top; break;
                    case "vcenter": dy[k] = (all.Top + all.Height / 2) - (b.Top + b.Height / 2); break;
                }
            }
            if (spread)
            {
                bool horizontal = mode == "hspread";
                List<int> order = new List<int>();
                float sizes = 0;
                for (int k = 0; k < nodes.Count; k++)
                {
                    order.Add(k);
                    sizes += horizontal ? boxes[k].Width : boxes[k].Height;
                }
                order.Sort(delegate(int p, int q)
                {
                    return horizontal ? boxes[p].Left.CompareTo(boxes[q].Left) : boxes[p].Top.CompareTo(boxes[q].Top);
                });
                float gap = ((horizontal ? all.Width : all.Height) - sizes) / (nodes.Count - 1);
                float at = horizontal ? all.Left : all.Top;
                foreach (int k in order)
                {
                    if (horizontal)
                    {
                        dx[k] = at - boxes[k].Left;
                        at += boxes[k].Width + gap;
                    }
                    else
                    {
                        dy[k] = at - boxes[k].Top;
                        at += boxes[k].Height + gap;
                    }
                }
            }
            bool moved = false;
            for (int k = 0; k < nodes.Count; k++)
            {
                if (Math.Abs(dx[k]) > 1e-4f || Math.Abs(dy[k]) > 1e-4f)
                {
                    if (this.dash.MoveBy(nodes[k], dx[k], dy[k]))
                    {
                        moved = true;
                    }
                    else
                    {
                        this.ReportBlocked(nodes[k].Name + " has no editable position");
                    }
                }
            }
            this.Invalidate();
            if (moved && this.Edited != null)
            {
                this.Edited(this, new DashEditEventArgs("align"));
            }
        }

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Left:
                case Keys.Right:
                case Keys.Up:
                case Keys.Down:
                    return true;
            }
            return base.IsInputKey(keyData);
        }

        // Arrows move the selection by 1 unit of the parent (Shift: 10); Ctrl+A selects all, Esc clears.
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (this.dash == null)
            {
                return;
            }
            if (e.Control && e.KeyCode == Keys.A)
            {
                e.Handled = true;
                this.SelectAll();
                return;
            }
            if (e.Control && e.KeyCode == Keys.D)
            {
                e.Handled = true;
                if (this.DuplicateRequested != null)
                {
                    this.DuplicateRequested(this, EventArgs.Empty);
                }
                return;
            }
            if (e.KeyCode == Keys.Delete)
            {
                e.Handled = true;
                if (this.DeleteRequested != null)
                {
                    this.DeleteRequested(this, EventArgs.Empty);
                }
                return;
            }
            if (e.KeyCode == Keys.Escape && this.Selection.Count > 0)
            {
                e.Handled = true;
                this.Selection.Clear();
                this.RaiseSelectionChanged();
                return;
            }
            float step = e.Shift ? 10 : 1, dx = 0, dy = 0;
            switch (e.KeyCode)
            {
                case Keys.Left: dx = -step; break;
                case Keys.Right: dx = step; break;
                case Keys.Up: dy = step; break;
                case Keys.Down: dy = -step; break;
                default: return;
            }
            e.Handled = true;
            bool moved = false;
            string blocked = null;
            foreach (AnarkDash.Node node in this.TopSelection())
            {
                if (this.dash.Nudge(node, dx, dy))
                {
                    moved = true;
                }
                else
                {
                    blocked = node.Name + " has no editable position";
                }
            }
            if (moved)
            {
                this.Invalidate();
                if (this.Edited != null)
                {
                    this.Edited(this, new DashEditEventArgs("position"));
                }
            }
            else
            {
                this.ReportBlocked(blocked);
            }
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            this.Fit();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            PointF before = this.ToWorld(e.Location);
            this.zoom *= (e.Delta > 0) ? 1.2f : 1 / 1.2f;
            this.zoom = Math.Max(0.01f, Math.Min(100, this.zoom));
            PointF after = this.ToWorld(e.Location);
            this.center = new PointF(this.center.X + before.X - after.X, this.center.Y + before.Y - after.Y);
            this.Invalidate();
        }
    }
}
