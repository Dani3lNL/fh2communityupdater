using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client;
using MonoTorrent.Client.Encryption;
using System.IO;
using MonoTorrent.Common;
using MonoTorrent.Dht;
using System.Net;


namespace FH2CommunityUpdater
{

    public enum EngineState
    {
        Downloading = 1,
        Seeding = -1,
        Paused = 0,
    }

    class TorrentUser
    {
        internal EngineState engineState;
        internal MainWindow parent;
        public ClientEngine engine;
        public List<TorrentManager> managers = new List<TorrentManager>();
        public List<Torrent> torrents = new List<Torrent>();
        public long downloadSize;
        private long initialSize;
        public long totalSize;
        public double progress = 0.0;
        private int downloadSpeed;
        private int uploadSpeed;
        private int seeds;
        private int leeches;
        
        private List<long> speedSample = new List<long>();
        private long lastTime = -10000;
        private string lastTimeMessage = "Estimating remaining Download Time...";

        private MonoTorrent.Dht.Listeners.DhtListener listener;
        private System.Timers.Timer timer = new System.Timers.Timer(50000);


        public delegate void TorrentStatusUpdateHandler(TorrentUser sender, TorrentStatusUpdateEventArgs e);
        public event TorrentStatusUpdateHandler StatusUpdate;

        public delegate void TorrentDownloadCompletedHandler(TorrentUser sender, TorrentStatusUpdateEventArgs e);
        public event TorrentDownloadCompletedHandler TorrentDownloadCompleted;

        public TorrentUser(MainWindow parent)
        {
            this.parent = parent;
            this.engineState = EngineState.Paused;
            SetupEngine();
        }   

        internal void setSeedRate(int newRate)
        {
            this.engine.Settings.GlobalMaxUploadSpeed = newRate;
        }

        internal void SetupEngine()
        {
            EngineSettings settings = new EngineSettings();
            settings.AllowedEncryption = EncryptionTypes.All;
            // If both encrypted and unencrypted connections are supported, an encrypted connection will be attempted
            // first if this is true. Otherwise an unencrypted connection will be attempted first.
            settings.PreferEncryption = true;
            // Torrents will be downloaded here by default when they are registered with the engine
            settings.SavePath = Path.Combine("..", "..");
            if (Properties.Settings.Default.limitSeed)
                settings.GlobalMaxUploadSpeed = Properties.Settings.Default.seedRate;
            this.engine = new ClientEngine(settings);
            // Tell the engine to listen at port 6969 for incoming connections
            engine.ChangeListenEndpoint(new IPEndPoint(IPAddress.Any, 6969));
            //StartDht(this.engine, 6969);

            this.engine.StatsUpdate += engine_StatsUpdate;
            this.timer.AutoReset = true;
            this.timer.Elapsed += timer_Elapsed;

        }

        public void StartDht(ClientEngine engine, int port)
        {

            // Send/receive DHT messages on the specified port
            IPEndPoint listenAddress = new IPEndPoint(IPAddress.Any, port);

            // Create a listener which will process incoming/outgoing dht messages
            this.listener = new MonoTorrent.Dht.Listeners.DhtListener(listenAddress);

            // Create the dht engine
            DhtEngine dht = new DhtEngine(this.listener);

            // Connect the Dht engine to the MonoTorrent engine
            engine.RegisterDht(dht);

            // Start listening for dht messages and activate the DHT engine
            listener.Start();

            // If there are existing DHT nodes stored on disk, load them
            // into the DHT engine so we can try and avoid a (very slow)
            // full bootstrap
            byte[] nodes = null;
            string path = Path.Combine(this.parent.localAppDataFolder, "nodes");
            if (File.Exists(path))
                nodes = File.ReadAllBytes(path);
            dht.Start(nodes);
        }
        public void StopDht()
        {
            string path = Path.Combine(this.parent.localAppDataFolder, "nodes");
            // Stop the listener and dht engine. This does not
            // clear internal data so the DHT can be started again
            // later without needing a full bootstrap.
            this.listener.Stop();
            this.engine.DhtEngine.Stop();

            // Save all known dht nodes to disk so they can be restored
            // later. This is *highly* recommended as it makes startup
            // much much faster.
            File.WriteAllBytes(path, this.engine.DhtEngine.SaveNodes());
        }

