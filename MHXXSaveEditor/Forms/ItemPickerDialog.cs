// MHXXSwitchSaveEditor – version française (modifiée). Licence : GNU GPL v3 (voir LICENSE).
// Fichier ajouté lors de la V2 (coffre graphique), le 14 novembre 2026.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Windows.Forms;
using MHXXSaveEditor.Data;
using MHXXSaveEditor.Util;

namespace MHXXSaveEditor.Forms
{
    // Choix d'un objet : la liste se filtre à chaque lettre tapée (n'importe où dans le nom, sans accents
    // ni majuscules, plusieurs mots, ou numéro d'ID). Les cases Français / Anglais changent la liste sans la fermer.
    class ItemPickerDialog : Form
    {
        public int SelectedItem { get; private set; }
        public int Amount { get; private set; }

        static readonly List<int> recents = new List<int>();

        // Catégories de la recherche, d'après la forme d'icône du jeu. La dernière (« Autres ») prend tout le reste.
        sealed class Cat
        {
            public readonly string En, Fr;
            public readonly int[] Shapes;
            public Cat(string en, string fr, int[] shapes) { En = en; Fr = fr; Shapes = shapes; }
        }

        static readonly Cat[] Cats = new Cat[]
        {
            new Cat("Potions and healing", "Potions et soins", new int[] { 22, 29, 51 }),
            new Cat("Food", "Nourriture", new int[] { 4, 20, 82 }),
            new Cat("Plants and mushrooms", "Plantes et champignons", new int[] { 1, 3, 8, 93, 94 }),
            new Cat("Fish and bugs", "Poissons et insectes", new int[] { 2, 6, 7, 18, 19 }),
            new Cat("Ores and stones", "Minerais et pierres", new int[] { 9, 41 }),
            new Cat("Ammo and coatings", "Munitions et enduits", new int[] { 30, 31, 32, 33 }),
            new Cat("Traps, bombs and tools", "Pièges, bombes et outils", new int[] { 21, 23, 24, 25, 26, 27, 28, 34, 35, 37, 38, 39, 42, 43, 105 }),
            new Cat("Monster materials", "Matériaux de monstres", new int[] { 5, 10, 11, 12, 13, 14, 15, 16, 17, 50, 83, 92 }),
            new Cat("Scraps", "Débris", new int[] { 40, 45, 49 }),
            new Cat("Decorations", "Joyaux", new int[] { 44 }),
            new Cat("Tickets and rewards", "Tickets et récompenses", new int[] { 36, 52, 71, 87 }),
            new Cat("Others", "Autres", new int[0])
        };

        readonly string[] fr;
        readonly string[] en;
        readonly string[] frNorm;
        readonly string[] enNorm;
        readonly bool both;

        ComboBox cmbCat;
        ComboBox cmbRar;
        TextBox txtSearch;
        ListBox list;
        CheckBox chkFr;
        CheckBox chkEn;
        NumericUpDown numAmount;
        Label lblResults;
        Button btnOk;
        Button btnCancel;
        readonly List<int> shown = new List<int>();

        public ItemPickerDialog(int currentItem, int currentAmount)
        {
            both = NameCatalog.HasBoth;
            if (both)
            {
                fr = NameCatalog.ItemFR;
                en = NameCatalog.ItemEN;
            }
            else
            {
                fr = GameConstants.ItemNameList;
                en = GameConstants.ItemNameList;
            }
            frNorm = Normalize(fr);
            enNorm = both ? Normalize(en) : frNorm;

            SelectedItem = currentItem;
            Amount = currentAmount;
            BuildUi(currentAmount);
            RefreshList();
            ThemeSettings.Apply(this);
            // Les couleurs du thème ne doivent pas écraser le dessin de la liste.
            list.BackColor = ThemeSettings.Field;
            list.ForeColor = ThemeSettings.FieldFore;
        }

        static string[] Normalize(string[] names)
        {
            string[] r = new string[names.Length];
            for (int i = 0; i < names.Length; i++) r[i] = NormalizeText(names[i]);
            return r;
        }

