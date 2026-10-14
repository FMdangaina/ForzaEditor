using System;
using System.Collections.Generic;
using System.Drawing;

namespace ForzaStudio
{
    // A digital dash: the .fbf scene and the .bgf graph of one Dash_<car>, joined by element id, with the
    // initial state of every node (the values in the graph; slides and animations are not applied yet).
    // Coordinates: orthographic camera on -Z, X right, Y up, rotations in radians (left-handed: a positive
    // rotation.z turns clockwise on screen). Node transform as in UI Composer: position + R * S * (p - pivot).
    public class AnarkDash
    {
        public class Node
        {
            public int Index;                      // in Bgf.Elements
            public AnarkBgf.Element Element;
            public AnarkFbf.Record Record;         // null for behaviours
            public AnarkBsg.Node Base;             // base transform (.bsg), null for materials, images, behaviours
            public Node Parent;
            public List<Node> Children = new List<Node>();

            // world transform: screen = (A*x + C*y + X, B*x + D*y + Y), depth Z
            public float A = 1, B, C, D = 1, X, Y, Z;
            public float Opacity = 1;               // 0..1, including the parents

            public string Name
            {
                get
                {
                    if (this.Element.Type == AnarkBgf.ElementType.Behaviour)
                    {
                        return "Behaviour";
                    }
                    return (this.Record != null) ? this.Record.Name : "#" + this.Element.Id;
                }
            }

            public PointF Transform(float x, float y)
            {
                return new PointF(this.A * x + this.C * y + this.X, this.B * x + this.D * y + this.Y);
            }
        }

        public enum ItemKind
        {
            Rectangle,
            Text,
        }

        // Something drawn: a model's rectangle (with its material and texture) or a text.
        public class Item
        {
            public ItemKind Kind;
            public Node Node;
            public PointF[] Corners;               // world: bottom-left, bottom-right, top-right, top-left
            public Color Color = Color.White;      // material diffuse or text colour, with the opacity in alpha
            public string Texture;                 // GAME:\ path of the image, or null
            public string Text;
            public string Font;
            public float Size;
            public int HorzAlign;                  // 0 left, 1 center, 2 right
            public int VertAlign;                  // 0 top, 1 middle, 2 bottom
        }

        private static readonly float[] DefaultValues = new float[] { 0, 0, 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, -50, -50, 0, 50, 50, 0, 1 };

        public AnarkFbf Fbf;
        public AnarkBgf Bgf;
        public AnarkBsg Bsg;                       // may be null
        public List<Node> Nodes = new List<Node>();
        public Node Root;
        public List<Item> Items = new List<Item>();   // in drawing order (far to near)

        public AnarkDash(AnarkFbf fbf, AnarkBgf bgf, AnarkBsg bsg)
        {
            this.Fbf = fbf;
            this.Bgf = bgf;
            this.Bsg = bsg;
            for (int i = 0; i < bgf.Elements.Count; i++)
            {
                Node node = new Node();
                node.Index = i;
                node.Element = bgf.Elements[i];
                if (node.Element.Type != AnarkBgf.ElementType.Behaviour)
                {
                    node.Record = fbf.Find(node.Element.Id);
                    node.Base = (bsg != null) ? bsg.Find(node.Element.Id) : null;
                }
                this.Nodes.Add(node);
            }
            foreach (Node node in this.Nodes)
            {
                int parent = node.Element.Parent;
                if (parent >= 0 && parent < this.Nodes.Count && parent != node.Index)
                {
                    node.Parent = this.Nodes[parent];
                    node.Parent.Children.Add(node);
                }
                else if (this.Root == null)
                {
                    this.Root = node;
                }
            }
            this.Update();
        }

