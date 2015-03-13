using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Xml;
using System.IO;

namespace FH2CommunityUpdater
{
    public class ContentClass
    {
        internal string rootFolder = "fh2";
        internal int ID;
        internal Uri torrent;
        internal string name;
        internal string version;
        internal string description;
        internal Uri contact;
        internal Uri pictureURL;
        internal string fileIndexURL;
        internal bool protection = false;
        internal string password = null;
        internal string webRoot = "temp";
        internal bool hasDirectDL = false;
        internal List<FH2File> fileIndex = new List<FH2File>();
        internal List<FH2File> localFiles = new List<FH2File>();
        internal List<FH2File> obsoleteFiles = new List<FH2File>();


        public void init()
        {
            InitFileIndex();
            InitLocalFiles();
            compareFiles();
        }
        

        private bool InitLocalFiles()
        {
            foreach (FH2File fh2File in this.fileIndex)
            {
                FH2File localfile = clone(fh2File);
                string fileName = fh2File.name;
                string filePath = Path.Combine("..", "..", fh2File.target, fh2File.name);
                localfile.Client(filePath, rootFolder, this.webRoot);
                localFiles.Add(localfile);
            }
            return true;
        }

        private FH2File clone(FH2File fh2file)
        {
            FH2File clonedFile = new FH2File();
            clonedFile.name = fh2file.name;
            clonedFile.target = fh2file.target;
            clonedFile.checksum = fh2file.checksum;
            clonedFile.source = fh2file.source;
            clonedFile.fullPath = fh2file.fullPath;
            return clonedFile;
        }

        private bool compareFiles()
        {
            int i = 0;
            while (i < this.fileIndex.Count)
            {
                if (!(this.fileIndex[i].Compare(this.localFiles[i])))
                    this.obsoleteFiles.Add(this.fileIndex[i]);
                i++;
            }
            if (this.obsoleteFiles.Count == 0)
                return true;
            else
                return false;
        }

        private bool InitFileIndex()
        {
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
                        fh2fileWeb.source = reader.GetAttribute("source");
                        fh2fileWeb.size = long.Parse(reader.GetAttribute("size"));
                        fh2fileWeb.checksum = reader.GetAttribute("checksum").ToUpper();
                        this.fileIndex.Add(fh2fileWeb);
                    }
                }


            }
            reader.Close();
            return true;
        }

    }
}