        internal void LoadTorrents(List<string> torrentPaths, List<FH2File> obsoleteFiles)
        {
            StartDht(this.engine, 6969);
            this.engineState = EngineState.Downloading;
            this.managers.Clear();
            this.torrents.Clear();
            var torrents = this.engine.Torrents;
            foreach (TorrentManager manager in torrents)
            {
                this.engine.Unregister(manager);
            }

            foreach ( string filePath in  torrentPaths)
            {
                Torrent torrent = null;
                try { torrent = Torrent.Load(filePath); }
                catch (Exception e) { Console.WriteLine(e); } 
                foreach (TorrentFile file in torrent.Files)
                {
                    file.Priority = Priority.DoNotDownload;
                    this.totalSize += file.Length;
                    Console.WriteLine(file.Path);
                    foreach (FH2File fh2File in obsoleteFiles)
                    {
                        Console.WriteLine(fh2File.fullPath);
                        if (true) //(fh2File.fullPath == file.FullPath)
                        {
                            file.Priority = Priority.Normal;
                            this.downloadSize += file.Length;
                            break;
                        }
                    }
                }
                this.torrents.Add(torrent);
                TorrentSettings settings = new TorrentSettings();
                TorrentManager manager = new TorrentManager(torrent, engine.Settings.SavePath, new TorrentSettings());
                this.managers.Add(manager);
                this.engine.Register(manager);

                 // Disable rarest first and randomised picking - only allow priority based picking (i.e. selective downloading)
                PiecePicker picker = new StandardPicker();
                picker = new PriorityPicker(picker);
                manager.ChangePicker(picker);
                manager.Start();
            }
        }

        internal void StopSeeding()
        {
            this.engineState = EngineState.Paused;
            this.engine.StopAll();
            //var torrents = this.engine.Torrents;
            foreach (TorrentManager manager in this.managers)
            {
                //this.engine.Unregister(manager);
            }
            this.managers.Clear();
            this.torrents.Clear();
        }

        internal void SeedTorrents(List<string> torrentPaths)
        {
            this.engineState = EngineState.Seeding;
            this.managers.Clear();
            this.torrents.Clear();
            var torrents = this.engine.Torrents;
            foreach (TorrentManager manager in torrents)
            {
                //this.engine.Unregister(manager);
            }

            foreach (string filePath in torrentPaths)
            {
                Torrent torrent = null;
                try { torrent = Torrent.Load(filePath); }
                catch (Exception e) { Console.WriteLine(e); }
                foreach (TorrentFile file in torrent.Files)
                {
                    file.Priority = Priority.Normal;
                }
                this.torrents.Add(torrent);
                TorrentSettings settings = new TorrentSettings();
                settings.InitialSeedingEnabled = true;

                TorrentManager manager = new TorrentManager(torrent, engine.Settings.SavePath, settings);
                this.managers.Add(manager);
                this.engine.Register(manager);

                manager.Start();
            }
        }

        void engine_StatsUpdate(object sender, StatsUpdateEventArgs e)
        {
            if (this.engine.TotalUploadSpeed != 0)
                Console.WriteLine(this.engine.TotalUploadSpeed);
            if (this.engineState == EngineState.Paused)
                return;
            this.uploadSpeed = this.engine.TotalUploadSpeed;
            this.downloadSpeed = this.engine.TotalDownloadSpeed;
            int seeds = 0;
            int leeches = 0;
            double totalProgress = 0.0;
            foreach ( TorrentManager manager in this.managers )
            {
                totalProgress = totalProgress + manager.Progress;
                seeds += manager.Peers.Seeds;
                leeches += manager.Peers.Leechs;
            }
            this.seeds = seeds;
            this.leeches = leeches;
            if (this.engineState == EngineState.Seeding)
            {
                string seedText;
                if ((this.leeches == 0)&&(this.uploadSpeed == 0))
                    seedText = "Waiting for peers...";
                else
                    seedText = "Seeding to " + this.leeches.ToString() + " peers at " + niceSize(this.uploadSpeed) + "/s";
                TorrentStatusUpdateEventArgs seedArgs = new TorrentStatusUpdateEventArgs(this.uploadSpeed, 0, this.leeches, 0, 0, seedText, "");
                StatusUpdate(this, seedArgs);
                return;
            }
            this.progress = (totalProgress / (double)this.managers.Count) / (this.downloadSize / this.totalSize);

            long remaining;
            if ((this.progress > 0.000001) && (this.downloadSpeed == 0))
            {
                this.initialSize = (long)((100.0 - this.progress) * (double)this.downloadSize / 100.0);
            }
            remaining = (long)((100.0 - this.progress) * (double)this.downloadSize / 100.0);

            this.speedSample.Add(this.downloadSpeed);
            if (this.speedSample.Count > 30)
                this.speedSample.RemoveAt(0);
            double weight = 0;
            long speedSum = 0;
            float fallOff = 0;
            int k = this.speedSample.Count;
            foreach (long entry in this.speedSample)
            {
                speedSum += (long)((double)entry / Math.Pow(k, fallOff));
                weight += (1.0 / Math.Pow(k, fallOff));
                k--;
            }
            long avgSpeed = (long)(speedSum / weight);
            if (avgSpeed == 0)
                avgSpeed = 1;
            long timeRemaining = remaining / avgSpeed;
            bool skipTime = false;
            if (this.lastTime == -10000)
            {
                this.lastTime = timeRemaining;
                skipTime = true;
            }
            else
            {
                if ((Math.Abs((this.lastTime - timeRemaining)) > 89) && (timeRemaining > 60))
                    skipTime = true;
            }
            this.lastTime = timeRemaining;
            if ((timeRemaining < 0) || (Math.Abs(timeRemaining) > 10000000))
                skipTime = true;

            string infoMessage = "";
            if (this.engineState == EngineState.Paused)
                infoMessage = "Download paused (" + ((int)this.progress).ToString() + "% of "
                    + niceSize(this.downloadSize) + " completed.)";
            else if ((this.downloadSpeed == 0) && ((int)this.progress != 100) && ((int)this.progress != 0))
                infoMessage = "Resuming previous update...";
            else if ((this.downloadSpeed == 0) && ((int)this.progress == 0))
                infoMessage = "Establishing Connections...";
            else
                infoMessage = "Downloading " + niceSize(this.downloadSize)
                    + " from " + this.seeds.ToString() + " seeds at " + niceSize(this.downloadSpeed)
                    + "/s (" + ((int)this.progress).ToString() + "% completed)";

            string timeMessage = "";
            if (!skipTime)
            {
                timeMessage = "Approximately " + niceTime((long)timeRemaining) + " remaining.";
                this.lastTimeMessage = timeMessage;
            }
            else
                timeMessage = this.lastTimeMessage;

            TorrentStatusUpdateEventArgs args = new TorrentStatusUpdateEventArgs(this.uploadSpeed, this.downloadSpeed, this.leeches, this.seeds, this.progress, infoMessage, timeMessage);
            if (this.progress == 100) //((this.downloadSpeed == 0) && ((int)this.progress == 100))
            {
                //this.engine.StopAll();
                //this.engineState = EngineState.Paused;
                //UpdateFinished(args);
                //StopDht();
                //StartDht(this.engine, 6969);
                this.timer.Enabled = true;

            }
            else if (this.engineState != EngineState.Paused)
                UpdateStatus(args);

            
        }

