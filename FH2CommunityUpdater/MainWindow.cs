using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.IO;
using System.Net;
using System.Diagnostics;
using System.Configuration;
using Microsoft.Win32;


namespace FH2CommunityUpdater
{
    public partial class MainWindow : Form
    {

        internal ContentManager contentManager;
        internal ProtectionManager protectionManager;
        internal TorrentUser torrentUser;
        internal UpdateWindow updateWindow;
        internal QuietSeed quietSeed;

        private ContentClass activeAddon;

        internal bool updateInProgress = false;
        internal string localAppDataFolder;

        private void setLocalAppDataFolder()
        {
            var userLevel = ConfigurationUserLevel.PerUserRoamingAndLocal;
            var UserConfig = ConfigurationManager.OpenExeConfiguration(userLevel);
            FileInfo settingsFile = new FileInfo(UserConfig.FilePath);
            DirectoryInfo settingsFolder = settingsFile.Directory;
            var Parent = settingsFolder.Parent;
            this.localAppDataFolder = settingsFolder.Parent.FullName;
        }

        private bool checkRequirements()
        {
            //check that only one instance of this program is running.
            Process[] processes = Process.GetProcessesByName("FH2CommunityUpdater");
            if (processes.Length > 1)
            {
                MessageBox.Show("FH2CommunityUpdater.exe is already running.");
                return false;
            }
            //check that the .exe is in /mods/fh2/bin/
            string pathEnd = Path.Combine("mods", Path.Combine("fh2", "bin"));
            if (!Application.StartupPath.EndsWith(pathEnd))
            {
                MessageBox.Show("FH2CommunityUpdater.exe can only be run from\ninside the \\mods\\fh2\\bin folder.");
                //return true;//false;
            }
            //check that forgottenhope2.exe exists 3 levels up
            DirectoryInfo exePath = new DirectoryInfo(Application.StartupPath.ToString());
            FileInfo[] checkThese = exePath.Parent.Parent.Parent.GetFiles();
            foreach (FileInfo file in checkThese)
            {
                if (file.Name.ToLower().Contains("bf2.exe"))
                    return true;
            }
            MessageBox.Show("BF2.exe could not be found. Are you sure\nFH2CommunityUpdater is installed to the right location?\n(Battlefield 2\\mods\\fh2\\bin)");
            return true; //false;
        }//Changed for Debugging!
        private void migrateSettings()
        {
            try
            {
                Version current = new Version(Application.ProductVersion);
                this.setLocalAppDataFolder();
                List<Version> versions = new List<Version>();
                foreach (DirectoryInfo folder in new DirectoryInfo(this.localAppDataFolder).GetDirectories())
                {
                    try
                    {
                        versions.Add(new Version(folder.Name));
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.ToString());
                    }
                }

                Version mostRecent = null;
                foreach (Version version in versions)
                {
                    int newerOnes = 0;
                    foreach (Version other in versions)
                    {
                        if (version.CompareTo(other) == -1)
                            newerOnes += 1;
                    }
                    if (newerOnes == 1)
                    {
                        mostRecent = version;
                        break;
                    }
                }
                if (mostRecent == null)
                    return;

                var userLevel = ConfigurationUserLevel.PerUserRoamingAndLocal;
                var UserConfig = ConfigurationManager.OpenExeConfiguration(userLevel);

                MessageBox.Show(mostRecent.ToString() + " <- old settings are being imported to new settings -> " + UserConfig.FilePath);
                string fileName = Path.Combine(
                    Path.Combine(this.localAppDataFolder, mostRecent.ToString()), "user.config");
                FileInfo file = new FileInfo(fileName);
                try
                {
                    file.CopyTo(UserConfig.FilePath, true);
                    Properties.Settings.Default.Reload();
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.ToString());
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("Failed to migrate settings.\nError Message: " + e.ToString());
            }
            Properties.Settings.Default.firstRun = false;
            Properties.Settings.Default.Save();
        }

        public MainWindow()
        {

            SelfUpdate selfUpdate = new SelfUpdate(Application.ProductVersion);

            if (!checkRequirements())
                Environment.Exit(0);

            if (Properties.Settings.Default.firstRun)
                migrateSettings();

            InitializeComponent();

            setLocalAppDataFolder();
            this.label3.Text = "FH2 Community Updater v." + Application.ProductVersion;
            this.toolTip1.ShowAlways = true;
            this.toolTip1.SetToolTip(linkLabel1, @"http://www.forgottenhonor.com/modules.php?name=Forums");

            this.protectionManager = new ProtectionManager();
            this.torrentUser = new TorrentUser(this);

            this.quietSeed = new QuietSeed(this);

            this.torrentUser.StatusUpdate += torrentUser_StatusUpdate;
            
            BackgroundWorker infoWorker = new BackgroundWorker();
            
            infoWorker.DoWork += new DoWorkEventHandler(
            delegate(object o, DoWorkEventArgs args)
            {
                this.contentManager = new ContentManager(this, @"http://hoststuff.forgottenhonor.com/hoststuff/fh2/CommunityUpdater/addons.xml");
                //contentManager = new ContentManager(this, @"http://fhpapillon.lw268.ultraseedbox.com/files/FH2CommunityUpdater/addons.xml");
            });

            infoWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(
            delegate(object o, RunWorkerCompletedEventArgs args)
            {
                this.label2.Visible = false;
                this.label2.Enabled = false;
                this.listBox1.Visible = true;
                this.listBox1.Enabled = true;

                var addons = contentManager.getAvailableAddons();
                foreach (ContentClass addon in addons)
                {
                    this.listBox1.Items.Add(addon.name);
                }
                //this.updateWindow.contentManager = this.contentManager;
                this.quietSeed.contentManager = this.contentManager;
                enableUpdates();
                this.quietSeed.QuietSeedInfo += quietSeed_QuietSeedInfo;
                this.quietSeed.Start();
            });

            infoWorker.RunWorkerAsync();
        }

