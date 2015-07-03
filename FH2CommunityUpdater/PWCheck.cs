using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO;
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

       internal ProtectionManager(MainWindow parent)
       {
           this.parent = parent;
       }

       internal string getPassToSave(string hash)
       {
           return getPassSave(hash);
       }
       internal bool checkPassword(ContentClass addon, string hash, bool some)
       {
           if (!addon.protection)
               return true;
           else
               return (checkSaved(hash, addon.password));
       }

       internal bool checkPassword(ContentClass addon, string password)
       {
           if (!addon.protection)
               return true;
           else
               return (checkTyped(password, addon.password));
       }

        private string getPassSave(string hash)
        {
			//Code removed.
            return hash
        }

        private bool checkSaved(string hash, string against)
        {
			//Code removed.
            return true;
        }

        private bool checkTyped(string pass, string against)
        {
			//Code removed.
            return true;
        }
    }
}
