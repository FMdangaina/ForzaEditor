namespace ForzaStudio
{
    using System;
    using System.Drawing;
    using System.Drawing.Drawing2D;

    // The small icons of the viewport's tool bar, drawn here (no image files): the six views as a wire cube
    // with the face seen filled, the projection, and the mesh / UV tools.
    public static class ToolIcons
    {
        private const int Size = 22;
        private static readonly Color Line = Color.FromArgb(205, 205, 210);

        private static Bitmap Canvas(out Graphics g)
        {
            Bitmap bitmap = new Bitmap(Size, Size);
            g = Graphics.FromImage(bitmap);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            return bitmap;
        }

        private static readonly float[][] Corners =
        {
            new float[] { 0, 0, 0 }, new float[] { 1, 0, 0 }, new float[] { 1, 1, 0 }, new float[] { 0, 1, 0 },
            new float[] { 0, 0, 1 }, new float[] { 1, 0, 1 }, new float[] { 1, 1, 1 }, new float[] { 0, 1, 1 }
        };

        // Faces as corner numbers (model axes: the front is z = 0, the right x = 1, the top y = 1).
        private static readonly int[][] Faces =
        {
            new int[] { 0, 1, 2, 3 },   // front
            new int[] { 4, 5, 6, 7 },   // back
            new int[] { 1, 5, 6, 2 },   // right
            new int[] { 0, 4, 7, 3 },   // left
            new int[] { 3, 2, 6, 7 },   // top
            new int[] { 0, 1, 5, 4 }    // bottom
        };

        private static PointF Project(float[] c)
        {
            const float s = 8f;
            return new PointF(11f + ((c[0] - c[2]) * 0.866f * s), 14f + (((c[0] + c[2]) * 0.5f) - c[1]) * s);
        }

        // face: 0 front, 1 back, 2 right, 3 left, 4 top, 5 bottom.
        public static Bitmap View(int face)
        {
            Graphics g;
            Bitmap bitmap = Canvas(out g);
            using (g)
            {
                PointF[] polygon = new PointF[4];
                for (int i = 0; i < 4; i++)
                {
                    polygon[i] = Project(Corners[Faces[face][i]]);
                }
                using (Brush fill = new SolidBrush(Color.FromArgb(200, 226, 38, 30)))
                {
                    g.FillPolygon(fill, polygon);
                }
                using (Pen pen = new Pen(Line, 1f))
                {
                    for (int a = 0; a < 8; a++)
                    {
                        for (int b = a + 1; b < 8; b++)
                        {
                            int differs = 0;
                            for (int k = 0; k < 3; k++)
                            {
                                differs += (Corners[a][k] != Corners[b][k]) ? 1 : 0;
                            }
                            if (differs == 1)
                            {
                                g.DrawLine(pen, Project(Corners[a]), Project(Corners[b]));
                            }
                        }
                    }
                }
            }
            return bitmap;
        }

        public static Bitmap Orthographic()
        {
            Graphics g;
            Bitmap bitmap = Canvas(out g);
            using (g)
            using (Pen pen = new Pen(Line, 1f))
            {
                g.DrawRectangle(pen, 3, 3, 15, 15);
                for (int i = 1; i < 3; i++)
                {
                    g.DrawLine(pen, 3 + (i * 5), 3, 3 + (i * 5), 18);
                    g.DrawLine(pen, 3, 3 + (i * 5), 18, 3 + (i * 5));
                }
                using (Pen red = new Pen(Color.FromArgb(226, 38, 30), 2f))
                {
                    g.DrawRectangle(red, 3, 3, 15, 15);
                }
            }
            return bitmap;
        }

        // all: the box is filled; otherwise it is empty.
        public static Bitmap Select(bool all)
        {
            Graphics g;
            Bitmap bitmap = Canvas(out g);
            using (g)
            using (Pen pen = new Pen(Line, 1f))
            {
                pen.DashStyle = DashStyle.Dash;
                if (all)
                {
                    using (Brush fill = new SolidBrush(Color.FromArgb(150, 255, 150, 30)))
                    {
                        g.FillRectangle(fill, 3, 3, 16, 16);
                    }
                }
                g.DrawRectangle(pen, 3, 3, 16, 16);
            }
            return bitmap;
        }

        private static void Triangle(Graphics g, Pen pen, Brush fill, float x, float y, float size)
        {
            PointF[] t = { new PointF(x, y + size), new PointF(x + size, y + size), new PointF(x + (size / 2f), y) };
            if (fill != null)
            {
                g.FillPolygon(fill, t);
            }
            g.DrawPolygon(pen, t);
        }

        public static Bitmap Linked()
        {
            Graphics g;
            Bitmap bitmap = Canvas(out g);
            using (g)
            using (Pen pen = new Pen(Line, 1f))
            using (Brush fill = new SolidBrush(Color.FromArgb(170, 255, 150, 30)))
            {
                Triangle(g, pen, fill, 2f, 9f, 10f);
                Triangle(g, pen, fill, 9f, 9f, 10f);
                Triangle(g, pen, fill, 5.5f, 2f, 10f);
            }
            return bitmap;
        }

        public static Bitmap Delete()
        {
            Graphics g;
            Bitmap bitmap = Canvas(out g);
            using (g)
            using (Pen pen = new Pen(Line, 1f))
            using (Pen red = new Pen(Color.FromArgb(226, 38, 30), 2.2f))
            {
                Triangle(g, pen, null, 3f, 5f, 15f);
                g.DrawLine(red, 5, 5, 17, 17);
                g.DrawLine(red, 17, 5, 5, 17);
            }
            return bitmap;
        }

        public static Bitmap Detach()
        {
            Graphics g;
            Bitmap bitmap = Canvas(out g);
            using (g)
            using (Pen pen = new Pen(Line, 1f))
            using (Brush fill = new SolidBrush(Color.FromArgb(170, 255, 150, 30)))
            {
                Triangle(g, pen, null, 2f, 8f, 11f);
                Triangle(g, pen, fill, 9f, 1f, 11f);
                using (Pen arrow = new Pen(Color.FromArgb(226, 38, 30), 1.6f))
                {
                    arrow.EndCap = LineCap.ArrowAnchor;
                    g.DrawLine(arrow, 8, 17, 18, 9);
                }
            }
            return bitmap;
        }

        private static readonly Color Accent = Color.FromArgb(226, 38, 30);

        private delegate void Painter(Graphics g, Pen pen, Pen red);

        private static Bitmap Draw(Painter paint)
        {
            Graphics g;
            Bitmap bitmap = Canvas(out g);
            using (g)
            using (Pen pen = new Pen(Line, 1.3f))
            using (Pen red = new Pen(Accent, 1.6f))
            {
                paint(g, pen, red);
            }
            return bitmap;
        }

        public static Bitmap UvSelect()
        {
            return Draw(delegate(Graphics g, Pen pen, Pen red)
            {
                g.FillPolygon(Brushes.White, new PointF[] { new PointF(5, 3), new PointF(5, 17), new PointF(9, 13), new PointF(12, 19), new PointF(14, 18), new PointF(11, 12), new PointF(16, 12) });
                g.DrawPolygon(red, new PointF[] { new PointF(5, 3), new PointF(5, 17), new PointF(9, 13), new PointF(12, 19), new PointF(14, 18), new PointF(11, 12), new PointF(16, 12) });
            });
        }

        public static Bitmap UvMove()
        {
            return Draw(delegate(Graphics g, Pen pen, Pen red)
            {
                red.EndCap = LineCap.ArrowAnchor;
                red.StartCap = LineCap.ArrowAnchor;
                g.DrawLine(red, 11, 3, 11, 19);
                g.DrawLine(red, 3, 11, 19, 11);
            });
        }

        public static Bitmap UvRotate()
        {
            return Draw(delegate(Graphics g, Pen pen, Pen red)
            {
                g.DrawArc(pen, 4, 4, 14, 14, 200, 280);
                red.EndCap = LineCap.ArrowAnchor;
                g.DrawArc(red, 4, 4, 14, 14, 120, 80);
            });
        }

        public static Bitmap UvScale()
        {
            return Draw(delegate(Graphics g, Pen pen, Pen red)
            {
                g.DrawRectangle(pen, 4, 8, 9, 9);
                red.EndCap = LineCap.ArrowAnchor;
                g.DrawLine(red, 10, 12, 19, 3);
            });
        }

        public static Bitmap UvFlip(bool horizontal)
        {
            return Draw(delegate(Graphics g, Pen pen, Pen red)
            {
                if (horizontal)
                {
                    g.DrawLine(red, 11, 2, 11, 20);
                    g.DrawPolygon(pen, new PointF[] { new PointF(9, 5), new PointF(9, 17), new PointF(2, 17) });
                    g.DrawPolygon(pen, new PointF[] { new PointF(13, 5), new PointF(13, 17), new PointF(20, 17) });
                }
                else
                {
                    g.DrawLine(red, 2, 11, 20, 11);
                    g.DrawPolygon(pen, new PointF[] { new PointF(5, 9), new PointF(17, 9), new PointF(17, 2) });
                    g.DrawPolygon(pen, new PointF[] { new PointF(5, 13), new PointF(17, 13), new PointF(17, 20) });
                }
            });
        }

        public static Bitmap UvRotate90()
        {
            return Draw(delegate(Graphics g, Pen pen, Pen red)
            {
                g.DrawRectangle(pen, 3, 10, 9, 9);
                red.EndCap = LineCap.ArrowAnchor;
                g.DrawArc(red, 6, 2, 13, 13, 180, 90);
                g.DrawString("90", new Font("Segoe UI", 6f, FontStyle.Bold), Brushes.White, 4f, 11.5f);
            });
        }

        public static Bitmap UvFit()
        {
            return Draw(delegate(Graphics g, Pen pen, Pen red)
            {
                g.DrawRectangle(red, 2, 2, 17, 17);
                g.FillRectangle(Brushes.Orange, 6, 7, 9, 7);
            });
        }

        public static Bitmap UvUndo()
        {
            return Draw(delegate(Graphics g, Pen pen, Pen red)
            {
                red.EndCap = LineCap.ArrowAnchor;
                g.DrawArc(red, 4, 6, 14, 12, 40, -250);
            });
        }

        public static Bitmap UvReset()
        {
            return Draw(delegate(Graphics g, Pen pen, Pen red)
            {
                g.DrawArc(pen, 4, 4, 14, 14, 30, 300);
                g.DrawLine(red, 12, 3, 12, 11);
                g.DrawLine(red, 12, 11, 17, 14);
            });
        }

        public static Bitmap UvView()
        {
            return Draw(delegate(Graphics g, Pen pen, Pen red)
            {
                g.DrawRectangle(pen, 6, 6, 10, 10);
                g.DrawLine(red, 2, 6, 2, 2);
                g.DrawLine(red, 2, 2, 6, 2);
                g.DrawLine(red, 20, 6, 20, 2);
                g.DrawLine(red, 20, 2, 16, 2);
                g.DrawLine(red, 2, 16, 2, 20);
                g.DrawLine(red, 2, 20, 6, 20);
                g.DrawLine(red, 20, 16, 20, 20);
                g.DrawLine(red, 20, 20, 16, 20);
            });
        }

        public static Bitmap Uv()
        {
            Graphics g;
            Bitmap bitmap = Canvas(out g);
            using (g)
            using (Pen pen = new Pen(Color.FromArgb(226, 38, 30), 1.5f))
            using (Font font = new Font("Segoe UI", 7.5f, FontStyle.Bold))
            using (Brush text = new SolidBrush(Line))
            {
                g.DrawRectangle(pen, 2, 3, 17, 15);
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                g.DrawString("UV", font, text, 2f, 4.5f);
            }
            return bitmap;
        }
    }
}
