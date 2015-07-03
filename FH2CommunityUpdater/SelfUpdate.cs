using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Xml;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace FH2CommunityUpdater
{
    internal class SelfUpdate
    {

        SelfUpdateWindow window = new SelfUpdateWindow();

        internal SelfUpdate(string localVersion)
        {
            string globalVersion = "";
            string mostRecentExe = "";
            try
            {
                XmlTextReader reader = new XmlTextReader(@"http://hoststuff.forgottenhonor.com/hoststuff/fh2/CommunityUpdater/addons.xml");
                while (reader.Read())
                {
                    if ((reader.NodeType == XmlNodeType.Element) && (reader.Name == "FH2CommunityUpdater"))
                    {
                        if (reader.HasAttributes)
                        {
                            globalVersion = reader.GetAttribute("version");
                            mostRecentExe = reader.GetAttribute("url");
                        }
                    }
                }

                Console.WriteLine(localVersion);
                Console.WriteLine(globalVersion);
            }
            catch ( WebException )
            {
                MessageBox.Show("Could not connect to the server.\nA Please check your connections and/or try again later.\nProgram will shut down.");
                Environment.Exit(4);
            }
            if (new Version(localVersion).CompareTo(new Version(globalVersion)) == -1)
                prepareUpdate( mostRecentExe );
            else
            window.Dispose();
        }

        private void prepareUpdate( string URL )
        {
            string message = "A new version of FH2CommunityUpdater is available.\nDo you want to download and install it now?";
            string caption = "Program Update";
            MessageBoxButtons buttons = MessageBoxButtons.YesNo;
            DialogResult result;
            result = MessageBox.Show(message, caption, buttons);
            if (result == System.Windows.Forms.DialogResult.No)
                return;
            else
            {
                Application.UseWaitCursor = true;
                WebClient web = new WebClient();
                web.DownloadFileCompleted += web_DownloadFileCompleted;
                web.DownloadProgressChanged += web_DownloadProgressChanged;
                web.DownloadFileAsync(new Uri(URL), "FH2CommunityUpdater.update");
                Application.UseWaitCursor = false;
                if (window.ShowDialog() == DialogResult.Cancel)
                {
                    string message2 = "Are you sure you want to\ncancel the update in progress?";
                    string caption2 = "Program Update";
                    MessageBoxButtons buttons2 = MessageBoxButtons.YesNo;
                    DialogResult result2;
                    result2 = MessageBox.Show(message2, caption2, buttons2);
                    if (result2 == System.Windows.Forms.DialogResult.Yes)
                    {
                        Environment.Exit(2);
                    }
                }
            }
        }

        void web_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            SetInfo(e);
        }

        private void SetInfo(DownloadProgressChangedEventArgs e)
        {
            if (this.window.label1.InvokeRequired)
            {
                SetTextCallback d = new SetTextCallback(SetInfo);
                this.window.Invoke(d, new object[] { e });
            }
            else
            {
                double size = (double)e.TotalBytesToReceive;
                string unit = "B";
                if (size>1024)
                {
                    size /= 1024;
                    unit = "KB";
                }
                if (size>1024)
                {
                    size /= 1024;
                    unit = "MB";
                }
                string niceSize = string.Format("{0:0.###}", size);
                this.window.label1.Text = "Downloading " + niceSize + unit + " (" + e.ProgressPercentage.ToString() + "% completed.)";
                if (this.window.progressBar1.Maximum == 0)
                    this.window.progressBar1.Maximum = 100;
                this.window.progressBar1.Value = e.ProgressPercentage;
            }
        }
        delegate void SetTextCallback(DownloadProgressChangedEventArgs e);

        private void web_DownloadFileCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            runUpdate();
        }

        private void runUpdate()
        {

            string exeToRun = Path.Combine(System.IO.Path.GetTempPath(), "fh2communityupdaterselfupdate.exe");
            byte[] exeBytes = Properties.Resources.fh2communityupdaterselfupdate;
            Console.WriteLine(exeToRun);
            using (FileStream exeFile = new FileStream(exeToRun, FileMode.Create))
                exeFile.Write(exeBytes, 0, exeBytes.Length);
            var p = new Process();
            p.StartInfo.FileName = exeToRun;
            p.StartInfo.Arguments = Application.StartupPath;
            p.StartInfo.UseShellExecute = false;
            p.Start();
            Environment.Exit(2);
        }
    }
}
