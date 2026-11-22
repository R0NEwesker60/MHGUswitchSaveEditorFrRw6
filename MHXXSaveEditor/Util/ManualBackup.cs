// MHXXSwitchSaveEditor – version française (modifiée). Licence : GNU GPL v3 (voir LICENSE).
// Fichier ajouté en version 48 : sauvegardes manuelles du dossier save-0 (notes, favoris, limite) et injection d'une copie
// dans save-0 avec une copie « avant injection » de ce qui va être remplacé.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace MHXXSaveEditor.Util
{
    static class ManualBackup
    {
        public class Item
        {
            public string Dir;          // dossier de la copie
            public string Name;         // nom du dossier copié (ex. « save-0 »)
            public string Stamp;        // « 2026-11-21_09-30-00 »
            public string Note = "";
            public bool Fav;
            public int Files;
            public long Size;
            public string When { get { return BackupManager.WhenFromStamp(Stamp); } }
        }

        public class CopyResult
        {
            public bool Ok;
            public string Dir;
            public int Files;
            public int Failed;
            public string Error;
        }

        public class InjectResult
        {
            public bool Ok;
            public string Error;
            public string BeforeDir;   // copie de ce qui a été remplacé (null s'il n'y avait rien à remplacer)
            public int Replaced;       // fichiers qui existaient et ont été écrasés
            public int Written;        // fichiers écrits en tout
            public int Failed;
        }

        // Les dossiers sont rangés à côté des autres dossiers de copies, dans le dossier choisi.
        public static string ManualFolder
        {
            get { return Path.Combine(BackupManager.Root, ThemeSettings.BackupManualName); }
        }

        public static string InjectFolder
        {
            get { return Path.Combine(BackupManager.Root, ThemeSettings.BackupInjectName); }
        }

        static readonly Regex Rx = new Regex(@"^(?<name>.+)_(?<stamp>\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2})(_\d+)?$");

        // La note est un fichier « Note.txt » DANS le dossier de la copie (comme dans l'autre application) ; il n'est jamais injecté dans le jeu.
        // Le favori n'est pas un fichier : il est mémorisé dans la configuration du programme.
        public const string NoteFileName = "Note.txt";
        static string NotePath(string dir) { return Path.Combine(dir, NoteFileName); }

        // Version 48 rangeait la note (« xxx.note.txt ») et le favori (« xxx.favori ») à côté de la copie : on les range au bon endroit.
        static void MigrateSidecars(string dir)
        {
            try
            {
                string oldNote = dir + ".note.txt";
                if (File.Exists(oldNote))
                {
                    if (!File.Exists(NotePath(dir))) File.Move(oldNote, NotePath(dir));
                    else File.Delete(oldNote);
                }
                string oldFav = dir + ".favori";
                if (File.Exists(oldFav))
                {
                    ThemeSettings.ManualFavs.Add(Path.GetFileName(dir));
                    File.Delete(oldFav);
                    ThemeSettings.Save();
                }
            }
            catch (Exception) { }
        }

        public static List<Item> List(string root)
        {
            List<Item> res = new List<Item>();
            try
            {
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return res;
                foreach (string d in Directory.GetDirectories(root))
                {
                    Match m = Rx.Match(Path.GetFileName(d));
                    if (!m.Success) continue;
                    MigrateSidecars(d);
                    Item it = new Item();
                    it.Dir = d;
                    it.Name = m.Groups["name"].Value;
                    it.Stamp = m.Groups["stamp"].Value;
                    try
                    {
                        if (File.Exists(NotePath(d))) it.Note = File.ReadAllText(NotePath(d), Encoding.UTF8);
                    }
                    catch (Exception) { }
                    it.Fav = ThemeSettings.ManualFavs.Contains(Path.GetFileName(d));
                    Count(d, out it.Files, out it.Size);
                    res.Add(it);
                }
                res.Sort(delegate (Item a, Item c)
                {
                    int k = string.Compare(c.Stamp, a.Stamp, StringComparison.Ordinal);
                    if (k != 0) return k;
                    return string.Compare(c.Dir, a.Dir, StringComparison.OrdinalIgnoreCase);
                });
            }
            catch (Exception) { }
            return res;
        }

        public static void Count(string dir, out int files, out long size)
        {
            files = 0;
            size = 0;
            try
            {
                string note = NotePath(dir);
                foreach (string f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                {
                    if (string.Equals(f, note, StringComparison.OrdinalIgnoreCase)) continue; // la note n'est pas une sauvegarde
                    files++;
                    try { size += new FileInfo(f).Length; } catch (Exception) { }
                }
            }
            catch (Exception) { }
        }

        public static void WriteNote(Item it, string text)
        {
            text = text ?? "";
            string p = NotePath(it.Dir);
            if (text.Trim().Length == 0)
            {
                if (File.Exists(p)) File.Delete(p);
                it.Note = "";
                return;
            }
            File.WriteAllText(p, text, new UTF8Encoding(true));
            it.Note = text;
        }

        public static void SetFav(Item it, bool fav)
        {
            string leaf = Path.GetFileName(it.Dir);
            if (fav) ThemeSettings.ManualFavs.Add(leaf);
            else ThemeSettings.ManualFavs.Remove(leaf);
            ThemeSettings.Save();
            it.Fav = fav;
        }

        public static void Delete(Item it)
        {
            Directory.Delete(it.Dir, true);
            if (ThemeSettings.ManualFavs.Remove(Path.GetFileName(it.Dir))) ThemeSettings.Save();
        }

        static string LeafName(string fullDir)
        {
            string n = Path.GetFileName(fullDir.TrimEnd('\\'));
            return string.IsNullOrEmpty(n) ? "save" : n;
        }

        static string NewCopyDir(string root, string name)
        {
            Directory.CreateDirectory(root);
            string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string dest = Path.Combine(root, name + "_" + stamp);
            int n = 1;
            while (Directory.Exists(dest) || File.Exists(dest))
            {
                dest = Path.Combine(root, name + "_" + stamp + "_" + n.ToString());
                n++;
            }
            return dest;
        }

        static void CopyTree(string src, string dest, CopyResult res)
        {
            Directory.CreateDirectory(dest);
            foreach (string f in Directory.GetFiles(src))
            {
                try
                {
                    File.Copy(f, Path.Combine(dest, Path.GetFileName(f)), false);
                    res.Files++;
                }
                catch (Exception) { res.Failed++; }
            }
            foreach (string d in Directory.GetDirectories(src))
            {
                try { CopyTree(d, Path.Combine(dest, Path.GetFileName(d)), res); }
                catch (Exception) { res.Failed++; }
            }
        }

        // Nouvelle sauvegarde manuelle : copie de TOUT le dossier save-0. Toujours faite (pas de comparaison).
        public static CopyResult CreateManual(string src)
        {
            CopyResult res = new CopyResult();
            try
            {
                if (string.IsNullOrEmpty(src) || !Directory.Exists(src))
                {
                    res.Error = RomfsConfig.L("Folder to copy not found: ", "Dossier à copier introuvable : ") + src;
                    return res;
                }
                string srcFull = Path.GetFullPath(src).TrimEnd('\\');
                string root = ManualFolder;
                string rootFull = Path.GetFullPath(root).TrimEnd('\\');
                if (string.Equals(rootFull, srcFull, StringComparison.OrdinalIgnoreCase)
                    || rootFull.StartsWith(srcFull + "\\", StringComparison.OrdinalIgnoreCase))
                {
                    res.Error = RomfsConfig.L("The backup folder is inside the folder to copy. Choose another backup folder.",
                                              "Le dossier des copies est à l'intérieur du dossier à copier. Choisissez un autre dossier de copies.");
                    return res;
                }
                string dest = NewCopyDir(root, LeafName(srcFull));
                CopyTree(srcFull, dest, res);
                res.Dir = dest;
                res.Ok = res.Files > 0 || res.Failed == 0;
                if (ThemeSettings.ManualLimitOn) Prune(root, ThemeSettings.ManualMax, true);
            }
            catch (Exception ex)
            {
                res.Ok = false;
                res.Error = ex.Message;
            }
            return res;
        }

        // Supprime les plus anciennes copies au-delà du maximum. Les favoris ne comptent pas et ne sont jamais supprimés.
        static void Prune(string root, int max, bool keepFavorites)
        {
            try
            {
                if (max < 1) max = 1;
                int kept = 0;
                foreach (Item it in List(root)) // de la plus récente à la plus ancienne
                {
                    if (keepFavorites && it.Fav) continue;
                    kept++;
                    if (kept > max)
                    {
                        try { Delete(it); } catch (Exception) { }
                    }
                }
            }
            catch (Exception) { }
        }

        static void ListFiles(string dirFull, string relPrefix, List<string> outList)
        {
            foreach (string f in Directory.GetFiles(dirFull))
                outList.Add(relPrefix.Length == 0 ? Path.GetFileName(f) : Path.Combine(relPrefix, Path.GetFileName(f)));
            foreach (string d in Directory.GetDirectories(dirFull))
            {
                string leaf = Path.GetFileName(d);
                ListFiles(d, relPrefix.Length == 0 ? leaf : Path.Combine(relPrefix, leaf), outList);
            }
        }

        // Injection d'une copie « dossier » : écrase seulement les fichiers de la copie, laisse les autres.
        // Avant, les fichiers qui vont être remplacés sont copiés dans le dossier « avant injection » (retour en arrière possible).
        public static InjectResult InjectFolderCopy(string srcDir, string targetDir)
        {
            InjectResult res = new InjectResult();
            try
            {
                if (string.IsNullOrEmpty(srcDir) || !Directory.Exists(srcDir))
                {
                    res.Error = RomfsConfig.L("The copy was not found: ", "La copie est introuvable : ") + srcDir;
                    return res;
                }
                if (string.IsNullOrEmpty(targetDir) || !Directory.Exists(targetDir))
                {
                    res.Error = RomfsConfig.L("The save-0 folder was not found: ", "Le dossier save-0 est introuvable : ") + targetDir;
                    return res;
                }
                string srcFull = Path.GetFullPath(srcDir).TrimEnd('\\');
                string tgtFull = Path.GetFullPath(targetDir).TrimEnd('\\');
                List<string> rels = new List<string>();
                ListFiles(srcFull, "", rels);
                rels.RemoveAll(delegate (string r) { return string.Equals(r, NoteFileName, StringComparison.OrdinalIgnoreCase); }); // la note n'est JAMAIS injectée
                if (rels.Count == 0)
                {
                    res.Error = RomfsConfig.L("The copy is empty.", "La copie est vide.");
                    return res;
                }
                List<string> existing = new List<string>();
                foreach (string rel in rels)
                    if (File.Exists(Path.Combine(tgtFull, rel))) existing.Add(rel);
                if (existing.Count > 0)
                {
                    string bdir = NewCopyDir(InjectFolder, LeafName(tgtFull));
                    foreach (string rel in existing)
                    {
                        string d = Path.Combine(bdir, rel);
                        Directory.CreateDirectory(Path.GetDirectoryName(d));
                        File.Copy(Path.Combine(tgtFull, rel), d, false);
                    }
                    res.BeforeDir = bdir;
                }
                foreach (string rel in rels)
                {
                    try
                    {
                        string t = Path.Combine(tgtFull, rel);
                        Directory.CreateDirectory(Path.GetDirectoryName(t));
                        File.Copy(Path.Combine(srcFull, rel), t, true);
                        res.Written++;
                    }
                    catch (Exception ex)
                    {
                        res.Failed++;
                        if (res.Error == null) res.Error = ex.Message;
                    }
                }
                res.Replaced = existing.Count;
                res.Ok = res.Failed == 0;
                Prune(InjectFolder, ThemeSettings.InjectMax, false);
            }
            catch (Exception ex)
            {
                res.Ok = false;
                res.Error = ex.Message;
            }
            return res;
        }

        // Injection d'une copie « un seul fichier » (transfert, avant enregistrement) : remplace seulement ce fichier dans save-0.
        public static InjectResult InjectSingleFile(string srcFile, string targetDir, string targetName)
        {
            InjectResult res = new InjectResult();
            try
            {
                if (string.IsNullOrEmpty(srcFile) || !File.Exists(srcFile))
                {
                    res.Error = RomfsConfig.L("The copy was not found: ", "La copie est introuvable : ") + srcFile;
                    return res;
                }
                if (string.IsNullOrEmpty(targetDir) || !Directory.Exists(targetDir))
                {
                    res.Error = RomfsConfig.L("The save-0 folder was not found: ", "Le dossier save-0 est introuvable : ") + targetDir;
                    return res;
                }
                string tgtFull = Path.GetFullPath(targetDir).TrimEnd('\\');
                string t = Path.Combine(tgtFull, targetName);
                if (File.Exists(t))
                {
                    string bdir = NewCopyDir(InjectFolder, LeafName(tgtFull));
                    Directory.CreateDirectory(bdir);
                    File.Copy(t, Path.Combine(bdir, targetName), false);
                    res.BeforeDir = bdir;
                    res.Replaced = 1;
                }
                File.Copy(srcFile, t, true);
                res.Written = 1;
                res.Ok = true;
                Prune(InjectFolder, ThemeSettings.InjectMax, false);
            }
            catch (Exception ex)
            {
                res.Ok = false;
                res.Error = ex.Message;
            }
            return res;
        }
    }
}
