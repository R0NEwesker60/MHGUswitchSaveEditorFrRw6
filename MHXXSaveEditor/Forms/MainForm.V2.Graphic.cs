// MHXXSwitchSaveEditor – version française (modifiée). Licence : GNU GPL v3 (voir LICENSE).
// Fichier créé en version 33 : morceau de la fenêtre principale (classe partielle MainForm), sans changement de comportement.
// Contenu : Vue graphique, icônes HD, dossier ROMFS et message « interface graphique non activée ».
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
        // Retourne null si les icônes sont utilisables ; sinon, la raison (dans la langue choisie).
        string GfxProblem()
        {
            if (!ThemeSettings.GraphicView)
                return RomfsConfig.L("The graphic view is turned off (View menu > Graphic chest view).",
                                     "La vue graphique est décochée (menu Affichage > Vue graphique des coffres).");
            if (graphicFailed)
                return string.IsNullOrEmpty(graphicFailReason)
                    ? RomfsConfig.L("The game icons could not be found.", "Les icônes du jeu sont introuvables.")
                    : graphicFailReason;
            if (!GameAssets.Loaded && !RomfsConfig.HasFolder)
                return RomfsConfig.L("The ROMFS folder has not been chosen yet.", "Le dossier ROMFS n'a pas encore été choisi.");
            return null;
        }

        void CreateGfxPageHint(TabPage page, Control host)
        {
            Label lb = new Label();
            lb.AutoSize = true;
            lb.BackColor = Color.Transparent;
            lb.Cursor = Cursors.Help;
            lb.Visible = false;
            lb.Click += delegate { tabControlMain.SelectedTab = generalTab; };
            host.Controls.Add(lb);
            pageGfxHints[page] = lb;
        }

        // Les polices du message sont gardées (on les refait seulement si le zoom change).
        void EnsureGfxFonts()
        {
            if (gfxFontBig != null && Math.Abs(gfxFontBase - Font.Size) < 0.01f) return;
            gfxFontBase = Font.Size;
            gfxFontBig = new Font(Font.FontFamily, Font.Size * 1.15f, FontStyle.Bold);
            gfxFontSmall = new Font(Font.FontFamily, Font.Size * 0.9f, FontStyle.Bold);
        }

        // Onglet Général : message jaune + bouton d'explication en bas à droite.
        // Onglets coffre d'objets / équipement / équipement Palico (sans la grille) : petit message jaune avec « (?) ».
        void UpdateGfxHints()
        {
            if (lblGfxHint == null || btnGfxHelp == null || tabControlMain == null || generalTab == null) return;
            EnsureGfxFonts();
            string problem = GfxProblem();
            Color yellow = ThemeSettings.Dark ? Color.FromArgb(255, 230, 90) : Color.FromArgb(160, 100, 0);
            string title = RomfsConfig.L("Graphic interface not enabled", "Interface graphique non activée");

            bool showGeneral = problem != null && tabControlMain.SelectedTab == generalTab;
            lblGfxHint.Visible = showGeneral;
            btnGfxHelp.Visible = showGeneral;
            if (showGeneral)
            {
                lblGfxHint.ForeColor = yellow;
                lblGfxHint.BackColor = Color.Transparent;
                lblGfxHint.Font = gfxFontBig;
                lblGfxHint.TextAlign = ContentAlignment.TopRight;
                lblGfxHint.MaximumSize = new Size(Math.Max(240, ClientSize.Width * 2 / 5), 0);
                lblGfxHint.Text = title + "\n" + problem;
                btnGfxHelp.Text = RomfsConfig.L("How to turn it on?", "Comment l'activer ?");
                int pad = Math.Max(8, Font.Height);
                Size gs = generalTab.ClientSize;
                btnGfxHelp.Location = PointToClient(generalTab.PointToScreen(new Point(gs.Width - btnGfxHelp.Width - pad, gs.Height - btnGfxHelp.Height - pad)));
                lblGfxHint.Location = PointToClient(generalTab.PointToScreen(new Point(gs.Width - lblGfxHint.Width - pad, gs.Height - btnGfxHelp.Height - pad - lblGfxHint.Height - 6)));
                lblGfxHint.BringToFront();
                btnGfxHelp.BringToFront();
            }

            string tip = RomfsConfig.L("Go to the General tab: the full explanation is at the bottom right (click here to go there).",
                                       "Allez dans l'onglet Général : l'explication complète est en bas à droite (cliquez ici pour y aller).");
            if (problem != null) tip += "\n\n" + problem;
            foreach (PageLayout pl in pageLayouts)
            {
                if (pl.View == null) continue;
                Label lb;
                if (!pageGfxHints.TryGetValue(pl.Page, out lb)) continue;
                bool show = !GraphicOn();
                lb.Visible = show;
                if (!show) continue;
                Control host = lb.Parent;
                if (host == null) continue;
                lb.ForeColor = yellow;
                lb.BackColor = Color.Transparent;
                lb.Font = gfxFontSmall;

                // Place libre : à droite des contrôles du bas, au milieu de leur rangée.
                List<Control> row = new List<Control>();
                if (host == pl.Page) row.AddRange(pl.Bottom);
                else foreach (Control c in host.Controls) if (c != lb) row.Add(c);
                int left = 0;
                int top = int.MaxValue;
                int bottom = int.MinValue;
                foreach (Control c in row)
                {
                    if (c.Right > left) left = c.Right;
                    if (c.Top < top) top = c.Top;
                    if (c.Bottom > bottom) bottom = c.Bottom;
                }
                int right = host.ClientSize.Width - 10;
                int avail = right - (left + 8);
                if (avail < 60)
                {
                    lb.MaximumSize = Size.Empty;
                    lb.Text = "(?)";
                }
                else
                {
                    lb.MaximumSize = new Size(avail, 0);
                    lb.Text = title + " (?)";
                }
                int y = top == int.MaxValue ? host.ClientSize.Height - lb.Height - 8 : (top + bottom) / 2 - lb.Height / 2;
                lb.Location = new Point(right - lb.Width, Math.Max(2, y));
                lb.BringToFront();
                gfxTip.SetToolTip(lb, tip);
            }
        }

        // Bouton « Comment l'activer ? » : explication pas à pas.
        void ShowGraphicHelp()
        {
            string problem = GfxProblem();
            string text = RomfsConfig.L(
                "GRAPHIC INTERFACE (grid chests with icons)\n\n" +
                "To turn it on:\n" +
                "1. In the View menu, tick \"Graphic chest view\".\n" +
                "2. Choose the ROMFS folder of Monster Hunter Generations Ultimate (the one that contains the \"nativeNX\" folder). To change it later: View > Change ROMFS folder.\n\n" +
                "Files read in that folder (nothing is copied):\n" +
                "- nativeNX\\table\\itemData.itm\n" +
                "- the game's basic icons: nativeNX\\eng\\GUI\\99_texture\\HD_cmn_icon_GSM_NOMIP.tex (or the same file in the \"fre\" folder)\n\n" +
                "Sharper icons (HD): if you have the HD texture pack (V4), tick View > HD icons. The program looks for it in your ROMFS folder; if it is somewhere else (for example a separate \"mods\" folder), it asks you once to choose the pack's folder: the \"romfs\" folder inside \"HD UI v4\" (for example mods\\HD UI v4\\romfs). Do NOT choose \"arc\" or any folder inside it (nativeNX, fre, arc): the program finds the right file by itself. To change it later: View > Change HD pack folder. Without the pack, the game's basic icons are used.\n\n" +
                "If the files cannot be found, the program says so and shows the usual lists instead.",
                "INTERFACE GRAPHIQUE (coffres en grille avec icônes)\n\n" +
                "Pour l'activer :\n" +
                "1. Dans le menu Affichage, cochez « Vue graphique des coffres ».\n" +
                "2. Choisissez le dossier ROMFS de Monster Hunter Generations Ultimate (celui qui contient le dossier « nativeNX »). Pour le changer plus tard : Affichage > Changer le dossier ROMFS.\n\n" +
                "Fichiers lus dans ce dossier (rien n'est copié) :\n" +
                "- nativeNX\\table\\itemData.itm\n" +
                "- les icônes de base du jeu : nativeNX\\eng\\GUI\\99_texture\\HD_cmn_icon_GSM_NOMIP.tex (ou le même fichier dans le dossier « fre »)\n\n" +
                "Icônes plus nettes (HD) : si vous avez le pack de textures HD (V4), cochez Affichage > Icônes HD. Le programme le cherche dans votre dossier ROMFS ; s'il est ailleurs (par exemple dans un dossier « mods » à part), il vous demande une seule fois de choisir le dossier du pack : le dossier « romfs » qui est dans « HD UI v4 » (par exemple mods\\HD UI v4\\romfs). Ne choisissez PAS « arc » ni un dossier à l'intérieur (nativeNX, fre, arc) : le programme trouve tout seul le bon fichier. Pour le changer plus tard : Affichage > Changer le dossier du pack HD. Sans le pack, ce sont les icônes de base du jeu qui sont utilisées.\n\n" +
                "Si les fichiers sont introuvables, le programme le dit et affiche les listes habituelles à la place.");
            if (problem != null)
                text += "\n\n" + RomfsConfig.L("Current problem: ", "Problème actuel : ") + problem;
            text += "\n\n" + RomfsConfig.L("Choose the ROMFS folder now and turn the graphic view on?", "Choisir le dossier ROMFS maintenant et activer la vue graphique ?");
            if (MessageBox.Show(this, text, RomfsConfig.L("Graphic interface", "Interface graphique"), MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes) return;
            if (!ChangeRomfsFolder()) return;
            if (!ThemeSettings.GraphicView || graphicFailed)
            {
                ThemeSettings.GraphicView = true;
                ThemeSettings.Save();
                graphicFailed = false;
                graphicFailReason = null;
                EnsureGraphicReady();
                ApplyGraphicMode();
            }
        }

        // Vrai si les coffres graphiques sont utilisés. (On n'utilise PAS « Visible » : une page
        // d'onglet qui n'est pas affichée répond toujours « non visible », ce qui cassait la mise en page.)
        bool GraphicOn()
        {
            return ThemeSettings.GraphicView && !graphicFailed;
        }

        // Prépare les icônes (dossier ROMFS demandé au besoin). En cas d'échec, on garde les listes habituelles.
        void EnsureGraphicReady()
        {
            if (!ThemeSettings.GraphicView || graphicFailed || GameAssets.Loaded) return;
            string reason = null;
            if (!RomfsConfig.Ensure(this)) reason = RomfsConfig.L("The ROMFS folder was not chosen.", "Le dossier ROMFS n'a pas été choisi.");
            else if (!GameAssets.Load()) reason = RomfsConfig.L("Could not read the game icons: ", "Impossible de lire les icônes du jeu : ") + GameAssets.Error;
            if (reason != null)
            {
                graphicFailed = true;
                graphicFailReason = reason;
                ApplyGraphicMode();
                MessageBox.Show(this,
                    reason + "\n\n" + RomfsConfig.L("The usual lists are shown instead. You can try again from View > Graphic chest view.",
                                                   "Les listes habituelles sont affichées à la place. Vous pouvez réessayer depuis Affichage > Vue graphique des coffres."),
                    RomfsConfig.L("Graphic chest", "Coffre graphique"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        void ApplyGraphicMode()
        {
            bool on = ThemeSettings.GraphicView && !graphicFailed;
            if (menuGraphic != null) menuGraphic.Checked = on;
            SyncHdMenu();

            itemView.Visible = on;
            equipView.Visible = on;
            palicoView.Visible = on;
            listViewItem.Visible = !on;
            groupBox1.Visible = !on;

            // La liste d'équipement reste « vivante » (les listes du bas s'en servent) : on la pousse hors de la zone.
            if (on) listViewEquipment.Location = new Point(-5000, -5000);
            else listViewEquipment.Location = equipListLocation;
            if (on) listViewPalicoEquipment.Location = new Point(-5000, -5000);
            else listViewPalicoEquipment.Location = palicoListLocation;

            LayoutAll();
            RefreshViews();
        }

        void RefreshViews()
        {
            if (itemView != null && GraphicOn()) itemView.RefreshView();
            if (equipView != null && GraphicOn()) equipView.RefreshView();
            if (palicoView != null && GraphicOn()) palicoView.RefreshView();
        }

        // La case du menu montre ce qui est vraiment affiché (ou le réglage, tant que les icônes ne sont pas chargées).
        void SyncHdMenu()
        {
            if (menuHd == null) return;
            menuHd.Checked = GameAssets.Loaded ? GameAssets.HdActive : ThemeSettings.HdIcons;
        }

        // Active ou coupe les icônes HD. Le fichier du pack HD (resident_fre.arc) est cherché dans le dossier ROMFS ;
        // s'il n'y est pas, on demande à la personne de le choisir (une seule fois : le choix est mémorisé).
        void ToggleHdIcons(bool on)
        {
            if (!on)
            {
                ThemeSettings.HdIcons = false;
                ThemeSettings.Save();
                GameAssets.DisableHd();
                RefreshViews();
                SyncHdMenu();
                return;
            }

            ThemeSettings.HdIcons = true;
            ThemeSettings.Save();
            if (!GameAssets.Loaded) EnsureGraphicReady();
            if (!GameAssets.Loaded)
            {
                // Vue graphique coupée (ou icônes illisibles) : le réglage est mémorisé pour plus tard.
                SyncHdMenu();
                MessageBox.Show(this,
                    RomfsConfig.L("The HD icons will be used as soon as the graphic chest view is on.",
                                  "Les icônes HD seront utilisées dès que la vue graphique des coffres sera activée."),
                    RomfsConfig.L("HD icons", "Icônes HD"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!GameAssets.EnableHd(null))
            {
                MessageBox.Show(this,
                    RomfsConfig.L("The HD texture pack was not found in the ROMFS folder.\n\nChoose the \"romfs\" folder inside \"HD UI v4\" (for example mods\\HD UI v4\\romfs), not \"arc\". The program finds the right file by itself.",
                                  "Le pack de textures HD n'a pas été trouvé dans le dossier ROMFS.\n\nChoisissez le dossier « romfs » qui est dans « HD UI v4 » (par exemple mods\\HD UI v4\\romfs), et pas « arc ». Le programme trouve tout seul le bon fichier."),
                    RomfsConfig.L("HD icons", "Icônes HD"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                if (!ChangeHdPackFolder())
                {
                    ThemeSettings.HdIcons = false;
                    ThemeSettings.Save();
                    SyncHdMenu();
                    return;
                }
            }
            ThemeSettings.Save();
            RefreshViews();
            SyncHdMenu();
        }

        // Choisir un autre dossier ROMFS. Si la personne annule, RIEN ne change (les icônes déjà réglées restent).
        // Si le nouveau dossier ne contient pas les icônes, l'ancien est gardé. Retourne false si rien n'a été changé.
        bool ChangeRomfsFolder()
        {
            string oldRoot = RomfsConfig.NativeRoot;
            bool hadOld = RomfsConfig.HasFolder;
            if (!RomfsConfig.Choose(this)) return false;
            GameAssets.Reset();
            graphicFailed = false;
            graphicFailReason = null;
            if (ThemeSettings.GraphicView && !GameAssets.Load())
            {
                string err = GameAssets.Error;
                bool restored = false;
                if (hadOld)
                {
                    RomfsConfig.Set(oldRoot);
                    GameAssets.Reset();
                    restored = GameAssets.Load();
                }
                string msg = RomfsConfig.L("Could not read the game icons in this folder: ", "Impossible de lire les icônes du jeu dans ce dossier : ") + err + "\n\n";
                if (restored)
                {
                    msg += RomfsConfig.L("The previous ROMFS folder was kept.", "L'ancien dossier ROMFS a été conservé.");
                }
                else
                {
                    graphicFailed = true;
                    graphicFailReason = RomfsConfig.L("Could not read the game icons: ", "Impossible de lire les icônes du jeu : ") + err;
                    msg += RomfsConfig.L("The usual lists are shown instead.", "Les listes habituelles sont affichées à la place.");
                }
                MessageBox.Show(this, msg, RomfsConfig.L("Graphic chest", "Coffre graphique"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            ApplyGraphicMode();
            return true;
        }

        // V38 « Changer le dossier du pack HD... » : la personne choisit le dossier du pack (« romfs », ou « nativeNX » dedans),
        // le bon fichier est trouvé tout seul. Si la personne annule ou si rien n'est trouvé, RIEN ne change. Retourne vrai si les icônes HD sont activées.
        bool ChangeHdPackFolder()
        {
            if (!GameAssets.Loaded) EnsureGraphicReady();
            if (!GameAssets.Loaded)
            {
                MessageBox.Show(this,
                    RomfsConfig.L("Turn on the graphic chest view first (View menu), then choose the HD pack folder.",
                                  "Activez d'abord la vue graphique des coffres (menu Affichage), puis choisissez le dossier du pack HD."),
                    RomfsConfig.L("HD icons", "Icônes HD"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
            string folder;
            using (FolderBrowserDialog dlg = new FolderBrowserDialog())
            {
                dlg.Description = RomfsConfig.L("Choose the \"romfs\" folder inside \"HD UI v4\" (for example mods\\HD UI v4\\romfs). Do not choose \"arc\". The program finds the right file by itself.",
                                                "Choisissez le dossier « romfs » qui est dans « HD UI v4 » (par exemple mods\\HD UI v4\\romfs). Ne choisissez pas « arc ». Le programme trouve tout seul le bon fichier.");
                try
                {
                    if (!string.IsNullOrEmpty(ThemeSettings.HdPath))
                    {
                        string d = Path.GetDirectoryName(ThemeSettings.HdPath);
                        if (!string.IsNullOrEmpty(d) && Directory.Exists(d)) dlg.SelectedPath = d;
                    }
                }
                catch (Exception) { }
                if (dlg.ShowDialog(this) != DialogResult.OK) return false;
                folder = dlg.SelectedPath;
            }
            string file = GameAssets.FindHdFileIn(folder);
            if (file == null || !GameAssets.EnableHd(file))
            {
                MessageBox.Show(this,
                    RomfsConfig.L("No HD icons were found in this folder.\n\nChoose the \"romfs\" folder inside \"HD UI v4\" (for example mods\\HD UI v4\\romfs), not \"arc\".",
                                  "Aucune icône HD n'a été trouvée dans ce dossier.\n\nChoisissez le dossier « romfs » qui est dans « HD UI v4 » (par exemple mods\\HD UI v4\\romfs), et pas « arc »."),
                    RomfsConfig.L("HD icons", "Icônes HD"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            ThemeSettings.HdIcons = true;
            ThemeSettings.Save();
            RefreshViews();
            SyncHdMenu();
            return true;
        }

        // V38 « Oublier le pack HD enregistré » : seulement si la personne le demande (et confirme).
        void ForgetHdPack()
        {
            if (string.IsNullOrEmpty(ThemeSettings.HdPath) && !GameAssets.HdActive)
            {
                MessageBox.Show(this, RomfsConfig.L("No HD pack is saved.", "Aucun pack HD n'est enregistré."),
                    RomfsConfig.L("HD icons", "Icônes HD"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (MessageBox.Show(this,
                    RomfsConfig.L("Forget the saved HD pack? The game's basic icons will be used until you choose a pack again.",
                                  "Oublier le pack HD enregistré ? Les icônes de base du jeu seront utilisées jusqu'à ce que vous choisissiez de nouveau un pack."),
                    RomfsConfig.L("HD icons", "Icônes HD"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            GameAssets.ForgetHd();
            ThemeSettings.HdIcons = false;
            ThemeSettings.Save();
            RefreshViews();
            SyncHdMenu();
        }

        // « Oublier le dossier ROMFS enregistré » : seulement si la personne le demande (et confirme).
        void ForgetRomfsFolder()
        {
            if (RomfsConfig.NativeRoot == null)
            {
                MessageBox.Show(this, RomfsConfig.L("No ROMFS folder is saved.", "Aucun dossier ROMFS n'est enregistré."),
                    "ROMFS", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (MessageBox.Show(this,
                    RomfsConfig.L("Forget the saved ROMFS folder? The graphic view will be turned off until you choose a folder again.",
                                  "Oublier le dossier ROMFS enregistré ? La vue graphique sera désactivée jusqu'à ce que vous choisissiez de nouveau un dossier."),
                    "ROMFS", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            RomfsConfig.Forget();
            GameAssets.Reset();
            graphicFailed = true;
            graphicFailReason = RomfsConfig.L("The saved ROMFS folder was forgotten.", "Le dossier ROMFS enregistré a été oublié.");
            ApplyGraphicMode();
        }
    }
}
