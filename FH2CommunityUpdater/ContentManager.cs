using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Net;
using System.Xml;
using System.IO;

namespace FH2CommunityUpdater
{

    class ContentManagerException : Exception
    {
        internal object Sender { get; private set;}
        internal string Description { get; private set; }

        internal ContentManagerException(object sender, string message)
        {
            this.Sender = sender;
            this.Description = message;
        }
    }

    class ContentManager
    {
        private List<ContentClass> availableAddons = new List<ContentClass>();
        private List<ContentClass> selectedAddons = new List<ContentClass>();
        private List<ContentClass> outdatedAddons = new List<ContentClass>();
        internal MainWindow Parent { get; private set; }
        private ProtectionManager protectionManager;
        private MD5Worker md5Worker;

        internal void informWorkerSize(ContentClass addon)
        {
            this.md5Worker.addSize(addon);
        }

        public ContentManager(MainWindow parent, string source)
        {
            this.Parent = parent;
            this.Initialize(source);
            this.protectionManager = parent.protectionManager;
            this.retrieveSelected();
        }

        private ContentClass ParseLine(XmlTextReader reader)
        {
            var id = int.Parse(reader.GetAttribute("id"));
            var name = reader.GetAttribute("name");
            var description = reader.GetAttribute("desc");
            var contact = new Uri(reader.GetAttribute("contact"));
            var version = reader.GetAttribute("version");
            var fileIndexURL = reader.GetAttribute("index");
            var torrent = new Uri(reader.GetAttribute("torrent"));
            var pictureURL = new Uri(reader.GetAttribute("image"));
            var password = reader.GetAttribute("password");
            var protection = false;
            if (password != null)
                protection = true;
            return new ContentClass(this, "fh2", id, torrent, name,
                version, description, contact, pictureURL, fileIndexURL,
                protection, password);

        }

        private void Initialize(string source)
        {
            XmlTextReader reader = new XmlTextReader(source);
            while (reader.Read())
            {
                if ((reader.NodeType==XmlNodeType.Element)&&(reader.Name=="addon"))
                {
                    if (reader.HasAttributes)
                    {
                        this.availableAddons.Add(ParseLine(reader));
                    }
                }
            }
            reader.Close();
        }
        
        private void retrieveSelected()
        {
            this.selectedAddons.Clear();
            List<int> selectedAddons = new List<int>();
            foreach ( string id in Properties.Settings.Default.selectedAddons )
            {
                if (!id.StartsWith("ID: "))
                    continue;
                int result = -1;
                if (!int.TryParse(id.Replace("ID: ", ""), out result))
                    continue;
                else if (result != -1)
                    selectedAddons.Add(result);
            }
            List<string> savedPass = new List<string>();
            foreach (string pass in Properties.Settings.Default.selectedAddons)
            {
                if (!pass.StartsWith("PW: "))
                    continue;
                string result = pass.Replace("PW: ", "");
                savedPass.Add(result);
            }
            if (selectedAddons.Count != savedPass.Count)
            {
                ContentManagerException e = new ContentManagerException(this, "Settings File doesn't have same amount of addon/pass.");
                throw e;
            }
            else
            {
                int i = 0;
                while (i < selectedAddons.Count)
                {
                    foreach (ContentClass addon in this.availableAddons)
                    {
                        if (addon.ID != selectedAddons[i])
                            continue;
                        if ((addon.protection)&&(!this.protectionManager.checkPassword(addon, savedPass[i])))
                            continue;
                        else
                        { 
                            SetAddonSelected(addon, true, true);
                        }

                    }
                    i++;
                }
            }
        }

        internal void SetAddonSelected(ContentClass addon, bool selected, bool ignoreSettings)
        {
            if ((selected) && (!this.selectedAddons.Contains(addon)))
            {
                this.selectedAddons.RemoveAll(x => x == addon);
                this.selectedAddons.Add(addon);
                addon.setSelected(selected, ignoreSettings);
            }
            else if ((!selected) && (this.selectedAddons.Contains(addon)))
            {
                this.selectedAddons.RemoveAll(x => x == addon);
                addon.setSelected(selected, ignoreSettings);
            }
            

        }
        internal void SetAddonSelected(ContentClass addon, bool selected)
        {
            if ((selected) && (!this.selectedAddons.Contains(addon)))
            {
                this.selectedAddons.RemoveAll(x => x == addon);
                this.selectedAddons.Add(addon);
                addon.setSelected(selected);
                if (this.getUpToDateAddons().Contains(addon))
                    this.Parent.quietSeed.Restart();
            }
            else if ((!selected) && (this.selectedAddons.Contains(addon)))
            {
                bool restart = (this.getUpToDateAddons().Contains(addon));
                this.selectedAddons.RemoveAll(x => x == addon);
                addon.setSelected(selected);
                if (restart)
                    this.Parent.quietSeed.Restart();
            }
        }
        internal void SetAddonSelected(string name, bool selected)
        {
            ContentClass addon = getAddonByName(name);
            if (addon != null)
                SetAddonSelected(addon, selected);
        }
        internal void SetAddonSelected(int id, bool selected)
        {
            ContentClass addon = getAddonByID(id);
            if (addon != null)
                SetAddonSelected(addon, selected);
        }

