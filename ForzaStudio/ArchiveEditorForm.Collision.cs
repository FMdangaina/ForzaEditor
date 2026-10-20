namespace ForzaStudio
{
    using Microsoft.Xna.Framework;
    using Microsoft.Xna.Framework.Graphics;
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Windows.Forms;
    using System.Xml;

    // The collision of the car whose model is open: physics/maxdata.xml next to the model, a bounding
    // box and spheres (Flags 2 = body, 1 = contact points of r 0.05; FH placeholders have 4 of r 0.375,
    // Flags 3). Drawn over the model while the COLLISION tab is open, spheres editable one by one
    // (position, radius, type), added, deleted and mirrored to the other side. Axes as in the file:
    // Z forward (the model's Z points backward), measured from the wheelbase centre
    // (Misc/BottomCenterWheelbasePos), so the view draws them moved by it, where the game puts them
    // (314 FM4 cars, colsurvey 2026-10-20). Edits go into the archive at once, pending until Save.
    public partial class ArchiveEditorForm
    {
        private Label lblCollInfo;
        private ListBox lstCollSpheres;
        private NumericUpDown[] numCollPos;
        private NumericUpDown numCollRadius;
        private ComboBox cmbCollFlags;
        private CheckBox chkCollShow;
        private CheckBox chkCollBox;
        private Button btnCollAdd;
        private Button btnCollDelete;
        private Button btnCollMirror;

        private ForzaZip.Entry collEntry;
        private XmlDocument collDocument;
        private string collLead;              // what came before <MAXData> in the file (BOM, line break)
        private bool fillingColl;

        private static readonly string[] CollFlagNames = { "1 - contact point", "2 - body", "3 - (FH placeholder)" };

        private void AddCollisionControls(FlowLayoutPanel side)
        {
            this.lblCollInfo = SideNote("Open a car model.");
            this.lblCollInfo.ForeColor = this.ForeColor;

            this.lstCollSpheres = new ListBox();
            this.lstCollSpheres.Size = new Size(SideWidth, 200);
            this.lstCollSpheres.IntegralHeight = false;
            this.lstCollSpheres.SelectedIndexChanged += delegate
            {
                this.FillCollisionControls();
                this.UpdateViewOverlay();
            };

            FlowLayoutPanel position = this.NumberRow(out this.numCollPos, -20, 20, 3, 0.01m, 0);
            foreach (NumericUpDown number in this.numCollPos)
            {
                // NumberRow wires the part edit preview; the collision has its own.
                number.ValueChanged -= new EventHandler(this.PartEditValueChanged);
                number.ValueChanged += delegate { this.CollisionValueChanged(); };
            }
            this.numCollRadius = new NumericUpDown();
            this.numCollRadius.Width = 72;
            this.numCollRadius.Minimum = 0.001m;
            this.numCollRadius.Maximum = 5m;
            this.numCollRadius.DecimalPlaces = 3;
            this.numCollRadius.Increment = 0.01m;
            this.numCollRadius.BorderStyle = BorderStyle.FixedSingle;
            this.numCollRadius.ValueChanged += delegate { this.CollisionValueChanged(); };

            this.cmbCollFlags = new ComboBox();
            this.cmbCollFlags.DropDownStyle = ComboBoxStyle.DropDownList;
            this.cmbCollFlags.Width = SideWidth;
            this.cmbCollFlags.Items.AddRange(CollFlagNames);
            this.cmbCollFlags.SelectedIndexChanged += delegate { this.CollisionValueChanged(); };

            this.chkCollShow = new CheckBox();
            this.chkCollShow.Text = "Show spheres in the view";
            this.chkCollShow.Checked = true;
            this.chkCollShow.Size = new Size(SideWidth, 22);
            this.chkCollShow.CheckedChanged += delegate { this.UpdateViewOverlay(); };
            this.chkCollBox = new CheckBox();
            this.chkCollBox.Text = "Show bounding box";
            this.chkCollBox.Checked = true;
            this.chkCollBox.Size = new Size(SideWidth, 22);
            this.chkCollBox.CheckedChanged += delegate { this.UpdateViewOverlay(); };

            this.btnCollAdd = new Button();
            this.btnCollAdd.Text = "Add";
            this.btnCollAdd.Click += delegate { this.AddCollisionSphere(); };
            this.btnCollDelete = new Button();
            this.btnCollDelete.Text = "Delete";
            this.btnCollDelete.Click += delegate { this.DeleteCollisionSphere(); };
            this.btnCollMirror = new Button();
            this.btnCollMirror.Text = "Mirror";
            this.btnCollMirror.Click += delegate { this.MirrorCollisionSphere(); };

            side.Controls.Add(SideHeading("Collision", true));
            side.Controls.Add(this.lblCollInfo);
            side.Controls.Add(this.lstCollSpheres);
            side.Controls.Add(SideCaption("Position (m, from wheelbase centre)"));
            side.Controls.Add(position);
            side.Controls.Add(SideCaption("Radius (m)"));
            side.Controls.Add(this.numCollRadius);
            side.Controls.Add(SideCaption("Type (Flags)"));
            side.Controls.Add(this.cmbCollFlags);
            side.Controls.Add(SideButtonRow(this.btnCollAdd, this.btnCollDelete, this.btnCollMirror));
            side.Controls.Add(this.chkCollShow);
            side.Controls.Add(this.chkCollBox);
            side.Controls.Add(SideNote("Blue: body spheres (2). Orange: contact points (1). Magenta: other types. Yellow: the selected one. Grey: the bounding box. Z points forward. Add copies the selected sphere, Mirror copies it to the other side (X negated). Edits are pending until File > Save."));
            this.EnableCollisionEdit(false);
        }

        private void EnableCollisionEdit(bool enabled)
        {
            bool sphere = enabled && this.SelectedCollisionSphere != null;
            foreach (NumericUpDown number in this.numCollPos)
            {
                number.Enabled = sphere;
            }
            this.numCollRadius.Enabled = sphere;
            this.cmbCollFlags.Enabled = sphere;
            this.btnCollAdd.Enabled = sphere;
            this.btnCollDelete.Enabled = sphere;
            this.btnCollMirror.Enabled = sphere;
            this.lstCollSpheres.Enabled = enabled;
        }

        // Called with every model shown: the car's physics/maxdata.xml from the same folder.
        private void LoadCollision()
        {
            this.collEntry = null;
            this.collDocument = null;
            this.lblCollInfo.Text = "";
            this.lstCollSpheres.Items.Clear();
            string name = (this.currentEntry != null) ? this.currentEntry.Name.ToLowerInvariant() : "";
            bool car = name.EndsWith(".carbin") && !name.Contains("_caliper") && !name.Contains("_rotor");
            ForzaZip.Entry entry = car ? this.zip.Find(this.ModelFolder + "physics/maxdata.xml") : null;
            this.SetCollisionTabVisible(entry != null);
            if (entry != null)
            {
                try
                {
                    string text = Encoding.UTF8.GetString(this.zip.Read(entry));
                    int start = text.IndexOf('<');
                    this.collLead = (start > 0) ? text.Substring(0, start) : "";
                    XmlDocument document = new XmlDocument();
                    document.LoadXml(text.Substring(Math.Max(start, 0)).TrimEnd());
                    if (this.CollisionSpheresOf(document) == null)
                    {
                        throw new InvalidDataException("no MAXData/Collision/CollSpheres");
                    }
                    this.collEntry = entry;
                    this.collDocument = document;
                }
                catch (Exception exception)
                {
                    this.lblCollInfo.Text = "physics/maxdata.xml not read: " + exception.Message;
                }
            }
            this.FillCollisionList(0);
            this.UpdateViewOverlay();
        }

        private XmlElement CollisionSpheresOf(XmlDocument document)
        {
            return (XmlElement) document.SelectSingleNode("/MAXData/Collision/CollSpheres");
        }

        // Misc/BottomCenterWheelbasePos: where the collision's 0,0,0 is, file axes.
        private Vector3 CollisionOrigin
        {
            get
            {
                XmlElement origin = (this.collDocument == null) ? null : (XmlElement) this.collDocument.SelectSingleNode("/MAXData/Misc/BottomCenterWheelbasePos");
                return (origin == null) ? Vector3.Zero : new Vector3((float) CollNumber(origin, "x"), (float) CollNumber(origin, "y"), (float) CollNumber(origin, "z"));
            }
        }

        private XmlElement CollisionBox
        {
            get { return (this.collDocument == null) ? null : (XmlElement) this.collDocument.SelectSingleNode("/MAXData/Collision/BoundingBox"); }
        }

        private List<XmlElement> CollisionSpheres
        {
            get
            {
                List<XmlElement> spheres = new List<XmlElement>();
                if (this.collDocument != null)
                {
                    foreach (XmlNode node in this.CollisionSpheresOf(this.collDocument).ChildNodes)
                    {
                        if (node is XmlElement)
                        {
                            spheres.Add((XmlElement) node);
                        }
                    }
                }
                return spheres;
            }
        }

        private XmlElement SelectedCollisionSphere
        {
            get
            {
                int index = this.lstCollSpheres.SelectedIndex;
                List<XmlElement> spheres = this.CollisionSpheres;
                return (index >= 0 && index < spheres.Count) ? spheres[index] : null;
            }
        }

        private void FillCollisionList(int select)
        {
            this.fillingColl = true;
            this.lstCollSpheres.BeginUpdate();
            this.lstCollSpheres.Items.Clear();
            List<XmlElement> spheres = this.CollisionSpheres;
            int body = 0, contact = 0;
            foreach (XmlElement sphere in spheres)
            {
                this.lstCollSpheres.Items.Add(CollisionSphereText(sphere));
                string flags = sphere.GetAttribute("Flags");
                body += (flags == "2") ? 1 : 0;
                contact += (flags == "1") ? 1 : 0;
            }
            this.lstCollSpheres.EndUpdate();
            if (spheres.Count > 0)
            {
                this.lstCollSpheres.SelectedIndex = Math.Max(0, Math.Min(select, spheres.Count - 1));
            }
            this.fillingColl = false;
            if (this.collDocument != null)
            {
                XmlElement box = this.CollisionBox;
                this.lblCollInfo.Text = string.Format(CultureInfo.InvariantCulture, "{0}\n{1} spheres: {2} body, {3} contact, {4} other{5}{6}",
                    this.collEntry.Name, spheres.Count, body, contact, spheres.Count - body - contact,
                    (box == null) ? "" : string.Format(CultureInfo.InvariantCulture, "\nBox x {0:0.###}..{1:0.###}, y {2:0.###}..{3:0.###}, z {4:0.###}..{5:0.###}",
                        CollNumber(box, "MinX"), CollNumber(box, "MaxX"), CollNumber(box, "MinY"), CollNumber(box, "MaxY"), CollNumber(box, "MinZ"), CollNumber(box, "MaxZ")),
                    this.collEntry.IsModified ? "\nedited, not saved" : "");
                this.lblCollInfo.ForeColor = this.collEntry.IsModified ? ForzaTheme.Red : this.ForeColor;
            }
            else if (this.lblCollInfo.Text.Length == 0)
            {
                this.lblCollInfo.Text = "No physics/maxdata.xml next to this model.";
                this.lblCollInfo.ForeColor = this.ForeColor;
            }
            this.FillCollisionControls();
        }

        private static string CollisionSphereText(XmlElement sphere)
        {
            string flags = sphere.GetAttribute("Flags");
            string kind = (flags == "2") ? "body" : (flags == "1") ? "contact" : "flags " + flags;
            return string.Format(CultureInfo.InvariantCulture, "{0}  r {1:0.000}  {2}  ({3:0.00}, {4:0.00}, {5:0.00})",
                sphere.Name, CollNumber(sphere, "Radius"), kind, CollNumber(sphere, "PosX"), CollNumber(sphere, "PosY"), CollNumber(sphere, "PosZ"));
        }

        private void FillCollisionControls()
        {
            if (this.fillingColl)
            {
                return;
            }
            XmlElement sphere = this.SelectedCollisionSphere;
            this.EnableCollisionEdit(this.collDocument != null);
            if (sphere == null)
            {
                return;
            }
            this.fillingColl = true;
            SetVector(this.numCollPos, new Vector3((float) CollNumber(sphere, "PosX"), (float) CollNumber(sphere, "PosY"), (float) CollNumber(sphere, "PosZ")));
            this.numCollRadius.Value = Math.Max(this.numCollRadius.Minimum, Math.Min(this.numCollRadius.Maximum, (decimal) Math.Round(CollNumber(sphere, "Radius"), 3)));
            int flags;
            int.TryParse(sphere.GetAttribute("Flags"), NumberStyles.Integer, CultureInfo.InvariantCulture, out flags);
            this.cmbCollFlags.SelectedIndex = (flags >= 1 && flags <= 3) ? flags - 1 : -1;
            this.fillingColl = false;
        }

        // Only the attributes the user changed are written again; the others keep the file's text.
        private void CollisionValueChanged()
        {
            XmlElement sphere = this.SelectedCollisionSphere;
            if (this.fillingColl || sphere == null)
            {
                return;
            }
            SetCollNumber(sphere, "PosX", this.numCollPos[0].Value);
            SetCollNumber(sphere, "PosY", this.numCollPos[1].Value);
            SetCollNumber(sphere, "PosZ", this.numCollPos[2].Value);
            SetCollNumber(sphere, "Radius", this.numCollRadius.Value);
            if (this.cmbCollFlags.SelectedIndex >= 0)
            {
                string flags = (this.cmbCollFlags.SelectedIndex + 1).ToString(CultureInfo.InvariantCulture);
                if (sphere.GetAttribute("Flags") != flags)
                {
                    sphere.SetAttribute("Flags", flags);
                }
            }
            int index = this.lstCollSpheres.SelectedIndex;
            this.fillingColl = true;
            this.lstCollSpheres.Items[index] = CollisionSphereText(sphere);
            if (this.lstCollSpheres.SelectedIndex != index)
            {
                this.lstCollSpheres.SelectedIndex = index;
            }
            this.fillingColl = false;
            this.StoreCollision(null, index);
        }

        private void AddCollisionSphere()
        {
            XmlElement sphere = this.SelectedCollisionSphere;
            if (sphere == null)
            {
                return;
            }
            XmlElement copy = (XmlElement) sphere.CloneNode(true);
            sphere.ParentNode.InsertAfter(copy, sphere);
            this.StoreCollision(sphere.Name + " copied (move it with the numbers)", this.lstCollSpheres.SelectedIndex + 1);
        }

        private void DeleteCollisionSphere()
        {
            XmlElement sphere = this.SelectedCollisionSphere;
            if (sphere == null)
            {
                return;
            }
            int index = this.lstCollSpheres.SelectedIndex;
            sphere.ParentNode.RemoveChild(sphere);
            this.StoreCollision(sphere.Name + " deleted", index);
        }

        private void MirrorCollisionSphere()
        {
            XmlElement sphere = this.SelectedCollisionSphere;
            if (sphere == null)
            {
                return;
            }
            double x = CollNumber(sphere, "PosX");
            if (Math.Abs(x) < 0.001)
            {
                this.SetStatus(sphere.Name + " is on the centre line (X 0): nothing to mirror.");
                return;
            }
            XmlElement copy = (XmlElement) sphere.CloneNode(true);
            SetCollNumber(copy, "PosX", (decimal) -x);
            sphere.ParentNode.AppendChild(copy);
            this.StoreCollision(sphere.Name + " mirrored to X " + (-x).ToString("0.###", CultureInfo.InvariantCulture), this.CollisionSpheres.Count - 1);
        }

        // Spheres keep the file's naming, Sphere0, Sphere1... in order, after an add or a delete.
        private void RenumberCollisionSpheres()
        {
            List<XmlElement> spheres = this.CollisionSpheres;
            for (int i = 0; i < spheres.Count; i++)
            {
                string name = "Sphere" + i.ToString(CultureInfo.InvariantCulture);
                if (spheres[i].Name == name)
                {
                    continue;
                }
                XmlElement renamed = this.collDocument.CreateElement(name);
                foreach (XmlAttribute attribute in spheres[i].Attributes)
                {
                    renamed.SetAttribute(attribute.Name, attribute.Value);
                }
                spheres[i].ParentNode.ReplaceChild(renamed, spheres[i]);
            }
        }

        // Writes the file into the archive (pending until Save) in FM4's layout: two-space indent,
        // " />" closing, a line break at the end.
        private void StoreCollision(string status, int select)
        {
            if (status != null)
            {
                this.RenumberCollisionSpheres();
            }
            StringBuilder text = new StringBuilder();
            using (XmlWriter writer = XmlWriter.Create(text, new XmlWriterSettings { Indent = true, IndentChars = "  ", OmitXmlDeclaration = true }))
            {
                this.collDocument.DocumentElement.WriteTo(writer);
            }
            this.zip.Replace(this.collEntry, Encoding.UTF8.GetBytes(this.collLead + text.ToString() + "\r\n"));
            this.RefreshEntryNode(this.collEntry);
            if (status != null)
            {
                this.FillCollisionList(select);
                this.SetStatus(status + " (pending: Save to write the archive)");
            }
            else
            {
                this.lblCollInfo.ForeColor = ForzaTheme.Red;
                if (!this.lblCollInfo.Text.EndsWith("edited, not saved"))
                {
                    this.lblCollInfo.Text += "\nedited, not saved";
                }
            }
            this.UpdateViewOverlay();
        }

        private static double CollNumber(XmlElement element, string name)
        {
            double value;
            return double.TryParse(element.GetAttribute(name), NumberStyles.Float, CultureInfo.InvariantCulture, out value) ? value : 0;
        }

        private static void SetCollNumber(XmlElement element, string name, decimal value)
        {
            // An unchanged value keeps its text (the file's own precision).
            if ((decimal) Math.Round(CollNumber(element, name), 3) != value)
            {
                element.SetAttribute(name, value.ToString("0.######", CultureInfo.InvariantCulture));
            }
        }

        // Spheres as wire globes (3 meridians, equator, 2 parallels) and the box, model axes (Z flipped).
        private List<VertexPositionColor> CollisionOverlayLines()
        {
            if (this.collDocument == null)
            {
                return null;
            }
            List<VertexPositionColor> lines = new List<VertexPositionColor>();
            Vector3 origin = this.CollisionOrigin;
            XmlElement box = this.CollisionBox;
            if (this.chkCollBox.Checked && box != null)
            {
                Microsoft.Xna.Framework.Graphics.Color grey = new Microsoft.Xna.Framework.Graphics.Color(190, 190, 190, 255);
                Vector3 min = CarCamera.ToModel(origin + new Vector3((float) CollNumber(box, "MinX"), (float) CollNumber(box, "MinY"), (float) CollNumber(box, "MinZ")));
                Vector3 max = CarCamera.ToModel(origin + new Vector3((float) CollNumber(box, "MaxX"), (float) CollNumber(box, "MaxY"), (float) CollNumber(box, "MaxZ")));
                AddBox(lines, min, max, grey);
            }
            if (this.chkCollShow.Checked)
            {
                XmlElement selected = this.SelectedCollisionSphere;
                foreach (XmlElement sphere in this.CollisionSpheres)
                {
                    if (sphere != selected)
                    {
                        AddGlobe(lines, origin, sphere, CollisionColor(sphere.GetAttribute("Flags")));
                    }
                }
                if (selected != null)
                {
                    // Last, so it is drawn over the others.
                    AddGlobe(lines, origin, selected, new Microsoft.Xna.Framework.Graphics.Color(255, 230, 40, 255));
                }
            }
            return lines;
        }

        private static Microsoft.Xna.Framework.Graphics.Color CollisionColor(string flags)
        {
            return (flags == "2") ? new Microsoft.Xna.Framework.Graphics.Color(70, 120, 255, 255)
                : (flags == "1") ? new Microsoft.Xna.Framework.Graphics.Color(255, 150, 40, 255)
                : new Microsoft.Xna.Framework.Graphics.Color(230, 70, 230, 255);
        }

        private static void AddGlobe(List<VertexPositionColor> lines, Vector3 origin, XmlElement sphere, Microsoft.Xna.Framework.Graphics.Color color)
        {
            Vector3 centre = CarCamera.ToModel(origin + new Vector3((float) CollNumber(sphere, "PosX"), (float) CollNumber(sphere, "PosY"), (float) CollNumber(sphere, "PosZ")));
            float r = (float) CollNumber(sphere, "Radius");
            const int steps = 24;
            // Parallels: equator and +-50 degrees.
            foreach (float latitude in new float[] { 0f, 0.873f, -0.873f })
            {
                float y = r * (float) Math.Sin(latitude), ring = r * (float) Math.Cos(latitude);
                for (int i = 0; i < steps; i++)
                {
                    float a = MathHelper.TwoPi * i / steps, b = MathHelper.TwoPi * (i + 1) / steps;
                    lines.Add(new VertexPositionColor(centre + new Vector3(ring * (float) Math.Cos(a), y, ring * (float) Math.Sin(a)), color));
                    lines.Add(new VertexPositionColor(centre + new Vector3(ring * (float) Math.Cos(b), y, ring * (float) Math.Sin(b)), color));
                }
            }
            // Meridians every 60 degrees.
            for (int m = 0; m < 3; m++)
            {
                float longitude = MathHelper.Pi * m / 3;
                Vector3 across = new Vector3((float) Math.Cos(longitude), 0f, (float) Math.Sin(longitude));
                for (int i = 0; i < steps; i++)
                {
                    float a = MathHelper.TwoPi * i / steps, b = MathHelper.TwoPi * (i + 1) / steps;
                    lines.Add(new VertexPositionColor(centre + (across * r * (float) Math.Cos(a)) + new Vector3(0f, r * (float) Math.Sin(a), 0f), color));
                    lines.Add(new VertexPositionColor(centre + (across * r * (float) Math.Cos(b)) + new Vector3(0f, r * (float) Math.Sin(b), 0f), color));
                }
            }
        }

        private static void AddBox(List<VertexPositionColor> lines, Vector3 a, Vector3 b, Microsoft.Xna.Framework.Graphics.Color color)
        {
            Vector3[] c =
            {
                new Vector3(a.X, a.Y, a.Z), new Vector3(b.X, a.Y, a.Z), new Vector3(b.X, a.Y, b.Z), new Vector3(a.X, a.Y, b.Z),
                new Vector3(a.X, b.Y, a.Z), new Vector3(b.X, b.Y, a.Z), new Vector3(b.X, b.Y, b.Z), new Vector3(a.X, b.Y, b.Z)
            };
            int[] edges = { 0, 1, 1, 2, 2, 3, 3, 0, 4, 5, 5, 6, 6, 7, 7, 4, 0, 4, 1, 5, 2, 6, 3, 7 };
            foreach (int e in edges)
            {
                lines.Add(new VertexPositionColor(c[e], color));
            }
        }
    }
}
