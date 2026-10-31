/*
    This is applicate to all source code of all project
    
    Forza Studio: Forza Motorsport resource extraction tool.

    Copyright (C) 2012 Mike Davis (http://codeescape.com/)

    Permission is hereby granted, free of charge, to any person 
    obtaining a copy of this software and associated documentation
    files (the "Software"), to deal in the Software without restriction,
    including without limitation the rights to use, copy, modify, merge,
    publish, distribute, sublicense, and/or sell copies of the Software,
    and to permit persons to whom the Software is furnished to do so,
    subject to the following conditions:

    The above copyright notice and this permission notice shall be included 
    in all copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS
    OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
    THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
    FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
    DEALINGS IN THE SOFTWARE.
*/

namespace ForzaStudio
{
    using Microsoft.Xna.Framework;
    using System;
    using System.Runtime.CompilerServices;
    using System.Windows.Forms;

    public class Camera
    {
        private float FieldOfView;
        public Vector3 ForwardDirection;
        private bool hasChanged = true;
        public Vector3 HorizontalDirection;
        private float LookAcceleration;
        private Vector2 LookAngle;
        public Vector3 LookAt;
        public float LookSpeedScale;
        private Vector2 LookVelocity;
        private float MoveAcceleration;
        private Vector3 Position;
        public float SpeedScale;
        private Vector3 Velocity;
        public Vector3 VerticalDirection;
        private float ViewDistance;
        private Panel Viewport;
        public readonly Matrix World = Matrix.Identity;
        private float ZoomVelocity;

        public Camera(Panel viewport)
        {
            this.Viewport = viewport;
            this.SpeedScale = 1f;
            this.LookSpeedScale = 1f;
            this.MoveAcceleration = 0.9f;
            this.LookAcceleration = 0.9f;
            this.ZoomVelocity = 0f;
            this.ViewDistance = 2500f;
            this.MaxLookAngle = 1.57f;
            this.MinLookAngle = -1.57f;
            this.MaxMoveSpeed = 5f;
            this.MinMoveSpeed = 0.01f;
            this.MaxLookSpeed = 5f;
            this.MinLookSpeed = 0.01f;
            this.MaxMoveAccel = 1f;
            this.MinMoveAccel = 0.01f;
            this.MaxLookAccel = 1f;
            this.MinLookAccel = 0.01f;
            // Vertical FOV, as in the game. FM4's photo mode and garage use 45 degrees
            // (UICarTargetCameraPresets.xml); its free camera zooms between 0.5 and 90
            // (Media\Camera\CameraSettings.ini, FreeCam\MinFOV and MaxFOV).
            this.FieldOfView = this.DegreesToRadians(45f);
            this.MinFieldOfView = this.DegreesToRadians(0.5f);
            this.MaxFieldOfView = this.DegreesToRadians(90f);
            this.MoveTo(new Vector3(2.1f, 1.25f, 1.1f));
            this.LookTo(new Vector2(-2.75f, -0.4f));
        }

        public void ApplyForce(Vector3 force)
        {
            this.Velocity += (Vector3) ((force * this.SpeedScale) * 0.5f);
        }

        public void ApplyLookForce(Vector2 force)
        {
            this.LookVelocity = (Vector2) ((force * this.LookSpeedScale) * 4f);
        }

        public void ApplyZoomForce(float force)
        {
            this.ZoomVelocity += force;
        }

        private float Clamp(float value, float lowerBound, float upperBound)
        {
            if (value > upperBound)
            {
                return upperBound;
            }
            if (value < lowerBound)
            {
                return lowerBound;
            }
            return value;
        }

        private float DegreesToRadians(float degrees)
        {
            return (float) (degrees * 0.017453292519943295);
        }

        public void LookTo(Vector2 direction)
        {
            this.LookAngle = direction;
            this.hasChanged = true;
        }

        public void MoveTo(Vector3 position)
        {
            this.Position = position;
            this.hasChanged = true;
        }

