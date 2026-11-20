// MHXXSwitchSaveEditor – version française (modifiée). Licence : GNU GPL v3 (voir LICENSE).
// Fichier créé en version 33 : morceau de la fenêtre principale (classe partielle MainForm), sans changement de comportement.
// Contenu : Copies de sécurité : menu, dossiers, notes, restauration.
using MHXXSaveEditor.Data;
using MHXXSaveEditor.Forms;
using MHXXSaveEditor.Util;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace MHXXSaveEditor
{
    public partial class MainForm : Form
    {
        // Remplace le fichier de sauvegarde ouvert par une copie choisie, puis la recharge dans l'éditeur.
        // (La version actuelle du fichier est d'abord copiée dans le dossier des copies.)
        // « Copies de sécurité : notes et restauration » : liste des copies, note de chaque copie, bouton Restaurer.
        void RestoreBackup()
        {
            string openFile = (!string.IsNullOrEmpty(filePath) && File.Exists(filePath)) ? filePath : null;
            using (BackupManagerDialog dlg = new BackupManagerDialog(openFile))
            {
                dlg.ShowDialog(this);
                if (dlg.ChosenCopy != null) RestoreFromCopy(dlg.ChosenCopy);
            }
        }

        void RestoreFromCopy(BackupManager.Entry chosenCopy)
        {
            string title = RomfsConfig.L("Restore a copy", "Restaurer une copie");
            string chosen = chosenCopy.DataFile;
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                MessageBox.Show(this,
                    RomfsConfig.L("Load the save to replace first (File > Load).", "Chargez d'abord la sauvegarde à remplacer (Fichier > Charger)."),
                    title, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                if (new FileInfo(chosen).Length != new FileInfo(filePath).Length)
                {
                    MessageBox.Show(this,
                        RomfsConfig.L("This copy does not have the same size as the open save: it is not from the same game. Nothing was changed.",
                                      "Cette copie n'a pas la même taille que la sauvegarde ouverte : ce n'est pas la même version de jeu. Rien n'a été modifié."),
                        title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (MessageBox.Show(this,
                        RomfsConfig.L("The save file\n", "Le fichier de sauvegarde\n") + filePath
                        + RomfsConfig.L("\nwill be replaced by the copy of ", "\nva être remplacé par la copie du ") + chosenCopy.When
                        + RomfsConfig.L(".\nThe current version is copied to the backup folder first. Changes not yet saved in the editor will be lost.\n\nContinue?",
                                        ".\nLa version actuelle est d'abord copiée dans le dossier des copies. Les modifications non enregistrées dans l'éditeur seront perdues.\n\nContinuer ?"),
                        title, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

                byte[] data = File.ReadAllBytes(chosen); // lue avant la copie de sécurité (qui peut effacer la plus ancienne)
                bool was = ThemeSettings.BackupOn;
                ThemeSettings.BackupOn = true;
                bool ok2;
                try { ok2 = BackupManager.Backup(this, filePath); }
                finally { ThemeSettings.BackupOn = was; }
                if (!ok2) return;
                SafeFile.WriteAllBytes(filePath, data);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            LoadSaveFile(filePath, Path.GetFileName(filePath));
        }

        void BuildBackupMenu()
        {
            ToolStripMenuItem bk = new ToolStripMenuItem(RomfsConfig.L("Safety backup", "Sauvegarde de sécurité"));

            menuBackupOn = new ToolStripMenuItem(RomfsConfig.L("Copy the save before saving", "Copier la sauvegarde avant d'enregistrer"));
            menuBackupOn.CheckOnClick = true;
            menuBackupOn.Checked = ThemeSettings.BackupOn;
            menuBackupOn.Click += delegate
            {
                ThemeSettings.BackupOn = menuBackupOn.Checked;
                ThemeSettings.Save();
            };
            bk.DropDownItems.Add(menuBackupOn);
            MenuChecks.Install(menuBackupOn);

            bk.DropDownItems.Add(new ToolStripSeparator());

            menuBackupFolder = new ToolStripMenuItem();
            menuBackupFolder.Click += delegate { ChooseBackupFolder(); };
            bk.DropDownItems.Add(menuBackupFolder);
            menuBackupFolder2 = new ToolStripMenuItem();
            menuBackupFolder2.Click += delegate { ChooseBackupFolder(); };
            bk.DropDownItems.Add(menuBackupFolder2);
            ToolStripMenuItem namesItem = new ToolStripMenuItem(RomfsConfig.L("Backup folder names...", "Noms des dossiers de copies..."));
            namesItem.Click += delegate { ChooseBackupNames(); };
            bk.DropDownItems.Add(namesItem);

            menuBackupMax = new ToolStripMenuItem();
            menuBackupMax.Click += delegate { ChooseBackupMax(); };
            bk.DropDownItems.Add(menuBackupMax);

            bk.DropDownItems.Add(new ToolStripSeparator());

            ToolStripMenuItem restore = new ToolStripMenuItem(RomfsConfig.L("Safety copies: notes and restore...", "Copies de sécurité : notes et restauration..."));
            restore.Click += delegate { RestoreBackup(); };
            bk.DropDownItems.Add(restore);

            ToolStripMenuItem now = new ToolStripMenuItem(RomfsConfig.L("Copy the save file now (as saved on disk)", "Copier maintenant le fichier de sauvegarde (version enregistrée)"));
            now.Click += delegate { BackupNow(); };
            bk.DropDownItems.Add(now);

            ToolStripMenuItem open = new ToolStripMenuItem(RomfsConfig.L("Open the backup folder", "Ouvrir le dossier des copies"));
            open.Click += delegate { OpenBackupFolder(); };
            bk.DropDownItems.Add(open);

            ToolStripMenuItem openTr = new ToolStripMenuItem(RomfsConfig.L("Open the transfer copies folder", "Ouvrir le dossier des copies de transfert"));
            openTr.Click += delegate { OpenTransferBackupFolder(); };
            bk.DropDownItems.Add(openTr);

            bk.DropDownOpening += delegate { UpdateBackupMenuTexts(); };
            UpdateBackupMenuTexts();
            menuStripMain.Items.Add(bk);
        }

        void UpdateBackupMenuTexts()
        {
            if (menuBackupOn == null) return;
            menuBackupOn.Checked = ThemeSettings.BackupOn;
            // Deux lignes : le dossier des copies normales, puis (alignée sous la fin de la 1re ligne) le dossier des transferts.
            string label = RomfsConfig.L("Backup folder: ", "Dossier des copies : ");
            string line1 = label + BackupManager.Folder;
            menuBackupFolder.Text = line1;
            int cut = line1.LastIndexOf('\\');
            string prefix = cut > 0 ? line1.Substring(0, cut) : line1;
            menuBackupFolder2.Text = new string(' ', SpacesToAlign(prefix, menuBackupFolder.Font)) + "\\" + BackupManager.TransferSubFolder;
            string tip = RomfsConfig.L("Click to choose the folder that holds both copy folders: ",
                                       "Cliquez pour choisir le dossier qui contient les deux dossiers de copies : ") + BackupManager.Root;
            menuBackupFolder.ToolTipText = tip;
            menuBackupFolder2.ToolTipText = tip;
            menuBackupMax.Text = RomfsConfig.L("Maximum number of copies: ", "Nombre maximum de copies : ") + ThemeSettings.BackupMax.ToString();
        }

        // Nombre d'espaces à mettre devant une ligne de menu pour qu'elle commence sous la fin du texte « prefix ».
        static int SpacesToAlign(string prefix, Font font)
        {
            try
            {
                TextFormatFlags f = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
                Size big = new Size(int.MaxValue, int.MaxValue);
                int none = TextRenderer.MeasureText("||", font, big, f).Width;
                int w = TextRenderer.MeasureText("|" + prefix + "|", font, big, f).Width - none;
                int sp = TextRenderer.MeasureText("| |", font, big, f).Width - none;
                if (sp <= 0) return 0;
                return Math.Max(0, (int)Math.Round(w / (double)sp));
            }
            catch (Exception) { return 0; }
        }

        // Changer les noms des deux dossiers de copies (les dossiers déjà créés sont renommés pour garder les anciennes copies).
        void ChooseBackupNames()
        {
            string title = RomfsConfig.L("Backup folder names", "Noms des dossiers de copies");
            string a = PromptText(title, RomfsConfig.L("Name of the folder for the normal backups:", "Nom du dossier des copies normales :"), ThemeSettings.BackupNormalName);
            if (a == null) return;
            string c = PromptText(title, RomfsConfig.L("Name of the folder for the transfer backups:", "Nom du dossier des copies de transfert :"), ThemeSettings.BackupTransferName);
            if (c == null) return;
            if (!ValidFolderName(a) || !ValidFolderName(c) || string.Equals(a, c, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this,
                    RomfsConfig.L("Each name must be filled in, different from the other, and without these characters: \\ / : * ? \" < > |",
                                  "Chaque nom doit être rempli, différent de l'autre, et sans ces caractères : \\ / : * ? \" < > |"),
                    title, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string oldNormal = BackupManager.Folder;
            string oldTransfer = BackupManager.TransferFolder;
            ThemeSettings.BackupNormalName = a;
            ThemeSettings.BackupTransferName = c;
            ThemeSettings.Save();
            TryRenameFolder(oldNormal, BackupManager.Folder);
            TryRenameFolder(oldTransfer, BackupManager.TransferFolder);
            UpdateBackupMenuTexts();
        }

        static bool ValidFolderName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Trim().Length == 0) return false;
            return name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
        }

        static void TryRenameFolder(string oldPath, string newPath)
        {
            try
            {
                if (string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase)) return;
                if (Directory.Exists(oldPath) && !Directory.Exists(newPath)) Directory.Move(oldPath, newPath);
            }
            catch (Exception) { }
        }

        void ChooseBackupFolder()
        {
            using (FolderBrowserDialog dlg = new FolderBrowserDialog())
            {
                dlg.Description = RomfsConfig.L("Choose the folder for the safety copies. The program creates two folders inside it: \"" + BackupManager.NormalSubFolder + "\" and \"" + BackupManager.TransferSubFolder + "\".",
                                                "Choisissez le dossier des copies de sécurité. Le programme y crée deux dossiers : « " + BackupManager.NormalSubFolder + " » et « " + BackupManager.TransferSubFolder + " ».");
                dlg.ShowNewFolderButton = true;
                string cur = BackupManager.Root;
                if (Directory.Exists(cur)) dlg.SelectedPath = cur;
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                ThemeSettings.BackupDir = dlg.SelectedPath;
                ThemeSettings.Save();
                UpdateBackupMenuTexts();
            }
        }

        void ChooseBackupMax()
        {
            int v = PromptNumber(RomfsConfig.L("Safety backup", "Sauvegarde de sécurité"),
                                 RomfsConfig.L("Maximum number of copies kept (the oldest is deleted beyond that):",
                                               "Nombre maximum de copies gardées (au-delà, la plus ancienne est supprimée) :"),
                                 ThemeSettings.BackupMax, 1, 500);
            if (v < 1) return;
            ThemeSettings.BackupMax = v;
            ThemeSettings.Save();
            UpdateBackupMenuTexts();
        }

        void BackupNow()
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                MessageBox.Show(this,
                    RomfsConfig.L("Load a save first (File > Load).", "Chargez d'abord une sauvegarde (Fichier > Charger)."),
                    RomfsConfig.L("Safety backup", "Sauvegarde de sécurité"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            bool was = ThemeSettings.BackupOn;
            ThemeSettings.BackupOn = true; // « maintenant » marche même si la copie automatique est coupée
            bool ok;
            try { ok = BackupManager.Backup(this, filePath); }
            finally { ThemeSettings.BackupOn = was; }
            if (ok && BackupManager.LastBackup != null)
                MessageBox.Show(this,
                    RomfsConfig.L("Copy made:\n", "Copie créée :\n") + BackupManager.LastBackup,
                    RomfsConfig.L("Safety backup", "Sauvegarde de sécurité"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        void OpenTransferBackupFolder()
        {
            try
            {
                string dir = BackupManager.TransferFolder;
                Directory.CreateDirectory(dir);
                System.Diagnostics.Process.Start(dir);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, RomfsConfig.L("Safety backup", "Sauvegarde de sécurité"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void OpenBackupFolder()
        {
            try
            {
                string dir = BackupManager.Folder;
                Directory.CreateDirectory(dir);
                System.Diagnostics.Process.Start(dir);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, RomfsConfig.L("Safety backup", "Sauvegarde de sécurité"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
