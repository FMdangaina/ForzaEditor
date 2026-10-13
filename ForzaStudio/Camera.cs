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
            this.FieldOfView = this.DegreesToRadians(90f);
            this.MinFieldOfView = this.DegreesToRadians(0.05f);
            this.MaxFieldOfView = this.DegreesToRadians(120f);
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
            this.FieldOfView *= 1f + this.ZoomVelocity;
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
            this.View = Matrix.CreateLookAt(this.Position, this.LookAt, new Vector3(0f, 1f, 0f));
            this.Projection = Matrix.CreatePerspectiveFieldOfView(this.FieldOfView, ((float) this.Viewport.Width) / ((float) this.Viewport.Height), 0.0001f, this.ViewDistance);
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

