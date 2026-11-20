// MHXXSwitchSaveEditor – version française (modifiée). Licence : GNU GPL v3 (voir LICENSE).
// Fichier créé en version 33 : morceau de la fenêtre principale (classe partielle MainForm), sans changement de comportement.
// Contenu : Emplacements de sauvegarde (boutons, échange, copie), fichiers récents / favoris, transfert A / B.
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
        // Pseudo du personnage de l'emplacement n (1 à 3) d'après la sauvegarde chargée ; null si l'emplacement est vide.
        // Rang de chasseur (RC) d'un emplacement ; -1 s'il est vide ou illisible.
        int SlotRank(int slot)
        {
            try
            {
                if (saveFile == null || saveFile.Length < 0x20 || saveFile[3 + slot] != 1) return -1;
                int off = BitConverter.ToInt32(saveFile, 0x10 + 4 * (slot - 1));
                int p = off + Offsets.HUNTER_RANK_OFFSET;
                if (off < 0 || p + 2 > saveFile.Length) return -1;
                return BitConverter.ToUInt16(saveFile, p);
            }
            catch (Exception) { return -1; }
        }

        string SlotName(int slot)
        {
            try
            {
                if (saveFile == null || saveFile.Length < 0x20 || saveFile[3 + slot] != 1) return null;
                int off = BitConverter.ToInt32(saveFile, 0x10 + 4 * (slot - 1));
                int p = off + Offsets.NAME_OFFSET;
                if (off < 0 || p + 32 > saveFile.Length) return "?";
                string n = Encoding.UTF8.GetString(saveFile, p, 32).Split('\0')[0].Trim();
                return n.Length == 0 ? "?" : n;
            }
            catch (Exception) { return "?"; }
        }

        // Met le pseudo de chaque emplacement dans les menus « Choisir », « Supprimer », « Échanger », « Dupliquer » et dans la liste du haut.
        void UpdateSlotMenuTexts()
        {
            ToolStripMenuItem[] choose = { toolStripMenuItemSaveSlot1, toolStripMenuItemSaveSlot2, toolStripMenuItemSaveSlot3 };
            ToolStripMenuItem[] del = { slot1ToolStripMenuItem, slot2ToolStripMenuItem, slot3ToolStripMenuItem };
            string empty = RomfsConfig.L("(empty)", "(vide)");
            string slotWord = RomfsConfig.L("Slot ", "Emplacement ");
            string[] names = new string[3];
            bool[] used = new bool[3];
            int usedCount = 0;
            for (int i = 0; i < 3; i++)
            {
                string n = SlotName(i + 1);
                used[i] = n != null;
                if (used[i]) usedCount++;
                names[i] = n ?? empty;
                choose[i].Text = (i + 1).ToString() + " - " + names[i];
                choose[i].Checked = currentPlayer == i + 1;
                del[i].Text = slotWord + (i + 1).ToString() + " - " + names[i];
            }

            if (menuSwapSlots != null && swapItems != null)
            {
                int[,] pairs = { { 1, 2 }, { 1, 3 }, { 2, 3 } };
                bool any = false;
                for (int k = 0; k < 3; k++)
                {
                    int a = pairs[k, 0], b = pairs[k, 1];
                    swapItems[k].Text = slotWord + a.ToString() + " (" + names[a - 1] + ")  \u2194  " + b.ToString() + " (" + names[b - 1] + ")";
                    swapItems[k].Enabled = used[a - 1] || used[b - 1];
                    if (swapItems[k].Enabled) any = true;
                }
                menuSwapSlots.Enabled = any;
            }

            if (menuCopySlots != null && copySrc != null)
            {
                for (int src = 0; src < 3; src++)
                {
                    copySrc[src].Text = slotWord + (src + 1).ToString() + " (" + names[src] + ")";
                    copySrc[src].Enabled = used[src];
                    int di = 0;
                    for (int dst = 0; dst < 3; dst++)
                    {
                        if (dst == src) continue;
                        copyDst[src][di].Text = RomfsConfig.L("to slot ", "vers l'emplacement ") + (dst + 1).ToString() + " (" + names[dst] + ")"
                                              + (used[dst] ? RomfsConfig.L(" - will be REPLACED", " - sera REMPLACÉ") : "");
                        di++;
                    }
                }
                menuCopySlots.Enabled = usedCount > 0;
            }

            UpdateSlotButtons();
        }

        // Change de personnage (emplacement 1 à 3), comme Fichier > Choisir l'emplacement de sauvegarde.
        void SwitchToSlot(int n)
        {
            if (n == currentPlayer) return;
            if (SlotName(n) == null) return; // emplacement vide : on ne change pas
            switch (n)
            {
                case 1: ToolStripMenuItemSaveSlot1_Click(this, EventArgs.Empty); break;
                case 2: ToolStripMenuItemSaveSlot2_Click(this, EventArgs.Empty); break;
                default: ToolStripMenuItemSaveSlot3_Click(this, EventArgs.Empty); break;
            }
        }

        // Les trois boutons d'emplacement, en haut à gauche de l'onglet Général : « 1 - Pseudo », « 2 - (vide) », « 3 - Pseudo ».
        void CreateSlotButtons()
        {
            slotButtons = new Button[3];
            for (int i = 0; i < 3; i++)
            {
                int n = i + 1;
                Button b = new Button();
                b.FlatStyle = FlatStyle.Flat;
                b.TextAlign = ContentAlignment.MiddleLeft;
                b.UseVisualStyleBackColor = false;
                b.TabStop = false;
                b.Visible = false;
                b.Click += delegate { SwitchToSlot(n); };
                Controls.Add(b);
                slotButtons[i] = b;
            }
        }

        void UpdateSlotButtons()
        {
            if (slotButtons == null || tabControlMain == null || generalTab == null) return;
            string empty = RomfsConfig.L("(empty)", "(vide)");
            string[] text = new string[3];
            bool[] used = new bool[3];
            int usedCount = 0;
            for (int i = 0; i < 3; i++)
            {
                string n = SlotName(i + 1);
                used[i] = n != null;
                if (used[i]) usedCount++;
                text[i] = (i + 1).ToString() + " - " + (n ?? empty);
            }
            bool show = tabControlMain.SelectedTab == generalTab && saveFile != null && usedCount > 0;
            for (int i = 0; i < 3; i++) slotButtons[i].Visible = show;
            if (!show) return;

            int pad = Math.Max(8, Font.Height);
            int gap = Math.Max(4, Font.Height / 3);
            int w = 0;
            for (int i = 0; i < 3; i++) w = Math.Max(w, TextRenderer.MeasureText(text[i], Font).Width);
            w += Font.Height * 2;
            int h = (int)(Font.Height * 2.2f);
            Color accent = ThemeSettings.Dark ? Color.FromArgb(60, 110, 170) : SystemColors.Highlight;
            for (int i = 0; i < 3; i++)
            {
                Button b = slotButtons[i];
                b.Text = text[i];
                b.Size = new Size(w, h);
                b.Enabled = used[i];
                bool selected = currentPlayer == i + 1;
                int rank = SlotRank(i + 1);
                bool byRank = ThemeSettings.ColorRanks && used[i] && rank > 0;
                Color selBack = accent;
                if (byRank) selBack = ThemeSettings.Dark ? Color.FromArgb(38, 72, 120) : Color.FromArgb(205, 225, 245); // fond plus sombre / plus clair : les couleurs de rang restent lisibles
                b.BackColor = selected ? selBack : ThemeSettings.Field;
                b.ForeColor = selected ? Color.White : (used[i] ? ThemeSettings.FieldFore : Color.Gray);
                if (byRank) b.ForeColor = ThemeSettings.RankColor(rank);
                gfxTip.SetToolTip(b, used[i] ? RomfsConfig.L("Hunter rank: ", "Rang de chasseur (RC) : ") + rank.ToString() : "");
                b.FlatAppearance.BorderColor = selected ? accent : (ThemeSettings.Dark ? Color.FromArgb(90, 92, 104) : SystemColors.ControlDark);
                b.FlatAppearance.MouseOverBackColor = selected ? accent : (ThemeSettings.Dark ? Color.FromArgb(70, 72, 84) : SystemColors.ControlLight);
                b.Location = PointToClient(generalTab.PointToScreen(new Point(pad, pad + i * (h + gap))));
                b.BringToFront();
            }
        }

        // Fichier > Dupliquer un emplacement de sauvegarde (menu à deux niveaux : d'où, puis vers où).
        void BuildCopySlotsMenu()
        {
            ToolStripMenuItem parent = deleteSaveSlotToolStripMenuItem.OwnerItem as ToolStripMenuItem;
            if (parent == null || menuSwapSlots == null) return;
            menuCopySlots = new ToolStripMenuItem(RomfsConfig.L("Duplicate a save slot", "Dupliquer un emplacement de sauvegarde"));
            menuCopySlots.Enabled = false;
            menuCopySlots.ToolTipText = RomfsConfig.L("Copies a character into another slot. You are warned if it replaces an existing character. A safety copy is made first.",
                                                      "Copie un personnage dans un autre emplacement. Un avertissement s'affiche si cela remplace un personnage existant. Une copie de sécurité est faite avant.");
            copySrc = new ToolStripMenuItem[3];
            copyDst = new ToolStripMenuItem[3][];
            for (int src = 0; src < 3; src++)
            {
                int s1 = src + 1;
                copySrc[src] = new ToolStripMenuItem(s1.ToString());
                copyDst[src] = new ToolStripMenuItem[2];
                int di = 0;
                for (int dst = 0; dst < 3; dst++)
                {
                    if (dst == src) continue;
                    int d1 = dst + 1;
                    ToolStripMenuItem it = new ToolStripMenuItem(s1.ToString() + " -> " + d1.ToString());
                    it.Click += delegate { CopySlot(s1, d1); };
                    copyDst[src][di] = it;
                    copySrc[src].DropDownItems.Add(it);
                    di++;
                }
                menuCopySlots.DropDownItems.Add(copySrc[src]);
            }
            parent.DropDownItems.Insert(parent.DropDownItems.IndexOf(menuSwapSlots) + 1, menuCopySlots);
        }

        // Copie le personnage de l'emplacement 'src' dans l'emplacement 'dst' (sauvegardes MHGU Switch). Avertissement renforcé si 'dst' est déjà utilisé.
        void CopySlot(int src, int dst)
        {
            string title = RomfsConfig.L("Duplicate a slot", "Dupliquer un emplacement");
            if (!ConfirmUnsaved("duplicating a slot", "dupliquer un emplacement")) return;
            try
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;
                byte[] raw = File.ReadAllBytes(filePath);
                if (raw.Length != MHGU_SAVE_SIZE)
                {
                    MessageBox.Show(this,
                        RomfsConfig.L("Duplicating a slot is only available for MHGU Switch saves. Nothing was changed.",
                                      "La duplication d'un emplacement n'existe que pour les sauvegardes MHGU Switch. Rien n'a été modifié."),
                        title, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                int fs = 36 + 4 + (src - 1), fd = 36 + 4 + (dst - 1);
                if (raw[fs] != 1)
                {
                    MessageBox.Show(this, RomfsConfig.L("The source slot is empty.", "L'emplacement à copier est vide."),
                        title, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                int absS = 36 + BitConverter.ToInt32(raw, 36 + 0x10 + 4 * (src - 1));
                int absD = 36 + BitConverter.ToInt32(raw, 36 + 0x10 + 4 * (dst - 1));
                bool ok = src != dst && absS >= 36 + 0x30 && absD >= 36 + 0x30
                          && absS + MHGU_SLOT_SIZE <= raw.Length && absD + MHGU_SLOT_SIZE <= raw.Length
                          && (absS + MHGU_SLOT_SIZE <= absD || absD + MHGU_SLOT_SIZE <= absS);
                if (!ok)
                {
                    MessageBox.Show(this,
                        RomfsConfig.L("Unexpected save structure. Nothing was changed.", "Structure de sauvegarde inattendue. Rien n'a été modifié."),
                        title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                string nameS = NameFromRaw(raw, absS, true);
                bool destUsed = raw[fd] == 1;
                string nameD = NameFromRaw(raw, absD, destUsed);
                string msg;
                MessageBoxIcon icon;
                if (destUsed)
                {
                    msg = RomfsConfig.L("WARNING: slot " + dst + " already contains a character (" + nameD + ").\nIt will be REPLACED by a copy of slot " + src + " (" + nameS + ") and lost, except in the safety copy made just before.\n\nChanges not yet saved in the editor will be lost.\n\nContinue?",
                                        "ATTENTION : l'emplacement " + dst + " contient déjà un personnage (" + nameD + ").\nIl sera REMPLACÉ par une copie de l'emplacement " + src + " (" + nameS + ") et perdu, sauf dans la copie de sécurité faite juste avant.\n\nLes modifications non enregistrées dans l'éditeur seront perdues.\n\nContinuer ?");
                    icon = MessageBoxIcon.Exclamation;
                }
                else
                {
                    msg = RomfsConfig.L("Copy slot " + src + " (" + nameS + ") into the empty slot " + dst + "?\n\nA safety copy is made first. Changes not yet saved in the editor will be lost.",
                                        "Copier l'emplacement " + src + " (" + nameS + ") dans l'emplacement vide " + dst + " ?\n\nUne copie de sécurité est faite avant. Les modifications non enregistrées dans l'éditeur seront perdues.");
                    icon = MessageBoxIcon.Question;
                }
                if (MessageBox.Show(this, msg, title, MessageBoxButtons.YesNo, icon) != DialogResult.Yes) return;

                Array.Copy(raw, absS, raw, absD, MHGU_SLOT_SIZE);
                raw[fd] = 1;

                bool was = ThemeSettings.BackupOn;
                ThemeSettings.BackupOn = true;
                bool okBackup;
                try { okBackup = BackupManager.Backup(this, filePath); }
                finally { ThemeSettings.BackupOn = was; }
                if (!okBackup) return;
                SafeFile.WriteAllBytes(filePath, raw);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            LoadSaveFile(filePath, Path.GetFileName(filePath));
        }

        // Fichier > « Fichiers récents » : les derniers fichiers de sauvegarde ouverts, un clic suffit.
        void BuildRecentMenu()
        {
            ToolStripMenuItem parent = deleteSaveSlotToolStripMenuItem.OwnerItem as ToolStripMenuItem;
            if (parent == null) return;
            menuRecent = new ToolStripMenuItem(RomfsConfig.L("Recent files", "Fichiers récents"));
            menuRecent.DropDownItems.Add(new ToolStripMenuItem("..."));
            menuRecent.DropDownOpening += delegate { FillRecentMenu(); };
            menuFavorites = new ToolStripMenuItem(RomfsConfig.L("Favorite files", "Fichiers favoris"));
            menuFavorites.DropDownItems.Add(new ToolStripMenuItem("..."));
            menuFavorites.DropDownOpening += delegate { FillFavoritesMenu(); };
            int at = parent.DropDownItems.IndexOf(loadToolStripMenuItem);
            at = at < 0 ? 0 : at + 1;
            parent.DropDownItems.Insert(at, menuRecent);
            parent.DropDownItems.Insert(at + 1, menuFavorites);
        }

        void FillRecentMenu()
        {
            RecentFilesMenu.FillRecents(menuRecent.DropDownItems, OpenRecent);
        }

        void FillFavoritesMenu()
        {
            RecentFilesMenu.FillFavorites(menuFavorites.DropDownItems, OpenRecent, filePath);
        }

        void OpenRecent(string path)
        {
            if (!ConfirmUnsaved("loading another save", "charger une autre sauvegarde")) return;
            if (!File.Exists(path))
            {
                MessageBox.Show(this, RomfsConfig.L("This file no longer exists:\n", "Ce fichier n'existe plus :\n") + path,
                    RomfsConfig.L("Recent files", "Fichiers récents"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            LoadSaveFile(path, Path.GetFileName(path));
        }

        // Fichier > « Transférer entre deux sauvegardes (A / B)... » : fenêtre pour échanger, copier ou couper
        // un emplacement d'une sauvegarde vers n'importe quel emplacement d'une autre.
        void BuildTransferMenu()
        {
            ToolStripMenuItem parent = deleteSaveSlotToolStripMenuItem.OwnerItem as ToolStripMenuItem;
            if (parent == null) return;
            menuTransfer = new ToolStripMenuItem(RomfsConfig.L("Transfer between two saves (A / B)...", "Transférer entre deux sauvegardes (A / B)..."));
            menuTransfer.ToolTipText = RomfsConfig.L("Swap, copy or cut a slot from one save into any slot of another save. A safety copy is made first.",
                                                     "Échanger, copier ou couper un emplacement d'une sauvegarde vers n'importe quel emplacement d'une autre. Une copie de sécurité est faite avant.");
            menuTransfer.Click += delegate { OpenTransferDialog(); };
            int at = menuCopySlots != null ? parent.DropDownItems.IndexOf(menuCopySlots) + 1 : parent.DropDownItems.Count;
            parent.DropDownItems.Insert(at, menuTransfer);
        }

        void OpenTransferDialog()
        {
            if (!ConfirmUnsaved("transferring slots", "transférer des emplacements")) return;
            string current = (!string.IsNullOrEmpty(filePath) && File.Exists(filePath)) ? filePath : null;
            using (TransferSlotsDialog dlg = new TransferSlotsDialog(current))
            {
                dlg.ShowDialog(this);
                bool reload = false;
                foreach (string f in dlg.ChangedFiles)
                {
                    if (string.Equals(f, filePath, StringComparison.OrdinalIgnoreCase)) reload = true;
                }
                if (reload) LoadSaveFile(filePath, Path.GetFileName(filePath));
            }
        }

        void BuildSwapSlotsMenu()
        {
            ToolStripMenuItem parent = deleteSaveSlotToolStripMenuItem.OwnerItem as ToolStripMenuItem;
            if (parent == null) return;
            menuSwapSlots = new ToolStripMenuItem(RomfsConfig.L("Swap two save slots", "Échanger deux emplacements de sauvegarde"));
            menuSwapSlots.Enabled = false;
            menuSwapSlots.ToolTipText = RomfsConfig.L("Swaps two characters (or moves one into an empty slot). A safety copy is made first.",
                                                      "Échange deux personnages (ou déplace l'un dans un emplacement vide). Une copie de sécurité est faite avant.");
            int[,] pairs = { { 1, 2 }, { 1, 3 }, { 2, 3 } };
            swapItems = new ToolStripMenuItem[3];
            for (int k = 0; k < 3; k++)
            {
                int a = pairs[k, 0], b = pairs[k, 1];
                swapItems[k] = new ToolStripMenuItem(a.ToString() + " <-> " + b.ToString());
                swapItems[k].Click += delegate { SwapSlots(a, b); };
                menuSwapSlots.DropDownItems.Add(swapItems[k]);
            }
            parent.DropDownItems.Insert(parent.DropDownItems.IndexOf(deleteSaveSlotToolStripMenuItem) + 1, menuSwapSlots);
        }

        string NameFromRaw(byte[] raw, int absSlot, bool used)
        {
            if (!used) return RomfsConfig.L("(empty)", "(vide)");
            int p = absSlot + Offsets.NAME_OFFSET;
            if (p < 0 || p + 32 > raw.Length) return "?";
            return Encoding.UTF8.GetString(raw, p, 32).Split('\0')[0].Trim();
        }

        // Échange le contenu de deux emplacements de personnage (sauvegardes MHGU Switch). Si l'un est vide, le personnage est déplacé.
        // On travaille sur le fichier tel qu'il est sur le disque, en gardant son en-tête Switch ; copie de sécurité obligatoire avant.
        void SwapSlots(int a, int b)
        {
            string title = RomfsConfig.L("Swap two slots", "Échanger deux emplacements");
            if (!ConfirmUnsaved("swapping slots", "échanger des emplacements")) return;
            try
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;
                byte[] raw = File.ReadAllBytes(filePath);
                if (raw.Length != MHGU_SAVE_SIZE)
                {
                    MessageBox.Show(this,
                        RomfsConfig.L("Swapping slots is only available for MHGU Switch saves. Nothing was changed.",
                                      "L'échange d'emplacements n'existe que pour les sauvegardes MHGU Switch. Rien n'a été modifié."),
                        title, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                int fa = 36 + 4 + (a - 1), fb = 36 + 4 + (b - 1);
                if (raw[fa] != 1 && raw[fb] != 1)
                {
                    MessageBox.Show(this, RomfsConfig.L("Both slots are empty.", "Les deux emplacements sont vides."),
                        title, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                int absA = 36 + BitConverter.ToInt32(raw, 36 + 0x10 + 4 * (a - 1));
                int absB = 36 + BitConverter.ToInt32(raw, 36 + 0x10 + 4 * (b - 1));
                bool ok = absA >= 36 + 0x30 && absB >= 36 + 0x30
                          && absA + MHGU_SLOT_SIZE <= raw.Length && absB + MHGU_SLOT_SIZE <= raw.Length
                          && (absA + MHGU_SLOT_SIZE <= absB || absB + MHGU_SLOT_SIZE <= absA);
                if (!ok)
                {
                    MessageBox.Show(this,
                        RomfsConfig.L("Unexpected save structure. Nothing was changed.", "Structure de sauvegarde inattendue. Rien n'a été modifié."),
                        title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                string nameA = NameFromRaw(raw, absA, raw[fa] == 1);
                string nameB = NameFromRaw(raw, absB, raw[fb] == 1);
                if (MessageBox.Show(this,
                        RomfsConfig.L("Swap slot " + a + " (" + nameA + ") with slot " + b + " (" + nameB + ")?\n\nA safety copy is made first. Changes not yet saved in the editor will be lost.",
                                      "Échanger l'emplacement " + a + " (" + nameA + ") avec l'emplacement " + b + " (" + nameB + ") ?\n\nUne copie de sécurité est faite avant. Les modifications non enregistrées dans l'éditeur seront perdues."),
                        title, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

                byte[] tmp = new byte[MHGU_SLOT_SIZE];
                Array.Copy(raw, absA, tmp, 0, MHGU_SLOT_SIZE);
                Array.Copy(raw, absB, raw, absA, MHGU_SLOT_SIZE);
                Array.Copy(tmp, 0, raw, absB, MHGU_SLOT_SIZE);
                byte flag = raw[fa];
                raw[fa] = raw[fb];
                raw[fb] = flag;

                bool was = ThemeSettings.BackupOn;
                ThemeSettings.BackupOn = true;
                bool okBackup;
                try { okBackup = BackupManager.Backup(this, filePath); }
                finally { ThemeSettings.BackupOn = was; }
                if (!okBackup) return;
                SafeFile.WriteAllBytes(filePath, raw);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            LoadSaveFile(filePath, Path.GetFileName(filePath));
        }
    }
}
