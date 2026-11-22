// MHXXSwitchSaveEditor – version française (modifiée). Licence : GNU GPL v3 (voir LICENSE).
// Fichier ajouté en version 48 : petite fenêtre « Sauvegardes manuelles et injection ».
using MHXXSaveEditor.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace MHXXSaveEditor.Forms
{
    class ManualBackupDialog : Form
    {
        class Row
        {
            public int Cat;                       // 0 manuelles, 1 automatiques, 2 avant injection, 3 transferts, 4 avant enregistrement
            public bool IsFolder;
            public string Location;
            public ManualBackup.Item Item;        // copies « dossier »
            public BackupManager.Entry Entry;     // copies « un seul fichier »
            public string When;
        }

        readonly Action<string> afterInject;
        readonly List<Row> rows = new List<Row>();
        Row cur;
        bool loadingNote;
        ComboBox cmbCat;
        ListView list;
        TextBox tbDir;
        TextBox tbNote;
        Button btNew, btFav, btDel, btInject, btOpen, btNoteOk, btNoteDel;
        ContextMenuStrip ctx;
        ToolStripMenuItem ctxFav;
        ToolStripMenuItem ctxNote;
        ToolStripMenuItem ctxNoteDel;
        CheckBox chkLimit;
        NumericUpDown numManual, numInject;

        static string L(string en, string fr) { return RomfsConfig.L(en, fr); }

        public ManualBackupDialog(Font font, Action<string> afterInject)
        {
            this.afterInject = afterInject;
            Font = font;
            int u = Math.Max(12, Font.Height);
            Text = L("Manual backups and injection", "Sauvegardes manuelles et injection");
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(Math.Max(900, u * 46), Math.Max(640, u * 32));
            MinimumSize = Size;

            // --- liste (au centre) ---
            list = new ListView();
            list.View = View.Details;
            list.FullRowSelect = true;
            list.MultiSelect = false;
            list.HideSelection = false;
            list.Dock = DockStyle.Fill;
            list.Columns.Add("★", u * 3);
            list.Columns.Add(L("Date", "Date"), u * 11);
            list.Columns.Add(L("Content", "Contenu"), u * 12);
            list.Columns.Add(L("Note", "Note"), u * 28);
            list.SelectedIndexChanged += delegate { OnSelect(); };
            Controls.Add(list);

            // --- bas : note, boutons, réglages ---
            TableLayoutPanel bottom = new TableLayoutPanel();
            bottom.Dock = DockStyle.Bottom;
            bottom.AutoSize = true;
            bottom.ColumnCount = 1;
            bottom.RowCount = 5;
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            for (int i = 0; i < 5; i++) bottom.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            bottom.Padding = new Padding(u / 2);

            Label lNote = new Label();
            lNote.Text = L("Note of the selected copy (manual backups only):", "Note de la copie sélectionnée (sauvegardes manuelles seulement) :");
            lNote.AutoSize = true;
            bottom.Controls.Add(lNote, 0, 0);

            tbNote = new TextBox();
            tbNote.Multiline = true;
            tbNote.ScrollBars = ScrollBars.Vertical;
            tbNote.Height = u * 4;
            tbNote.Dock = DockStyle.Fill;
            bottom.Controls.Add(tbNote, 0, 1);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.AutoSize = true;
            buttons.WrapContents = true;
            btNew = MakeButton(L("New manual backup", "Nouvelle sauvegarde manuelle"), u);
            btFav = MakeButton(L("Favorite", "Favori"), u);
            btDel = MakeButton(L("Delete the backup", "Supprimer la sauvegarde"), u);
            btInject = MakeButton(L("Inject into save-0", "Injecter dans save-0"), u);
            btOpen = MakeButton(L("Open the folder", "Ouvrir le dossier"), u);
            btNoteDel = MakeButton(L("Delete the note", "Supprimer la note"), u);
            btNoteOk = MakeButton(L("Confirm the note", "Valider la note"), u);
            Button btClose = MakeButton(L("Close", "Fermer"), u);
            btClose.DialogResult = DialogResult.Cancel;
            buttons.Controls.Add(btNew);
            buttons.Controls.Add(btFav);
            buttons.Controls.Add(btDel);
            buttons.Controls.Add(btInject);
            buttons.Controls.Add(btOpen);
            buttons.Controls.Add(btNoteDel);
            buttons.Controls.Add(btNoteOk);
            buttons.Controls.Add(btClose);
            bottom.Controls.Add(buttons, 0, 2);
            CancelButton = btClose;

            FlowLayoutPanel settings = new FlowLayoutPanel();
            settings.Dock = DockStyle.Fill;
            settings.AutoSize = true;
            settings.WrapContents = false;
            chkLimit = new CheckBox();
            chkLimit.Text = L("Limit manual backups to", "Limiter les sauvegardes manuelles à");
            chkLimit.AutoSize = true;
            chkLimit.Checked = ThemeSettings.ManualLimitOn;
            numManual = new NumericUpDown();
            numManual.Minimum = 1;
            numManual.Maximum = 500;
            numManual.Value = Math.Min(500, Math.Max(1, ThemeSettings.ManualMax));
            numManual.Width = u * 5;
            numManual.Enabled = chkLimit.Checked;
            Label lFav = new Label();
            lFav.Text = L("(favorites are not counted and never deleted)", "(les favoris ne comptent pas et ne sont jamais supprimés)");
            lFav.AutoSize = true;
            lFav.Margin = new Padding(u / 2, 6, u, 0);
            Label lInj = new Label();
            lInj.Text = L("Maximum before-injection copies:", "Maximum de copies avant injection :");
            lInj.AutoSize = true;
            lInj.Margin = new Padding(u, 6, 3, 0);
            numInject = new NumericUpDown();
            numInject.Minimum = 1;
            numInject.Maximum = 500;
            numInject.Value = Math.Min(500, Math.Max(1, ThemeSettings.InjectMax));
            numInject.Width = u * 5;
            settings.Controls.Add(chkLimit);
            settings.Controls.Add(numManual);
            settings.Controls.Add(lInj);
            settings.Controls.Add(numInject);
            bottom.Controls.Add(settings, 0, 3);
            lFav.Margin = new Padding(u / 2, 0, 0, u / 3);
            bottom.Controls.Add(lFav, 0, 4);
            Controls.Add(bottom);

            // --- haut : dossier save-0 et catégorie ---
            TableLayoutPanel top = new TableLayoutPanel();
            top.Dock = DockStyle.Top;
            top.AutoSize = true;
            top.ColumnCount = 3;
            top.RowCount = 2;
            top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            top.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            top.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            top.Padding = new Padding(u / 2);
            Label l1 = new Label();
            l1.Text = L("save-0 folder:", "Dossier save-0 :");
            l1.AutoSize = true;
            l1.Anchor = AnchorStyles.Left;
            top.Controls.Add(l1, 0, 0);
            tbDir = new TextBox();
            tbDir.ReadOnly = true;
            tbDir.Dock = DockStyle.Fill;
            tbDir.Text = ThemeSettings.ManualDir ?? "";
            top.Controls.Add(tbDir, 1, 0);
            Button btDir = new Button();
            btDir.Text = L("Choose...", "Choisir...");
            btDir.AutoSize = true;
            btDir.Click += delegate { ChooseDir(); };
            top.Controls.Add(btDir, 2, 0);
            Label l2 = new Label();
            l2.Text = L("Show:", "Afficher :");
            l2.AutoSize = true;
            l2.Anchor = AnchorStyles.Left;
            top.Controls.Add(l2, 0, 1);
            cmbCat = new ComboBox();
            cmbCat.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbCat.Dock = DockStyle.Fill;
            cmbCat.Items.Add(L("Manual backups", "Sauvegardes manuelles"));
            cmbCat.Items.Add(L("Automatic copies", "Copies automatiques"));
            cmbCat.Items.Add(L("Before injection", "Avant injection"));
            cmbCat.Items.Add(L("Transfer copies (one file)", "Copies de transfert (un seul fichier)"));
            cmbCat.Items.Add(L("Copies before saving (one file)", "Copies avant enregistrement (un seul fichier)"));
            cmbCat.SelectedIndex = 0;
            cmbCat.SelectedIndexChanged += delegate { RefreshList(); };
            top.Controls.Add(cmbCat, 1, 1);
            Controls.Add(top);

            // --- actions ---
            btNew.Click += delegate { DoNew(); };
            btFav.Click += delegate { DoFav(); };
            btDel.Click += delegate { DoDelete(); };
            btInject.Click += delegate { DoInject(); };
            btOpen.Click += delegate { DoOpen(); };
            btNoteOk.Click += delegate { DoNoteOk(); };
            btNoteDel.Click += delegate { DoNoteDel(); };
            tbNote.TextChanged += delegate { if (!loadingNote) UpdateButtons(); };

            // Clic droit sur une sauvegarde : mêmes actions que les boutons.
            ctx = new ContextMenuStrip();
            ctx.Renderer = ThemeSettings.MenuRenderer(); // mêmes couleurs que les menus du programme (plus de carrés blancs)
            ctx.ShowImageMargin = false;                 // pas de bande blanche à gauche
            ctxFav = new ToolStripMenuItem();
            ctxFav.Click += delegate { DoFav(); };
            ctxNote = new ToolStripMenuItem();
            ctxNote.Click += delegate
            {
                tbNote.Focus();
                tbNote.SelectionStart = tbNote.TextLength;
            };
            ToolStripMenuItem ctxDel = new ToolStripMenuItem(L("Delete the backup", "Supprimer la sauvegarde"));
            ctxDel.Click += delegate { DoDelete(); };
            ToolStripMenuItem ctxInject = new ToolStripMenuItem(L("Inject into save-0", "Injecter dans save-0"));
            ctxInject.Click += delegate { DoInject(); };
            ToolStripMenuItem ctxOpen = new ToolStripMenuItem(L("Open this folder", "Ouvrir ce dossier"));
            ctxOpen.Click += delegate { DoOpenCopy(); };
            ctx.Items.Add(ctxFav);
            ctxNoteDel = new ToolStripMenuItem(L("Delete the note", "Supprimer la note"));
            ctxNoteDel.Click += delegate { DoNoteDel(); };
            ctx.Items.Add(ctxNote);
            ctx.Items.Add(ctxNoteDel);
            ctx.Items.Add(ctxDel);
            ctx.Items.Add(ctxInject);
            ctx.Items.Add(ctxOpen);
            ctx.Opening += delegate (object sender, System.ComponentModel.CancelEventArgs ce)
            {
                if (cur == null) { ce.Cancel = true; return; }
                ctxFav.Visible = cur.Cat == 0;
                ctxNote.Visible = cur.Cat == 0;
                ctxNoteDel.Visible = cur.Cat == 0 && cur.Item.Note.Trim().Length > 0;
                ctxNote.Text = (cur.Cat == 0 && cur.Item.Note.Trim().Length > 0) ? L("Edit the note", "Modifier la note") : L("Add a note", "Ajouter une note");
                ctxFav.Text = (cur.Cat == 0 && cur.Item.Fav) ? L("Remove from favorites ★", "Retirer des favoris ★") : L("Make favorite ★", "Mettre en favori ★");
            };
            list.ContextMenuStrip = ctx;
            list.MouseDown += delegate (object sender, MouseEventArgs me)
            {
                if (me.Button != MouseButtons.Right) return;
                ListViewItem hit = list.GetItemAt(me.X, me.Y);
                if (hit != null) hit.Selected = true;
                else
                {
                    foreach (ListViewItem sel in new List<ListViewItem>(list.SelectedItems.Cast<ListViewItem>())) sel.Selected = false;
                }
            };
            chkLimit.CheckedChanged += delegate
            {
                numManual.Enabled = chkLimit.Checked;
                ThemeSettings.ManualLimitOn = chkLimit.Checked;
                ThemeSettings.Save();
            };
            numManual.ValueChanged += delegate
            {
                ThemeSettings.ManualMax = (int)numManual.Value;
                ThemeSettings.Save();
            };
            numInject.ValueChanged += delegate
            {
                ThemeSettings.InjectMax = (int)numInject.Value;
                ThemeSettings.Save();
            };

            RefreshList();
            ThemeSettings.Apply(this);
            ctx.BackColor = list.BackColor;
            ctx.ForeColor = list.ForeColor;
            foreach (ToolStripItem ti in ctx.Items)
            {
                ti.BackColor = list.BackColor;
                ti.ForeColor = list.ForeColor;
            }
        }

        static Button MakeButton(string text, int u)
        {
            Button b = new Button();
            b.Text = text;
            b.AutoSize = true;
            b.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            b.Padding = new Padding(u / 3, 2, u / 3, 2);
            return b;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            SaveNoteIfChanged();
            base.OnFormClosing(e);
        }

        static string SizeText(long size)
        {
            if (size < 1024L * 1024L) return (size / 1024.0).ToString("0.#") + " Ko";
            return (size / 1048576.0).ToString("0.#") + " Mo";
        }

        static string FirstLine(string note)
        {
            if (string.IsNullOrEmpty(note)) return "";
            return note.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
        }

        // ---------- liste ----------

        void RefreshList()
        {
            SaveNoteIfChanged();
            string keep = cur != null ? cur.Location : null;
            rows.Clear();
            list.Items.Clear();
            cur = null;
            int cat = cmbCat.SelectedIndex;
            try
            {
                if (cat >= 0 && cat <= 2)
                {
                    string root = cat == 0 ? ManualBackup.ManualFolder : (cat == 1 ? AutoBackup.Folder : ManualBackup.InjectFolder);
                    foreach (ManualBackup.Item it in ManualBackup.List(root))
                    {
                        Row r = new Row();
                        r.Cat = cat;
                        r.IsFolder = true;
                        r.Location = it.Dir;
                        r.Item = it;
                        r.When = it.When;
                        rows.Add(r);
                        ListViewItem lv = new ListViewItem((cat == 0 && it.Fav) ? "★" : "");
                        lv.SubItems.Add(it.When);
                        lv.SubItems.Add(it.Files.ToString() + L(" files, ", " fichiers, ") + SizeText(it.Size));
                        lv.SubItems.Add(FirstLine(it.Note));
                        lv.Tag = r;
                        list.Items.Add(lv);
                    }
                }
                else
                {
                    List<BackupManager.Entry> es = cat == 3 ? BackupManager.ListTransfer() : BackupManager.ListNormal();
                    foreach (BackupManager.Entry e in es)
                    {
                        Row r = new Row();
                        r.Cat = cat;
                        r.IsFolder = false;
                        r.Location = e.Location;
                        r.Entry = e;
                        r.When = e.When;
                        rows.Add(r);
                        ListViewItem lv = new ListViewItem("");
                        lv.SubItems.Add(e.When);
                        lv.SubItems.Add(e.Name + ", " + SizeText(e.Size));
                        lv.SubItems.Add(FirstLine(e.Note));
                        lv.Tag = r;
                        list.Items.Add(lv);
                    }
                }
            }
            catch (Exception) { }
            if (keep != null)
            {
                foreach (ListViewItem lv in list.Items)
                {
                    if (((Row)lv.Tag).Location == keep) { lv.Selected = true; lv.EnsureVisible(); break; }
                }
            }
            if (list.SelectedItems.Count == 0) OnSelect();
        }

        void OnSelect()
        {
            SaveNoteIfChanged();
            cur = list.SelectedItems.Count > 0 ? (Row)list.SelectedItems[0].Tag : null;
            loadingNote = true;
            string note = "";
            if (cur != null) note = cur.IsFolder ? cur.Item.Note : cur.Entry.Note;
            tbNote.Text = note ?? "";
            loadingNote = false;
            UpdateButtons();
        }

        void UpdateButtons()
        {
            bool has = cur != null;
            bool manual = has && cur.Cat == 0;
            btFav.Enabled = manual;
            btFav.Text = (manual && cur.Item.Fav) ? L("Remove from favorites ★", "Retirer des favoris ★") : L("Make favorite ★", "Mettre en favori ★");
            btDel.Enabled = has;
            btInject.Enabled = has;
            tbNote.ReadOnly = !manual;
            btNoteOk.Enabled = manual;
            btNoteDel.Enabled = manual && (tbNote.Text.Trim().Length > 0 || cur.Item.Note.Trim().Length > 0);
        }

        void SaveNoteIfChanged()
        {
            if (loadingNote || cur == null || cur.Cat != 0 || cur.Item == null) return;
            string text = tbNote.Text;
            if (text == cur.Item.Note) return;
            try
            {
                ManualBackup.WriteNote(cur.Item, text);
                foreach (ListViewItem lv in list.Items)
                {
                    if (lv.Tag == cur) { lv.SubItems[3].Text = FirstLine(cur.Item.Note); break; }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // ---------- dossier save-0 ----------

        bool ChooseDir()
        {
            string start = ThemeSettings.ManualDir;
            if (string.IsNullOrEmpty(start) || !Directory.Exists(start)) start = ThemeSettings.AutoDir;
            string picked = FolderPicker.Pick(this,
                L("Choose your save-0 folder (used for manual backups and injection). The program remembers it.",
                  "Choisissez votre dossier save-0 (utilisé pour les sauvegardes manuelles et l'injection). Le programme s'en souvient."),
                start);
            if (picked == null) return false;
            ThemeSettings.ManualDir = picked;
            ThemeSettings.Save();
            tbDir.Text = picked;
            return true;
        }

        bool EnsureDir()
        {
            string d = ThemeSettings.ManualDir;
            if (!string.IsNullOrEmpty(d) && Directory.Exists(d)) return true;
            return ChooseDir();
        }

        // ---------- actions ----------

        void DoNew()
        {
            if (!EnsureDir()) return;
            SaveNoteIfChanged();
            ManualBackup.CopyResult r;
            Cursor old = Cursor;
            Cursor = Cursors.WaitCursor;
            try { r = ManualBackup.CreateManual(ThemeSettings.ManualDir); }
            finally { Cursor = old; }
            if (!r.Ok)
            {
                MessageBox.Show(this, L("The backup could not be made: ", "La sauvegarde n'a pas pu être faite : ") + r.Error,
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            cur = null;
            if (cmbCat.SelectedIndex != 0) cmbCat.SelectedIndex = 0;
            else RefreshList();
            foreach (ListViewItem lv in list.Items)
            {
                if (((Row)lv.Tag).Location == r.Dir) { lv.Selected = true; lv.EnsureVisible(); break; }
            }
            if (r.Failed > 0)
                MessageBox.Show(this, r.Failed.ToString() + L(" item(s) could not be copied.", " élément(s) n'ont pas pu être copiés."),
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            tbNote.Focus();
        }

        void DoFav()
        {
            if (cur == null || cur.Cat != 0) return;
            try
            {
                ManualBackup.SetFav(cur.Item, !cur.Item.Fav);
                foreach (ListViewItem lv in list.Items)
                {
                    if (lv.Tag == cur) { lv.SubItems[0].Text = cur.Item.Fav ? "★" : ""; break; }
                }
                UpdateButtons();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void DoDelete()
        {
            if (cur == null) return;
            string extra = (cur.Cat == 0 && cur.Item.Fav) ? L("\n\nThis copy is a favorite.", "\n\nCette copie est en favori.") : "";
            if (MessageBox.Show(this,
                    L("Delete the copy of ", "Supprimer la copie du ") + cur.When + L("? This cannot be undone.", " ? C'est définitif.") + extra,
                    Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try
            {
                if (cur.IsFolder) ManualBackup.Delete(cur.Item);
                else BackupManager.Delete(cur.Entry);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            cur = null;
            RefreshList();
        }

        // Bouton « Valider la note » : enregistre la note tout de suite et le confirme un instant.
        void DoNoteOk()
        {
            if (cur == null || cur.Cat != 0) return;
            SaveNoteIfChanged();
            string normal = L("Confirm the note", "Valider la note");
            btNoteOk.Text = L("Note saved ✓", "Note enregistrée ✓");
            System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
            t.Interval = 1500;
            t.Tick += delegate
            {
                t.Stop();
                t.Dispose();
                btNoteOk.Text = normal;
            };
            t.Start();
        }

        // Bouton « Supprimer la note » : efface la note de la sauvegarde sélectionnée.
        void DoNoteDel()
        {
            if (cur == null || cur.Cat != 0) return;
            tbNote.Text = "";
            SaveNoteIfChanged();
            UpdateButtons();
        }

        // Clic droit > « Ouvrir ce dossier » : ouvre le dossier de la copie sélectionnée.
        void DoOpenCopy()
        {
            if (cur == null) return;
            try
            {
                if (cur.IsFolder || (cur.Entry != null && !cur.Entry.IsLegacy)) Process.Start(cur.Location);
                else Process.Start("explorer.exe", "/select,\"" + cur.Location + "\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void DoOpen()
        {
            string dir;
            switch (cmbCat.SelectedIndex)
            {
                case 0: dir = ManualBackup.ManualFolder; break;
                case 1: dir = AutoBackup.Folder; break;
                case 2: dir = ManualBackup.InjectFolder; break;
                case 3: dir = BackupManager.TransferFolder; break;
                default: dir = BackupManager.Folder; break;
            }
            try
            {
                Directory.CreateDirectory(dir);
                Process.Start(dir);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void DoInject()
        {
            if (cur == null) return;
            string title = L("Inject a copy into save-0", "Injecter une copie dans save-0");
            if (!EnsureDir()) return;
            string target = ThemeSettings.ManualDir;
            if (!Directory.Exists(target))
            {
                MessageBox.Show(this, L("The save-0 folder was not found: ", "Le dossier save-0 est introuvable : ") + target,
                    title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!string.IsNullOrEmpty(ThemeSettings.AutoExe) && AutoBackup.IsRunning(ThemeSettings.AutoExe))
            {
                MessageBox.Show(this,
                    L("The game seems to be open (" + ThemeSettings.AutoExe + ".exe). Close it before injecting a copy.",
                      "Le jeu semble ouvert (" + ThemeSettings.AutoExe + ".exe). Fermez-le avant d'injecter une copie."),
                    title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            string unloadNote = L("\n\nIf the save open in the editor is in this folder, it is unloaded (changes not yet saved are lost).",
                                  "\n\nSi la sauvegarde ouverte dans l'éditeur est dans ce dossier, elle est déchargée (les modifications non enregistrées sont perdues).");
            ManualBackup.InjectResult res;
            if (cur.IsFolder)
            {
                int n;
                long size;
                ManualBackup.Count(cur.Location, out n, out size);
                if (MessageBox.Show(this,
                        L("The copy of ", "La copie du ") + cur.When + L(" will be injected into:\n", " va être injectée dans :\n") + target
                        + L("\n\nIts " + n.ToString() + " file(s) overwrite the files with the same name; the other files of the folder are kept.\nBefore that, the files that will be replaced are copied to the before-injection folder, so you can go back.",
                            "\n\nSes " + n.ToString() + " fichier(s) écrasent ceux du même nom ; les autres fichiers du dossier sont gardés.\nAvant, les fichiers qui vont être remplacés sont copiés dans le dossier « avant injection » : tu pourras revenir en arrière.")
                        + unloadNote + L("\n\nContinue?", "\n\nContinuer ?"),
                        title, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                res = ManualBackup.InjectFolderCopy(cur.Location, target);
            }
            else
            {
                BackupManager.Entry e = cur.Entry;
                string fname = e.IsLegacy ? (e.Name + e.Ext) : Path.GetFileName(e.DataFile);
                string t = Path.Combine(target, fname);
                try
                {
                    if (File.Exists(t) && new FileInfo(t).Length != new FileInfo(e.DataFile).Length)
                    {
                        MessageBox.Show(this,
                            L("This copy does not have the same size as the file \"" + fname + "\" in save-0: it is not from the same game. Nothing was changed.",
                              "Cette copie n'a pas la même taille que le fichier « " + fname + " » de save-0 : ce n'est pas la même version de jeu. Rien n'a été modifié."),
                            title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }
                catch (Exception) { }
                if (MessageBox.Show(this,
                        L("Careful: this copy contains only ONE file (\"" + fname + "\", the system). Only this file is replaced in:\n",
                          "Attention : cette copie ne contient qu'UN seul fichier (« " + fname + " », le système). Seul ce fichier est remplacé dans :\n") + target
                        + L("\n\nEverything else in the folder is kept (mods, game backup...).\nThe file that will be replaced is copied to the before-injection folder first, so you can go back.",
                            "\n\nTout le reste du dossier est gardé (mods, backup du jeu...).\nLe fichier qui va être remplacé est d'abord copié dans le dossier « avant injection » : tu pourras revenir en arrière.")
                        + unloadNote + L("\n\nContinue?", "\n\nContinuer ?"),
                        title, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                res = ManualBackup.InjectSingleFile(e.DataFile, target, fname);
            }
            if (!res.Ok)
            {
                MessageBox.Show(this, L("The injection did not finish: ", "L'injection ne s'est pas terminée : ") + res.Error
                    + (res.Failed > 0 ? "\n" + res.Failed.ToString() + L(" file(s) could not be written.", " fichier(s) n'ont pas pu être écrits.") : ""),
                    title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else
            {
                MessageBox.Show(this,
                    L("Done: ", "C'est fait : ") + res.Written.ToString() + L(" file(s) written, ", " fichier(s) écrit(s), ") + res.Replaced.ToString()
                    + L(" replaced.", " remplacé(s).")
                    + (res.BeforeDir != null ? L("\n\nBefore injection copy:\n", "\n\nCopie avant injection :\n") + res.BeforeDir : ""),
                    title, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            if (afterInject != null) afterInject(target);
            RefreshList();
        }
    }
}
