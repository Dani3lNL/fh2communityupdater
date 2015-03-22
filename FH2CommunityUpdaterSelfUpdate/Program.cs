using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace FH2CommunityUpdaterSelfUpdate
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main( string [] args )
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SelfUpdateWindow( args ));
        }
    }
}
