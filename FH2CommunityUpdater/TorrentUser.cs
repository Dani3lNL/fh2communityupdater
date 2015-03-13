using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MonoTorrent.Client;
using MonoTorrent.Client.Encryption;
using System.IO;
using MonoTorrent.Common;
using System.Net;


namespace FH2CommunityUpdater
{
    public class TorrentUser
    {
        public ClientEngine engine;
        public List<FH2File> neededFiles = new List<FH2File>();
        public List<TorrentManager> managers = new List<TorrentManager>();
        public List<string> torrentPathList = new List<string>();
        public List<Torrent> torrents = new List<Torrent>();
        public string rootFolder;
        public int maxSeedRate;
        public long totalReceived;
        public long totalSize;
        public double progress = 0.0;
        public int downloadSpeed;
        public int uploadSpeed;
        public int seeds;
        public int leeches;

        public delegate void StatusUpdateHandler(object sender, ProgressEventArgs e);
        public event StatusUpdateHandler OnUpdateStatus;

        //public TorrentUser( List<string> getTheseTorrents, string rootFolder, int maxSeedRate = 50*1024 )
        public TorrentUser( List<FH2File> toDownload, List<ContentClass> addonsToUpdate, List<string> torrentPaths )
        {
            
            SetupEngine();
            this.rootFolder = addonsToUpdate[0].rootFolder;
            //this.maxSeedRate = maxSeedRate;
            this.torrentPathList = torrentPaths;
            LoadTorrents();
        }   

        void SetupEngine()
        {
            EngineSettings settings = new EngineSettings();
            settings.AllowedEncryption = EncryptionTypes.All;
            // If both encrypted and unencrypted connections are supported, an encrypted connection will be attempted
            // first if this is true. Otherwise an unencrypted connection will be attempted first.
            settings.PreferEncryption = true;
            // Torrents will be downloaded here by default when they are registered with the engine
            settings.SavePath = this.rootFolder;
            settings.GlobalMaxUploadSpeed = this.maxSeedRate;
            this.engine = new ClientEngine(settings);
            // Tell the engine to listen at port 6969 for incoming connections
            //engine.ChangeListenEndpoint(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 6969));

            this.engine.StatsUpdate += engine_StatsUpdate;

        }

        void LoadTorrents()
        {
            this.managers = new List<TorrentManager>();
            this.torrents = new List<Torrent>();

            foreach ( string filePath in this.torrentPathList )
            {
                Torrent torrent = Torrent.Load(filePath);
                foreach ( TorrentFile file in torrent.Files )
                {
                    if  (false) 
                        file.Priority = Priority.DoNotDownload;
                    else
                    {
                        file.Priority = Priority.Normal;
                        this.totalSize += file.Length;
                    }
                        
                    Console.WriteLine(file.Path);
                    foreach ( FH2File fh2File in this.neededFiles )
                    {
                        Console.WriteLine(fh2File.fullPath);
                        if (fh2File.fullPath == file.FullPath)
                        if (true)
                        {
                            file.Priority = Priority.Normal;
                            this.totalSize += file.Length;
                            break;
                        }
                    }
                }
                
                
                this.torrents.Add(torrent);
                
                TorrentManager manager = new TorrentManager(torrent, this.rootFolder, new TorrentSettings());
                this.managers.Add(manager);
                this.engine.Register(manager);

                 // Disable rarest first and randomised picking - only allow priority based picking (i.e. selective downloading)
                //PiecePicker picker = new StandardPicker();
                //picker = new PriorityPicker(picker);
                //manager.ChangePicker(picker);
                manager.Start();

            }

        }

        public void Start()
        {
            //engine.StartAll();

        }

        public void Pause()
        {
            engine.PauseAll();
        }

        void engine_StatsUpdate(object sender, StatsUpdateEventArgs e)
        {
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
            this.progress = totalProgress / (double)this.managers.Count;
            this.seeds = seeds;
            this.leeches = leeches;

            UpdateStatus(this.uploadSpeed, this.downloadSpeed, this.leeches, this.seeds, this.progress);
        }

        private void UpdateStatus(int uploadSpeed, int downloadSpeed, int leeches, int seeds, double progress)
        {
            // Make sure someone is listening to event
            if (OnUpdateStatus == null) return;

            ProgressEventArgs args = new ProgressEventArgs(uploadSpeed, downloadSpeed, leeches, seeds, progress);
            OnUpdateStatus(this, args);
        }


    }

    public class ProgressEventArgs : EventArgs
    {
        public int UploadSpeed { get; private set; }
        public int DownloadSpeed { get; private set; }
        public int Leeches { get; private set; }
        public int Seeds { get; private set; }
        public double Progress { get; private set; }

        public ProgressEventArgs(int uploadSpeed, int downloadSpeed, int leeches, int seeds, double progress)
        {
            this.UploadSpeed = uploadSpeed;
            this.DownloadSpeed = downloadSpeed;
            this.Seeds = seeds;
            this.Leeches = leeches;
            this.Progress = progress;
        }
    }
}
