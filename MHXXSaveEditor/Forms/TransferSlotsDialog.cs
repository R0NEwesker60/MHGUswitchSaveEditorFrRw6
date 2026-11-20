// MHXXSwitchSaveEditor – version française (modifiée). Licence : GNU GPL v3 (voir LICENSE).
// Fichier ajouté lors de la version 24 : transfert d'emplacements entre deux sauvegardes système (A et B).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using MHXXSaveEditor.Data;
using MHXXSaveEditor.Util;

namespace MHXXSaveEditor.Forms
{
    // Fenêtre : on choisit deux fichiers de sauvegarde (A et B), un emplacement dans chacun,
    // puis l'action : Échanger, Copier ou Couper. Une copie de sécurité est faite avant d'écrire.
    class TransferSlotsDialog : Form
    {
        const int SaveSize = 4726152 + 432948; // taille d'une sauvegarde MHGU Switch
        const int SlotSize = 1177796;          // taille d'un emplacement de personnage
        const int Header = 36;                 // en-tête Switch

        // Fichiers modifiés pendant que la fenêtre était ouverte (l'éditeur recharge le sien si besoin).
        public readonly List<string> ChangedFiles = new List<string>();

        readonly TextBox[] txt = new TextBox[2];
        readonly ListBox[] lst = new ListBox[2];
        readonly Button[] btnPick = new Button[2];
        readonly Button[] btnRecent = new Button[2];
        readonly Button[] btnFav = new Button[2];
        readonly string[] path = new string[2];

        RadioButton rbSwap;
        RadioButton rbCopy;
        RadioButton rbCut;
        RadioButton rbAtoB;
        RadioButton rbBtoA;
        FlowLayoutPanel dirPanel;
        Label lblDir;
        Label lblSummary;
        Button btnApply;
        Button btnClose;
        bool building = true;

        static string L(string en, string fr)
        {
            return RomfsConfig.L(en, fr);
        }

        public TransferSlotsDialog(string currentFile)
        {
            Text = L("Transfer slots between two saves", "Transférer des emplacements entre deux sauvegardes");
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(10);

            TableLayoutPanel root = new TableLayoutPanel();
            root.AutoSize = true;
            root.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            root.ColumnCount = 2;
            root.RowCount = 4;
            root.Dock = DockStyle.Fill;

            root.Controls.Add(MakeSide(0), 0, 0);
            root.Controls.Add(MakeSide(1), 1, 0);

            GroupBox gbAction = new GroupBox();
            gbAction.Text = L("What to do", "Que faire");
            gbAction.AutoSize = true;
            gbAction.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            gbAction.Dock = DockStyle.Fill;
            gbAction.Padding = new Padding(8, 4, 8, 8);

            TableLayoutPanel actionTable = new TableLayoutPanel();
            actionTable.AutoSize = true;
            actionTable.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            actionTable.ColumnCount = 2;
            actionTable.RowCount = 2;
            actionTable.Dock = DockStyle.Fill;

            Label lblAction = new Label();
            lblAction.AutoSize = true;
            lblAction.Text = L("Action:", "Action :");
            lblAction.Margin = new Padding(3, 8, 8, 3);
            actionTable.Controls.Add(lblAction, 0, 0);

            FlowLayoutPanel actionFlow = new FlowLayoutPanel();
            actionFlow.AutoSize = true;
            actionFlow.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            actionFlow.WrapContents = false;
            rbSwap = MakeRadio(L("Swap", "Échanger"), true);
            rbCopy = MakeRadio(L("Copy", "Copier"), false);
            rbCut = MakeRadio(L("Cut (move)", "Couper (déplacer)"), false);
            actionFlow.Controls.Add(rbSwap);
            actionFlow.Controls.Add(rbCopy);
            actionFlow.Controls.Add(rbCut);
            actionTable.Controls.Add(actionFlow, 1, 0);

            lblDir = new Label();
            lblDir.AutoSize = true;
            lblDir.Text = L("Direction:", "Sens :");
            lblDir.Margin = new Padding(3, 8, 8, 3);
            actionTable.Controls.Add(lblDir, 0, 1);

            dirPanel = new FlowLayoutPanel();
            dirPanel.AutoSize = true;
            dirPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            dirPanel.WrapContents = false;
            rbAtoB = MakeRadio(L("A  →  B", "A  →  B"), true);
            rbBtoA = MakeRadio(L("B  →  A", "B  →  A"), false);
            dirPanel.Controls.Add(rbAtoB);
            dirPanel.Controls.Add(rbBtoA);
            actionTable.Controls.Add(dirPanel, 1, 1);

            gbAction.Controls.Add(actionTable);
            root.Controls.Add(gbAction, 0, 1);
            root.SetColumnSpan(gbAction, 2);

            lblSummary = new Label();
            lblSummary.AutoSize = true;
            lblSummary.Margin = new Padding(3, 10, 3, 6);
            root.Controls.Add(lblSummary, 0, 2);
            root.SetColumnSpan(lblSummary, 2);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.AutoSize = true;
            buttons.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.Dock = DockStyle.Fill;
            btnClose = new Button();
            btnClose.AutoSize = true;
            btnClose.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            btnClose.Text = L("Close", "Fermer");
            btnClose.Click += delegate { Close(); };
            btnApply = new Button();
            btnApply.AutoSize = true;
            btnApply.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            btnApply.Text = L("Apply", "Appliquer");
            btnApply.Click += delegate { ApplyTransfer(); };
            buttons.Controls.Add(btnClose);
            buttons.Controls.Add(btnApply);
            root.Controls.Add(buttons, 0, 3);
            root.SetColumnSpan(buttons, 2);

            Controls.Add(root);
            CancelButton = btnClose;

            ThemeSettings.ApplyZoom(this);
            if (ThemeSettings.Dark) ThemeSettings.Apply(this);
            FitSizes();

            building = false;
            if (!string.IsNullOrEmpty(currentFile)) SetFile(0, currentFile, false);
            UpdateSummary();
        }

