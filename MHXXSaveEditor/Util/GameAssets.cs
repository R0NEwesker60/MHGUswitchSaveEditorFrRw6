// MHXXSwitchSaveEditor – version française (modifiée). Licence : GNU GPL v3 (voir LICENSE).
// Fichier ajouté lors de la V2 (coffre graphique), le 14 novembre 2026.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;

namespace MHXXSaveEditor.Util
{
    // Lit sur place, dans le ROMFS de la personne, l'image d'icônes et les tables qui donnent
    // à chaque objet / équipement sa forme et sa couleur. Rien n'est copié ni redistribué.
    static class GameAssets
    {
        public static bool Loaded;
        public static string Error;

        // Image d'icônes utilisée en ce moment, octets R,G,B,A.
        //  - icônes d'origine : 512 x 1024 pixels, cases de 32 x 32 ;
        //  - icônes du pack HD : 1024 x 2048 pixels, cases de 64 x 64 (même disposition, deux fois plus nettes).
        static byte[] atlas;
        static int AtlasW = 512;
        static int AtlasH = 1024;
        static int CellSize = 32;
        static byte[] atlasSD;   // icônes d'origine (ROMFS)
        static byte[] atlasHD;   // icônes du pack HD (lues sur place dans le fichier .arc de la personne)
        public static string HdError;

        // Table des objets (44 octets par objet).
        static int itemCount;
        static byte[] itemShape;
        static byte[] itemColor;
        static byte[] itemRarity;

        // Table des armures (127 octets par pièce).
        static int armorCount;
        static byte[] armorRarity;
        static byte[] amuletRarity; // rareté (0 à 9) des talismans, lue dans table\amuletData.amlt
        static byte[][] armorFlags; // [0]=homme [1]=femme [2]=lame [3]=arme à distance

        // Tables des armes : une par type d'arme (types 7 à 21 dans la sauvegarde).
        static byte[][] weaponRarity = new byte[15][];

        static readonly Dictionary<string, Bitmap> cache = new Dictionary<string, Bitmap>();

        // Couleurs de rareté des équipements (rareté 1 à 11 = « X »).
        static readonly Color[] RarityColors = new Color[]
        {
            Color.FromArgb(0xE6, 0xE6, 0xE6), // 1
            Color.FromArgb(0xB0, 0x92, 0xF2), // 2
            Color.FromArgb(0xDE, 0xD5, 0x5E), // 3
            Color.FromArgb(0xE8, 0x8E, 0x9E), // 4
            Color.FromArgb(0x6F, 0xC7, 0x73), // 5
            Color.FromArgb(0x70, 0x8E, 0xF8), // 6
            Color.FromArgb(0xDA, 0x56, 0x5A), // 7
            Color.FromArgb(0x4C, 0x9E, 0xAC), // 8
            Color.FromArgb(0xEE, 0x8C, 0x5D), // 9
            Color.FromArgb(0xFF, 0x45, 0xBD), // 10
            Color.FromArgb(0x7D, 0x00, 0xFF)  // X
        };

        // Couleurs d'icônes d'objets (numéro de couleur de la table des objets).
        static readonly Color[] ItemColors = new Color[]
        {
            Color.FromArgb(0xEA, 0xEA, 0xEA), // 0 blanc
            Color.FromArgb(0xE0, 0x50, 0x5C), // 1 rouge
            Color.FromArgb(0x69, 0xBF, 0x83), // 2 vert
            Color.FromArgb(0x9E, 0xBA, 0xFE), // 3 bleu
            Color.FromArgb(0xF6, 0xCE, 0x65), // 4 jaune
            Color.FromArgb(0xC2, 0xA2, 0xD0), // 5 violet
            Color.FromArgb(0x9A, 0xDE, 0xEF), // 6 cyan
            Color.FromArgb(0xF6, 0x99, 0x65), // 7 orange
            Color.FromArgb(0xF7, 0x87, 0x9C), // 8 rose
            Color.FromArgb(0xAD, 0xD5, 0x6D), // 9 vert clair
            Color.FromArgb(0xA9, 0xA9, 0xA9), // 10 gris
            Color.FromArgb(0x8A, 0x6A, 0x2A), // 11 marron
            Color.FromArgb(0x57, 0xEB, 0xC7), // 12 menthe
            Color.FromArgb(0x55, 0x89, 0x3B), // 13 vert foncé
            Color.FromArgb(0x8A, 0x00, 0x00), // 14 rouge foncé
            Color.FromArgb(0x00, 0x33, 0x99), // 15 bleu foncé
            Color.FromArgb(0x5A, 0x00, 0x50)  // 16 violet foncé
        };

