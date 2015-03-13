using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.IO;

namespace FH2CommunityUpdater
{

    public partial class Updater : Form
    {

        public List<ContentClass> toUpdate;
        public List<FH2File> toDownload = new List<FH2File>();
        public TorrentUser TorrentEngine;
        public List<string> torrentFiles;
        private BackgroundWorker torrentWorker;
        public bool Paused = false;
        public long count = 0;
        public double avgspeed = 0.0;
        public long speedsum = 0;
        public long initialSize = 0;
        public double lastMinute = -10000;
        public List<long> speeds = new List<long>();


        public delegate void UpdaterStatusHandler(object sender, ProgressEventArgs e);
        public event UpdaterStatusHandler OnUpdateStatus;


        public Updater( List<ContentClass> toUpdate )
        {
            InitializeComponent();
            this.toUpdate = toUpdate;

            List<TorrentTarget> torrentTargets = new List<TorrentTarget>();
            foreach (ContentClass addon in toUpdate)
            {
                TorrentTarget torrent = new TorrentTarget(addon.ID.ToString(), addon.torrent);
                torrentTargets.Add(torrent);
                addon.init();
                if (addon.obsoleteFiles.Count != 0)
                    this.toDownload.AddRange(addon.obsoleteFiles);
            }

            BackgroundWorker torrentDL = new BackgroundWorker();

            torrentDL.DoWork += new DoWorkEventHandler(
            delegate(object o, DoWorkEventArgs args)
            {
                this.torrentFiles = downloadTorrentFiles(torrentTargets);
            });

            torrentDL.RunWorkerCompleted += new RunWorkerCompletedEventHandler(
            delegate(object o, RunWorkerCompletedEventArgs args)
            {
                Console.WriteLine("FInished Downloading torrents");
                startUpdate();
                torrentDL.Dispose();
            });

            torrentDL.RunWorkerAsync();

        }

        private void SetInfo(ProgressEventArgs e)
        {
            if (this.label1.InvokeRequired)
            {
                SetTextCallback d = new SetTextCallback(SetInfo);
                this.Invoke(d, new object[] { e });
            }
            else
            {
                //Console.WriteLine("Stats Update:  DLSpeed: " + e.DownloadSpeed.ToString() + " ULSpeed: " + e.UploadSpeed.ToString() + " Progress: " + e.Progress.ToString() + "% S/L: " + e.Seeds.ToString() + "/" + e.Leeches.ToString());
                float speed = (float)e.DownloadSpeed;
                string unit = "B";
                if (speed > 1024)
                {
                    speed = (float)speed / 1024;
                    unit = "KB";
                }
                if (speed > 1024)
                {
                    speed = (float)speed / 1024;
                    unit = "MB";
                }
                speed = (int)speed;
                progressBar1.Maximum = 100;
                progressBar1.Value = (int)e.Progress;

                long remaining;
                long total = TorrentEngine.totalSize;
                if ((e.Progress > 0.000001)&&(e.DownloadSpeed == 0))
                {
                    this.initialSize = (long)((100.0 - e.Progress) * (double)total / 100.0);
                }
                remaining = (long)((100.0 - e.Progress) * (double)total / 100.0);
                
                this.speeds.Add(e.DownloadSpeed);
                if (this.speeds.Count > 30)
                    this.speeds.RemoveAt(0);

                List<double> weights = new List<double>();
                double weight = 0;
                this.speedsum = 0;
                int k = this.speeds.Count;
                foreach (long entry in this.speeds)
                {
                    this.speedsum += (long)( (double)entry / Math.Pow(k, 0));
                    weights.Add(1.0 / Math.Pow(k, 0));
                    weight += (1.0 / Math.Pow(k, 0));
                    k--;
                }

                this.avgspeed = this.speedsum / weight;

                double timeRemaining = (double)remaining / this.avgspeed;



                string timeUnit1 = "seconds";

                bool skipTime = false;
                double minutesRemaining = timeRemaining / 60.0;
                if (this.lastMinute == -10000)
                {
                    this.label2.Text = "";
                    this.lastMinute = minutesRemaining;
                    skipTime = true;
                }
                else
                {
                    if (Math.Abs((this.lastMinute - minutesRemaining)) > 1.1)
                        skipTime = true;
                }
                this.lastMinute = minutesRemaining;
                if ((minutesRemaining < 0.0)||(minutesRemaining > 1000000))
                    skipTime = true;

                string timeUnit2 = "minutes";
                double secondsRemaining = (timeRemaining - (int)minutesRemaining * 60);

                if (!this.Paused)
                {
                    this.label1.Text = "Downloading from " + e.Seeds.ToString() + " Peers at " + speed.ToString() + unit + "/s (" + ((int)e.Progress).ToString() + "% completed)";
                    if (!skipTime)
                        this.label2.Text = "Approximately " + ((int)minutesRemaining).ToString() + " " + timeUnit2 + /**" and " + ((int)secondsRemaining).ToString() + " " + timeUnit1 + **/ " remaining.";
                }
            }
        }
        delegate void SetTextCallback(ProgressEventArgs e);


        public void startUpdate()
        {
            BackgroundWorker torrentWorker = new BackgroundWorker();
            torrentWorker.WorkerReportsProgress = true;
            this.torrentWorker = torrentWorker;

            torrentWorker.DoWork += new DoWorkEventHandler(
            delegate(object o, DoWorkEventArgs args)
            {
                this.TorrentEngine.Start();                 

            });

            torrentWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(
            delegate(object o, RunWorkerCompletedEventArgs args)
            {
                Console.WriteLine("Finished doing this");
            });

            TorrentUser TEngine = new TorrentUser(this.toDownload, this.toUpdate, this.torrentFiles);
            this.TorrentEngine = TEngine;
            this.TorrentEngine.OnUpdateStatus += TorrentEngine_OnUpdateStatus;
            torrentWorker.RunWorkerAsync();
            

        }

        void TorrentEngine_OnUpdateStatus(object sender, ProgressEventArgs e)
        {
            this.SetInfo(e);

            if (OnUpdateStatus == null) return;
            OnUpdateStatus(this, e);
        }

        public class TorrentTarget
        {
            public string ID;
            public Uri source;
            public TorrentTarget( string id, Uri source )
            {
                this.ID = id;
                this.source = source;
            }
        }

        private List<string> downloadTorrentFiles( List<TorrentTarget> torrentTargets )
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "forgotten_hope", "communityupdater");
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            List<string> torrentFiles = new List<string>();

            foreach ( TorrentTarget target in torrentTargets )
            {
                var fileName = Path.Combine(path, (target.ID + ".torrent"));
                WebClient webClient = new WebClient();
                webClient.DownloadFile(target.source, fileName);
                torrentFiles.Add(fileName);
            }
            return torrentFiles;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (this.button1.Text == "Pause")
            {
                this.label1.Text = "Download Paused.";
                this.TorrentEngine.engine.PauseAll();
                this.button1.Text = "Resume";
                this.Paused = true;
            }
            else
            {
                this.TorrentEngine.engine.StartAll();
                this.button1.Text = "Pause";
                this.Paused = false;
            }
        }
           


    }
}
