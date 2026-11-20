// MHXXSwitchSaveEditor – version française (modifiée). Licence : GNU GPL v3 (voir LICENSE).
// Fichier créé en version 33 : morceau de la fenêtre principale (classe partielle MainForm), sans changement de comportement.
// Contenu : Choix de l'équipement et du Palico depuis la grille.
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
        ListViewItem FindListItem(ListView lv, int slot)
        {
            string number = (slot + 1).ToString();
            if (slot >= 0 && slot < lv.Items.Count && lv.Items[slot].SubItems[0].Text == number)
                return lv.Items[slot];
            foreach (ListViewItem candidate in lv.Items)
            {
                if (candidate.SubItems[0].Text == number) return candidate;
            }
            return null;
        }

        // Sélectionner une case de la grille sélectionne la ligne correspondante de la liste d'équipement :
        // les listes du bas (type, nom, niveau, décorations, transmog...) travaillent alors sur cet équipement.
        public void SelectEquipSlot(int slot)
        {
            ListViewItem it = FindListItem(listViewEquipment, slot);
            if (it == null) return;
            // Sans ce drapeau, la liste ignore le choix quand elle n'a pas le focus (c'est ce qui bloquait l'interface).
            listSelectBypass = true;
            try
            {
                listViewEquipment.SelectedItems.Clear();
                it.Selected = true;
                it.Focused = true;
            }
            finally
            {
                listSelectBypass = false;
            }
        }

        // Double-clic (ou Entrée) sur une case de la grille : fenêtre de choix du type puis du nom.
        public void FocusEquipEditor(int slot)
        {
            SelectEquipSlot(slot);
            ChooseEquipForSlot(slot);
        }

        public void SelectPalicoSlot(int slot)
        {
            ListViewItem it = FindListItem(listViewPalicoEquipment, slot);
            if (it == null) return;
            listSelectBypass = true;
            try
            {
                listViewPalicoEquipment.SelectedItems.Clear();
                it.Selected = true;
                it.Focused = true;
            }
            finally
            {
                listSelectBypass = false;
            }
        }

        public void FocusPalicoEditor(int slot)
        {
            SelectPalicoSlot(slot);
            ChoosePalicoForSlot(slot);
        }

        // Rareté (0 = rareté 1...) de chaque nom d'une liste d'équipement, pour le filtre de rareté.
        int[] EquipRaritiesFor(int type, int count)
        {
            int[] r = new int[count];
            for (int i = 0; i < count; i++)
            {
                int id = NameCatalog.IdForIndex(type, i);
                r[i] = (i == 0 || id < 0) ? -1 : GameAssets.EquipRarity(type, id);
            }
            return r;
        }

        // Écrit le type et le nom dans la case, par les mêmes listes que l'onglet Équipement
        // (c'est donc exactement la même modification dans la sauvegarde).
        public void SetEquipSlot(int slot, int type, int nameIndex)
        {
            SelectEquipSlot(slot);
            if (listViewEquipment.SelectedItems.Count == 0) return;
            bool old = BypassFocus;
            BypassFocus = true;
            try
            {
                if (comboBoxEquipType.SelectedIndex != type) comboBoxEquipType.SelectedIndex = type;
                if (type != 0 && type != 12 && nameIndex >= 0 && nameIndex < comboBoxEquipName.Items.Count
                    && comboBoxEquipName.SelectedIndex != nameIndex)
                    comboBoxEquipName.SelectedIndex = nameIndex;
            }
            finally
            {
                BypassFocus = old;
            }
            RefreshViews();
        }

        void ChooseEquipForSlot(int slot)
        {
            if (player == null || player.EquipmentInfo == null || listViewEquipment.SelectedItems.Count == 0) return;
            if (slot < 0 || (slot + 1) * 36 > player.EquipmentInfo.Length) return;
            int curType = player.EquipmentInfo[slot * 36] & 0x1F;
            if (curType > 21) curType = 0;
            int curName = (comboBoxEquipType.SelectedIndex == curType) ? comboBoxEquipName.SelectedIndex : -1;
            int type = curType;
            bool askType = (curType == 0 || curType == 12); // case vide : on demande d'abord le type
            while (true)
            {
                if (askType)
                {
                    string[] types = GameConstants.EquipmentTypes;
                    using (NamePickerDialog dt = new NamePickerDialog(
                        RomfsConfig.L("Choose the equipment type", "Choisir le type d'équipement"),
                        types, NameCatalog.EnglishFor(types), type, "", Font))
                    {
                        if (dt.ShowDialog(this) != DialogResult.OK) return;
                        type = dt.SelectedIndex;
                    }
                    if (type == 12) return;
                    if (type == 0)
                    {
                        SetEquipSlot(slot, 0, 0);
                        return;
                    }
                }
                string[] names = NameCatalog.NamesForType(type);
                if (names == null || names.Length < 2) return;
                int cur = (type == curType && curName >= 0) ? curName : 1;
                int[] rar = GameAssets.Loaded ? EquipRaritiesFor(type, names.Length) : null;
                string title = RomfsConfig.L("Choose the equipment: ", "Choisir l'équipement : ") + GameConstants.EquipmentTypes[type];
                DialogResult res;
                int picked;
                using (NamePickerDialog dn = new NamePickerDialog(title, names, NameCatalog.EnglishFor(names), cur, "", Font, rar, true))
                {
                    res = dn.ShowDialog(this);
                    picked = dn.SelectedIndex;
                }
                if (res == DialogResult.OK)
                {
                    SetEquipSlot(slot, type, picked);
                    return;
                }
                if (res == DialogResult.Retry)
                {
                    askType = true; // bouton « Changer le type... »
                    continue;
                }
                return;
            }
        }

        // Même chose pour l'équipement des Palicos (0 = aucun, 1 = arme, 2 = casque, 3 = armure).
        public void SetPalicoSlot(int slot, int type, int nameIndex)
        {
            SelectPalicoSlot(slot);
            if (listViewPalicoEquipment.SelectedItems.Count == 0) return;
            bool old = BypassFocus;
            BypassFocus = true;
            try
            {
                if (comboBoxPalicoEqpType.SelectedIndex != type) comboBoxPalicoEqpType.SelectedIndex = type;
                if (type != 0 && nameIndex >= 0 && nameIndex < comboBoxPalicoEquip.Items.Count
                    && comboBoxPalicoEquip.SelectedIndex != nameIndex)
                    comboBoxPalicoEquip.SelectedIndex = nameIndex;
            }
            finally
            {
                BypassFocus = old;
            }
            RefreshViews();
        }

        static string[] PalicoNamesFor(int type)
        {
            switch (type)
            {
                case 1: return GameConstants.PalicoWeaponNames;
                case 2: return GameConstants.PalicoHeadNames;
                case 3: return GameConstants.PalicoArmorNames;
                default: return null;
            }
        }

        void ChoosePalicoForSlot(int slot)
        {
            if (player == null || player.EquipmentPalico == null || listViewPalicoEquipment.SelectedItems.Count == 0) return;
            int curType = comboBoxPalicoEqpType.SelectedIndex;
            if (curType < 0) curType = 0;
            int curName = comboBoxPalicoEquip.SelectedIndex;
            string[] types = new string[comboBoxPalicoEqpType.Items.Count];
            for (int i = 0; i < types.Length; i++) types[i] = Convert.ToString(comboBoxPalicoEqpType.Items[i]);
            if (types.Length < 2) return;
            int type = curType;
            bool askType = (curType == 0);
            while (true)
            {
                if (askType)
                {
                    using (NamePickerDialog dt = new NamePickerDialog(
                        RomfsConfig.L("Choose the equipment type", "Choisir le type d'équipement"),
                        types, NameCatalog.EnglishFor(types), type, "", Font))
                    {
                        if (dt.ShowDialog(this) != DialogResult.OK) return;
                        type = dt.SelectedIndex;
                    }
                    if (type == 0)
                    {
                        SetPalicoSlot(slot, 0, 0);
                        return;
                    }
                }
                string[] names = PalicoNamesFor(type);
                if (names == null || names.Length < 2) return;
                int cur = (type == curType && curName >= 0 && curName < names.Length) ? curName : 1;
                string title = RomfsConfig.L("Choose the equipment: ", "Choisir l'équipement : ") + types[type];
                int[] rar = null;
                if (GameAssets.Loaded)
                {
                    rar = new int[names.Length];
                    for (int i = 0; i < rar.Length; i++)
                    {
                        int pid = i;
                        if (type == 2 && i < GameConstants.PalicoHeadIDs.Length) pid = GameConstants.PalicoHeadIDs[i];
                        else if (type == 3 && i < GameConstants.PalicoArmorIDs.Length) pid = GameConstants.PalicoArmorIDs[i];
                        rar[i] = (i == 0) ? -1 : GameAssets.PalicoRarity(21 + type, pid);
                    }
                }
                DialogResult res;
                int picked;
                using (NamePickerDialog dn = new NamePickerDialog(title, names, NameCatalog.EnglishFor(names), cur, "", Font, rar, true))
                {
                    res = dn.ShowDialog(this);
                    picked = dn.SelectedIndex;
                }
                if (res == DialogResult.OK)
                {
                    SetPalicoSlot(slot, type, picked);
                    return;
                }
                if (res == DialogResult.Retry)
                {
                    askType = true;
                    continue;
                }
                return;
            }
        }

        // Modifie un emplacement du coffre d'objets (même effet que dans l'onglet Objets).
        public void SetItemSlot(int slot, int itemId, int count)
        {
            if (player == null || player.ItemId == null) return;
            if (slot < 0 || slot >= player.ItemId.Length) return;
            if (itemId < 0 || itemId >= GameConstants.ItemNameList.Length) return;
            if (count < 0) count = 0;
            if (count > 99) count = 99;
            if (itemId == 0) count = 0;
            ListViewItem it = FindListItem(listViewItem, slot);
            if (it == null) return;
            it.SubItems[1].Text = GameConstants.ItemNameList[itemId];
            it.SubItems[2].Text = count.ToString();
            player.ItemId[slot] = itemId.ToString();
            player.ItemCount[slot] = count.ToString();
        }
    }
}