        // Transmogrification : dégradé bleu (haut) vers jaune (bas).
        static readonly Color TransmogTop = Color.FromArgb(0x3A, 0x3A, 0xC8);
        static readonly Color TransmogBottom = Color.FromArgb(0xE0, 0xE0, 0x22);

        // Case de l'image d'icônes pour chaque type d'équipement de la sauvegarde.
        static int EquipCell(int type)
        {
            switch (type)
            {
                case 1: return 112; // tête
                case 2: return 113; // torse
                case 3: return 115; // bras
                case 4: return 114; // taille
                case 5: return 116; // jambes
                case 6: return 117; // talisman
                case 7: return 85;  // grande épée
                case 8: return 87;  // épée et bouclier
                case 9: return 89;  // marteau
                case 10: return 97; // lance
                case 11: return 101; // fusil-arbalète lourd
                case 12: return 100;
                case 13: return 99; // fusil-arbalète léger
                case 14: return 86; // épée longue
                case 15: return 103; // morpho-hache
                case 16: return 98; // lancecanon
                case 17: return 102; // arc
                case 18: return 88; // lames doubles
                case 19: return 96; // cor de chasse
                case 20: return 104; // insectoglaive
                case 21: return 105; // volto-hache
                default: return -1;
            }
        }

        // Les formes des objets sont numérotées par rangées de 10, l'image a 16 cases par rangée.
        static int ItemCell(int shape)
        {
            return (shape / 10) * 16 + (shape % 10);
        }

        // Oublie tout (utile si la personne change de dossier ROMFS).
        public static void Reset()
        {
            Loaded = false;
            Error = null;
            atlas = null;
            atlasSD = null;
            atlasHD = null;
            amuletRarity = null;
            AtlasW = 512;
            AtlasH = 1024;
            CellSize = 32;
            HdError = null;
            foreach (Bitmap b in cache.Values) b.Dispose();
            cache.Clear();
        }