        GroupBox MakeSide(int i)
        {
            GroupBox gb = new GroupBox();
            gb.Text = L("Save ", "Sauvegarde ") + (i == 0 ? "A" : "B");
            gb.AutoSize = true;
            gb.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            gb.Dock = DockStyle.Fill;
            gb.Padding = new Padding(8, 4, 8, 8);

            FlowLayoutPanel flow = new FlowLayoutPanel();
            flow.AutoSize = true;
            flow.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            flow.FlowDirection = FlowDirection.TopDown;
            flow.WrapContents = false;
            flow.Dock = DockStyle.Fill;

            txt[i] = new TextBox();
            txt[i].ReadOnly = true;
            flow.Controls.Add(txt[i]);

            FlowLayoutPanel pickRow = new FlowLayoutPanel();
            pickRow.AutoSize = true;
            pickRow.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            pickRow.WrapContents = false;
            pickRow.Margin = new Padding(0);
            btnPick[i] = new Button();
            btnPick[i].AutoSize = true;
            btnPick[i].AutoSizeMode = AutoSizeMode.GrowAndShrink;
            btnPick[i].Text = L("Choose a file...", "Choisir un fichier...");
            int side = i;
            btnPick[i].Click += delegate { PickFile(side); };
            pickRow.Controls.Add(btnPick[i]);
            btnRecent[i] = new Button();
            btnRecent[i].AutoSize = true;
            btnRecent[i].AutoSizeMode = AutoSizeMode.GrowAndShrink;
            btnRecent[i].Text = L("Recent  v", "Récents  v");
            btnRecent[i].Click += delegate { ShowRecent(side); };
            pickRow.Controls.Add(btnRecent[i]);
            btnFav[i] = new Button();
            btnFav[i].AutoSize = true;
            btnFav[i].AutoSizeMode = AutoSizeMode.GrowAndShrink;
            btnFav[i].Text = L("Favorites  v", "Favoris  v");
            btnFav[i].Click += delegate { ShowFavorites(side); };
            pickRow.Controls.Add(btnFav[i]);
            flow.Controls.Add(pickRow);

            lst[i] = new ListBox();
            lst[i].IntegralHeight = false;
            lst[i].SelectedIndexChanged += delegate { UpdateSummary(); };
            flow.Controls.Add(lst[i]);

            gb.Controls.Add(flow);
            return gb;
        }

        RadioButton MakeRadio(string text, bool check)
        {
            RadioButton rb = new RadioButton();
            rb.AutoSize = true;
            rb.Text = text;
            rb.Checked = check;
            rb.CheckedChanged += delegate { UpdateSummary(); };
            return rb;
        }

        // Tailles des zones : calculées d'après la police (donc d'après le zoom).
        void FitSizes()
        {
            int h = Font.Height;
            for (int i = 0; i < 2; i++)
            {
                txt[i].Width = h * 22;
                lst[i].Width = h * 22;
                lst[i].Height = h * 3 + 14;
            }
            lblSummary.MaximumSize = new Size(h * 46, 0);
        }

        // ---- Lecture des fichiers ----

