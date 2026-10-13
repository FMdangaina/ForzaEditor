using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;

namespace ForzaStudio
{
    public partial class SaveModding : Form
    {
        public SaveModding()
        {
            InitializeComponent();
        }

        private void openFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var save = new OpenFileDialog { Filter = "ForzaProfile or CON Package|*.*" };

            if (save.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    //Fucking NET Framework
                    //DecryptFile(File.WriteAllBytes(Application.StartupPath + "\\save.tmp", File.ReadAllBytes(save.FileName)), save.FileName, (byte)0x33));
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Save " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        void DecryptFile(string OutPath, string InPath, byte[] ProfileID)
        {/*
            BinaryReader reader = new BinaryReader(new FileStream(InPath, FileMode.Open, FileAccess.Read));
            BinaryWriter writer = new BinaryWriter(new FileStream(OutPath, FileMode.Create, FileAccess.Write));

            byte[] AES_KEY = { 
                            0xB4, 0xEB, 0x7F, 0xB9, 
                            0xA9, 0xAC, 0x05, 0x92, 
                            0xAB, 0x8E, 0x42, 0xAB,
	                        0xCC, 0x61, 0xD0, 0xC0
                        };

            if (reader.ReadUInt32() != 0x73736D63)
                throw new Exception("Invalid File");

            uint ReadSize = BSwapUint(reader.ReadUInt32());

            byte[] Hash = reader.ReadBytes(0x20);
            uint DataSizeEnc = BSwapUint(reader.ReadUInt32());

            byte[] AES_IV = reader.ReadBytes(0x10);

            uint DataSizeDec = DecryptDataSize(AES_IV, DataSizeEnc);
            byte[] DataBuffer = reader.ReadBytes((int)(DataSizeDec));

            XorAes(ref AES_KEY, ProfileID, 1);

            AesCryptoServiceProvider aes = new AesCryptoServiceProvider();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.None;

            byte[] Decrypted = aes.CreateDecryptor(AES_KEY, AES_IV).TransformFinalBlock(DataBuffer, 0, DataBuffer.Length);

            writer.Write(0x73736D63);
            writer.Write(BSwapUint(ReadSize));
            writer.Write(Hash);
            writer.Write(BSwapUint(DataSizeEnc));
            writer.Write(AES_IV);
            writer.Write(Decrypted);

            writer.Flush();
            writer.Close();
            reader.Close();
          */
        }

        uint DecryptDataSize(byte[] IV, uint DataSize)
        {
            return
                (uint)((
                DataSize ^
                BSwapUint(BitConverter.ToUInt32(IV, 0)) ^
                BSwapUint(BitConverter.ToUInt32(IV, 4)) ^
                BSwapUint(BitConverter.ToUInt32(IV, 8)) ^
                BSwapUint(BitConverter.ToUInt32(IV, 12))) + 15) & 0xFFFFFFF0;
        }

        uint BSwapUint(uint num)
        {
            return
                (uint)((num & 0x000000FF) << 24) |
                (uint)((num & 0x0000FF00) << 8) |
                (uint)((num & 0x00FF0000) >> 8) |
                (uint)((num & 0xFF000000) >> 24)
                ;
        }

        void XorAes(ref byte[] Buf, byte[] Key, int Start)
        {
            for (int i = 0; i < Buf.Length; i++)
                Buf[i & 15] ^= Key[(i + Start) & 7];
        }
    }
}