        // Charge tout. Retourne false (et remplit Error) en cas de problème.
        public static bool Load()
        {
            if (Loaded) return true;
            try
            {
                string texPath = RomfsConfig.PathOf(Path.Combine("eng", "GUI", "99_texture", "HD_cmn_icon_GSM_NOMIP.tex"));
                if (texPath == null || !File.Exists(texPath))
                    texPath = RomfsConfig.PathOf(Path.Combine("fre", "GUI", "99_texture", "HD_cmn_icon_GSM_NOMIP.tex"));
                if (texPath == null || !File.Exists(texPath))
                {
                    Error = "HD_cmn_icon_GSM_NOMIP.tex introuvable / not found";
                    return false;
                }
                atlas = DecodeTex(File.ReadAllBytes(texPath), 512, 1024);
                if (atlas == null)
                {
                    Error = "Format d'image inattendu / unexpected image format";
                    return false;
                }
                atlasSD = atlas;
                AtlasW = 512;
                AtlasH = 1024;
                CellSize = 32;

                byte[] it = File.ReadAllBytes(RomfsConfig.PathOf(Path.Combine("table", "itemData.itm")));
                itemCount = (int)BitConverter.ToUInt32(it, 4);
                itemShape = new byte[itemCount];
                itemColor = new byte[itemCount];
                itemRarity = new byte[itemCount];
                for (int i = 0; i < itemCount; i++)
                {
                    int o = 8 + i * 44;
                    if (o + 44 > it.Length) break;
                    itemRarity[i] = it[o + 5];
                    itemShape[i] = it[o + 16];
                    itemColor[i] = it[o + 17];
                }

                string asd = RomfsConfig.PathOf(Path.Combine("table", "armorSeriesData.asd"));
                if (File.Exists(asd))
                {
                    byte[] ar = File.ReadAllBytes(asd);
                    armorCount = (int)BitConverter.ToUInt32(ar, 4);
                    armorRarity = new byte[armorCount];
                    armorFlags = new byte[armorCount][];
                    for (int i = 0; i < armorCount; i++)
                    {
                        int o = 8 + i * 127;
                        if (o + 127 > ar.Length) break;
                        armorRarity[i] = ar[o + 103];
                        armorFlags[i] = new byte[] { ar[o + 13], ar[o + 14], ar[o + 15], ar[o + 16] };
                    }
                }

                for (int t = 0; t < 15; t++)
                {
                    string name = "weapon" + t.ToString("00") + "BaseData.w" + t.ToString("00") + "d";
                    string wp = RomfsConfig.PathOf(Path.Combine("table", name));
                    if (wp == null || !File.Exists(wp)) continue;
                    byte[] w = File.ReadAllBytes(wp);
                    int n = (int)BitConverter.ToUInt32(w, 4);
                    if (n <= 0) continue;
                    int rec = (w.Length - 8) / n;
                    if (rec < 12) continue;
                    byte[] rar = new byte[n];
                    for (int i = 0; i < n; i++) rar[i] = w[8 + i * rec + 11];
                    weaponRarity[t] = rar;
                }

                try
                {
                    string ow = RomfsConfig.PathOf(Path.Combine("table", "otWeaponData.owp"));
                    if (ow != null && File.Exists(ow))
                    {
                        byte[] w = File.ReadAllBytes(ow);
                        int n = (int)BitConverter.ToUInt32(w, 4);
                        if (n > 0 && n < 100000)
                        {
                            int rec = (w.Length - 8) / n;
                            if (rec > 6)
                            {
                                palicoHammer = new bool[n];
                                palicoWeaponRarity = new byte[n];
                                for (int i = 0; i < n; i++)
                                {
                                    palicoHammer[i] = w[8 + i * rec + 6] == 1;
                                    if (rec > 22) palicoWeaponRarity[i] = w[8 + i * rec + 22];
                                }
                            }
                        }
                    }
                }
                catch (Exception) { palicoHammer = null; palicoWeaponRarity = null; }

                try
                {
                    string oa = RomfsConfig.PathOf(Path.Combine("table", "otArmorData.oar"));
                    if (oa != null && File.Exists(oa))
                    {
                        byte[] a = File.ReadAllBytes(oa);
                        int n = (int)BitConverter.ToUInt32(a, 4);
                        if (n > 0 && n < 100000)
                        {
                            int rec = (a.Length - 8) / n;
                            if (rec > 7)
                            {
                                palicoArmorRarity = new byte[n];
                                for (int i = 0; i < n; i++) palicoArmorRarity[i] = a[8 + i * rec + 7];
                            }
                        }
                    }
                }
                catch (Exception) { palicoArmorRarity = null; }

                try
                {
                    string am = RomfsConfig.PathOf(Path.Combine("table", "amuletData.amlt"));
                    if (am != null && File.Exists(am))
                    {
                        byte[] a = File.ReadAllBytes(am);
                        int n = (int)BitConverter.ToUInt32(a, 4);
                        if (n > 0 && n < 1000)
                        {
                            int rec = (a.Length - 8) / n;
                            if (rec >= 9)
                            {
                                amuletRarity = new byte[n];
                                for (int i = 0; i < n; i++) amuletRarity[i] = a[8 + i * rec + 8];
                            }
                        }
                    }
                }
                catch (Exception) { amuletRarity = null; }

                Loaded = true;
                // Icônes HD demandées dans les réglages : on essaie, sinon on garde les icônes d'origine.
                if (ThemeSettings.HdIcons) EnableHd(null);
                return true;
            }
            catch (Exception ex)
            {
                Error = ex.Message;
                return false;
            }
        }