        void quietSeed_QuietSeedInfo(object sender, QuietSeedEventArgs e)
        {
            if (true)//(this.torrentUser.engineState == EngineState.Seeding)
            {
                this.notifyIcon1.Text = e.notifyMessage;
                setStatusStrip(e.infoMessage);
            }
        }

        void torrentUser_StatusUpdate(object sender, TorrentStatusUpdateEventArgs e)
        {
            if (this.torrentUser.engineState == EngineState.Downloading)
            {
                this.notifyIcon1.Text = e.notifyMessage;
            }
        }

        private void enableUpdates()
        {
            if (contentManager.getSelectedAddons().Count > 0)
            {
                this.button2.Enabled = true;
                this.button3.Enabled = true;
            }
            else
            {
                this.button2.Enabled = false;
                this.button3.Enabled = false;
            }
        }

        private void getAddonImage (ContentClass addon)
        {
            WebClient webClient = new WebClient();
            webClient.DownloadFileCompleted += new AsyncCompletedEventHandler(
            delegate(object o, AsyncCompletedEventArgs args)
            {
                this.pictureBox1.ImageLocation = Path.Combine(this.localAppDataFolder, addon.ID + ".png");
                webClient.Dispose();
            });
            webClient.DownloadFileAsync(addon.pictureURL, Path.Combine(this.localAppDataFolder, addon.ID + ".png"));
        }



