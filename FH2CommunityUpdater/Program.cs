using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.IO;
using System.Text;
using System.Net;
using System.Threading;

namespace FH2CommunityUpdater
{
    class dummy
    {
        internal Exception e;
        internal dummy(Exception e)
        {
            this.e = e;
            this.raise();
        }
        internal void raise()
        {
            try
            {
                string error = e.ToString();
                ErrorReport errorReport = new ErrorReport(e);
                errorReport.ShowDialog();
            }
            catch
            {

            }
        }
    }

    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main( string[] args )
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
            {
                if (dum == null)
                    dum = new dummy((Exception)e.ExceptionObject);
            };
            Thread.GetDomain().UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
            {
                if (dum == null)
                    dum = new dummy((Exception)e.ExceptionObject);
            };
            try
            {
                Application.Run(new MainWindow(args));
            }
            catch (Exception e)
            {
                if (e.GetType() == typeof(WebException))
                    MessageBox.Show("Could not connect to the server.\nPlease check your connections and/or try again later.\nProgram will shut down.");
                if (dum == null)
                    dum = new dummy(e);
            }
        }

        static dummy dum = null;
    }
}
