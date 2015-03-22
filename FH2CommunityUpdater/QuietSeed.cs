using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel;
using MonoTorrent.Client;
using MonoTorrent.Client.Encryption;
using System.IO;
using MonoTorrent.Common;
using MonoTorrent.Dht;
using System.Net;


namespace FH2CommunityUpdater
{
    class QuietSeed
    {
        public MainWindow parent;
        public ContentManager contentManager;
        public TorrentUser torrentUser;
        public EngineState engineState = EngineState.Paused;

        public delegate void QuietSeedEventHandler(object sender, QuietSeedEventArgs e);
        public event QuietSeedEventHandler QuietSeedInfo;

        public delegate void QuietSeedStartedEventHandler(object sender, EventArgs e);
        public event QuietSeedStartedEventHandler WaitForFinish;

        private bool waiting = false;
        public static bool isInRestart = false;

        public QuietSeed(MainWindow parent)
        {
            this.parent = parent;
            this.torrentUser = parent.torrentUser;
        }

        public void Restart()
        {
            this.Stop();
            this.Start();
        }

        public void Start()
        {
            if (isInRestart)
            {
                this.WaitForFinish += QuietSeed_WaitForFinish;
                return;
            }
            isInRestart = true;
            this.torrentUser.engine.Dispose();
            this.torrentUser.SetupEngine();
            this.engineState = EngineState.Seeding;
            if (contentManager.getSelectedAddons().Count == 0)
            {
                QuietSeedInfo(this, new QuietSeedEventArgs("No content active...", "Idle"));
                isInRestart = false;
                return;
            }
            if (true) // (contentManager.getOutdatedAddons().Count == 0)
            {
                string infoMessage = "Checking local files.";
                QuietSeedInfo(this, new QuietSeedEventArgs(infoMessage, "Preparing to seed."));
                contentManager.MD5Completed += contentManager_MD5Completed;
                this.waiting = true;
                contentManager.findObsoleteFiles(this);
            }
        }

        void QuietSeed_WaitForFinish(object sender, EventArgs e)
        {
            this.WaitForFinish -= QuietSeed_WaitForFinish;
            Console.WriteLine("Wait your turn!");
            this.Start();
        }

        void contentManager_MD5Completed(object sender, MD5ProgressChangedEventArgs e)
        {
            contentManager.MD5Completed -= contentManager_MD5Completed;
            //if ((this.engineState == EngineState.Seeding)&&(this.waiting))
            Continue();
        }

        private void Continue()
        {
            this.waiting = false;
            QuietSeedInfo(this, new QuietSeedEventArgs("Downloading torrent files.", "Preparing to seed."));
            List<Uri[]> torrentURLs = new List<Uri[]>();
            List<ContentClass> seedThese = contentManager.getUpToDateAddons();
            if (seedThese.Count == 0)
            {
                QuietSeedInfo(this, new QuietSeedEventArgs("Nothing to seed...", "Idle"));
                this.engineState = EngineState.Paused;
                isInRestart = false;
                return;
            }
            foreach (ContentClass addon in seedThese)
            {
                Uri[] info = {addon.torrent, new Uri(
                    Path.Combine(this.parent.localAppDataFolder, addon.ID.ToString() + ".torrent"))};
                torrentURLs.Add(info);
            }
            WebClient web = new WebClient();
            int i = 0;
            web.DownloadFileCompleted += new AsyncCompletedEventHandler(
            delegate(object o, AsyncCompletedEventArgs args)
            {
                if (i == torrentURLs.Count)
                {
                    torrentDLFinished(torrentURLs);
                    web.Dispose();
                    return;
                }
                else if (i < torrentURLs.Count)
                {
                    Console.WriteLine(torrentURLs[i][0]);
                    Console.WriteLine(torrentURLs[i][1]);
                    web.DownloadFileAsync(torrentURLs[i][0], torrentURLs[i][1].OriginalString);
                    i++;
                }
            });
            if (i != 0)
                return;
            Console.WriteLine(torrentURLs[i][0]);
            Console.WriteLine(torrentURLs[i][1]);
            web.DownloadFileAsync(torrentURLs[i][0], torrentURLs[i][1].OriginalString);
            i++;
        }

        private void torrentDLFinished(List<Uri[]> torrentURLs)
        {
            int total = torrentURLs.Count;
            List<string> torrentPaths = new List<string>();
            foreach (Uri[] entry in torrentURLs)
            {
                torrentPaths.Add(entry[1].OriginalString);
            }
            this.torrentUser.SeedTorrents(torrentPaths);
            isInRestart = false;
            if (this.WaitForFinish != null)
                WaitForFinish(this, new EventArgs());

            this.torrentUser.StatusUpdate += torrentUser_StatusUpdate;
        }

        void torrentUser_StatusUpdate(TorrentUser sender, TorrentStatusUpdateEventArgs e)
        {
            if (sender.engineState != EngineState.Seeding)
                return;
            QuietSeedInfo(this, new QuietSeedEventArgs(e.infoMessage, e.notifyMessage));
        }

        public void Stop()
        {
            this.engineState = EngineState.Paused;
            this.torrentUser.StatusUpdate -= torrentUser_StatusUpdate;
            this.torrentUser.StopSeeding();
        }

    }

    public class QuietSeedEventArgs : EventArgs
    {
        public string infoMessage { get; private set; }
        public string notifyMessage { get; private set; }

        public QuietSeedEventArgs(string infoMessage, string notifyMessage)
        {
            this.infoMessage = infoMessage;
            this.notifyMessage = notifyMessage;
        }
    }

}