        static bool TryRead(string p, out byte[] raw)
        {
            raw = null;
            try
            {
                if (string.IsNullOrEmpty(p) || !File.Exists(p)) return false;
                if (new FileInfo(p).Length != SaveSize) return false;
                raw = File.ReadAllBytes(p);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        static string NameOf(byte[] raw, int slot)
        {
            int abs = Header + BitConverter.ToInt32(raw, Header + 0x10 + 4 * (slot - 1));
            int p = abs + Offsets.NAME_OFFSET;
            if (abs < 0 || p < 0 || p + 32 > raw.Length) return "?";
            string n = Encoding.UTF8.GetString(raw, p, 32).Split('\0')[0].Trim();
            return n.Length == 0 ? "?" : n;
        }

        static string SlotText(byte[] raw, int slot)
        {
            string word = L("Slot ", "Emplacement ") + slot.ToString() + " - ";
            try
            {
                if (raw[Header + 4 + (slot - 1)] != 1) return word + L("(empty)", "(vide)");
                return word + NameOf(raw, slot);
            }
            catch (Exception)
            {
                return word + "?";
            }
        }

        void PickFile(int i)
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Title = L("Choose save ", "Choisir la sauvegarde ") + (i == 0 ? "A" : "B");
                dlg.Filter = L("All files", "Tous les fichiers") + " (*.*)|*.*";
                if (!string.IsNullOrEmpty(path[i]))
                {
                    try { dlg.InitialDirectory = Path.GetDirectoryName(path[i]); }
                    catch (Exception) { }
                }
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                SetFile(i, dlg.FileName, true);
            }
        }

        // Derniers fichiers utilisés / favoris : un clic remplit A ou B sans refaire la recherche.
        void ShowRecent(int i)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            ThemeSettings.StyleContextMenu(menu);
            RecentFilesMenu.FillRecents(menu.Items, delegate (string f) { SetFile(i, f, true); });
            menu.Show(btnRecent[i], new Point(0, btnRecent[i].Height));
        }

