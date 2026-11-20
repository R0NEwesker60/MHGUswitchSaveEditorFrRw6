// MHXXSwitchSaveEditor – version française (modifiée). Licence : GNU GPL v3 (voir LICENSE).
// Fichier créé en version 33 : morceau de la fenêtre principale (classe partielle MainForm), sans changement de comportement.
// Contenu : Les 3 destinations, aide, message « sauvegarde détectée », petites fenêtres de saisie.
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
        // En bas à gauche de l'onglet Général, au-dessus du message GitHub : le format de sauvegarde détecté.
        void UpdateDetectedLabel()
        {
            if (lblDetected == null || lblIssueHint == null || tabControlMain == null || generalTab == null) return;
            bool show = !string.IsNullOrEmpty(detectedText) && saveFile != null && tabControlMain.SelectedTab == generalTab;
            lblDetected.Visible = show;
            if (!show) return;
            lblDetected.Text = detectedText;
            lblDetected.ForeColor = ThemeSettings.Dark ? Color.FromArgb(120, 230, 130) : Color.FromArgb(20, 120, 40);
            lblDetected.BackColor = Color.Transparent;
            lblDetected.Location = new Point(lblIssueHint.Left, lblIssueHint.Top - lblDetected.Height - 6);
            lblDetected.BringToFront();
        }

        string DestLabel(int i)
        {
            string n = ThemeSettings.DestName[i];
            return string.IsNullOrEmpty(n) ? RomfsConfig.L("Destination ", "Destination ") + (i + 1).ToString() : n;
        }

        void CreateDestButtons()
        {
            destButtons = new Button[3];
            for (int i = 0; i < 3; i++)
            {
                int d = i;
                Button b = new Button();
                b.FlatStyle = FlatStyle.Flat;
                b.TextAlign = ContentAlignment.MiddleCenter;
                b.UseVisualStyleBackColor = false;
                b.TabStop = false;
                b.Visible = false;
                b.MouseUp += delegate (object sender, MouseEventArgs e)
                {
                    if (e.Button == MouseButtons.Left)
                    {
                        if (b.ClientRectangle.Contains(e.Location)) DestLeftClick(d);
                    }
                    else if (e.Button == MouseButtons.Right)
                    {
                        ShowDestMenu(d);
                    }
                };
                Controls.Add(b);
                destButtons[i] = b;
            }
            btnDestHelp = new Button();
            btnDestHelp.AutoSize = true;
            btnDestHelp.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            btnDestHelp.TabStop = false;
            btnDestHelp.Visible = false;
            btnDestHelp.Click += delegate { ShowDestHelp(); };
            Controls.Add(btnDestHelp);
        }

        // Placement : au milieu du haut de l'onglet Général (les trois boutons d'emplacement sont à gauche), bouton d'aide à droite.
        void UpdateDestButtons()
        {
            if (destButtons == null || tabControlMain == null || generalTab == null) return;
            bool show = tabControlMain.SelectedTab == generalTab;
            for (int i = 0; i < 3; i++) destButtons[i].Visible = show;
            btnDestHelp.Visible = show;
            if (!show) return;

            int pad = Math.Max(8, Font.Height);
            int gap = Math.Max(4, Font.Height / 3);
            int w = Font.Height * 10;
            for (int i = 0; i < 3; i++) w = Math.Max(w, TextRenderer.MeasureText(DestLabel(i), Font).Width + Font.Height * 2);
            int h = (int)(Font.Height * 2.2f);
            Size gs = generalTab.ClientSize;
            Color accent = ThemeSettings.Dark ? Color.FromArgb(60, 110, 170) : SystemColors.Highlight;
            for (int i = 0; i < 3; i++)
            {
                Button b = destButtons[i];
                string path = ThemeSettings.DestPath[i];
                bool defined = !string.IsNullOrEmpty(path);
                bool current = defined && !string.IsNullOrEmpty(filePath) && string.Equals(path, filePath, StringComparison.OrdinalIgnoreCase);
                b.Text = DestLabel(i);
                b.Size = new Size(w, h);
                b.BackColor = current ? accent : ThemeSettings.Field;
                b.ForeColor = current ? Color.White : (defined ? ThemeSettings.FieldFore : Color.Gray);
                if (!string.IsNullOrEmpty(ThemeSettings.DestColor[i]))
                {
                    try { b.ForeColor = ColorTranslator.FromHtml(ThemeSettings.DestColor[i]); }
                    catch (Exception) { }
                }
                b.FlatAppearance.BorderColor = current ? accent : (ThemeSettings.Dark ? Color.FromArgb(90, 92, 104) : SystemColors.ControlDark);
                b.FlatAppearance.MouseOverBackColor = current ? accent : (ThemeSettings.Dark ? Color.FromArgb(70, 72, 84) : SystemColors.ControlLight);
                b.Location = PointToClient(generalTab.PointToScreen(new Point((gs.Width - w) / 2, pad + i * (h + gap))));
                string tip = defined
                    ? path + "\n" + RomfsConfig.L("Click: load this file. Right-click: change, rename, clear.", "Clic : charger ce fichier. Clic droit : changer, renommer, effacer.")
                    : RomfsConfig.L("Not set yet. Click to choose the save file. Right-click for more.", "Pas encore défini. Cliquez pour choisir le fichier de sauvegarde. Clic droit pour plus d'options.");
                gfxTip.SetToolTip(b, tip);
                b.BringToFront();
            }
            btnDestHelp.Text = RomfsConfig.L("Help: favorites and destinations", "Aide : favoris et destinations");
            btnDestHelp.Location = PointToClient(generalTab.PointToScreen(new Point(gs.Width - btnDestHelp.Width - pad, pad)));
            btnDestHelp.BringToFront();
        }

        void DestLeftClick(int i)
        {
            string path = ThemeSettings.DestPath[i];
            if (string.IsNullOrEmpty(path))
            {
                if (DefineDestination(i)) LoadDestination(i);
                return;
            }
            LoadDestination(i);
        }

        // Choisit le fichier de la destination. Retourne false si la personne annule.
        bool DefineDestination(int i)
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Title = RomfsConfig.L("Choose the save file for: ", "Choisir le fichier de sauvegarde pour : ") + DestLabel(i);
                dlg.Filter = RomfsConfig.L("All files", "Tous les fichiers") + " (*.*)|*.*";
                string cur = ThemeSettings.DestPath[i];
                if (!string.IsNullOrEmpty(cur))
                {
                    try { dlg.InitialDirectory = Path.GetDirectoryName(cur); }
                    catch (Exception) { }
                }
                if (dlg.ShowDialog(this) != DialogResult.OK) return false;
                ThemeSettings.DestPath[i] = Path.GetFullPath(dlg.FileName);
            }
            ThemeSettings.Save();
            UpdateDestButtons();
            return true;
        }

        void LoadDestination(int i)
        {
            string path = ThemeSettings.DestPath[i];
            if (string.IsNullOrEmpty(path)) return;
            if (!File.Exists(path))
            {
                MessageBox.Show(this, RomfsConfig.L("This file no longer exists:\n", "Ce fichier n'existe plus :\n") + path,
                    DestLabel(i), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!ConfirmUnsaved("loading another save", "charger une autre sauvegarde")) return;
            LoadSaveFile(path, Path.GetFileName(path));
        }

        void ShowDestMenu(int i)
        {
            ContextMenuStrip cm = new ContextMenuStrip();
            ThemeSettings.StyleContextMenu(cm);
            ToolStripMenuItem def = new ToolStripMenuItem(RomfsConfig.L("Set / change the file...", "Définir / changer le fichier..."));
            def.Click += delegate { DefineDestination(i); };
            cm.Items.Add(def);
            ToolStripMenuItem useOpen = new ToolStripMenuItem(RomfsConfig.L("Put the currently open file in this destination", "Mettre le fichier actuellement ouvert dans cette destination"));
            useOpen.ToolTipText = RomfsConfig.L("No need to browse: this button will point to the save file that is open in the editor right now.",
                                                "Pas besoin de chercher le fichier : ce bouton pointera vers le fichier de sauvegarde qui est ouvert dans l'éditeur en ce moment.");
            useOpen.Enabled = !string.IsNullOrEmpty(filePath) && File.Exists(filePath);
            useOpen.Click += delegate
            {
                ThemeSettings.DestPath[i] = Path.GetFullPath(filePath);
                ThemeSettings.Save();
                UpdateDestButtons();
            };
            cm.Items.Add(useOpen);
            ToolStripMenuItem ren = new ToolStripMenuItem(RomfsConfig.L("Rename...", "Renommer..."));
            ren.Click += delegate
            {
                string v = PromptText(RomfsConfig.L("Rename the destination", "Renommer la destination"),
                                      RomfsConfig.L("Name shown on the button (leave empty for the default name):", "Nom affiché sur le bouton (laisser vide pour le nom par défaut) :"),
                                      ThemeSettings.DestName[i]);
                if (v == null) return;
                ThemeSettings.DestName[i] = v;
                ThemeSettings.Save();
                UpdateDestButtons();
            };
            cm.Items.Add(ren);
            ToolStripMenuItem col = new ToolStripMenuItem(RomfsConfig.L("Text color...", "Couleur des lettres..."));
            col.Click += delegate { ChooseDestColor(i); };
            cm.Items.Add(col);
            ToolStripMenuItem colReset = new ToolStripMenuItem(RomfsConfig.L("Default text color", "Couleur des lettres par défaut"));
            colReset.Enabled = !string.IsNullOrEmpty(ThemeSettings.DestColor[i]);
            colReset.Click += delegate
            {
                ThemeSettings.DestColor[i] = "";
                ThemeSettings.Save();
                UpdateDestButtons();
            };
            cm.Items.Add(colReset);
            cm.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem clear = new ToolStripMenuItem(RomfsConfig.L("Clear this destination", "Effacer cette destination"));
            clear.Click += delegate
            {
                ThemeSettings.DestName[i] = "";
                ThemeSettings.DestPath[i] = "";
                ThemeSettings.DestColor[i] = "";
                ThemeSettings.Save();
                UpdateDestButtons();
            };
            cm.Items.Add(clear);
            cm.Show(Cursor.Position);
        }

        // Choix de la couleur des lettres d'une destination.
        void ChooseDestColor(int i)
        {
            using (ColorDialog dlg = new ColorDialog())
            {
                dlg.FullOpen = true;
                dlg.AnyColor = true;
                try
                {
                    if (!string.IsNullOrEmpty(ThemeSettings.DestColor[i])) dlg.Color = ColorTranslator.FromHtml(ThemeSettings.DestColor[i]);
                    else dlg.Color = destButtons[i].ForeColor;
                }
                catch (Exception) { }
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                Color c = dlg.Color;
                ThemeSettings.DestColor[i] = string.Format("#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B);
                ThemeSettings.Save();
                UpdateDestButtons();
            }
        }

        // Petite fenêtre « écrire un texte ». Retourne null si la personne annule.
        string PromptText(string title, string label, string value)
        {
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
                Label lbl = new Label();
                lbl.Text = label;
                lbl.AutoSize = true;
                lbl.MaximumSize = new Size(u * 28, 0);
                lbl.Location = new Point(u, u);
                dlg.Controls.Add(lbl);
                Size sz = TextRenderer.MeasureText(label, dlg.Font, new Size(u * 28, 0), TextFormatFlags.WordBreak);
                TextBox tb = new TextBox();
                tb.Text = value ?? "";
                tb.MaxLength = 40;
                tb.Width = u * 20;
                tb.Location = new Point(u, u + sz.Height + u / 2);
                dlg.Controls.Add(tb);
                Button ok = new Button();
                ok.Text = "OK";
                ok.DialogResult = DialogResult.OK;
                ok.Size = new Size(u * 6, u * 2);
                ok.Location = new Point(u, tb.Bottom + u);
                dlg.Controls.Add(ok);
                Button cancel = new Button();
                cancel.Text = RomfsConfig.L("Cancel", "Annuler");
                cancel.DialogResult = DialogResult.Cancel;
                cancel.Size = new Size(u * 6, u * 2);
                cancel.Location = new Point(ok.Right + u, tb.Bottom + u);
                dlg.Controls.Add(cancel);
                dlg.ClientSize = new Size(Math.Max(Math.Max(sz.Width, tb.Width) + u * 2, cancel.Right + u), ok.Bottom + u);
                dlg.AcceptButton = ok;
                dlg.CancelButton = cancel;
                ThemeSettings.Apply(dlg);
                if (dlg.ShowDialog(this) != DialogResult.OK) return null;
                return tb.Text.Trim();
            }
        }

        // Bouton jaune en haut à droite de l'onglet Général : explique les favoris et les trois destinations.
        void ShowDestHelp()
        {
            string text = RomfsConfig.L(
                "QUICK ACCESS TO YOUR SAVE FILES\n\n" +
                "1) Recent files and favorites (File menu, top left)\n" +
                "- \"Recent files\": the last 10 files you opened.\n" +
                "- \"Favorite files\": the files you keep. To add one: right-click it in \"Recent files\" and choose \"Add to favorites\", or use \"Add the open file to favorites\" in \"Favorite files\". Right-click a favorite to remove it. Favorites never disappear on their own.\n\n" +
                "2) The 3 destinations (buttons at the top of this General tab)\n" +
                "- A destination is a shortcut button to ONE save file, with the name you want (for example \"MHGU Recomp\").\n" +
                "- Button not set yet: click it to choose the file (it is then loaded).\n" +
                "- Button set: click it to load that file directly (same as File > Load). The button of the file currently open is highlighted.\n" +
                "- Right-click a destination: set or change the file, put the currently open file in it (no need to browse), rename, choose the text color, clear.\n" +
                "- Everything is remembered from one launch to the next.",
                "ACCÈS RAPIDE À TES FICHIERS DE SAUVEGARDE\n\n" +
                "1) Fichiers récents et favoris (menu Fichier, en haut à gauche)\n" +
                "- « Fichiers récents » : les 10 derniers fichiers ouverts.\n" +
                "- « Fichiers favoris » : les fichiers que tu gardes. Pour en ajouter : clic droit sur un fichier dans « Fichiers récents » puis « Mettre en favori », ou « Mettre le fichier ouvert en favori » dans « Fichiers favoris ». Clic droit sur un favori pour le retirer. Les favoris ne disparaissent jamais tout seuls.\n\n" +
                "2) Les 3 destinations (boutons en haut de cet onglet Général)\n" +
                "- Une destination est un bouton raccourci vers UN fichier de sauvegarde, avec le nom que tu veux (par exemple « MHGU Recomp »).\n" +
                "- Bouton pas encore défini : clique dessus pour choisir le fichier (il est ensuite chargé).\n" +
                "- Bouton défini : clique dessus pour charger directement ce fichier (comme Fichier > Charger). Le bouton du fichier actuellement ouvert est mis en évidence.\n" +
                "- Clic droit sur une destination : définir ou changer le fichier, mettre dans la destination le fichier actuellement ouvert (pas besoin de le chercher), renommer, choisir la couleur des lettres, effacer.\n" +
                "- Tout est mémorisé d'un lancement à l'autre.");
            MessageBox.Show(this, text, RomfsConfig.L("Help: favorites and destinations", "Aide : favoris et destinations"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
