using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace FH2CommunityUpdater
{
    public partial class DebugWindow : Form
    {
        public DebugWindow()
        {
            InitializeComponent();
        }

        internal void Debug(string text)
        {
            Console.WriteLine(text);
            this.addDebugLine(text);
        }

        private void addDebugLine(string text)
        {
            if (this.listBox1.InvokeRequired)
            {
                addDebugCallback d = new addDebugCallback(addDebugLine);
                this.Invoke(d, new object[] { text });
            }
            else
            {
                //this.listBox1.Items.Add(text);
                this.listBox1.Items.Insert(0, text);
                this.listBox1.Refresh();
            }
        }
        delegate void addDebugCallback(string text);

    }



}
