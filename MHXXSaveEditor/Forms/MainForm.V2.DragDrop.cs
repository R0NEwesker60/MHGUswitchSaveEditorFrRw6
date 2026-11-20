// MHXXSwitchSaveEditor – version française (modifiée). Licence : GNU GPL v3 (voir LICENSE).
// Fichier créé en version 34 : morceau de la fenêtre principale (classe partielle MainForm).
// Contenu : glisser-déposer d'un fichier de sauvegarde sur la fenêtre pour l'ouvrir (comme Fichier > Charger).
using MHXXSaveEditor.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace MHXXSaveEditor
{
    public partial class MainForm : Form
    {
        readonly HashSet<Control> dropHooked = new HashSet<Control>();

        // Autorise le dépôt de fichiers sur ce contrôle et sur tous ceux qu'il contient (y compris ceux ajoutés plus tard).
        // Les champs de saisie sont laissés tranquilles : glisser du texte dedans continue de fonctionner normalement.
        void HookDragDrop(Control c)
        {
            if (c == null) return;
            if (c is TextBoxBase || c is ComboBox || c is UpDownBase) return;
            if (!dropHooked.Add(c)) return;
            c.AllowDrop = true;
            c.DragEnter += DropSave_DragEnter;
            c.DragDrop += DropSave_DragDrop;
            c.ControlAdded += (s, e) => HookDragDrop(e.Control);
            foreach (Control child in c.Controls) HookDragDrop(child);
        }

        void DropSave_DragEnter(object sender, DragEventArgs e)
        {
            bool isFile = e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop);
            e.Effect = isFile ? DragDropEffects.Copy : DragDropEffects.None;
        }

        void DropSave_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = null;
            try { files = e.Data.GetData(DataFormats.FileDrop) as string[]; }
            catch { }
            if (files == null || files.Length == 0) return;
            // Le travail se fait après la fin du dépôt : sinon l'Explorateur Windows reste figé tant qu'une fenêtre est ouverte.
            BeginInvoke(new Action(() => OpenDroppedFile(files)));
        }

        void OpenDroppedFile(string[] files)
        {
            if (files.Length != 1)
            {
                MessageBox.Show(this,
                    RomfsConfig.L("Please drop one save file at a time.", "Déposez un seul fichier de sauvegarde à la fois."),
                    RomfsConfig.L("Drag and drop", "Glisser-déposer"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string path = files[0];
            long length;
            try
            {
                if (Directory.Exists(path)) throw new IOException("dossier");
                length = new FileInfo(path).Length;
                // Test de lecture avant de toucher à quoi que ce soit : si le fichier est verrouillé, rien n'est modifié.
                using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) { }
            }
            catch (Exception)
            {
                MessageBox.Show(this,
                    RomfsConfig.L("This file cannot be read. Nothing was changed.", "Ce fichier ne peut pas être lu. Rien n'a été modifié."),
                    RomfsConfig.L("Drag and drop", "Glisser-déposer"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            // Même tailles acceptées que par Fichier > Charger (3DS, MHXX Switch, MHGU Switch).
            if (length != 4726152 && length != SWITCH_SAVE_SIZE && length != MHGU_SAVE_SIZE)
            {
                MessageBox.Show(this,
                    RomfsConfig.L("This file is not a recognized save file (wrong size). Nothing was changed.",
                                  "Ce fichier n'est pas une sauvegarde reconnue (taille incorrecte). Rien n'a été modifié."),
                    RomfsConfig.L("Drag and drop", "Glisser-déposer"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!ConfirmUnsaved("loading another save", "charger une autre sauvegarde")) return;
            LoadSaveFile(path, Path.GetFileName(path));
        }
    }
}