        // Places the camera at 'position' looking at 'target'.
        public void LookFrom(Vector3 position, Vector3 target)
        {
            Vector3 forward = target - position;
            if (forward.LengthSquared() < 1e-10f)
            {
                return;
            }
            forward.Normalize();
            this.Orthographic = false;
            this.Velocity = Vector3.Zero;
            this.LookVelocity = Vector2.Zero;
            this.MoveTo(position);
            this.LookTo(new Vector2((float) Math.Atan2(forward.Z, forward.X), (float) Math.Asin(forward.Y)));
        }

        private float RadiansToDegrees(float radians)
        {
            return (float) (radians * 57.295779513082323);
        }

        public void Update(float screenWidth, float screenHeight)
        {
            this.Velocity -= (Vector3) (this.Velocity * this.MoveAcceleration);
            this.LookVelocity -= (Vector2) (this.LookVelocity * this.LookAcceleration);
            this.ZoomVelocity -= this.ZoomVelocity * 0.2f;
            this.hasChanged = ((Math.Abs(this.Velocity.Length()) > 0.0001) || (Math.Abs(this.LookVelocity.Length()) > 0.0001)) || (Math.Abs(this.ZoomVelocity) > 0.0001);
            this.Position += this.Velocity;
            this.LookAngle += this.LookVelocity;
            if (this.Orthographic)
            {
                this.OrthoHeight = this.Clamp(this.OrthoHeight * (1f + this.ZoomVelocity), 0.05f, 200f);
            }
            else
            {
                this.FieldOfView *= 1f + this.ZoomVelocity;
            }
            this.LookAngle.X = (float) ((((((double) this.LookAngle.X) / 6.2831853071795862) - ((int) (((double) this.LookAngle.X) / 6.2831853071795862))) * 3.1415926535897931) * 2.0);
            this.LookAngle.Y = this.Clamp(this.LookAngle.Y, this.MinLookAngle, this.MaxLookAngle);
            this.FieldOfView = this.Clamp(this.FieldOfView, this.MinFieldOfView, this.MaxFieldOfView);
            float num = (float) Math.Sin((double) this.LookAngle.X);
            float y = (float) Math.Sin((double) this.LookAngle.Y);
            float num3 = (float) Math.Cos((double) this.LookAngle.X);
            float num4 = (float) Math.Cos((double) this.LookAngle.Y);
            float x = (float) Math.Cos((double) (this.LookAngle.X + 1.57f));
            float z = (float) Math.Sin((double) (this.LookAngle.X + 1.57f));
            this.ForwardDirection = new Vector3(num3 * num4, y, num * num4);
            this.VerticalDirection = new Vector3(-num3 * y, num4, -num * y);
            this.HorizontalDirection = new Vector3(x, 0f, z);
            this.LookAt = this.Position + this.ForwardDirection;
            // Up is the camera's own up, which equals world up except looking straight up or down
            // (the top and bottom views), where world up would be parallel to the view direction.
            this.View = Matrix.CreateLookAt(this.Position, this.LookAt, this.VerticalDirection);
            // The aspect ratio is the panel's, the size the image is shown at. Near clip 1 cm: the old
            // 0.1 mm against a 2500 m far plane left the depth buffer too little precision.
            float aspect = (screenHeight >= 1f) ? (screenWidth / screenHeight) : 1f;
            this.ViewportHeight = screenHeight;
            if (this.Orthographic)
            {
                this.Projection = Matrix.CreateOrthographic(this.OrthoHeight * aspect, this.OrthoHeight, 0.01f, this.ViewDistance);
            }
            else
            {
                this.Projection = Matrix.CreatePerspectiveFieldOfView(this.FieldOfView, aspect, 0.01f, this.ViewDistance);
            }
        }

        // ---- Orthographic projection and the standard views ----
        // An orthographic camera always sits OrthoDistance in front of the point it looks at, so
        // switching between the two projections keeps what is in the middle of the view.
        public const float OrthoDistance = 30f;
        public Vector3 EyePosition { get { return this.Position; } }
        public bool Orthographic { get; private set; }
        public float OrthoHeight { get; private set; }     // metres visible from the bottom to the top of the view
        private float ViewportHeight = 1f;

