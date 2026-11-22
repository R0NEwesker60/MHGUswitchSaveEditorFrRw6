// MHXXSwitchSaveEditor – version française (modifiée). Licence : GNU GPL v3 (voir LICENSE).
// Fichier créé en version 33 : morceau de la fenêtre principale (classe partielle MainForm), sans changement de comportement.
// Contenu : Emplacements de sauvegarde (boutons, échange, copie), fichiers récents / favoris, transfert A / B.
using MHXXSaveEditor.Data;
using MHXXSaveEditor.Forms;
using MHXXSaveEditor.Util;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace MHXXSaveEditor
{
    public partial class MainForm : Form
    {
        // Pseudo du personnage de l'emplacement n (1 à 3) d'après la sauvegarde chargée ; null si l'emplacement est vide.
        // Rang de chasseur (RC) d'un emplacement ; -1 s'il est vide ou illisible.
        int SlotRank(int slot)
        {
            try
            {
                if (saveFile == null || saveFile.Length < 0x20 || saveFile[3 + slot] != 1) return -1;
                int off = BitConverter.ToInt32(saveFile, 0x10 + 4 * (slot - 1));
                int p = off + Offsets.HUNTER_RANK_OFFSET;
                if (off < 0 || p + 2 > saveFile.Length) return -1;
                return BitConverter.ToUInt16(saveFile, p);
            }
            catch (Exception) { return -1; }
        }

        string SlotName(int slot)
        {
            try
            {
                if (saveFile == null || saveFile.Length < 0x20 || saveFile[3 + slot] != 1) return null;
                int off = BitConverter.ToInt32(saveFile, 0x10 + 4 * (slot - 1));
                int p = off + Offsets.NAME_OFFSET;
                if (off < 0 || p + 32 > saveFile.Length) return "?";
                string n = Encoding.UTF8.GetString(saveFile, p, 32).Split('\0')[0].Trim();
                return n.Length == 0 ? "?" : n;
            }
            catch (Exception) { return "?"; }
        }

        // Met le pseudo de chaque emplacement dans les menus « Choisir », « Supprimer », « Échanger », « Dupliquer » et dans la liste du haut.
        void UpdateSlotMenuTexts()
        {
            ToolStripMenuItem[] choose = { toolStripMenuItemSaveSlot1, toolStripMenuItemSaveSlot2, toolStripMenuItemSaveSlot3 };
            ToolStripMenuItem[] del = { slot1ToolStripMenuItem, slot2ToolStripMenuItem, slot3ToolStripMenuItem };
            string empty = RomfsConfig.L("(empty)", "(vide)");
            string slotWord = RomfsConfig.L("Slot ", "Emplacement ");
            string[] names = new string[3];
            bool[] used = new bool[3];
            int usedCount = 0;
            for (int i = 0; i < 3; i++)
            {
                string n = SlotName(i + 1);
                used[i] = n != null;
                if (used[i]) usedCount++;
                names[i] = n ?? empty;
                choose[i].Text = (i + 1).ToString() + " - " + names[i];
                choose[i].Checked = currentPlayer == i + 1;
                del[i].Text = slotWord + (i + 1).ToString() + " - " + names[i];
            }

            if (menuSwapSlots != null && swapItems != null)
            {
                int[,] pairs = { { 1, 2 }, { 1, 3 }, { 2, 3 } };
                bool any = false;
                for (int k = 0; k < 3; k++)
                {
                    int a = pairs[k, 0], b = pairs[k, 1];
                    swapItems[k].Text = slotWord + a.ToString() + " (" + names[a - 1] + ")  \u2194  " + b.ToString() + " (" + names[b - 1] + ")";
                    swapItems[k].Enabled = used[a - 1] || used[b - 1];
                    if (swapItems[k].Enabled) any = true;
                }
                menuSwapSlots.Enabled = any;
            }

            if (menuCopySlots != null && copySrc != null)
            {
                for (int src = 0; src < 3; src++)
                {
                    copySrc[src].Text = slotWord + (src + 1).ToString() + " (" + names[src] + ")";
                    copySrc[src].Enabled = used[src];
                    int di = 0;
                    for (int dst = 0; dst < 3; dst++)
                    {
                        if (dst == src) continue;
                        copyDst[src][di].Text = RomfsConfig.L("to slot ", "vers l'emplacement ") + (dst + 1).ToString() + " (" + names[dst] + ")"
                                              + (used[dst] ? RomfsConfig.L(" - will be REPLACED", " - sera REMPLACÉ") : "");
                        di++;
                    }
                }
                menuCopySlots.Enabled = usedCount > 0;
            }

            UpdateSlotButtons();
        }

        // Change de personnage (emplacement 1 à 3), comme Fichier > Choisir l'emplacement de sauvegarde.
        void SwitchToSlot(int n)
        {
            if (n == currentPlayer) return;
            if (SlotName(n) == null) return; // emplacement vide : on ne change pas
            switch (n)
            {
                case 1: ToolStripMenuItemSaveSlot1_Click(this, EventArgs.Empty); break;
                case 2: ToolStripMenuItemSaveSlot2_Click(this, EventArgs.Empty); break;
                default: ToolStripMenuItemSaveSlot3_Click(this, EventArgs.Empty); break;
            }
        }

        // V39 : nombre de monstres EX (Déviants) DIFFÉRENTS terminés par le personnage d'un emplacement (sauvegardes MHGU Switch) ; -1 si inconnu.
        // La liste est de 24 cases (une par monstre EX, 18 utilisées), à l'octet 0x3191 de l'emplacement. Trouvé en comparant des sauvegardes
        // avant / après, et paliers vérifiés en jeu : 1 monstre = petite couronne, 10 = couronne moyenne, 18 = couronne maximale.
        int SlotExCount(int slot)
        {
            try
            {
                if (saveFile == null || saveFile.Length != MHGU_SAVE_SIZE - 36 || saveFile[3 + slot] != 1) return -1;
                int off = BitConverter.ToInt32(saveFile, 0x10 + 4 * (slot - 1));
                int p = off + 0x3191;
                if (off < 0 || p + 3 > saveFile.Length) return -1;
                int bits = (saveFile[p] | (saveFile[p + 1] << 8) | (saveFile[p + 2] << 16)) & 0x3FFFF;
                int n = 0;
                while (bits != 0) { n += bits & 1; bits >>= 1; }
                return n;
            }
            catch (Exception) { return -1; }
        }

        // 0 = pas de couronne, 1 = petite, 2 = moyenne, 3 = maximale.
        static int CrownLevel(int exCount)
        {
            if (exCount >= 18) return 3;
            if (exCount >= 10) return 2;
            if (exCount >= 1) return 1;
            return 0;
        }

        static string CrownName(int level)
        {
            if (level == 3) return RomfsConfig.L("maximum crown", "couronne maximale");
            if (level == 2) return RomfsConfig.L("medium crown", "couronne moyenne");
            return RomfsConfig.L("small crown", "petite couronne");
        }

        // Dessine la couronne à droite du pseudo, sur le bouton d'emplacement (le niveau est dans b.Tag).
        void SlotButton_Paint(object sender, PaintEventArgs e)
        {
            Button b = (Button)sender;
            if (!(b.Tag is int)) return;
            int level = (int)b.Tag;
            if (level <= 0) return;
            int size = (int)(b.Height * 0.55f);
            int pad = Math.Max(6, b.Height / 6);
            DrawCrownIcon(e.Graphics, new Rectangle(b.Width - size - pad, (b.Height - size) / 2, size, size), level);
        }

        // Couronnes dessinées par le programme (ce ne sont pas les images du jeu), refaites d'après les captures du jeu :
        // 1 = bulbe jaune avec losange rouge, 2 = couronne dorée à gemme bleue et collerette blanche, 3 = cœur rouge, arcs dorés, gemme rose et quatre étoiles.
        static RectangleF CU(Rectangle r, float x, float y, float w, float h)
        {
            return new RectangleF(r.X + r.Width * x, r.Y + r.Height * y, r.Width * w, r.Height * h);
        }

        static PointF[] CP(Rectangle r, params float[] xy)
        {
            PointF[] p = new PointF[xy.Length / 2];
            for (int i = 0; i < p.Length; i++) p[i] = new PointF(r.X + r.Width * xy[2 * i], r.Y + r.Height * xy[2 * i + 1]);
            return p;
        }

        static void CFillEllipse(Graphics g, RectangleF e, Color fill, float pw)
        {
            using (Brush br = new SolidBrush(fill)) g.FillEllipse(br, e);
            using (Pen pe = new Pen(Color.Black, pw)) g.DrawEllipse(pe, e);
        }

        static void CFillPoly(Graphics g, PointF[] pts, Color fill, Color edge, float pw)
        {
            using (Brush br = new SolidBrush(fill)) g.FillPolygon(br, pts);
            using (Pen pe = new Pen(edge, pw)) { pe.LineJoin = LineJoin.Round; g.DrawPolygon(pe, pts); }
        }

        static void CFillRoundRect(Graphics g, RectangleF e, float rad, Color fill, float pw)
        {
            using (GraphicsPath gp = new GraphicsPath())
            {
                float d = rad * 2f;
                gp.AddArc(e.X, e.Y, d, d, 180, 90);
                gp.AddArc(e.Right - d, e.Y, d, d, 270, 90);
                gp.AddArc(e.Right - d, e.Bottom - d, d, d, 0, 90);
                gp.AddArc(e.X, e.Bottom - d, d, d, 90, 90);
                gp.CloseFigure();
                using (Brush br = new SolidBrush(fill)) g.FillPath(br, gp);
                using (Pen pe = new Pen(Color.Black, pw)) { pe.LineJoin = LineJoin.Round; g.DrawPath(pe, gp); }
            }
        }

        static void CStar(Graphics g, Rectangle r, float cx, float cy, float rad, float pw)
        {
            PointF[] pts = new PointF[8];
            for (int i = 0; i < 8; i++)
            {
                double a = -Math.PI / 2 + i * Math.PI / 4;
                float rr = (i % 2 == 0) ? rad : rad * 0.32f;
                pts[i] = new PointF(r.X + r.Width * cx + (float)Math.Cos(a) * r.Width * rr, r.Y + r.Height * cy + (float)Math.Sin(a) * r.Width * rr);
            }
            CFillPoly(g, pts, Color.FromArgb(255, 215, 50), Color.Black, pw);
            using (Brush wh = new SolidBrush(Color.FromArgb(255, 250, 220)))
                g.FillEllipse(wh, r.X + r.Width * cx - r.Width * rad * 0.18f, r.Y + r.Height * cy - r.Width * rad * 0.18f, r.Width * rad * 0.36f, r.Width * rad * 0.36f);
        }

        static void DrawCrownIcon(Graphics g, Rectangle r, int level)
        {
            SmoothingMode old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float pw = Math.Max(1f, r.Width * 0.06f);
            Color gold = Color.FromArgb(252, 200, 30);
            Color darkGold = Color.FromArgb(185, 125, 20);
            Color white = Color.FromArgb(245, 248, 252);
            if (level == 1)
            {
                // collerette bleu clair, bulbe jaune, losange rouge, petite perle blanche
                CFillRoundRect(g, CU(r, 0.24f, 0.72f, 0.52f, 0.24f), r.Width * 0.04f, Color.FromArgb(190, 220, 245), pw);
                CFillEllipse(g, CU(r, 0.03f, 0.16f, 0.94f, 0.68f), gold, pw);
                using (Brush shade = new SolidBrush(Color.FromArgb(235, 170, 20)))
                    g.FillPie(shade, CU(r, 0.12f, 0.30f, 0.76f, 0.50f).X, CU(r, 0.12f, 0.30f, 0.76f, 0.50f).Y, CU(r, 0.12f, 0.30f, 0.76f, 0.50f).Width, CU(r, 0.12f, 0.30f, 0.76f, 0.50f).Height, 20, 140);
                CFillPoly(g, CP(r, 0.5f, 0.30f, 0.66f, 0.48f, 0.5f, 0.66f, 0.34f, 0.48f), Color.FromArgb(235, 25, 30), Color.FromArgb(150, 10, 20), Math.Max(1f, pw * 0.5f));
                CFillEllipse(g, CU(r, 0.43f, 0.03f, 0.14f, 0.14f), white, Math.Max(1f, pw * 0.8f));
            }
            else if (level == 2)
            {
                // collerette blanche à quatre dents, deux pétales, grand dôme doré, gemme bleue, bouton doré
                CFillRoundRect(g, CU(r, 0.20f, 0.74f, 0.60f, 0.12f), r.Width * 0.02f, white, pw);
                for (int k = 0; k < 4; k++)
                    CFillRoundRect(g, CU(r, 0.22f + k * 0.15f, 0.82f, 0.11f, 0.15f), r.Width * 0.03f, white, pw);
                CFillEllipse(g, CU(r, 0.02f, 0.28f, 0.32f, 0.52f), gold, pw);
                CFillEllipse(g, CU(r, 0.66f, 0.28f, 0.32f, 0.52f), gold, pw);
                CFillRoundRect(g, CU(r, 0.44f, 0.02f, 0.12f, 0.20f), r.Width * 0.05f, gold, pw);
                CFillEllipse(g, CU(r, 0.20f, 0.12f, 0.60f, 0.70f), gold, pw);
                using (Pen ring = new Pen(darkGold, Math.Max(1f, pw * 0.8f)))
                {
                    RectangleF e = CU(r, 0.27f, 0.20f, 0.46f, 0.58f);
                    g.DrawEllipse(ring, e);
                }
                CFillPoly(g, CP(r, 0.5f, 0.28f, 0.60f, 0.38f, 0.60f, 0.54f, 0.5f, 0.64f, 0.40f, 0.54f, 0.40f, 0.38f), Color.FromArgb(40, 215, 240), Color.Black, Math.Max(1f, pw * 0.7f));
            }
            else
            {
                // cœur rouge, deux arcs dorés, flèche dorée, gemme rose, barre dorée, quatre étoiles
                CFillEllipse(g, CU(r, 0.06f, 0.24f, 0.88f, 0.60f), Color.FromArgb(215, 20, 25), pw);
                CFillRoundRect(g, CU(r, 0.26f, 0.22f, 0.48f, 0.56f), r.Width * 0.12f, Color.FromArgb(235, 30, 35), pw);
                RectangleF bar = CU(r, 0.22f, 0.86f, 0.56f, 0.10f);
                using (Brush br = new SolidBrush(gold)) g.FillRectangle(br, bar);
                using (Pen pe = new Pen(Color.Black, pw)) g.DrawRectangle(pe, bar.X, bar.Y, bar.Width, bar.Height);
                using (Pen under = new Pen(Color.Black, r.Width * 0.16f))
                using (Pen arm = new Pen(gold, r.Width * 0.09f))
                {
                    under.StartCap = under.EndCap = LineCap.Round;
                    arm.StartCap = arm.EndCap = LineCap.Round;
                    PointF[] left = CP(r, 0.20f, 0.30f, 0.30f, 0.62f, 0.40f, 0.84f);
                    PointF[] right = CP(r, 0.80f, 0.30f, 0.70f, 0.62f, 0.60f, 0.84f);
                    g.DrawCurve(under, left); g.DrawCurve(under, right);
                    g.DrawCurve(arm, left); g.DrawCurve(arm, right);
                }
                CFillPoly(g, CP(r, 0.5f, 0.02f, 0.60f, 0.18f, 0.54f, 0.18f, 0.54f, 0.40f, 0.46f, 0.40f, 0.46f, 0.18f, 0.40f, 0.18f), gold, Color.Black, pw);
                CFillPoly(g, CP(r, 0.5f, 0.42f, 0.60f, 0.52f, 0.60f, 0.66f, 0.5f, 0.76f, 0.40f, 0.66f, 0.40f, 0.52f), Color.FromArgb(240, 150, 190), Color.FromArgb(120, 20, 80), Math.Max(1f, pw * 0.9f));
                CStar(g, r, 0.14f, 0.16f, 0.15f, pw * 0.8f);
                CStar(g, r, 0.86f, 0.16f, 0.15f, pw * 0.8f);
                CStar(g, r, 0.12f, 0.86f, 0.14f, pw * 0.8f);
                CStar(g, r, 0.88f, 0.86f, 0.14f, pw * 0.8f);
            }
            g.SmoothingMode = old;
        }

        // Les trois boutons d'emplacement, en haut à gauche de l'onglet Général : « 1 - Pseudo », « 2 - (vide) », « 3 - Pseudo ».
        void CreateSlotButtons()
        {
            slotButtons = new Button[3];
            for (int i = 0; i < 3; i++)
            {
                int n = i + 1;
                Button b = new Button();
                b.FlatStyle = FlatStyle.Flat;
                b.TextAlign = ContentAlignment.MiddleLeft;
                b.UseVisualStyleBackColor = false;
                b.TabStop = false;
                b.Visible = false;
                b.Click += delegate { SwitchToSlot(n); };
                b.Paint += SlotButton_Paint;
                Controls.Add(b);
                slotButtons[i] = b;
            }
        }

        void UpdateSlotButtons()
        {
            if (slotButtons == null || tabControlMain == null || generalTab == null) return;
            string empty = RomfsConfig.L("(empty)", "(vide)");
            string[] text = new string[3];
            bool[] used = new bool[3];
            int usedCount = 0;
            for (int i = 0; i < 3; i++)
            {
                string n = SlotName(i + 1);
                used[i] = n != null;
                if (used[i]) usedCount++;
                text[i] = (i + 1).ToString() + " - " + (n ?? empty);
            }
            bool show = tabControlMain.SelectedTab == generalTab && saveFile != null && usedCount > 0;
            for (int i = 0; i < 3; i++) slotButtons[i].Visible = show;
            if (!show) return;

            int pad = Math.Max(8, Font.Height);
            int gap = Math.Max(4, Font.Height / 3);
            int w = 0;
            for (int i = 0; i < 3; i++) w = Math.Max(w, TextRenderer.MeasureText(text[i], Font).Width);
            // place pour la couronne à droite du pseudo (sauvegardes MHGU Switch seulement)
            int crownRoom = (saveFile != null && saveFile.Length == MHGU_SAVE_SIZE - 36) ? (int)(Font.Height * 1.8f) : 0;
            w += Font.Height * 2 + crownRoom;
            int h = (int)(Font.Height * 2.2f);
            Color accent = ThemeSettings.Dark ? Color.FromArgb(60, 110, 170) : SystemColors.Highlight;
            for (int i = 0; i < 3; i++)
            {
                Button b = slotButtons[i];
                b.Text = text[i];
                b.Size = new Size(w, h);
                b.Enabled = used[i];
                bool selected = currentPlayer == i + 1;
                int rank = SlotRank(i + 1);
                bool byRank = ThemeSettings.ColorRanks && used[i] && rank > 0;
                Color selBack = accent;
                if (byRank) selBack = ThemeSettings.Dark ? Color.FromArgb(38, 72, 120) : Color.FromArgb(205, 225, 245); // fond plus sombre / plus clair : les couleurs de rang restent lisibles
                b.BackColor = selected ? selBack : ThemeSettings.Field;
                b.ForeColor = selected ? Color.White : (used[i] ? ThemeSettings.FieldFore : Color.Gray);
                if (byRank) b.ForeColor = ThemeSettings.RankColor(rank);
                int exCount = used[i] ? SlotExCount(i + 1) : -1;
                int crown = CrownLevel(exCount);
                b.Tag = crown;
                b.Invalidate();
                string tip = used[i] ? RomfsConfig.L("Hunter rank: ", "Rang de chasseur (RC) : ") + rank.ToString() : "";
                if (used[i] && exCount > 0)
                    tip += "\n" + RomfsConfig.L("EX monsters: ", "Monstres EX : ") + exCount.ToString() + " (" + CrownName(crown) + ")";
                gfxTip.SetToolTip(b, tip);
                b.FlatAppearance.BorderColor = selected ? accent : (ThemeSettings.Dark ? Color.FromArgb(90, 92, 104) : SystemColors.ControlDark);
                b.FlatAppearance.MouseOverBackColor = selected ? accent : (ThemeSettings.Dark ? Color.FromArgb(70, 72, 84) : SystemColors.ControlLight);
                b.Location = PointToClient(generalTab.PointToScreen(new Point(pad, pad + i * (h + gap))));
                b.BringToFront();
            }
        }

        // Fichier > Dupliquer un emplacement de sauvegarde (menu à deux niveaux : d'où, puis vers où).
        void BuildCopySlotsMenu()
        {
            ToolStripMenuItem parent = deleteSaveSlotToolStripMenuItem.OwnerItem as ToolStripMenuItem;
            if (parent == null || menuSwapSlots == null) return;
            menuCopySlots = new ToolStripMenuItem(RomfsConfig.L("Duplicate a save slot", "Dupliquer un emplacement de sauvegarde"));
            menuCopySlots.Enabled = false;
            menuCopySlots.ToolTipText = RomfsConfig.L("Copies a character into another slot. You are warned if it replaces an existing character. A safety copy is made first.",
                                                      "Copie un personnage dans un autre emplacement. Un avertissement s'affiche si cela remplace un personnage existant. Une copie de sécurité est faite avant.");
            copySrc = new ToolStripMenuItem[3];
            copyDst = new ToolStripMenuItem[3][];
            for (int src = 0; src < 3; src++)
            {
                int s1 = src + 1;
                copySrc[src] = new ToolStripMenuItem(s1.ToString());
                copyDst[src] = new ToolStripMenuItem[2];
                int di = 0;
                for (int dst = 0; dst < 3; dst++)
                {
                    if (dst == src) continue;
                    int d1 = dst + 1;
                    ToolStripMenuItem it = new ToolStripMenuItem(s1.ToString() + " -> " + d1.ToString());
                    it.Click += delegate { CopySlot(s1, d1); };
                    copyDst[src][di] = it;
                    copySrc[src].DropDownItems.Add(it);
                    di++;
                }
                menuCopySlots.DropDownItems.Add(copySrc[src]);
            }
            parent.DropDownItems.Insert(parent.DropDownItems.IndexOf(menuSwapSlots) + 1, menuCopySlots);
        }

        // Copie le personnage de l'emplacement 'src' dans l'emplacement 'dst' (sauvegardes MHGU Switch). Avertissement renforcé si 'dst' est déjà utilisé.
        void CopySlot(int src, int dst)
        {
            string title = RomfsConfig.L("Duplicate a slot", "Dupliquer un emplacement");
            if (!ConfirmUnsaved("duplicating a slot", "dupliquer un emplacement")) return;
            try
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;
                byte[] raw = File.ReadAllBytes(filePath);
                if (raw.Length != MHGU_SAVE_SIZE)
                {
                    MessageBox.Show(this,
                        RomfsConfig.L("Duplicating a slot is only available for MHGU Switch saves. Nothing was changed.",
                                      "La duplication d'un emplacement n'existe que pour les sauvegardes MHGU Switch. Rien n'a été modifié."),
                        title, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                int fs = 36 + 4 + (src - 1), fd = 36 + 4 + (dst - 1);
                if (raw[fs] != 1)
                {
                    MessageBox.Show(this, RomfsConfig.L("The source slot is empty.", "L'emplacement à copier est vide."),
                        title, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                int absS = 36 + BitConverter.ToInt32(raw, 36 + 0x10 + 4 * (src - 1));
                int absD = 36 + BitConverter.ToInt32(raw, 36 + 0x10 + 4 * (dst - 1));
                bool ok = src != dst && absS >= 36 + 0x30 && absD >= 36 + 0x30
                          && absS + MHGU_SLOT_SIZE <= raw.Length && absD + MHGU_SLOT_SIZE <= raw.Length
                          && (absS + MHGU_SLOT_SIZE <= absD || absD + MHGU_SLOT_SIZE <= absS);
                if (!ok)
                {
                    MessageBox.Show(this,
                        RomfsConfig.L("Unexpected save structure. Nothing was changed.", "Structure de sauvegarde inattendue. Rien n'a été modifié."),
                        title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                string nameS = NameFromRaw(raw, absS, true);
                bool destUsed = raw[fd] == 1;
                string nameD = NameFromRaw(raw, absD, destUsed);
                string msg;
                MessageBoxIcon icon;
                if (destUsed)
                {
                    msg = RomfsConfig.L("WARNING: slot " + dst + " already contains a character (" + nameD + ").\nIt will be REPLACED by a copy of slot " + src + " (" + nameS + ") and lost, except in the safety copy made just before.\n\nChanges not yet saved in the editor will be lost.\n\nContinue?",
                                        "ATTENTION : l'emplacement " + dst + " contient déjà un personnage (" + nameD + ").\nIl sera REMPLACÉ par une copie de l'emplacement " + src + " (" + nameS + ") et perdu, sauf dans la copie de sécurité faite juste avant.\n\nLes modifications non enregistrées dans l'éditeur seront perdues.\n\nContinuer ?");
                    icon = MessageBoxIcon.Exclamation;
                }
                else
                {
                    msg = RomfsConfig.L("Copy slot " + src + " (" + nameS + ") into the empty slot " + dst + "?\n\nA safety copy is made first. Changes not yet saved in the editor will be lost.",
                                        "Copier l'emplacement " + src + " (" + nameS + ") dans l'emplacement vide " + dst + " ?\n\nUne copie de sécurité est faite avant. Les modifications non enregistrées dans l'éditeur seront perdues.");
                    icon = MessageBoxIcon.Question;
                }
                if (MessageBox.Show(this, msg, title, MessageBoxButtons.YesNo, icon) != DialogResult.Yes) return;

                Array.Copy(raw, absS, raw, absD, MHGU_SLOT_SIZE);
                raw[fd] = 1;

                bool was = ThemeSettings.BackupOn;
                ThemeSettings.BackupOn = true;
                bool okBackup;
                try { okBackup = BackupManager.Backup(this, filePath); }
                finally { ThemeSettings.BackupOn = was; }
                if (!okBackup) return;
                SafeFile.WriteAllBytes(filePath, raw);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            LoadSaveFile(filePath, Path.GetFileName(filePath));
        }

        // Fichier > « Fichiers récents » : les derniers fichiers de sauvegarde ouverts, un clic suffit.
        void BuildRecentMenu()
        {
            ToolStripMenuItem parent = deleteSaveSlotToolStripMenuItem.OwnerItem as ToolStripMenuItem;
            if (parent == null) return;
            menuRecent = new ToolStripMenuItem(RomfsConfig.L("Recent files", "Fichiers récents"));
            menuRecent.DropDownItems.Add(new ToolStripMenuItem("..."));
            menuRecent.DropDownOpening += delegate { FillRecentMenu(); };
            menuFavorites = new ToolStripMenuItem(RomfsConfig.L("Favorite files", "Fichiers favoris"));
            menuFavorites.DropDownItems.Add(new ToolStripMenuItem("..."));
            menuFavorites.DropDownOpening += delegate { FillFavoritesMenu(); };
            int at = parent.DropDownItems.IndexOf(loadToolStripMenuItem);
            at = at < 0 ? 0 : at + 1;
            parent.DropDownItems.Insert(at, menuRecent);
            parent.DropDownItems.Insert(at + 1, menuFavorites);
        }

        void FillRecentMenu()
        {
            RecentFilesMenu.FillRecents(menuRecent.DropDownItems, OpenRecent);
        }

        void FillFavoritesMenu()
        {
            RecentFilesMenu.FillFavorites(menuFavorites.DropDownItems, OpenRecent, filePath);
        }

        void OpenRecent(string path)
        {
            if (!ConfirmUnsaved("loading another save", "charger une autre sauvegarde")) return;
            if (!File.Exists(path))
            {
                MessageBox.Show(this, RomfsConfig.L("This file no longer exists:\n", "Ce fichier n'existe plus :\n") + path,
                    RomfsConfig.L("Recent files", "Fichiers récents"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            LoadSaveFile(path, Path.GetFileName(path));
        }

        // Fichier > « Transférer entre deux sauvegardes (A / B)... » : fenêtre pour échanger, copier ou couper
        // un emplacement d'une sauvegarde vers n'importe quel emplacement d'une autre.
        void BuildTransferMenu()
        {
            ToolStripMenuItem parent = deleteSaveSlotToolStripMenuItem.OwnerItem as ToolStripMenuItem;
            if (parent == null) return;
            menuTransfer = new ToolStripMenuItem(RomfsConfig.L("Transfer between two saves (A / B)...", "Transférer entre deux sauvegardes (A / B)..."));
            menuTransfer.ToolTipText = RomfsConfig.L("Swap, copy or cut a slot from one save into any slot of another save. A safety copy is made first.",
                                                     "Échanger, copier ou couper un emplacement d'une sauvegarde vers n'importe quel emplacement d'une autre. Une copie de sécurité est faite avant.");
            menuTransfer.Click += delegate { OpenTransferDialog(); };
            int at = menuCopySlots != null ? parent.DropDownItems.IndexOf(menuCopySlots) + 1 : parent.DropDownItems.Count;
            parent.DropDownItems.Insert(at, menuTransfer);
        }

        void OpenTransferDialog()
        {
            if (!ConfirmUnsaved("transferring slots", "transférer des emplacements")) return;
            string current = (!string.IsNullOrEmpty(filePath) && File.Exists(filePath)) ? filePath : null;
            using (TransferSlotsDialog dlg = new TransferSlotsDialog(current))
            {
                dlg.ShowDialog(this);
                bool reload = false;
                foreach (string f in dlg.ChangedFiles)
                {
                    if (string.Equals(f, filePath, StringComparison.OrdinalIgnoreCase)) reload = true;
                }
                if (reload) LoadSaveFile(filePath, Path.GetFileName(filePath));
            }
        }

        void BuildSwapSlotsMenu()
        {
            ToolStripMenuItem parent = deleteSaveSlotToolStripMenuItem.OwnerItem as ToolStripMenuItem;
            if (parent == null) return;
            menuSwapSlots = new ToolStripMenuItem(RomfsConfig.L("Swap two save slots", "Échanger deux emplacements de sauvegarde"));
            menuSwapSlots.Enabled = false;
            menuSwapSlots.ToolTipText = RomfsConfig.L("Swaps two characters (or moves one into an empty slot). A safety copy is made first.",
                                                      "Échange deux personnages (ou déplace l'un dans un emplacement vide). Une copie de sécurité est faite avant.");
            int[,] pairs = { { 1, 2 }, { 1, 3 }, { 2, 3 } };
            swapItems = new ToolStripMenuItem[3];
            for (int k = 0; k < 3; k++)
            {
                int a = pairs[k, 0], b = pairs[k, 1];
                swapItems[k] = new ToolStripMenuItem(a.ToString() + " <-> " + b.ToString());
                swapItems[k].Click += delegate { SwapSlots(a, b); };
                menuSwapSlots.DropDownItems.Add(swapItems[k]);
            }
            parent.DropDownItems.Insert(parent.DropDownItems.IndexOf(deleteSaveSlotToolStripMenuItem) + 1, menuSwapSlots);
        }

        string NameFromRaw(byte[] raw, int absSlot, bool used)
        {
            if (!used) return RomfsConfig.L("(empty)", "(vide)");
            int p = absSlot + Offsets.NAME_OFFSET;
            if (p < 0 || p + 32 > raw.Length) return "?";
            return Encoding.UTF8.GetString(raw, p, 32).Split('\0')[0].Trim();
        }

        // Échange le contenu de deux emplacements de personnage (sauvegardes MHGU Switch). Si l'un est vide, le personnage est déplacé.
        // On travaille sur le fichier tel qu'il est sur le disque, en gardant son en-tête Switch ; copie de sécurité obligatoire avant.
        void SwapSlots(int a, int b)
        {
            string title = RomfsConfig.L("Swap two slots", "Échanger deux emplacements");
            if (!ConfirmUnsaved("swapping slots", "échanger des emplacements")) return;
            try
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;
                byte[] raw = File.ReadAllBytes(filePath);
                if (raw.Length != MHGU_SAVE_SIZE)
                {
                    MessageBox.Show(this,
                        RomfsConfig.L("Swapping slots is only available for MHGU Switch saves. Nothing was changed.",
                                      "L'échange d'emplacements n'existe que pour les sauvegardes MHGU Switch. Rien n'a été modifié."),
                        title, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                int fa = 36 + 4 + (a - 1), fb = 36 + 4 + (b - 1);
                if (raw[fa] != 1 && raw[fb] != 1)
                {
                    MessageBox.Show(this, RomfsConfig.L("Both slots are empty.", "Les deux emplacements sont vides."),
                        title, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                int absA = 36 + BitConverter.ToInt32(raw, 36 + 0x10 + 4 * (a - 1));
                int absB = 36 + BitConverter.ToInt32(raw, 36 + 0x10 + 4 * (b - 1));
                bool ok = absA >= 36 + 0x30 && absB >= 36 + 0x30
                          && absA + MHGU_SLOT_SIZE <= raw.Length && absB + MHGU_SLOT_SIZE <= raw.Length
                          && (absA + MHGU_SLOT_SIZE <= absB || absB + MHGU_SLOT_SIZE <= absA);
                if (!ok)
                {
                    MessageBox.Show(this,
                        RomfsConfig.L("Unexpected save structure. Nothing was changed.", "Structure de sauvegarde inattendue. Rien n'a été modifié."),
                        title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                string nameA = NameFromRaw(raw, absA, raw[fa] == 1);
                string nameB = NameFromRaw(raw, absB, raw[fb] == 1);
                if (MessageBox.Show(this,
                        RomfsConfig.L("Swap slot " + a + " (" + nameA + ") with slot " + b + " (" + nameB + ")?\n\nA safety copy is made first. Changes not yet saved in the editor will be lost.",
                                      "Échanger l'emplacement " + a + " (" + nameA + ") avec l'emplacement " + b + " (" + nameB + ") ?\n\nUne copie de sécurité est faite avant. Les modifications non enregistrées dans l'éditeur seront perdues."),
                        title, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

                byte[] tmp = new byte[MHGU_SLOT_SIZE];
                Array.Copy(raw, absA, tmp, 0, MHGU_SLOT_SIZE);
                Array.Copy(raw, absB, raw, absA, MHGU_SLOT_SIZE);
                Array.Copy(tmp, 0, raw, absB, MHGU_SLOT_SIZE);
                byte flag = raw[fa];
                raw[fa] = raw[fb];
                raw[fb] = flag;

                bool was = ThemeSettings.BackupOn;
                ThemeSettings.BackupOn = true;
                bool okBackup;
                try { okBackup = BackupManager.Backup(this, filePath); }
                finally { ThemeSettings.BackupOn = was; }
                if (!okBackup) return;
                SafeFile.WriteAllBytes(filePath, raw);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            LoadSaveFile(filePath, Path.GetFileName(filePath));
        }
    }
}
