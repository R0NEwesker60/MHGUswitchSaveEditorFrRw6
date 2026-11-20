// MHXXSwitchSaveEditor – version française (modifiée). Licence : GNU GPL v3 (voir LICENSE).
// Fichier ajouté lors de la version 25 : fenêtre de gestion des copies de sécurité (liste, notes, restauration).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using MHXXSaveEditor.Util;

namespace MHXXSaveEditor.Forms
{
    // On voit la liste des copies à gauche et la note de la copie choisie à droite.
    // Les notes sont de vrais fichiers « note.txt » rangés à côté de la sauvegarde, dans le dossier de la copie.
    class BackupManagerDialog : Form
    {
        // Copie choisie pour être restaurée (null si la personne ferme la fenêtre sans restaurer).
        public BackupManager.Entry ChosenCopy;

        readonly string openFile;
        RadioButton rbNormal;
        RadioButton rbTransfer;
        Label lblFolder;
        ListView lv;
        TextBox txtNote;
        Button btnSaveNote;
        Button btnRestore;
        Button btnOpen;
        Button btnDelete;
        Button btnClose;
        Label lblStatus;
        BackupManager.Entry current;
        bool dirty;
        bool filling;
        bool building = true;

        static string L(string en, string fr)
        {
            return RomfsConfig.L(en, fr);
        }

