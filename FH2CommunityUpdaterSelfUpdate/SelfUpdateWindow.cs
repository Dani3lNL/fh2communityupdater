using System;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;

namespace FH2CommunityUpdaterSelfUpdate
{
    public partial class SelfUpdateWindow : Form
    {
        public SelfUpdateWindow( string[] args )
        {
            InitializeComponent();
            this.progressBar1.Maximum = 100;
            this.progressBar1.Value = 100;
            this.label1.Text = "Waiting for FH2CommunityUpdater.exe to exit...";
            SelfUpdate selfUpdate = new SelfUpdate(args);
        }
    }

    class SelfUpdate
    {
        string filePath;

        internal SelfUpdate(string[] args)
        {
            this.filePath = string.Join("", args);
            Console.WriteLine(Path.Combine(this.filePath, "FH2CommunityUpdater.exe"));
            checkProcesses();
        }

        private void checkProcesses()
        {
            Process[] pname = Process.GetProcessesByName("FH2CommunityUpdater");
            if (pname.Length == 0)
            {
                Console.WriteLine("Go ahead.");
                update();
            }
            else
            {
                Console.WriteLine("Wait.");
                Process[] processes = Process.GetProcesses();
                foreach (Process p in processes)
                {
                    if (p.ProcessName.ToLower() == "fh2communityupdater")
                    {
                        p.EnableRaisingEvents = true;
                        p.Exited += p_Exited;
                    }
                }
            }
        }

        void p_Exited(object sender, EventArgs e)
        {
            checkProcesses();
        }

        void update()
        {
            int success = 0;
            if (File.Exists(Path.Combine(this.filePath, "FH2CommunityUpdater.update")))
            {
                File.Delete(Path.Combine(this.filePath, "FH2CommunityUpdater.exe"));
                FileInfo file = new FileInfo(Path.Combine(this.filePath, "FH2CommunityUpdater.update"));
                file.MoveTo(Path.Combine(this.filePath, "FH2CommunityUpdater.exe"));
                success = 1;
            }
            if (File.Exists(Path.Combine(this.filePath, "FH2CommunityUpdater.exe")))
            {
                var p = new Process();
                p.StartInfo.FileName = Path.Combine(this.filePath, "FH2CommunityUpdater.exe");
                p.StartInfo.WorkingDirectory = this.filePath;
                p.Start();
            }
            Environment.Exit(success);            
        }
    }
}