namespace ForzaStudio
{
    using Microsoft.Xna.Framework;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Windows.Forms;
    using System.Xml;

    // "Generate Basic Collision": a starting collision for a car whose own is missing or wrong (FH1's
    // Agera), built from the model that is shown and edited by hand afterwards (the orthographic
    // views, numpad 1-6, are for that). Same layout as an FM4 car (BMW_M5_95: 32 spheres): two
    // body spheres on the centre line (nose, tail), per side a row of body spheres resting on the
    // ground that tapers toward the ends, two on the roof, and contact points (Flags 1, r 0.05).
    // The box follows the rule fitted on 313 FM4 cars and tuned in game (CarPort.ReplacePlaceholderCollision):
    // half width = body half width - 0.018, top = body top - 0.010, front = mesh front - 0.077,
    // rear = mesh rear + 0.095, floor 0.006, bevels 0.611 / 0.744 of the width and 0.819 / 0.805 of the
    // length. Axes of the file: Z forward, measured from the wheelbase centre.
    public partial class ArchiveEditorForm
    {
        private Button btnCollGenerate;

        private const int CollisionSidePairs = 7;      // lower body spheres per side

        private void GenerateBasicCollision()
        {
            if (this.collDocument == null || this.currentEntry == null)
            {
                return;
            }
            string name = this.currentEntry.Name.ToLowerInvariant();
            if (name.Contains("_cockpit") || name.Contains("_caliper") || name.Contains("_rotor"))
            {
                MessageBox.Show(this, "Open the car's main model (the .carbin without _cockpit) with its body shown: the collision is built from it.", "Generate Basic Collision", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            Vector3 min, max, bodyMin, bodyMax;
            if (!this.ModelBounds(out min, out max))
            {
                MessageBox.Show(this, "Check the parts of the car in the model tree (LOD0 or LOD1, all sections): the collision is built from what is shown.", "Generate Basic Collision", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!this.BodyBounds(out bodyMin, out bodyMax))
            {
                bodyMin = min;
                bodyMax = max;
            }
            XmlElement box = this.CollisionBox;
            XmlElement spheres = this.CollisionSpheresOf(this.collDocument);
            if (box == null || spheres == null)
            {
                MessageBox.Show(this, "This maxdata.xml has no BoundingBox or CollSpheres to fill.", "Generate Basic Collision", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            int existing = this.CollisionSpheres.Count;
            string question = string.Format(CultureInfo.InvariantCulture,
                "Replace the bounding box and the {0} spheres with a basic collision built from the shown model?\n\nIt is a starting point: check it in the views (Top and Left, numpad 6 and 2) and edit the spheres. Pending until File > Save.", existing);
            if (MessageBox.Show(this, question, "Generate Basic Collision", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
            {
                return;
            }

            Vector3 origin = this.CollisionOrigin;
            // File axes: model X as is, model Z backward -> Z forward, minus the wheelbase centre.
            double front = -min.Z - origin.Z;
            double rear = -max.Z - origin.Z;
            double half = Math.Max(Math.Abs(bodyMin.X), Math.Abs(bodyMax.X)) - 0.018;
            double top = bodyMax.Y - 0.010 - origin.Y;
            double floor = 0.006 - origin.Y;
            double boxFront = front - 0.077, boxRear = rear + 0.095;
            double width = 2 * half, length = boxFront - boxRear, height = top - floor;

            SetCollisionAttribute(box, "MinX", -half);
            SetCollisionAttribute(box, "MaxX", half);
            SetCollisionAttribute(box, "MinY", floor);
            SetCollisionAttribute(box, "MaxY", top);
            SetCollisionAttribute(box, "MinZ", boxRear);
            SetCollisionAttribute(box, "MaxZ", boxFront);
            SetCollisionAttribute(box, "BevelFrontX", width * 0.611);
            SetCollisionAttribute(box, "BevelFrontZ", length * 0.819);
            SetCollisionAttribute(box, "BevelRearX", width * 0.744);
            SetCollisionAttribute(box, "BevelRearZ", length * 0.805);

            while (spheres.HasChildNodes)
            {
                spheres.RemoveChild(spheres.FirstChild);
            }
            double ends = 0.042, tail = 0.047;
            double bodyFront = front - ends, bodyRear = rear + tail;      // outermost surfaces of the body spheres
            double rBase = Math.Max(0.2, Math.Min(0.55, height * 0.4));
            double x = half * 0.55;

            // Nose and tail on the centre line.
            double rNose = rBase * 0.75, rTail = rBase * 0.85;
            this.AddCollisionSphere(spheres, 0, rNose, rNose * 0.97, bodyFront - rNose, 2);
            this.AddCollisionSphere(spheres, 0, rTail, rTail * 1.1, bodyRear + rTail, 2);

            // Rows of body spheres, big in the middle, resting on the ground (y = r).
            List<double[]> row = new List<double[]>();      // z, r
            for (int i = 0; i < CollisionSidePairs; i++)
            {
                double t = (CollisionSidePairs == 1) ? 0 : (2.0 * i / (CollisionSidePairs - 1)) - 1;     // -1 rear .. 1 front
                double r = rBase * (1.0 - (0.45 * t * t));
                double z = (bodyRear + rTail * 0.6) + (((bodyFront - rNose * 0.6) - (bodyRear + rTail * 0.6)) * (t + 1) / 2);
                row.Add(new double[] { z, r });
            }
            // Roof: two spheres per side, above the cabin (a little behind the middle).
            double roofR = Math.Max(0.15, Math.Min(0.28, height * 0.22));
            double roofY = top - roofR;
            double midZ = (boxFront + boxRear) / 2;
            double[] roofZ = { midZ + (length * 0.04), midZ - (length * 0.24) };
            // Contact points: sills at the wheelbase ends, bottom of the nose and tail, roof corners.
            XmlElement misc = (XmlElement) this.collDocument.SelectSingleNode("/MAXData/Misc");
            double wheelbase = (misc != null && misc.HasAttribute("Wheelbase")) ? CollNumber(misc, "Wheelbase") : length * 0.55;
            double[][] contacts =
            {
                new double[] { half - 0.08, floor + 0.03, -wheelbase / 2 },
                new double[] { half - 0.08, floor + 0.03, wheelbase / 2 },
                new double[] { half - 0.06, floor + 0.12, boxRear + 0.05 },
                new double[] { half - 0.06, floor + 0.12, boxFront - 0.05 },
                new double[] { half * 0.75, top - 0.03, roofZ[0] },
                new double[] { half * 0.75, top - 0.03, roofZ[1] }
            };
            foreach (int side in new int[] { 1, -1 })
            {
                foreach (double[] s in row)
                {
                    this.AddCollisionSphere(spheres, side * x * (0.7 + (0.3 * (s[1] / rBase))), s[1] * 1.0, s[1] * 1.0, s[0], 2);
                }
                foreach (double z in roofZ)
                {
                    this.AddCollisionSphere(spheres, side * half * 0.5, roofR, roofY, z, 2);
                }
                foreach (double[] c in contacts)
                {
                    this.AddCollisionSphere(spheres, side * c[0], 0.05, c[1], c[2], 1);
                }
            }
            this.StoreCollision(string.Format(CultureInfo.InvariantCulture, "Basic collision generated: box x +-{0:0.###}, y {1:0.###}..{2:0.###}, z {3:0.###}..{4:0.###}, {5} spheres",
                half, floor, top, boxRear, boxFront, this.CollisionSpheres.Count), 0);
        }

        private void AddCollisionSphere(XmlElement parent, double x, double radius, double y, double z, int flags)
        {
            XmlElement sphere = this.collDocument.CreateElement("Sphere" + parent.ChildNodes.Count.ToString(CultureInfo.InvariantCulture));
            sphere.SetAttribute("Radius", Format(radius));
            sphere.SetAttribute("PosX", Format(x));
            sphere.SetAttribute("PosY", Format(y));
            sphere.SetAttribute("PosZ", Format(z));
            sphere.SetAttribute("Flags", flags.ToString(CultureInfo.InvariantCulture));
            parent.AppendChild(sphere);
        }

        private static string Format(double value)
        {
            return Math.Round(value, 6).ToString("0.######", CultureInfo.InvariantCulture);
        }

        private static void SetCollisionAttribute(XmlElement element, string name, double value)
        {
            element.SetAttribute(name, Format(value));
        }

        // The "body" section's bounds among the shown parts (the width and roof of the car without
        // mirrors, wings and bumpers that stick out).
        private bool BodyBounds(out Vector3 min, out Vector3 max)
        {
            min = new Vector3(float.MaxValue);
            max = new Vector3(float.MinValue);
            bool any = false;
            foreach (ForzaMesh mesh in GetMeshes(this.tvModel.Nodes, true))
            {
                if (mesh.Hidden || mesh.Vertices == null || !mesh.Name.StartsWith("body_", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                foreach (ForzaVertex vertex in mesh.Vertices)
                {
                    min = Vector3.Min(min, vertex.position);
                    max = Vector3.Max(max, vertex.position);
                    any = true;
                }
            }
            return any;
        }
    }
}
