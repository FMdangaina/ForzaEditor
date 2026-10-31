namespace ForzaStudio
{
    using Microsoft.Xna.Framework;
    using Microsoft.Xna.Framework.Graphics;
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Windows.Forms;

    // The EXTERIOR CAMERAS section of the CAMERA tab: the hood, bumper and chase offsets of the car's
    // CameraOverrides row (CarCamera.Exterior), saved with the cockpit camera by Save to Game Database Now.
    // Only the hood camera can be previewed: its eye is the car-space point (0, height, z) with the
    // game's HoodCam FOV 66. How the game applies the bumper and chase offsets is not decoded (they are 0
    // on every car but one), so those are plain numbers without a preview.
    public partial class ArchiveEditorForm
    {
        private ComboBox cmbExterior;
        private Label[] lblExterior;
        private NumericUpDown[] numExterior;
        private Button btnExteriorView;
        private bool fillingExterior;

        // Per choice: the first CarCamera.ExteriorColumns index, then the labels of its fields.
        private static readonly string[] ExteriorNames = { "Hood", "Bumper high", "Bumper low", "Chase low", "Chase high" };
        private static readonly int[] ExteriorFirst = { 0, 3, 5, 7, 10 };
        private static readonly string[][] ExteriorLabels =
        {
            new string[] { "Height (m)", "Z forward (m)", "Pitch" },
            new string[] { "Height (m)", "Z (m)" },
            new string[] { "Height (m)", "Z (m)" },
            new string[] { "Height (m)", "Radius (m)", "Target Y (m)" },
            new string[] { "Height (m)", "Radius (m)", "Target Y (m)" }
        };

        private void AddExteriorCameraControls(FlowLayoutPanel side)
        {
            side.Controls.Add(SideHeading("Exterior cameras", false));
            side.Controls.Add(SideNote("Hood, bumper and chase offsets of the car (CameraOverrides). 0 = the game's default camera."));
            this.cmbExterior = new ComboBox();
            this.cmbExterior.DropDownStyle = ComboBoxStyle.DropDownList;
            this.cmbExterior.Items.AddRange(ExteriorNames);
            this.cmbExterior.SelectedIndex = 0;
            this.cmbExterior.Width = SideWidth;
            this.cmbExterior.Margin = new Padding(0, 3, 0, 3);
            this.cmbExterior.SelectedIndexChanged += delegate
            {
                this.FillExteriorControls();
                this.UpdateCameraOverlay();
                if (this.LiveCameraView && this.cmbExterior.SelectedIndex <= 2)
                {
                    this.ViewFromExteriorCamera();
                }
            };
            side.Controls.Add(this.cmbExterior);

            this.lblExterior = new Label[3];
            this.numExterior = new NumericUpDown[3];
            for (int i = 0; i < 3; i++)
            {
                FlowLayoutPanel row = new FlowLayoutPanel();
                row.Size = new Size(SideWidth, 28);
                row.Margin = new Padding(0);
                Label label = new Label();
                label.AutoSize = false;
                label.Size = new Size(110, 22);
                label.TextAlign = ContentAlignment.MiddleLeft;
                NumericUpDown number = new NumericUpDown();
                number.Width = 80;
                number.Minimum = -20;
                number.Maximum = 20;
                number.DecimalPlaces = 3;
                number.Increment = 0.01m;
                number.BorderStyle = BorderStyle.FixedSingle;
                number.ValueChanged += delegate { this.ExteriorValueChanged(); };
                row.Controls.Add(label);
                row.Controls.Add(number);
                side.Controls.Add(row);
                this.lblExterior[i] = label;
                this.numExterior[i] = number;
            }
            this.btnExteriorView = SideWideButton("View From This Camera", delegate { this.ViewFromExteriorCamera(); });
            side.Controls.Add(this.btnExteriorView);
            side.Controls.Add(SideNote("Green: the eye of the chosen camera and its view (hood FOV 66, bumper 53). A hood offset of 0, 0 puts the eye on the floor. The bumper preview is an ESTIMATE: the game builds that camera from the car's box in a way not decoded, so the eye is assumed at the front of the model, at 45% (high) or 30% (low) of its height, plus your offsets. Chase has no preview. Saved with Save to Game Database Now."));
            this.FillExteriorControls();
        }

        private void FillExteriorControls()
        {
            int choice = Math.Max(0, this.cmbExterior.SelectedIndex);
            string[] labels = ExteriorLabels[choice];
            this.fillingExterior = true;
            for (int i = 0; i < 3; i++)
            {
                bool used = i < labels.Length;
                this.lblExterior[i].Visible = used;
                this.numExterior[i].Visible = used;
                if (used)
                {
                    this.lblExterior[i].Text = labels[i];
                    double value = (this.camState != null) ? this.camState.Exterior[ExteriorFirst[choice] + i] : 0.0;
                    this.numExterior[i].Value = Math.Max(this.numExterior[i].Minimum, Math.Min(this.numExterior[i].Maximum, (decimal) Math.Round(value, 3)));
                }
            }
            this.fillingExterior = false;
            this.btnExteriorView.Enabled = (this.camState != null) && choice <= 2;
        }

        private void ExteriorValueChanged()
        {
            if (this.fillingExterior || this.camState == null)
            {
                return;
            }
            int choice = Math.Max(0, this.cmbExterior.SelectedIndex);
            for (int i = 0; i < ExteriorLabels[choice].Length; i++)
            {
                this.camState.Exterior[ExteriorFirst[choice] + i] = (double) this.numExterior[i].Value;
            }
            this.camPending = true;
            this.UpdateCameraLabels();
            this.UpdateCameraOverlay();
            if (this.LiveCameraView && this.cmbExterior.SelectedIndex <= 2)
            {
                this.ViewFromExteriorCamera();
            }
        }

        private void EnableExteriorEdit(bool enabled)
        {
            this.cmbExterior.Enabled = enabled;
            foreach (NumericUpDown number in this.numExterior)
            {
                number.Enabled = enabled;
            }
            this.btnExteriorView.Enabled = enabled && this.cmbExterior.SelectedIndex <= 2;
        }

        private Vector3 HoodEye()
        {
            return new Vector3(0f, (float) this.camState.Exterior[CarCamera.HoodHeight], (float) this.camState.Exterior[CarCamera.HoodZ]);
        }

        // Bumper cameras: the game builds them from the car's box in a way not decoded (the offsets are 0
        // on all but one car). The preview ASSUMES the eye on the centre line at the front of the model's
        // shown parts, 5 cm back (CameraSettings.ini BumperHighCam CamZOffset -0.05), at this fraction of
        // the car's height above its lowest point, plus the height / Z offsets of the row. FOV 53.
        private const float BumperHighHeightFraction = 0.45f;
        private const float BumperLowHeightFraction = 0.30f;
        private const float BumperBackOffset = 0.05f;
        private const float BumperFov = 53f;

        // The eye of the chosen exterior camera in model axes, and its FOV. False when there is no
        // preview (chase cameras) or the bumper needs a model and none is shown.
        private bool ExteriorEye(int choice, out Vector3 eye, out float fov)
        {
            eye = Vector3.Zero;
            fov = CarCamera.HoodFov;
            if (this.camState == null)
            {
                return false;
            }
            if (choice == 0)
            {
                eye = CarCamera.ToModel(this.HoodEye());
                return true;
            }
            if (choice == 1 || choice == 2)
            {
                Vector3 min, max;
                if (!this.ModelBounds(out min, out max))
                {
                    return false;
                }
                float height = (float) this.camState.Exterior[ExteriorFirst[choice]];
                float z = (float) this.camState.Exterior[ExteriorFirst[choice] + 1];
                float fraction = (choice == 1) ? BumperHighHeightFraction : BumperLowHeightFraction;
                eye = new Vector3(0f, min.Y + ((max.Y - min.Y) * fraction) + height, min.Z + BumperBackOffset - z);
                fov = BumperFov;
                return true;
            }
            return false;
        }

        private void ViewFromExteriorCamera()
        {
            Vector3 eye;
            float fov;
            if (this.scene == null || !this.ExteriorEye(Math.Max(0, this.cmbExterior.SelectedIndex), out eye, out fov))
            {
                return;
            }
            this.scene.Camera.LookFrom(eye, eye + new Vector3(0f, 0f, -1f));
            this.scene.Camera.FieldOfViewDegrees = fov;
            this.scene.Update(true);
        }

        // The eye of the chosen camera (green cross) and its view, 0.6 m deep, 16:9.
        private void AddHoodOverlay(List<VertexPositionColor> lines)
        {
            Microsoft.Xna.Framework.Graphics.Color green = new Microsoft.Xna.Framework.Graphics.Color(90, 230, 90, 255);
            Vector3 eye;
            float fov;
            if (!this.ExteriorEye(Math.Max(0, this.cmbExterior.SelectedIndex), out eye, out fov) && !this.ExteriorEye(0, out eye, out fov))
            {
                return;
            }
            AddCross(lines, eye, 0.06f, green);
            float depth = 0.6f;
            float halfHeight = depth * (float) Math.Tan(MathHelper.ToRadians(fov) / 2f);
            float halfWidth = halfHeight * 16f / 9f;
            Vector3 centre = eye + new Vector3(0f, 0f, -depth);
            Vector3[] corners =
            {
                centre + new Vector3(-halfWidth, halfHeight, 0f), centre + new Vector3(halfWidth, halfHeight, 0f),
                centre + new Vector3(halfWidth, -halfHeight, 0f), centre + new Vector3(-halfWidth, -halfHeight, 0f)
            };
            for (int i = 0; i < 4; i++)
            {
                lines.Add(new VertexPositionColor(eye, green));
                lines.Add(new VertexPositionColor(corners[i], green));
                lines.Add(new VertexPositionColor(corners[i], green));
                lines.Add(new VertexPositionColor(corners[(i + 1) % 4], green));
            }
        }
    }
}