        internal bool GetAddonSelected(ContentClass addon)
        {
            if (addon != null)
                return addon.isActive;
            else return false;
        }
        internal bool GetAddonSelected(string name)
        {
            ContentClass addon = getAddonByName(name);
            if (addon != null)
                return addon.isActive;
            else return false;
        }
        internal bool GetAddonSelected(int id)
        {
            ContentClass addon = getAddonByID(id);
            if (addon != null)
                return addon.isActive;
            else return false;
        }

        internal List<ContentClass> getSelectedAddons()
        {
            return this.selectedAddons;
        }
        internal List<ContentClass> getOutdatedAddons()
        {
            return this.outdatedAddons;
        }
        internal List<ContentClass> getAvailableAddons()
        {
            return this.availableAddons;
        }
        internal List<ContentClass> getUpToDateAddons()
        {
            List<ContentClass> selected = new List<ContentClass>();
            selected.AddRange(this.selectedAddons);
            foreach (ContentClass addon in this.selectedAddons)
            {
                if (this.outdatedAddons.Contains(addon))
                    selected.Remove(addon);
            }
            return selected;
        }

        internal List<int> getSelectedAddonsByID()
        {
            List<int> addonIDs = new List<int>();
            foreach ( ContentClass addon in this.selectedAddons )
            {
                addonIDs.Add(addon.ID);
            }
            return addonIDs;
        }
        internal List<string> getSelectedAddonsByName()
        {
            List<string> addonNames = new List<string>();
            foreach (ContentClass addon in this.selectedAddons)
            {
                addonNames.Add(addon.name);
            }
            return addonNames;
        }

        internal ContentClass getAddonByName(string name)
        {
            ContentClass found = null;
            foreach ( ContentClass addon in this.availableAddons )
            {
                if (addon.name == name)
                {
                    if (found == null)
                        found = addon;
                    else
                    {
                        ContentManagerException e = new ContentManagerException(addon, "More then one addon of this name.");
                        throw e;
                    }
                }
            }
            return found;
        }
        internal ContentClass getAddonByID(int id)
        {
            ContentClass found = null;
            foreach (ContentClass addon in this.availableAddons)
            {
                if (addon.ID == id)
                {
                    if (found == null)
                        found = addon;
                    else
                    {
                        ContentManagerException e = new ContentManagerException(addon, "Only one addon per ID is supported.");
                        throw e;
                    }
                }
            }
            return found;
        }
        
        internal bool confirmUpdated()
        {
            foreach (ContentClass addon in this.selectedAddons)
            {
                if (!addon.InitializeLocal())
                    return false;
            }
            return true;
        }

        internal void findObsoleteFiles(object sender)
        {
            this.outdatedAddons.Clear();
            this.md5Worker = new MD5Worker(this.selectedAddons);
            this.md5Worker.DoWork += new DoWorkEventHandler(
            delegate(object o, DoWorkEventArgs args)
            {
                foreach (ContentClass addon in this.selectedAddons)
                {
                    addon.Initialize();
                }
                foreach (ContentClass addon in this.selectedAddons)
                {
                    if (!addon.InitializeLocal())
                        this.outdatedAddons.Add(addon);

                }
            });
            this.md5Worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(
            delegate(object o, RunWorkerCompletedEventArgs args)
            {
                if ((this.md5Worker != null)&&(this.MD5Completed != null))
                {
                    this.md5Worker.Dispose();
                    this.md5Worker = null;
                    this.MD5Completed(this, new MD5ProgressChangedEventArgs(1.0));
                }
            });
            this.md5Worker.RunWorkerAsync();
        }
        internal List<FH2File> getObsoleteFiles(object sender)
        {
            List<FH2File> obsoleteFiles = new List<FH2File>();
            foreach (ContentClass addon in this.selectedAddons)
            {
                if (!addon.isInitiated)
                    throw new ContentManagerException(addon, "Addon is not initiated yet.");
                else
                {
                    obsoleteFiles.AddRange(addon.obsoleteFiles);
                }
            }
            return obsoleteFiles;
        }