        void ShowFavorites(int i)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            ThemeSettings.StyleContextMenu(menu);
            RecentFilesMenu.FillFavorites(menu.Items, delegate (string f) { SetFile(i, f, true); }, path[i]);
            menu.Show(btnFav[i], new Point(0, btnFav[i].Height));
        }

        // Charge un fichier dans le côté A (0) ou B (1). Retourne false si ce n'est pas une sauvegarde MHGU Switch.
        bool SetFile(int i, string file, bool warn)
        {
            byte[] raw;
            if (!TryRead(file, out raw))
            {
                if (warn)
                    MessageBox.Show(this,
                        L("This file is not a MHGU Switch save (unexpected size). Nothing was changed.",
                          "Ce fichier n'est pas une sauvegarde MHGU Switch (taille inattendue). Rien n'a été modifié."),
                        Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
            path[i] = file;
            txt[i].Text = file;
            ThemeSettings.AddRecent(file);
            FillList(i, raw);
            UpdateSummary();
            return true;
        }

        void FillList(int i, byte[] raw)
        {
            int keep = lst[i].SelectedIndex;
            lst[i].Items.Clear();
            for (int s = 1; s <= 3; s++) lst[i].Items.Add(SlotText(raw, s));
            if (keep >= 0 && keep < 3) lst[i].SelectedIndex = keep;
        }

        void Reload(int i)
        {
            byte[] raw;
            if (path[i] != null && TryRead(path[i], out raw)) FillList(i, raw);
        }

        // ---- Affichage de ce qui va se passer ----

        int Mode
        {
            get { return rbSwap.Checked ? 0 : (rbCopy.Checked ? 1 : 2); }
        }

        string SideText(int i)
        {
            string letter = i == 0 ? "A" : "B";
            if (lst[i].SelectedIndex < 0) return letter + " : ?";
            return letter + " : " + Convert.ToString(lst[i].SelectedItem);
        }

        void UpdateSummary()
        {
            if (building) return;
            bool moving = Mode != 0;
            dirPanel.Enabled = moving;
            lblDir.Enabled = moving;

            if (path[0] == null || path[1] == null)
            {
                lblSummary.Text = L("Choose the two saves, A and B.", "Choisissez les deux sauvegardes, A et B.");
                return;
            }
            if (lst[0].SelectedIndex < 0 || lst[1].SelectedIndex < 0)
            {
                lblSummary.Text = L("Choose one slot in A and one slot in B.", "Choisissez un emplacement dans A et un emplacement dans B.");
                return;
            }
            string a = SideText(0);
            string b = SideText(1);
            if (Mode == 0)
            {
                lblSummary.Text = L("Swap:  ", "Échanger :  ") + a + "   <->   " + b;
            }
            else
            {
                string fromText = rbAtoB.Checked ? a : b;
                string toText = rbAtoB.Checked ? b : a;
                if (Mode == 1)
                    lblSummary.Text = L("Copy:  ", "Copier :  ") + fromText + "   ->   " + toText;
                else
                    lblSummary.Text = L("Cut:  ", "Couper :  ") + fromText + "   ->   " + toText + "   " + L("(the original is emptied)", "(l'original est vidé)");
            }
        }

        // ---- Le transfert ----

        static bool SamePath(string x, string y)
        {
            try
            {
                return string.Equals(Path.GetFullPath(x), Path.GetFullPath(y), StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return string.Equals(x, y, StringComparison.OrdinalIgnoreCase);
            }
        }

        // Copie de sécurité obligatoire (même si la copie automatique est coupée), rangée dans le dossier des transferts avec une note.
        bool SafeBackup(string file, string note)
        {
            bool was = ThemeSettings.BackupOn;
            ThemeSettings.BackupOn = true;
            try { return BackupManager.BackupTransfer(this, file, note); }
            finally { ThemeSettings.BackupOn = was; }
        }

        // Note automatique écrite dans note.txt à côté de la copie : ce qui s'est passé et de quelle sauvegarde il s'agit.
        string BuildNote(int i, string summary)
        {
            string side = i == 0 ? "A" : "B";
            return L("Automatic copy made just before a transfer (", "Copie automatique faite juste avant un transfert (")
                + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss") + ").\r\n"
                + L("This file is the OLD save " + side + " (before the transfer).", "Ce fichier est l'ANCIENNE sauvegarde " + side + " (avant le transfert).") + "\r\n"
                + summary + "\r\n\r\n"
                + "A : " + path[0] + "\r\n"
                + "B : " + path[1] + "\r\n";
        }

        void Info(string msg, MessageBoxIcon icon)
        {
            MessageBox.Show(this, msg, Text, MessageBoxButtons.OK, icon);
        }

        void ApplyTransfer()
        {
            if (path[0] == null || path[1] == null)
            {
                Info(L("Choose the two saves, A and B.", "Choisissez les deux sauvegardes, A et B."), MessageBoxIcon.Information);
                return;
            }
            if (SamePath(path[0], path[1]))
            {
                Info(L("A and B are the same file. To swap or duplicate slots inside one save, use the File menu.",
                       "A et B sont le même fichier. Pour échanger ou dupliquer des emplacements dans une seule sauvegarde, utilisez le menu Fichier."),
                     MessageBoxIcon.Information);
                return;
            }
            if (lst[0].SelectedIndex < 0 || lst[1].SelectedIndex < 0)
            {
                Info(L("Choose one slot in A and one slot in B.", "Choisissez un emplacement dans A et un emplacement dans B."), MessageBoxIcon.Information);
                return;
            }

            byte[] rawA;
            byte[] rawB;
            if (!TryRead(path[0], out rawA) || !TryRead(path[1], out rawB))
            {
                Info(L("One of the two files can no longer be read. Nothing was changed.",
                       "Un des deux fichiers ne peut plus être lu. Rien n'a été modifié."), MessageBoxIcon.Warning);
                return;
            }
            byte[][] raw = new byte[][] { rawA, rawB };
            int[] slot = new int[] { lst[0].SelectedIndex + 1, lst[1].SelectedIndex + 1 };
            int[] abs = new int[2];
            int[] flag = new int[2];
            bool[] used = new bool[2];
            for (int i = 0; i < 2; i++)
            {
                flag[i] = Header + 4 + (slot[i] - 1);
                abs[i] = Header + BitConverter.ToInt32(raw[i], Header + 0x10 + 4 * (slot[i] - 1));
                if (abs[i] < Header + 0x30 || abs[i] + SlotSize > raw[i].Length)
                {
                    Info(L("Unexpected save structure. Nothing was changed.", "Structure de sauvegarde inattendue. Rien n'a été modifié."), MessageBoxIcon.Warning);
                    return;
                }
                used[i] = raw[i][flag[i]] == 1;
            }

            int mode = Mode;
            int src = rbAtoB.Checked ? 0 : 1;
            int dst = 1 - src;

            if (mode == 0)
            {
                if (!used[0] && !used[1])
                {
                    Info(L("Both slots are empty.", "Les deux emplacements sont vides."), MessageBoxIcon.Information);
                    return;
                }
            }
            else if (!used[src])
            {
                Info(L("The source slot is empty: there is nothing to transfer.", "L'emplacement d'origine est vide : il n'y a rien à transférer."), MessageBoxIcon.Information);
                return;
            }

            byte[] template = null;
            if (mode == 2)
            {
                template = MHXXSaveEditor.Properties.Resources.EmptySlotMHGU;
                if (template == null || template.Length != SlotSize)
                {
                    Info("Empty slot template missing / modèle d'emplacement vide absent. Nothing was changed.", MessageBoxIcon.Warning);
                    return;
                }
            }

            string sa = SideText(0);
            string sb = SideText(1);
            string summary;
            if (mode == 0)
            {
                summary = L("Swap: ", "Échange : ") + sa + "  <->  " + sb;
            }
            else
            {
                string f2 = src == 0 ? sa : sb;
                string t2 = dst == 0 ? sa : sb;
                summary = (mode == 1 ? L("Copy: ", "Copie : ") : L("Cut (move): ", "Couper (déplacer) : ")) + f2 + "  ->  " + t2;
            }
            string[] notes = new string[] { BuildNote(0, summary), BuildNote(1, summary) };
            string msg;
            if (mode == 0)
            {
                msg = L("Swap these two slots?\n\n" + sa + "\n" + sb + "\n\nBoth saves will be modified. A safety copy of each is made first.",
                        "Échanger ces deux emplacements ?\n\n" + sa + "\n" + sb + "\n\nLes deux sauvegardes seront modifiées. Une copie de sécurité de chacune est faite avant.");
            }
            else
            {
                string fromText = src == 0 ? sa : sb;
                string toText = dst == 0 ? sa : sb;
                string verbEn = mode == 1 ? "Copy" : "Cut (move)";
                string verbFr = mode == 1 ? "Copier" : "Couper (déplacer)";
                msg = L(verbEn + " this slot?\n\nFrom  " + fromText + "\nTo  " + toText, verbFr + " cet emplacement ?\n\nDe  " + fromText + "\nVers  " + toText);
                if (used[dst])
                    msg += L("\n\nWARNING: the destination slot already contains a character. It will be REPLACED and lost, except in the safety copy made just before.",
                             "\n\nATTENTION : l'emplacement de destination contient déjà un personnage. Il sera REMPLACÉ et perdu, sauf dans la copie de sécurité faite juste avant.");
                if (mode == 2)
                    msg += L("\n\nThe original slot will be emptied.", "\n\nL'emplacement d'origine sera vidé.");
                msg += L("\n\nA safety copy is made first.", "\n\nUne copie de sécurité est faite avant.");
            }
            if (MessageBox.Show(this, msg, Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            // On modifie les deux tableaux en mémoire.
            bool[] changed = new bool[2];
            if (mode == 0)
            {
                byte[] tmp = new byte[SlotSize];
                Array.Copy(raw[0], abs[0], tmp, 0, SlotSize);
                Array.Copy(raw[1], abs[1], raw[0], abs[0], SlotSize);
                Array.Copy(tmp, 0, raw[1], abs[1], SlotSize);
                byte f = raw[0][flag[0]];
                raw[0][flag[0]] = raw[1][flag[1]];
                raw[1][flag[1]] = f;
                changed[0] = true;
                changed[1] = true;
            }
            else
            {
                Array.Copy(raw[src], abs[src], raw[dst], abs[dst], SlotSize);
                raw[dst][flag[dst]] = 1;
                changed[dst] = true;
                if (mode == 2)
                {
                    Array.Copy(template, 0, raw[src], abs[src], SlotSize);
                    raw[src][flag[src]] = 0;
                    changed[src] = true;
                }
            }

            // Copies de sécurité d'abord (toutes), puis écriture.
            try
            {
                for (int i = 0; i < 2; i++)
                    if (changed[i] && !SafeBackup(path[i], notes[i])) return;
                for (int i = 0; i < 2; i++)
                {
                    if (!changed[i]) continue;
                    SafeFile.WriteAllBytes(path[i], raw[i]);
                    if (!ChangedFiles.Contains(path[i])) ChangedFiles.Add(path[i]);
                }
            }
            catch (Exception ex)
            {
                Info(ex.Message, MessageBoxIcon.Warning);
                Reload(0);
                Reload(1);
                return;
            }
            Reload(0);
            Reload(1);
            UpdateSummary();
            Info(L("Done. The safety copies were made before writing, with an automatic note, in:\n", "Terminé. Les copies de sécurité ont été faites avant l'écriture, avec une note automatique, dans :\n") + BackupManager.TransferFolder, MessageBoxIcon.Information);
        }
    }
}
