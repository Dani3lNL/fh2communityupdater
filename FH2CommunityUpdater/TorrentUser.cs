#region using
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
using MonoTorrent.BEncoding;
using MonoTorrent.Dht.Listeners;
using Mono.Nat;
#endregion


namespace FH2CommunityUpdater
{

    public enum EngineState
    {
        Downloading = 1,
        Seeding = -1,
        Paused = 0,
        Waiting = 2,
    }

    class TorrentUser
    {
        #region Fields

        internal EngineState engineState;
        internal MainWindow parent;
        public ClientEngine engine;
        public List<TorrentManager> managers = new List<TorrentManager>();
        public List<Torrent> torrents = new List<Torrent>();
        public long downloadSize;
        private long initialSize;
        public long totalSize;
        public DebugWindow debugWindow;
        
        private List<long> speedSample = new List<long>();
        private long lastTime = -10000;
        private string lastTimeMessage = "Estimating remaining Download Time...";
        private double lastProgress;
        private double initialProgress = -999999;
        private int activeWebSeeds = 0;

        private MonoTorrent.Dht.Listeners.DhtListener listener;
        private int waitCount = 0;
        private List<string> torrentPaths;

        #endregion

        public delegate void TorrentStatusUpdateHandler(TorrentUser sender, TorrentStatusUpdateEventArgs e);
        public event TorrentStatusUpdateHandler StatusUpdate;

        public delegate void TorrentDownloadCompletedHandler(TorrentUser sender, TorrentStatusUpdateEventArgs e);
        public event TorrentDownloadCompletedHandler TorrentDownloadCompleted;

        public delegate void SeedingStoppedHandler(TorrentUser sender, EventArgs e);
        public event SeedingStoppedHandler StoppedSeeding;


        public TorrentUser(MainWindow parent)
        {
            this.parent = parent;
            this.engineState = EngineState.Paused;
            if (parent.debugMode)
                this.debugWindow = new DebugWindow();

            // Hook into the events so you know when a router has been detected or has gone offline
            NatUtility.DeviceFound += DeviceFound;
            NatUtility.DeviceLost += DeviceLost;
            // Start searching for upnp enabled routers
            NatUtility.StartDiscovery();
            SetupEngine();
            this.engine.StatsUpdate += this.engine_StatsUpdate;
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
                settings.GlobalMaxUploadSpeed = Properties.Settings.Default.seedRate*1024;
            this.engine = new ClientEngine(settings);
            // Tell the engine to listen at port 6969 for incoming connections
            engine.ChangeListenEndpoint(new IPEndPoint(IPAddress.Any, Properties.Settings.Default.listenPort));
        }

        #region UDP-NAT
        void DeviceFound(object sender, DeviceEventArgs args)
        {
            // This is the upnp enabled router
            INatDevice device = args.Device;

            // Create a mapping to forward external port 3000 to local port 1500
            device.CreatePortMap(new Mono.Nat.Mapping(Mono.Nat.Protocol.Tcp, Properties.Settings.Default.listenPort, Properties.Settings.Default.listenPort));
        }

        private void DeviceLost (object sender, DeviceEventArgs args)
        {
            INatDevice device = args.Device;

            Console.WriteLine ("Device Lost");
            Console.WriteLine ("Type: {0}", device.GetType().Name);
        }
        #endregion

        #region DHT
        public void StartDht(ClientEngine engine, int port)
        {
            IPEndPoint listenAddress = new IPEndPoint(IPAddress.Any, port);
            this.listener = new DhtListener(listenAddress);
            DhtEngine dht = new DhtEngine(this.listener);
            engine.RegisterDht(dht);
            listener.Start();
            byte[] nodes = null;
            string path = Path.Combine(this.parent.localAppDataFolder, "nodes");
            if (File.Exists(path))
                nodes = File.ReadAllBytes(path);
            engine.DhtEngine.Start(nodes);
        }
        public void StopDht()
        {
            string path = Path.Combine(this.parent.localAppDataFolder, "nodes");
            File.WriteAllBytes(path, this.engine.DhtEngine.SaveNodes());
            this.listener.Stop();
            this.engine.DhtEngine.Stop();
        }
        #endregion

