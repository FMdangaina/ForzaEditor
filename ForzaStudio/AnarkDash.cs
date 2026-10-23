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
            public RectangleF LocalRect;           // rectangles: the mesh bounds in the node's own space (y up)
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
                item.LocalRect = RectangleF.FromLTRB(minX, minY, maxX, maxY);
                float opacity = node.Opacity;
                Color color = Color.White;
                Node material = MaterialOf(node);
                if (material != null)
                {
                    float[] rgb = this.MaterialDiffuse(material);
                    opacity *= material.Element.GetFloat("opacity", 100) / 100;
                    color = Color.FromArgb(255, Clamp(rgb[0] / 255), Clamp(rgb[1] / 255), Clamp(rgb[2] / 255));
                    Node image = ImageOf(material);
                    if (image != null)
                    {
                        item.Texture = image.Record.Path;
                    }
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

        // The material under a model, and the image under a material.
        public static Node MaterialOf(Node model)
        {
            foreach (Node child in model.Children)
            {
                if (child.Record != null && child.Record.Type == AnarkFbf.RecordType.Material)
                {
                    return child;
                }
            }
            return null;
        }

        public static Node ImageOf(Node material)
        {
            foreach (Node child in material.Children)
            {
                if (child.Record != null && child.Record.Type == AnarkFbf.RecordType.Image)
                {
                    return child;
                }
            }
            return null;
        }

        // .fbf material: float diffuse r, g, b, a (0..1); the graph may override diffuse (0..255). Returns 0..255.
        private float[] MaterialDiffuse(Node material)
        {
            byte[] m = material.Record.Body;
            float[] rgb = new float[3];
            for (int k = 0; k < 3; k++)
            {
                rgb[k] = material.Element.GetFloat(DiffuseNames[k], AnarkFbf.ReadFloat(m, k * 4) * 255);
            }
            return rgb;
        }

        // ---- Editing ----
        // Only values already in the files change: no property, string or record is added, so every count in the
        // .bgf header (strings at 0x3C, properties at 0x2C/0x78, components 0x28/0x74, behaviours 0x30/0x7C) and the
        // order of the graph properties, which the slides and animations may count on, stay as they are.
        // A value is written where the game reads it: the graph property when the element has one, else the base
        // (.bsg node, .fbf material/text/image record). Every .bgf string belongs to one property only (all 431 files),
        // so a text or font is changed in place.

        public const string SourceGraph = "bgf", SourceBase = "bsg", SourceScene = "fbf";

        private static readonly string[] DiffuseNames = new string[] { "diffuse.r", "diffuse.g", "diffuse.b" };
        private static readonly string[] TextColorNames = new string[] { "textcolor.r", "textcolor.g", "textcolor.b" };

        public bool FbfChanged, BgfChanged, BsgChanged;

        public bool Changed { get { return this.FbfChanged || this.BgfChanged || this.BsgChanged; } }

        // Index in AnarkBsg.Node.Values of a transform property, or -1.
        private static int BaseIndex(string name)
        {
            if (name == "opacity")
            {
                return AnarkBsg.Node.Opacity;
            }
            int dot = name.IndexOf('.');
            if (dot < 0 || dot != name.Length - 2)
            {
                return -1;
            }
            int axis = name[dot + 1] - 'x';
            if (axis < 0 || axis > 2)
            {
                return -1;
            }
            switch (name.Substring(0, dot))
            {
                case "position": return AnarkBsg.Node.Position + axis;
                case "rotation": return AnarkBsg.Node.Rotation + axis;
                case "scale": return AnarkBsg.Node.Scale + axis;
                case "pivot": return AnarkBsg.Node.Pivot + axis;
            }
            return -1;
        }

        private static bool IsSpatial(Node node)
        {
            AnarkBgf.ElementType t = node.Element.Type;
            return t == AnarkBgf.ElementType.Node || t == AnarkBgf.ElementType.Text || t == AnarkBgf.ElementType.Component;
        }

        // Where a transform property (position/rotation/scale/pivot .x/.y/.z, opacity) of a node lives: SourceGraph,
        // SourceBase, or null when it cannot be edited without adding a property.
        public string TransformSource(Node node, string name)
        {
            if (!IsSpatial(node) || BaseIndex(name) < 0)
            {
                return null;
            }
            AnarkBgf.Property p = node.Element.Find(name);
            if (p != null)
            {
                return (p.Kind == AnarkBgf.ValueKind.Float) ? SourceGraph : null;
            }
            return (node.Base != null) ? SourceBase : null;
        }

        // Value as the dash shows it: graph units (radians, opacity 0..100).
        public float GetTransform(Node node, string name)
        {
            int k = BaseIndex(name);
            if (k < 0)
            {
                throw new ArgumentException("not a transform property: " + name);
            }
            float[] v = (node.Base != null) ? node.Base.Values : DefaultValues;
            float fallback = (k == AnarkBsg.Node.Opacity) ? v[k] * 100 : v[k];
            return node.Element.GetFloat(name, fallback);
        }

        public bool SetTransform(Node node, string name, float value)
        {
            if (!this.WriteTransform(node, name, value))
            {
                return false;
            }
            this.Update();
            return true;
        }

        // The write alone, for edits that change several values before one Update.
        private bool WriteTransform(Node node, string name, float value)
        {
            string source = this.TransformSource(node, name);
            if (source == SourceGraph)
            {
                node.Element.Find(name).Float = value;
                this.BgfChanged = true;
            }
            else if (source == SourceBase)
            {
                int k = BaseIndex(name);
                node.Base.Values[k] = (k == AnarkBsg.Node.Opacity) ? value / 100 : value;
                this.BsgChanged = true;
            }
            else
            {
                return false;
            }
            return true;
        }

        // ---- Mouse editing (move and resize on the preview) ----

        // A drag in progress. Every Apply works from the values the drag started with, so nothing drifts.
        public class Drag
        {
            internal Node Node;
            internal bool Resize;
            internal float Hx, Hy;                  // handle position in the mesh rectangle: 0, 0.5 or 1
            internal PointF StartParent;            // cursor in the parent's space when the drag began
            internal float PosX, PosY, ScaleX, ScaleY, Rot, PivotX, PivotY;
            internal RectangleF Local;
            internal PointF Fixed;                  // resize: the opposite handle in the parent's space
            public string What { get { return this.Resize ? "size" : "position"; } }
        }

        // Why the last BeginMove / BeginResize returned null.
        public string Blocked;

        // Cursor (world) to the space of the node's parent, where position and pivot live.
        private static PointF ToParent(Node node, PointF world)
        {
            Node p = node.Parent;
            if (p == null)
            {
                return world;
            }
            float det = p.A * p.D - p.B * p.C;
            if (Math.Abs(det) < 1e-9f)
            {
                return PointF.Empty;
            }
            float dx = world.X - p.X, dy = world.Y - p.Y;
            return new PointF((p.D * dx - p.C * dy) / det, (-p.B * dx + p.A * dy) / det);
        }

        public Drag BeginMove(Node node, PointF world)
        {
            this.Blocked = null;
            if (!IsSpatial(node))
            {
                return null;
            }
            if (this.TransformSource(node, "position.x") == null || this.TransformSource(node, "position.y") == null)
            {
                this.Blocked = node.Name + " has no editable position";
                return null;
            }
            Drag d = new Drag();
            d.Node = node;
            d.StartParent = ToParent(node, world);
            d.PosX = this.GetTransform(node, "position.x");
            d.PosY = this.GetTransform(node, "position.y");
            return d;
        }

        // Resize by a handle of the mesh rectangle: scale x / y and position change so that the opposite handle stays.
        public Drag BeginResize(Node node, float hx, float hy)
        {
            this.Blocked = null;
            Item item = this.ItemOf(node);
            if (item == null || item.Kind != ItemKind.Rectangle)
            {
                return null;
            }
            string[] needed = new string[] { "position.x", "position.y", "scale.x", "scale.y" };
            foreach (string name in needed)
            {
                if (this.TransformSource(node, name) == null)
                {
                    this.Blocked = node.Name + " has no editable " + name;
                    return null;
                }
            }
            if (Math.Abs(this.GetTransform(node, "rotation.x")) > 1e-4f || Math.Abs(this.GetTransform(node, "rotation.y")) > 1e-4f)
            {
                this.Blocked = node.Name + " is turned around X or Y, set its scale in the property list";
                return null;
            }
            Drag d = new Drag();
            d.Node = node;
            d.Resize = true;
            d.Hx = hx;
            d.Hy = hy;
            d.Local = item.LocalRect;
            d.PosX = this.GetTransform(node, "position.x");
            d.PosY = this.GetTransform(node, "position.y");
            d.ScaleX = this.GetTransform(node, "scale.x");
            d.ScaleY = this.GetTransform(node, "scale.y");
            d.Rot = this.GetTransform(node, "rotation.z");
            d.PivotX = this.GetTransform(node, "pivot.x");
            d.PivotY = this.GetTransform(node, "pivot.y");
            PointF fixedLocal = new PointF(d.Local.Left + (1 - hx) * d.Local.Width, d.Local.Top + (1 - hy) * d.Local.Height);
            d.Fixed = ParentPoint(d, d.ScaleX, d.ScaleY, fixedLocal);
            return d;
        }

        // A point of the node's own space in the parent's space: position + R * S * (q - pivot).
        private static PointF ParentPoint(Drag d, float sx, float sy, PointF q)
        {
            double cos = Math.Cos(d.Rot), sin = Math.Sin(d.Rot);
            float ux = (q.X - d.PivotX) * sx, uy = (q.Y - d.PivotY) * sy;
            return new PointF((float) (d.PosX + cos * ux + sin * uy), (float) (d.PosY - sin * ux + cos * uy));
        }

        // Moves a node by (dx, dy) in its parent's space.
        public bool Nudge(Node node, float dx, float dy)
        {
            if (!IsSpatial(node) || this.TransformSource(node, "position.x") == null || this.TransformSource(node, "position.y") == null)
            {
                return false;
            }
            this.WriteTransform(node, "position.x", this.GetTransform(node, "position.x") + dx);
            this.WriteTransform(node, "position.y", this.GetTransform(node, "position.y") + dy);
            this.Update();
            return true;
        }

        public Item ItemOf(Node node)
        {
            foreach (Item item in this.Items)
            {
                if (item.Node == node)
                {
                    return item;
                }
            }
            return null;
        }

        // Follows the cursor (world). keepRatio: corner handles keep the shape.
        public void ApplyDrag(Drag d, PointF world, bool keepRatio)
        {
            PointF t = ToParent(d.Node, world);
            if (!d.Resize)
            {
                this.WriteTransform(d.Node, "position.x", d.PosX + t.X - d.StartParent.X);
                this.WriteTransform(d.Node, "position.y", d.PosY + t.Y - d.StartParent.Y);
                this.Update();
                return;
            }
            double cos = Math.Cos(d.Rot), sin = Math.Sin(d.Rot);
            float dx = t.X - d.Fixed.X, dy = t.Y - d.Fixed.Y;
            // back into the node's axes (undo the rotation)
            float vx = (float) (cos * dx - sin * dy), vy = (float) (sin * dx + cos * dy);
            float sx = d.ScaleX, sy = d.ScaleY;
            float hw = (d.Hx - (1 - d.Hx)) * d.Local.Width, hh = (d.Hy - (1 - d.Hy)) * d.Local.Height;
            if (d.Hx != 0.5f && Math.Abs(hw) > 1e-6f)
            {
                sx = SafeScale(vx / hw);
            }
            if (d.Hy != 0.5f && Math.Abs(hh) > 1e-6f)
            {
                sy = SafeScale(vy / hh);
            }
            if (keepRatio && d.Hx != 0.5f && d.Hy != 0.5f)
            {
                float fx = sx / d.ScaleX, fy = sy / d.ScaleY;
                float f = (Math.Abs(fx - 1) > Math.Abs(fy - 1)) ? fx : fy;
                sx = SafeScale(d.ScaleX * f);
                sy = SafeScale(d.ScaleY * f);
            }
            PointF fixedLocal = new PointF(d.Local.Left + (1 - d.Hx) * d.Local.Width, d.Local.Top + (1 - d.Hy) * d.Local.Height);
            // position that keeps the fixed handle where it was: F - R * S' * (q - pivot)
            float ux = (fixedLocal.X - d.PivotX) * sx, uy = (fixedLocal.Y - d.PivotY) * sy;
            this.WriteTransform(d.Node, "scale.x", sx);
            this.WriteTransform(d.Node, "scale.y", sy);
            this.WriteTransform(d.Node, "position.x", (float) (d.Fixed.X - (cos * ux + sin * uy)));
            this.WriteTransform(d.Node, "position.y", (float) (d.Fixed.Y - (-sin * ux + cos * uy)));
            this.Update();
        }

        private static float SafeScale(float s)
        {
            if (float.IsNaN(s) || float.IsInfinity(s))
            {
                return 1;
            }
            return (Math.Abs(s) < 0.001f) ? (s < 0 ? -0.001f : 0.001f) : s;
        }

        // ---- Any other graph property ----

        public bool SetFloat(Node node, AnarkBgf.Property p, float value)
        {
            if (p.Kind != AnarkBgf.ValueKind.Float || float.IsNaN(value) || float.IsInfinity(value))
            {
                return false;
            }
            p.Float = value;
            this.BgfChanged = true;
            this.Update();
            return true;
        }

        // Int, bool (0 / 1) and enum-like properties: the raw value.
        public bool SetRaw(Node node, AnarkBgf.Property p, uint value)
        {
            if (p.Kind == AnarkBgf.ValueKind.Float || p.Kind == AnarkBgf.ValueKind.String)
            {
                return false;
            }
            p.Value = value;
            this.BgfChanged = true;
            this.Update();
            return true;
        }

        // A string property in place (one string belongs to one property only).
        public bool SetStringProperty(Node node, AnarkBgf.Property p, string value)
        {
            if (p.Kind != AnarkBgf.ValueKind.String || value == null || !IsLatin1(value) || p.Value >= this.Bgf.Strings.Count)
            {
                return false;
            }
            this.Bgf.Strings[(int) p.Value] = value;
            this.BgfChanged = true;
            this.Update();
            return true;
        }

        // Colour of a text (graph textcolor) or of a model (its material: graph diffuse, else the .fbf material).
        public string ColorSource(Node node)
        {
            if (node.Record == null)
            {
                return null;
            }
            if (node.Record.Type == AnarkFbf.RecordType.Text)
            {
                foreach (string name in TextColorNames)
                {
                    AnarkBgf.Property p = node.Element.Find(name);
                    if (p == null || p.Kind != AnarkBgf.ValueKind.Float)
                    {
                        return null;
                    }
                }
                return SourceGraph;
            }
            if (node.Record.Type == AnarkFbf.RecordType.Model)
            {
                Node material = MaterialOf(node);
                if (material == null)
                {
                    return null;
                }
                bool graph = false, scene = false;
                foreach (string name in DiffuseNames)
                {
                    AnarkBgf.Property p = material.Element.Find(name);
                    if (p != null && p.Kind != AnarkBgf.ValueKind.Float)
                    {
                        return null;
                    }
                    graph |= p != null;
                    scene |= p == null;
                }
                return (graph && scene) ? SourceGraph + "+" + SourceScene : graph ? SourceGraph : SourceScene;
            }
            return null;
        }

        public Color GetColor(Node node)
        {
            if (node.Record != null && node.Record.Type == AnarkFbf.RecordType.Text)
            {
                AnarkBgf.Element e = node.Element;
                return Color.FromArgb(Clamp(e.GetFloat("textcolor.r", 255) / 255), Clamp(e.GetFloat("textcolor.g", 255) / 255), Clamp(e.GetFloat("textcolor.b", 255) / 255));
            }
            Node material = (node.Record != null && node.Record.Type == AnarkFbf.RecordType.Model) ? MaterialOf(node) : null;
            if (material == null)
            {
                return Color.White;
            }
            float[] rgb = this.MaterialDiffuse(material);
            return Color.FromArgb(Clamp(rgb[0] / 255), Clamp(rgb[1] / 255), Clamp(rgb[2] / 255));
        }

        public bool SetColor(Node node, Color color)
        {
            if (this.ColorSource(node) == null)
            {
                return false;
            }
            float[] rgb = new float[] { color.R, color.G, color.B };
            if (node.Record.Type == AnarkFbf.RecordType.Text)
            {
                for (int k = 0; k < 3; k++)
                {
                    node.Element.Find(TextColorNames[k]).Float = rgb[k];
                }
                this.BgfChanged = true;
            }
            else
            {
                Node material = MaterialOf(node);
                for (int k = 0; k < 3; k++)
                {
                    AnarkBgf.Property p = material.Element.Find(DiffuseNames[k]);
                    if (p != null)
                    {
                        p.Float = rgb[k];
                        this.BgfChanged = true;
                    }
                    else
                    {
                        AnarkFbf.WriteFloat(material.Record.Body, k * 4, rgb[k] / 255);
                        this.FbfChanged = true;
                    }
                }
            }
            this.Update();
            return true;
        }

        // Text and font of a text node: the graph strings (every text element has them).
        public bool SetText(Node node, string text)
        {
            return this.SetString(node, "textstring", text);
        }

        public bool SetFont(Node node, string font)
        {
            return this.SetString(node, "font", font);
        }

        private bool SetString(Node node, string name, string value)
        {
            if (node.Record == null || node.Record.Type != AnarkFbf.RecordType.Text || value == null || !IsLatin1(value))
            {
                return false;
            }
            AnarkBgf.Property p = node.Element.Find(name);
            if (p == null || p.Kind != AnarkBgf.ValueKind.String || p.Value >= this.Bgf.Strings.Count)
            {
                return false;
            }
            this.Bgf.Strings[(int) p.Value] = value;
            this.BgfChanged = true;
            this.Update();
            return true;
        }

        private static bool IsLatin1(string s)
        {
            foreach (char c in s)
            {
                if (c > 0xFF)
                {
                    return false;
                }
            }
            return true;
        }

        // Texture of a model: the GAME:\ path in the .fbf image record under its material.
        public Node TextureNode(Node node)
        {
            if (node.Record == null)
            {
                return null;
            }
            if (node.Record.Type == AnarkFbf.RecordType.Image)
            {
                return node;
            }
            if (node.Record.Type == AnarkFbf.RecordType.Model)
            {
                Node material = MaterialOf(node);
                return (material != null) ? ImageOf(material) : null;
            }
            if (node.Record.Type == AnarkFbf.RecordType.Material)
            {
                return ImageOf(node);
            }
            return null;
        }

        public bool SetTexture(Node node, string gamePath)
        {
            Node image = this.TextureNode(node);
            if (image == null || string.IsNullOrEmpty(gamePath) || !IsLatin1(gamePath))
            {
                return false;
            }
            image.Record.Path = gamePath;
            this.FbfChanged = true;
            this.Update();
            return true;
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