        // Recomputes the transforms and the drawn items (after a property changed).
        public void Update()
        {
            foreach (Node node in this.Nodes)
            {
                if (node.Parent == null)
                {
                    this.Place(node, null);
                }
            }
            this.Items.Clear();
            foreach (Node node in this.Nodes)
            {
                Item item = this.MakeItem(node);
                if (item != null)
                {
                    this.Items.Add(item);
                }
            }
            // far to near; the graph order breaks ties (the sort is made stable by the index)
            this.Items.Sort(delegate(Item p, Item q)
            {
                int c = q.Node.Z.CompareTo(p.Node.Z);
                return (c != 0) ? c : p.Node.Index.CompareTo(q.Node.Index);
            });
        }

        private void Place(Node node, Node parent)
        {
            AnarkBgf.Element e = node.Element;
            bool spatial = e.Type == AnarkBgf.ElementType.Node || e.Type == AnarkBgf.ElementType.Text || e.Type == AnarkBgf.ElementType.Component;
            float a = 1, b = 0, c = 0, d = 1, x = 0, y = 0, z = 0, opacity = 1;
            if (spatial)
            {
                float[] v = (node.Base != null) ? node.Base.Values : DefaultValues;
                float sx = e.GetFloat("scale.x", v[AnarkBsg.Node.Scale]), sy = e.GetFloat("scale.y", v[AnarkBsg.Node.Scale + 1]);
                float rx = e.GetFloat("rotation.x", v[AnarkBsg.Node.Rotation]), ry = e.GetFloat("rotation.y", v[AnarkBsg.Node.Rotation + 1]);
                float rz = e.GetFloat("rotation.z", v[AnarkBsg.Node.Rotation + 2]);
                float px = e.GetFloat("pivot.x", v[AnarkBsg.Node.Pivot]), py = e.GetFloat("pivot.y", v[AnarkBsg.Node.Pivot + 1]);
                // flat objects seen along Z: a rotation about X or Y only shortens (or mirrors) the other axis
                sx *= (float) Math.Cos(ry);
                sy *= (float) Math.Cos(rx);
                float cos = (float) Math.Cos(rz), sin = (float) Math.Sin(rz);
                // clockwise rotation in a Y-up frame: x' = cos*x + sin*y, y' = -sin*x + cos*y
                a = cos * sx;
                b = -sin * sx;
                c = sin * sy;
                d = cos * sy;
                x = e.GetFloat("position.x", v[AnarkBsg.Node.Position]) - (a * px + c * py);
                y = e.GetFloat("position.y", v[AnarkBsg.Node.Position + 1]) - (b * px + d * py);
                z = e.GetFloat("position.z", v[AnarkBsg.Node.Position + 2]);
                opacity = e.GetFloat("opacity", v[AnarkBsg.Node.Opacity] * 100) / 100f;
            }
            if (parent != null)
            {
                node.A = parent.A * a + parent.C * b;
                node.B = parent.B * a + parent.D * b;
                node.C = parent.A * c + parent.C * d;
                node.D = parent.B * c + parent.D * d;
                node.X = parent.A * x + parent.C * y + parent.X;
                node.Y = parent.B * x + parent.D * y + parent.Y;
                node.Z = parent.Z + z;
                node.Opacity = parent.Opacity * opacity;
            }
            else
            {
                node.A = a;
                node.B = b;
                node.C = c;
                node.D = d;
                node.X = x;
                node.Y = y;
                node.Z = z;
                node.Opacity = opacity;
            }
            foreach (Node child in node.Children)
            {
                this.Place(child, node);
            }
        }

