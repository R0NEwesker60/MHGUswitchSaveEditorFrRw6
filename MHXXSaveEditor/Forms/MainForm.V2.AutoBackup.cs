// MHXXSwitchSaveEditor – version française (modifiée). Licence : GNU GPL v3 (voir LICENSE).
// Fichier créé en version 42 : morceau de la fenêtre principale (classe partielle MainForm).
// Contenu : copie automatique du dossier de sauvegarde à la fermeture du jeu (icône dans la zone de notification),
// et déchargement de la sauvegarde après « Enregistrer ».
using MHXXSaveEditor.Data;
using MHXXSaveEditor.Forms;
using MHXXSaveEditor.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using Microsoft.Win32;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MHXXSaveEditor
{
    public partial class MainForm : Form
    {
        NotifyIcon autoTray;
        Icon autoTrayIcon;
        System.Windows.Forms.Timer autoTimer;
        bool autoGameWasRunning;
        bool autoCopying;
        bool realExit;               // vrai quand on quitte vraiment (menu de l'icône) : la croix ne réduit plus la fenêtre
        bool keepLoadedAfterSave;    // vrai quand l'enregistrement vient d'une question « enregistrer avant de continuer ? » : on ne décharge pas
        DateTime autoPendingAt = DateTime.MinValue;
        DateTime autoSeenAt = DateTime.MinValue;   // quand le jeu a été vu se lancer (MinValue = déjà ouvert avant l'éditeur : durée inconnue)
        bool autoOwed;                             // une copie était prévue mais le jeu a été relancé avant : elle reste due
        // Lancé par Windows au démarrage (argument --tray) : pas de fenêtre, seulement l'icône dans la zone de notification.
        bool startHidden = StartedWithTrayArg();
        const string StartupValueName = "MHGUsaveEditorFrRw6";
        const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        ToolStripMenuItem menuStartup;
        ToolStripMenuItem menuTrayNotice;
        ToolStripMenuItem menuCopyNotice;
        ToolStripMenuItem menuStartupPath;

        static bool StartedWithTrayArg()
        {
            string[] a = Environment.GetCommandLineArgs();
            for (int i = 1; i < a.Length; i++)
                if (string.Equals(a[i], "--tray", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        protected override void SetVisibleCore(bool value)
        {
            if (startHidden)
            {
                value = false; // démarrage caché : la fenêtre ne s'ouvre pas
                if (!IsHandleCreated) CreateHandle();
            }
            base.SetVisibleCore(value);
        }
        ToolStripMenuItem menuUnloadAfterSave;

        // L'icône reste (et la croix de la fenêtre la réduit) seulement si la copie automatique est activée et le jeu choisi.
        bool AutoTrayWanted
        {
            get { return autoTray != null && ThemeSettings.AutoOn && !string.IsNullOrEmpty(ThemeSettings.AutoExe); }
        }

        void InitAutoBackup()
        {
            autoTimer = new System.Windows.Forms.Timer();
            autoTimer.Interval = 2000;
            autoTimer.Tick += delegate { AutoTick(); };

            autoTray = new NotifyIcon();
            autoTray.Text = RomfsConfig.L("Save editor - automatic copy on", "Éditeur de sauvegarde - copie auto active");
            try
            {
                using (Bitmap bmp = new Bitmap(32, 32))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.Clear(Color.Transparent);
                        DrawCrownIcon(g, new Rectangle(0, 0, 32, 32), 3); // la couronne des 18 monstres EX
                    }
                    autoTrayIcon = Icon.FromHandle(bmp.GetHicon());
                }
            }
            catch (Exception) { autoTrayIcon = null; }
            autoTray.Icon = autoTrayIcon ?? this.Icon ?? SystemIcons.Application;

            ContextMenuStrip cm = new ContextMenuStrip();
            ToolStripMenuItem open = new ToolStripMenuItem(RomfsConfig.L("Open the editor", "Ouvrir l'éditeur"));
            open.Click += delegate { RestoreFromTray(); };
            cm.Items.Add(open);
            ToolStripMenuItem now = new ToolStripMenuItem(RomfsConfig.L("Copy the save folder now", "Copier le dossier de sauvegarde maintenant"));
            now.Click += delegate { RunAutoCopy(true); };
            cm.Items.Add(now);
            ToolStripMenuItem fold = new ToolStripMenuItem(RomfsConfig.L("Open the automatic copies folder", "Ouvrir le dossier des copies automatiques"));
            fold.Click += delegate { OpenAutoFolder(); };
            cm.Items.Add(fold);
            cm.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem quit = new ToolStripMenuItem(RomfsConfig.L("Quit", "Quitter"));
            quit.Click += delegate { QuitFromTray(); };
            cm.Items.Add(quit);
            autoTray.ContextMenuStrip = cm;
            autoTray.DoubleClick += delegate { RestoreFromTray(); };

            FormClosed += delegate
            {
                try
                {
                    autoTimer.Stop();
                    autoTray.Visible = false;
                    autoTray.Dispose();
                }
                catch (Exception) { }
            };
            // Démarrage caché seulement si l'icône existe (copie automatique activée) : sinon la fenêtre s'ouvre normalement.
            if (startHidden && !AutoTrayWanted) startHidden = false;
            StartShowListener();
            ApplyAutoState();
        }

        // Un 2e lancement du programme envoie un signal : on réaffiche cette fenêtre au lieu d'en ouvrir une deuxième.
        void StartShowListener()
        {
            EventWaitHandle h = Program.ShowSignal;
            if (h == null) return;
            Thread t = new Thread(delegate ()
            {
                while (true)
                {
                    try { h.WaitOne(); } catch (Exception) { return; }
                    try
                    {
                        if (IsDisposed) return;
                        BeginInvoke(new Action(RestoreFromTray));
                    }
                    catch (Exception) { }
                }
            });
            t.IsBackground = true;
            t.Start();
        }

        // Réglages changés : l'icône et la surveillance du jeu suivent.
        void ApplyAutoState()
        {
            if (autoTray == null) return;
            bool on = AutoTrayWanted;
            autoTray.Visible = on;
            autoTimer.Enabled = on;
            autoPendingAt = DateTime.MinValue;
            autoGameWasRunning = on && AutoBackup.IsRunning(ThemeSettings.AutoExe);
            autoSeenAt = DateTime.MinValue;
            autoOwed = false;
        }

        // Toutes les 2 secondes : le jeu tourne-t-il ? Quand il vient de se fermer, copie 7 secondes plus tard.
        void AutoTick()
        {
            if (!AutoTrayWanted) return;
            bool running = AutoBackup.IsRunning(ThemeSettings.AutoExe);
            if (running)
            {
                if (!autoGameWasRunning) autoSeenAt = DateTime.Now;
                if (autoPendingAt != DateTime.MinValue) autoOwed = true; // le jeu est relancé avant la copie : elle reste due
                autoGameWasRunning = true;
                autoPendingAt = DateTime.MinValue;
            }
            else if (autoGameWasRunning)
            {
                autoGameWasRunning = false;
                // Jeu lancé puis refermé en moins de 15 secondes : pas de copie (sauf si une copie était encore due).
                bool longEnough = autoSeenAt == DateTime.MinValue || (DateTime.Now - autoSeenAt).TotalSeconds >= AutoBackup.MinRunSeconds;
                if (longEnough || autoOwed) autoPendingAt = DateTime.Now.AddSeconds(AutoBackup.DelaySeconds);
            }
            if (autoPendingAt != DateTime.MinValue && DateTime.Now >= autoPendingAt)
            {
                autoPendingAt = DateTime.MinValue;
                autoOwed = false;
                RunAutoCopy(false);
            }
        }

        // Appelé en premier à la fermeture de la fenêtre : la croix la cache (l'éditeur reste dans la zone de notification).
        void AutoTrayClosing(object sender, FormClosingEventArgs e)
        {
            if (realExit || e.CloseReason != CloseReason.UserClosing || !AutoTrayWanted) return;
            if (!ConfirmUnsaved("closing the window (the editor stays in the notification area)", "fermer la fenêtre (l'éditeur reste dans la zone de notification)"))
            {
                e.Cancel = true;
                return;
            }
            e.Cancel = true;
            UnloadSave(); // la fenêtre cachée ne garde aucune sauvegarde ouverte : on ne peut pas écraser la progression du jeu en rouvrant
            Hide();
            if (!ThemeSettings.TrayNotice) return;
            try
            {
                autoTray.ShowBalloonTip(4000, RomfsConfig.L("Still running", "Toujours actif"),
                    RomfsConfig.L("The editor stays here and copies your save folder when the game closes. Right-click the icon > Quit to stop it.",
                                  "L'éditeur reste ici et copie ton dossier de sauvegarde à la fermeture du jeu. Clic droit sur l'icône > Quitter pour l'arrêter."),
                    ToolTipIcon.Info);
            }
            catch (Exception) { }
        }

        void RestoreFromTray()
        {
            startHidden = false;
            Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
            BringToFront();
        }

        void QuitFromTray()
        {
            realExit = true;
            Close();
            if (!IsDisposed)
            {
                realExit = false; // la fermeture a été annulée (modifications non enregistrées) : on remontre la fenêtre
                RestoreFromTray();
            }
        }

        // Lance la copie du dossier de sauvegarde dans un autre fil (la fenêtre ne se fige pas).
        void RunAutoCopy(bool manual)
        {
            if (autoCopying) return;
            string src = ThemeSettings.AutoDir;
            int max = ThemeSettings.AutoMax;
            if (string.IsNullOrEmpty(src))
            {
                if (manual)
                    MessageBox.Show(this, RomfsConfig.L("Choose the folder to copy first.", "Choisissez d'abord le dossier à copier."),
                        RomfsConfig.L("Automatic copy", "Copie automatique"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            autoCopying = true;
            Task.Run(() => AutoBackup.Run(src, max, !manual)).ContinueWith(t =>
            {
                AutoBackup.Result r = t.Status == TaskStatus.RanToCompletion ? t.Result : null;
                try
                {
                    if (IsDisposed || !IsHandleCreated) return;
                    BeginInvoke(new Action(() => AutoCopyDone(r, manual)));
                }
                catch (Exception) { }
            });
        }

        void AutoCopyDone(AutoBackup.Result r, bool manual)
        {
            autoCopying = false;
            string title = RomfsConfig.L("Automatic copy", "Copie automatique");
            string text;
            bool ok = r != null && r.Ok;
            if (r != null && r.Skipped)
            {
                // Rien n'a changé depuis la dernière copie automatique : l'ancienne reste en place, pas de nouvelle copie.
                if (manual)
                    MessageBox.Show(this,
                        RomfsConfig.L("Nothing changed since the last automatic copy, so no new copy was made:\n", "Rien n'a changé depuis la dernière copie automatique, donc aucune nouvelle copie n'a été faite :\n") + r.Dir,
                        title, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (r == null) text = RomfsConfig.L("The copy could not be made.", "La copie n'a pas pu être faite.");
            else if (!r.Ok) text = RomfsConfig.L("The copy could not be made: ", "La copie n'a pas pu être faite : ") + r.Error;
            else
            {
                text = RomfsConfig.L("Copy made (", "Copie faite (") + r.Files.ToString() + RomfsConfig.L(" files):\n", " fichiers) :\n") + r.Dir;
                if (r.Failed > 0)
                    text += "\n" + r.Failed.ToString() + RomfsConfig.L(" item(s) could not be copied.", " élément(s) n'ont pas pu être copiés.");
            }
            if (manual || !AutoTrayWanted)
            {
                MessageBox.Show(this, text, title, MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
                return;
            }
            if (ok && !ThemeSettings.CopyNotice) return; // notification de copie désactivée dans Options (les erreurs restent signalées)
            try { autoTray.ShowBalloonTip(6000, title, text, ok ? ToolTipIcon.Info : ToolTipIcon.Warning); }
            catch (Exception) { }
        }

        void OpenAutoFolder()
        {
            try
            {
                string dir = AutoBackup.Folder;
                Directory.CreateDirectory(dir);
                Process.Start(dir);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, RomfsConfig.L("Automatic copy", "Copie automatique"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // ---- Menu Sauvegarde de sécurité > Copie automatique (une seule ligne : elle ouvre une fenêtre de réglages) ----

        // La copie automatique n'est plus dans le menu « Sauvegarde de sécurité » : elle a son propre bouton en haut (voir AddAutoCopyTopMenu).
        void BuildAutoBackupMenu(ToolStripMenuItem bk)
        {
            InitAutoBackup();
        }

        // Bouton « Save auto » (en jaune) en haut, à côté de Options : ouvre directement les réglages de la copie automatique.
        void AddAutoCopyTopMenu()
        {
            ToolStripMenuItem top = new ToolStripMenuItem(RomfsConfig.L("Auto save", "Save auto"));
            top.Name = "menuAutoCopyTop";
            top.ToolTipText = RomfsConfig.L("Automatic copy of the save folder when the game closes", "Copie automatique du dossier de sauvegarde quand le jeu se ferme");
            top.Click += delegate { ShowAutoCopyDialog(); };
            menuStripMain.Items.Add(top);
        }

        // Les onglets restent accessibles même sans sauvegarde chargée : seul leur contenu est grisé.
        bool tabsLoaded;

        // On ne grise plus les pages d'onglet en entier (une liste grisée devenait toute blanche en thème sombre) :
        // on grise les champs et les boutons un par un, et les listes restent sombres (vides). L'état d'origine de chaque
        // contrôle est mémorisé puis remis quand une sauvegarde est chargée.
        readonly Dictionary<Control, bool> tabSaved = new Dictionary<Control, bool>();

        void SetTabsEnabled(bool loaded)
        {
            tabsLoaded = loaded;
            tabControlMain.Enabled = true; // sinon on ne peut plus changer d'onglet
            foreach (TabPage p in tabControlMain.TabPages) p.Enabled = true;
            if (loaded)
            {
                foreach (KeyValuePair<Control, bool> kv in tabSaved) kv.Key.Enabled = kv.Value;
                tabSaved.Clear();
            }
            else
            {
                foreach (TabPage p in tabControlMain.TabPages) DisableControlsIn(p);
            }
            if (IsHandleCreated) UpdateLoadHint(); // au démarrage (fenêtre pas encore créée), la mise en page s'en charge
        }

        void DisableControlsIn(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                if (c is ListView) continue; // les listes restent en thème sombre
                bool container = (c is GroupBox || c is Panel || c is TableLayoutPanel || c is FlowLayoutPanel) && !(c is ChestView);
                if (container)
                {
                    DisableControlsIn(c);
                    continue;
                }
                if (!tabSaved.ContainsKey(c))
                {
                    tabSaved[c] = c.Enabled;
                    c.Enabled = false;
                }
            }
        }

        // Bouton « Aide : sauvegarde automatique » (onglet Général).
        void ShowAutoHelp()
        {
            string text = RomfsConfig.L(
                "AUTOMATIC BACKUP (copy of the save-0 folder)\n\n" +
                "What it does: each time you close the game, the editor copies the whole save folder (save-0: saves, the game's backup, mods...) into a dated backup folder. If a save gets damaged, you can go back.\n\n" +
                "To turn it on (once):\n" +
                "1) Click \"Auto save\" at the top, next to \"Options\".\n" +
                "2) Tick \"Turn on the automatic copy\".\n" +
                "3) Choose the game's program (the .exe of MHGU Recomp), then the save-0 folder.\n\n" +
                "After that it is automatic:\n" +
                "- The copy is made 7 seconds after the game closes.\n" +
                "- No copy if the game ran less than 15 seconds, or if nothing changed since the last copy.\n" +
                "- 10 copies are kept by default (adjustable); the oldest are deleted.\n" +
                "- If you close the editor's window with the cross, it stays in the notification area (crown icon) to watch the game. Right-click the icon > Quit to really stop it.\n" +
                "- In the Options menu you can start the editor with Windows, with no window.\n\n" +
                "For hand-made copies with notes and favorites, and to inject a copy back: Safety backup > Manual backups and injection.",
                "SAUVEGARDE AUTOMATIQUE (copie du dossier save-0)\n\n" +
                "À quoi ça sert : à chaque fois que tu fermes le jeu, l'éditeur copie tout le dossier de sauvegarde (save-0 : sauvegardes, backup du jeu, mods...) dans un dossier de copies datées. Si une sauvegarde est abîmée, tu peux revenir en arrière.\n\n" +
                "Pour l'activer (une seule fois) :\n" +
                "1) Clique sur « Save auto » en haut, à côté de « Options ».\n" +
                "2) Coche « Activer la copie automatique ».\n" +
                "3) Choisis le programme du jeu (le .exe de MHGU Recomp), puis le dossier save-0.\n\n" +
                "Ensuite, c'est automatique :\n" +
                "- La copie est faite 7 secondes après la fermeture du jeu.\n" +
                "- Pas de copie si le jeu a tourné moins de 15 secondes, ni si rien n'a changé depuis la dernière copie.\n" +
                "- 10 copies sont gardées par défaut (réglable) ; les plus anciennes sont supprimées.\n" +
                "- Si tu fermes la fenêtre de l'éditeur avec la croix, il reste dans la zone de notification (icône couronne) pour surveiller le jeu. Clic droit sur l'icône > Quitter pour l'arrêter vraiment.\n" +
                "- Dans le menu Options, tu peux lancer l'éditeur au démarrage de Windows, sans fenêtre.\n\n" +
                "Pour des copies faites à la main avec notes et favoris, et pour réinjecter une copie : Sauvegarde de sécurité > Sauvegardes manuelles et injection.");
            MessageBox.Show(this, text, RomfsConfig.L("Help: automatic backup", "Aide : sauvegarde automatique"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // Fenêtre de réglages : elle reste ouverte pendant qu'on change les réglages (plus besoin de refaire tout le chemin du menu).
        void ShowAutoCopyDialog()
        {
            string title = RomfsConfig.L("Automatic copy when the game closes", "Copie automatique à la fermeture du jeu");
            using (Form dlg = new Form())
            {
                dlg.Text = title;
                dlg.Font = Font;
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;
                dlg.ShowInTaskbar = false;
                int u = Math.Max(12, Font.Height);
                int fieldW = u * 26;
                int btnW = u * 8;
                int y = u;

                CheckBox chkOn = new CheckBox();
                chkOn.Text = RomfsConfig.L("Turn on the automatic copy", "Activer la copie automatique");
                chkOn.AutoSize = true;
                chkOn.Location = new Point(u, y);
                dlg.Controls.Add(chkOn);
                y += chkOn.PreferredSize.Height + u;

                Label l1 = new Label();
                l1.Text = RomfsConfig.L("Game program (the .exe of MHGU Recomp):", "Programme du jeu (le .exe de MHGU Recomp) :");
                l1.AutoSize = true;
                l1.Location = new Point(u, y);
                dlg.Controls.Add(l1);
                y += l1.PreferredSize.Height + u / 3;
                TextBox tbExe = new TextBox();
                tbExe.ReadOnly = true;
                tbExe.Width = fieldW;
                tbExe.Location = new Point(u, y);
                dlg.Controls.Add(tbExe);
                Button btExe = new Button();
                btExe.Text = RomfsConfig.L("Choose...", "Choisir...");
                btExe.Size = new Size(btnW, tbExe.Height + 2);
                btExe.Location = new Point(tbExe.Right + u / 2, y - 1);
                dlg.Controls.Add(btExe);
                y += tbExe.Height + u;

                Label l2 = new Label();
                l2.Text = RomfsConfig.L("Folder to copy (save-0):", "Dossier à copier (save-0) :");
                l2.AutoSize = true;
                l2.Location = new Point(u, y);
                dlg.Controls.Add(l2);
                y += l2.PreferredSize.Height + u / 3;
                TextBox tbDir = new TextBox();
                tbDir.ReadOnly = true;
                tbDir.Width = fieldW;
                tbDir.Location = new Point(u, y);
                dlg.Controls.Add(tbDir);
                Button btDir = new Button();
                btDir.Text = RomfsConfig.L("Choose...", "Choisir...");
                btDir.Size = new Size(btnW, tbDir.Height + 2);
                btDir.Location = new Point(tbDir.Right + u / 2, y - 1);
                dlg.Controls.Add(btDir);
                y += tbDir.Height + u;

                Label l3 = new Label();
                l3.Text = RomfsConfig.L("Maximum number of automatic copies:", "Nombre maximum de copies automatiques :");
                l3.AutoSize = true;
                l3.Location = new Point(u, y + 3);
                dlg.Controls.Add(l3);
                NumericUpDown num = new NumericUpDown();
                num.Minimum = 1;
                num.Maximum = 500;
                num.Value = Math.Min(500, Math.Max(1, ThemeSettings.AutoMax));
                num.Width = u * 5;
                num.Location = new Point(l3.Right + u / 2, y);
                dlg.Controls.Add(num);
                y += num.Height + u;

                Button btNow = new Button();
                btNow.Text = RomfsConfig.L("Copy the folder now", "Copier le dossier maintenant");
                btNow.Size = new Size(u * 14, u * 2);
                btNow.Location = new Point(u, y);
                dlg.Controls.Add(btNow);
                Button btOpen = new Button();
                btOpen.Text = RomfsConfig.L("Open the copies folder", "Ouvrir le dossier des copies");
                btOpen.Size = new Size(u * 14, u * 2);
                btOpen.Location = new Point(btNow.Right + u / 2, y);
                dlg.Controls.Add(btOpen);
                y += btNow.Height + u;

                Button btClose = new Button();
                btClose.Text = RomfsConfig.L("Close", "Fermer");
                btClose.DialogResult = DialogResult.Cancel;
                btClose.Size = new Size(u * 8, u * 2);
                dlg.Controls.Add(btClose);
                dlg.CancelButton = btClose;
                dlg.ClientSize = new Size(Math.Max(btOpen.Right, btExe.Right) + u, y + btClose.Height + u);
                btClose.Location = new Point(dlg.ClientSize.Width - btClose.Width - u, y);

                bool refreshing = false;
                Action refresh = delegate
                {
                    refreshing = true;
                    string none = RomfsConfig.L("(not chosen)", "(pas choisi)");
                    tbExe.Text = string.IsNullOrEmpty(ThemeSettings.AutoExe) ? none : ThemeSettings.AutoExe + ".exe";
                    tbDir.Text = string.IsNullOrEmpty(ThemeSettings.AutoDir) ? none : ThemeSettings.AutoDir;
                    chkOn.Checked = ThemeSettings.AutoOn;
                    refreshing = false;
                };
                refresh();

                chkOn.CheckedChanged += delegate
                {
                    if (refreshing) return;
                    if (chkOn.Checked) TryEnableAuto(dlg);
                    else
                    {
                        ThemeSettings.AutoOn = false;
                        ThemeSettings.Save();
                        ApplyAutoState();
                    }
                    refresh();
                };
                btExe.Click += delegate { ChooseAutoExe(dlg); refresh(); };
                btDir.Click += delegate { ChooseAutoDir(dlg); refresh(); };
                num.ValueChanged += delegate
                {
                    ThemeSettings.AutoMax = (int)num.Value;
                    ThemeSettings.Save();
                };
                btNow.Click += delegate { RunAutoCopy(true); };
                btOpen.Click += delegate { OpenAutoFolder(); };

                ThemeSettings.Apply(dlg);
                dlg.ShowDialog(this);
            }
        }

        // Active la copie automatique (demande le programme du jeu et le dossier s'ils manquent). Retourne true si elle est activée.
        bool TryEnableAuto(IWin32Window owner)
        {
            string title = RomfsConfig.L("Automatic copy", "Copie automatique");
            if (string.IsNullOrEmpty(ThemeSettings.AutoExe))
            {
                MessageBox.Show(owner,
                    RomfsConfig.L("First, choose the game's program (the .exe of MHGU Recomp). The editor watches it and copies your save folder when it closes.",
                                  "D'abord, choisissez le programme du jeu (le .exe de MHGU Recomp). L'éditeur le surveille et copie votre dossier de sauvegarde quand il se ferme."),
                    title, MessageBoxButtons.OK, MessageBoxIcon.Information);
                ChooseAutoExe(owner);
            }
            if (!string.IsNullOrEmpty(ThemeSettings.AutoExe) && string.IsNullOrEmpty(ThemeSettings.AutoDir)) ChooseAutoDir(owner);
            if (string.IsNullOrEmpty(ThemeSettings.AutoExe) || string.IsNullOrEmpty(ThemeSettings.AutoDir))
            {
                ThemeSettings.AutoOn = false;
                return false;
            }
            ThemeSettings.AutoOn = true;
            ThemeSettings.Save();
            ApplyAutoState();
            MessageBox.Show(owner,
                RomfsConfig.L("Automatic copy is on. About " + AutoBackup.DelaySeconds.ToString() + " seconds after \"" + ThemeSettings.AutoExe + ".exe\" closes, the folder \"" + ThemeSettings.AutoDir + "\" is copied.\n\nIf you close this window with the cross, the editor stays in the notification area (crown icon). Right-click the icon > Quit to stop it.",
                              "La copie automatique est activée. Environ " + AutoBackup.DelaySeconds.ToString() + " secondes après la fermeture de « " + ThemeSettings.AutoExe + ".exe », le dossier « " + ThemeSettings.AutoDir + " » est copié.\n\nSi vous fermez cette fenêtre avec la croix, l'éditeur reste dans la zone de notification (icône couronne). Clic droit sur l'icône > Quitter pour l'arrêter."),
                title, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return true;
        }

        void ChooseAutoExe(IWin32Window owner)
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Title = RomfsConfig.L("Choose the game's program (MHGU Recomp)", "Choisissez le programme du jeu (MHGU Recomp)");
                dlg.Filter = RomfsConfig.L("Programs (*.exe)|*.exe", "Programmes (*.exe)|*.exe");
                if (dlg.ShowDialog(owner) != DialogResult.OK) return;
                ThemeSettings.AutoExe = Path.GetFileNameWithoutExtension(dlg.FileName);
                ThemeSettings.Save();
                ApplyAutoState();
            }
        }

        void ChooseAutoDir(IWin32Window owner)
        {
            string cur = ThemeSettings.AutoDir;
            if (string.IsNullOrEmpty(cur) && !string.IsNullOrEmpty(filePath) && File.Exists(filePath)) cur = Path.GetDirectoryName(filePath);
            string picked = FolderPicker.Pick(owner,
                RomfsConfig.L("Choose the folder to copy (save-0). Everything inside is copied: saves, the game's backup, mods...",
                              "Choisissez le dossier à copier (save-0). Tout ce qu'il contient est copié : sauvegardes, backup du jeu, mods..."),
                cur);
            if (picked == null) return;
            ThemeSettings.AutoDir = picked;
            ThemeSettings.Save();
        }

        // ---- Titre de la fenêtre : le nom du programme à gauche, « Modifié et amélioré par : R0NEwesker60 » vers le milieu ----
        string titleBase = "";

        public override string Text
        {
            get { return base.Text; }
            set
            {
                titleBase = value ?? "";
                base.Text = BuildTitle(titleBase);
            }
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (titleBase.Length > 0) base.Text = BuildTitle(titleBase);
        }

        // Windows écrit le titre collé à gauche : on ajoute des espaces (calculés d'après la largeur de la fenêtre) pour que la mention soit au milieu.
        string BuildTitle(string t)
        {
            try
            {
                string credit = RomfsConfig.L("Modified and improved by: R0NEwesker60", "Modifié et amélioré par : R0NEwesker60");
                Font f = SystemFonts.CaptionFont;
                Size big = new Size(10000, 100);
                TextFormatFlags fl = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
                int wLeft = TextRenderer.MeasureText(t, f, big, fl).Width;
                int wCredit = TextRenderer.MeasureText(credit, f, big, fl).Width;
                int wSpace = TextRenderer.MeasureText("a a", f, big, fl).Width - TextRenderer.MeasureText("aa", f, big, fl).Width;
                if (wSpace < 2) wSpace = 3;
                int start = (Width - wCredit) / 2;   // là où la mention doit commencer pour être centrée
                int gap = start - 30 - wLeft;        // 30 = place de l'icône à gauche du titre
                int spaces = gap / wSpace;
                if (spaces < 4) spaces = 4;
                if (spaces > 500) spaces = 500;
                return t + new string(' ', spaces) + credit;
            }
            catch (Exception) { return t; }
        }

        // ---- Menu Options : lancer au démarrage de Windows ----

        string StartupExePath
        {
            get { return string.IsNullOrEmpty(ThemeSettings.StartupExe) ? Application.ExecutablePath : ThemeSettings.StartupExe; }
        }

        bool StartupRegistered()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                    return k != null && k.GetValue(StartupValueName) != null;
            }
            catch (Exception) { return false; }
        }

        bool WriteStartup(bool on)
        {
            string title = RomfsConfig.L("Start with Windows", "Lancer au démarrage de Windows");
            try
            {
                if (on && !File.Exists(StartupExePath))
                {
                    MessageBox.Show(this, RomfsConfig.L("This program was not found: ", "Ce programme est introuvable : ") + StartupExePath,
                        title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RunKeyPath))
                {
                    if (k == null) return false;
                    if (on) k.SetValue(StartupValueName, "\"" + StartupExePath + "\" --tray");
                    else if (k.GetValue(StartupValueName) != null) k.DeleteValue(StartupValueName, false);
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
        }

        void BuildStartupMenuItems(ToolStripMenuItem opt)
        {
            menuTrayNotice = new ToolStripMenuItem(RomfsConfig.L("Show the \"Still running\" notification", "Afficher la notification « Toujours actif »"));
            menuTrayNotice.CheckOnClick = true;
            menuTrayNotice.Checked = ThemeSettings.TrayNotice;
            menuTrayNotice.ToolTipText = RomfsConfig.L("The message shown when the window goes into the notification area. Only this message: the copy messages stay.",
                                                       "Le message affiché quand la fenêtre se réduit dans la zone de notification. Seulement ce message : les messages de copie restent.");
            menuTrayNotice.Click += delegate
            {
                ThemeSettings.TrayNotice = menuTrayNotice.Checked;
                ThemeSettings.Save();
            };
            opt.DropDownItems.Add(menuTrayNotice);
            MenuChecks.Install(menuTrayNotice);

            menuCopyNotice = new ToolStripMenuItem(RomfsConfig.L("Show a notification when an automatic copy is made", "Afficher une notification quand une copie automatique est faite"));
            menuCopyNotice.CheckOnClick = true;
            menuCopyNotice.Checked = ThemeSettings.CopyNotice;
            menuCopyNotice.ToolTipText = RomfsConfig.L("The message shown after the game closes and the folder has been copied. Errors are always shown.",
                                                       "Le message affiché après la fermeture du jeu, une fois le dossier copié. Les erreurs sont toujours signalées.");
            menuCopyNotice.Click += delegate
            {
                ThemeSettings.CopyNotice = menuCopyNotice.Checked;
                ThemeSettings.Save();
            };
            opt.DropDownItems.Add(menuCopyNotice);
            MenuChecks.Install(menuCopyNotice);

            opt.DropDownItems.Add(new ToolStripSeparator());

            menuStartup = new ToolStripMenuItem(RomfsConfig.L("Start with Windows (no window, notification area only)", "Lancer au démarrage de Windows (sans fenêtre, zone de notification seulement)"));
            menuStartup.CheckOnClick = true;
            menuStartup.Checked = StartupRegistered();
            menuStartup.ToolTipText = RomfsConfig.L("Needs the automatic copy to be turned on: the editor then starts hidden, with only its crown icon.",
                                                    "Demande que la copie automatique soit activée : l'éditeur démarre alors caché, avec seulement son icône couronne.");
            menuStartup.Click += delegate
            {
                bool want = menuStartup.Checked;
                if (want && string.IsNullOrEmpty(ThemeSettings.StartupExe))
                {
                    ThemeSettings.StartupExe = Application.ExecutablePath;
                    ThemeSettings.Save();
                }
                if (!WriteStartup(want)) { menuStartup.Checked = !want; return; }
                if (want && !ThemeSettings.AutoOn)
                    MessageBox.Show(this,
                        RomfsConfig.L("The program will start with Windows, but the window will open normally until you turn on the automatic copy (Safety backup menu).",
                                      "Le programme se lancera au démarrage de Windows, mais la fenêtre s'ouvrira normalement tant que la copie automatique n'est pas activée (menu Sauvegarde de sécurité)."),
                        RomfsConfig.L("Start with Windows", "Lancer au démarrage de Windows"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            opt.DropDownItems.Add(menuStartup);
            MenuChecks.Install(menuStartup);

            menuStartupPath = new ToolStripMenuItem();
            menuStartupPath.Click += delegate { ShowStartupExe(); };
            opt.DropDownItems.Add(menuStartupPath);

            opt.DropDownOpening += delegate { UpdateStartupMenuTexts(); };
            UpdateStartupMenuTexts();
        }

        void UpdateStartupMenuTexts()
        {
            if (menuStartup == null) return;
            menuStartup.Checked = StartupRegistered();
            menuStartupPath.Text = RomfsConfig.L("Program started with Windows...", "Programme lancé au démarrage...");
            menuStartupPath.ToolTipText = StartupExePath;
        }

        // Affiche où se trouve le programme lancé au démarrage, avec la possibilité de le changer.
        void ShowStartupExe()
        {
            DialogResult r = MessageBox.Show(this,
                RomfsConfig.L("Program started with Windows:\n\n", "Programme lancé au démarrage de Windows :\n\n") + StartupExePath
                + RomfsConfig.L("\n\nDo you want to change it?", "\n\nVoulez-vous le changer ?"),
                RomfsConfig.L("Start with Windows", "Lancer au démarrage de Windows"), MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r == DialogResult.Yes) ChooseStartupExe();
        }

        // Pour changer l'endroit du programme (si on a déplacé l'exe) : le démarrage de Windows est mis à jour tout de suite.
        void ChooseStartupExe()
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Title = RomfsConfig.L("Choose the program to start with Windows", "Choisissez le programme à lancer au démarrage de Windows");
                dlg.Filter = RomfsConfig.L("Programs (*.exe)|*.exe", "Programmes (*.exe)|*.exe");
                try
                {
                    string cur = StartupExePath;
                    if (File.Exists(cur)) { dlg.InitialDirectory = Path.GetDirectoryName(cur); dlg.FileName = Path.GetFileName(cur); }
                }
                catch (Exception) { }
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                ThemeSettings.StartupExe = dlg.FileName;
                ThemeSettings.Save();
                if (StartupRegistered()) WriteStartup(true);
                UpdateStartupMenuTexts();
            }
        }

        // ---- Déchargement de la sauvegarde après « Enregistrer » ----
        // L'éditeur revient comme au démarrage (aucune sauvegarde ouverte) : si le jeu a avancé entre-temps,
        // un 2e « Enregistrer » ne peut plus écraser la progression du jeu avec une vieille version.
        void UnloadSave()
        {
            try
            {
                filePath = null;
                saveFile = null;
                saveFileRaw = null;
                detectedText = "";
                cleanHash = null;
                currentPlayer = 0;
                player = new Player();
                toolStripMenuItemSaveSlot1.Enabled = false;
                toolStripMenuItemSaveSlot2.Enabled = false;
                toolStripMenuItemSaveSlot3.Enabled = false;
                toolStripMenuItemSaveSlot1.Checked = false;
                toolStripMenuItemSaveSlot2.Checked = false;
                toolStripMenuItemSaveSlot3.Checked = false;
                slot1ToolStripMenuItem.Enabled = false;
                slot2ToolStripMenuItem.Enabled = false;
                slot3ToolStripMenuItem.Enabled = false;
                saveToolStripMenuItemSave.Enabled = false;
                editToolStripMenuItem.Enabled = false;
                saveAsToolStripMenuItem.Enabled = false;
                convertToolStripMenuItem.Enabled = false;
                SetTabsEnabled(false);
                Text = Constants.EDITOR_VERSION;
                UpdateSlotMenuTexts();
                UpdateSlotButtons();
                UpdateDetectedLabel();
            }
            catch (Exception) { }
        }
    }
}
