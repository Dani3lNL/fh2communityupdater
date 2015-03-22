using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Security.Cryptography;

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

    internal class ProtectionManager
    {
       
       internal MainWindow parent;


       internal bool checkPassword(ContentClass addon, string password)
       {
           return false;
       }

        void checkSaved(string hash)
        {

        }

        void checkTyped(string pass)
        {

        }

    }

}
