using System;
using System.Drawing;
using System.Diagnostics;
using System.Windows.Forms;
using System.IO;

namespace ForzaStudio
{
    public partial class XPREncoder : Form
    {
        public XPREncoder()
        {
            InitializeComponent();
        }

        /* 
           My anti decompile is a very big shit but hard to find without source code
          
           Simply, the old bundler compiled with an old gcc try to grab the file name in the rdf file
           
           but the function "sub862951" (she read the name for put in file) don't support ASCII Invalid chars after 20 chars ??? 
           
           I don't now really how but the invalid byte are stuck in memory like BO (BufferOverflow) "(char)NRD.Next(10, 15)" 
           
           It's interesting cause the unbunder crash because the file overwrite the bytes name after 20 in the DWORD's stack

           I tried to exploit it for make a 0day exploit with a simple StackOverflow with a shellcode

           I have test a MsgBox Shellcode in a stuck structure (20 chars + StackOverflowBuffer + MsgboxShellcode + ref_return)

           And after when I make the file and I try to "unbunder" it, I was surprise to see an MessageBox say me 'RemoteCodeExecutionSALEPUTE' before the crash of unbunder ¯\_(ツ)_/¯ 

           So .... my theory work lol

           It's mine we can make an infected xpr file, an noob try to decompile the xpr file and a dna (DownloadAndExecute) shellcode drop and execute a backdoor in the computer of the kid
           
           Personally I did not test this like for spread virus to the community for 'FUN', I love the forza modders <3

           I leave the keys of this exploit in the reader's reflection of this ( ͡° ͜ʖ ͡°)

           it's still not nice but I'm proud of this 0day lul
           
           Good hax my friend
           
        */

        private void twogirlonecup()
        {
            Random NRD = new Random();
            //BinaryWriter ne = new BinaryWriter();
            TextWriter writer = new StreamWriter(Application.StartupPath + @"\scr.rdf");
            string str = File.ReadAllText(Application.StartupPath + @"\scr.org");
            ProcessStartInfo processInfo = new ProcessStartInfo();

            if (checkBox1.Checked)
            {
                writer.Write(((((str.Replace("XNAME",
                    /*
                        A simple method for detect the "protect" without forza studio crash
                    */
                    "The XPR have protected entrypoint\n      ////////////////////////////////\n      "
                    + (char)NRD.Next(10, 15)
                    + (char)NRD.Next(10, 15)
                    + (char)NRD.Next(10, 15)
                    + (char)NRD.Next(10, 15)
                    + (char)NRD.Next(10, 15)
                    + (char)NRD.Next(10, 15)
                    + (char)NRD.Next(10, 15)
                    + (char)NRD.Next(10, 15)
                    + (char)NRD.Next(10, 15)
                    + (char)NRD.Next(10, 15)
                    + (char)NRD.Next(10, 15)
                    + (char)NRD.Next(10, 15) // The flow is too busy .... :O
                    + (char)NRD.Next(10, 15)
                    + (char)NRD.Next(10, 15)
                    + (char)NRD.Next(10, 15)
                    + (char)NRD.Next(10, 15)
                    + (char)NRD.Next(10, 15)
                    + (char)NRD.Next(10, 15)
                    + (char)NRD.Next(10, 15)
                    + (char)NRD.Next(10, 15)
                    + (char)NRD.Next(10, 15)))
                    .Replace("XLOCATION", Types.Location))
                    .Replace("XW", Types.Width))
                    .Replace("XH", Types.Height)));
            }
            else
            {
                writer.Write(((((str.Replace("XNAME", Types.Name))
                    .Replace("XLOCATION", Types.Location))
                    .Replace("XW", Types.Width))
                    .Replace("XH", Types.Height)));
            }

            writer.Close();

            processInfo.WindowStyle = ProcessWindowStyle.Hidden;
            processInfo.FileName = "cmd.exe";
            processInfo.WorkingDirectory = Application.StartupPath;
            processInfo.Arguments = "/cbundler scr.rdf";

            Process.Start(processInfo);

            label4.Visible = true;
            pictureBox1.Visible = true;
            pictureBox2.Visible = true;

            timer1.Start();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            // This code can be more clear

            if (radioButton1.Checked)
            {
                Types.Height = "2048";
                Types.Width = "2048";

                twogirlonecup();
            }

            if (radioButton2.Checked)
            {
                Types.Height = "1024";
                Types.Width = "1024";

                twogirlonecup();
            }
            if (radioButton3.Checked)
            {
                Types.Height = "64";
                Types.Width = "64";

                twogirlonecup();
            }
            if (radioButton4.Checked)
            {
                Types.Height = "512";
                Types.Width = "512";

                twogirlonecup();
            }
            if (radioButton5.Checked)
            {
                Types.Height = "512";
                Types.Width = "512";

                twogirlonecup();
            }
            if (radioButton6.Checked)
            {
                Types.Height = "1024";
                Types.Width = "1024";

                twogirlonecup();
            }
            if (radioButton7.Checked)
            {
                Types.Height = "1024";
                Types.Width = "1024";

                twogirlonecup();
            }
            if (radioButton8.Checked)
            {
                Types.Height = "2048";
                Types.Width = "2048";

                twogirlonecup();
            }
            if (radioButton13.Checked)
            {
                Types.Height = textBox1.Text;
                Types.Width = textBox2.Text;

                twogirlonecup();
            }
            if (radioButton14.Checked)
            {
                Types.Height = "128";
                Types.Width = "64";

                twogirlonecup();
            }
            if (radioButton15.Checked)
            {
                Types.Height = "512";
                Types.Width = "512";

                twogirlonecup();
            }
            if (radioButton16.Checked)
            {
                Types.Height = "1024";
                Types.Width = "1024";

                twogirlonecup();
            }
            if (radioButton10.Checked)
            {
                Types.Height = "256";
                Types.Width = "256";

                twogirlonecup();
            }
            if (radioButton9.Checked)
            {
                Types.Height = "512";
                Types.Width = "512";

                twogirlonecup();
            }
            if (radioButton12.Checked)
            {
                Types.Height = "1024";
                Types.Width = "512";

                twogirlonecup();
            }
            if (radioButton11.Checked)
            {
                Types.Height = "128";
                Types.Width = "256";

                twogirlonecup();
            }
        }

        private void radioButton13_CheckedChanged(object sender, EventArgs e)
        {
            textBox1.BackColor = Color.White;
            textBox2.BackColor = Color.White;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            string scr = File.ReadAllText(Application.StartupPath + @"\scr.org");

            File.Delete(Application.StartupPath + @"\scr.rdf");

            using (StreamWriter sw = File.CreateText(Application.StartupPath + @"\scr.rdf"))
            {
                sw.Write(scr);
            }

            timer1.Stop();

            SaveFileDialog save = new SaveFileDialog();

            save.Filter = "XPR File (*.xpr)|*.xpr";
            save.FileName = Types.Height + "x" + Types.Width + ".xpr";

            if (save.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string xpatch = save.FileName;

                if (File.Exists(Types.Location))
                {
                    FileInfo old = new FileInfo(Application.StartupPath + @"\scr.xpr");
                    old.CopyTo(xpatch);
                    File.Delete(Application.StartupPath + @"\scr.xpr");
                }

            }
            else
            {
                MessageBox.Show("Error, Please retry");
                this.Close();
            }

            MessageBox.Show("Done");

            this.Close();
        }
    }
}