        internal void LoadTorrents(List<string> torrentPaths, List<FH2File> obsoleteFiles)
        {
            StartDht(this.engine, Properties.Settings.Default.listenPort);
            this.engineState = EngineState.Downloading;
            this.managers.Clear();
            this.torrents.Clear();
            this.totalSize = 0;
            this.downloadSize = 0;
            this.initialProgress = -999999;
            this.lastProgress = 0;
            this.lastTime = -10000;
            this.activeWebSeeds = 0;

            string fastResumeFile = Path.Combine(this.parent.localAppDataFolder, "fastresume.data");
            BEncodedDictionary fastResume;
            try
            {
                fastResume = BEncodedValue.Decode<BEncodedDictionary>(File.ReadAllBytes(fastResumeFile));
            }
            catch
            {
                fastResume = new BEncodedDictionary();
            }

            foreach ( string filePath in  torrentPaths)
            {
                Torrent torrent = null;
                try { torrent = Torrent.Load(filePath); }
                catch (Exception e) { Console.WriteLine(e); debug(e.ToString()); continue; } 
                foreach (TorrentFile file in torrent.Files)
                {
                    //file.Priority = Priority.DoNotDownload;
                    //file.Priority = Priority.Normal;
                    this.totalSize += file.Length;
                    foreach (FH2File fh2File in obsoleteFiles)
                    {
                        if ((fh2File.fullPath == Path.Combine(torrent.Name, file.FullPath))
                            ||(torrent.Name.ToLower().Contains("updater"))
                            ||((fh2File.name.ToLower().Contains("ubuntu")&&(fh2File.name.ToLower().Contains(".iso")))))
                        {
                            //file.Priority = Priority.Normal;
                            this.downloadSize += file.Length;
                            break;
                        }
                    }
                }
                this.torrents.Add(torrent);
                //TorrentSettings settings = new TorrentSettings(5, 50, 0, 0);
                TorrentSettings settings = new TorrentSettings(5, 50, 0, this.engine.Settings.GlobalMaxUploadSpeed);
                TorrentManager manager = new TorrentManager(torrent, engine.Settings.SavePath, settings);
                if (fastResume.ContainsKey(torrent.InfoHash.ToHex()))
                    manager.LoadFastResume(new FastResume((BEncodedDictionary)fastResume[torrent.InfoHash.ToHex()]));
                this.managers.Add(manager);
                manager.PeerConnected += manager_PeerConnected;
                manager.PeerDisconnected += manager_PeerDisconnected;
                this.engine.Register(manager);
                manager.TorrentStateChanged += waitForFinish;
                manager.TrackerManager.Announce();
                 // Disable rarest first and randomised picking - only allow priority based picking (i.e. selective downloading)
                //PiecePicker picker = new StandardPicker();
                //picker = new PriorityPicker(picker);
                //manager.ChangePicker(picker);
                try { manager.Start(); }
                catch (Exception e) {
                    MessageBox.Show("Could not start the torrent.\nError Message:\n" + e.Message);
                    Console.WriteLine(e); debug(e.ToString()); }
            }
        }

        void manager_PeerDisconnected(object sender, PeerConnectionEventArgs e)
        {
            if (e.PeerID.ClientApp.Client.ToString() == "WebSeed")
                this.activeWebSeeds -= 1;
        }

