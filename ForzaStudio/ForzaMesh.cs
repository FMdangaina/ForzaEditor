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
    using Microsoft.Xna.Framework.Graphics;
    using System;

    public class ForzaMesh : IDisposable
    {
        public int[] Indices;
        public string MaterialName;
        public string Name;
        public ForzaVertex[] Vertices;
        // Position in the carbin (section, subsection in file order), -1 if unknown: lets an edit
        // reach this one mesh when several in a part share the material name.
        public int SectionIndex = -1;
        public int SubIndex = -1;

        public ForzaMesh(string name, string materialName, int[] indices, ForzaVertex[] vertices)
        {
            this.Name = name;
            this.MaterialName = materialName;
            this.Indices = indices;
            this.Vertices = vertices;
        }

        public void Dispose()
        {
            this.Name = null;
            this.MaterialName = null;
            this.Indices = null;
            this.Vertices = null;
            this.displayVertices = null;
        }

        // Draw with the second UV set in TEXCOORD0 (some materials map their texture with UV2).
        public bool UseSecondUV;

        // Live preview of a pending part edit: drawn with this extra world transform, tinted when
        // highlighted. The vertices themselves are untouched until the edit is applied.
        public Matrix PreviewTransform = Matrix.Identity;
        public bool Highlight;
        public bool Hidden;     // "show only this part": the other parts are not drawn
        private ForzaVertex[] displayVertices;
        private bool displaySecondUV;

        public void Draw(ref GraphicsDevice device)
        {
            // Parsed UVs are bottom-up (OBJ convention); Direct3D samples top-down.
            if (this.displayVertices == null || this.displaySecondUV != this.UseSecondUV)
            {
                this.displayVertices = (ForzaVertex[]) this.Vertices.Clone();
                for (int i = 0; i < this.displayVertices.Length; i++)
                {
                    Vector2 uv = this.UseSecondUV ? this.displayVertices[i].texture1 : this.displayVertices[i].texture0;
                    this.displayVertices[i].texture0 = new Vector2(uv.X, 1f - uv.Y);
                }
                this.displaySecondUV = this.UseSecondUV;
            }
            ForzaVertex[] vertices = this.displayVertices;
            device.VertexDeclaration = new VertexDeclaration(device, ForzaVertex.VertexElements);
            device.DrawUserIndexedPrimitives<ForzaVertex>(PrimitiveType.TriangleList, vertices, 0, vertices.Length, this.Indices, 0, this.FaceCount);
        }

        ~ForzaMesh()
        {
            this.Dispose();
        }

        public int FaceCount
        {
            get
            {
                return (this.Indices.Length / 3);
            }
        }

        public int VertexCount
        {
            get
            {
                return this.Vertices.Length;
            }
        }
    }
}