        internal void AddonProgress(ContentClass addon, double progress)
        {
            if ((this.md5Worker != null)&&(this.MD5ProgressChanged != null))
            {
                this.md5Worker.updateProgress(addon, progress);
                this.MD5ProgressChanged(this, new MD5ProgressChangedEventArgs(md5Worker.totalProgress));
            }
        }

        public delegate void MD5ProgessChangedEventHandler(object sender, MD5ProgressChangedEventArgs e);
        public event MD5ProgessChangedEventHandler MD5ProgressChanged;

        public delegate void MD5CompletedEventHandler(object sender, MD5ProgressChangedEventArgs e);
        public event MD5CompletedEventHandler MD5Completed;

    }

    class ContentClass
    {
        internal ContentManager parent { get; private set; }
        internal bool isInitiated { get; private set; }
        internal bool isActive { get; private set; }              
        internal string rootFolder { get; private set; }  
        internal int ID { get; private set; }
        internal Uri torrent { get; private set; }
        internal string name { get; private set; }
        internal string version { get; private set; }
        internal string description { get; private set; }
        internal Uri contact { get; private set; }
        internal Uri pictureURL { get; private set; }
        internal string fileIndexURL { get; private set; }
        internal bool protection { get; private set; }    
        internal string password { get; private set; }      
        internal long totalSize { get; private set; }
        internal List<FH2File> fileIndex = new List<FH2File>();
        internal List<FH2File> localFiles = new List<FH2File>();
        internal List<FH2File> obsoleteFiles = new List<FH2File>();

        public ContentClass(ContentManager parent, string root, int id,
            Uri torrent, string name, string version, string desc,
            Uri contact, Uri pictureURL, string fileIndexURL, bool protection, string password)
        {
            this.parent = parent;
            this.rootFolder = root;
            this.ID = id;
            this.torrent = torrent;
            this.name = name;
            this.version = version;
            this.description = desc;
            this.contact = contact;
            this.pictureURL = pictureURL;
            this.fileIndexURL = fileIndexURL;

            this.isActive = false;
            this.protection = protection;
            this.password = password;

            this.fileIndex = new List<FH2File>();
            this.localFiles = new List<FH2File>();
            this.obsoleteFiles = new List<FH2File>();
            this.isInitiated = false;
        }

        internal void setSelected(bool active)
        {
            this.isActive = active;
            if (active)
                this.addToSettings();
            else
                this.removeFromSettings();
            Properties.Settings.Default.Save();
        }
        internal void setSelected(bool active, bool ignoreSettings)
        {
            this.isActive = active;
        }

        private void addToSettings()
        {
            removeFromSettings();
            string[] addonInfo = { "ID: " + this.ID.ToString(), "PW: " + this.password };
            Properties.Settings.Default.selectedAddons.AddRange(addonInfo);
        }
        private void removeFromSettings()
        {
            if (Properties.Settings.Default.selectedAddons.Equals(null) || Properties.Settings.Default.selectedAddons.Count.Equals(0))
                return;
            int i = 0;
            while (i < Properties.Settings.Default.selectedAddons.Count)
            {
                string line = Properties.Settings.Default.selectedAddons[i];
                if (line.StartsWith("ID: "))
                {
                    int result = -1;
                    if (!int.TryParse(line.Replace("ID: ", ""), out result))
                        throw new ContentManagerException(this, "Invalid ID entry in App Settings.");
                    else if (result == this.ID)
                    {
                        Properties.Settings.Default.selectedAddons.RemoveAt(i + 1);
                        Properties.Settings.Default.selectedAddons.RemoveAt(i);

                    }

                }
                i++;
            }
        }
        private void addVersion()
        {
            removeVersion();
            string[] addonInfo = { "ID: " + this.ID.ToString(), "V: " + this.version };
            Properties.Settings.Default.addonVersions.AddRange(addonInfo);
        }
        private void removeVersion()
        {
            if (Properties.Settings.Default.addonVersions.Equals(null)||Properties.Settings.Default.addonVersions.Count.Equals(0))
                return;
            int i = 0;
            while (i < Properties.Settings.Default.addonVersions.Count)
            {
                string line = Properties.Settings.Default.addonVersions[i];
                if (line.StartsWith("ID: "))
                {
                    int result = -1;
                    if (!int.TryParse(line.Replace("ID: ", ""), out result))
                        throw new ContentManagerException(this, "Invalid ID entry in App Settings.");
                    else if (result == this.ID)
                    {
                        Properties.Settings.Default.addonVersions.RemoveAt(i + 1);
                        Properties.Settings.Default.addonVersions.RemoveAt(i);
                    }
                }
                i++;
            }
        }