        private void waitForFinish(object sender, TorrentStateChangedEventArgs e)
        {
            foreach (TorrentManager manager in this.managers)
            {
                if (!manager.Complete)
                    return;
            }
            this.engineState = EngineState.Paused;
            foreach (TorrentManager manager in this.managers)
            {
                    if (manager.State == TorrentState.Stopping)
                        return;
                    else if (manager.State == TorrentState.Stopped)
                        manager.TorrentStateChanged -= waitForFinish;
                    else
                    {
                        manager.Stop();
                        return;
                    }
            }
            TorrentStatusUpdateEventArgs args = new TorrentStatusUpdateEventArgs(0, 0, 0, 0, 100, "Update Completed", "Approximately 0 seconds remaining.");
            foreach (TorrentManager manager in this.managers)
            {
                this.engine.Unregister(manager);
            }
            this.managers.Clear();
            this.torrents.Clear();
            this.activeWebSeeds = 0;
            this.engineState = EngineState.Paused;
            StopDht();
            if (parent.debugMode)
                this.refreshDebugWindow();
            UpdateFinished(args);
        }

        internal void refreshDebugWindow(bool b = false)
        {
            if (this.debugWindow.InvokeRequired)
            {
                refreshDebugWindowHandler d = new refreshDebugWindowHandler(refreshDebugWindow);
                this.debugWindow.Invoke(d, new object[] { b });
            }
            else
            {
                this.debugWindow.Hide();
                this.debugWindow.listBox1.Items.Clear();
                this.debugWindow.listBox1.Update();
            }
        }
        internal delegate void refreshDebugWindowHandler(bool b);

        internal void StopUpdate()
        {
            this.engineState = EngineState.Waiting;
            foreach (TorrentManager manager in this.managers)
            {
                manager.TorrentStateChanged += managerStopped;
            }
            this.engine.StopAll();
        }

        private void managerStopped(object sender, TorrentStateChangedEventArgs args)
        {
            foreach (TorrentManager manager in this.managers)
            {
                if (this.engineState != EngineState.Waiting)
                    return;
                if (manager.State != TorrentState.Stopped)
                    return;
            }
            if (this.engineState == EngineState.Waiting)
                this.engineState = EngineState.Paused;
            else
                return;
            foreach (TorrentManager manager in this.managers)
            {
               this.engine.Unregister(manager);
            }
            if (parent.debugMode)
                this.refreshDebugWindow();
            this.managers.Clear();
            this.torrents.Clear();
            this.lastTimeMessage = "";
        }

        internal void StopSeeding(bool human)
        {
            if (this.engine == null)
                return;
            this.engineState = EngineState.Paused;
            if ((this.StoppedSeeding != null) && (this.managers.Count == 0))
                this.StoppedSeeding(this, new EventArgs());
            foreach (TorrentManager manager in this.managers)
            {
                manager.TorrentStateChanged += manager_TorrentStateChanged;
                manager.Stop();
            }
            if (!human)
                return;
            if (parent.debugMode)
                this.refreshDebugWindow();
        }

        void manager_TorrentStateChanged(object sender, TorrentStateChangedEventArgs e)
        {
            if (e.NewState == TorrentState.Stopped)
            {
                TorrentManager manager = (TorrentManager)sender;
                this.engine.Unregister(manager);
                manager.TorrentStateChanged -= manager_TorrentStateChanged;
                if (this.managers.Contains(manager))
                    this.managers.Remove(manager);
                if (this.torrents.Contains(manager.Torrent))
                    this.torrents.Remove(manager.Torrent);
            }

            if ((this.StoppedSeeding != null)&&(this.managers.Count == 0))
                this.StoppedSeeding(this, new EventArgs());
        }

