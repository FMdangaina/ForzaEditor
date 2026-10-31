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
            this.cmbExterior.SelectedIndexChanged += delegate { this.FillExteriorControls(); };
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
            this.btnExteriorView = SideWideButton("View From Hood Camera", delegate { this.ViewFromHoodCamera(); });
            side.Controls.Add(this.btnExteriorView);
            side.Controls.Add(SideNote("Green: hood eye and its view (FOV 66). A hood offset of 0, 0 puts the eye on the floor. Bumper and chase have no preview: the game's use of them is not decoded. Saved with Save to Game Database Now."));
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
            this.btnExteriorView.Enabled = (this.camState != null) && choice == 0;
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
        }

        private void EnableExteriorEdit(bool enabled)
        {
            this.cmbExterior.Enabled = enabled;
            foreach (NumericUpDown number in this.numExterior)
            {
                number.Enabled = enabled;
            }
            this.btnExteriorView.Enabled = enabled && this.cmbExterior.SelectedIndex <= 0;
        }

        private Vector3 HoodEye()
        {
            return new Vector3(0f, (float) this.camState.Exterior[CarCamera.HoodHeight], (float) this.camState.Exterior[CarCamera.HoodZ]);
        }

        private void ViewFromHoodCamera()
        {
            if (this.scene == null || this.camState == null)
            {
                return;
            }
            Vector3 eye = CarCamera.ToModel(this.HoodEye());
            this.scene.Camera.LookFrom(eye, eye + new Vector3(0f, 0f, -1f));
            this.scene.Camera.FieldOfViewDegrees = CarCamera.HoodFov;
            this.scene.Update(true);
        }

        // The hood eye (green cross) and its view, 0.6 m deep, 16:9.
        private void AddHoodOverlay(List<VertexPositionColor> lines)
        {
            Microsoft.Xna.Framework.Graphics.Color green = new Microsoft.Xna.Framework.Graphics.Color(90, 230, 90, 255);
            Vector3 eye = CarCamera.ToModel(this.HoodEye());
            AddCross(lines, eye, 0.06f, green);
            float depth = 0.6f;
            float halfHeight = depth * (float) Math.Tan(MathHelper.ToRadians(CarCamera.HoodFov) / 2f);
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
