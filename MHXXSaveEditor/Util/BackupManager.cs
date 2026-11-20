// MHXXSwitchSaveEditor – version française (modifiée). Licence : GNU GPL v3 (voir LICENSE).
// Fichier ajouté lors de la version 16 : copie de sécurité automatique de la sauvegarde.
// Version 25 : chaque copie est rangée dans son propre dossier (avec une note.txt facultative) ;
// les copies faites avant un transfert A / B vont dans un autre dossier, posé à côté du premier.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace MHXXSaveEditor.Util
{
    // Avant chaque écriture dans un fichier de sauvegarde, copie l'ancienne version dans un dossier :
    //   <dossier des copies>\<nom>_<date>_<heure>\<fichier de sauvegarde>   (+ note.txt si une note existe)
    // Quand il y a plus de copies que le maximum choisi, les plus anciennes sont supprimées.
    static class BackupManager
    {
        public const int DefaultMax = 10;
        public const string NoteFileName = "note.txt";
        public static string LastBackup;

        // Une copie de sécurité trouvée sur le disque.
        public class Entry
        {
            public string Location;   // dossier de la copie (ou fichier, pour une ancienne copie sans dossier)
            public bool IsLegacy;     // ancienne copie : fichier posé directement dans le dossier des copies
            public string DataFile;   // le fichier de sauvegarde à restaurer
            public string Name;       // nom du système (ex. « system »)
            public string Ext;        // extension du fichier d'origine (anciennes copies seulement)
            public string BaseName;   // « system_2026-11-16_20-41-03 » (avec _2 si deux copies dans la même seconde)
            public string Stamp;      // « 2026-11-16_20-41-03 »
            public long Size;
            public string Note = "";

            public string When
            {
                get { return WhenFromStamp(Stamp); }
            }
        }

        public static string DefaultFolder
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MHXX Save Editor - Sauvegardes");
            }
        }

        // Le dossier choisi par la personne (« Dossier des copies ») : le programme y crée DEUX dossiers côte à côte,
        // par exemple  ...\MHGU -Recomp\Save avant edit  et  ...\MHGU -Recomp\Save avant edit - Transferts
        public static string Root
        {
            get { return string.IsNullOrEmpty(ThemeSettings.BackupDir) ? DefaultFolder : ThemeSettings.BackupDir; }
        }

        public static string NormalSubFolder
        {
            get { return ThemeSettings.BackupNormalName; }
        }

        public static string TransferSubFolder
        {
            get { return ThemeSettings.BackupTransferName; }
        }
        // Noms utilisés par la version 26 (dossiers rangés à l'intérieur du dossier « normal ») : on les lit encore.
        const string OldNormalSubFolder = "Copies normales";
        const string OldTransferSubFolder = "Copies de transfert";

        // Dossier des copies « normales » (avant chaque enregistrement, échange, duplication, restauration...).
        public static string Folder
        {
            get { return Path.Combine(Root, NormalSubFolder); }
        }

        // Dossier des copies faites avant un transfert A / B : à côté du premier, dans le même dossier choisi.
        public static string TransferFolder
        {
            get { return Path.Combine(Root, TransferSubFolder); }
        }

        // Toutes les copies normales (les plus récentes d'abord), y compris celles rangées par la version 26.
        public static List<Entry> ListNormal()
        {
            List<Entry> res = List(Folder);
            res.AddRange(List(Path.Combine(Folder, OldNormalSubFolder)));
            SortEntries(res);
            return res;
        }

        // Toutes les copies de transfert, y compris celles rangées par la version 26.
        public static List<Entry> ListTransfer()
        {
            List<Entry> res = List(TransferFolder);
            res.AddRange(List(Path.Combine(Folder, OldTransferSubFolder)));
            SortEntries(res);
            return res;
        }

        static void SortEntries(List<Entry> res)
        {
            res.Sort(delegate (Entry a, Entry b)
            {
                int c = string.Compare(b.Stamp, a.Stamp, StringComparison.Ordinal);
                if (c != 0) return c;
                return string.Compare(b.Location, a.Location, StringComparison.OrdinalIgnoreCase);
            });
        }

        // Copie « normale ». Retourne true si on peut continuer (copie faite, ou copie désactivée, ou la personne accepte de continuer sans copie).
        public static bool Backup(IWin32Window owner, string file)
        {
            return MakeCopy(owner, file, Folder, null);
        }

        // Copie faite avant un transfert : dans le dossier des transferts, avec une note automatique.
        public static bool BackupTransfer(IWin32Window owner, string file, string note)
        {
            return MakeCopy(owner, file, TransferFolder, note);
        }

        static bool MakeCopy(IWin32Window owner, string file, string root, string note)
        {
            LastBackup = null;
            if (!ThemeSettings.BackupOn) return true;
            try
            {
                if (string.IsNullOrEmpty(file) || !File.Exists(file)) return true; // rien à protéger : le fichier n'existe pas encore
                Directory.CreateDirectory(root);
                string name = Path.GetFileNameWithoutExtension(file);
                string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string copyDir = Path.Combine(root, name + "_" + stamp);
                int n = 1;
                while (Directory.Exists(copyDir) || File.Exists(copyDir))
                {
                    copyDir = Path.Combine(root, name + "_" + stamp + "_" + n.ToString());
                    n++;
                }
                Directory.CreateDirectory(copyDir);
                File.Copy(file, Path.Combine(copyDir, Path.GetFileName(file)), false);
                if (!string.IsNullOrEmpty(note))
                    File.WriteAllText(Path.Combine(copyDir, NoteFileName), note, new UTF8Encoding(true));
                LastBackup = copyDir;
                Prune(root, name);
                return true;
            }
            catch (Exception ex)
            {
                DialogResult r = MessageBox.Show(owner,
                    RomfsConfig.L("The safety copy could not be made: ", "La copie de sécurité n'a pas pu être faite : ") + ex.Message
                    + "\n\n" + RomfsConfig.L("Save anyway?", "Enregistrer quand même ?"),
                    RomfsConfig.L("Safety backup", "Sauvegarde de sécurité"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                return r == DialogResult.Yes;
            }
        }

        // Date et heure lues dans le nom (« 16/11/2026 20:41:03 »).
        public static string WhenFromStamp(string stamp)
        {
            if (string.IsNullOrEmpty(stamp) || stamp.Length < 19) return stamp ?? "";
            return stamp.Substring(8, 2) + "/" + stamp.Substring(5, 2) + "/" + stamp.Substring(0, 4) + " "
                 + stamp.Substring(11, 2) + ":" + stamp.Substring(14, 2) + ":" + stamp.Substring(17, 2);
        }

        static readonly Regex StampRx = new Regex(
            @"^(?<name>.+)_(?<stamp>\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2})(?<n>_\d+)?(?<ext>\.[^.]*)?$", RegexOptions.IgnoreCase);

        static string ReadNoteFile(string dir)
        {
            try
            {
                string p = Path.Combine(dir, NoteFileName);
                if (File.Exists(p)) return File.ReadAllText(p, Encoding.UTF8);
            }
            catch (Exception) { }
            return "";
        }

        // Toutes les copies d'un dossier (de la plus récente à la plus ancienne), nouvelles (dossiers) et anciennes (fichiers).
        public static List<Entry> List(string root)
        {
            List<Entry> res = new List<Entry>();
            try
            {
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return res;
                foreach (string d in Directory.GetDirectories(root))
                {
                    Match m = StampRx.Match(Path.GetFileName(d));
                    if (!m.Success) continue;
                    string data = null;
                    long best = -1;
                    foreach (string f in Directory.GetFiles(d))
                    {
                        if (string.Equals(Path.GetFileName(f), NoteFileName, StringComparison.OrdinalIgnoreCase)) continue;
                        long len = new FileInfo(f).Length;
                        if (len > best)
                        {
                            best = len;
                            data = f;
                        }
                    }
                    if (data == null) continue;
                    Entry e = new Entry();
                    e.Location = d;
                    e.IsLegacy = false;
                    e.DataFile = data;
                    e.Name = m.Groups["name"].Value;
                    e.Ext = "";
                    e.BaseName = Path.GetFileName(d);
                    e.Stamp = m.Groups["stamp"].Value;
                    e.Size = best;
                    e.Note = ReadNoteFile(d);
                    res.Add(e);
                }
                foreach (string f in Directory.GetFiles(root))
                {
                    Match m = StampRx.Match(Path.GetFileName(f));
                    if (!m.Success) continue;
                    Entry e = new Entry();
                    e.Location = f;
                    e.IsLegacy = true;
                    e.DataFile = f;
                    e.Name = m.Groups["name"].Value;
                    e.Ext = m.Groups["ext"].Value;
                    e.BaseName = e.Name + "_" + m.Groups["stamp"].Value + m.Groups["n"].Value;
                    e.Stamp = m.Groups["stamp"].Value;
                    e.Size = new FileInfo(f).Length;
                    res.Add(e);
                }
                SortEntries(res);
            }
            catch (Exception) { }
            return res;
        }

        // Écrit la note (note.txt à côté de la sauvegarde). Texte vide : le fichier note.txt est supprimé.
        // Une ancienne copie (fichier sans dossier) est d'abord rangée dans son propre dossier.
        public static void WriteNote(Entry e, string text)
        {
            text = text ?? "";
            if (text.Trim().Length == 0)
            {
                if (!e.IsLegacy)
                {
                    string p = Path.Combine(e.Location, NoteFileName);
                    if (File.Exists(p)) File.Delete(p);
                }
                e.Note = "";
                return;
            }
            if (e.IsLegacy) Migrate(e);
            File.WriteAllText(Path.Combine(e.Location, NoteFileName), text, new UTF8Encoding(true));
            e.Note = text;
        }

        static void Migrate(Entry e)
        {
            string parent = Path.GetDirectoryName(e.Location);
            string dir = Path.Combine(parent, e.BaseName);
            int n = 1;
            while (Directory.Exists(dir) || (File.Exists(dir) && !string.Equals(dir, e.Location, StringComparison.OrdinalIgnoreCase)))
            {
                dir = Path.Combine(parent, e.BaseName + "_" + n.ToString());
                n++;
            }
            string tmp = e.Location + ".migrating";
            File.Move(e.Location, tmp);
            Directory.CreateDirectory(dir);
            string dest = Path.Combine(dir, e.Name + e.Ext);
            File.Move(tmp, dest);
            e.Location = dir;
            e.DataFile = dest;
            e.IsLegacy = false;
        }

        public static void Delete(Entry e)
        {
            if (e.IsLegacy) File.Delete(e.Location);
            else Directory.Delete(e.Location, true);
        }

        // Supprime les plus anciennes copies de CE système (nom identique) quand il y en a plus que le maximum choisi.
        static void Prune(string root, string name)
        {
            try
            {
                int max = ThemeSettings.BackupMax;
                if (max < 1) max = 1;
                List<Entry> mine = new List<Entry>();
                foreach (Entry e in List(root))
                {
                    if (string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase)) mine.Add(e);
                }
                for (int i = max; i < mine.Count; i++)
                {
                    try { Delete(mine[i]); } catch (Exception) { }
                }
            }
            catch (Exception) { }
        }
    }
}
