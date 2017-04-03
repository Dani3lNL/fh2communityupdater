using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Windows.Forms;

namespace FH2CommunityUpdater
{
    internal partial class ErrorReport : Form
    {
        string fileName;
        string error;

        internal ErrorReport( string eString )
        {
            InitializeComponent();
            fileName = "error.log";
            if (File.Exists(fileName))
                File.Delete(fileName);
            using (FileStream fs = File.Create(fileName))
            {
                string version = Environment.OSVersion.ToString();
                string ex = eString;
                string eName = "";
                string appversion = Application.ProductVersion;
                error = appversion + " on " + version + " reported this error " + eName + "\n\nFull error:\n" + ex;
                Byte[] bE = new UTF8Encoding(true).GetBytes(error);
                fs.Write(bE, 0, bE.Length);
            }
            if (!File.Exists(fileName))
                return;
            string message = "Encountered an unhandled exception.\nPlease help improve the program by sending an error report.\nYou can check the data being submitted before sending.";
            string caption = "Error";
            MessageBoxButtons buttons = MessageBoxButtons.OK;
            DialogResult dresult;
            dresult = MessageBox.Show(message, caption, buttons);
        }
        internal ErrorReport( Exception e )
        {
            InitializeComponent();
            fileName = "error.log";
            if (File.Exists(fileName))
                File.Delete(fileName);
            using (FileStream fs = File.Create(fileName))
            {
                string version = Environment.OSVersion.ToString();
                string ex = e.ToString();
                string eName = e.Message;
                string appversion = Application.ProductVersion;
                error = appversion + " on " + version + " reported this error " + eName + "\n\nFull error:\n" + ex;
                Exception inner = e.InnerException;
                while ( inner != null)
                {
                    error += "/nInnerException:" + inner.ToString();
                    inner = inner.InnerException;
                }
                Byte[] bE = new UTF8Encoding(true).GetBytes(error);
                fs.Write(bE, 0, bE.Length);
            }
            if (!File.Exists(fileName))
                return;
            string message = "Encountered an unhandled exception.\nPlease help improve the program by sending an error report.\nYou can check the data being submitted before sending.";
            string caption = "Error";
            MessageBoxButtons buttons = MessageBoxButtons.OK;
            DialogResult dresult;
            dresult = MessageBox.Show(message, caption, buttons);
            this.richTextBox1.Text = error;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            this.button1.Enabled = false;
            this.button1.Visible = false;
            try
            {
                this.label1.Text = "Sending error report.";
                SmtpClient mailer = new SmtpClient("smtp.gmail.com");
                mailer.Port = 587;
                mailer.EnableSsl = true;
                mailer.DeliveryMethod = SmtpDeliveryMethod.Network;
                mailer.UseDefaultCredentials = false;
                mailer.Credentials = new NetworkCredential("username", "password");
                this.progressBar1.Maximum = 100;
                string upName = DateTime.Now.ToString().Replace(" ", ".");
                MailMessage mailMsg = new MailMessage(new MailAddress("address"), new MailAddress("error@cmp-gaming.com"));
                mailMsg.Subject = "Error";
                String message = "";
                using (FileStream fs = File.Open(fileName, FileMode.Open))
                {
                    byte[] b = new byte[1024];
                    UTF8Encoding temp = new UTF8Encoding(true);

                    while (fs.Read(b, 0, b.Length) > 0)
                    {
                        message += temp.GetString(b);
                    }
                }
                mailMsg.Body = message;
                mailer.Send(mailMsg);
                this.progressBar1.Value = 100;
                this.label1.Text = "Report sent.";
                this.button2.Text = "Close";
            }
            catch (Exception ee)
            {
                this.progressBar1.Value = 100;
                this.label1.Text = "Report sent failed.\n" + ee;
                this.button2.Text = "Close";
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            Application.Exit();
            Environment.Exit(0);
        }
    }
}
