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
    using System.Collections;

    public static class Utilities
    {
        public static void AssertEquals(byte obj1, byte obj2)
        {
            if (!obj1.Equals(obj2))
            {
                throw new Exception(string.Format("Analyze this! Expected: {0}, Actual: {1}", obj2.ToString(), obj1.ToString()));
            }
        }

        public static void AssertEquals(float obj1, float obj2)
        {
            if (!obj1.Equals(obj2))
            {
                throw new Exception(string.Format("Analyze this! Expected: {0}, Actual: {1}", obj2.ToString(), obj1.ToString()));
            }
        }

        public static void AssertEquals(uint obj1, uint obj2)
        {
            if (!obj1.Equals(obj2))
            {
                throw new Exception(string.Format("Analyze this! Expected: {0}, Actual: {1}", obj2.ToString(), obj1.ToString()));
            }
        }

        public static Vector3 CalculateBoundTargetValue(Vector3 actualValue, Vector3 actualMin, Vector3 actualMax, Vector3 targetMin, Vector3 targetMax)
        {
            return (targetMin + (((actualValue - actualMin) / (actualMax - actualMin)) * (targetMax - targetMin)));
        }

        public static float CalculateBoundTargetValue(float actualValue, float actualMin, float actualMax, float targetMin, float targetMax)
        {
            if ((actualValue > actualMax) || (actualValue < actualMin))
            {
                throw new ArgumentException("Actual value falls outside of actual range");
            }
            if (targetMin >= targetMax)
            {
                throw new ArgumentException("Invalid target range");
            }
            return (targetMin + (((actualValue - actualMin) / (actualMax - actualMin)) * (targetMax - targetMin)));
        }

        public static int CalculateFaceCount(int[] indices, IndexType type)
        {
            if (type == IndexType.TriStrip)
            {
                int num = 0;
                int num2 = 0;
                while (num < (indices.Length - 2))
                {
                    if (indices[num + 2] != -1)
                    {
                        num++;
                        num2++;
                    }
                    else
                    {
                        num += 3;
                    }
                }
                return num2;
            }
            if (type != IndexType.TriList)
            {
                throw new NotSupportedException("Unknown index type.");
            }
            return (indices.Length / 3);
        }

        public static int CalculateVertexCount(int[] indices)
        {
            Hashtable hashtable = new Hashtable();
            foreach (int num in indices)
            {
                if (num != -1)
                {
                    hashtable[num] = 0;
                }
            }
            return hashtable.Count;
        }

        public static int[] GenerateTriangleList(int[] indices, int faceCount)
        {
            int[] numArray = new int[faceCount * 3];
            bool flag = true;
            int index = 0;
            int num5 = 0;
            while (index < (indices.Length - 2))
            {
                int num3 = indices[index + 2];
                if (num3 != -1)
                {
                    int num = indices[index];
                    int num2 = indices[index + 1];
                    index++;
                    if (((num != num2) && (num2 != num3)) && (num != num3))
                    {
                        if (flag)
                        {
                            numArray[num5] = num;
                            numArray[num5 + 1] = num2;
                            numArray[num5 + 2] = num3;
                        }
                        else
                        {
                            numArray[num5] = num2;
                            numArray[num5 + 1] = num;
                            numArray[num5 + 2] = num3;
                        }
                    }
                    num5 += 3;
                    flag = !flag;
                }
                else
                {
                    flag = true;
                    index += 3;
                }
            }
            return numArray;
        }

        public static ForzaVertex[] GenerateVertices(ForzaVertex[] sectionVertices, ref int[] subSectionIndices)
        {
            int num = 0;
            Hashtable hashtable = new Hashtable();
            int length = subSectionIndices.Length;
            for (int i = 0; i < length; i++)
            {
                int num4 = subSectionIndices[i];
                if (num4 != -1)
                {
                    if (hashtable[num4] == null)
                    {
                        hashtable[num4] = num;
                        num++;
                    }
                    subSectionIndices[i] = (int) hashtable[num4];
                }
            }
            ForzaVertex[] vertexArray = new ForzaVertex[num];
            foreach (DictionaryEntry entry in hashtable)
            {
                vertexArray[(int) entry.Value] = sectionVertices[(int) entry.Key];
            }
            return vertexArray;
        }

        public static Color GetRandomColor(int seed)
        {
            Random random = new Random(seed);
            return new Color((byte) random.Next(0, 0xff), (byte) random.Next(0, 0xff), (byte) random.Next(0, 0xff));
        }

        public static bool IsNullOrWhiteSpace(string s)
        {
            if (s != null)
            {
                return (s.Trim().Length == 0);
            }
            return true;
        }

        public static int[] ReadIndices(EndianStream stream, int count, int size)
        {
            int[] numArray = new int[count];
            for (int i = 0; i < numArray.Length; i++)
            {
                int num2 = (size == 2) ? stream.ReadUInt16() : stream.ReadInt32();
                if (((size == 2) && (num2 == 0xffff)) || ((size == 4) && (num2 >= 0xffffff)))
                {
                    num2 = -1;
                    numArray[i] = num2;
                }
                else
                {
                    numArray[i] = num2 + ForzaCarSection.IBoffset;
                }
            }
            return numArray;
        }
    }
}