        // Décode l'image Switch (pixels rangés par blocs) en octets R,G,B,A rangés ligne par ligne.
        static byte[] DecodeTex(byte[] file, int expectW, int expectH)
        {
            if (file.Length < 24 + expectW * expectH * 4) return null;
            if (file[0] != (byte)'T' || file[1] != (byte)'E' || file[2] != (byte)'X') return null;
            uint b = BitConverter.ToUInt32(file, 8);
            int w = (int)((b >> 6) & 0x1FFF);
            int h = (int)((b >> 19) & 0x1FFF);
            if (w != expectW || h != expectH) return null;
            const int bpp = 4;
            const int blockHeight = 16;
            int rowBytes = w * bpp;
            int gobsPerRow = (rowBytes + 63) / 64;
            byte[] result = new byte[w * h * bpp];
            for (int y = 0; y < h; y++)
            {
                for (int xb = 0; xb < rowBytes; xb++)
                {
                    int gob = (y / (8 * blockHeight)) * 512 * blockHeight * gobsPerRow
                            + (xb / 64) * 512 * blockHeight
                            + ((y % (8 * blockHeight)) / 8) * 512;
                    int xx = xb % 64;
                    int yy = y % 8;
                    int addr = gob + ((xx % 64) / 32) * 256 + ((yy % 8) / 2) * 64
                             + ((xx % 32) / 16) * 32 + (yy % 2) * 16 + (xx % 16);
                    int src = 24 + addr;
                    if (src < file.Length) result[y * rowBytes + xb] = file[src];
                }
            }
            return result;
        }

        // ---- Icônes HD (pack de textures HD) ----

        // Vrai si ce sont les icônes HD qui sont affichées en ce moment.
        public static bool HdActive
        {
            get { return atlas != null && atlasHD != null && object.ReferenceEquals(atlas, atlasHD); }
        }

        // Choisit l'image d'icônes utilisée (HD ou d'origine). Les anciennes icônes en mémoire sont oubliées.
        static void UseAtlas(bool hd)
        {
            if (hd && atlasHD != null)
            {
                atlas = atlasHD;
                AtlasW = 1024;
                AtlasH = 2048;
                CellSize = 64;
            }
            else if (atlasSD != null)
            {
                atlas = atlasSD;
                AtlasW = 512;
                AtlasH = 1024;
                CellSize = 32;
            }
            cache.Clear();
        }

        // Revient aux icônes d'origine (celles un peu floues).
        public static void DisableHd()
        {
            UseAtlas(false);
        }

        // Active les icônes HD. 'path' = fichier du pack HD choisi par la personne (resident_fre.arc, ou un .tex) ;
        // null = on cherche tout seul (réglage mémorisé, puis dossier ROMFS). Retourne false (et remplit HdError) sinon.
        public static bool EnableHd(string path)
        {
            HdError = null;
            if (!Loaded)
            {
                HdError = "Icons not loaded / icônes non chargées";
                return false;
            }
            if (path == null && atlasHD != null)
            {
                UseAtlas(true);
                return true;
            }
            List<string> candidates = new List<string>();
            if (path != null) candidates.Add(path);
            else
            {
                if (!string.IsNullOrEmpty(ThemeSettings.HdPath)) candidates.Add(ThemeSettings.HdPath);
                string[] langs = new string[] { "fre", "eng", "ger", "ita", "spa" };
                foreach (string l in langs)
                {
                    string p = RomfsConfig.PathOf(Path.Combine(l, "arc", "resident_" + l + ".arc"));
                    if (p != null && !candidates.Contains(p)) candidates.Add(p);
                }
            }
            foreach (string c in candidates)
            {
                byte[] a = ReadHdAtlas(c);
                if (a != null)
                {
                    atlasHD = a;
                    UseAtlas(true);
                    if (path != null) ThemeSettings.HdPath = path;
                    return true;
                }
            }
            if (HdError == null)
                HdError = "Pack HD introuvable / HD pack not found";
            return false;
        }

        // Lit l'image d'icônes HD dans un fichier .arc (ou un .tex). Retourne null si ce n'est pas le bon fichier.
        static byte[] ReadHdAtlas(string file)
        {
            try
            {
                if (string.IsNullOrEmpty(file) || !File.Exists(file)) return null;
                byte[] data = File.ReadAllBytes(file);
                byte[] tex = data;
                if (data.Length >= 12 && data[0] == (byte)'A' && data[1] == (byte)'R' && data[2] == (byte)'C' && data[3] == 0)
                {
                    tex = ExtractFromArc(data, "HD_cmn_icon_GSM_NOMIP");
                    if (tex == null)
                    {
                        HdError = "Les icônes ne sont pas dans ce fichier / the icons are not in this file";
                        return null;
                    }
                }
                byte[] result = DecodeTex(tex, 1024, 2048);
                if (result == null)
                {
                    HdError = "Ce fichier ne contient pas les icônes HD (peut-être le fichier d'origine du jeu) / this file does not hold the HD icons";
                    return null;
                }
                return result;
            }
            catch (Exception ex)
            {
                HdError = ex.Message;
                return null;
            }
        }

