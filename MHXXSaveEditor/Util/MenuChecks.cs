// MHXXSwitchSaveEditor – version française (modifiée). Licence : GNU GPL v3 (voir LICENSE).
// Fichier ajouté lors de la version 24 : cases à cocher visibles dans les menus.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace MHXXSaveEditor.Util
{
    // Dans un menu, une option décochée n'affichait rien : impossible de deviner qu'on pouvait la cocher.
    // Ici, une option décochée montre une petite case vide ; cochée, elle montre la coche habituelle.
    static class MenuChecks
    {
        static readonly List<ToolStripMenuItem> items = new List<ToolStripMenuItem>();
        static Bitmap boxDark;
        static Bitmap boxLight;

        static Bitmap MakeBox(Color c)
        {
            Bitmap bmp = new Bitmap(16, 16);
            using (Graphics g = Graphics.FromImage(bmp))
            using (Pen p = new Pen(c, 1.6f))
            {
                g.Clear(Color.Transparent);
                g.DrawRectangle(p, 2, 2, 11, 11);
            }
            return bmp;
        }

        // À appeler une fois pour chaque élément de menu qui peut être coché.
        public static void Install(ToolStripMenuItem mi)
        {
            if (mi == null || items.Contains(mi)) return;
            items.Add(mi);
            mi.CheckedChanged += delegate { UpdateOne(mi); };
            UpdateOne(mi);
        }

        static void UpdateOne(ToolStripMenuItem mi)
        {
            if (mi.Checked)
            {
                mi.Image = null; // la coche habituelle est dessinée par le menu
                return;
            }
            if (ThemeSettings.Dark)
            {
                if (boxDark == null) boxDark = MakeBox(Color.FromArgb(170, 172, 185));
                mi.Image = boxDark;
            }
            else
            {
                if (boxLight == null) boxLight = MakeBox(Color.FromArgb(90, 90, 90));
                mi.Image = boxLight;
            }
        }

        // Après un changement de thème (clair / sombre) : on remet la bonne couleur de case.
        public static void Refresh()
        {
            foreach (ToolStripMenuItem mi in items) UpdateOne(mi);
        }
    }

    // Dessine un texte lettre par lettre, chaque lettre dans une couleur différente.
    static class RainbowText
    {
        static readonly Size Big = new Size(int.MaxValue, int.MaxValue);
        const TextFormatFlags Flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;

        static int CharWidth(char c, Font font)
        {
            int none = TextRenderer.MeasureText("||", font, Big, Flags).Width;
            return TextRenderer.MeasureText("|" + c.ToString() + "|", font, Big, Flags).Width - none;
        }

        public static int Measure(string text, Font font)
        {
            int w = 0;
            foreach (char c in text) w += CharWidth(c, font);
            return w;
        }

        // x, y = coin haut-gauche du texte. rainbow = false : une seule couleur (plain).
        public static void Draw(Graphics g, string text, Font font, int x, int y, bool rainbow, Color plain)
        {
            int k = 0;
            foreach (char c in text)
            {
                int w = CharWidth(c, font);
                if (c != ' ')
                {
                    Color col = rainbow ? ThemeSettings.RainbowColor(k) : plain;
                    TextRenderer.DrawText(g, c.ToString(), font, new Point(x, y), col, Flags);
                    k++;
                }
                x += w;
            }
        }
    }

    // Étiquette de menu dont chaque caractère a sa couleur (« V 02.31 »). Même place et même taille qu'une étiquette normale.
    class RainbowLabel : ToolStripLabel
    {
        public RainbowLabel(string text) : base(text)
        {
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (!ThemeSettings.ColorMenus)
            {
                base.OnPaint(e);
                return;
            }
            int w = RainbowText.Measure(Text, Font);
            int x = (Width - w) / 2;
            int y = (Height - Font.Height) / 2;
            RainbowText.Draw(e.Graphics, Text, Font, Math.Max(0, x), Math.Max(0, y), true, ForeColor);
        }
    }
}