        public enum StandardView { Bottom, Left, Right, Front, Back, Top }

        // Model axes: Y up, the car's front toward -Z, its right side toward +X. Top and bottom views
        // have the front at the top of the screen.
        public void SetStandardView(StandardView view, Vector3 centre, float height)
        {
            float azimuth, elevation;
            switch (view)
            {
                case StandardView.Top: azimuth = -1.5707964f; elevation = this.MinLookAngle; break;
                case StandardView.Bottom: azimuth = 1.5707964f; elevation = this.MaxLookAngle; break;
                case StandardView.Left: azimuth = 0f; elevation = 0f; break;           // from -X, looking toward +X
                case StandardView.Right: azimuth = 3.1415927f; elevation = 0f; break;  // from +X, looking toward -X
                case StandardView.Front: azimuth = 1.5707964f; elevation = 0f; break;  // from -Z, looking toward +Z
                default: azimuth = -1.5707964f; elevation = 0f; break;                 // Back: from +Z, looking toward -Z
            }
            this.Velocity = Vector3.Zero;
            this.LookVelocity = Vector2.Zero;
            this.ZoomVelocity = 0f;
            this.Orthographic = true;
            this.OrthoHeight = Math.Max(0.1f, height);
            this.LookAngle = new Vector2(azimuth, elevation);
            float cosE = (float) Math.Cos(elevation);
            Vector3 forward = new Vector3((float) Math.Cos(azimuth) * cosE, (float) Math.Sin(elevation), (float) Math.Sin(azimuth) * cosE);
            this.Position = centre - (forward * OrthoDistance);
            this.hasChanged = true;
        }

        // Switches projection keeping the pose: perspective -> orthographic fits the height seen at the
        // 'focus' distance; orthographic -> perspective moves back so the same height is visible.
        public void SetOrthographic(bool on, Vector3 focus, float halfDepth)
        {
            if (on == this.Orthographic)
            {
                return;
            }
            float tan = (float) Math.Tan(this.FieldOfView / 2f);
            if (on)
            {
                float distance = Math.Max(0.5f, Vector3.Dot(focus - this.Position, this.ForwardDirection));
                this.OrthoHeight = Math.Max(0.1f, 2f * Math.Max(0.3f, distance - halfDepth) * tan);
                this.Position += this.ForwardDirection * (distance - OrthoDistance);
            }
            else
            {
                float distance = ((this.OrthoHeight / 2f) / tan) + halfDepth;
                this.Position += this.ForwardDirection * (OrthoDistance - distance);
            }
            this.Orthographic = on;
            this.hasChanged = true;
        }

        private Vector3 ForwardOf(Vector2 angle)
        {
            float cosE = (float) Math.Cos(angle.Y);
            return new Vector3((float) Math.Cos(angle.X) * cosE, (float) Math.Sin(angle.Y), (float) Math.Sin(angle.X) * cosE);
        }

        // Turns the view around 'pivot' (perspective) or around the point the orthographic camera
        // looks at, keeping the distance: the model turns under the mouse as in a 3D editor.

        // Centres the view on a sphere and backs off until it fits (perspective), or sizes the view to it (orthographic).
        public void FocusOn(Vector3 centre, float radius)
        {
            radius = Math.Max(radius, 0.05f);
            Vector3 forward = this.ForwardOf(this.LookAngle);
            this.Velocity = Vector3.Zero;
            if (this.Orthographic)
            {
                this.OrthoHeight = Math.Max(0.1f, radius * 2.4f);
                this.Position = centre - (forward * OrthoDistance);
            }
            else
            {
                float distance = (radius / (float) Math.Sin(Math.Min(this.FieldOfView, 1.5f) / 2f)) * 1.15f;
                this.Position = centre - (forward * distance);
            }
            this.hasChanged = true;
        }