        // V38 : cherche, dans un dossier choisi par la personne (le dossier du pack HD, ou un dossier au-dessus),
        // le fichier .arc qui contient les icônes HD. Le chemin exact dans le pack n'a pas besoin d'être connu :
        // on regarde tous les fichiers « resident*.arc » du dossier et de ses sous-dossiers.
        public static string FindHdFileIn(string folder)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return null;
            List<string> files = new List<string>();
            CollectResidentArcs(folder, 0, files);
            files.Sort(delegate (string x, string y) { return HdFileRank(x).CompareTo(HdFileRank(y)); });
            foreach (string f in files)
            {
                if (ReadHdAtlas(f) != null) return f;
            }
            return null;
        }

        static int HdFileRank(string file)
        {
            string n = Path.GetFileName(file).ToLowerInvariant();
            if (n == "resident_fre.arc") return 0;
            if (n.StartsWith("resident_fre")) return 1;
            return 2;
        }

        static void CollectResidentArcs(string dir, int depth, List<string> files)
        {
            if (depth > 8) return;
            try
            {
                foreach (string f in Directory.GetFiles(dir, "resident*.arc")) files.Add(f);
            }
            catch (Exception) { }
            try
            {
                foreach (string d in Directory.GetDirectories(dir)) CollectResidentArcs(d, depth + 1, files);
            }
            catch (Exception) { }
        }

        // Oublie le pack HD : retour aux icônes d'origine, et le fichier mémorisé est effacé.
        public static void ForgetHd()
        {
            atlasHD = null;
            ThemeSettings.HdPath = "";
            if (Loaded) UseAtlas(false);
        }

        // Sort un fichier d'une archive .arc (format MT Framework : 12 octets d'en-tête, puis 80 octets par fichier :
        // nom sur 64 octets, type, taille compressée, taille d'origine, position ; données compressées en zlib).
        static byte[] ExtractFromArc(byte[] d, string nameEnd)
        {
            int count = BitConverter.ToUInt16(d, 6);
            for (int i = 0; i < count; i++)
            {
                int o = 12 + 80 * i;
                if (o + 80 > d.Length) break;
                int len = 0;
                while (len < 64 && d[o + len] != 0) len++;
                string name = System.Text.Encoding.ASCII.GetString(d, o, len);
                if (!name.EndsWith(nameEnd, StringComparison.OrdinalIgnoreCase)) continue;
                if (BitConverter.ToUInt32(d, o + 64) != 0x241F5DEBu) continue; // type « TEX »
                long csize = BitConverter.ToUInt32(d, o + 68);
                long usize = BitConverter.ToUInt32(d, o + 72) & 0x1FFFFFFFu;
                long off = BitConverter.ToUInt32(d, o + 76);
                if (off + csize > d.Length || usize <= 0 || usize > 64 * 1024 * 1024) return null;
                byte[] outp = new byte[usize];
                if (csize == usize)
                {
                    Buffer.BlockCopy(d, (int)off, outp, 0, (int)usize);
                    return outp;
                }
                // zlib = 2 octets d'en-tête + données « deflate » (+ somme de contrôle à la fin, ignorée).
                using (MemoryStream ms = new MemoryStream(d, (int)off + 2, (int)csize - 2))
                using (DeflateStream ds = new DeflateStream(ms, CompressionMode.Decompress))
                {
                    int total = 0;
                    while (total < outp.Length)
                    {
                        int n = ds.Read(outp, total, outp.Length - total);
                        if (n <= 0) break;
                        total += n;
                    }
                    if (total != outp.Length) return null;
                }
                return outp;
            }
            return null;
        }

        // ---- Informations de rareté ----

