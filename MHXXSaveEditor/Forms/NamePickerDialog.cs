// MHXXSwitchSaveEditor – version française (modifiée). Licence : GNU GPL v3 (voir LICENSE).
// Fichier ajouté lors de la V2 (recherche évoluée des noms), le 15 novembre 2026.
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
    // Branche la recherche évoluée sur une liste déroulante de noms : taper une lettre ou ouvrir la liste
    // affiche la fenêtre de recherche (filtre en direct, n'importe où dans le nom, Français / English, numéro).
    static class ComboSearch
    {
        static bool opening;

        public static void Attach(ComboBox cb, string title)
        {
            if (cb == null) return;
            cb.DropDown += delegate
            {
                if (opening) return;
                if (cb.IsHandleCreated)
                    cb.BeginInvoke(new Action(delegate { Open(cb, title, ""); }));
            };
            cb.KeyPress += delegate (object sender, KeyPressEventArgs e)
            {
                if (char.IsControl(e.KeyChar)) return;
                e.Handled = true;
                string first = e.KeyChar.ToString();
                if (cb.IsHandleCreated)
                    cb.BeginInvoke(new Action(delegate { Open(cb, title, first); }));
            };
        }

        static void Open(ComboBox cb, string title, string initial)
        {
            if (opening) return;
            if (!cb.Enabled || cb.Items.Count < 2) return;
            opening = true;
            try
            {
                cb.DroppedDown = false;
                string[] names = new string[cb.Items.Count];
                for (int i = 0; i < names.Length; i++) names[i] = Convert.ToString(cb.Items[i]);
                string[] en = NameCatalog.EnglishFor(names);
                using (NamePickerDialog dlg = new NamePickerDialog(title, names, en, cb.SelectedIndex, initial, cb.FindForm() != null ? cb.FindForm().Font : cb.Font))
                {
                    if (dlg.ShowDialog(cb.FindForm()) == DialogResult.OK)
                    {
                        if (dlg.SelectedIndex >= 0 && dlg.SelectedIndex < cb.Items.Count)
                        {
                            // Les listes n'enregistrent un choix que si elles ont le focus : ici on force.
                            MHXXSaveEditor.MainForm.BypassFocus = true;
                            try { cb.SelectedIndex = dlg.SelectedIndex; }
                            finally { MHXXSaveEditor.MainForm.BypassFocus = false; }
                        }
                    }
                }
                cb.Focus();
            }
            finally
            {
                opening = false;
            }
        }
    }

    // Choix d'un nom dans une liste : la liste se filtre à chaque lettre tapée.
    class NamePickerDialog : Form
    {
        public int SelectedIndex { get; private set; }

        readonly string[] fr;
        readonly string[] en;
        readonly string[] frNorm;
        readonly string[] enNorm;
        readonly bool both;
        readonly List<int> shown = new List<int>();

        readonly int[] rarities;     // rareté (0 = rareté 1...) de chaque nom, ou null : pas de filtre de rareté
        readonly bool typeButton;    // bouton « Changer le type... » (résultat : DialogResult.Retry)
        ComboBox cmbRarity;

        TextBox txtSearch;
        ListBox list;
        CheckBox chkFr;
        CheckBox chkEn;
        Label lblResults;
        Button btnOk;
        Button btnCancel;
        bool building = true;

        public NamePickerDialog(string title, string[] names, string[] englishNames, int current, string initialText, Font baseFont)
            : this(title, names, englishNames, current, initialText, baseFont, null, false)
        {
        }

        public NamePickerDialog(string title, string[] names, string[] englishNames, int current, string initialText, Font baseFont, int[] rarityPerName, bool changeTypeButton)
        {
            rarities = (rarityPerName != null && rarityPerName.Length == names.Length) ? rarityPerName : null;
            typeButton = changeTypeButton;
            fr = names;
            both = englishNames != null && englishNames.Length == names.Length;
            en = both ? englishNames : names;
            frNorm = Normalize(fr);
            enNorm = both ? Normalize(en) : frNorm;
            SelectedIndex = current;

            BuildUi(title, baseFont);
            building = false;
            if (!string.IsNullOrEmpty(initialText))
            {
                txtSearch.Text = initialText;
                txtSearch.SelectionStart = txtSearch.Text.Length;
            }
            RefreshList(current);
            ThemeSettings.Apply(this);
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

        void BuildUi(string title, Font baseFont)
        {
            // Même taille d'écriture que la fenêtre principale (zoom compris).
            if (baseFont != null) Font = baseFont;
            float r = Font.Size / 9f;
            if (r < 1f) r = 1f;
            Text = title;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size((int)(560 * r), (int)(640 * r));
            MinimumSize = new Size((int)(420 * r), (int)(360 * r));
            KeyPreview = true;
            ShowInTaskbar = false;

            list = new ListBox();
            list.Dock = DockStyle.Fill;
            list.IntegralHeight = false;
            list.DoubleClick += delegate { Accept(); };
            list.KeyDown += delegate (object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter) { Accept(); e.Handled = true; e.SuppressKeyPress = true; }
            };

            FlowLayoutPanel bottom = new FlowLayoutPanel();
            bottom.Dock = DockStyle.Bottom;
            bottom.AutoSize = true;
            bottom.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            bottom.FlowDirection = FlowDirection.RightToLeft;
            bottom.Padding = new Padding(8);

            btnCancel = new Button();
            btnCancel.Text = RomfsConfig.L("Cancel", "Annuler");
            btnCancel.AutoSize = true;
            btnCancel.MinimumSize = new Size((int)(90 * r), (int)(30 * r));
            btnCancel.DialogResult = DialogResult.Cancel;
            btnOk = new Button();
            btnOk.Text = "OK";
            btnOk.AutoSize = true;
            btnOk.MinimumSize = new Size((int)(90 * r), (int)(30 * r));
            btnOk.Click += delegate { Accept(); };
            bottom.Controls.Add(btnCancel);
            bottom.Controls.Add(btnOk);
            if (typeButton)
            {
                Button btnType = new Button();
                btnType.Text = RomfsConfig.L("Change type...", "Changer le type...");
                btnType.AutoSize = true;
                btnType.MinimumSize = new Size((int)(90 * r), (int)(30 * r));
                btnType.DialogResult = DialogResult.Retry;
                bottom.Controls.Add(btnType);
            }

            FlowLayoutPanel top = new FlowLayoutPanel();
            top.Dock = DockStyle.Top;
            top.AutoSize = true;
            top.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            top.FlowDirection = FlowDirection.TopDown;
            top.WrapContents = false;
            top.Padding = new Padding(8, 8, 8, 4);

            Label lbl = new Label();
            lbl.Text = RomfsConfig.L("Search (name, several words, or number):", "Recherche (nom, plusieurs mots, ou numéro) :");
            lbl.AutoSize = true;
            top.Controls.Add(lbl);

            txtSearch = new TextBox();
            txtSearch.Width = 500;
            txtSearch.TextChanged += delegate { RefreshList(-1); };
            txtSearch.KeyDown += SearchKeyDown;
            top.Controls.Add(txtSearch);

            FlowLayoutPanel opts = new FlowLayoutPanel();
            opts.AutoSize = true;
            opts.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            opts.FlowDirection = FlowDirection.LeftToRight;
            opts.WrapContents = false;
            opts.Margin = new Padding(0);

            chkFr = new CheckBox();
            chkFr.Text = "Français";
            chkFr.Checked = true;
            chkFr.AutoSize = true;
            chkFr.CheckedChanged += delegate { RefreshList(-1); };
            chkEn = new CheckBox();
            chkEn.Text = "English";
            chkEn.Checked = false;
            chkEn.AutoSize = true;
            chkEn.CheckedChanged += delegate { RefreshList(-1); };
            lblResults = new Label();
            lblResults.AutoSize = true;
            lblResults.Margin = new Padding(16, 6, 3, 3);
            if (both)
            {
                opts.Controls.Add(chkFr);
                opts.Controls.Add(chkEn);
            }
            if (rarities != null)
            {
                Label lblRar = new Label();
                lblRar.Text = RomfsConfig.L("Rarity:", "Rareté :");
                lblRar.AutoSize = true;
                lblRar.Margin = new Padding(16, 6, 3, 3);
                cmbRarity = new ComboBox();
                cmbRarity.DropDownStyle = ComboBoxStyle.DropDownList;
                cmbRarity.Width = (int)(90 * r);
                cmbRarity.Items.Add(RomfsConfig.L("All", "Toutes"));
                int maxRar = 0;
                foreach (int v in rarities) if (v > maxRar) maxRar = v;
                for (int v = 0; v <= maxRar; v++) cmbRarity.Items.Add(v >= 10 ? "X" : (v + 1).ToString());
                cmbRarity.SelectedIndex = 0;
                cmbRarity.SelectedIndexChanged += delegate { RefreshList(-1); };
                opts.Controls.Add(lblRar);
                opts.Controls.Add(cmbRarity);
            }
            opts.Controls.Add(lblResults);
            top.Controls.Add(opts);

            // L'ordre d'ajout compte pour l'ancrage : la zone centrale d'abord.
            Controls.Add(list);
            Controls.Add(bottom);
            Controls.Add(top);

            Resize += delegate
            {
                int w = ClientSize.Width - 32;
                if (w > 100) txtSearch.Width = w;
            };
            AcceptButton = null;
            CancelButton = btnCancel;
            Shown += delegate
            {
                int w = ClientSize.Width - 32;
                if (w > 100) txtSearch.Width = w;
                txtSearch.Focus();
                txtSearch.SelectionStart = txtSearch.Text.Length;
            };
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

        bool Matches(int i, string[] tokens, bool useFr, bool useEn)
        {
            foreach (string t in tokens)
            {
                bool ok = false;
                if (useFr && frNorm[i].IndexOf(t, StringComparison.Ordinal) >= 0) ok = true;
                else if (useEn && enNorm[i].IndexOf(t, StringComparison.Ordinal) >= 0) ok = true;
                else if (i.ToString() == t) ok = true;
                if (!ok) return false;
            }
            return true;
        }

        bool StartsWithToken(int i, string token, bool useFr, bool useEn)
        {
            if (useFr && frNorm[i].StartsWith(token, StringComparison.Ordinal)) return true;
            if (useEn && enNorm[i].StartsWith(token, StringComparison.Ordinal)) return true;
            return false;
        }

        string LabelFor(int i)
        {
            bool useFr = chkFr.Checked;
            bool useEn = both && chkEn.Checked;
            if (!useFr && !useEn) useFr = true;
            string text;
            if (useFr && useEn) text = (fr[i] == en[i]) ? fr[i] : fr[i] + "   /   " + en[i];
            else text = useFr ? fr[i] : en[i];
            return text + "      (n° " + i.ToString() + ")";
        }

        // keep = numéro à garder sélectionné quand la zone de recherche est vide (-1 = aucun).
        void RefreshList(int keep)
        {
            if (building || list == null) return;
            bool useFr = chkFr.Checked;
            bool useEn = both && chkEn.Checked;
            if (!useFr && !useEn) useFr = true;

            string query = NormalizeText(txtSearch.Text).Trim();
            list.BeginUpdate();
            list.Items.Clear();
            shown.Clear();
            if (query.Length == 0)
            {
                for (int i = 0; i < fr.Length; i++) shown.Add(i);
            }
            else
            {
                string[] tokens = query.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                List<int> starts = new List<int>();
                List<int> others = new List<int>();
                for (int i = 0; i < fr.Length; i++)
                {
                    if (!Matches(i, tokens, useFr, useEn)) continue;
                    if (StartsWithToken(i, tokens[0], useFr, useEn) || i.ToString() == tokens[0]) starts.Add(i);
                    else others.Add(i);
                }
                shown.AddRange(starts);
                shown.AddRange(others);
            }
            if (cmbRarity != null && cmbRarity.SelectedIndex > 0)
            {
                int wantRar = cmbRarity.SelectedIndex - 1;
                shown.RemoveAll(delegate (int i) { return rarities[i] != wantRar; });
            }
            int cap = Math.Min(shown.Count, 2000);
            for (int i = 0; i < cap; i++) list.Items.Add(LabelFor(shown[i]));
            list.EndUpdate();

            if (list.Items.Count > 0)
            {
                int idx = (query.Length == 0 && keep >= 0) ? shown.IndexOf(keep) : -1;
                list.SelectedIndex = (idx >= 0 && idx < cap) ? idx : 0;
            }
            lblResults.Text = shown.Count.ToString() + RomfsConfig.L(" results", " résultats");
        }

        void Accept()
        {
            if (list.SelectedIndex < 0 || list.SelectedIndex >= shown.Count)
            {
                DialogResult = DialogResult.Cancel;
                return;
            }
            SelectedIndex = shown[list.SelectedIndex];
            DialogResult = DialogResult.OK;
        }
    }
}
