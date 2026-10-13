using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Be.Windows.Forms;
using System.IO;

namespace ForzaStudio
{
    public partial class HexView : Form
    {

        public HexView()
        {
            InitializeComponent();
        }

        private void HexView_FormClosed(object sender, FormClosedEventArgs e)
        {
            Types.HexView = false;
        }

        private void HexView_Load(object sender, EventArgs e)
        {
            FileInfo info = new FileInfo(Types.CarbinLocation);
            DynamicByteProvider bin = new DynamicByteProvider(File.ReadAllBytes(Types.CarbinLocation)) { IsWriteByte = true };
            this.hexBox1.ByteProvider = bin;
            this.hexBox1.Refresh();
            this.Text = info.Name + " - Hex View";
        }

        private void hexBox1_TextChanged(object sender, EventArgs e)
        {

        }
    }
}
