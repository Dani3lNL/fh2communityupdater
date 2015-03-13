using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace FH2CommunityUpdater
{
    public partial class PWCheck : Form
    {
        public PWCheck()
        {
            InitializeComponent();
            textBox1.UseSystemPasswordChar = true;
        }

    }
}
