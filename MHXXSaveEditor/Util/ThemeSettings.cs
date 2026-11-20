// MHXXSwitchSaveEditor – version française (modifiée). Licence : GNU GPL v3 (voir LICENSE).
// Fichier ajouté lors de la V2 (coffre graphique), le 14 novembre 2026.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MHXXSaveEditor.Util
{
    // Réglages d'affichage mémorisés d'une fois sur l'autre : thème sombre, vue graphique des coffres, zoom.
    // Contient aussi l'application du thème sombre (barre de titre, onglets, menus, listes, boutons...).
    static class ThemeSettings
    {
        public static bool Dark = false;
        public static bool GraphicView = true;
        // Icônes HD (pack de textures HD) : réglage et fichier .arc du pack choisi par la personne.
        public static bool HdIcons = false;
        public static string HdPath = "";
        // Copie de sécurité automatique avant d'enregistrer (activée par défaut, 10 copies au maximum).
        public static bool BackupOn = true;
        // Transmogrification des armes (possible seulement avec des codes de triche dans le jeu).
        public static bool WeaponTransmog = false;
        // Prévenir avant de quitter, de charger une autre sauvegarde ou de changer de personnage s'il reste des modifications non enregistrées (activé par défaut).
        public static bool WarnUnsaved = true;
        public static string BackupDir = "";
        public static int BackupMax = 10;
        // Noms des deux dossiers de copies créés dans le dossier choisi. Par défaut, selon la langue :
        // « Copies normales » / « Copies de transfert » ou « Normal backups » / « Transfer backups ».
        // Pour les réglages créés avant la version 33, on garde les anciens noms (« Save avant edit »...).
        static string backupNormalRaw;
        static string backupTransferRaw;

        public static string BackupNormalName
        {
            get
            {
                if (string.IsNullOrEmpty(backupNormalRaw)) backupNormalRaw = RomfsConfig.L("Normal backups", "Copies normales");
                return backupNormalRaw;
            }
            set { backupNormalRaw = value; }
        }

        public static string BackupTransferName
        {
            get
            {
                if (string.IsNullOrEmpty(backupTransferRaw)) backupTransferRaw = RomfsConfig.L("Transfer backups", "Copies de transfert");
                return backupTransferRaw;
            }
            set { backupTransferRaw = value; }
        }
        // Couleurs différentes pour chaque menu du haut et chaque onglet (activé par défaut).
        public static bool ColorMenus = true;
        // Pseudos des emplacements colorés selon le rang de chasseur (comme dans le jeu).
        public static bool ColorRanks = true;
        // Trois « destinations » : boutons raccourcis (nom au choix + fichier de sauvegarde) dans l'onglet Général.
        public static readonly string[] DestName = new string[] { "", "", "" };
        public static readonly string[] DestPath = new string[] { "", "", "" };
        // Couleur des lettres de chaque destination (« #RRGGBB »), vide = couleur normale.
        public static readonly string[] DestColor = new string[] { "", "", "" };
        // Version de l'organisation des copies : 3 = le dossier choisi contient « Save avant edit » et « Save avant edit - Transferts ».
        // Avant, le dossier choisi était directement le dossier des copies : on le remonte d'un cran au premier lancement.
        public static int BackupLayout = 3;
        public static int Zoom = 100;
        // Derniers fichiers de sauvegarde ouverts (les plus récents en premier, 10 au maximum).
        public static readonly List<string> Recent = new List<string>();
        // Fichiers favoris : gardés tant qu'on ne les retire pas (jamais effacés automatiquement).
        public static readonly List<string> Favorites = new List<string>();
        // Taille et position de la fenêtre principale (mémorisées d'un lancement à l'autre).
        public static int WinX, WinY, WinW, WinH;
        public static bool WinMax;

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string pszSubIdList);

        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        static string ConfigPath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MHXXSaveEditor", "settings.txt"); }
        }

        public static Color Back { get { return Dark ? Color.FromArgb(32, 33, 38) : SystemColors.Control; } }
        public static Color Fore { get { return Dark ? Color.Gainsboro : SystemColors.ControlText; } }
        public static Color Field { get { return Dark ? Color.FromArgb(46, 48, 56) : SystemColors.Window; } }
        public static Color FieldFore { get { return Dark ? Color.White : SystemColors.WindowText; } }

        public static void Load()
        {
            try
            {
                if (!File.Exists(ConfigPath)) return;
                BackupLayout = 0;
                foreach (string line in File.ReadAllLines(ConfigPath))
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                    string val = line.Substring(eq + 1).Trim();
                    if (key == "dark") Dark = val == "1";
                    else if (key == "graphic") GraphicView = val != "0";
                    else if (key == "hd") HdIcons = val == "1";
                    else if (key == "hdpath") HdPath = val;
                    else if (key == "backup") BackupOn = val != "0";
                    else if (key == "weapontransmog") WeaponTransmog = val == "1";
                    else if (key == "warnsave") WarnUnsaved = val != "0";
                    else if (key == "backupdir") BackupDir = val;
                    else if (key == "backuplayout") int.TryParse(val, out BackupLayout);
                    else if (key == "backupnormalname") backupNormalRaw = val;
                    else if (key == "backuptransfername") backupTransferRaw = val;
                    else if (key == "backupmax")
                    {
                        int bmx;
                        if (int.TryParse(val, out bmx) && bmx >= 1 && bmx <= 500) BackupMax = bmx;
                    }
                    else if (key == "favorite")
                    {
                        if (val.Length > 0 && !Favorites.Contains(val)) Favorites.Add(val);
                    }
                    else if (key == "recent")
                    {
                        if (val.Length > 0 && Recent.Count < 10) Recent.Add(val);
                    }
                    else if (key == "colormenus") ColorMenus = val != "0";
                    else if (key == "colorranks") ColorRanks = val != "0";
                    else if (key.StartsWith("destname") || key.StartsWith("destpath") || key.StartsWith("destcolor"))
                    {
                        int di;
                        int prefix = key.StartsWith("destcolor") ? 9 : 8;
                        if (int.TryParse(key.Substring(prefix), out di) && di >= 1 && di <= 3)
                        {
                            if (key.StartsWith("destname")) DestName[di - 1] = val;
                            else if (key.StartsWith("destpath")) DestPath[di - 1] = val;
                            else DestColor[di - 1] = val;
                        }
                    }
                    else if (key == "winx") int.TryParse(val, out WinX);
                    else if (key == "winy") int.TryParse(val, out WinY);
                    else if (key == "winw") int.TryParse(val, out WinW);
                    else if (key == "winh") int.TryParse(val, out WinH);
                    else if (key == "winmax") WinMax = val == "1";
                    else if (key == "zoom")
                    {
                        int z;
                        if (int.TryParse(val, out z) && z >= 50 && z <= 400) Zoom = z;
                    }
                }
            }
            catch (Exception) { }
            // Réglages d'une ancienne version (sans ces noms) : on garde les noms d'avant.
            if (string.IsNullOrEmpty(backupNormalRaw)) backupNormalRaw = "Save avant edit";
            if (string.IsNullOrEmpty(backupTransferRaw)) backupTransferRaw = "Save avant edit - Transferts";
            MigrateBackupLayout();
        }

        static void MigrateBackupLayout()
        {
            try
            {
                if (BackupLayout >= 3) return;
                BackupLayout = 3;
                if (string.IsNullOrEmpty(BackupDir)) return;
                string t = BackupDir.TrimEnd('\\', '/');
                // Ancien réglage : le dossier choisi ÉTAIT « Save avant edit ». Le nouveau dossier choisi est celui qui le contient.
                if (string.Equals(Path.GetFileName(t), BackupManager.NormalSubFolder, StringComparison.OrdinalIgnoreCase))
                {
                    string parent = Path.GetDirectoryName(t);
                    if (!string.IsNullOrEmpty(parent)) BackupDir = parent;
                }
            }
            catch (Exception) { }
        }

        public static bool IsFavorite(string path)
        {
            foreach (string f in Favorites)
            {
                if (string.Equals(f, path, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        public static void AddFavorite(string path)
        {
            if (string.IsNullOrEmpty(path) || IsFavorite(path)) return;
            try
            {
                Favorites.Add(Path.GetFullPath(path));
                Save();
            }
            catch (Exception) { }
        }

        public static void RemoveFavorite(string path)
        {
            for (int i = Favorites.Count - 1; i >= 0; i--)
            {
                if (string.Equals(Favorites[i], path, StringComparison.OrdinalIgnoreCase)) Favorites.RemoveAt(i);
            }
            Save();
        }

        public static void RemoveRecent(string path)
        {
            for (int i = Recent.Count - 1; i >= 0; i--)
            {
                if (string.Equals(Recent[i], path, StringComparison.OrdinalIgnoreCase)) Recent.RemoveAt(i);
            }
            Save();
        }

        public static void ClearRecent()
        {
            Recent.Clear();
            Save();
        }

        // Chemin affiché dans un menu : en entier, sans « ... ». Un chemin très long est coupé en deux lignes
        // (coupure juste après un « \ », la deuxième ligne est un peu décalée).
        public static string[] MenuPathLines(string p)
        {
            if (string.IsNullOrEmpty(p) || p.Length <= 100) return new string[] { p };
            int mid = p.Length / 2;
            int best = -1;
            for (int i = 0; i < p.Length; i++)
            {
                if (p[i] != '\\' && p[i] != '/') continue;
                if (best < 0 || Math.Abs(i - mid) < Math.Abs(best - mid)) best = i;
            }
            if (best <= 0 || best >= p.Length - 1) return new string[] { p };
            return new string[] { p.Substring(0, best + 1), "      " + p.Substring(best + 1) };
        }

        // ---- Couleurs des menus du haut et des onglets ----

        static readonly Color[] MenuPalette = new Color[]
        {
            Color.FromArgb(255, 214, 64),   // Fichier : jaune
            Color.FromArgb(110, 220, 120),  // Enregistrer : vert
            Color.FromArgb(255, 130, 190),  // Aide : rose
            Color.FromArgb(70, 225, 235),   // Language / Langue : cyan
            Color.FromArgb(120, 165, 255),  // Affichage : bleu
            Color.FromArgb(255, 100, 100),  // Sauvegarde de sécurité : rouge
            Color.FromArgb(195, 145, 255)   // Options : violet
        };

        static readonly Color[] TabPalette = new Color[]
        {
            Color.FromArgb(190, 235, 70),   // Général : vert citron
            Color.FromArgb(60, 215, 165),   // Joueur : turquoise
            Color.FromArgb(250, 175, 90),   // Coffre à objets : ambre
            Color.FromArgb(255, 130, 110),  // Équipement : corail
            Color.FromArgb(235, 120, 235),  // Palico : magenta
            Color.FromArgb(150, 150, 255)   // Équipement Palico : bleu-violet
        };

        // En thème clair, les couleurs sont assombries pour rester lisibles sur fond clair.
        static Color Tone(Color c)
        {
            if (Dark) return c;
            return Color.FromArgb(c.R * 6 / 10, c.G * 6 / 10, c.B * 6 / 10);
        }

        public static Color MenuColor(int i)
        {
            return Tone(MenuPalette[i % MenuPalette.Length]);
        }

        public static Color TabColor(int i)
        {
            return Tone(TabPalette[i % TabPalette.Length]);
        }

        static readonly Color[] RainbowPalette = new Color[]
        {
            Color.FromArgb(255, 90, 90),
            Color.FromArgb(255, 150, 60),
            Color.FromArgb(255, 214, 64),
            Color.FromArgb(170, 230, 70),
            Color.FromArgb(80, 220, 120),
            Color.FromArgb(60, 215, 190),
            Color.FromArgb(80, 210, 255),
            Color.FromArgb(110, 150, 255),
            Color.FromArgb(170, 130, 255),
            Color.FromArgb(225, 120, 240),
            Color.FromArgb(255, 120, 180)
        };

        // Une couleur par lettre (le nom de l'application, la version).
        public static Color RainbowColor(int i)
        {
            return Tone(RainbowPalette[i % RainbowPalette.Length]);
        }

        // Couleur du pseudo selon le rang de chasseur (RC), comme dans le jeu :
        // blanc 1-3, jaune/orange 4-7, vert 8-12, rouge 13-99, violet/rose 100-998, bleu clair 999.
        public static Color RankColor(int hr)
        {
            Color c;
            if (hr >= 999) c = Color.FromArgb(110, 235, 255);
            else if (hr >= 100) c = Color.FromArgb(215, 140, 255);
            else if (hr >= 13) c = Color.FromArgb(255, 110, 110);
            else if (hr >= 8) c = Color.FromArgb(110, 230, 120);
            else if (hr >= 4) c = Color.FromArgb(255, 190, 70);
            else c = Color.FromArgb(240, 240, 240);
            if (Dark) return c;
            if (hr < 4) return Color.FromArgb(60, 60, 60);
            return Color.FromArgb(c.R * 6 / 10, c.G * 6 / 10, c.B * 6 / 10);
        }

        // Couleur du texte d'un menu du haut (Fichier, Modifier...) ; « normal » pour tout le reste.
        static Color TopMenuColor(ToolStripItem item, Color normal)
        {
            if (!ColorMenus || !(item is ToolStripMenuItem) || !(item.Owner is MenuStrip)) return normal;
            int k = 0;
            foreach (ToolStripItem it in item.Owner.Items)
            {
                if (!(it is ToolStripMenuItem)) continue;
                if (it == item) return MenuColor(k);
                k++;
            }
            return normal;
        }

        // Dessin des menus selon le thème (sombre ou clair) ; les deux colorent les menus du haut.
        public static ToolStripRenderer MenuRenderer()
        {
            if (Dark) return new DarkRenderer();
            return new LightRenderer();
        }

        // Menu contextuel (clic droit) aux couleurs du thème.
        public static void StyleContextMenu(ContextMenuStrip cm)
        {
            if (!Dark) return;
            cm.Renderer = new DarkRenderer();
            cm.ForeColor = Fore;
            cm.BackColor = Back;
        }

        // Ajoute un fichier en tête de la liste des fichiers récents.
        public static void AddRecent(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                string full = Path.GetFullPath(path);
                for (int i = Recent.Count - 1; i >= 0; i--)
                {
                    if (string.Equals(Recent[i], full, StringComparison.OrdinalIgnoreCase)) Recent.RemoveAt(i);
                }
                Recent.Insert(0, full);
                while (Recent.Count > 10) Recent.RemoveAt(Recent.Count - 1);
                Save();
            }
            catch (Exception) { }
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
                List<string> lines = new List<string>(new string[]
                {
                    "dark=" + (Dark ? "1" : "0"),
                    "graphic=" + (GraphicView ? "1" : "0"),
                    "hd=" + (HdIcons ? "1" : "0"),
                    "hdpath=" + (HdPath ?? ""),
                    "backup=" + (BackupOn ? "1" : "0"),
                    "weapontransmog=" + (WeaponTransmog ? "1" : "0"),
                    "warnsave=" + (WarnUnsaved ? "1" : "0"),
                    "backupdir=" + (BackupDir ?? ""),
                    "backupmax=" + BackupMax.ToString(),
                    "backuplayout=" + BackupLayout.ToString(),
                    "backupnormalname=" + BackupNormalName,
                    "backuptransfername=" + BackupTransferName,
                    "colormenus=" + (ColorMenus ? "1" : "0"),
                    "colorranks=" + (ColorRanks ? "1" : "0"),
                    "zoom=" + Zoom.ToString(),
                    "winx=" + WinX.ToString(),
                    "winy=" + WinY.ToString(),
                    "winw=" + WinW.ToString(),
                    "winh=" + WinH.ToString(),
                    "winmax=" + (WinMax ? "1" : "0")
                });
                foreach (string r in Recent) lines.Add("recent=" + r);
                foreach (string fav in Favorites) lines.Add("favorite=" + fav);
                for (int d = 0; d < 3; d++)
                {
                    lines.Add("destname" + (d + 1).ToString() + "=" + (DestName[d] ?? ""));
                    lines.Add("destpath" + (d + 1).ToString() + "=" + (DestPath[d] ?? ""));
                    lines.Add("destcolor" + (d + 1).ToString() + "=" + (DestColor[d] ?? ""));
                }
                File.WriteAllLines(ConfigPath, lines.ToArray());
            }
            catch (Exception) { }
        }

        // Zoom des petites fenêtres (carte de chasseur, Palico, talisman...) : même taux que la fenêtre principale.
        // Ces fenêtres se mettent à l'échelle toutes seules quand leur police change (AutoScaleMode = Font).
        public static void ApplyZoom(Form f)
        {
            if (f == null || Zoom == 100) return;
            try
            {
                float k = Zoom / 100f;
                f.Font = new Font(f.Font.FontFamily, f.Font.Size * k, f.Font.Style);
                ScaleColumns(f, k);
            }
            catch (Exception) { }
        }

        static void ScaleColumns(Control c, float k)
        {
            ListView lv = c as ListView;
            if (lv != null)
            {
                foreach (ColumnHeader h in lv.Columns) h.Width = (int)(h.Width * k);
            }
            foreach (Control child in c.Controls) ScaleColumns(child, k);
        }

        // Applique le thème courant à un contrôle et à tous ses enfants.
        // Le coffre graphique (ChestGridMarker) garde les couleurs du jeu.
        public static void Apply(Control c)
        {
            if (c == null) return;
            if (c is ChestGridMarker) return;
            ApplyOne(c);
            foreach (Control child in c.Controls) Apply(child);
        }

        static void ApplyOne(Control c)
        {
            if (c is Form)
            {
                c.BackColor = Back;
                c.ForeColor = Fore;
                HookNative(c);
                return;
            }
            if (c is MenuStrip)
            {
                MenuStrip ms = (MenuStrip)c;
                ms.Renderer = MenuRenderer();
                ms.BackColor = Back;
                ms.ForeColor = Fore;
                return;
            }
            if (c is TabControl)
            {
                c.BackColor = Back;
                c.ForeColor = Fore;
                c.Invalidate();
                return;
            }
            if (c is TabPage)
            {
                TabPage tp = (TabPage)c;
                tp.UseVisualStyleBackColor = !Dark;
                if (Dark) tp.BackColor = Back;
                tp.ForeColor = Fore;
                return;
            }
            if (c is ComboBox)
            {
                ComboBox cb = (ComboBox)c;
                cb.FlatStyle = Dark ? FlatStyle.Flat : FlatStyle.Standard;
                cb.BackColor = Field;
                cb.ForeColor = FieldFore;
                AttachComboPainter(cb);
                HookNative(c);
                cb.Invalidate();
                return;
            }
            if (c is ListView || c is TreeView)
            {
                c.BackColor = Field;
                c.ForeColor = FieldFore;
                ListView lvw = c as ListView;
                if (lvw != null)
                {
                    // En-têtes de colonnes dessinés par nous : texte clair sur fond sombre (Windows les laisse noirs sur noir).
                    lvw.DrawColumnHeader -= HeaderDraw;
                    lvw.DrawItem -= ListItemDraw;
                    lvw.DrawSubItem -= ListSubItemDraw;
                    if (Dark)
                    {
                        lvw.DrawColumnHeader += HeaderDraw;
                        lvw.DrawItem += ListItemDraw;
                        lvw.DrawSubItem += ListSubItemDraw;
                    }
                    lvw.OwnerDraw = Dark;
                    lvw.Invalidate();
                }
                HookNative(c);
                return;
            }
            if (c is TextBoxBase || c is ListBox || c is NumericUpDown)
            {
                c.BackColor = Field;
                c.ForeColor = FieldFore;
                return;
            }
            if (c is Button)
            {
                Button b = (Button)c;
                if (Dark)
                {
                    b.FlatStyle = FlatStyle.Flat;
                    b.UseVisualStyleBackColor = false;
                    b.BackColor = Color.FromArgb(62, 64, 74);
                    b.ForeColor = Fore;
                    b.FlatAppearance.BorderColor = Color.FromArgb(90, 92, 104);
                }
                else
                {
                    b.FlatStyle = FlatStyle.Standard;
                    b.BackColor = SystemColors.Control;
                    b.UseVisualStyleBackColor = true;
                    b.ForeColor = SystemColors.ControlText;
                }
                return;
            }
            c.BackColor = Back;
            c.ForeColor = Fore;
        }

        // ---- Éléments natifs (barre de titre, barres de défilement, listes déroulantes) ----

        static void HookNative(Control c)
        {
            c.HandleCreated -= NativeHandleCreated;
            c.HandleCreated += NativeHandleCreated;
            if (c.IsHandleCreated) ApplyNative(c);
        }

        static void NativeHandleCreated(object sender, EventArgs e)
        {
            ApplyNative((Control)sender);
        }

        static void ApplyNative(Control c)
        {
            try
            {
                if (c is Form)
                {
                    int value = Dark ? 1 : 0;
                    // 20 = Windows 10 (2004) et 11 ; 19 = versions plus anciennes de Windows 10.
                    if (DwmSetWindowAttribute(c.Handle, 20, ref value, 4) != 0)
                        DwmSetWindowAttribute(c.Handle, 19, ref value, 4);
                    return;
                }
                if (!Dark)
                {
                    SetWindowTheme(c.Handle, null, null);
                    if (c is ListView) SetHeaderTheme((ListView)c, null);
                    return;
                }
                if (c is ListView)
                {
                    SetWindowTheme(c.Handle, "DarkMode_Explorer", null);
                    SetHeaderTheme((ListView)c, "DarkMode_ItemsView");
                }
                else if (c is TreeView) SetWindowTheme(c.Handle, "DarkMode_Explorer", null);
                else if (c is ComboBox) SetWindowTheme(c.Handle, "DarkMode_CFD", null);
            }
            catch (Exception) { }
        }

        // ---- Listes : en-têtes sombres ----

        static void HeaderDraw(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            using (SolidBrush b = new SolidBrush(Color.FromArgb(52, 54, 64)))
                e.Graphics.FillRectangle(b, e.Bounds);
            using (Pen p = new Pen(Color.FromArgb(90, 92, 104)))
                e.Graphics.DrawRectangle(p, e.Bounds.X, e.Bounds.Y, e.Bounds.Width - 1, e.Bounds.Height - 1);
            Rectangle tr = new Rectangle(e.Bounds.X + 6, e.Bounds.Y, Math.Max(1, e.Bounds.Width - 10), e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, e.Header.Text, e.Font, tr, Color.Gainsboro,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }

        static void ListItemDraw(object sender, DrawListViewItemEventArgs e)
        {
            ListView lv = sender as ListView;
            // En mode détails, ce sont les cases (sous-éléments) qui se dessinent ; sinon, dessin normal.
            if (lv == null || lv.View != View.Details) e.DrawDefault = true;
        }

        // Ligne sélectionnée : fond bleu foncé, texte jaune clair. Les textes « noirs » de l'éditeur deviennent clairs.
        static void ListSubItemDraw(object sender, DrawListViewSubItemEventArgs e)
        {
            ListView lv = (ListView)sender;
            bool sel = e.Item.Selected;
            Rectangle b = e.Bounds;
            if (e.ColumnIndex == 0 && lv.Columns.Count > 0) b.Width = Math.Min(b.Width, lv.Columns[0].Width);
            Color back = sel ? Color.FromArgb(48, 74, 120) : lv.BackColor;
            Color fore;
            if (sel)
            {
                fore = Color.FromArgb(255, 240, 150);
            }
            else
            {
                fore = e.Item.UseItemStyleForSubItems ? e.Item.ForeColor : e.SubItem.ForeColor;
                int argb = fore.ToArgb();
                if (argb == Color.Black.ToArgb() || argb == SystemColors.WindowText.ToArgb() || argb == SystemColors.ControlText.ToArgb())
                    fore = FieldFore;
            }
            using (SolidBrush br = new SolidBrush(back))
                e.Graphics.FillRectangle(br, b);
            using (Pen p = new Pen(Color.FromArgb(64, 66, 78)))
                e.Graphics.DrawLine(p, b.Left, b.Bottom - 1, b.Right, b.Bottom - 1);
            Rectangle tr = new Rectangle(b.X + 6, b.Y, Math.Max(1, b.Width - 8), b.Height);
            TextRenderer.DrawText(e.Graphics, e.SubItem.Text, e.SubItem.Font, tr, fore,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        // ---- Listes déroulantes désactivées : fond sombre ----

        static readonly Dictionary<ComboBox, DarkComboPainter> comboPainters = new Dictionary<ComboBox, DarkComboPainter>();

        static void AttachComboPainter(ComboBox cb)
        {
            DarkComboPainter painter;
            if (!comboPainters.TryGetValue(cb, out painter))
            {
                painter = new DarkComboPainter(cb);
                comboPainters[cb] = painter;
                DarkComboPainter captured = painter;
                cb.HandleCreated += delegate { captured.Attach(); };
                cb.HandleDestroyed += delegate { captured.Detach(); };
            }
            painter.Attach();
        }

        // Une liste déroulante désactivée se dessine toujours en clair sous Windows : on repeint par-dessus.
        class DarkComboPainter : NativeWindow
        {
            [DllImport("gdi32.dll")]
            static extern int SetTextColor(IntPtr hdc, int color);
            [DllImport("gdi32.dll")]
            static extern int SetBkColor(IntPtr hdc, int color);
            [DllImport("gdi32.dll")]
            static extern IntPtr CreateSolidBrush(int color);
            [DllImport("user32.dll")]
            static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string title);
            [DllImport("user32.dll")]
            static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
            [DllImport("user32.dll")]
            static extern bool ScreenToClient(IntPtr hWnd, ref POINT pt);

            [StructLayout(LayoutKind.Sequential)]
            struct RECT { public int Left, Top, Right, Bottom; }
            [StructLayout(LayoutKind.Sequential)]
            struct POINT { public int X, Y; }

            const int WM_PAINT = 0x000F;
            const int WM_CTLCOLOREDIT = 0x0133;
            const int WM_CTLCOLORSTATIC = 0x0138;

            static IntPtr brush = IntPtr.Zero;
            static readonly Color FieldBack = Color.FromArgb(38, 40, 48);
            static readonly Color DisabledText = Color.FromArgb(130, 132, 142);

            readonly ComboBox combo;
            bool assigned;

            public DarkComboPainter(ComboBox combo)
            {
                this.combo = combo;
            }

            public void Attach()
            {
                if (!combo.IsHandleCreated) return;
                if (assigned && Handle == combo.Handle) return;
                if (assigned) { ReleaseHandle(); assigned = false; }
                AssignHandle(combo.Handle);
                assigned = true;
            }

            public void Detach()
            {
                if (assigned) { ReleaseHandle(); assigned = false; }
            }

            bool Active { get { return ThemeSettings.Dark && !combo.Enabled; } }

            protected override void WndProc(ref Message m)
            {
                if (Active && (m.Msg == WM_CTLCOLOREDIT || m.Msg == WM_CTLCOLORSTATIC))
                {
                    if (brush == IntPtr.Zero) brush = CreateSolidBrush(ColorTranslator.ToWin32(FieldBack));
                    SetTextColor(m.WParam, ColorTranslator.ToWin32(DisabledText));
                    SetBkColor(m.WParam, ColorTranslator.ToWin32(FieldBack));
                    m.Result = brush;
                    return;
                }
                base.WndProc(ref m);
                if (m.Msg == WM_PAINT && Active) PaintDisabled();
            }

            void PaintDisabled()
            {
                try
                {
                    Rectangle rc = combo.ClientRectangle;
                    if (rc.Width < 4 || rc.Height < 4) return;
                    using (Graphics g = Graphics.FromHwnd(combo.Handle))
                    {
                        // Si la liste est modifiable, la zone de texte est une fenêtre à part : on ne la repeint pas.
                        IntPtr edit = FindWindowEx(combo.Handle, IntPtr.Zero, "Edit", null);
                        bool hasEdit = edit != IntPtr.Zero;
                        if (hasEdit)
                        {
                            RECT r;
                            if (GetWindowRect(edit, out r))
                            {
                                POINT tl = new POINT(); tl.X = r.Left; tl.Y = r.Top;
                                POINT br = new POINT(); br.X = r.Right; br.Y = r.Bottom;
                                ScreenToClient(combo.Handle, ref tl);
                                ScreenToClient(combo.Handle, ref br);
                                g.SetClip(new Rectangle(tl.X, tl.Y, br.X - tl.X, br.Y - tl.Y), System.Drawing.Drawing2D.CombineMode.Exclude);
                            }
                        }
                        using (SolidBrush b = new SolidBrush(FieldBack))
                            g.FillRectangle(b, rc);
                        using (Pen p = new Pen(Color.FromArgb(70, 72, 84)))
                            g.DrawRectangle(p, 0, 0, rc.Width - 1, rc.Height - 1);

                        int arrowW = SystemInformation.VerticalScrollBarWidth;
                        int cx = rc.Width - arrowW / 2 - 1;
                        int cy = rc.Height / 2;
                        Point[] tri = new Point[]
                        {
                            new Point(cx - 4, cy - 2),
                            new Point(cx + 4, cy - 2),
                            new Point(cx, cy + 2)
                        };
                        using (SolidBrush ab = new SolidBrush(DisabledText))
                            g.FillPolygon(ab, tri);

                        if (!hasEdit && combo.Text.Length > 0)
                        {
                            Rectangle tr = new Rectangle(4, 0, Math.Max(1, rc.Width - arrowW - 6), rc.Height);
                            TextRenderer.DrawText(g, combo.Text, combo.Font, tr, DisabledText,
                                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                        }
                    }
                }
                catch (Exception) { }
            }
        }

        // L'en-tête des colonnes d'une liste est une fenêtre à part.
        static void SetHeaderTheme(ListView lv, string theme)
        {
            IntPtr header = SendMessage(lv.Handle, 0x101F, IntPtr.Zero, IntPtr.Zero); // LVM_GETHEADER
            if (header != IntPtr.Zero) SetWindowTheme(header, theme, null);
        }

        // ---- Menus ----

        class DarkColorTable : ProfessionalColorTable
        {
            static readonly Color bar = Color.FromArgb(32, 33, 38);
            static readonly Color drop = Color.FromArgb(40, 41, 48);
            static readonly Color hot = Color.FromArgb(70, 72, 84);
            static readonly Color border = Color.FromArgb(90, 92, 104);

            public override Color MenuStripGradientBegin { get { return bar; } }
            public override Color MenuStripGradientEnd { get { return bar; } }
            public override Color ToolStripDropDownBackground { get { return drop; } }
            public override Color ImageMarginGradientBegin { get { return drop; } }
            public override Color ImageMarginGradientMiddle { get { return drop; } }
            public override Color ImageMarginGradientEnd { get { return drop; } }
            public override Color MenuItemSelected { get { return hot; } }
            public override Color MenuItemSelectedGradientBegin { get { return hot; } }
            public override Color MenuItemSelectedGradientEnd { get { return hot; } }
            public override Color MenuItemPressedGradientBegin { get { return hot; } }
            public override Color MenuItemPressedGradientMiddle { get { return hot; } }
            public override Color MenuItemPressedGradientEnd { get { return hot; } }
            public override Color MenuItemBorder { get { return border; } }
            public override Color MenuBorder { get { return border; } }
            public override Color SeparatorDark { get { return border; } }
            public override Color SeparatorLight { get { return border; } }
            public override Color CheckBackground { get { return hot; } }
            public override Color CheckSelectedBackground { get { return hot; } }
            public override Color CheckPressedBackground { get { return hot; } }
        }

        class LightRenderer : ToolStripProfessionalRenderer
        {
            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                e.TextColor = TopMenuColor(e.Item, e.TextColor);
                base.OnRenderItemText(e);
            }
        }

        class DarkRenderer : ToolStripProfessionalRenderer
        {
            public DarkRenderer() : base(new DarkColorTable())
            {
                RoundedEdges = false;
            }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                e.TextColor = TopMenuColor(e.Item, Color.Gainsboro);
                base.OnRenderItemText(e);
            }

            protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
            {
                e.ArrowColor = Color.Gainsboro;
                base.OnRenderArrow(e);
            }
        }
    }

    // Les contrôles qui dessinent le coffre à la main héritent de cette base
    // pour que le thème ne touche pas à leurs couleurs.
    class ChestGridMarker : Panel
    {
    }

    // Onglets dessinés à la main : la bande des onglets, le cadre et le texte suivent le thème (clair ou sombre).
    class ThemedTabControl : TabControl
    {
        public ThemedTabControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Multiline = true;
            // À régler ici, avant la création de la fenêtre : le changer plus tard recrée la fenêtre et plante.
            SizeMode = TabSizeMode.Fixed;
        }

        // Les onglets sont assez larges pour le texte le plus long, à n'importe quelle taille d'écriture.
        bool fitting;

        public void FitTabs()
        {
            if (fitting || TabCount == 0 || !IsHandleCreated) return;
            fitting = true;
            try
            {
                int w = 0;
                int h = 0;
                foreach (TabPage tp in TabPages)
                {
                    Size sz = TextRenderer.MeasureText(tp.Text, Font);
                    if (sz.Width > w) w = sz.Width;
                    if (sz.Height > h) h = sz.Height;
                }
                Size want = new Size(w + 30, h + 14);
                if (ItemSize != want) ItemSize = want;
            }
            finally
            {
                fitting = false;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            FitTabs();
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            FitTabs();
        }

        // Couleur du texte d'un onglet : une couleur par onglet (atténuée tant qu'aucune sauvegarde n'est chargée).
        Color TabTextColor(int i, Color normal, Color back)
        {
            if (!ThemeSettings.ColorMenus) return normal;
            Color c = ThemeSettings.TabColor(i);
            if (!Enabled) c = Color.FromArgb((c.R + back.R * 2) / 3, (c.G + back.G * 2) / 3, (c.B + back.B * 2) / 3);
            return c;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            FitTabs();
            Graphics g = e.Graphics;
            bool dark = ThemeSettings.Dark;
            Color back = dark ? Color.FromArgb(32, 33, 38) : SystemColors.Control;
            Color idle = dark ? Color.FromArgb(44, 45, 52) : SystemColors.Control;
            Color selected = dark ? Color.FromArgb(74, 76, 90) : SystemColors.ControlLightLight;
            Color border = dark ? Color.FromArgb(96, 98, 112) : SystemColors.ControlDark;
            Color text = dark ? Color.Gainsboro : SystemColors.ControlText;
            if (!Enabled) text = Color.Gray;

            g.Clear(back);

            int tabsBottom = 0;
            for (int i = 0; i < TabCount; i++)
            {
                Rectangle r = GetTabRect(i);
                if (r.Bottom > tabsBottom) tabsBottom = r.Bottom;
            }
            using (Pen pen = new Pen(border))
            {
                g.DrawRectangle(pen, 0, tabsBottom, Width - 1, Height - tabsBottom - 1);
                for (int i = 0; i < TabCount; i++)
                {
                    Rectangle r = GetTabRect(i);
                    bool isSel = (i == SelectedIndex);
                    using (SolidBrush br = new SolidBrush(isSel ? selected : idle))
                        g.FillRectangle(br, r);
                    g.DrawRectangle(pen, r.X, r.Y, r.Width - 1, r.Height - 1);
                    TextRenderer.DrawText(g, TabPages[i].Text, Font, r, TabTextColor(i, text, back),
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                }
                if (SelectedIndex >= 0)
                {
                    // La page choisie se « raccorde » à son onglet.
                    Rectangle sr = GetTabRect(SelectedIndex);
                    using (SolidBrush br = new SolidBrush(selected))
                        g.FillRectangle(br, sr.X + 1, tabsBottom - 1, sr.Width - 2, 2);
                }
            }
        }
    }
}
