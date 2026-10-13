using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace ForzaStudio
{
    public partial class XPRDecoder : Form
    {
        public XPRDecoder()
        {
            InitializeComponent();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            for (int i = 0; i <= 354; i++)
            {
                pictureBox3.Width = i;
            }
            this.Close();
        }

        private void XPRDecoder_Load(object sender, EventArgs e)
        {
            timer1.Start();
        }
    }
}
