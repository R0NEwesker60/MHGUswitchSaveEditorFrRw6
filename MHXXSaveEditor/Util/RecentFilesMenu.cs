// MHXXSwitchSaveEditor – version française (modifiée). Licence : GNU GPL v3 (voir LICENSE).
// Fichier ajouté lors de la version 26 ; version 27 : deux menus séparés (Fichiers récents / Fichiers favoris).
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace MHXXSaveEditor.Util
{
    // Deux listes de fichiers de sauvegarde, dans deux menus séparés :
    //  - les RÉCENTS : les 10 derniers fichiers ouverts ;
    //  - les FAVORIS : gardés tant qu'on ne les retire pas (jamais effacés automatiquement).
    // Clic droit sur un chemin : mettre en favori / retirer des favoris / retirer des récents.
    // Les chemins sont affichés en entier (pas de « ... ») ; un chemin très long prend deux lignes.
    static class RecentFilesMenu
    {
        static string L(string en, string fr)
        {
            return RomfsConfig.L(en, fr);
        }

        public static void FillRecents(ToolStripItemCollection items, Action<string> onPick)
        {
            items.Clear();
            Action refresh = delegate { FillRecents(items, onPick); };
            int count = 0;
            foreach (string p in ThemeSettings.Recent.ToArray())
            {
                if (!File.Exists(p)) continue;
                AddPath(items, p, onPick, refresh);
                count++;
            }
            if (count == 0)
            {
                ToolStripMenuItem none = new ToolStripMenuItem(L("(none yet)", "(aucun pour l'instant)"));
                none.Enabled = false;
                items.Add(none);
                return;
            }
            items.Add(new ToolStripSeparator());
            ToolStripMenuItem clear = new ToolStripMenuItem(L("Clear the recent files", "Vider les fichiers récents"));
            clear.Click += delegate
            {
                ThemeSettings.ClearRecent();
                refresh();
            };
            items.Add(clear);
        }

        public static void FillFavorites(ToolStripItemCollection items, Action<string> onPick, string currentFile)
        {
            items.Clear();
            Action refresh = delegate { FillFavorites(items, onPick, currentFile); };
            if (ThemeSettings.Favorites.Count == 0)
            {
                ToolStripMenuItem none = new ToolStripMenuItem(L("(none: right-click a recent file > Add to favorites)", "(aucun : clic droit sur un fichier récent > Mettre en favori)"));
                none.Enabled = false;
                items.Add(none);
            }
            foreach (string f in ThemeSettings.Favorites.ToArray())
                AddPath(items, f, onPick, refresh);

            bool canAddCurrent = !string.IsNullOrEmpty(currentFile) && File.Exists(currentFile) && !ThemeSettings.IsFavorite(currentFile);
            if (canAddCurrent)
            {
                items.Add(new ToolStripSeparator());
                ToolStripMenuItem addCur = new ToolStripMenuItem(L("Add the open file to favorites", "Mettre le fichier ouvert en favori"));
                addCur.Click += delegate
                {
                    ThemeSettings.AddFavorite(currentFile);
                    refresh();
                };
                items.Add(addCur);
            }
        }

        // Un chemin = une ligne de menu (deux si le chemin est très long ; les deux lignes font la même chose).
        static void AddPath(ToolStripItemCollection items, string path, Action<string> onPick, Action refresh)
        {
            string[] lines = ThemeSettings.MenuPathLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string text = lines[i];
                if (i == lines.Length - 1 && !File.Exists(path)) text += "   " + L("(not found)", "(introuvable)");
                ToolStripMenuItem it = new ToolStripMenuItem(text.Replace("&", "&&"));
                it.ToolTipText = path;
                string p = path;
                it.Click += delegate { onPick(p); };
                it.MouseUp += delegate (object sender, MouseEventArgs e)
                {
                    if (e.Button == MouseButtons.Right) ShowItemMenu(p, refresh);
                };
                items.Add(it);
            }
        }

        static void ShowItemMenu(string path, Action refresh)
        {
            ContextMenuStrip cm = new ContextMenuStrip();
            ThemeSettings.StyleContextMenu(cm);
            if (ThemeSettings.IsFavorite(path))
            {
                ToolStripMenuItem rm = new ToolStripMenuItem(L("Remove from favorites", "Retirer des favoris"));
                rm.Click += delegate
                {
                    ThemeSettings.RemoveFavorite(path);
                    refresh();
                };
                cm.Items.Add(rm);
            }
            else
            {
                ToolStripMenuItem add = new ToolStripMenuItem(L("Add to favorites", "Mettre en favori"));
                add.Click += delegate
                {
                    ThemeSettings.AddFavorite(path);
                    refresh();
                };
                cm.Items.Add(add);
            }
            ToolStripMenuItem rmRecent = new ToolStripMenuItem(L("Remove from the recent files", "Retirer des fichiers récents"));
            rmRecent.Click += delegate
            {
                ThemeSettings.RemoveRecent(path);
                refresh();
            };
            cm.Items.Add(rmRecent);
            cm.Show(Cursor.Position);
        }
    }
}
