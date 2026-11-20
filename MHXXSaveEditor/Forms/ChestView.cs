// MHXXSwitchSaveEditor – version française (modifiée). Licence : GNU GPL v3 (voir LICENSE).
// Fichier ajouté lors de la V2 (coffre graphique), le 14 novembre 2026.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using MHXXSaveEditor.Data;
using MHXXSaveEditor.Util;

namespace MHXXSaveEditor.Forms
{
    // Ce que la grille doit savoir pour dessiner une case.
    class SlotInfo
    {
        public bool Empty = true;
        public Bitmap Icon;
        public string Title = "";
        public string Detail = "";
        public string Short = "";
    }

    // Grille 10 x 10 dessinée à la main, comme le coffre du jeu.
    class ChestGrid : ChestGridMarker
    {
        public Func<int, SlotInfo> GetSlot;       // index dans la page (0 à 99)
        public Action<int> SlotEdit;              // double-clic ou Entrée
        public Action<int> SlotClear;             // clic droit ou Suppr
        public Action<int> PageStep;              // -1 / +1
        public Action<int> ZoomStep;              // Ctrl + molette
        public Action<int> SelectionChanged;      // la sélection a changé
        public Action<int> HoverChanged;          // survol (-1 = aucune case)

        int selected = 0;
        int hover = -1;
        int cell = 72;

        static readonly Color PanelColor = Color.FromArgb(0x5A, 0x2A, 0x31);
        static readonly Color SlotColor = Color.FromArgb(0x4A, 0x1F, 0x27);
        static readonly Color Gold = Color.FromArgb(0xF5, 0xB3, 0x00);

        public ChestGrid()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = PanelColor;
            TabStop = true;
            SetStyle(ControlStyles.Selectable, true);
        }

        public int Selected
        {
            get { return selected; }
            set
            {
                if (value < 0) value = 0;
                if (value > 99) value = 99;
                selected = value;
                Invalidate();
                if (SelectionChanged != null) SelectionChanged(selected);
            }
        }

        // La grille remplit la zone : chaque case fait au plus 1/10 du plus petit côté.
        public void RecomputeLayout()
        {
            int side = Math.Min(ClientSize.Width, ClientSize.Height);
            cell = Math.Max(12, side / 10);
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            RecomputeLayout();
        }

        Point Origin()
        {
            // Si la grille est plus petite que la zone, on la centre.
            int ox = Math.Max(0, (ClientSize.Width - cell * 10) / 2);
            int oy = Math.Max(0, (ClientSize.Height - cell * 10) / 2);
            return new Point(ox, oy);
        }