        public BackupManagerDialog(string openSaveFile)
        {
            openFile = openSaveFile;
            Text = L("Safety copies and notes", "Copies de sécurité et notes");
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = false;
            ShowInTaskbar = false;
            Padding = new Padding(10);

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 4;
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // Ligne 1 : quel dossier de copies
            FlowLayoutPanel kinds = new FlowLayoutPanel();
            kinds.AutoSize = true;
            kinds.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            kinds.WrapContents = false;
            rbNormal = new RadioButton();
            rbNormal.AutoSize = true;
            rbNormal.Checked = true;
            rbNormal.Text = L("Normal copies (before each save)", "Copies normales (avant chaque enregistrement)");
            rbTransfer = new RadioButton();
            rbTransfer.AutoSize = true;
            rbTransfer.Text = L("Transfer copies (before an A / B transfer)", "Copies de transfert (avant un transfert A / B)");
            kinds.Controls.Add(rbNormal);
            kinds.Controls.Add(rbTransfer);
            root.Controls.Add(kinds, 0, 0);

            lblFolder = new Label();
            lblFolder.AutoSize = true;
            lblFolder.Margin = new Padding(3, 4, 3, 6);
            root.Controls.Add(lblFolder, 0, 1);

            // Ligne 3 : liste à gauche, note à droite
            TableLayoutPanel main = new TableLayoutPanel();
            main.Dock = DockStyle.Fill;
            main.ColumnCount = 2;
            main.RowCount = 1;
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58f));
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42f));

            lv = new ListView();
            lv.Dock = DockStyle.Fill;
            lv.View = View.Details;
            lv.FullRowSelect = true;
            lv.MultiSelect = false;
            lv.HideSelection = false;
            lv.Columns.Add(L("Date", "Date"), 100);
            lv.Columns.Add(L("Save", "Sauvegarde"), 70);
            lv.Columns.Add(L("Size", "Taille"), 60);
            lv.Columns.Add(L("Note", "Note"), 150);
            lv.SelectedIndexChanged += delegate { OnSelectionChanged(); };
            main.Controls.Add(lv, 0, 0);

            TableLayoutPanel right = new TableLayoutPanel();
            right.Dock = DockStyle.Fill;
            right.ColumnCount = 1;
            right.RowCount = 3;
            right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Label lblNote = new Label();
            lblNote.AutoSize = true;
            lblNote.Text = L("Note on the selected copy (saved as note.txt next to the save):", "Note sur la copie choisie (enregistrée dans note.txt à côté de la sauvegarde) :");
            lblNote.MaximumSize = new Size(300, 0);
            right.Controls.Add(lblNote, 0, 0);
            txtNote = new TextBox();
            txtNote.Dock = DockStyle.Fill;
            txtNote.Multiline = true;
            txtNote.AcceptsReturn = true;
            txtNote.ScrollBars = ScrollBars.Vertical;
            txtNote.TextChanged += delegate
            {
                if (filling) return;
                dirty = true;
                btnSaveNote.Enabled = current != null;
                lblStatus.Text = "";
            };
            right.Controls.Add(txtNote, 0, 1);
            btnSaveNote = new Button();
            btnSaveNote.AutoSize = true;
            btnSaveNote.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            btnSaveNote.Text = L("Save the note", "Enregistrer la note");
            btnSaveNote.Click += delegate { SaveNote(true); };
            right.Controls.Add(btnSaveNote, 0, 2);
            main.Controls.Add(right, 1, 0);
            root.Controls.Add(main, 0, 2);

            // Ligne 4 : boutons
            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.AutoSize = true;
            buttons.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            buttons.Dock = DockStyle.Fill;
            btnRestore = MakeButton(L("Restore this copy", "Restaurer cette copie"));
            btnRestore.Click += delegate { OnRestore(); };
            btnOpen = MakeButton(L("Open the copy's folder", "Ouvrir le dossier de la copie"));
            btnOpen.Click += delegate { OnOpenFolder(); };
            btnDelete = MakeButton(L("Delete this copy", "Supprimer cette copie"));
            btnDelete.Click += delegate { OnDelete(); };
            btnClose = MakeButton(L("Close", "Fermer"));
            btnClose.Click += delegate { Close(); };
            buttons.Controls.Add(btnRestore);
            buttons.Controls.Add(btnOpen);
            buttons.Controls.Add(btnDelete);
            buttons.Controls.Add(btnClose);
            lblStatus = new Label();
            lblStatus.AutoSize = true;
            lblStatus.Margin = new Padding(12, 8, 3, 3);
            buttons.Controls.Add(lblStatus);
            root.Controls.Add(buttons, 0, 3);

            Controls.Add(root);
            CancelButton = btnClose;

            rbNormal.CheckedChanged += delegate { if (rbNormal.Checked) OnKindChanged(); };
            rbTransfer.CheckedChanged += delegate { if (rbTransfer.Checked) OnKindChanged(); };
            FormClosing += delegate { SaveNoteIfDirty(); };

            ThemeSettings.ApplyZoom(this);
            if (ThemeSettings.Dark) ThemeSettings.Apply(this);
            int h = Font.Height;
            ClientSize = new Size(h * 60, h * 28);
            MinimumSize = new Size(h * 40, h * 20);
            lv.Columns[0].Width = h * 10;
            lv.Columns[1].Width = h * 5;
            lv.Columns[2].Width = h * 5;
            lv.Columns[3].Width = h * 12;
            lblNote.MaximumSize = new Size(h * 22, 0);

            building = false;
            if (openFile == null)
                lblStatus.Text = L("To restore, first load the save to replace (File > Load).", "Pour restaurer, chargez d'abord la sauvegarde à remplacer (Fichier > Charger).");
            Reload();
        }

        static Button MakeButton(string text)
        {
            Button b = new Button();
            b.AutoSize = true;
            b.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            b.Text = text;
            return b;
        }

        string CurrentRoot()
        {
            return rbTransfer.Checked ? BackupManager.TransferFolder : BackupManager.Folder;
        }

        List<BackupManager.Entry> CurrentList()
        {
            return rbTransfer.Checked ? BackupManager.ListTransfer() : BackupManager.ListNormal();
        }

        static string FirstLine(string note)
        {
            if (string.IsNullOrEmpty(note)) return "";
            string t = note.Replace("\r\n", "\n").Replace("\r", "\n").Trim();
            int nl = t.IndexOf('\n');
            if (nl >= 0) t = t.Substring(0, nl) + " ...";
            if (t.Length > 80) t = t.Substring(0, 80) + "...";
            return t;
        }

        void Reload()
        {
            if (building) return;
            current = null;
            dirty = false;
            string root = CurrentRoot();
            lblFolder.Text = L("Folder: ", "Dossier : ") + root;
            lv.BeginUpdate();
            lv.Items.Clear();
            foreach (BackupManager.Entry e in CurrentList())
            {
                string mb = (e.Size / 1048576.0).ToString("0.0") + " " + L("MB", "Mo");
                ListViewItem it = new ListViewItem(e.When);
                it.SubItems.Add(e.Name);
                it.SubItems.Add(mb);
                it.SubItems.Add(FirstLine(e.Note));
                it.Tag = e;
                lv.Items.Add(it);
            }
            lv.EndUpdate();
            if (lv.Items.Count > 0) lv.Items[0].Selected = true;
            else ShowCurrent();
        }

        void OnKindChanged()
        {
            if (building) return;
            SaveNoteIfDirty();
            Reload();
        }

        void OnSelectionChanged()
        {
            if (building) return;
            BackupManager.Entry next = lv.SelectedItems.Count > 0 ? lv.SelectedItems[0].Tag as BackupManager.Entry : null;
            if (next == current) return;
            SaveNoteIfDirty(); // enregistre la note de la copie qu'on quitte
            current = next;
            ShowCurrent();
        }

        void ShowCurrent()
        {
            filling = true;
            try
            {
                if (current == null)
                {
                    txtNote.Text = "";
                }
                else
                {
                    string n = current.Note ?? "";
                    txtNote.Text = n.Replace("\r\n", "\n").Replace("\n", "\r\n");
                }
            }
            finally
            {
                filling = false;
            }
            dirty = false;
            txtNote.Enabled = current != null;
            btnSaveNote.Enabled = false;
            btnRestore.Enabled = current != null && openFile != null;
            btnOpen.Enabled = current != null;
            btnDelete.Enabled = current != null;
        }

        void SaveNoteIfDirty()
        {
            if (dirty && current != null) SaveNote(false);
        }

        void SaveNote(bool show)
        {
            if (current == null) return;
            try
            {
                BackupManager.WriteNote(current, txtNote.Text);
                dirty = false;
                btnSaveNote.Enabled = false;
                foreach (ListViewItem it in lv.Items)
                {
                    if (it.Tag == current) it.SubItems[3].Text = FirstLine(current.Note);
                }
                if (show) lblStatus.Text = L("Note saved.", "Note enregistrée.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void OnRestore()
        {
            if (current == null || openFile == null) return;
            SaveNoteIfDirty();
            ChosenCopy = current;
            DialogResult = DialogResult.OK;
            Close();
        }

        void OnOpenFolder()
        {
            if (current == null) return;
            try
            {
                Process.Start("explorer.exe", "/select,\"" + current.Location + "\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void OnDelete()
        {
            if (current == null) return;
            if (MessageBox.Show(this,
                    L("Delete the copy of ", "Supprimer la copie du ") + current.When
                    + L(" and its note?\n\nThis cannot be undone.", " et sa note ?\n\nC'est définitif."),
                    Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try
            {
                BackupManager.Delete(current);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            Reload();
        }
    }
}
