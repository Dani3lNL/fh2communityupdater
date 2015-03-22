using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.IO;

namespace FH2CommunityUpdater
{

    public partial class UpdateWindow : Form
    {
        public List<string> torrentFiles;
        
        internal ContentManager contentManager;
        internal TorrentUser torrentUser;

        private int noOfRuns = 0;
        public MainWindow parent;


        protected internal DialogResult ShowDialog(IWin32Window owner, bool start)
        {
            Start();
            try
            {
                return this.ShowDialog(owner);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return DialogResult.Abort;
            }
        }

        internal UpdateWindow(MainWindow parent)
        {
            InitializeComponent();
            this.parent = parent;
            this.contentManager = parent.contentManager;
            this.torrentUser = parent.torrentUser;
        }

        public void Start()
        {
            this.contentManager.MD5ProgressChanged += new ContentManager.MD5ProgessChangedEventHandler(
            delegate(object o, MD5ProgressChangedEventArgs args)
            {
                setMD5Info(args.Progress);
            });
            this.contentManager.MD5Completed += new ContentManager.MD5CompletedEventHandler(
            delegate(object o, MD5ProgressChangedEventArgs args)
            {
                setMD5Info(args.Progress);
                Continue();
            });
            this.contentManager.findObsoleteFiles(this);
        }

        private void Continue()
        {
            setProgressBar(0);
            List<FH2File> toDownload = this.contentManager.getObsoleteFiles(this);
            if (toDownload.Count == 0)
            {
                MessageBox.Show("All active content is up to date.");
                this.Visible = false;
                return;
            }
            int i = 0;
            List<Uri[]> torrentURLs = new List<Uri[]>();
            foreach (ContentClass addon in this.contentManager.getOutdatedAddons())
            {
                Uri[] info = {addon.torrent, new Uri(
                    Path.Combine(this.parent.localAppDataFolder, addon.ID.ToString() + ".torrent"))};
                torrentURLs.Add(info);
            }
            WebClient web = new WebClient();
            web.DownloadFileCompleted += new AsyncCompletedEventHandler(
            delegate(object o, AsyncCompletedEventArgs args)
            {
                if (i >= torrentURLs.Count)
                {
                    torrentDLFinished(torrentURLs);
                    web.Dispose();
                }
                else
                {
                    Console.WriteLine(torrentURLs[i][0]);
                    Console.WriteLine(torrentURLs[i][1]);
                    web.DownloadFileAsync(torrentURLs[i][0], torrentURLs[i][1].OriginalString);
                    setTorrentDLInfo(i, torrentURLs.Count);
                    i++;
                }
            });
            if (i != 0)
                return;
            Console.WriteLine(torrentURLs[i][0]);
            Console.WriteLine(torrentURLs[i][1]);
            web.DownloadFileAsync(torrentURLs[i][0], torrentURLs[i][1].OriginalString);
            setTorrentDLInfo(i, torrentURLs.Count);
            i++;
        }

        private void torrentDLFinished( List<Uri[]> torrentURLs )
        {
            setProgressBar(10000);
            int total = torrentURLs.Count;
            string info = "Downloading Torrent Files ( " + total.ToString() + " of " + total.ToString() + " completed. )";
            setInfoLabel(info);
            List<string> torrentPaths = new List<string>();
            foreach (Uri[] entry in torrentURLs)
            {
                torrentPaths.Add(entry[1].OriginalString);
            }
            this.torrentUser.LoadTorrents(torrentPaths, this.contentManager.getObsoleteFiles(this));
            this.torrentUser.StatusUpdate += torrentUser_StatusUpdate;
            this.torrentUser.TorrentDownloadCompleted += torrentUser_TorrentDownloadCompleted;
            this.button1.Enabled = true;
        }

        void torrentUser_TorrentDownloadCompleted(object sender, TorrentStatusUpdateEventArgs e)
        {
            setTorrentInfo(e);
            if (contentManager.confirmUpdated())
                MessageBox.Show("Update completed.");
            else
                MessageBox.Show("Not good.");
        }

        void torrentUser_StatusUpdate(object sender, TorrentStatusUpdateEventArgs e)
        {
            setTorrentInfo(e);
        }

        private void setTorrentInfo(TorrentStatusUpdateEventArgs e)
        {
            setProgressBar((int)(e.Progress * 100));
            setInfoLabel(e.infoMessage);
            setTimeLabel(e.timeMessage);
        }

        private void setTorrentDLInfo(int i, int total)
        {
            int progress = (int)(((double)i / (double)total)*10000);
            setProgressBar(progress);
            string info = "Downloading Torrent Files ( " + i.ToString() + " of " + total.ToString() + " completed. )";
            setInfoLabel(info);
        }

        private void setMD5Info(double progress)
        {
            setProgressBar((int)(progress * 100));
            string info = "Checking local files (" + ((int)(progress)).ToString() + "% complete.)";
            setInfoLabel(info);
        }

        private void setProgressBar(int progress)
        {
            if (this.progressBar1.InvokeRequired)
            {
                setProgressBarCallback d = new setProgressBarCallback(setProgressBar);
                this.Invoke(d, new object[] { progress });
            }
            else
            {
                this.progressBar1.Maximum = 10000;
                this.progressBar1.Value = progress;
            }
        }
        delegate void setProgressBarCallback(int progress);

        private void setInfoLabel(string text)
        {
            if (this.label1.InvokeRequired)
            {
                setInfoLabelCallback d = new setInfoLabelCallback(setInfoLabel);
                this.Invoke(d, new object[] { text });
            }
            else
            {
                this.label1.Text = text;
            }
        }
        delegate void setInfoLabelCallback(string text);

        private void setTimeLabel(string text)
        {
            if (this.label2.InvokeRequired)
            {
                setTimeLabelCallback d = new setTimeLabelCallback(setTimeLabel);
                this.Invoke(d, new object[] { text });
            }
            else
            {
                this.label2.Text = text;
            }
        }
        delegate void setTimeLabelCallback(string text);


        private void button1_Click(object sender, EventArgs e)
        {
            if (this.button1.Text == "Pause")
            {
                this.label1.Text = "Download Paused.";
                this.torrentUser.engine.PauseAll();
                this.button1.Text = "Resume";
                this.torrentUser.engineState = EngineState.Paused;
            }
            else
            {
                this.torrentUser.engine.StartAll();
                this.button1.Text = "Pause";
                this.torrentUser.engineState = EngineState.Downloading;
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {

        }

        private void button3_Click(object sender, EventArgs e)
        {
            this.Visible = false;
            this.parent.button2.Enabled = false;
            this.parent.button3.Enabled = false;
        }
           


    }
}
