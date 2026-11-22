// MHXXSwitchSaveEditor – version française (modifiée). Licence : GNU GPL v3 (voir LICENSE).
// Fichier ajouté lors de la V2 (coffre graphique), le 14 novembre 2026.
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
    // Partie « V2 » de la fenêtre principale :
    //  - le coffre graphique intégré aux onglets « Coffre à objets » et « Équipement » ;
    //  - la fenêtre redimensionnable : les listes et les grilles remplissent la place disponible ;
    //  - le zoom de toute l'application ;
    //  - le mode sombre.
    public partial class MainForm : Form
    {
        // Un onglet dont une liste (ou la grille qui la remplace) occupe la place libre,
        // les contrôles situés sous la liste restant collés en bas.
        class PageLayout
        {
            public TabPage Page;
            public ListView List;
            public ChestView View;
            public List<Control> Bottom = new List<Control>();
            public int MarginL, MarginT, MarginR, MarginB, Gap;
            public bool IgnoreBottomWhenGraphic;
        }

        static readonly int[] ZoomSteps = new int[] { 75, 100, 125, 150, 175, 200, 250, 300 };

        readonly List<PageLayout> pageLayouts = new List<PageLayout>();
        ChestView itemView;
        ChestView equipView;
        ChestView palicoView;
        Point palicoListLocation;
        bool listSelectBypass;
        Label lblLoadHint;
        Label lblIssueHint;
        public static bool BypassFocus;
        ToolStripMenuItem menuDarkMode;
        ToolStripMenuItem menuGraphic;
        ToolStripMenuItem menuHd;
        ToolStripMenuItem menuBackupOn;
        ToolStripMenuItem menuBackupFolder;
        ToolStripMenuItem menuBackupMax;
        ToolStripMenuItem menuZoom;
        float baseFontSize;
        float baseMenuFontSize;
        ToolStripMenuItem menuWeaponTransmog;
        ToolStripMenuItem menuWarnUnsaved;
        ToolStripMenuItem menuSwapSlots;
        ToolStripMenuItem[] swapItems;
        ToolStripMenuItem menuCopySlots;
        ToolStripMenuItem[] copySrc;
        ToolStripMenuItem[][] copyDst;
        Button[] slotButtons; // trois boutons (un par emplacement) en haut à gauche de l'onglet Général
        string cleanHash;
        Size baseMinimumSize;
        int tabMarginL, tabMarginT, tabMarginR, tabMarginB;
        int currentZoom = 100;
        bool graphicFailed;
        string graphicFailReason;
        Label lblGfxHint;
        Button btnGfxHelp;
        ToolTip gfxTip;
        readonly Dictionary<TabPage, Label> pageGfxHints = new Dictionary<TabPage, Label>();
        Font gfxFontBig;
        Font gfxFontSmall;
        float gfxFontBase;
        ToolStripMenuItem menuForgetRomfs;
        ToolStripMenuItem menuTransfer;
        ToolStripMenuItem menuRecent;
        ToolStripMenuItem menuColors;
        Label lblDetected;      // « Sauvegarde MHGU Switch détectée » (remplace l'ancienne fenêtre à fermer)
        string detectedText = "";
        ToolStripMenuItem menuRankColors;
        Button[] destButtons;   // trois destinations (boutons raccourcis vers un fichier system), en haut de l'onglet Général
        Button btnAutoHelp;     // 2e bouton d'aide (sauvegarde automatique), sous le premier
        Button btnDestHelp;     // bouton d'aide (en haut à droite de l'onglet Général)
        ToolStripMenuItem menuFavorites;
        ToolStripMenuItem menuBackupFolder2;
        ToolStripLabel lblVersion;
        bool v2Laying;
        bool v2Zooming;
        Point equipListLocation;
        readonly Dictionary<TabPage, Point> tabOffsets = new Dictionary<TabPage, Point>();

        // Appelé une fois, à la fin du constructeur.
        void AddV2Menus()
        {
            ThemeSettings.Load();
            FormClosing += AutoTrayClosing; // V42 : la croix réduit la fenêtre dans la zone de notification si la copie automatique est active
            FormClosing += delegate { RememberWindowBounds(); };
            FormClosing += delegate (object sender, FormClosingEventArgs e)
            {
                if (e.Cancel) return;
                if (!ConfirmUnsaved("quitting", "quitter")) e.Cancel = true;
            };
            baseFontSize = Font.Size;
            baseMenuFontSize = menuStripMain.Font.Size;

            BuildV2Layout();
            BuildV2Menus();

            tabControlMain.SelectedIndexChanged += delegate
            {
                if (tabControlMain.SelectedTab == itemBoxTab || tabControlMain.SelectedTab == equipTab || tabControlMain.SelectedTab == palicoEquipTab)
                    EnsureGraphicReady();
                // On remet tout en place tout de suite, puis une 2e fois quand l'onglet a fini de s'afficher.
                LayoutAll();
                UpdateLoadHint();
                RefreshViews();
                if (IsHandleCreated)
                {
                    BeginInvoke(new Action(delegate { LayoutAll(); RefreshViews(); }));
                }
            };
            generalTab.SizeChanged += delegate { CenterGeneral(); };
            lblLoadHint = new Label();
            lblLoadHint.AutoSize = true;
            lblLoadHint.BackColor = Color.Transparent;
            lblLoadHint.ForeColor = Color.FromArgb(255, 230, 90);
            lblLoadHint.Visible = false;
            Controls.Add(lblLoadHint);
            lblIssueHint = new Label();
            lblIssueHint.AutoSize = true;
            lblIssueHint.BackColor = Color.Transparent;
            lblIssueHint.ForeColor = Color.FromArgb(255, 230, 90);
            lblIssueHint.Cursor = Cursors.Hand;
            lblIssueHint.Visible = false;
            lblIssueHint.Click += delegate { OpenUrl("https://github.com/R0NEwesker60/MHGUswitchSaveEditorFrRw6/issues"); };
            Controls.Add(lblIssueHint);

            // Message jaune « interface graphique non activée » (onglet Général + onglets censés être graphiques).
            gfxTip = new ToolTip();
            gfxTip.InitialDelay = 150;
            gfxTip.ReshowDelay = 100;
            gfxTip.AutoPopDelay = 20000;
            lblGfxHint = new Label();
            lblGfxHint.AutoSize = true;
            lblGfxHint.BackColor = Color.Transparent;
            lblGfxHint.Cursor = Cursors.Hand;
            lblGfxHint.Visible = false;
            lblGfxHint.Click += delegate { ShowGraphicHelp(); };
            Controls.Add(lblGfxHint);
            btnGfxHelp = new Button();
            btnGfxHelp.AutoSize = true;
            btnGfxHelp.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            btnGfxHelp.Visible = false;
            btnGfxHelp.Click += delegate { ShowGraphicHelp(); };
            Controls.Add(btnGfxHelp);
            CreateGfxPageHint(itemBoxTab, groupBox1);
            CreateGfxPageHint(equipTab, equipTab);
            CreateGfxPageHint(palicoEquipTab, palicoEquipTab);
            CreateSlotButtons();
            CreateDestButtons();
            lblDetected = new Label();
            lblDetected.AutoSize = true;
            lblDetected.BackColor = Color.Transparent;
            lblDetected.Visible = false;
            Controls.Add(lblDetected);
            SetTabsEnabled(false); // au démarrage : le contenu des onglets est grisé, mais les onglets restent accessibles
            playerTab.SizeChanged += delegate { CenterGeneral(); };
            Activated += delegate { RefreshViews(); };
            Shown += delegate
            {
                LayoutAll();
                // La taille mémorisée est appliquée une fois la fenêtre affichée, puis tout est remis en page.
                BeginInvoke(new Action(delegate
                {
                    RestoreWindowBounds();
                    LayoutAll();
                    BeginInvoke(new Action(delegate { LayoutAll(); RefreshViews(); }));
                }));
            };
            Resize += delegate { LayoutAll(); };

            ApplyGraphicMode();
            menuStripMain.Renderer = ThemeSettings.MenuRenderer();
            if (ThemeSettings.Dark) ApplyThemeEverywhere();
            if (ThemeSettings.Zoom != 100) SetZoom(ThemeSettings.Zoom, false);
        }

        // ---- Mise en page ----

        void BuildV2Layout()
        {
            // Les marges de départ (celles du dessin d'origine de la fenêtre).
            tabMarginL = tabControlMain.Left;
            tabMarginT = tabControlMain.Top;
            tabMarginR = ClientSize.Width - tabControlMain.Right;
            tabMarginB = ClientSize.Height - tabControlMain.Bottom;
            if (tabMarginR < 2) tabMarginR = 2;
            if (tabMarginB < 2) tabMarginB = 2;

            // Les deux coffres graphiques (leur taille est donnée par la mise en page).
            itemView = new ChestView(this, false);
            itemBoxTab.Controls.Add(itemView);
            equipView = new ChestView(this, true);
            equipTab.Controls.Add(equipView);

            equipListLocation = listViewEquipment.Location;

            RegisterPage(itemBoxTab, listViewItem, itemView, true);
            RegisterPage(equipTab, listViewEquipment, equipView, false);
            RegisterPage(palicoTab, listViewPalico, null, false);
            palicoView = new ChestView(this, false, true);
            palicoEquipTab.Controls.Add(palicoView);
            palicoView.BringToFront();
            palicoListLocation = listViewPalicoEquipment.Location;
            RegisterPage(palicoEquipTab, listViewPalicoEquipment, palicoView, false);

            // Quand on change l'équipement du Palico avec les listes du bas, la grille se redessine.
            comboBoxPalicoEqpType.SelectedIndexChanged += delegate { RefreshViews(); };
            comboBoxPalicoEquip.SelectedIndexChanged += delegate { RefreshViews(); };
            buttonTransmogrifyPalico.Click += delegate { RefreshViews(); };

            // Recherche évoluée (filtre en direct, FR/EN, numéro) sur les listes de noms.
            string pickTitle = RomfsConfig.L("Choose a name", "Choisir un nom");
            ComboSearch.Attach(comboBoxPalicoEquip, pickTitle);
            ComboSearch.Attach(comboBoxItem, RomfsConfig.L("Choose an item", "Choisir un objet"));
            ComboSearch.Attach(comboBoxEquipName, pickTitle);
            ComboSearch.Attach(comboBoxEquipDeco1, pickTitle);
            ComboSearch.Attach(comboBoxEquipDeco2, pickTitle);
            ComboSearch.Attach(comboBoxEquipDeco3, pickTitle);

            // Quand on modifie l'équipement avec les listes du bas, la grille se redessine.
            comboBoxEquipType.SelectedIndexChanged += delegate { RefreshViews(); };
            comboBoxEquipName.SelectedIndexChanged += delegate { RefreshViews(); };
            numericUpDownEquipLevel.ValueChanged += delegate { RefreshViews(); };
            buttonTransmogrify.Click += delegate { RefreshViews(); };

            // La fenêtre devient redimensionnable et démarre plus grande.
            baseMinimumSize = Size;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimumSize = baseMinimumSize;
            Rectangle wa = Screen.FromControl(this).WorkingArea;
            int w = Math.Min(960, (int)(wa.Width * 0.95));
            int h = Math.Min(860, (int)(wa.Height * 0.92));
            ClientSize = new Size(Math.Max(ClientSize.Width, w), Math.Max(ClientSize.Height, h));
            CenterToScreen();
            LayoutAll();
        }

        void RegisterPage(TabPage page, ListView lv, ChestView view, bool ignoreBottomWhenGraphic)
        {
            PageLayout pl = new PageLayout();
            pl.Page = page;
            pl.List = lv;
            pl.View = view;
            pl.IgnoreBottomWhenGraphic = ignoreBottomWhenGraphic;
            Size cs = page.ClientSize;
            pl.MarginL = lv.Left;
            pl.MarginT = lv.Top;
            pl.MarginR = Math.Min(12, Math.Max(2, cs.Width - lv.Right));

            int minTop = int.MaxValue;
            int maxBottom = int.MinValue;
            foreach (Control c in page.Controls)
            {
                if (c == lv || c == view) continue;
                if (c.Top >= lv.Bottom - 2)
                {
                    pl.Bottom.Add(c);
                    if (c.Top < minTop) minTop = c.Top;
                    if (c.Bottom > maxBottom) maxBottom = c.Bottom;
                }
            }
            if (pl.Bottom.Count > 0)
            {
                pl.Gap = Math.Max(2, minTop - lv.Bottom);
                pl.MarginB = Math.Min(12, Math.Max(2, cs.Height - maxBottom));
            }
            else
            {
                pl.Gap = 0;
                pl.MarginB = Math.Min(12, Math.Max(2, cs.Height - lv.Bottom));
            }
            pageLayouts.Add(pl);
            page.SizeChanged += delegate { LayoutPage(pl); };
        }

        float ScaleFactor()
        {
            if (baseFontSize <= 0f) return 1f;
            return Font.Size / baseFontSize;
        }

        static int Sc(int value, float factor)
        {
            return (int)Math.Round(value * factor);
        }

        // Dispose le grand onglet et chaque page. Peut être appelée autant de fois qu'on veut :
        // elle part toujours de la position actuelle des contrôles.
        void LayoutAll()
        {
            if (v2Zooming || tabControlMain == null) return;
            LayoutTabControl();
            foreach (PageLayout pl in pageLayouts) LayoutPage(pl);
            CenterGeneral();
            UpdateLoadHint();
        }

        // Message jaune tant qu'aucune sauvegarde n'est chargée (interface grisée).
        void UpdateLoadHint()
        {
            if (lblLoadHint == null || tabControlMain == null) return;
            UpdateIssueHint();
            UpdateGfxHints();
            UpdateSlotButtons();
            UpdateDestButtons();
            UpdateDetectedLabel();
            bool show = !tabsLoaded && tabControlMain.SelectedTab == generalTab;
            lblLoadHint.Visible = show;
            if (!show) return;
            lblLoadHint.Font = new Font(Font.FontFamily, Font.Size * 1.6f, FontStyle.Bold);
            lblLoadHint.ForeColor = Color.FromArgb(255, 230, 90);
            lblLoadHint.Text = RomfsConfig.L("Do \"File\" -> \"Load\"", "Faites \"Fichier\" -> \"Charger\"");
            int minTop = int.MaxValue;
            foreach (Control c in generalTab.Controls)
            {
                if (c.Top < minTop) minTop = c.Top;
            }
            if (minTop == int.MaxValue) minTop = 0;
            Point top = PointToClient(generalTab.PointToScreen(new Point(0, minTop)));
            lblLoadHint.Left = Math.Max(0, (ClientSize.Width - lblLoadHint.Width) / 2);
            lblLoadHint.Top = Math.Max(0, top.Y - lblLoadHint.Height - 28);
            lblLoadHint.BringToFront();
        }

        // Mention en bas à gauche de l'onglet Général : où signaler un bug ou donner un retour.
        void UpdateIssueHint()
        {
            if (lblIssueHint == null || tabControlMain == null || generalTab == null) return;
            bool show = tabControlMain.SelectedTab == generalTab;
            lblIssueHint.Visible = show;
            if (!show) return;
            lblIssueHint.Text = RomfsConfig.L(
                "Found a bug or have feedback? Go to my GitHub page (R0NEwesker60/MHGUswitchSaveEditorFrRw6) and open an \"issue\".",
                "Un bug ou un retour à me faire ? Rendez-vous sur mon GitHub (R0NEwesker60/MHGUswitchSaveEditorFrRw6) et créez une « issue ».");
            lblIssueHint.MaximumSize = new Size(Math.Max(200, ClientSize.Width / 2), 0);
            int pad = Math.Max(8, Font.Height);
            Point p = PointToClient(generalTab.PointToScreen(new Point(pad, generalTab.ClientSize.Height - lblIssueHint.Height - pad)));
            lblIssueHint.Location = p;
            lblIssueHint.BringToFront();
        }

        void OpenUrl(string url)
        {
            try { System.Diagnostics.Process.Start(url); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message); }
        }

        // Taille utile d'une page (la même pour tous les onglets, même ceux qui ne sont pas affichés).
        Size PageArea(TabPage page)
        {
            Size c = page.ClientSize;
            // Ne jamais lire DisplayRectangle avant que la fenêtre existe : cela force la création de l'onglet trop tôt (plantage).
            if (!tabControlMain.IsHandleCreated) return c;
            Size d = tabControlMain.DisplayRectangle.Size;
            if (d.Width >= 100 && d.Height >= 60) return d;
            return c;
        }

        // Onglets « Général » et « Joueur » : leur contenu est centré dans la page.
        // La fenêtre garde sa taille, sa position (et « agrandie ») d'un lancement à l'autre.
        void RestoreWindowBounds()
        {
            try
            {
                if (ThemeSettings.WinW < 600 || ThemeSettings.WinH < 400) return;
                Rectangle r = new Rectangle(ThemeSettings.WinX, ThemeSettings.WinY, ThemeSettings.WinW, ThemeSettings.WinH);
                bool visible = false;
                foreach (Screen sc in Screen.AllScreens)
                {
                    if (sc.WorkingArea.IntersectsWith(r)) visible = true;
                }
                if (!visible) return;
                StartPosition = FormStartPosition.Manual;
                Bounds = r;
                if (ThemeSettings.WinMax) WindowState = FormWindowState.Maximized;
            }
            catch (Exception) { }
        }

        void RememberWindowBounds()
        {
            try
            {
                Rectangle r = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
                ThemeSettings.WinX = r.X;
                ThemeSettings.WinY = r.Y;
                ThemeSettings.WinW = r.Width;
                ThemeSettings.WinH = r.Height;
                ThemeSettings.WinMax = WindowState == FormWindowState.Maximized;
                ThemeSettings.Save();
            }
            catch (Exception) { }
        }

        void ClearTabOffsets()
        {
            foreach (KeyValuePair<TabPage, Point> kv in tabOffsets)
            {
                foreach (Control c in kv.Key.Controls)
                    c.Location = new Point(c.Left - kv.Value.X, c.Top - kv.Value.Y);
            }
            tabOffsets.Clear();
        }

        void FitGeneralButtons()
        {
            Button[] bs = new Button[] { buttonEditGuildCard, buttonEditShoutouts };
            int gap = 12;
            int x = bs[0].Left;
            foreach (Button b in bs)
            {
                // Taille calculée d'après le texte (et non d'après la taille actuelle) : elle ne grossit donc jamais toute seule.
                Size ts = TextRenderer.MeasureText(b.Text, b.Font);
                b.AutoSize = false;
                b.Width = ts.Width + 44;
                b.Height = ts.Height + 22;
                b.Left = x;
                x = b.Right + gap;
            }
        }

        void CenterGeneral()
        {
            if (generalTab == null || playerTab == null || tabControlMain == null || v2Zooming || v2Laying) return;
            Size cs = PageArea(generalTab);
            if (cs.Width < 100 || cs.Height < 60) return;
            v2Laying = true;
            try
            {
                ClearTabOffsets();
                FitGeneralButtons();
                foreach (TabPage tp in new TabPage[] { generalTab, playerTab })
                {
                    int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
                    foreach (Control c in tp.Controls)
                    {
                        if (c.Left < minX) minX = c.Left;
                        if (c.Top < minY) minY = c.Top;
                        if (c.Right > maxX) maxX = c.Right;
                        if (c.Bottom > maxY) maxY = c.Bottom;
                    }
                    if (minX == int.MaxValue) continue;
                    int dx = (cs.Width - (maxX - minX)) / 2 - minX;
                    int dy = (cs.Height - (maxY - minY)) / 2 - minY;
                    if (dx < 0) dx = 0;
                    if (dy < 0) dy = 0;
                    if (dx == 0 && dy == 0) continue;
                    foreach (Control c in tp.Controls)
                        c.Location = new Point(c.Left + dx, c.Top + dy);
                    tabOffsets[tp] = new Point(dx, dy);
                }
            }
            finally
            {
                v2Laying = false;
            }
        }

        void LayoutTabControl()
        {
            Size cs = ClientSize;
            if (cs.Width < 200 || cs.Height < 150) return;
            float s = ScaleFactor();
            int left = Sc(tabMarginL, s);
            int top = Sc(tabMarginT, s);
            int w = cs.Width - left - Sc(tabMarginR, s);
            int h = cs.Height - top - Sc(tabMarginB, s);
            if (w < 100 || h < 100) return;
            Rectangle target = new Rectangle(left, top, w, h);
            if (tabControlMain.Bounds != target) tabControlMain.Bounds = target;
        }

        void LayoutPage(PageLayout pl)
        {
            if (v2Laying || v2Zooming) return;
            Size cs = PageArea(pl.Page);
            if (cs.Width < 100 || cs.Height < 60) return;
            v2Laying = true;
            try
            {
                float s = ScaleFactor();
                int ml = Sc(pl.MarginL, s);
                int mt = Sc(pl.MarginT, s);
                int mr = Sc(pl.MarginR, s);
                int mb = Sc(pl.MarginB, s);
                int gap = Sc(pl.Gap, s);

                bool graphic = pl.View != null && GraphicOn();
                bool useBottom = pl.Bottom.Count > 0 && !(graphic && pl.IgnoreBottomWhenGraphic);

                int listBottom;
                if (useBottom)
                {
                    int curTop = int.MaxValue;
                    int curBottom = int.MinValue;
                    foreach (Control c in pl.Bottom)
                    {
                        if (c.Top < curTop) curTop = c.Top;
                        if (c.Bottom > curBottom) curBottom = c.Bottom;
                    }
                    int dy = (cs.Height - mb) - curBottom;
                    if (dy != 0)
                    {
                        foreach (Control c in pl.Bottom) c.Top = c.Top + dy;
                    }
                    listBottom = curTop + dy - gap;
                }
                else
                {
                    listBottom = cs.Height - mb;
                }

                int w = Math.Max(100, cs.Width - ml - mr);
                int h = Math.Max(60, listBottom - mt);
                Rectangle r = new Rectangle(ml, mt, w, h);
                if (graphic)
                {
                    if (pl.View.Bounds != r) pl.View.Bounds = r;
                    // La liste d'origine ne bouge pas : elle est cachée (coffre d'objets)
                    // ou poussée hors de la zone (équipement).
                }
                else
                {
                    if (pl.List.Bounds != r) pl.List.Bounds = r;
                }
            }
            finally
            {
                v2Laying = false;
            }
        }

        // ---- Menus ----

        void BuildV2Menus()
        {
            ToolStripMenuItem view = new ToolStripMenuItem(RomfsConfig.L("View", "Affichage"));

            menuGraphic = new ToolStripMenuItem(RomfsConfig.L("Graphic chest view", "Vue graphique des coffres"));
            menuGraphic.CheckOnClick = true;
            menuGraphic.Checked = ThemeSettings.GraphicView;
            menuGraphic.Click += delegate
            {
                ThemeSettings.GraphicView = menuGraphic.Checked;
                ThemeSettings.Save();
                graphicFailed = false;
                if (ThemeSettings.GraphicView) EnsureGraphicReady();
                ApplyGraphicMode();
            };
            view.DropDownItems.Add(menuGraphic);

            menuDarkMode = new ToolStripMenuItem(RomfsConfig.L("Dark mode", "Mode sombre"));
            menuDarkMode.CheckOnClick = true;
            menuDarkMode.Checked = ThemeSettings.Dark;
            menuDarkMode.Click += delegate
            {
                ThemeSettings.Dark = menuDarkMode.Checked;
                ThemeSettings.Save();
                ApplyThemeEverywhere();
            };
            view.DropDownItems.Add(menuDarkMode);

            menuHd = new ToolStripMenuItem(RomfsConfig.L("HD icons (texture pack)", "Icônes HD (pack de textures)"));
            menuHd.CheckOnClick = true;
            menuHd.Checked = ThemeSettings.HdIcons;
            menuHd.Click += delegate { ToggleHdIcons(menuHd.Checked); };
            view.DropDownItems.Add(menuHd);
            MenuChecks.Install(menuGraphic);
            MenuChecks.Install(menuDarkMode);
            MenuChecks.Install(menuHd);
            menuColors = new ToolStripMenuItem(RomfsConfig.L("Colored menus and tabs", "Couleurs des menus et des onglets"));
            menuColors.CheckOnClick = true;
            menuColors.Checked = ThemeSettings.ColorMenus;
            menuColors.Click += delegate
            {
                ThemeSettings.ColorMenus = menuColors.Checked;
                ThemeSettings.Save();
                menuStripMain.Invalidate();
                tabControlMain.Invalidate();
            };
            view.DropDownItems.Add(menuColors);
            MenuChecks.Install(menuColors);
            menuRankColors = new ToolStripMenuItem(RomfsConfig.L("Hunter rank colors on the names", "Couleurs du rang de chasseur sur les pseudos"));
            menuRankColors.CheckOnClick = true;
            menuRankColors.Checked = ThemeSettings.ColorRanks;
            menuRankColors.ToolTipText = RomfsConfig.L("White RC 1-3, yellow/orange 4-7, green 8-12, red 13-99, purple 100-998, light blue 999.",
                                                       "Blanc RC 1-3, jaune/orange 4-7, vert 8-12, rouge 13-99, violet 100-998, bleu clair 999.");
            menuRankColors.Click += delegate
            {
                ThemeSettings.ColorRanks = menuRankColors.Checked;
                ThemeSettings.Save();
                UpdateSlotButtons();
            };
            view.DropDownItems.Add(menuRankColors);
            MenuChecks.Install(menuRankColors);

            view.DropDownItems.Add(new ToolStripSeparator());

            menuZoom = new ToolStripMenuItem("Zoom");
            ToolStripMenuItem zoomIn = new ToolStripMenuItem(RomfsConfig.L("Zoom in", "Agrandir"));
            zoomIn.ShortcutKeys = Keys.Control | Keys.Oemplus;
            zoomIn.Click += delegate { StepZoom(1); };
            ToolStripMenuItem zoomOut = new ToolStripMenuItem(RomfsConfig.L("Zoom out", "Réduire"));
            zoomOut.ShortcutKeys = Keys.Control | Keys.OemMinus;
            zoomOut.Click += delegate { StepZoom(-1); };
            menuZoom.DropDownItems.Add(zoomIn);
            menuZoom.DropDownItems.Add(zoomOut);
            menuZoom.DropDownItems.Add(new ToolStripSeparator());
            foreach (int percent in ZoomSteps)
            {
                int p = percent;
                ToolStripMenuItem item = new ToolStripMenuItem(p.ToString() + " %");
                item.Tag = p;
                MenuChecks.Install(item);
                item.Click += delegate { SetZoom(p, true); };
                menuZoom.DropDownItems.Add(item);
            }
            view.DropDownItems.Add(menuZoom);
            UpdateZoomChecks();

            ToolStripMenuItem romfs = new ToolStripMenuItem(RomfsConfig.L("Change ROMFS folder...", "Changer le dossier ROMFS..."));
            romfs.Click += delegate { ChangeRomfsFolder(); };
            menuForgetRomfs = new ToolStripMenuItem(RomfsConfig.L("Forget the saved ROMFS folder", "Oublier le dossier ROMFS enregistré"));
            menuForgetRomfs.ToolTipText = RomfsConfig.L("Only if you want to unregister the folder. \"Change ROMFS folder...\" keeps the current one when you cancel.",
                                                        "Seulement si vous voulez désenregistrer le dossier. « Changer le dossier ROMFS... » garde l'actuel si vous annulez.");
            menuForgetRomfs.Click += delegate { ForgetRomfsFolder(); };
            view.DropDownItems.Add(new ToolStripSeparator());
            view.DropDownItems.Add(romfs);
            view.DropDownItems.Add(menuForgetRomfs);
            ToolStripMenuItem hdPack = new ToolStripMenuItem(RomfsConfig.L("Change HD pack folder...", "Changer le dossier du pack HD..."));
            hdPack.Click += delegate { ChangeHdPackFolder(); };
            ToolStripMenuItem forgetHd = new ToolStripMenuItem(RomfsConfig.L("Forget the saved HD pack", "Oublier le pack HD enregistré"));
            forgetHd.ToolTipText = RomfsConfig.L("Only if you want to unregister the HD pack. \"Change HD pack folder...\" keeps the current one when you cancel.",
                                                 "Seulement si vous voulez désenregistrer le pack HD. « Changer le dossier du pack HD... » garde l'actuel si vous annulez.");
            forgetHd.Click += delegate { ForgetHdPack(); };
            view.DropDownItems.Add(hdPack);
            view.DropDownItems.Add(forgetHd);

            menuStripMain.Items.Add(view);
            BuildBackupMenu();
            BuildOptionsMenu();
            loadToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.O;
            saveToolStripMenuItemSave.ShortcutKeys = Keys.Control | Keys.S;
            lblVersion = new RainbowLabel("V " + Constants.EDITOR_VERSION_NUMBER);
            lblVersion.Alignment = ToolStripItemAlignment.Right;
            lblVersion.ForeColor = ThemeSettings.Dark ? Color.FromArgb(170, 172, 185) : Color.DimGray;
            menuStripMain.Items.Add(lblVersion);
            MenuChecks.Install(toolStripMenuItemSaveSlot1);
            MenuChecks.Install(toolStripMenuItemSaveSlot2);
            MenuChecks.Install(toolStripMenuItemSaveSlot3);
        }

        // ---- Zoom de toute l'application ----

        public void StepZoom(int dir)
        {
            int index = 1;
            for (int i = 0; i < ZoomSteps.Length; i++)
            {
                if (ZoomSteps[i] <= currentZoom) index = i;
            }
            index += dir;
            if (index < 0) index = 0;
            if (index >= ZoomSteps.Length) index = ZoomSteps.Length - 1;
            SetZoom(ZoomSteps[index], true);
        }

        // Change la taille de l'écriture : la fenêtre se règle sur la police, donc les contrôles,
        // les textes et les grilles grandissent ensemble. La fenêtre grandit aussi (dans la limite de l'écran).
        void SetZoom(int percent, bool save)
        {
            if (percent < 50) percent = 50;
            if (percent > 400) percent = 400;
            if (percent == currentZoom && save) return;

            float ratio = (float)percent / (float)currentZoom;
            currentZoom = percent;
            ThemeSettings.Zoom = percent;
            if (save) ThemeSettings.Save();

            v2Zooming = true;
            try
            {
                ClearTabOffsets();
                ScaleListColumns(this, ratio);
                Font = new Font(Font.FontFamily, baseFontSize * percent / 100f, Font.Style);
                // La barre de menus (Fichier, Modifier...) a sa propre police : elle suit aussi le zoom.
                if (menuStripMain != null && baseMenuFontSize > 0f)
                    menuStripMain.Font = new Font(menuStripMain.Font.FontFamily, baseMenuFontSize * percent / 100f, menuStripMain.Font.Style);
            }
            finally
            {
                v2Zooming = false;
            }

            // Taille de la fenêtre : proportionnelle, mais jamais plus grande que l'écran.
            Rectangle wa = Screen.FromControl(this).WorkingArea;
            int minW = Math.Min(Sc(baseMinimumSize.Width, percent / 100f), wa.Width);
            int minH = Math.Min(Sc(baseMinimumSize.Height, percent / 100f), wa.Height);
            MinimumSize = new Size(minW, minH);
            int newW = Math.Min(wa.Width, Math.Max(minW, (int)Math.Round(Width * ratio)));
            int newH = Math.Min(wa.Height, Math.Max(minH, (int)Math.Round(Height * ratio)));
            Size = new Size(newW, newH);
            int left = Math.Max(wa.Left, Math.Min(Left, wa.Right - Width));
            int top = Math.Max(wa.Top, Math.Min(Top, wa.Bottom - Height));
            Location = new Point(left, top);

            UpdateZoomChecks();
            LayoutAll();
            RefreshViews();
        }

        // Les colonnes d'une liste ne suivent pas l'écriture toutes seules.
        static void ScaleListColumns(Control root, float ratio)
        {
            ListView lv = root as ListView;
            if (lv != null)
            {
                foreach (ColumnHeader col in lv.Columns)
                    col.Width = (int)Math.Round(col.Width * ratio);
            }
            foreach (Control child in root.Controls) ScaleListColumns(child, ratio);
        }

        void UpdateZoomChecks()
        {
            if (menuZoom == null) return;
            foreach (ToolStripItem it in menuZoom.DropDownItems)
            {
                ToolStripMenuItem mi = it as ToolStripMenuItem;
                if (mi != null && mi.Tag is int) mi.Checked = ((int)mi.Tag == currentZoom);
            }
        }

        // ---- Thème ----

        void ApplyThemeEverywhere()
        {
            ThemeSettings.Apply(this);
            if (menuDarkMode != null) menuDarkMode.Checked = ThemeSettings.Dark;
            MenuChecks.Refresh();
            if (lblVersion != null) lblVersion.ForeColor = ThemeSettings.Dark ? Color.FromArgb(170, 172, 185) : Color.DimGray;
            UpdateGfxHints();
            Refresh();
        }

        // ---- Modifications non enregistrées ----

        // Empreinte de ce qui serait écrit dans le fichier si on enregistrait maintenant.
        string CurrentHash()
        {
            PackSaveFile();
            using (SHA1 sha = SHA1.Create())
                return Convert.ToBase64String(sha.ComputeHash(saveFile));
        }

        // À appeler quand l'éditeur et le fichier sont d'accord (après chargement ou enregistrement).
        void MarkClean()
        {
            try
            {
                if (saveFile == null || player == null) { cleanHash = null; return; }
                cleanHash = CurrentHash();
            }
            catch (Exception) { cleanHash = null; }
        }

        bool HasUnsavedChanges()
        {
            try
            {
                if (saveFile == null || player == null || cleanHash == null) return false;
                return CurrentHash() != cleanHash;
            }
            catch (Exception) { return false; }
        }

        // Retourne false si la personne veut annuler (quitter / charger). Ne fait rien si l'option est désactivée.
        bool ConfirmUnsaved(string whenEn, string whenFr)
        {
            if (!ThemeSettings.WarnUnsaved || !HasUnsavedChanges()) return true;
            DialogResult r = MessageBox.Show(this,
                RomfsConfig.L("Some changes are not saved.\n\nSave them before " + whenEn + "?\n\nYes = save, No = continue without saving, Cancel = go back.",
                              "Des modifications ne sont pas enregistrées.\n\nVoulez-vous les enregistrer avant de " + whenFr + " ?\n\nOui = enregistrer, Non = continuer sans enregistrer, Annuler = revenir."),
                RomfsConfig.L("Unsaved changes", "Modifications non enregistrées"),
                MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
            if (r == DialogResult.Cancel) return false;
            if (r == DialogResult.Yes)
            {
                keepLoadedAfterSave = true; // ici on continue avec la sauvegarde ouverte : pas de déchargement
                try { SaveToolStripMenuItemSave_Click(this, EventArgs.Empty); }
                finally { keepLoadedAfterSave = false; }
                if (HasUnsavedChanges()) return false; // l'enregistrement n'a pas abouti : on ne perd rien
            }
            return true;
        }

        // ---- Menu Options ----

        void BuildOptionsMenu()
        {

            // Menu Aide : une entrée de plus pour la page GitHub de cette version (l'autre est celle de l'original).
            ToolStripMenuItem help = visitGithubPageToolStripMenuItem.OwnerItem as ToolStripMenuItem;
            if (help != null)
            {
                ToolStripMenuItem rw6 = new ToolStripMenuItem(RomfsConfig.L("Visit Github page (RW6)", "Visiter la page GitHub RW6"));
                rw6.Click += delegate
                {
                    if (MessageBox.Show(this,
                            RomfsConfig.L("Do you wish visit the RW6 Github page?", "Voulez-vous visiter la page GitHub RW6 ?"),
                            RomfsConfig.L("Visit Github", "Visiter GitHub"), MessageBoxButtons.YesNo, MessageBoxIcon.Asterisk) == DialogResult.Yes)
                        OpenUrl("https://github.com/R0NEwesker60/MHGUswitchSaveEditorFrRw6");
                };
                help.DropDownItems.Add(rw6);
            }

            BuildSwapSlotsMenu();
            BuildCopySlotsMenu();
            BuildTransferMenu();
            BuildRecentMenu();

            ToolStripMenuItem opt = new ToolStripMenuItem(RomfsConfig.L("Options", "Options"));
            menuWeaponTransmog = new ToolStripMenuItem(RomfsConfig.L("Weapon transmogrification (cheat codes)", "Transmogrification des armes (codes de triche)"));
            menuWeaponTransmog.CheckOnClick = true;
            menuWeaponTransmog.Checked = ThemeSettings.WeaponTransmog;
            menuWeaponTransmog.ToolTipText = RomfsConfig.L("Only useful if you use cheat codes in the game: allows transmogrifying weapons.",
                                                           "Utile seulement avec des codes de triche dans le jeu : permet de transmogrifier les armes.");
            menuWeaponTransmog.Click += delegate
            {
                ThemeSettings.WeaponTransmog = menuWeaponTransmog.Checked;
                ThemeSettings.Save();
                SyncTransmogButton();
            };
            opt.DropDownItems.Add(menuWeaponTransmog);
            MenuChecks.Install(menuWeaponTransmog);

            menuWarnUnsaved = new ToolStripMenuItem(RomfsConfig.L("Warn about unsaved changes", "Prévenir s'il y a des modifications non enregistrées"));
            menuWarnUnsaved.CheckOnClick = true;
            menuWarnUnsaved.Checked = ThemeSettings.WarnUnsaved;
            menuWarnUnsaved.ToolTipText = RomfsConfig.L("Asks before quitting, loading another save or switching character if some changes were not saved.",
                                                        "Demande confirmation avant de quitter, de charger une autre sauvegarde ou de changer de personnage s'il reste des modifications non enregistrées.");
            menuWarnUnsaved.Click += delegate
            {
                ThemeSettings.WarnUnsaved = menuWarnUnsaved.Checked;
                ThemeSettings.Save();
                if (ThemeSettings.WarnUnsaved) MarkClean(); // on repart de l'état actuel
            };
            opt.DropDownItems.Add(menuWarnUnsaved);
            MenuChecks.Install(menuWarnUnsaved);

            menuUnloadAfterSave = new ToolStripMenuItem(RomfsConfig.L("Unload the save after saving", "Décharger la sauvegarde après l'avoir enregistrée"));
            menuUnloadAfterSave.CheckOnClick = true;
            menuUnloadAfterSave.Checked = ThemeSettings.UnloadAfterSave;
            menuUnloadAfterSave.ToolTipText = RomfsConfig.L("After Save, the editor goes back to its start state (no save open), so an old copy kept open cannot overwrite what you did in the game. Load the save again to keep editing.",
                                                            "Après Enregistrer, l'éditeur revient comme au démarrage (aucune sauvegarde ouverte) : une vieille version gardée ouverte ne peut pas écraser ce que vous avez fait dans le jeu. Rechargez la sauvegarde pour continuer à modifier.");
            menuUnloadAfterSave.Click += delegate
            {
                ThemeSettings.UnloadAfterSave = menuUnloadAfterSave.Checked;
                ThemeSettings.Save();
            };
            opt.DropDownItems.Add(menuUnloadAfterSave);
            MenuChecks.Install(menuUnloadAfterSave);
            BuildStartupMenuItems(opt);
            menuStripMain.Items.Add(opt);
            AddAutoCopyTopMenu(); // V56 : bouton « Save auto » à côté de Options

        }

        // Après un changement de l'option : le bouton « Transmogrifier » de l'arme sélectionnée suit tout de suite.
        void SyncTransmogButton()
        {
            if (player == null || listViewEquipment.SelectedItems.Count == 0) return;
            int t = comboBoxEquipType.SelectedIndex;
            if (t >= 7 && t <= 21 && t != 12) buttonTransmogrify.Enabled = ThemeSettings.WeaponTransmog;
        }

        // Petite fenêtre « choisir un nombre ». Retourne -1 si annulée.
        int PromptNumber(string title, string label, int value, int min, int max)
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
                NumericUpDown num = new NumericUpDown();
                num.Minimum = min;
                num.Maximum = max;
                num.Value = Math.Max(min, Math.Min(max, value));
                num.Location = new Point(u, u + sz.Height + u / 2);
                num.Width = u * 6;
                dlg.Controls.Add(num);
                Button ok = new Button();
                ok.Text = "OK";
                ok.DialogResult = DialogResult.OK;
                ok.Size = new Size(u * 6, u * 2);
                ok.Location = new Point(u, num.Bottom + u);
                dlg.Controls.Add(ok);
                Button cancel = new Button();
                cancel.Text = RomfsConfig.L("Cancel", "Annuler");
                cancel.DialogResult = DialogResult.Cancel;
                cancel.Size = new Size(u * 6, u * 2);
                cancel.Location = new Point(ok.Right + u, num.Bottom + u);
                dlg.Controls.Add(cancel);
                dlg.ClientSize = new Size(Math.Max(sz.Width + u * 2, cancel.Right + u), ok.Bottom + u);
                dlg.AcceptButton = ok;
                dlg.CancelButton = cancel;
                ThemeSettings.Apply(dlg);
                if (dlg.ShowDialog(this) != DialogResult.OK) return -1;
                return (int)num.Value;
            }
        }
    }
}
