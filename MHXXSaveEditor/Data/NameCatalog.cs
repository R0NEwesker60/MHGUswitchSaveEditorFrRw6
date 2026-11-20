// MHXXSwitchSaveEditor – version française (modifiée). Licence : GNU GPL v3 (voir LICENSE).
// Fichier ajouté lors de la V2 (coffre graphique), le 14 novembre 2026.
using System;
using System.Collections.Generic;
using System.Reflection;

namespace MHXXSaveEditor.Data
{
    // Garde une copie des noms d'objets en anglais et en français, pour pouvoir chercher dans les deux.
    // (Les tableaux de GameConstants, eux, suivent la langue choisie dans le menu Langue.)
    static class NameCatalog
    {
        public static string[] ItemEN;
        public static string[] ItemFR;

        public static bool HasBoth
        {
            get { return ItemEN != null && ItemFR != null && ItemEN.Length == ItemFR.Length; }
        }

        // À appeler avant que les noms français ne remplacent les noms anglais.
        public static void CaptureEnglish()
        {
            ItemEN = (string[])GameConstants.ItemNameList.Clone();
            // Copie anglaise de toutes les listes de noms (armes, armures, Palico, décorations...).
            englishLists.Clear();
            try
            {
                FieldInfo[] fields = typeof(GameConstants).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                foreach (FieldInfo f in fields)
                {
                    if (f.FieldType != typeof(string[])) continue;
                    string[] now = f.GetValue(null) as string[];
                    if (now == null) continue;
                    englishLists.Add(new KeyValuePair<FieldInfo, string[]>(f, (string[])now.Clone()));
                }
            }
            catch (Exception) { }
        }

        static readonly List<KeyValuePair<FieldInfo, string[]>> englishLists = new List<KeyValuePair<FieldInfo, string[]>>();

        // Retrouve les noms anglais d'une liste affichée (null si on ne les a pas ou si le programme est déjà en anglais).
        public static string[] EnglishFor(string[] current)
        {
            if (!MHXXSaveEditor.Loc.French || current == null) return null;
            foreach (KeyValuePair<FieldInfo, string[]> kv in englishLists)
            {
                string[] now = null;
                try { now = kv.Key.GetValue(null) as string[]; } catch (Exception) { }
                if (now == null || now.Length != current.Length) continue;
                bool same = true;
                for (int i = 0; i < now.Length; i++)
                {
                    if (now[i] != current[i]) { same = false; break; }
                }
                if (same && kv.Value.Length == current.Length) return kv.Value;
            }
            return null;
        }

        // À appeler juste après FrenchNames.Apply().
        public static void CaptureFrench()
        {
            ItemFR = (string[])GameConstants.ItemNameList.Clone();
        }

        // Nom d'un objet dans la langue courante.
        public static string ItemName(int id)
        {
            if (id < 0 || id >= GameConstants.ItemNameList.Length) return "?";
            return GameConstants.ItemNameList[id];
        }

        // Nom d'un équipement dans la langue courante (types de la sauvegarde : 1 à 5 armures, 7 à 21 armes).
        // Pour les armures, l'identifiant de la sauvegarde n'est pas la position dans la liste de noms :
        // on passe par les tableaux d'identifiants, comme le fait l'onglet Équipement.
        public static string EquipName(int type, int id)
        {
            string[] list = EquipList(type);
            if (list == null) return "?";
            int index = id;
            int[] ids = ArmorIds(type);
            if (ids != null) index = Array.IndexOf(ids, id);
            if (index < 0 || index >= list.Length) return "?";
            return list[index];
        }

        // Liste de noms d'un type d'équipement (talisman compris) dans la langue courante ; null si type sans liste.
        public static string[] NamesForType(int type)
        {
            if (type == 6) return GameConstants.EquipTalismanNames;
            return EquipList(type);
        }

        // Identifiant du jeu pour la position 'index' dans la liste de noms (armures : tableaux d'ID ; sinon la position).
        public static int IdForIndex(int type, int index)
        {
            int[] ids = ArmorIds(type);
            if (ids == null) return index;
            return (index >= 0 && index < ids.Length) ? ids[index] : -1;
        }

        static int[] ArmorIds(int type)
        {
            switch (type)
            {
                case 1: return GameConstants.EquipHeadIDs;
                case 2: return GameConstants.EquipChestIDs;
                case 3: return GameConstants.EquipArmsIDs;
                case 4: return GameConstants.EquipWaistIDs;
                case 5: return GameConstants.EquipLegsIDs;
                default: return null;
            }
        }

        static string[] EquipList(int type)
        {
            switch (type)
            {
                case 1: return GameConstants.EquipHeadNames;
                case 2: return GameConstants.EquipChestNames;
                case 3: return GameConstants.EquipArmsNames;
                case 4: return GameConstants.EquipWaistNames;
                case 5: return GameConstants.EquipLegsNames;
                case 7: return GameConstants.EquipGreatSwordNames;
                case 8: return GameConstants.EquipSwordnShieldNames;
                case 9: return GameConstants.EquipHammerNames;
                case 10: return GameConstants.EquipLanceNames;
                case 11: return GameConstants.EquipHeavyBowgunNames;
                case 13: return GameConstants.EquipLightBowgunNames;
                case 14: return GameConstants.EquipLongswordNames;
                case 15: return GameConstants.EquipSwitchAxeNames;
                case 16: return GameConstants.EquipGunlanceNames;
                case 17: return GameConstants.EquipBowNames;
                case 18: return GameConstants.EquipDualBladesNames;
                case 19: return GameConstants.EquipHuntingHornNames;
                case 20: return GameConstants.EquipInsectGlaiveNames;
                case 21: return GameConstants.EquipChargeBladeNames;
                default: return null;
            }
        }
    }
}
