using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using XDevkit;

namespace ForzaStudio
{
    public partial class ConsoleModding : Form
    {
        // WHOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOO THIS IS FUCKING SHIT 
        
        IXboxConsole client_default;

        ConsoleMethod client_0 = new ConsoleMethod();
        ConsoleFunctions client_1 = new ConsoleFunctions();

        public string FM4_TittleID = "4D530910";
        public string ID;
        public uint clicked;

        public ConsoleModding()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                if (!client_0.activeConnection)
                {
                    client_0.Connect();

                    if (FM4_TittleID == Tittle())
                    {
                        clicked++;

                        if (clicked == 1)
                        {
                            client_0.WriteFloat(0x82000EFC, (float)1000); // wong address is dynamic 
                            button1.BackColor = Color.DarkRed;            // base address + 0x5FE8
                        }
                        else if (clicked == 2)
                        {
                            clicked = 0;
                            client_0.WriteFloat(0x82000EFC, (float)1.300000);
                            button1.BackColor = Color.Black;
                        }
                    }
                    else
                    {
                        MessageBox.Show("The current game is not Forza 4");
                    }
                }

                if (client_0.activeConnection)
                {
                    if (FM4_TittleID == Tittle())
                    {
                        clicked++;

                        if (clicked == 1)
                        {
                            client_0.WriteFloat(0x82000EFC, (float)1000);
                            button1.BackColor = Color.DarkRed;
                        }
                        else if (clicked == 2)
                        {
                            clicked = 0;
                            client_0.WriteFloat(0x82000EFC, (float)1.300000);
                            button1.BackColor = Color.Black;
                        }
                    }
                    else
                    {
                        MessageBox.Show("The current game is not Forza 4");
                    }
                }
            }
            catch
            {
                MessageBox.Show("Fall to connect to console");
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            try
            {
                if (!client_0.activeConnection)
                {
                    client_0.Connect();

                    if (FM4_TittleID == Tittle())
                    {
                        clicked++;

                        if (clicked == 1)
                        {
                            client_0.WriteFloat(0x820CF794, (float)9.8066502);
                            button1.BackColor = Color.DarkRed;
                        }
                        else if (clicked == 2)
                        {
                            clicked = 0;
                            client_0.WriteFloat(0x820CF794, (float)1);
                            button1.BackColor = Color.Black;
                        }
                    }
                    else
                    {
                        MessageBox.Show("The current game is not Forza 4");
                    }
                }
                if (client_0.activeConnection)
                {
                    if (FM4_TittleID == Tittle())
                    {
                        clicked++;
                        if (clicked == 1)
                        {
                            client_0.WriteFloat(0x820CF794, (float)9.8066502);
                            button1.BackColor = Color.DarkRed;
                        }
                        else if (clicked == 2)
                        {
                            clicked = 0;
                            client_0.WriteFloat(0x820CF794, (float)1);
                            button1.BackColor = Color.Black;
                        }
                    }
                    else
                    {
                        MessageBox.Show("The current game is not Forza 4");
                    }
                }
            }
            catch
            {
                MessageBox.Show("Fall to connect to console");
            }
        }

        private string Tittle()
        {
            ID = client_0.XamGetCurrentTitleId().ToString("X");
            return ID;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            try
            {
                if (!client_0.activeConnection)
                {
                    client_0.Connect();

                    if (FM4_TittleID == Tittle())
                    {
                        client_0.WriteUInt32(0xC4E0F398, Convert.ToUInt32(numericUpDown1.Value));
                        client_0.WriteUInt32(0xC4E0F39C, Convert.ToUInt32(numericUpDown1.Value));
                        client_0.WriteUInt32(0xC6CA3A68, Convert.ToUInt32(numericUpDown1.Value));
                    }
                    else
                    {
                        MessageBox.Show("The current game is not Forza 4");
                    }
                }
                if (client_0.activeConnection)
                {
                    if (FM4_TittleID == Tittle())
                    {
                        client_0.WriteUInt32(0xC4E0F398, Convert.ToUInt32(numericUpDown1.Value));
                        client_0.WriteUInt32(0xC4E0F39C, Convert.ToUInt32(numericUpDown1.Value));
                        client_0.WriteUInt32(0xC6CA3A68, Convert.ToUInt32(numericUpDown1.Value));
                    }
                    else
                    {
                        MessageBox.Show("The current game is not Forza 4");
                    }
                }
            }
            catch
            {
                MessageBox.Show("Fall to connect to console");
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            try
            {
                if (!client_0.activeConnection)
                {
                    client_0.Connect();

                    if (FM4_TittleID == Tittle())
                    {
                        client_0.SetMemory(0x838C3A1E, Encoding.UTF8.GetBytes(textBox1.Text));
                    }
                    else
                    {
                        MessageBox.Show("The current game is not Forza 4");
                    }
                }
                if (client_0.activeConnection)
                {
                    if (FM4_TittleID == Tittle())
                    {
                        client_0.SetMemory(0x838C3A1E, Encoding.UTF8.GetBytes(textBox1.Text));
                    }
                    else
                    {
                        MessageBox.Show("The current game is not Forza 4");
                    }
                }
            }
            catch
            {
                MessageBox.Show("Fall to connect to console");
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            try
            {
                if (!client_0.activeConnection)
                {
                    client_0.Connect();

                    if (FM4_TittleID == Tittle())
                    {
                        this.textBox2.Text = Convert.ToString(client_1.GetMemory(0xC569F813, 0xB96D8));
                    }
                    else
                    {
                        MessageBox.Show("The current game is not Forza 4");
                    }
                }
                if (client_0.activeConnection)
                {
                    if (FM4_TittleID == Tittle())
                    {
                        this.textBox2.Text = Convert.ToString(client_1.GetMemory(0xC569F813, 0xB96D8));
                    }
                    else
                    {
                        MessageBox.Show("The current game is not Forza 4");
                    }
                }
            }
            catch
            {
                MessageBox.Show("Fall to connect to console");
            }
        }

        private void button6_Click(object sender, EventArgs e)
        {
            try
            {
                if (!client_0.activeConnection)
                {
                    client_0.Connect();

                    if (FM4_TittleID == Tittle())
                    {
                        client_0.SetMemory(0xC569F813, Encoding.UTF8.GetBytes(textBox2.Text));
                    }
                    else
                    {
                        MessageBox.Show("The current game is not Forza 4");
                    }
                }
                if (client_0.activeConnection)
                {
                    if (FM4_TittleID == Tittle())
                    {
                        client_0.SetMemory(0xC569F813, Encoding.UTF8.GetBytes(textBox2.Text));
                    }
                    else
                    {
                        MessageBox.Show("The current game is not Forza 4");
                    }
                }
            }
            catch
            {
                MessageBox.Show("Fall to connect to console");
            }
        }

        private void button7_Click(object sender, EventArgs e)
        {

        }
    }
}