        public void Initialize()
        {
            this.isInitiated = false;
            InitFileIndex();
        }

        public bool InitializeLocal()
        {
            InitLocalFiles();
            bool result = compareFiles();
            this.isInitiated = true;
            return result;
        }

        private void Report(double progress)
        {
            if (this.parent == null) return;
            this.parent.AddonProgress(this, progress);
        }

        private bool InitFileIndex()
        {
            if (this.fileIndexURL.Equals(null))
            {
                throw new ContentManagerException(this, "Addon does not have a file index specified!");
            }
            this.fileIndex.Clear();
            this.totalSize = 0;
            XmlTextReader reader = new XmlTextReader(this.fileIndexURL);
            while (reader.Read())
            {
                if ((reader.NodeType == XmlNodeType.Element) && (reader.Name == "file"))
                {
                    if (reader.HasAttributes)
                    {
                        FH2File fh2fileWeb = new FH2File();
                        fh2fileWeb.name = reader.GetAttribute("name");
                        fh2fileWeb.target = reader.GetAttribute("target");
                        fh2fileWeb.size = long.Parse(reader.GetAttribute("size"));
                        this.totalSize += fh2fileWeb.size;
                        fh2fileWeb.checksum = reader.GetAttribute("checksum").ToUpper();
                        fh2fileWeb.fullPath = Path.Combine(fh2fileWeb.target, fh2fileWeb.name);
                        this.fileIndex.Add(fh2fileWeb);
                    }
                }
            }
            this.parent.informWorkerSize(this);
            reader.Close();
            return true;
        }
        private bool InitLocalFiles()
        {
            this.localFiles.Clear();
            long dealtSize = 0;
            foreach (FH2File fh2File in this.fileIndex)
            {
                FH2File localfile = fh2File.Clone();
                string fileName = fh2File.name;
                string filePath = Path.Combine(Path.Combine(Path.Combine("..", ".."), fh2File.target), fh2File.name);
                localfile.Client(filePath, rootFolder);
                dealtSize += fh2File.size;
                double Progress = dealtSize / this.totalSize;
                localFiles.Add(localfile);
                Report(Progress);
            }
            return true;
        }
        private bool compareFiles()
        {
            this.obsoleteFiles.Clear();
            int i = 0;
            while (i < this.fileIndex.Count)
            {
                if (!(this.fileIndex[i].Compare(this.localFiles[i])))
                    this.obsoleteFiles.Add(this.fileIndex[i]);
                i++;
            }
            if (this.obsoleteFiles.Count == 0)
            {
                this.addVersion();
                return true;
            }
            else
                return false;
        }

    }

    class MD5Worker : BackgroundWorker
    {
        internal double totalProgress { get; private set; }
        private List<ContentClass> toHash;
        private double[] progressList;
        private long[] sizes;
        private long totalSize;
        public MD5Worker(List<ContentClass> toHash)
        {
            this.toHash = toHash;
            this.progressList = new double[toHash.Count];
            this.sizes = new long[toHash.Count];
        }

        internal void addSize( ContentClass sender )
        {
            int i = this.toHash.FindIndex( x => x == sender );
            this.sizes[i] = sender.totalSize;
            this.totalSize += sender.totalSize;
        }

        internal double updateProgress( ContentClass sender, double progress )
        {
            int i = this.toHash.FindIndex( x => x == sender );
            this.progressList[i] = progress;
            this.totalProgress = 0.0;
            for (int k = 0; k < this.toHash.Count; k++)
            {
                this.totalProgress += this.progressList[k] * this.sizes[k];
            }
            this.totalProgress /= this.totalSize;
            return this.totalProgress;
        }
    }

    class MD5ProgressChangedEventArgs : EventArgs
    {
        public double Progress { get; private set; }

        public MD5ProgressChangedEventArgs(double progress)
        {
            this.Progress = progress*100;
        }
    }

}
