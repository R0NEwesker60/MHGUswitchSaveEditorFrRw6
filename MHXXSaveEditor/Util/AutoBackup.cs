// MHXXSwitchSaveEditor – version française (modifiée). Licence : GNU GPL v3 (voir LICENSE).
// Fichier ajouté en version 42 : copie automatique du dossier de sauvegarde (save-0) après la fermeture du jeu.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace MHXXSaveEditor.Util
{
    static class AutoBackup
    {
        // Temps d'attente, en secondes, entre la fermeture du jeu et la copie.
        public const int DelaySeconds = 7;
        // Le jeu doit avoir tourné au moins ce nombre de secondes pour qu'une copie automatique soit faite (lancé par erreur = pas de copie).
        public const int MinRunSeconds = 15;

        public class Result
        {
            public bool Ok;
            public string Dir;
            public int Files;
            public int Failed;
            public string Error;
            public bool Skipped;   // rien n'a changé depuis la dernière copie automatique : pas de nouvelle copie
        }

        // Dossier des copies automatiques : à côté des deux autres dossiers de copies.
        public static string Folder
        {
            get { return Path.Combine(BackupManager.Root, ThemeSettings.BackupAutoName); }
        }

        public static bool IsRunning(string exeName)
        {
            if (string.IsNullOrEmpty(exeName)) return false;
            try
            {
                Process[] ps = Process.GetProcessesByName(exeName);
                bool running = ps.Length > 0;
                foreach (Process p in ps) p.Dispose();
                return running;
            }
            catch (Exception) { return false; }
        }

        // Copie TOUT le contenu du dossier (fichiers et sous-dossiers : sauvegardes, backup du jeu, mods...) dans un nouveau dossier daté.
        public static Result Run(string src, int max, bool skipIfSame)
        {
            Result res = new Result();
            try
            {
                if (string.IsNullOrEmpty(src) || !Directory.Exists(src))
                {
                    res.Error = RomfsConfig.L("Folder to copy not found: ", "Dossier à copier introuvable : ") + src;
                    return res;
                }
                string srcFull = Path.GetFullPath(src).TrimEnd('\\');
                string root = Folder;
                string rootFull = Path.GetFullPath(root).TrimEnd('\\');
                if (string.Equals(rootFull, srcFull, StringComparison.OrdinalIgnoreCase)
                    || rootFull.StartsWith(srcFull + "\\", StringComparison.OrdinalIgnoreCase))
                {
                    res.Error = RomfsConfig.L("The backup folder is inside the folder to copy. Choose another backup folder.",
                                              "Le dossier des copies est à l'intérieur du dossier à copier. Choisissez un autre dossier de copies.");
                    return res;
                }
                string name = Path.GetFileName(srcFull);
                if (string.IsNullOrEmpty(name)) name = "save";
                if (skipIfSame)
                {
                    string last = LatestCopy(root, name);
                    if (last != null && SameTree(srcFull, last))
                    {
                        res.Skipped = true;
                        res.Ok = true;
                        res.Dir = last;
                        return res;
                    }
                }
                Directory.CreateDirectory(root);
                string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string dest = Path.Combine(root, name + "_" + stamp);
                int n = 1;
                while (Directory.Exists(dest) || File.Exists(dest))
                {
                    dest = Path.Combine(root, name + "_" + stamp + "_" + n.ToString());
                    n++;
                }
                CopyDir(srcFull, dest, res);
                res.Dir = dest;
                res.Ok = res.Files > 0 || res.Failed == 0;
                Prune(root, name, max);
            }
            catch (Exception ex)
            {
                res.Ok = false;
                res.Error = ex.Message;
            }
            return res;
        }

        // La copie automatique la plus récente de ce dossier (ou null).
        static string LatestCopy(string root, string name)
        {
            try
            {
                if (!Directory.Exists(root)) return null;
                string best = null;
                string bestKey = null;
                foreach (string d in Directory.GetDirectories(root))
                {
                    string leaf = Path.GetFileName(d);
                    Match m = CopyRx.Match(leaf);
                    if (!m.Success) continue;
                    if (!string.Equals(m.Groups["name"].Value, name, StringComparison.OrdinalIgnoreCase)) continue;
                    string key = m.Groups["stamp"].Value + "|" + leaf;
                    if (bestKey == null || string.Compare(key, bestKey, StringComparison.Ordinal) > 0)
                    {
                        bestKey = key;
                        best = d;
                    }
                }
                return best;
            }
            catch (Exception) { return null; }
        }

        // Les deux dossiers contiennent exactement les mêmes fichiers, octet pour octet ?
        static bool SameTree(string a, string b)
        {
            try
            {
                string[] fa = Directory.GetFiles(a);
                string[] fb = Directory.GetFiles(b);
                if (fa.Length != fb.Length) return false;
                foreach (string f in fa)
                {
                    string g = Path.Combine(b, Path.GetFileName(f));
                    if (!File.Exists(g) || !SameFile(f, g)) return false;
                }
                string[] da = Directory.GetDirectories(a);
                string[] db = Directory.GetDirectories(b);
                if (da.Length != db.Length) return false;
                foreach (string d in da)
                {
                    string e = Path.Combine(b, Path.GetFileName(d));
                    if (!Directory.Exists(e) || !SameTree(d, e)) return false;
                }
                return true;
            }
            catch (Exception) { return false; } // en cas de doute, on fait la copie
        }

        static bool SameFile(string f, string g)
        {
            if (new FileInfo(f).Length != new FileInfo(g).Length) return false;
            byte[] x = new byte[65536];
            byte[] y = new byte[65536];
            using (FileStream sx = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (FileStream sy = new FileStream(g, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                while (true)
                {
                    int nx = ReadFull(sx, x);
                    int ny = ReadFull(sy, y);
                    if (nx != ny) return false;
                    if (nx == 0) return true;
                    for (int i = 0; i < nx; i++)
                        if (x[i] != y[i]) return false;
                }
            }
        }

        static int ReadFull(Stream s, byte[] buf)
        {
            int total = 0;
            while (total < buf.Length)
            {
                int n = s.Read(buf, total, buf.Length - total);
                if (n <= 0) break;
                total += n;
            }
            return total;
        }

        static void CopyDir(string src, string dest, Result res)
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
                try { CopyDir(d, Path.Combine(dest, Path.GetFileName(d)), res); }
                catch (Exception) { res.Failed++; }
            }
        }

        static readonly Regex CopyRx = new Regex(@"^(?<name>.+)_(?<stamp>\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2})(_\d+)?$");

        // Supprime les plus anciennes copies automatiques (de ce dossier) au-delà du maximum choisi.
        static void Prune(string root, string name, int max)
        {
            try
            {
                if (max < 1) max = 1;
                List<KeyValuePair<string, string>> mine = new List<KeyValuePair<string, string>>(); // (stamp + nom du dossier, chemin)
                foreach (string d in Directory.GetDirectories(root))
                {
                    string leaf = Path.GetFileName(d);
                    Match m = CopyRx.Match(leaf);
                    if (!m.Success) continue;
                    if (!string.Equals(m.Groups["name"].Value, name, StringComparison.OrdinalIgnoreCase)) continue;
                    mine.Add(new KeyValuePair<string, string>(m.Groups["stamp"].Value + "|" + leaf, d));
                }
                mine.Sort(delegate (KeyValuePair<string, string> a, KeyValuePair<string, string> c)
                {
                    return string.Compare(c.Key, a.Key, StringComparison.Ordinal); // les plus récentes d'abord
                });
                for (int i = max; i < mine.Count; i++)
                {
                    try { Directory.Delete(mine[i].Value, true); } catch (Exception) { }
                }
            }
            catch (Exception) { }
        }
    }
}
