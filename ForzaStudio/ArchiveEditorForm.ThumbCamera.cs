namespace ForzaStudio
{
    using Microsoft.Xna.Framework;
    using Microsoft.Xna.Framework.Graphics;
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Globalization;
    using System.IO;
    using System.Windows.Forms;

    // The THUMBNAIL CAMERA section of the CAMERA tab: preview of the camera the game takes the car's
    // thumbnail with (GAME:\Media\ThumbnailCamSettings.xml, see ThumbnailCamera) and a per-car override
    // for the long cars: move the camera further away along its line of sight, or edit it by hand.
    public partial class ArchiveEditorForm
    {
        private ComboBox cmbThumbView;
        private NumericUpDown[] numThumbPos;
        private NumericUpDown[] numThumbFace;
        private NumericUpDown numThumbDistance;
        private NumericUpDown numThumbFov;
        private CheckBox chkThumbShow;
        private Label lblThumbState;
        private Button btnThumbView;
        private Button btnThumbSave;
        private Button btnThumbDefault;
        private ThumbnailCamera thumbCurrent;
        private Vector3 thumbAim;
        private bool thumbOwn;
        private bool thumbPending;
        private bool fillingThumb;

        private void AddThumbnailCameraControls(FlowLayoutPanel side)
        {
            side.Controls.Add(SideHeading("Thumbnail camera", false));
            this.lblThumbState = SideNote("Open a car model.");
            side.Controls.Add(this.lblThumbState);

            this.cmbThumbView = new ComboBox();
            this.cmbThumbView.DropDownStyle = ComboBoxStyle.DropDownList;
            this.cmbThumbView.Items.Add("3/4 front (608 x 288)");
            this.cmbThumbView.Items.Add("Side strip (288 x 160)");
            this.cmbThumbView.SelectedIndex = 0;
            this.cmbThumbView.Width = SideWidth;
            this.cmbThumbView.Margin = new Padding(0, 3, 0, 3);
            this.cmbThumbView.SelectedIndexChanged += delegate { this.LoadThumbnailCamera(); };
            side.Controls.Add(this.cmbThumbView);

            side.Controls.Add(SideCaption("Distance from the car (m)"));
            FlowLayoutPanel row = new FlowLayoutPanel();
            row.Size = new Size(SideWidth, 28);
            row.Margin = new Padding(0);
            this.numThumbDistance = ThumbNumber(0.1m, 60m, 2, 0.25m);
            this.numThumbFov = ThumbNumber(0.5m, 90m, 2, 0.5m);
            Label fovLabel = new Label();
            fovLabel.Text = "FOV";
            fovLabel.AutoSize = false;
            fovLabel.Size = new Size(30, 22);
            fovLabel.TextAlign = ContentAlignment.MiddleRight;
            row.Controls.Add(this.numThumbDistance);
            row.Controls.Add(fovLabel);
            row.Controls.Add(this.numThumbFov);
            side.Controls.Add(row);

            side.Controls.Add(SideCaption("Camera position (m)"));
            side.Controls.Add(this.ThumbVectorRow(out this.numThumbPos, -60, 60, 3, 0.05m));
            side.Controls.Add(SideCaption("Facing (unit vector)"));
            side.Controls.Add(this.ThumbVectorRow(out this.numThumbFace, -1, 1, 4, 0.005m));

            this.chkThumbShow = new CheckBox();
            this.chkThumbShow.Text = "Show frame in the view";
            this.chkThumbShow.Checked = true;
            this.chkThumbShow.Size = new Size(SideWidth, 22);
            this.chkThumbShow.CheckedChanged += delegate { this.UpdateCameraOverlay(); };
            side.Controls.Add(this.chkThumbShow);

            this.btnThumbView = SideWideButton("View From Thumbnail Camera", delegate { this.ViewFromThumbnailCamera(); });
            this.btnThumbSave = SideWideButton("Save Thumbnail Camera Now", delegate { this.SaveThumbnailCamera(false); });
            this.btnThumbDefault = SideWideButton("Back To Default (remove car row)", delegate { this.SaveThumbnailCamera(true); });
            side.Controls.Add(this.btnThumbView);
            side.Controls.Add(this.btnThumbSave);
            side.Controls.Add(this.btnThumbDefault);
            side.Controls.Add(SideNote("Writes Media\\ThumbnailCamSettings.xml at once (backup first). Orange: the point it looks at and the frame the thumbnail captures there. The game's default is the same camera for every car; long cars have their own, further away. Z points forward."));
            this.EnableThumbnailEdit(false);
        }

        private NumericUpDown ThumbNumber(decimal min, decimal max, int decimals, decimal step)
        {
            NumericUpDown number = new NumericUpDown();
            number.Width = 66;
            number.Minimum = min;
            number.Maximum = max;
            number.DecimalPlaces = decimals;
            number.Increment = step;
            number.Value = min;
            number.BorderStyle = BorderStyle.FixedSingle;
            number.Margin = new Padding(0, 0, 5, 0);
            number.ValueChanged += new EventHandler(this.ThumbValueChanged);
            return number;
        }

        private FlowLayoutPanel ThumbVectorRow(out NumericUpDown[] numbers, decimal min, decimal max, int decimals, decimal step)
        {
            FlowLayoutPanel row = this.NumberRow(out numbers, min, max, decimals, step, 0);
            foreach (NumericUpDown number in numbers)
            {
                number.ValueChanged -= new EventHandler(this.PartEditValueChanged);
                number.ValueChanged += new EventHandler(this.ThumbValueChanged);
            }
            return row;
        }

        private void EnableThumbnailEdit(bool enabled)
        {
            foreach (NumericUpDown number in this.numThumbPos)
            {
                number.Enabled = enabled;
            }
            foreach (NumericUpDown number in this.numThumbFace)
            {
                number.Enabled = enabled;
            }
            this.numThumbDistance.Enabled = enabled;
            this.numThumbFov.Enabled = enabled;
            this.cmbThumbView.Enabled = enabled;
            this.btnThumbView.Enabled = enabled;
            this.btnThumbSave.Enabled = enabled;
            this.btnThumbDefault.Enabled = enabled;
        }

        private void ThumbViewSize(out int width, out int height)
        {
            bool main = this.cmbThumbView.SelectedIndex <= 0;
            width = main ? ThumbnailCamera.MainWidth : ThumbnailCamera.SideWidth;
            height = main ? ThumbnailCamera.MainHeight : ThumbnailCamera.SideHeight;
        }

        // Called with every car model shown (after the cockpit camera) and when the view changes.
        private void LoadThumbnailCamera()
        {
            this.thumbCurrent = null;
            this.thumbPending = false;
            this.EnableThumbnailEdit(false);
            string path = ThumbnailCamera.PathFor(this.GameRoot);
            if (this.camState == null)
            {
                this.lblThumbState.Text = "Open a car model from the game folder.";
            }
            else if (path == null || !File.Exists(path))
            {
                this.lblThumbState.Text = "GAME:\\Media\\ThumbnailCamSettings.xml not found.";
            }
            else
            {
                try
                {
                    int width, height;
                    this.ThumbViewSize(out width, out height);
                    ThumbnailCamera found = ThumbnailCamera.Find(ThumbnailCamera.Load(path), this.camState.CarId, width, height, out this.thumbOwn);
                    if (found == null)
                    {
                        this.lblThumbState.Text = "The file has no default camera for this view.";
                    }
                    else
                    {
                        this.thumbCurrent = found.Clone();
                        this.thumbAim = this.thumbCurrent.AimPoint;
                        this.FillThumbnailControls();
                        this.EnableThumbnailEdit(true);
                        this.btnThumbDefault.Enabled = this.thumbOwn;
                    }
                }
                catch (Exception exception)
                {
                    this.lblThumbState.Text = "Thumbnail camera not read: " + exception.Message;
                }
            }
            this.UpdateThumbnailLabel();
            this.UpdateCameraOverlay();
        }

        private void FillThumbnailControls()
        {
            this.fillingThumb = true;
            SetVector(this.numThumbPos, this.thumbCurrent.Position);
            SetVector(this.numThumbFace, this.thumbCurrent.Facing);
            this.numThumbDistance.Value = Math.Max(this.numThumbDistance.Minimum, Math.Min(this.numThumbDistance.Maximum, (decimal) Math.Round(this.thumbCurrent.Distance, 2)));
            this.numThumbFov.Value = Math.Max(this.numThumbFov.Minimum, Math.Min(this.numThumbFov.Maximum, (decimal) Math.Round(this.thumbCurrent.Fov, 2)));
            this.fillingThumb = false;
        }

        private void ThumbValueChanged(object sender, EventArgs e)
        {
            if (this.fillingThumb || this.thumbCurrent == null)
            {
                return;
            }
            this.thumbCurrent.Position = GetVector(this.numThumbPos);
            this.thumbCurrent.Facing = GetVector(this.numThumbFace);
            this.thumbCurrent.Fov = (float) this.numThumbFov.Value;
            if (sender == this.numThumbDistance)
            {
                // Same line of sight and aim point, the camera moves along it.
                this.thumbCurrent.Position = this.thumbAim - (this.thumbCurrent.Facing * (float) this.numThumbDistance.Value);
                this.fillingThumb = true;
                SetVector(this.numThumbPos, this.thumbCurrent.Position);
                this.fillingThumb = false;
            }
            else if (sender != this.numThumbFov)
            {
                this.thumbAim = this.thumbCurrent.AimPoint;
                this.fillingThumb = true;
                this.numThumbDistance.Value = Math.Max(this.numThumbDistance.Minimum, Math.Min(this.numThumbDistance.Maximum, (decimal) Math.Round(this.thumbCurrent.Distance, 2)));
                this.fillingThumb = false;
            }
            this.thumbPending = true;
            this.UpdateThumbnailLabel();
            this.UpdateCameraOverlay();
            if (this.LiveCameraView)
            {
                this.ViewFromThumbnailCamera();
            }
        }

        private void UpdateThumbnailLabel()
        {
            if (this.thumbCurrent == null)
            {
                return;
            }
            Vector3 aim = this.thumbAim;
            this.lblThumbState.Text = string.Format(CultureInfo.InvariantCulture, "{0} (id {1}): {2}{3}\nLooks at {4:0.00}, {5:0.00}, {6:0.00}",
                this.camState.MediaName, this.camState.CarId, this.thumbOwn ? "this car's own camera" : "the game's default camera (no row for this car)",
                this.thumbPending ? ", edited, not saved" : "", aim.X, aim.Y, aim.Z);
            this.lblThumbState.ForeColor = this.thumbPending ? ForzaTheme.Red : this.ForeColor;
        }

        private void ViewFromThumbnailCamera()
        {
            if (this.scene == null || this.thumbCurrent == null || this.thumbCurrent.Facing.LengthSquared() < 1e-6f)
            {
                return;
            }
            Vector3 position = CarCamera.ToModel(this.thumbCurrent.Position);
            Vector3 facing = Vector3.Normalize(CarCamera.ToModel(this.thumbCurrent.Facing));
            this.scene.Camera.LookFrom(position, position + facing);
            // The thumbnail is wider than most viewports: widen the vertical FOV so its whole width fits.
            float fov = this.thumbCurrent.Fov;
            float wanted = (float) this.thumbCurrent.Width / this.thumbCurrent.Height;
            float have = (float) this.scene.Viewport.Width / Math.Max(1, this.scene.Viewport.Height);
            if (have < wanted)
            {
                fov = MathHelper.ToDegrees(2f * (float) Math.Atan(Math.Tan(MathHelper.ToRadians(fov) / 2.0) * wanted / have));
            }
            this.scene.Camera.FieldOfViewDegrees = fov;
            this.scene.Update(true);
        }

        private void SaveThumbnailCamera(bool remove)
        {
            if (this.camState == null || this.thumbCurrent == null)
            {
                return;
            }
            string path = ThumbnailCamera.PathFor(this.GameRoot);
            int width, height;
            this.ThumbViewSize(out width, out height);
            ForzaZip.Entry open = this.EntryForFile(path);
            if (open != null && open.IsModified)
            {
                MessageBox.Show(this, "ThumbnailCamSettings.xml has pending changes in the open folder. Save them first (File > Save).", "Thumbnail Camera", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string question = remove
                ? string.Format("Remove the {0} x {1} thumbnail camera of {2} (id {3}) from\n{4}?\n\nThe game's default camera is used again.", width, height, this.camState.MediaName, this.camState.CarId, path)
                : string.Format(CultureInfo.InvariantCulture, "Write the {0} x {1} thumbnail camera of {2} (id {3}) into\n{4}?\n\nDistance {5:0.00} m, FOV {6:0.00}\n\nThe file is backed up first (bak folder next to the exe).",
                    width, height, this.camState.MediaName, this.camState.CarId, path, this.thumbCurrent.Distance, this.thumbCurrent.Fov);
            if (MessageBox.Show(this, question, "Thumbnail Camera", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
            {
                return;
            }
            try
            {
                string backup = BackupFile(path, "ThumbnailCamSettings.xml", BackupStamp());
                ThumbnailCamera camera = null;
                if (!remove)
                {
                    camera = this.thumbCurrent.Clone();
                    if (camera.Facing.LengthSquared() > 1e-6f)
                    {
                        camera.Facing = Vector3.Normalize(camera.Facing);
                    }
                }
                string done = ThumbnailCamera.Save(path, this.camState.CarId, width, height, camera);
                this.SetStatus(done + (backup != null ? "   (backup " + Path.GetFileName(backup) + ")" : "   (no backup: Backup is off in the ini)"));
                this.LoadThumbnailCamera();
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, "ThumbnailCamSettings.xml was not written: " + exception.Message, "Thumbnail Camera", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }

        // Aim point (orange cross), camera (orange cross), and at the aim distance the frame the
        // thumbnail captures (its aspect, the file's vertical FOV) with lines back to the camera.
        private List<VertexPositionColor> ThumbnailOverlayLines()
        {
            if (this.thumbCurrent == null || !this.chkThumbShow.Checked || this.thumbCurrent.Facing.LengthSquared() < 1e-6f)
            {
                return null;
            }
            Microsoft.Xna.Framework.Graphics.Color orange = new Microsoft.Xna.Framework.Graphics.Color(255, 150, 30, 255);
            List<VertexPositionColor> lines = new List<VertexPositionColor>();
            Vector3 eye = CarCamera.ToModel(this.thumbCurrent.Position);
            Vector3 forward = Vector3.Normalize(CarCamera.ToModel(this.thumbCurrent.Facing));
            Vector3 aim = CarCamera.ToModel(this.thumbAim);
            AddCross(lines, eye, 0.1f, orange);
            AddCross(lines, aim, 0.1f, orange);
            lines.Add(new VertexPositionColor(eye, orange));
            lines.Add(new VertexPositionColor(aim, orange));
            float depth = this.thumbCurrent.Distance;
            float halfHeight = depth * (float) Math.Tan(MathHelper.ToRadians(this.thumbCurrent.Fov) / 2f);
            float halfWidth = halfHeight * this.thumbCurrent.Width / this.thumbCurrent.Height;
            Vector3 right = Vector3.Normalize(Vector3.Cross(forward, Vector3.Up));
            Vector3 up = Vector3.Cross(right, forward);
            Vector3 centre = eye + (forward * depth);
            Vector3[] corners =
            {
                centre - (right * halfWidth) + (up * halfHeight), centre + (right * halfWidth) + (up * halfHeight),
                centre + (right * halfWidth) - (up * halfHeight), centre - (right * halfWidth) - (up * halfHeight)
            };
            for (int i = 0; i < 4; i++)
            {
                lines.Add(new VertexPositionColor(eye, orange));
                lines.Add(new VertexPositionColor(corners[i], orange));
                lines.Add(new VertexPositionColor(corners[i], orange));
                lines.Add(new VertexPositionColor(corners[(i + 1) % 4], orange));
            }
            return lines;
        }
    }
}
