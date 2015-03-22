using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace FH2CommunityUpdater
{
    public partial class SelfUpdateWindow : Form
    {
        public SelfUpdateWindow()
        {
            InitializeComponent();
            this.progressBar1.Maximum = 0;
        }
    }
}
