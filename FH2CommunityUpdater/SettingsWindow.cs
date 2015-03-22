using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Configuration;
using System.Reflection;

namespace FH2CommunityUpdater
{
    public partial class SettingsWindow : Form
    {
        public SettingsWindow()
        {
            InitializeComponent();
            checkBox6.Checked = Properties.Settings.Default.limitSeed;
            checkBox5.Checked = Properties.Settings.Default.autoSeed;
            checkBox3.Checked = Properties.Settings.Default.showTray;
            checkBox2.Checked = Properties.Settings.Default.hideBar;
            checkBox4.Checked = Properties.Settings.Default.checkStart;
            checkBox1.Checked = Properties.Settings.Default.runStartUp;
            checkBox6.Checked = Properties.Settings.Default.limitSeed;
            numericUpDown1.Value = (decimal)Properties.Settings.Default.seedRate;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            this.Dispose();
        }

        private void checkBox6_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox6.Checked)
                numericUpDown1.Enabled = true;
            else
                numericUpDown1.Enabled = false;
        }

        private void checkBox3_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox3.Checked)
                checkBox2.Enabled = true;
            else
            {
                checkBox2.Enabled = false;
                checkBox2.Checked = false;
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            checkBox5.Checked = true;
            checkBox3.Checked = true;
            checkBox2.Checked = false;
            checkBox4.Checked = true;
            checkBox1.Checked = false;
            checkBox6.Checked = false;
            numericUpDown1.Value = 0;
        }
    }
}
