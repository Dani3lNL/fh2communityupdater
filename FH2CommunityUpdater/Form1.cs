using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.IO;
using System.Net;


namespace FH2CommunityUpdater
{
    public partial class MainWindow : Form
    {

        private List<ContentClass> availableAddons = new List<ContentClass>();
        private List<ContentClass> selectedAddons = new List<ContentClass>();
        private ContentClass activeAddon = new ContentClass();
        private bool updateInProgress = false;
        ToolTip toolTip1 = new ToolTip();
        private bool clbJustFired = false;
        private int clbLastIndex;
        private CheckState clbOldState;
        private CheckState clbNewState;
        private Updater updater;


        public MainWindow()
        {
            InitializeComponent();
            activeAddon.contact = new Uri(@"http://www.forgottenhonor.com/modules.php?name=Forums");
            BackgroundWorker infoWorker = new BackgroundWorker();
            infoWorker.DoWork += new DoWorkEventHandler(
            delegate(object o, DoWorkEventArgs args)
            {
                initContentInfo(@"http://hoststuff.forgottenhonor.com/hoststuff/fh2/CommunityUpdater/addons.xml");
            });
            infoWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(
            delegate(object o, RunWorkerCompletedEventArgs args)
            {
                foreach (ContentClass addon in this.availableAddons)
                {
                    this.checkedListBox1.Items.Add(addon.name);
                    this.label2.Enabled = false;
                    this.label2.Visible = false;
                    this.checkedListBox1.Enabled = true;
                    this.checkedListBox1.Visible = true;
                }
            });

            this.toolTip1.ShowAlways = true;
            this.toolTip1.SetToolTip(linkLabel1, activeAddon.contact.ToString());

            infoWorker.RunWorkerAsync();
        }

        private void checkedListBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            Console.WriteLine("SelInChanged fired.");
            if (checkedListBox1.SelectedItem == null)
                return;
            foreach (ContentClass addon in this.availableAddons)
            {
                if (addon.name == checkedListBox1.SelectedItem.ToString())
                {
                    this.label3.Text = addon.name + " v." + addon.version;
                    this.richTextBox1.Text = addon.description;
                    this.toolTip1.SetToolTip(linkLabel1, addon.contact.ToString()); 
                    string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "forgotten_hope", "communityupdater");

                    if (!(File.Exists(Path.Combine(path, addon.ID + ".png"))))
                    {
                        getAddonImage(addon);
                        this.pictureBox1.ImageLocation = Path.Combine(path, "placeholder.png");
                    }
                    else
                        this.pictureBox1.ImageLocation = Path.Combine(path, addon.ID + ".png");
                    break;
                }
            }
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
                                this.selectedAddons.Add(addon);
                            break;
                        }
                        if ((this.clbNewState == CheckState.Unchecked) && (this.clbOldState == CheckState.Checked))
                        {
                            this.selectedAddons.Remove(addon);
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
        }

        private void getAddonImage (ContentClass addon)
        {
            BackgroundWorker imageWorker = new BackgroundWorker();
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "forgotten_hope", "communityupdater");

            imageWorker.DoWork += new DoWorkEventHandler(
            delegate(object o, DoWorkEventArgs args)
            {
                WebClient web = new WebClient();
                web.DownloadFile(addon.pictureURL, this.pictureBox1.ImageLocation = Path.Combine(path, addon.ID + ".png"));
            });

            imageWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(
            delegate(object o, RunWorkerCompletedEventArgs args)
            {
                this.pictureBox1.ImageLocation = Path.Combine(path, addon.ID + ".png");
            });

            imageWorker.RunWorkerAsync();
        }



        private void somethingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int i = 0;
            i++;
        }

        private void initContentInfo( string source )
        {
            XmlTextReader reader = new XmlTextReader(source);
            while (reader.Read())
            {
                if ((reader.NodeType == XmlNodeType.Element) && (reader.Name == "addon"))
                {
                    if (reader.HasAttributes)
                    {
                        ContentClass addon = new ContentClass();
                        addon.ID = int.Parse(reader.GetAttribute("id"));
                        addon.name = reader.GetAttribute("name");
                        addon.description = reader.GetAttribute("desc");
                        addon.contact = new Uri(reader.GetAttribute("contact"));
                        addon.version = reader.GetAttribute("version");
                        addon.fileIndexURL = reader.GetAttribute("index");
                        addon.torrent = new Uri(reader.GetAttribute("torrent"));
                        addon.pictureURL = new Uri(reader.GetAttribute("image"));
                        this.availableAddons.Add(addon);
                    }
                }


            }
            reader.Close();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            updater = new Updater(this.selectedAddons);
            updater.OnUpdateStatus += updater_OnUpdateStatus;
            updater.ShowDialog();
        }

        void updater_OnUpdateStatus(object sender, ProgressEventArgs e)
        {
            SetInfo(e);
        }

        private void SetInfo(ProgressEventArgs e)
        {
            if (this.label1.InvokeRequired)
            {
                SetTextCallback d = new SetTextCallback(SetInfo);
                this.Invoke(d, new object[] { e });
            }
            else
            {
                Console.WriteLine("Stats Update:  DLSpeed: " + e.DownloadSpeed.ToString() + " ULSpeed: " + e.UploadSpeed.ToString() + " Progress: " + e.Progress.ToString() + "% S/L: " + e.Seeds.ToString() + "/" + e.Leeches.ToString());
                float speed = (float)e.DownloadSpeed;
                string unit = "B";
                if (speed > 1024)
                {
                    speed = (float)speed / 1024;
                    unit = "KB";
                }
                if (speed > 1024)
                {
                    speed = (float)speed / 1024;
                    unit = "MB";
                }
                speed = (int)speed;
                if (!updater.Paused)
                {
                    this.notifyIcon1.Text = "Downloading from " + e.Seeds.ToString() + " Peers at " + speed.ToString() + unit + "/s (" + ((int)e.Progress).ToString() + "% completed)";
                    this.statusStrip1.Items[0].Text = "Downloading from " + e.Seeds.ToString() + " Peers at " + speed.ToString() + unit + "/s (" + ((int)e.Progress).ToString() + "% completed)";
                }
            }
        }
        delegate void SetTextCallback(ProgressEventArgs e);

        private void checkedListBox1_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            this.clbJustFired = true;
            this.clbLastIndex = e.Index;
            this.clbOldState = e.CurrentValue;
            this.clbNewState = e.NewValue;       
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start(this.activeAddon.contact.ToString());
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
            if (settings.ShowDialog(this) == DialogResult.OK)
            {
                Console.WriteLine(settings.checkBox1.CheckState);
                Console.WriteLine(settings.checkBox2.CheckState);
                Console.WriteLine(settings.checkBox3.CheckState);
                Console.WriteLine(settings.checkBox4.CheckState);
                Console.WriteLine(settings.checkBox5.CheckState);

            }
            settings.Dispose();
        }

    }
}
