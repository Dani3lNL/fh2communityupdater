using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Windows.Forms;

namespace FH2CommunityUpdater
{

    public class FH2File
    {

        public string name { get; set; }
        public string target { get; set; }
        public long size { get; set; }
        public string checksum { get; set; }
        public string fullPath { get; set; }

        public void Client( string fileName, string rootFolder )
        {
            if (File.Exists(fileName))
            {
                FileInfo file = new FileInfo(fileName);
                //MessageBox.Show(fileName);
                string folder = file.DirectoryName.ToLower();
                this.name = file.Name;
                this.size = file.Length;
                while (!(folder.StartsWith(Path.DirectorySeparatorChar + rootFolder + Path.DirectorySeparatorChar) || (folder == Path.DirectorySeparatorChar + rootFolder)))
                {
                    folder = folder.Substring(1);
                }
                folder = folder.Substring(1);
                this.target = folder;
                this.fullPath = folder + "\\" + this.name;
                this.checksum = this.getChecksum(fileName).ToLower();
            }
            else
            {
                this.size = 0;
                this.checksum = "NOTFOUND";
            }
        }

        public void Server(string name, string target, string fullPath, long size, string checksum)
        {
            this.name = name;
            this.target = target;
            this.fullPath = Path.Combine(this.target, this.name);
            this.size = size;
            this.checksum = checksum.ToLower();
        }

        public FH2File Clone()
        {
            FH2File clonedFile = new FH2File();
            clonedFile.name = this.name;
            clonedFile.target = this.target;
            clonedFile.checksum = this.checksum;
            clonedFile.fullPath = this.fullPath;
            return clonedFile;
        }

        public bool Compare (FH2File other)
        {
            if (this.name != other.name)
                return false;
            else if (this.target != other.target)
                return false;
            else if (this.size != other.size)
                return false;
            else if (this.checksum.ToLower() != other.checksum.ToLower())
                return false;
            else
                return true;
        }

        private string getChecksum(string fileName)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = new BufferedStream(File.OpenRead(fileName), 120000))
                {
                    return BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToLower();
                }
            }
        }

    }
}
