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
        //internal QuietSeed quietSeed;

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
            //check that the .exe is in /mods/fh2/CommunityUpdater/
            string pathEnd = Path.Combine("mods", Path.Combine("fh2", "CommunityUpdater"));
            if (!Application.StartupPath.EndsWith(pathEnd))
            {
                string message2 = "FH2CommunityUpdater.exe should be run from\ninside the \\mods\\fh2\\CommunityUpdater folder.\nAddons will not be playable if the program is launched from the wrong location.\nAre you sure you want to start the program?";
                string caption2 = "Installation Folder";
                MessageBoxButtons buttons2 = MessageBoxButtons.YesNo;
                DialogResult result2;
                result2 = MessageBox.Show(message2, caption2, buttons2);
                if (result2 == System.Windows.Forms.DialogResult.No)
                    return false;
                else
                    return true;
            }
            //check that forgottenhope2.exe exists 3 levels up
            DirectoryInfo exePath = new DirectoryInfo(Application.StartupPath.ToString());
            var test = exePath.Parent.Parent;
            if (test != null)
            {
                FileInfo[] checkThese = exePath.Parent.Parent.Parent.GetFiles();
                foreach (FileInfo file in checkThese)
                {
                    if (file.Name.ToLower().Contains("bf2.exe"))
                        return true;
                }
            }
            string message = "BF2.exe could not be found.\nFH2CommunityUpdater might not be installed to the right location.\n(Battlefield 2\\mods\\fh2\\CommunityUpdater)\nDo you want to continue anyway?";
            string caption = "Installation Folder";
            MessageBoxButtons buttons = MessageBoxButtons.YesNo;
            DialogResult result;
            result = MessageBox.Show(message, caption, buttons);
            if (result == System.Windows.Forms.DialogResult.No)
                return false;
            else
                return true;
        }

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

                //MessageBox.Show(mostRecent.ToString() + " <- old settings are being imported to new settings -> " + UserConfig.FilePath);
                Properties.Settings.Default.Save();
                string folderName = Path.Combine(this.localAppDataFolder, mostRecent.ToString());
                if (!Directory.Exists(Path.GetDirectoryName(UserConfig.FilePath)))
                    Directory.CreateDirectory(Path.GetDirectoryName(UserConfig.FilePath));
                string fileName = Path.Combine(folderName, "user.config");
                FileInfo file = new FileInfo(fileName);
                file.CopyTo(UserConfig.FilePath, true);
                Properties.Settings.Default.Reload();
            }
            catch (Exception e)
            {
                MessageBox.Show("Failed to migrate settings.\n\nError Message: " + e.Message);
            }
            Properties.Settings.Default.firstRun = false;
            Properties.Settings.Default.Save();
        }


        public MainWindow()
        {
            protectionManager = new ProtectionManager(this);

            Application.ApplicationExit += Application_ApplicationExit;

            if (!checkRequirements())
                Environment.Exit(0);

            if (Properties.Settings.Default.firstRun)
                migrateSettings();

            InitializeComponent();
            this.listBox1.DrawItem += listBox1_DrawItem;


            setLocalAppDataFolder();
            this.label3.Text = "FH2 Community Updater v." + Application.ProductVersion;
            this.toolTip1.ShowAlways = true;
            this.toolTip1.SetToolTip(linkLabel1, @"http://www.forgottenhonor.com/modules.php?name=Forums");

            this.torrentUser = new TorrentUser(this);

            //this.quietSeed = new QuietSeed(this);

            this.torrentUser.StatusUpdate += torrentUser_StatusUpdate;
            
            BackgroundWorker infoWorker = new BackgroundWorker();
            
            infoWorker.DoWork += new DoWorkEventHandler(
            delegate(object o, DoWorkEventArgs args)
            {
                this.contentManager = new ContentManager(this, @"http://hoststuff.forgottenhonor.com/hoststuff/fh2/CommunityUpdater/addonstest.xml");
            });

            infoWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(
            delegate(object o, RunWorkerCompletedEventArgs args)
            {
                if (args.Error != null)
                    if (args.Error.GetType() == (typeof(WebException)))
                    {
                        MessageBox.Show("Could not connect to the server.\nA Please check your connections and/or try again later.\nProgram will shut down.");
                        Environment.Exit(4);
                    }
                    else if (args.Error.GetType() == (typeof(XmlException)))
                    {
                        MessageBox.Show("There was an error in the server index.\nIf this error persists, please leave a message on the forums.");
                        Environment.Exit(5);
                    }
                    else
                        throw args.Error;
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
                //this.quietSeed.contentManager = this.contentManager;
                enableUpdates();
                //this.quietSeed.QuietSeedInfo += quietSeed_QuietSeedInfo;
                //this.quietSeed.Start(false);
                if (Properties.Settings.Default.checkStart)
                {
                    this.button2_Click(null, new EventArgs());
                }
            });

            var some = unlimitedToolStripMenuItem.GetCurrentParent();
            some.ItemClicked +=some_ItemClicked;

            infoWorker.RunWorkerAsync();
        }

        void Application_ApplicationExit(object sender, EventArgs e)
        {
            try { this.notifyIcon1.Visible = false; }
            catch { }
        }

        void listBox1_DrawItem(object sender, DrawItemEventArgs e)
        {
            var names = new List<string>();
            var colors = new List<Color>();
            foreach (ContentClass addon in this.contentManager.getAvailableAddons())
            {
                names.Add(addon.name);
                colors.Add(getStateColor(addon.addonState));
            }
            e.DrawBackground();
            e.DrawFocusRectangle();
            e.Graphics.DrawString(names[e.Index],
                                  new Font(FontFamily.GenericSansSerif,
                                           (float)8.25, FontStyle.Regular),
                                  new SolidBrush(colors[e.Index]),
                                  e.Bounds);
        }

        private Color getStateColor(AddonState state)
        {
            switch (state)
            {
                case AddonState.Installed: return Color.Black;
                case AddonState.NotInstalled: return Color.Gray;
                case AddonState.NeedsRepair: return Color.Red;
                case AddonState.UpdateAvailable: return Color.Blue;
                default: return Color.Empty;
            }
        }

        void some_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            var clickedItem = ((ToolStripMenuItem)e.ClickedItem);
            if (clickedItem.Checked)
                return;
            var some = unlimitedToolStripMenuItem.GetCurrentParent();
            foreach (ToolStripMenuItem item in some.Items)
            {
                if (item == clickedItem)
                {
                    item.Checked = true;
                }
                else
                    item.Checked = false;
            }
            if (e.ClickedItem.Text == "Unlimited")
            {
                Properties.Settings.Default.limitSeed = false;
                Properties.Settings.Default.Save();
                this.updateSettings(false, false);
            }
            else
            {
                int speed = int.Parse(e.ClickedItem.Text.Replace(" KB/s", ""));
                Properties.Settings.Default.limitSeed = true;
                Properties.Settings.Default.seedRate = speed;
                Properties.Settings.Default.Save();
                this.updateSettings(false, false);
            }
        }

       /**void quietSeed_QuietSeedInfo(object sender, QuietSeedEventArgs e)
        {
            if (true)//(this.torrentUser.engineState == EngineState.Seeding)
            {
                this.notifyIcon1.Text = e.notifyMessage;
                setStatusStrip(e.infoMessage);
            }
        }**/

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
                if (args.Error != null)
                {
                    if (args.Error.GetType() == (typeof(WebException)))
                    {
                        MessageBox.Show("Could not connect to the server.\nA Please check your connections and/or try again later.\nProgram will shut down.");
                        Environment.Exit(4);
                    }
                }
                this.label6.Visible = false;
                this.label6.Enabled = false;
                this.pictureBox1.ImageLocation = Path.Combine(this.localAppDataFolder, addon.ID + addon.pictureType);
                this.pictureBox1.Visible = true;
                this.pictureBox1.Enabled = true;
                webClient.Dispose();
            });
            webClient.DownloadFileAsync(addon.pictureURL, Path.Combine(this.localAppDataFolder, addon.ID + addon.pictureType));
        }



        private void somethingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int i = 0;
            i++;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            //if (this.quietSeed != null)
             //   this.quietSeed.Stop();
            this.updateInProgress = true;
            setStatusStrip("Update in Progress...");
            this.updateWindow = new UpdateWindow(this);
            this.updateWindow.Disposed += new EventHandler(
            delegate(object o, EventArgs args)
            {
                this.updateWindow = null;
            });
            DialogResult result = this.updateWindow.ShowDialog(this, true);
            //this.setStatusStrip("Auto-Seed is not active.");
            this.setStatusStrip("");
            foreach (ContentClass addon in contentManager.getSelectedAddons())
            {
                if (result != DialogResult.Abort)
                {
                    addon.addonState = AddonState.Installed;
                    addon.addVersion();
                }
                else if (addon.addonState != AddonState.UpdateAvailable)
                    addon.addonState = AddonState.NeedsRepair;
            }
            this.refreshList();
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
            bool autoSeedChanged = false;
            var some = unlimitedToolStripMenuItem.GetCurrentParent();
            some.Enabled = false;
            if (settings.ShowDialog(this) == DialogResult.OK)
            {

                if (Properties.Settings.Default.runStartUp != settings.checkBox1.Checked)
                    startUpChanged = true;
                if (Properties.Settings.Default.autoSeed != settings.checkBox5.Checked)
                    autoSeedChanged = true;
                Properties.Settings.Default.autoSeed = settings.checkBox5.Checked;
                Properties.Settings.Default.showTray = settings.checkBox3.Checked;
                Properties.Settings.Default.hideBar = settings.checkBox2.Checked;
                Properties.Settings.Default.checkStart = settings.checkBox4.Checked;
                Properties.Settings.Default.runStartUp = settings.checkBox1.Checked;
                Properties.Settings.Default.limitSeed = settings.checkBox6.Checked;
                Properties.Settings.Default.seedRate = (int)settings.numericUpDown1.Value;
                Properties.Settings.Default.listenPort = (int)settings.numericUpDown2.Value;
                Properties.Settings.Default.Save();

                foreach (ToolStripMenuItem item in some.Items)
                {
                    if (!Properties.Settings.Default.limitSeed)
                    {
                        if (item.Text == "Unlimited")
                            item.Checked = true;
                        else
                            item.Checked = false;
                    }
                    else
                    {
                        if ((item.Text == "Unlimited")||(int.Parse(item.Text.Replace(" KB/s", "")) != Properties.Settings.Default.seedRate))
                            item.Checked = false;
                        else
                            item.Checked = true;
                    }
                }
            }
            some.Enabled = true;
            settings.Dispose();
            updateSettings(startUpChanged, autoSeedChanged);
        }
        internal void updateSettings(bool startUpChanged, bool autoSeedChanged)
        {
            if (this.torrentUser != null)
                this.torrentUser.engine.ChangeListenEndpoint(new IPEndPoint(IPAddress.Any, Properties.Settings.Default.listenPort));
            if (Properties.Settings.Default.showTray)
                this.ShowInTaskbar = !Properties.Settings.Default.hideBar;
            else
                this.ShowInTaskbar = true;

            this.notifyIcon1.Visible = Properties.Settings.Default.showTray;

            if (Properties.Settings.Default.limitSeed)
                this.torrentUser.setSeedRate(Properties.Settings.Default.seedRate*1024);
            else
                this.torrentUser.setSeedRate(int.MaxValue);
            /**if (autoSeedChanged)
            {
                if (Properties.Settings.Default.autoSeed)
                    //this.quietSeed.Start(false);
                else
                    //this.quietSeed.Stop();
            }**/
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

        public void refreshLabel()
        {
            if (this.label4.InvokeRequired)
            {
                refreshLabelCallBack d = new refreshLabelCallBack(refreshLabel);
                this.Invoke(d, new object[] { });
            }
            else
            {
                if (this.activeAddon != null)
                {
                    this.label4.Text = this.activeAddon.addonState.ToString().Replace("sR", "s R").Replace("tI", "t I").Replace("eA", "e A");
                    this.label4.ForeColor = getStateColor(this.activeAddon.addonState);
                }
            }
        }
        delegate void refreshLabelCallBack();
        private void refreshList(object sender)
        {
            if (this.listBox1.InvokeRequired)
            {
                refreshListCallBack d = new refreshListCallBack(refreshList);
                this.Invoke(d, new object[] { this });
            }
            else
            {
                this.listBox1.Refresh();
            }
        }
        delegate void refreshListCallBack( object sender );

        public void refreshList()
        {
            refreshList(this);
            refreshLabel();
        }

        private void listBox1_SelectedValueChanged(object sender, EventArgs e)
        {
            if (listBox1.SelectedItem == null)
                return;
            this.activeAddon = contentManager.getAddonByName(listBox1.Text);
            this.label3.Text = this.activeAddon.name + " v." + this.activeAddon.version;
            this.label4.Text = this.activeAddon.addonState.ToString().Replace("sR", "s R").Replace("tI", "t I").Replace("eA", "e A");
            this.label5.Text = "Current Status:";
            this.label4.ForeColor = getStateColor(this.activeAddon.addonState);
            this.richTextBox1.Text = this.activeAddon.description;
            this.toolTip1.SetToolTip(linkLabel1, this.activeAddon.contact.ToString());
            //this.checkBox1.Checked = this.activeAddon.isActive;
            if (this.activeAddon.isActive)
                this.button5.Text = "Remove";
            else
                this.button5.Text = "Install";
            if (!(File.Exists(Path.Combine(this.localAppDataFolder, this.activeAddon.ID + this.activeAddon.pictureType))))
            {
                getAddonImage(this.activeAddon);
                //this.pictureBox1.ImageLocation = Path.Combine(this.localAppDataFolder, "placeholder.png");
                this.pictureBox1.Visible = false;
                this.pictureBox1.Enabled = false;
                this.label6.Enabled = true;
                this.label6.Visible = true;
            }
            else
                this.pictureBox1.ImageLocation = Path.Combine(this.localAppDataFolder, this.activeAddon.ID + this.activeAddon.pictureType);
        }

        private void richTextBox1_ContentsResized(object sender, ContentsResizedEventArgs e)
        {
            richTextBox1.Height = e.NewRectangle.Height + 5;
        }

        private void button5_Click(object sender, EventArgs e)
        {
            if (this.activeAddon != null)
            {
                bool Checked = false;
                if (this.button5.Text == "Install")
                {
                    if (!this.activeAddon.protection)
                    {
                        string message = "Are you sure you want to install the addon \"" + this.activeAddon.name + "\"?";
                        string caption = "Confirm";
                        MessageBoxButtons buttons = MessageBoxButtons.YesNo;
                        DialogResult result;
                        result = MessageBox.Show(message, caption, buttons);
                        if (result == System.Windows.Forms.DialogResult.No)
                            return;
                        Checked = true;
                        this.button5.Text = "Remove";
                    }
                    else
                    {
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
                                return;
                            }
                            if (protectionManager.checkPassword(this.activeAddon, result))
                            {
                                stop = true;
                                string message = "Are you sure you want to install the addon \"" + this.activeAddon.name + "\"?";
                                string caption = "Confirm";
                                MessageBoxButtons buttons = MessageBoxButtons.YesNo;
                                DialogResult dresult;
                                dresult = MessageBox.Show(message, caption, buttons);
                                if (dresult == System.Windows.Forms.DialogResult.No)
                                    return;
                                Checked = true;
                                this.button5.Text = "Remove";
                            }
                            else if (!stop)
                            {
                                MessageBox.Show("Wrong password.", "Wrong Password", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                            if (stop)
                                check.Dispose();

                        }
                    }
                }
                else
                {
                    //string message = "Do you also want to remove all files belonging to the addon \"" + this.activeAddon.name + "\"?";
                    string message = "Are you sure you want to stop receiving updates for \"" + this.activeAddon.name + "\"?";
                    string caption = "Confirm";
                    //MessageBoxButtons buttons = MessageBoxButtons.YesNoCancel;
                    MessageBoxButtons buttons = MessageBoxButtons.YesNo;
                    DialogResult result;
                    result = MessageBox.Show(message, caption, buttons);
                    if (result == System.Windows.Forms.DialogResult.Yes)
                    {
                        Checked = false;
                        this.button5.Text = "Install";
                    }
                    else //if (result == System.Windows.Forms.DialogResult.Cancel)
                        return;
                    /**else
                    {
                        Checked = false;
                        this.button5.Text = "Install";
                        MessageBox.Show("Sorry, not implemented yet.");
                    }**/
                }
                contentManager.SetAddonSelected(this.activeAddon, Checked);
                if (Checked)
                {
                    this.contentManager.setOnly(this.activeAddon);
                    button3_Click(this, new EventArgs());
                    this.contentManager.resetOnly();
                }
            }
            enableUpdates();
            refreshList();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            SelfUpdate selfUpdate = new SelfUpdate(Application.ProductVersion);
            var list = this.contentManager.findOutdatedAddons();
            int count = list.Count;
            if (count == 0)
            {
                if (sender != null)
                    MessageBox.Show("All installed addons are up to date!");
            }
            else if (count == 1)
            {
                string message = "A new update for \"" + list[0].name + "\" is available.\nWould you like to install it now?";
                string caption = "New Update";
                MessageBoxButtons buttons = MessageBoxButtons.YesNo;
                DialogResult result;
                result = MessageBox.Show(message, caption, buttons);
                if (result == System.Windows.Forms.DialogResult.Yes)
                {
                    this.contentManager.setOnly(list[0]);
                    button3_Click(this, new EventArgs());
                    this.contentManager.resetOnly();
                }
                else
                    return;
            }
            else
            {
                string message = "There are multiple addon updates available.\nWould you like to install them now?";
                string caption = "New Updates";
                MessageBoxButtons buttons = MessageBoxButtons.YesNo;
                DialogResult result;
                result = MessageBox.Show(message, caption, buttons);
                if (result == System.Windows.Forms.DialogResult.Yes)
                    button3_Click(this, new EventArgs());
                else
                    return;
            }
        }

    }
}