        private void somethingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int i = 0;
            i++;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (this.quietSeed != null)
                this.quietSeed.Stop();
            this.updateWindow = new UpdateWindow(this);
            this.updateWindow.Disposed += new EventHandler(
            delegate(object o, EventArgs args)
            {
                this.updateWindow = null;
            });
            this.updateWindow.ShowDialog(this, true);
        }

        private void setStatusStrip(string text)
        {
            if (this.statusStrip1.InvokeRequired)
            {
                setStatusStripCallback d = new setStatusStripCallback(setStatusStrip);
                this.Invoke(d, new object[] { text });
            }
            else
            {
                this.statusStrip1.Items[0].Text = text;
            }
        }
        delegate void setStatusStripCallback(string text);


        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            string link = null;
            if (this.activeAddon == null)
                link = @"http://www.forgottenhonor.com/modules.php?name=Forums";
            else
                link = this.activeAddon.contact.ToString();
            System.Diagnostics.Process.Start(link);
        }


        private void quitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (this.updateInProgress)
            {
                string message = "A Update is in progress. Are you sure you want to cancel?\nThe content being updated may become unplayable if you cancel. ";
                string caption = "Update in progress";
                MessageBoxButtons buttons = MessageBoxButtons.YesNo;
                DialogResult result;
                result = MessageBox.Show(message, caption, buttons);
                if (result == System.Windows.Forms.DialogResult.No)
                    return;
            }
            Application.Exit();
        }


        private void notifyIcon1_DoubleClick(object sender, EventArgs e)
        {
            Activate();
            this.WindowState = FormWindowState.Normal;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            SettingsWindow settings = new SettingsWindow();
            bool startUpChanged = false;
            if (settings.ShowDialog(this) == DialogResult.OK)
            {
                if (Properties.Settings.Default.runStartUp != settings.checkBox1.Checked)
                    startUpChanged = true;
                Properties.Settings.Default.autoSeed = settings.checkBox5.Checked;
                Properties.Settings.Default.showTray = settings.checkBox3.Checked;
                Properties.Settings.Default.hideBar = settings.checkBox2.Checked;
                Properties.Settings.Default.checkStart = settings.checkBox4.Checked;
                Properties.Settings.Default.runStartUp = settings.checkBox1.Checked;
                Properties.Settings.Default.limitSeed = settings.checkBox6.Checked;
                Properties.Settings.Default.seedRate = (int)settings.numericUpDown1.Value;
                Properties.Settings.Default.Save();
            }
            settings.Dispose();
            updateSettings(startUpChanged);
        }
        internal void updateSettings(bool startUpChanged)
        {
            if (Properties.Settings.Default.limitSeed)
                this.torrentUser.setSeedRate(Properties.Settings.Default.seedRate*1024);
            else
                this.torrentUser.setSeedRate(int.MaxValue);
            if (startUpChanged)
            {
                RegistryKey rkApp = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                if (Properties.Settings.Default.runStartUp)
                {
                    if ((rkApp.GetValue("FH2CommunityUpdater") == null)||(rkApp.GetValue("FH2CommunityUpdater").ToString() != Application.ExecutablePath.ToString()))
                        rkApp.SetValue("FH2CommunityUpdater", Application.ExecutablePath.ToString());
                }
                else
                {
                    if (rkApp.GetValue("FH2CommunityUpdater") != null)
                        rkApp.DeleteValue("FH2CommunityUpdater", false);
                }
            }
        }


        private void listBox1_SelectedValueChanged(object sender, EventArgs e)
        {
            if (listBox1.SelectedItem == null)
            {
                this.checkBox1.Enabled = false;
                this.checkBox1.Visible = false;
                return;
            }
            this.checkBox1.Enabled = true;
            this.checkBox1.Visible = true;

            this.activeAddon = contentManager.getAddonByName(listBox1.Text);
            this.label3.Text = this.activeAddon.name + " v." + this.activeAddon.version;
            this.richTextBox1.Text = this.activeAddon.description;
            this.toolTip1.SetToolTip(linkLabel1, this.activeAddon.contact.ToString());
            this.checkBox1.Checked = this.activeAddon.isActive;
            if (!(File.Exists(Path.Combine(this.localAppDataFolder, this.activeAddon.ID + ".png"))))
            {
                getAddonImage(this.activeAddon);
                this.pictureBox1.ImageLocation = Path.Combine(this.localAppDataFolder, "placeholder.png");
            }
            else
                this.pictureBox1.ImageLocation = Path.Combine(this.localAppDataFolder, this.activeAddon.ID + ".png");
        }

        private void richTextBox1_ContentsResized(object sender, ContentsResizedEventArgs e)
        {
            richTextBox1.Height = e.NewRectangle.Height + 5;
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (this.activeAddon != null)
                contentManager.SetAddonSelected(this.activeAddon, checkBox1.Checked);
            enableUpdates();
        }



            /**
            if (this.clbJustFired)
            {
                this.clbJustFired = false;
                var item = checkedListBox1.Items[this.clbLastIndex];
                string name = item.ToString().ToLower();
                foreach (ContentClass addon in this.availableAddons)
                {
                    if (name == addon.name.ToLower())
                    {
                        if ((this.clbNewState == CheckState.Checked) && (this.clbOldState == CheckState.Unchecked))
                        {
                            if (addon.protection)
                            {
                                checkedListBox1.SetItemCheckState(this.clbLastIndex, CheckState.Indeterminate);
                                bool stop = false;
                                while (!stop)
                                {
                                    string result = "";

                                    PWCheck check = new PWCheck();
                                    if (check.ShowDialog(this) == DialogResult.OK)
                                    {
                                        result = check.textBox1.Text;
                                    }
                                    else
                                    {
                                        result = "";
                                        stop = true;
                                        checkedListBox1.SetItemCheckState(this.clbLastIndex, CheckState.Unchecked);
                                    }
                                    if (result == addon.password)
                                    {
                                        stop = true;
                                        if (!this.selectedAddons.Contains(addon))
                                            this.selectedAddons.Add(addon);
                                        checkedListBox1.SetItemCheckState(this.clbLastIndex, CheckState.Checked);
                                    }
                                    else if (!stop)
                                    {
                                        MessageBox.Show("Wrong password was entered.", "Wrong Password", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                        checkedListBox1.SetItemCheckState(this.clbLastIndex, CheckState.Unchecked);
                                    }
                                    if (stop)
                                        check.Dispose();

                                }
                            }
                            else
                            {
                                if (!this.selectedAddons.Contains(addon))
                                {
                                    this.selectedAddons.Add(addon);
                                    Properties.Settings.Default.addons.Add(addon.ID.ToString());
                                    Properties.Settings.Default.addons.Add(addon.password);
                                    Properties.Settings.Default.Save();
                                }
                            }
                            break;
                        }
                        if ((this.clbNewState == CheckState.Unchecked) && (this.clbOldState == CheckState.Checked))
                        {
                            if (this.selectedAddons.Contains(addon))
                            {
                                this.selectedAddons.Remove(addon);
                                Properties.Settings.Default.addons.Remove(addon.ID.ToString());
                                Properties.Settings.Default.addons.Remove(addon.password);
                                Properties.Settings.Default.Save();
                            }
                        }
                    }

                }
            }
            if (checkedListBox1.CheckedItems.Count != 0)
            {
                this.button2.Enabled = true;
                this.button3.Enabled = true;
            }
            else
            {
                this.button2.Enabled = false;
                this.button3.Enabled = false;
            }
        }**/

    }
}
