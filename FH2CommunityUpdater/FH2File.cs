using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Security.Cryptography;

namespace FH2CommunityUpdater
{

    public class FH2File
    {

        public string name { get; set; }
        public string target { get; set; }
        public long size { get; set; }
        public string checksum { get; set; }
        public string source { get; set; }
        public string fullPath { get; set; }

        public void Client( string fileName, string rootFolder, string webRoot )
        {
            if (File.Exists(fileName))
            {
                FileInfo file = new FileInfo(fileName);
                string folder = file.DirectoryName.ToLower();
                this.name = file.Name;
                this.size = file.Length;
                while (!folder.StartsWith(rootFolder))
                {
                    folder = folder.Substring(1);
                }
                this.target = folder;
                this.fullPath = folder + "\\" + this.name;
                this.source = webRoot + "/" + this.fullPath.Replace("\\", "/");
                this.checksum = this.getChecksum(fileName);
            }
            else
            {
                this.size = 0;
                this.checksum = "NOTFOUND";
            }
        }

        public void Server(string name, string target, string fullPath, long size, string checksum, string source)
        {
            this.name = name;
            this.target = target;
            this.fullPath = this.target + "\\" + this.name;
            this.size = size;
            this.checksum = checksum;
            this.source = source;
        }

        public bool Compare (FH2File other)
        {
            if (this.name != other.name)
                return false;
            else if (this.target != other.target)
                return false;
            else if (this.size != other.size)
                return false;
            else if (this.checksum != other.checksum)
                return false;
            else
                return true;
        }

        private string getChecksum(string fileName)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = new BufferedStream(File.OpenRead(fileName), 1200000))
                {
                    return BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToLower();
                }
            }
        }

    }
}
