using System;
using System.Threading;
using System.Windows.Forms;

namespace MHXXSaveEditor
{
    static class Program
    {
        // Signal envoyé par une 2e lancement pour que la fenêtre déjà ouverte (ou cachée dans la zone de notification) réapparaisse.
        internal static EventWaitHandle ShowSignal;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // Une seule copie du programme à la fois : un 2e lancement réveille la 1re et s'arrête.
            bool first;
            using (Mutex single = new Mutex(true, @"Local\MHGUsaveEditorFrRw6_Single", out first))
            {
                ShowSignal = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\MHGUsaveEditorFrRw6_Show");
                if (!first)
                {
                    // Lancé par Windows au démarrage (--tray) alors que le programme tourne déjà : on ne dérange pas.
                    bool trayStart = Array.IndexOf(Environment.GetCommandLineArgs(), "--tray") > 0;
                    if (!trayStart) ShowSignal.Set();
                    return;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Loc.Init();
                Application.Run(new MainForm());
            }
        }
    }
}
