// MHXXSwitchSaveEditor – version française (modifiée). Licence : GNU GPL v3 (voir LICENSE).
// Fichier créé en version 36 : morceau de la fenêtre principale (classe partielle MainForm).
// Contenu : barre de menus du haut. Les boutons « Modifier » et « Convertir » sont retirés de la barre
// (leur code reste dans le projet, simplement plus relié à la barre) et un bouton « Enregistrer »
// est placé juste à droite de « Fichier ».
using MHXXSaveEditor.Util;
using System;
using System.Windows.Forms;

namespace MHXXSaveEditor
{
    public partial class MainForm : Form
    {
        ToolStripMenuItem saveTopButton;

        void AddSaveTopButton()
        {
            saveTopButton = new ToolStripMenuItem(RomfsConfig.L("Save", "Enregistrer"));
            saveTopButton.Name = "saveTopButton";
            saveTopButton.Enabled = saveToolStripMenuItemSave.Enabled;
            // Même action que Fichier > Enregistrer (Ctrl+S reste sur ce menu).
            saveTopButton.Click += (s, e) => SaveToolStripMenuItemSave_Click(s, e);
            // Le bouton est grisé et activé en même temps que Fichier > Enregistrer.
            saveToolStripMenuItemSave.EnabledChanged += (s, e) => saveTopButton.Enabled = saveToolStripMenuItemSave.Enabled;

            int at = menuStripMain.Items.IndexOf(fileToolStripMenuItem);
            menuStripMain.Items.Insert(at + 1, saveTopButton);

            menuStripMain.Items.Remove(editToolStripMenuItem);
            menuStripMain.Items.Remove(convertToolStripMenuItem);
        }
    }
}