        internal void SeedTorrents(List<string> torrentPaths)
        {
            this.engineState = EngineState.Seeding;
            this.StartDht(this.engine, Properties.Settings.Default.listenPort);
            this.managers.Clear();
            this.torrents.Clear();
            this.torrentPaths = torrentPaths;

            string fastResumeFile = Path.Combine(this.parent.localAppDataFolder, "fastresume.data");
            BEncodedDictionary fastResume;
            try
            {
                fastResume = BEncodedValue.Decode<BEncodedDictionary>(File.ReadAllBytes(fastResumeFile));
            }
            catch
            {
                fastResume = new BEncodedDictionary();
            }

            foreach (string filePath in torrentPaths)
            {
                Torrent torrent = null;
                try { torrent = Torrent.Load(filePath); }
                catch (Exception e) { Console.WriteLine(e); debug(e.ToString()); continue; }
                this.torrents.Add(torrent);
                Console.WriteLine(Properties.Settings.Default.seedRate);
                TorrentSettings settings = new TorrentSettings(10, 50, 0, this.engine.Settings.GlobalMaxUploadSpeed);
                TorrentManager manager = new TorrentManager(torrent, engine.Settings.SavePath, settings);
                if (fastResume.ContainsKey(torrent.InfoHash.ToHex()))
                    manager.LoadFastResume(new FastResume((BEncodedDictionary)fastResume[torrent.InfoHash.ToHex()]));
                this.managers.Add(manager);
                this.engine.Register(manager);
                manager.PeerConnected += manager_PeerConnected;
                manager.TorrentStateChanged += checkIncomplete;
                manager.TrackerManager.Announce();
                manager.Start();
            }
            //this.engine.StartAll();
        }

        private void checkIncomplete(object sender, TorrentStateChangedEventArgs e)
        {
            if ((e.OldState == TorrentState.Hashing)&&(e.NewState == TorrentState.Downloading))
            {
                TorrentManager manager = (TorrentManager)sender;
                if (!manager.Complete)
                {
                    this.parent.button4_dummy(this, new EventArgs());
                }
            }
        }

        void manager_PeerConnected(object sender, PeerConnectionEventArgs e)
        {
            if (e.PeerID.ClientApp.Client.ToString() == "WebSeed")
                this.activeWebSeeds += 1;
            Console.WriteLine("Peer added");
            debug("Peer added. ");
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
            foreach (TorrentManager manager in this.managers)
            {
                totalProgress = totalProgress + manager.Progress;
                seeds += manager.Peers.Seeds + this.activeWebSeeds;
                leeches += manager.Peers.Leechs;
                nPeers += manager.InactivePeers;
                names += manager.Torrent.Comment;
                Console.WriteLine(manager.Monitor.DataBytesDownloaded);
                if ((manager.State == TorrentState.Downloading)&&(manager.UseWebSeeding == false))
                    manager.UseWebSeeding = true;
            }
            if ((this.engineState == EngineState.Seeding) && (leeches == 0))
            {
                if (this.waitCount != 30)
                    this.waitCount += 1;
                else 
                {
                    this.waitCount = 0;
                    this.debug("Announcing to Tracker.");
                    foreach (TorrentManager manager in this.managers)
                    {
                        manager.TrackerManager.Announce();
                    }
                    return;
                }
            }                
            if ((this.engineState == EngineState.Downloading) && (seeds == 0))
                seeds += leeches;
            string debugMessage = names + " Inactive Peers: " + nPeers.ToString()
                + " Seeds: " + seeds.ToString() + " Peers: " + leeches.ToString()
                + " UL: " + uploadSpeed.ToString() + " DL: " + downloadSpeed.ToString() +
                " Progress: " + (((totalProgress / (double)this.managers.Count) - 100 * (1 - (double)this.downloadSize / (double)this.totalSize)) * (double)this.totalSize / (double)this.downloadSize).ToString()
                +" DLSize: " + this.downloadSize.ToString() + " TotalSize: " + this.totalSize.ToString();
            Console.WriteLine(debugMessage);
            debug(debugMessage);
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
                try
                {
                    if ((Math.Abs((this.lastTime - timeRemaining)) > 89) && (timeRemaining > 60))
                        skipTime = true;
                }
                catch (OverflowException)
                {
                    return;
                }
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
            if (this.engineState != EngineState.Paused)
                UpdateStatus(args);
        }

        void TorrentUser_StoppedSeeding(TorrentUser sender, EventArgs e)
        {
            SeedTorrents(this.torrentPaths);
            this.StoppedSeeding -= TorrentUser_StoppedSeeding;
        }

        private void debug(string message)
        {
            if ((this.parent.debugMode)&&(this.debugWindow != null))
                this.debugWindow.Debug(message);
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

    #region TorrentStatusUpdateEventArgs
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
    #endregion
}