        // Minuscules, sans accents.
        static string NormalizeText(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            string d = s.ToLowerInvariant().Normalize(NormalizationForm.FormD);
            StringBuilder sb = new StringBuilder(d.Length);
            foreach (char c in d)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
            }
            return sb.ToString();
        }

        void BuildUi(int currentAmount)
        {
            Text = RomfsConfig.L("Choose an item", "Choisir un objet");
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(560, 640);
            MinimumSize = new Size(440, 420);
            KeyPreview = true;

            Label lbl = new Label();
            lbl.Text = RomfsConfig.L("Search (name, several words, or ID):", "Recherche (nom, plusieurs mots, ou numéro d'ID) :");
            lbl.Location = new Point(12, 10);
            lbl.AutoSize = true;
            Controls.Add(lbl);

            txtSearch = new TextBox();
            txtSearch.Location = new Point(12, 30);
            txtSearch.Size = new Size(520, 24);
            txtSearch.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtSearch.TextChanged += delegate { RefreshList(); };
            txtSearch.KeyDown += SearchKeyDown;
            Controls.Add(txtSearch);

            chkFr = new CheckBox();
            chkFr.Text = "Français";
            chkFr.Checked = true;
            chkFr.AutoSize = true;
            chkFr.Location = new Point(12, 60);
            chkFr.CheckedChanged += delegate { RefreshList(); };
            Controls.Add(chkFr);

            chkEn = new CheckBox();
            chkEn.Text = "English";
            chkEn.Checked = !both ? true : false;
            chkEn.AutoSize = true;
            chkEn.Location = new Point(110, 60);
            chkEn.CheckedChanged += delegate { RefreshList(); };
            Controls.Add(chkEn);

            if (!both)
            {
                // Un seul jeu de noms disponible (langue courante) : on cache les cases.
                chkFr.Visible = false;
                chkEn.Visible = false;
                chkFr.Checked = true;
            }

            lblResults = new Label();
            lblResults.AutoSize = true;
            lblResults.Location = new Point(220, 62);
            Controls.Add(lblResults);

            // Filtres : catégorie et rareté (utilisent les tables du jeu, donc seulement si elles sont chargées).
            if (GameAssets.Loaded)
            {
                Label lblCat = new Label();
                lblCat.Text = RomfsConfig.L("Category:", "Catégorie :");
                lblCat.AutoSize = true;
                lblCat.Location = new Point(12, 92);
                Controls.Add(lblCat);

                cmbCat = new ComboBox();
                cmbCat.DropDownStyle = ComboBoxStyle.DropDownList;
                cmbCat.Location = new Point(90, 88);
                cmbCat.Size = new Size(200, 24);
                cmbCat.Items.Add(RomfsConfig.L("All", "Toutes"));
                foreach (Cat c in Cats) cmbCat.Items.Add(RomfsConfig.L(c.En, c.Fr));
                cmbCat.SelectedIndex = 0;
                cmbCat.SelectedIndexChanged += delegate { RefreshList(); };
                Controls.Add(cmbCat);

                Label lblRar = new Label();
                lblRar.Text = RomfsConfig.L("Rarity:", "Rareté :");
                lblRar.AutoSize = true;
                lblRar.Location = new Point(306, 92);
                Controls.Add(lblRar);

                cmbRar = new ComboBox();
                cmbRar.DropDownStyle = ComboBoxStyle.DropDownList;
                cmbRar.Location = new Point(372, 88);
                cmbRar.Size = new Size(100, 24);
                cmbRar.Items.Add(RomfsConfig.L("All", "Toutes"));
                int maxRar = 0;
                for (int i = 0; i < fr.Length; i++)
                {
                    int rv = GameAssets.ItemRarityOf(i);
                    if (rv > maxRar && rv < 30) maxRar = rv;
                }
                for (int v = 0; v <= maxRar; v++) cmbRar.Items.Add((v + 1).ToString());
                cmbRar.SelectedIndex = 0;
                cmbRar.SelectedIndexChanged += delegate { RefreshList(); };
                Controls.Add(cmbRar);
            }

            list = new ListBox();
            list.Location = new Point(12, 120);
            list.Size = new Size(520, 408);
            list.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            list.DrawMode = DrawMode.OwnerDrawFixed;
            list.ItemHeight = 36;
            list.IntegralHeight = false;
            list.DrawItem += ListDrawItem;
            list.DoubleClick += delegate { Accept(); };
            list.KeyDown += delegate (object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter) { Accept(); e.Handled = true; e.SuppressKeyPress = true; }
            };
            Controls.Add(list);

