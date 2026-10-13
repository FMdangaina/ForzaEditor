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
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential)]
    public struct BoundingBox
    {
        public float MinX;
        public float MaxX;
        public float MinY;
        public float MaxY;
        public float MinZ;
        public float MaxZ;
        public float XRange
        {
            get
            {
                return (this.MaxX - this.MinX);
            }
        }
        public float YRange
        {
            get
            {
                return (this.MaxY - this.MinY);
            }
        }
        public float ZRange
        {
            get
            {
                return (this.MaxZ - this.MaxX);
            }
        }
        public Vector3 Min
        {
            get
            {
                return new Vector3(this.MinX, this.MinY, this.MinZ);
            }
        }
        public Vector3 Max
        {
            get
            {
                return new Vector3(this.MaxX, this.MaxY, this.MaxZ);
            }
        }
        public Vector3 Range
        {
            get
            {
                return new Vector3(this.MaxX - this.MinX, this.MaxY - this.MinY, this.MaxZ - this.MinZ);
            }
        }
        public static ForzaStudio.BoundingBox FromVertices(ForzaVertex[] vertices)
        {
            ForzaStudio.BoundingBox box = new ForzaStudio.BoundingBox();
            if ((vertices != null) && (vertices.Length > 0))
            {
                foreach (ForzaVertex vertex in vertices)
                {
                    box.MinX = Math.Min(vertex.position.X, box.MinX);
                    box.MaxX = Math.Max(vertex.position.X, box.MaxX);
                    box.MinY = Math.Min(vertex.position.Y, box.MinY);
                    box.MaxY = Math.Max(vertex.position.Y, box.MaxY);
                    box.MinZ = Math.Min(vertex.position.Z, box.MinZ);
                    box.MaxZ = Math.Max(vertex.position.Z, box.MaxZ);
                }
            }
            return box;
        }
    }
}