        public static int EquipRarity(int type, int id)
        {
            if (type >= 1 && type <= 5)
            {
                if (armorRarity != null && id >= 0 && id < armorRarity.Length) return armorRarity[id];
                return 0;
            }
            if (type == 6)
            {
                // Talismans : rareté lue dans la table du jeu (Pion = 1 ... Divin = 10), comme les armes et armures.
                if (amuletRarity != null && id >= 0 && id < amuletRarity.Length) return amuletRarity[id];
                return (id >= 1 && id <= 10) ? id - 1 : 0;
            }
            if (type >= 7 && type <= 21)
            {
                byte[] r = weaponRarity[type - 7];
                if (r != null && id >= 0 && id < r.Length) return r[id];
            }
            return 0;
        }

        // Rareté (0 = rareté 1...) d'un équipement de Palico : 22 = arme, 23 = casque, 24 = armure.
        public static int PalicoRarity(int type, int id)
        {
            if (type == 22)
            {
                if (palicoWeaponRarity != null && id >= 0 && id < palicoWeaponRarity.Length) return palicoWeaponRarity[id];
            }
            else if (palicoArmorRarity != null && id >= 0 && id < palicoArmorRarity.Length) return palicoArmorRarity[id];
            return 0;
        }

        // Une armure est utilisable si le sexe et le type de chasseur (lame / arme à distance) conviennent.
        public static bool ArmorUsable(int id, int gender, int style)
        {
            if (armorFlags == null || id < 0 || id >= armorFlags.Length || armorFlags[id] == null) return true;
            byte[] f = armorFlags[id];
            bool genderOk = (gender == 0) ? f[0] != 0 : f[1] != 0;
            bool styleOk = (style == 0) ? f[2] != 0 : f[3] != 0;
            return genderOk && styleOk;
        }

        // ---- Icônes ----

        // Icône d'un objet du coffre (32 x 32, transparente autour de la forme). Null si indisponible.
        public static Bitmap ItemIcon(int id)
        {
            if (!Loaded || id <= 0 || id >= itemCount) return null;
            int shape = itemShape[id];
            // Forme 0 = entrée interne du jeu (ID 132 à 205 : « Inutilisé », munitions, enduits...) : pas de vrai logo.
            if (shape == 0) return null;
            int col = itemColor[id];
            Color tint = col < ItemColors.Length ? ItemColors[col] : Color.White;
            string key = "i" + shape + "_" + col;
            Bitmap bmp;
            if (cache.TryGetValue(key, out bmp)) return bmp;
            bmp = MakeIcon(ItemCell(shape), tint, tint, false, 1.0f);
            cache[key] = bmp;
            return bmp;
        }

        // Numéro de forme d'icône d'un objet (sert aux catégories de la recherche) ; -1 si inconnu.
        public static int ItemShapeOf(int id)
        {
            if (!Loaded || itemShape == null || id < 0 || id >= itemCount) return -1;
            return itemShape[id];
        }

        public static int ItemRarityOf(int id)
        {
            if (!Loaded || id < 0 || id >= itemCount) return 0;
            return itemRarity[id];
        }

        // Icône d'une arme ou d'une pièce d'armure. 'dim' assombrit (pièce inutilisable pour ce chasseur).
        public static Bitmap EquipIcon(int type, int id, bool transmog, bool dim)
        {
            if (!Loaded) return null;
            int cell = EquipCell(type);
            if (cell < 0) return null;
            int rarity = EquipRarity(type, id);
            if (rarity < 0 || rarity >= RarityColors.Length) rarity = 0;
            string key = "e" + type + "_" + rarity + "_" + (transmog ? "t" : "n") + (dim ? "d" : "l");
            Bitmap bmp;
            if (cache.TryGetValue(key, out bmp)) return bmp;
            float factor = dim ? 0.4f : 1.0f;
            if (transmog) bmp = MakeIcon(cell, TransmogTop, TransmogBottom, true, factor);
            else bmp = MakeIcon(cell, RarityColors[rarity], RarityColors[rarity], false, factor);
            cache[key] = bmp;
            return bmp;
        }

        // Icône d'un équipement de Palico (22 = arme, 23 = casque, 24 = armure). Case de l'atlas choisie à l'œil.
        const int PalicoWeaponCell = 119; // massue (panneau avec une patte)
        const int PalicoBladeCell = 118;  // lame (arme Palico)
        const int PalicoHeadCell = 120;
        const int PalicoArmorCell = 121;

