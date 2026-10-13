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

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace ForzaStudio
{
    // Token: 0x02000005 RID: 5
    public class ForzaTexture : ForzaResource
    {
        // Token: 0x0600001A RID: 26 RVA: 0x00002601 File Offset: 0x00000801
        public ForzaTexture(string filePath)
            : base(filePath, (string) null)
        {
        }

        // Token: 0x06000019 RID: 25 RVA: 0x000025F7 File Offset: 0x000007F7
        public ForzaTexture(string archivePath, string filePath)
            : base(filePath, archivePath)
        {
        }

        // Token: 0x06000018 RID: 24
        [DllImport("xds.dll")]
        private unsafe static extern bool ConvertToBitmap(byte* inputData, int inputSize, int d3dFormat, int width, int height, void* outputData);

        // Token: 0x06000017 RID: 23
        [DllImport("xds.dll")]
        private unsafe static extern void Create(byte* inputData, int inputSize, void* outputData);

        // Token: 0x0600001E RID: 30 RVA: 0x00002B54 File Offset: 0x00000D54
        public override void Dispose()
        {
        }

        // Token: 0x0600001D RID: 29 RVA: 0x00002900 File Offset: 0x00000B00
        public Image GetImage()
        {
            try
            {
                if (base.FilePath.EndsWith(".xds", StringComparison.InvariantCultureIgnoreCase))
                {
                    byte[] data = base.GetData();
                    try
                    {
                        return new XdsTexture(data).GetLevelBitmap(0);
                    }
                    catch (NotSupportedException)
                    {
                    }
                    using (MemoryStream memoryStream = new MemoryStream(data))
                    {
                        Image result = this.LoadXds(memoryStream);
                        return result;
                    }
                }
                if (base.FilePath.EndsWith(".bix", StringComparison.InvariantCultureIgnoreCase))
                {
                    Path.GetDirectoryName(base.FilePath);
                    string str = base.FilePath.Replace("_b.bix", string.Empty).Replace(".bix", string.Empty);
                    string text = str + ".bix";
                    string text2 = str + "_b.bix";
                    byte[] array = null;
                    byte[] array2 = null;
                    if (Utilities.IsNullOrWhiteSpace(base.ArchivePath))
                    {
                        using (FileStream fileStream = new FileStream(text, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            array = new byte[fileStream.Length];
                            fileStream.Read(array, 0, array.Length);
                        }
                        using (FileStream fileStream2 = new FileStream(text2, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            array2 = new byte[fileStream2.Length];
                            fileStream2.Read(array2, 0, array2.Length);
                            goto IL_133;
                        }
                    }
                    array = ForzaArchive.GetFileData(base.ArchivePath, text);
                    array2 = ForzaArchive.GetFileData(base.ArchivePath, text2);
                IL_133:
                    uint width;
                    uint height;
                    uint format;
                    using (EndianStream endianStream = new EndianStream(array, EndianType.BigEndian))
                    {
                        uint num = endianStream.ReadUInt32();
                        if (num != 1112102960u && num != 1112102961u)
                        {
                            throw new NotSupportedException("Unrecognized bix file format.");
                        }
                        width = endianStream.ReadUInt32();
                        height = endianStream.ReadUInt32();
                        endianStream.ReadUInt32();
                        format = endianStream.ReadUInt32();
                        endianStream.ReadUInt32();
                        endianStream.ReadUInt32();
                    }
                    Image result = this.LoadBix(array2, (int)width, (int)height, (int)format);
                    return result;
                }
                throw new NotSupportedException("Unknown file extension");
            }
            catch (Exception)
            {
            }
            return null;
        }

        // Token: 0x06000016 RID: 22
        [DllImport("xds.dll")]
        private unsafe static extern void GetTextureSize(byte* inputData, int inputSize, out int outputWidth, out int outputHeight);

        // Token: 0x0600001C RID: 28 RVA: 0x00002820 File Offset: 0x00000A20
        private unsafe Image LoadBix(byte[] data, int width, int height, int format)
        {
            if (data != null && data.Length > 0 && width > 0 && height > 0 && format > 0)
            {
                Bitmap bitmap = null;
                try
                {
                    bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                    BitmapData bitmapData = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadWrite, bitmap.PixelFormat);
                    try
                    {
                        fixed (byte* ptr = data)
                        {
                            if (!ForzaTexture.ConvertToBitmap(ptr, data.Length, format, width, height, bitmapData.Scan0.ToPointer()))
                            {
                                throw new Exception();
                            }
                            bitmap.UnlockBits(bitmapData);
                            return bitmap;
                        }
                    }
                    finally
                    {
                        byte* ptr = null;
                    }
                }
                catch (Exception)
                {
                    if (bitmap != null)
                    {
                        bitmap.Dispose();
                    }
                    throw new Exception("Unable to load texture.");
                }
            }
            throw new ArgumentException();
        }

        // Token: 0x0600001B RID: 27 RVA: 0x0000260C File Offset: 0x0000080C
        private unsafe Image LoadXds(Stream stream)
        {
            int num = (int)stream.Length;
            byte[] array = new byte[num + 8192];
            fixed (byte* ptr = array)
            {
                uint num2 = new UIntPtr((void*)ptr).ToUInt32();
                byte* ptr2 = (byte*)new UIntPtr(num2 + 4095u & 4294963200u).ToPointer();
                int num3 = (int)(new UIntPtr((void*)ptr2).ToUInt32() - num2);
                Image result;
                using (MemoryStream memoryStream = new MemoryStream(array))
                {
                    using (BinaryWriter binaryWriter = new BinaryWriter(memoryStream))
                    {
                        memoryStream.Position = (long)num3;
                        binaryWriter.Write(16777216);
                        binaryWriter.Write(1144150100);
                        binaryWriter.Write(805306368);
                        binaryWriter.Write(872415232);
                        binaryWriter.Write(335544320);
                        binaryWriter.Write(new byte[28]);
                        stream.Read(array, num3 + 48, 52);
                        if (BitConverter.ToInt32(array, num3 + 48) != 50331648 || BitConverter.ToInt32(array, num3 + 52) != 16777216)
                        {
                            throw new Exception("Not a valid XDS file");
                        }
                        stream.Read(array, num3 + 4096, num - 52);
                        int width;
                        int height;
                        ForzaTexture.GetTextureSize(ptr2, num, out width, out height);
                        Bitmap bitmap = null;
                        try
                        {
                            bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                            BitmapData bitmapData = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadWrite, bitmap.PixelFormat);
                            ForzaTexture.Create(ptr2, num, bitmapData.Scan0.ToPointer());
                            bitmap.UnlockBits(bitmapData);
                            result = bitmap;
                        }
                        catch (Exception)
                        {
                            if (bitmap != null)
                            {
                                bitmap.Dispose();
                            }
                            throw new Exception("Unable to load texture.");
                        }
                    }
                }
                return result;
            }
        }
    }
}