            Label lblAmount = new Label();
            lblAmount.Text = RomfsConfig.L("Quantity:", "Quantité :");
            lblAmount.AutoSize = true;
            lblAmount.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            lblAmount.Location = new Point(12, 548);
            Controls.Add(lblAmount);

            numAmount = new NumericUpDown();
            numAmount.Minimum = 0;
            numAmount.Maximum = 99;
            numAmount.Value = Math.Max(0, Math.Min(99, currentAmount));
            numAmount.Location = new Point(80, 544);
            numAmount.Size = new Size(70, 24);
            numAmount.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            Controls.Add(numAmount);

            btnOk = new Button();
            btnOk.Text = "OK";
            btnOk.Size = new Size(90, 28);
            btnOk.Location = new Point(342, 540);
            btnOk.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnOk.Click += delegate { Accept(); };
            Controls.Add(btnOk);

            btnCancel = new Button();
            btnCancel.Text = RomfsConfig.L("Cancel", "Annuler");
            btnCancel.Size = new Size(90, 28);
            btnCancel.Location = new Point(442, 540);
            btnCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnCancel.DialogResult = DialogResult.Cancel;
            Controls.Add(btnCancel);

            AcceptButton = null;
            CancelButton = btnCancel;
            Shown += delegate { txtSearch.Focus(); };
        }

        void SearchKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Down)
            {
                if (list.Items.Count > 0)
                {
                    int i = list.SelectedIndex < 0 ? 0 : Math.Min(list.Items.Count - 1, list.SelectedIndex + 1);
                    list.SelectedIndex = i;
                }
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Up)
            {
                if (list.Items.Count > 0 && list.SelectedIndex > 0) list.SelectedIndex = list.SelectedIndex - 1;
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                Accept();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        // Cherche dans les langues cochées, mot par mot.
        bool Matches(int id, string[] tokens, bool useFr, bool useEn)
        {
            foreach (string t in tokens)
            {
                bool ok = false;
                if (useFr && frNorm[id].IndexOf(t, StringComparison.Ordinal) >= 0) ok = true;
                else if (useEn && enNorm[id].IndexOf(t, StringComparison.Ordinal) >= 0) ok = true;
                else if (id.ToString() == t) ok = true;
                if (!ok) return false;
            }
            return true;
        }

        bool StartsWith(int id, string token, bool useFr, bool useEn)
        {
            if (useFr && frNorm[id].StartsWith(token, StringComparison.Ordinal)) return true;
            if (useEn && enNorm[id].StartsWith(token, StringComparison.Ordinal)) return true;
            return false;
        }

        void RefreshList()
        {
            if (list == null) return;
            bool useFr = chkFr.Checked;
            bool useEn = both ? chkEn.Checked : false;
            if (!useFr && !useEn) useFr = true;

            string query = NormalizeText(txtSearch.Text).Trim();
            int keep = SelectedItem;

            list.BeginUpdate();
            list.Items.Clear();
            shown.Clear();

            if (query.Length == 0)
            {
                foreach (int r in recents) shown.Add(r);
                for (int i = 0; i < fr.Length; i++)
                    if (!recents.Contains(i)) shown.Add(i);
            }
            else
            {
                string[] tokens = query.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                List<int> starts = new List<int>();
                List<int> others = new List<int>();
                for (int i = 0; i < fr.Length; i++)
                {
                    if (!Matches(i, tokens, useFr, useEn)) continue;
                    if (StartsWith(i, tokens[0], useFr, useEn) || i.ToString() == tokens[0]) starts.Add(i);
                    else others.Add(i);
                }
                shown.AddRange(starts);
                shown.AddRange(others);
            }

            if (cmbCat != null || cmbRar != null) shown.RemoveAll(delegate (int id) { return !PassFilters(id); });

            int cap = Math.Min(shown.Count, 1500);
            for (int i = 0; i < cap; i++) list.Items.Add(shown[i]);
            list.EndUpdate();

            if (list.Items.Count > 0)
            {
                int idx = shown.IndexOf(keep);
                list.SelectedIndex = (idx >= 0 && idx < cap && query.Length == 0) ? idx : 0;
            }
            lblResults.Text = shown.Count.ToString() + RomfsConfig.L(" results", " résultats");
        }

        // Objets « inutilisés » du jeu (cases vides de la liste) : rangés dans « Autres ».
        bool IsUnusedName(int id)
        {
            if (id < 0 || id >= fr.Length) return false;
            string n = fr[id];
            return n != null && (n.IndexOf("nutilis", StringComparison.OrdinalIgnoreCase) >= 0
                              || n.IndexOf("unused", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        // Vrai si l'objet passe les filtres de catégorie et de rareté.
        bool PassFilters(int id)
        {
            if (cmbCat != null && cmbCat.SelectedIndex > 0)
            {
                int shape = GameAssets.ItemShapeOf(id);
                int ci = cmbCat.SelectedIndex - 1;
                bool unused = IsUnusedName(id);
                if (ci < Cats.Length - 1)
                {
                    if (unused) return false; // les « inutilisés » vont tous dans « Autres »
                    if (Array.IndexOf(Cats[ci].Shapes, shape) < 0) return false;
                }
                else if (unused)
                {
                    return true;
                }
                else
                {
                    for (int k = 0; k < Cats.Length - 1; k++)
                        if (Array.IndexOf(Cats[k].Shapes, shape) >= 0) return false;
                }
            }
            if (cmbRar != null && cmbRar.SelectedIndex > 0)
            {
                if (GameAssets.ItemRarityOf(id) != cmbRar.SelectedIndex - 1) return false;
            }
            return true;
        }

        string LabelFor(int id)
        {
            bool useFr = chkFr.Checked;
            bool useEn = both ? chkEn.Checked : false;
            if (!useFr && !useEn) useFr = true;
            if (useFr && useEn)
            {
                if (fr[id] == en[id]) return fr[id];
                return fr[id] + "   /   " + en[id];
            }
            return useFr ? fr[id] : en[id];
        }

        void ListDrawItem(object sender, DrawItemEventArgs e)
        {
            e.DrawBackground();
            if (e.Index < 0 || e.Index >= list.Items.Count) return;
            int id = (int)list.Items[e.Index];
            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Bitmap icon = GameAssets.ItemIcon(id);
            Rectangle b = e.Bounds;
            if (icon != null)
            {
                Rectangle back = new Rectangle(b.X + 3, b.Y + 2, 32, 32);
                using (SolidBrush bb = new SolidBrush(Color.FromArgb(70, 32, 36)))
                    e.Graphics.FillRectangle(bb, back);
                e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                e.Graphics.DrawImage(icon, back);
            }
            Color textColor = selected ? SystemColors.HighlightText : list.ForeColor;
            Color idColor = selected ? SystemColors.HighlightText : Color.Gray;
            using (SolidBrush tb = new SolidBrush(textColor))
            using (SolidBrush ib = new SolidBrush(idColor))
            {
                e.Graphics.DrawString(LabelFor(id), list.Font, tb, b.X + 42, b.Y + 3);
                e.Graphics.DrawString("ID " + id.ToString(), list.Font, ib, b.X + 42, b.Y + 19);
            }
            e.DrawFocusRectangle();
        }

        void Accept()
        {
            if (list.SelectedIndex < 0 || list.SelectedIndex >= shown.Count)
            {
                DialogResult = DialogResult.Cancel;
                return;
            }
            SelectedItem = shown[list.SelectedIndex];
            Amount = (int)numAmount.Value;
            if (SelectedItem != 0)
            {
                recents.Remove(SelectedItem);
                recents.Insert(0, SelectedItem);
                if (recents.Count > 12) recents.RemoveAt(recents.Count - 1);
                if (Amount == 0) Amount = 1;
            }
            else
            {
                Amount = 0;
            }
            DialogResult = DialogResult.OK;
        }
    }
}