        private Item MakeItem(Node node)
        {
            if (node.Record == null)
            {
                return null;
            }
            AnarkBgf.Element e = node.Element;
            if (node.Record.Type == AnarkFbf.RecordType.Model)
            {
                int meshIndex = (int) AnarkFbf.ReadU32(node.Record.Body, 0);
                float minX = -50, minY = -50, maxX = 50, maxY = 50;
                if (meshIndex >= 0 && meshIndex < this.Fbf.Meshes.Count)
                {
                    byte[] desc = this.Fbf.Meshes[meshIndex].VertexDesc;
                    minX = AnarkFbf.ReadFloat(desc, 16);
                    minY = AnarkFbf.ReadFloat(desc, 20);
                    maxX = AnarkFbf.ReadFloat(desc, 32);
                    maxY = AnarkFbf.ReadFloat(desc, 36);
                }
                Item item = new Item();
                item.Kind = ItemKind.Rectangle;
                item.Node = node;
                item.Corners = new PointF[] { node.Transform(minX, minY), node.Transform(maxX, minY), node.Transform(maxX, maxY), node.Transform(minX, maxY) };
                float opacity = node.Opacity;
                Color color = Color.White;
                foreach (Node child in node.Children)
                {
                    if (child.Record == null || child.Record.Type != AnarkFbf.RecordType.Material)
                    {
                        continue;
                    }
                    // .fbf material: float diffuse r, g, b, a (0..1); the graph may override diffuse (0..255) and opacity
                    byte[] m = child.Record.Body;
                    float r = AnarkFbf.ReadFloat(m, 0), g = AnarkFbf.ReadFloat(m, 4), bl = AnarkFbf.ReadFloat(m, 8);
                    AnarkBgf.Element me = child.Element;
                    r = me.GetFloat("diffuse.r", r * 255) / 255;
                    g = me.GetFloat("diffuse.g", g * 255) / 255;
                    bl = me.GetFloat("diffuse.b", bl * 255) / 255;
                    opacity *= me.GetFloat("opacity", 100) / 100;
                    color = Color.FromArgb(255, Clamp(r), Clamp(g), Clamp(bl));
                    foreach (Node image in child.Children)
                    {
                        if (image.Record != null && image.Record.Type == AnarkFbf.RecordType.Image)
                        {
                            item.Texture = image.Record.Path;
                            break;
                        }
                    }
                    break;
                }
                item.Color = Color.FromArgb(Clamp(opacity), color);
                return item;
            }
            if (node.Record.Type == AnarkFbf.RecordType.Text)
            {
                Item item = new Item();
                item.Kind = ItemKind.Text;
                item.Node = node;
                item.Text = this.Bgf.GetString(e.Find("textstring"));
                if (string.IsNullOrEmpty(item.Text))
                {
                    item.Text = string.IsNullOrEmpty(node.Record.Text) ? node.Record.Name : node.Record.Text;
                }
                item.Font = this.Bgf.GetString(e.Find("font")) ?? node.Record.Font;
                item.Size = e.GetFloat("size", 24);
                item.HorzAlign = e.GetInt("horzalign", 0);
                item.VertAlign = e.GetInt("vertalign", 1);
                float r = e.GetFloat("textcolor.r", 255), g = e.GetFloat("textcolor.g", 255), bl = e.GetFloat("textcolor.b", 255);
                item.Color = Color.FromArgb(Clamp(node.Opacity), Clamp(r / 255), Clamp(g / 255), Clamp(bl / 255));
                item.Corners = new PointF[] { node.Transform(0, 0) };
                return item;
            }
            return null;
        }

        private static int Clamp(float unit)
        {
            int v = (int) Math.Round(unit * 255);
            return (v < 0) ? 0 : (v > 255) ? 255 : v;
        }

        // The image file of a GAME:\MEDIA\CARS\<car>\DIGITALGAUGE\<name>.TGA path inside a car zip: digitalgauge/<name>.xds
        public static string TextureEntry(string gamePath, out string car)
        {
            car = null;
            string[] parts = gamePath.Replace('/', '\\').Split('\\');
            int cars = Array.FindIndex(parts, delegate(string p) { return string.Equals(p, "CARS", StringComparison.OrdinalIgnoreCase); });
            if (cars < 0 || cars + 2 >= parts.Length)
            {
                return null;
            }
            car = parts[cars + 1];
            string rest = string.Join("/", parts, cars + 2, parts.Length - cars - 2);
            int dot = rest.LastIndexOf('.');
            return ((dot > 0) ? rest.Substring(0, dot) : rest) + ".xds";
        }
    }
}