        int IndexAt(Point p)
        {
            Point o = Origin();
            int x = p.X - o.X;
            int y = p.Y - o.Y;
            if (x < 0 || y < 0) return -1;
            int c = x / cell;
            int r = y / cell;
            if (c > 9 || r > 9) return -1;
            return r * 10 + c;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            Point o = Origin();
            int pad = Math.Max(2, cell / 16);
            using (SolidBrush slotBrush = new SolidBrush(SlotColor))
            using (Pen goldPen = new Pen(Gold, Math.Max(2f, cell / 22f)))
            using (SolidBrush goldBrush = new SolidBrush(Color.FromArgb(70, Gold)))
            using (Font small = new Font(Font.FontFamily, Math.Max(7f, cell / 7f), FontStyle.Bold))
            {
                for (int i = 0; i < 100; i++)
                {
                    int r = i / 10;
                    int c = i % 10;
                    Rectangle rect = new Rectangle(o.X + c * cell, o.Y + r * cell, cell, cell);
                    if (!rect.IntersectsWith(ClientRectangle)) continue;
                    Rectangle inner = new Rectangle(rect.X + pad, rect.Y + pad, cell - pad * 2, cell - pad * 2);
                    g.FillRectangle(slotBrush, inner);

                    SlotInfo info = GetSlot != null ? GetSlot(i) : null;
                    if (info != null && !info.Empty)
                    {
                        int ip = Math.Max(1, cell / 12);
                        Rectangle iconRect = new Rectangle(inner.X + ip, inner.Y + ip, inner.Width - ip * 2, inner.Height - ip * 2);
                        if (info.Icon != null)
                        {
                            g.DrawImage(info.Icon, iconRect);
                        }
                        else
                        {
                            using (StringFormat sf = new StringFormat())
                            {
                                sf.Alignment = StringAlignment.Center;
                                sf.LineAlignment = StringAlignment.Center;
                                g.DrawString(info.Short, small, Brushes.Gainsboro, iconRect, sf);
                            }
                        }
                    }
                    if (i == selected)
                    {
                        g.FillRectangle(goldBrush, inner);
                        g.DrawRectangle(goldPen, inner);
                    }
                    else if (i == hover)
                    {
                        using (Pen hp = new Pen(Color.FromArgb(150, Gold), 1f))
                            g.DrawRectangle(hp, inner);
                    }
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int idx = IndexAt(e.Location);
            if (idx != hover)
            {
                hover = idx;
                Invalidate();
                if (HoverChanged != null) HoverChanged(hover);
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hover = -1;
            Invalidate();
            if (HoverChanged != null) HoverChanged(-1);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            int idx = IndexAt(e.Location);
            if (idx < 0) return;
            Selected = idx;
            if (e.Button == MouseButtons.Right && SlotClear != null) SlotClear(idx);
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button != MouseButtons.Left) return;
            int idx = IndexAt(e.Location);
            if (idx >= 0 && SlotEdit != null) SlotEdit(idx);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if ((Control.ModifierKeys & Keys.Control) == Keys.Control)
            {
                if (ZoomStep != null) ZoomStep(e.Delta > 0 ? 1 : -1);
                return;
            }
            base.OnMouseWheel(e);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Left:
                case Keys.Right:
                case Keys.Up:
                case Keys.Down:
                case Keys.Enter:
                case Keys.Delete:
                case Keys.PageUp:
                case Keys.PageDown:
                    return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            int r = selected / 10;
            int c = selected % 10;
            switch (e.KeyCode)
            {
                case Keys.Left: if (c > 0) Selected = selected - 1; break;
                case Keys.Right: if (c < 9) Selected = selected + 1; break;
                case Keys.Up: if (r > 0) Selected = selected - 10; break;
                case Keys.Down: if (r < 9) Selected = selected + 10; break;
                case Keys.Enter: if (SlotEdit != null) SlotEdit(selected); break;
                case Keys.Delete: if (SlotClear != null) SlotClear(selected); break;
                case Keys.PageUp: if (PageStep != null) PageStep(-1); break;
                case Keys.PageDown: if (PageStep != null) PageStep(1); break;
            }
            e.Handled = true;
        }
    }

    // Le coffre graphique tel qu'il apparaît dans un onglet : pages en haut, grille, barre d'information en bas.
    class ChestView : ChestGridMarker
    {
        const int ItemPages = 23;
        const int EquipPages = 20;
        const int PalicoPages = 10;

        static readonly Color Maroon = Color.FromArgb(0x5A, 0x2A, 0x31);
        static readonly Color MaroonLight = Color.FromArgb(0x7A, 0x3A, 0x42);

        readonly MainForm owner;
        readonly bool equipMode;
        readonly bool palicoMode;
        ChestGrid grid;
        FlowLayoutPanel nav;
        Button btnPrev;
        Button btnNext;
        Label lblPage;
        Label lblInfo;
        Label lblSlot;
        Label lblStyle;
        ComboBox cmbStyle;
        int page = 0;

        public ChestView(MainForm owner, bool equipMode, bool palicoMode = false)
        {
            this.owner = owner;
            this.equipMode = equipMode;
            this.palicoMode = palicoMode;
            BackColor = Maroon;

            grid = new ChestGrid();
            grid.Dock = DockStyle.Fill;
            grid.GetSlot = GetSlot;
            grid.SlotEdit = EditSlot;
            grid.SlotClear = ClearSlot;
            grid.PageStep = StepPage;
            grid.ZoomStep = delegate (int dir) { owner.StepZoom(dir); };
            grid.SelectionChanged = OnSelected;
            grid.HoverChanged = OnHover;

            lblInfo = new Label();
            lblInfo.Dock = DockStyle.Bottom;
            lblInfo.AutoSize = false;
            lblInfo.AutoEllipsis = true;
            lblInfo.UseMnemonic = false;
            lblInfo.ForeColor = Color.White;
            lblInfo.BackColor = Maroon;
            lblInfo.TextAlign = ContentAlignment.MiddleLeft;
            lblInfo.Padding = new Padding(10, 8, 10, 8);

            lblSlot = new Label();
            lblSlot.AutoSize = false;
            lblSlot.ForeColor = Color.White;
            lblSlot.BackColor = Maroon;
            lblSlot.TextAlign = ContentAlignment.MiddleRight;

            nav = new FlowLayoutPanel();
            nav.Dock = DockStyle.Top;
            nav.AutoSize = true;
            nav.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            nav.WrapContents = false;
            nav.FlowDirection = FlowDirection.LeftToRight;
            nav.Padding = new Padding(4, 4, 4, 4);
            nav.BackColor = Maroon;

            btnPrev = MakeButton("◀");
            btnPrev.Click += delegate { StepPage(-1); };
            lblPage = new Label();
            lblPage.AutoSize = false;
            lblPage.Size = new Size(80, 30);
            lblPage.TextAlign = ContentAlignment.MiddleCenter;
            lblPage.ForeColor = Color.White;
            lblPage.BackColor = Maroon;
            btnNext = MakeButton("▶");
            btnNext.Click += delegate { StepPage(1); };
            nav.Controls.Add(btnPrev);
            nav.Controls.Add(lblPage);
            nav.Controls.Add(btnNext);

            if (equipMode && !palicoMode)
            {
                lblStyle = new Label();
                lblStyle.Text = RomfsConfig.L("Grey out gear for:", "Griser l'équipement pour :");
                lblStyle.AutoSize = true;
                lblStyle.Margin = new Padding(24, 8, 3, 3);
                lblStyle.ForeColor = Color.White;
                lblStyle.BackColor = Maroon;
                cmbStyle = new ComboBox();
                cmbStyle.DropDownStyle = ComboBoxStyle.DropDownList;
                cmbStyle.Items.Add(RomfsConfig.L("Default (none)", "Défaut (aucun)"));
                cmbStyle.Items.Add(RomfsConfig.L("Blademaster", "Épéiste"));
                cmbStyle.Items.Add(RomfsConfig.L("Gunner", "Tireur"));
                cmbStyle.SelectedIndex = 0;
                cmbStyle.Width = 130;
                cmbStyle.Margin = new Padding(3, 5, 3, 3);
                cmbStyle.SelectedIndexChanged += delegate { RefreshView(); };
                nav.Controls.Add(lblStyle);
                nav.Controls.Add(cmbStyle);
            }

            // L'ordre d'ajout compte pour l'ancrage : la zone centrale d'abord.
            Controls.Add(grid);
            Controls.Add(lblInfo);
            Controls.Add(lblSlot);
            Controls.Add(nav);

            lblInfo.Font = new Font(Font.FontFamily, Font.Size * 1.4f, FontStyle.Bold);
            lblSlot.Font = lblInfo.Font;
            lblInfo.SizeChanged += delegate { PlaceSlotLabel(); };
            SizeChanged += delegate { PlaceSlotLabel(); };
            UpdatePageLabel();
            UpdateInfo(0);
        }

        Button MakeButton(string text)
        {
            Button b = new Button();
            b.Text = text;
            b.AutoSize = true;
            b.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            b.MinimumSize = new Size(40, 28);
            b.Margin = new Padding(3, 2, 3, 2);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = Color.FromArgb(0xA0, 0x60, 0x68);
            b.BackColor = MaroonLight;
            b.ForeColor = Color.White;
            b.UseVisualStyleBackColor = false;
            b.TabStop = false;
            return b;
        }

        // La description de la case (en bas) est plus grande que le texte courant et suit le zoom.
        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            if (lblInfo != null)
            {
                lblInfo.Font = new Font(Font.FontFamily, Font.Size * 1.4f, FontStyle.Bold);
                if (lblSlot != null) lblSlot.Font = lblInfo.Font;
                PlaceSlotLabel();
            }
        }

        // Le numéro d'emplacement reste toujours au même endroit, à droite, de largeur fixe : plus de tremblement.
        void PlaceSlotLabel()
        {
            if (lblInfo == null || lblSlot == null) return;
            int h = TextRenderer.MeasureText("Ag", lblInfo.Font).Height + 20;
            if (lblInfo.Height != h) lblInfo.Height = h;
            string sample = RomfsConfig.L("Slot ", "Emplacement ") + "2300";
            int w = TextRenderer.MeasureText(sample, lblSlot.Font).Width + 24;
            // Le nom à gauche ne passe jamais sous le numéro (il est coupé avec « … » si besoin).
            lblInfo.Padding = new Padding(10, 8, w + 14, 8);
            lblSlot.Size = new Size(w, h);
            lblSlot.Location = new Point(Math.Max(0, ClientSize.Width - w - 6), Math.Max(0, ClientSize.Height - h));
            lblSlot.BringToFront();
        }

        int PageCount { get { return palicoMode ? PalicoPages : (equipMode ? EquipPages : ItemPages); } }

        // Pour que la fenêtre principale demande un nouveau dessin (après une modification ailleurs).
        public void RefreshView()
        {
            grid.Invalidate();
            UpdateInfo(grid.Selected);
        }

        // Emplacement actuellement sélectionné dans la sauvegarde (0 à 2299 / 1999).
        public int SelectedSlot { get { return page * 100 + grid.Selected; } }

        void StepPage(int dir)
        {
            page = (page + dir + PageCount) % PageCount;
            UpdatePageLabel();
            grid.Invalidate();
            OnSelected(grid.Selected);
        }

        void UpdatePageLabel()
        {
            lblPage.Text = (page + 1).ToString() + " / " + PageCount.ToString();
        }

        static int ToInt(string s)
        {
            int v;
            if (int.TryParse(s, out v)) return v;
            return 0;
        }

        // ---- Contenu des cases ----

        SlotInfo GetSlot(int indexInPage)
        {
            SlotInfo s = new SlotInfo();
            int slot = page * 100 + indexInPage;
            Player p = owner.player;
            if (p == null) return s;

            if (!equipMode && !palicoMode)
            {
                if (p.ItemId == null || slot >= p.ItemId.Length) return s;
                int id = ToInt(p.ItemId[slot]);
                if (id == 0) return s;
                int count = ToInt(p.ItemCount[slot]);
                s.Empty = false;
                s.Icon = GameAssets.ItemIcon(id);
                s.Title = NameCatalog.ItemName(id);
                s.Detail = "× " + count.ToString();
                s.Short = id.ToString();
                return s;
            }

            if (palicoMode)
            {
                byte[] pe = p.EquipmentPalico;
                int po = slot * 36;
                if (pe == null || po + 36 > pe.Length) return s;
                int ptype = pe[po];
                int peid = pe[po + 2] | (pe[po + 3] << 8);
                int ptid = pe[po + 4] | (pe[po + 5] << 8);
                if (ptype < 22 || ptype > 24) return s;
                s.Empty = false;
                s.Icon = GameAssets.PalicoIcon(ptype, peid, ptid != 0);
                s.Title = PalicoName(ptype, peid);
                s.Detail = ptid != 0 ? RomfsConfig.L("Transmog", "Transmog") : "";
                s.Short = peid.ToString();
                return s;
            }

            byte[] eq = p.EquipmentInfo;
            int o = slot * 36;
            if (eq == null || o + 36 > eq.Length) return s;
            int type = eq[o] & 0x1F;
            int level = ((eq[o + 1] << 8) | eq[o]) >> 5;
            int eid = eq[o + 2] | (eq[o + 3] << 8);
            int tid = eq[o + 4] | (eq[o + 5] << 8);
            if (type == 0) return s; // « (None) » : case vide, même s'il reste un numéro dans les données
            s.Empty = false;

            bool isArmor = type >= 1 && type <= 5;
            bool usable = true;
            if (isArmor)
            {
                int gender = 0;
                try { gender = Convert.ToInt32(p.Gender); } catch (Exception) { gender = 0; }
                int styleIndex = cmbStyle != null ? cmbStyle.SelectedIndex : 0;
                if (styleIndex > 0) usable = GameAssets.ArmorUsable(eid, gender == 1 ? 1 : 0, styleIndex - 1);
            }
            s.Icon = GameAssets.EquipIcon(type, eid, tid != 0, !usable);
            s.Title = NameCatalog.EquipName(type, eid);
            if (type == 6) s.Title = RomfsConfig.L("Talisman", "Talisman");
            int rarity = GameAssets.EquipRarity(type, eid);
            string rar = rarity >= 10 ? "X" : (rarity + 1).ToString();
            s.Detail = RomfsConfig.L("Lv ", "Niv. ") + level.ToString() + "   RARE " + rar;
            if (tid != 0)
                s.Detail += "   " + RomfsConfig.L("Transmog: ", "Transmog : ") + NameCatalog.EquipName(type, tid);
            if (!usable)
                s.Detail += "   " + RomfsConfig.L("(Your hunter type cannot wear this.)", "(Votre type de chasseur ne peut pas porter ceci.)");
            s.Short = type == 6 ? "T" : eid.ToString();
            return s;
        }

        static string PalicoName(int type, int id)
        {
            try
            {
                if (type == 22)
                    return (id >= 0 && id < GameConstants.PalicoWeaponNames.Length) ? GameConstants.PalicoWeaponNames[id] : "?";
                int[] ids = type == 23 ? GameConstants.PalicoHeadIDs : GameConstants.PalicoArmorIDs;
                string[] names = type == 23 ? GameConstants.PalicoHeadNames : GameConstants.PalicoArmorNames;
                int idx = Array.IndexOf(ids, id);
                return (idx >= 0 && idx < names.Length) ? names[idx] : "?";
            }
            catch (Exception) { return "?"; }
        }

        void UpdateInfo(int indexInPage)
        {
            if (indexInPage < 0) indexInPage = grid.Selected;
            SlotInfo s = GetSlot(indexInPage);
            lblSlot.Text = RomfsConfig.L("Slot ", "Emplacement ") + (page * 100 + indexInPage + 1).ToString();
            if (s.Empty)
                lblInfo.Text = RomfsConfig.L("(empty)", "(vide)");
            else
                lblInfo.Text = s.Title + "     " + s.Detail;
            PlaceSlotLabel();
        }

        void OnHover(int idx)
        {
            UpdateInfo(idx >= 0 ? idx : grid.Selected);
        }

        void OnSelected(int idx)
        {
            UpdateInfo(idx);
            if (palicoMode) owner.SelectPalicoSlot(page * 100 + idx);
            else if (equipMode) owner.SelectEquipSlot(page * 100 + idx);
        }

        // ---- Actions ----

        void EditSlot(int indexInPage)
        {
            if (palicoMode)
            {
                owner.FocusPalicoEditor(page * 100 + indexInPage);
                return;
            }
            if (equipMode)
            {
                owner.FocusEquipEditor(page * 100 + indexInPage);
                return;
            }
            int slot = page * 100 + indexInPage;
            Player p = owner.player;
            if (p == null || p.ItemId == null || slot >= p.ItemId.Length) return;
            int curId = ToInt(p.ItemId[slot]);
            int curCount = ToInt(p.ItemCount[slot]);
            using (ItemPickerDialog dlg = new ItemPickerDialog(curId, curCount))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    owner.SetItemSlot(slot, dlg.SelectedItem, dlg.Amount);
                    RefreshView();
                }
            }
        }

        void ClearSlot(int indexInPage)
        {
            if (equipMode || palicoMode) return;
            int slot = page * 100 + indexInPage;
            Player p = owner.player;
            if (p == null || p.ItemId == null || slot >= p.ItemId.Length) return;
            if (ToInt(p.ItemId[slot]) == 0) return;
            owner.SetItemSlot(slot, 0, 0);
            RefreshView();
        }
    }
}
