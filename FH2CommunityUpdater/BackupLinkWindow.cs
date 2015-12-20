using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace FH2CommunityUpdater
{
    public partial class BackupLinkWindow : Form
    {
        public BackupLinkWindow()
        {
            InitializeComponent();
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            if (textBox1.Text != "")
                this.button1.Enabled = true;
        }
    }
}