        // The wheel in perspective: moves toward (+1) or away from (-1) the pivot by a tenth of the distance,
        // and not through it.
        public void Dolly(int direction, Vector3 pivot)
        {
            Vector3 forward = this.ForwardOf(this.LookAngle);
            float distance = Vector3.Distance(this.Position, pivot);
            float step = Math.Max(distance, 0.5f) * 0.1f * direction;
            if (direction > 0 && distance - step < 0.3f)
            {
                step = Math.Max(0f, distance - 0.3f);
            }
            this.Velocity = Vector3.Zero;
            this.Position += forward * step;
            this.hasChanged = true;
        }

        public void Orbit(float dAzimuth, float dElevation, Vector3 pivot)
        {
            // The pivot keeps its place on the screen: its offset from the camera is kept in the camera's
            // own axes and rebuilt with the new ones, so the first drag does not swing the view to the pivot.
            Vector3 right, up, forward;
            this.Basis(this.LookAngle, out right, out up, out forward);
            Vector3 offset = pivot - this.Position;
            float along = Vector3.Dot(offset, right), high = Vector3.Dot(offset, up), ahead = Vector3.Dot(offset, forward);
            Vector2 angle = this.LookAngle + new Vector2(dAzimuth, dElevation);
            angle.Y = this.Clamp(angle.Y, this.MinLookAngle, this.MaxLookAngle);
            this.Basis(angle, out right, out up, out forward);
            this.Velocity = Vector3.Zero;
            this.LookVelocity = Vector2.Zero;
            this.LookAngle = angle;
            this.Position = pivot - ((right * along) + (up * high) + (forward * ahead));
            this.hasChanged = true;
        }

        private void Basis(Vector2 angle, out Vector3 right, out Vector3 up, out Vector3 forward)
        {
            float sinA = (float) Math.Sin(angle.X), cosA = (float) Math.Cos(angle.X);
            float sinE = (float) Math.Sin(angle.Y), cosE = (float) Math.Cos(angle.Y);
            forward = new Vector3(cosA * cosE, sinE, sinA * cosE);
            up = new Vector3(-cosA * sinE, cosE, -sinA * sinE);
            right = new Vector3(-sinA, 0f, cosA);
        }

        // Moves the view with the mouse (middle drag): the scene follows the cursor, 1:1 at the plane
        // 'distance' away (perspective) or anywhere (orthographic).
        public void PanPixels(float dx, float dy, float distance)
        {
            float height = this.Orthographic ? this.OrthoHeight : 2f * Math.Max(0.3f, distance) * (float) Math.Tan(this.FieldOfView / 2f);
            float perPixel = height / Math.Max(1f, this.ViewportHeight);
            Vector3 right, up, forward;
            this.Basis(this.LookAngle, out right, out up, out forward);
            this.Position += (up * (dy * perPixel)) - (right * (dx * perPixel));
            this.hasChanged = true;
        }
        // Vertical field of view in degrees, clamped to the zoom range.
        public float FieldOfViewDegrees
        {
            get
            {
                return this.RadiansToDegrees(this.FieldOfView);
            }
            set
            {
                this.ZoomVelocity = 0f;
                this.FieldOfView = this.Clamp(this.DegreesToRadians(value), this.MinFieldOfView, this.MaxFieldOfView);
                this.hasChanged = true;
            }
        }

        public void Zoom(float scale)
        {
            this.FieldOfView = this.Clamp(this.FieldOfView * scale, this.MinFieldOfView, this.MaxFieldOfView);
        }

        public bool HasChanged
        {
            get
            {
                return this.hasChanged;
            }
        }

        public float MaxFieldOfView { get; set; }

        public float MaxLookAccel { get; set; }

        public float MaxLookAngle { get; set; }

        public float MaxLookSpeed { get; set; }

        public float MaxMoveAccel { get; set; }

        public float MaxMoveSpeed { get; set; }

        public float MinFieldOfView { get; set; }

        public float MinLookAccel { get; set; }

        public float MinLookAngle { get; set; }

        public float MinLookSpeed { get; set; }

        public float MinMoveAccel { get; set; }

        public float MinMoveSpeed { get; set; }

        public Matrix Projection { get; set; }

        public Matrix View { get; set; }
    }
}