        static bool[] palicoHammer;
        static byte[] palicoWeaponRarity;
        static byte[] palicoArmorRarity;

        // Arme Palico : massue (panneau) ou lame (lame courbe), d'après la table du jeu.
        static bool IsPalicoHammer(int eid)
        {
            return palicoHammer != null && eid >= 0 && eid < palicoHammer.Length && palicoHammer[eid];
        }

        public static Bitmap PalicoIcon(int type, int eid, bool transmog)
        {
            if (!Loaded) return null;
            int cell;
            switch (type)
            {
                case 22: cell = IsPalicoHammer(eid) ? PalicoWeaponCell : PalicoBladeCell; break;
                case 23: cell = PalicoHeadCell; break;
                case 24: cell = PalicoArmorCell; break;
                default: return null;
            }
            int rar = 0;
            if (type == 22) { if (palicoWeaponRarity != null && eid >= 0 && eid < palicoWeaponRarity.Length) rar = palicoWeaponRarity[eid]; }
            else if (palicoArmorRarity != null && eid >= 0 && eid < palicoArmorRarity.Length) rar = palicoArmorRarity[eid];
            if (rar < 0 || rar >= RarityColors.Length) rar = 0;
            string key = "p" + cell + "_" + rar + (transmog ? "t" : "n");
            Bitmap bmp;
            if (cache.TryGetValue(key, out bmp)) return bmp;
            if (transmog) bmp = MakeIcon(cell, TransmogTop, TransmogBottom, true, 1.0f);
            else bmp = MakeIcon(cell, RarityColors[rar], RarityColors[rar], false, 1.0f);
            cache[key] = bmp;
            return bmp;
        }

        public static Color RarityColor(int rarity)
        {
            if (rarity < 0 || rarity >= RarityColors.Length) return RarityColors[0];
            return RarityColors[rarity];
        }

        // Fabrique une icône : le gris de l'image d'origine est multiplié par la couleur (ou le dégradé).
        // L'icône fait 32 x 32 (icônes d'origine) ou 64 x 64 (icônes HD) ; l'affichage la met à l'échelle.
        static Bitmap MakeIcon(int cell, Color top, Color bottom, bool gradient, float factor)
        {
            int cs = CellSize;
            int perRow = AtlasW / cs;
            Bitmap bmp = new Bitmap(cs, cs, PixelFormat.Format32bppArgb);
            int cx = (cell % perRow) * cs;
            int cy = (cell / perRow) * cs;
            if (cy + cs > AtlasH) return bmp;
            int[] px = new int[cs * cs]; // pixels ARGB, remplis d'un coup (plus rapide que SetPixel)
            int minY = cs - 1, maxY = 0;
            for (int y = 0; y < cs; y++)
                for (int x = 0; x < cs; x++)
                {
                    int ai = ((cy + y) * AtlasW + (cx + x)) * 4;
                    if (atlas[ai + 3] > 0)
                    {
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            for (int y = 0; y < cs; y++)
            {
                float t = 0f;
                if (gradient && maxY > minY) t = (float)(y - minY) / (float)(maxY - minY);
                if (t < 0f) t = 0f;
                if (t > 1f) t = 1f;
                float r = (top.R + (bottom.R - top.R) * t) * factor;
                float g = (top.G + (bottom.G - top.G) * t) * factor;
                float b = (top.B + (bottom.B - top.B) * t) * factor;
                for (int x = 0; x < cs; x++)
                {
                    int ai = ((cy + y) * AtlasW + (cx + x)) * 4;
                    int a = atlas[ai + 3];
                    if (a == 0) continue;
                    float gray = atlas[ai] / 255f;
                    int rr = Clamp(r * gray);
                    int gg = Clamp(g * gray);
                    int bb = Clamp(b * gray);
                    px[y * cs + x] = Color.FromArgb(a, rr, gg, bb).ToArgb();
                }
            }
            BitmapData bd = bmp.LockBits(new Rectangle(0, 0, cs, cs), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            System.Runtime.InteropServices.Marshal.Copy(px, 0, bd.Scan0, px.Length);
            bmp.UnlockBits(bd);
            return bmp;
        }

        static int Clamp(float v)
        {
            if (v < 0f) return 0;
            if (v > 255f) return 255;
            return (int)v;
        }
    }
}