        void timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            Console.WriteLine("Resetting timer.");
            this.engine.StopAll();
            this.engine.StartAll();
        }

        private void UpdateFinished(TorrentStatusUpdateEventArgs e)
        {
            if (this.TorrentDownloadCompleted == null) return;
            TorrentDownloadCompleted(this, e);
        }
        private void UpdateStatus(TorrentStatusUpdateEventArgs e)
        {
            if (this.StatusUpdate == null) return;
            StatusUpdate(this, e);
        }

        private string sizeUnit(int i)
        {
            switch (i)
            {
                case 0: return "B";
                case 1: return "KB";
                case 2: return "MB";
                case 3: return "GB";
                case 4: return "TB";
                default: return null;
            }
        }
        private string niceSize(long size)
        {
            double dSize = (double)size;
            int i = 0;
            while ((dSize > 1024) && (sizeUnit(i + 1) != null))
            {
                dSize /= 1024;
                i++;
            }
            return string.Format("{0:0.##}", dSize) + sizeUnit(i);
        }
        private string timeUnit(int i)
        {
            switch (i)
            {
                case 0: return "seconds";
                case 1: return "minutes";
                case 2: return "hours";
                default: return null;
            }
        }
        private string niceTime(long time)
        {
            double dTime = (double)time;
            int i = 0;
            while ((dTime > 60) && (timeUnit(i + 1) != null))
            {
                dTime /= 60;
                i++;
            }
            return string.Format("{0:0}", dTime) + " " + timeUnit(i);
        }
    }

    public class TorrentStatusUpdateEventArgs : EventArgs
    {
        public int UploadSpeed { get; private set; }
        public int DownloadSpeed { get; private set; }
        public int Leeches { get; private set; }
        public int Seeds { get; private set; }
        public double Progress { get; private set; }
        public string timeMessage { get; private set; }
        public string infoMessage { get; private set; }
        public string notifyMessage { get; private set; }

        public TorrentStatusUpdateEventArgs(int uploadSpeed, int downloadSpeed, int leeches, int seeds, double progress, string infoMessage, string timeMessage)
        {
            this.UploadSpeed = uploadSpeed;
            this.DownloadSpeed = downloadSpeed;
            this.Seeds = seeds;
            this.Leeches = leeches;
            this.Progress = progress;
            this.timeMessage = timeMessage;
            this.infoMessage = infoMessage;
            if ((this.DownloadSpeed == 0) && (this.UploadSpeed == 0))
                this.notifyMessage = "Establishing Connections.";
            else if (this.DownloadSpeed < this.UploadSpeed)
                this.notifyMessage = "Seeding to " + leeches.ToString() + " peers at " + niceSize(uploadSpeed) + "/s";
            else
                this.notifyMessage = "Downloading from " + seeds.ToString() + " seeds at " + niceSize(downloadSpeed) + "/s";
        
        }
        private string sizeUnit(int i)
        {
            switch (i)
            {
                case 0: return "B";
                case 1: return "KB";
                case 2: return "MB";
                case 3: return "GB";
                case 4: return "TB";
                default: return null;
            }
        }
        private string niceSize(long size)
        {
            double dSize = (double)size;
            int i = 0;
            while ((dSize > 1024) && (sizeUnit(i + 1) != null))
            {
                dSize /= 1024;
                i++;
            }
            return string.Format("{0:0.##}", dSize) + sizeUnit(i);
        }
        
    }
}
