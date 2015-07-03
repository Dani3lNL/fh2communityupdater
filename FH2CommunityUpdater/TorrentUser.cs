using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
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
        //public DebugWindow debugWindow = new DebugWindow();
        
        private List<long> speedSample = new List<long>();
        private long lastTime = -10000;
        private string lastTimeMessage = "Estimating remaining Download Time...";
        private double lastProgress;
        private double initialProgress = -999999;

        private MonoTorrent.Dht.Listeners.DhtListener listener;


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
            if (this.engine != null)
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
            DirectoryInfo folder = new DirectoryInfo(Application.StartupPath);
            folder = folder.Parent.Parent;
            settings.SavePath = folder.FullName;
            if (Properties.Settings.Default.limitSeed)
                settings.GlobalMaxUploadSpeed = Properties.Settings.Default.seedRate;
            this.engine = new ClientEngine(settings);
            // Tell the engine to listen at port 6969 for incoming connections
            engine.ChangeListenEndpoint(new IPEndPoint(IPAddress.Any, Properties.Settings.Default.listenPort));
            //StartDht(this.engine, 6969);

            this.engine.StatsUpdate += engine_StatsUpdate;

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
            SetupEngine();
            //this.debugWindow.Show();
            //StartDht(this.engine, 6969);
            this.engineState = EngineState.Downloading;
            this.managers.Clear();
            this.torrents.Clear();
            this.totalSize = 0;
            this.downloadSize = 0;
            this.initialProgress = -999999;
            this.lastProgress = 0;
            this.lastTime = -10000;

            foreach ( string filePath in  torrentPaths)
            {
                Torrent torrent = null;
                try { torrent = Torrent.Load(filePath); }
                catch (Exception e) { Console.WriteLine(e); }//debugWindow.Debug(e.ToString()); } 
                foreach (TorrentFile file in torrent.Files)
                {
                    //file.Priority = Priority.DoNotDownload;
                    file.Priority = Priority.Normal;
                    this.totalSize += file.Length;
                    foreach (FH2File fh2File in obsoleteFiles)
                    {
                        if ((fh2File.fullPath == Path.Combine(torrent.Name, file.FullPath)
                            ||((fh2File.name.ToLower().Contains("ubuntu")&&(fh2File.name.ToLower().Contains(".iso"))))))
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
                //PiecePicker picker = new StandardPicker();
                //picker = new PriorityPicker(picker);
                //manager.ChangePicker(picker);
                try { manager.Start(); }
                catch (Exception e) {
                    MessageBox.Show("Could not start the torrent.\nError Message:\n" + e.Message);
                    Console.WriteLine(e); }//debugWindow.Debug(e.ToString()); }
            }
        }

        internal void StopUpdate()
        {
            this.engineState = EngineState.Paused;
            if (this.engine == null)
                return;
            this.engine.PauseAll();
            this.engine.StopAll();
            this.managers.Clear();
            this.torrents.Clear();
            this.engine.Dispose();
            this.engine = null;
            this.lastTimeMessage = "";
        }

        internal void StopSeeding()
        {
            if (this.engine == null)
                return;
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
            //this.StartDht(this.engine, 6969);
            this.managers.Clear();
            this.torrents.Clear();
            foreach (string filePath in torrentPaths)
            {
                Torrent torrent = null;
                try { torrent = Torrent.Load(filePath); }
                catch (Exception e) { Console.WriteLine(e); } //debugWindow.Debug(e.ToString()); }
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
            this.engine.StartAll();
        }

        void engine_StatsUpdate(object sender, StatsUpdateEventArgs e)
        {
            if (this.engineState == EngineState.Paused)
                return;
            long uploadSpeed = this.engine.TotalUploadSpeed;
            long downloadSpeed = this.engine.TotalDownloadSpeed;
            int seeds = 0;
            int leeches = 0;
            int nPeers = 0;
            double totalProgress = 0.0;
            string names = "Torrents: ";
            bool done = true;
            foreach ( TorrentManager manager in this.managers )
            {
                if (!manager.Complete)
                    done = false;
                totalProgress = totalProgress + manager.Progress;
                seeds += manager.Peers.Seeds;
                leeches += manager.Peers.Leechs;
                nPeers += manager.InactivePeers;
                names += manager.Torrent.Comment;
            }
            if ((this.engineState == EngineState.Downloading) && (seeds == 0))
                seeds += leeches;
            string debugMessage = names + " Inactive Peers: " + nPeers.ToString()
                + " Seeds: " + seeds.ToString() + " Peers: " + leeches.ToString()
                + " UL: " + uploadSpeed.ToString() + " DL: " + downloadSpeed.ToString() +
                " Progress: " + (((totalProgress / (double)this.managers.Count) - 100 * (1 - (double)this.downloadSize / (double)this.totalSize)) * (double)this.totalSize / (double)this.downloadSize).ToString()
                +" DLSize: " + this.downloadSize.ToString() + " TotalSize: " + this.totalSize.ToString();
            //debugWindow.Debug(debugMessage);
            Console.WriteLine(debugMessage);
            if (this.engineState == EngineState.Seeding)
            {
                string seedText;
                if ((leeches == 0)&&(uploadSpeed == 0))
                    seedText = "Waiting for peers...";
                else
                    seedText = "Seeding to " + leeches.ToString() + " peers at " + niceSize(uploadSpeed) + "/s";
                TorrentStatusUpdateEventArgs seedArgs = new TorrentStatusUpdateEventArgs((int)uploadSpeed, 0, leeches, 0, 0, seedText, "");
                StatusUpdate(this, seedArgs);
                return;
            }
            double progress = (totalProgress / (double)this.managers.Count);
            progress = (progress - 100 * (1 - (double)this.downloadSize / (double)this.totalSize)) * (double)this.totalSize / (double)this.downloadSize;
            bool stillHashing = false;
            string hashMessage = "";
            if (progress < 0)
            {
                stillHashing = true;
                if (this.initialProgress == -999999)
                    this.initialProgress = progress;
                var hashProgress = 1 - progress / this.initialProgress;
                hashMessage = "Hashing existing pieces (" + String.Format("{0:0.#}",(hashProgress * 100)) + "%)";
                progress = 0;
            }

            long remaining;
            if ((progress > 0.000001) && (downloadSpeed == 0))
            {
                this.initialSize = (long)((100.0 - progress) * (double)this.downloadSize / 100.0);
            }
            remaining = (long)((100.0 - progress) * (double)this.downloadSize / 100.0);
            progress = Math.Round(progress, 6);

            this.speedSample.Add(downloadSpeed);
            if (this.speedSample.Count > 30)
                this.speedSample.RemoveAt(0);
            double weight = 0;
            long speedSum = 0;
            float fallOff = 0;
            int k = this.speedSample.Count;
            List<long> speeds = new List<long>(this.speedSample);
            foreach (long entry in speeds)
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
            string plural = "";
            if (seeds > 1)
                plural = "s";
            this.lastTime = timeRemaining;
            if ((timeRemaining < 0) || (Math.Abs(timeRemaining) > 100000))
                skipTime = true;

            string infoMessage = "";
            if (this.engineState == EngineState.Paused)
                infoMessage = "Download paused (" + ((int)progress).ToString() + "% of "
                    + niceSize(this.downloadSize) + " completed.)";
            else if ((downloadSpeed == 0) && ((int)progress != 100) && ((int)progress != 0))
            {
                if ((progress == this.lastProgress) && (progress > 0))
                    infoMessage = "Waiting for seeds...";
                else
                    infoMessage = "Resuming previous update...";
                this.lastProgress = progress;
            }
            else if ((downloadSpeed == 0) && ((int)progress == 0))
                if (stillHashing)
                    infoMessage = hashMessage;
                else
                    infoMessage = "Establishing Connections...";
            else
                infoMessage = "Downloading " + niceSize(this.downloadSize)
                    + " from " + seeds.ToString() + " seed" + plural + " at " + niceSize(downloadSpeed)
                    + "/s (" + ((int)progress).ToString() + "% completed)";
            string timeMessage = "";
            if (!skipTime)
            {
                timeMessage = "Approximately " + niceTime((long)timeRemaining) + " remaining.";
                this.lastTimeMessage = timeMessage;
            }
            else
                timeMessage = this.lastTimeMessage;

            TorrentStatusUpdateEventArgs args = new TorrentStatusUpdateEventArgs((int)uploadSpeed, (int)downloadSpeed, leeches, seeds, progress, infoMessage, timeMessage);
            if ((progress == 100)||(done))
            {
                this.engine.StopAll();
                this.engineState = EngineState.Paused;
                //StopDht();
                UpdateFinished(args);
            }
            else if (this.engineState != EngineState.Paused)
                UpdateStatus(args);
        }

        private void UpdateFinished(TorrentStatusUpdateEventArgs e)
        {
            this.lastTimeMessage = "";
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
                case 0: return "second";
                case 1: return "minute";
                case 2: return "hour";
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
            string unit = timeUnit(i);
            var sTime = string.Format("{0:0}", dTime);
            if (int.Parse(sTime) > 1)
                unit += "s";
            return sTime + " " + unit;
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
                if (progress < 0)
                    this.notifyMessage = "Hashing existing Files.";
                else
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
