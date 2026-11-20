// MHXXSwitchSaveEditor – version française (modifiée). Licence : GNU GPL v3 (voir LICENSE).
// Fichier ajouté lors de la V2 (coffre graphique), le 14 novembre 2026.
using System;
using System.IO;
using System.Windows.Forms;

namespace MHXXSaveEditor.Util
{
    // Retrouve (et mémorise) le dossier ROMFS de MHGU de la personne.
    // Les fichiers du jeu ne sont jamais copiés : le programme les lit sur place.
    static class RomfsConfig
    {
        static string nativeRoot;
        static bool loaded;

        static string ConfigPath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MHXXSaveEditor", "romfs.txt"); }
        }

        // Petit utilitaire : texte français ou anglais selon la langue choisie.
        public static string L(string en, string fr)
        {
            return Loc.French ? fr : en;
        }

        // Dossier « nativeNX » ou null si on ne l'a pas encore trouvé.
        public static string NativeRoot
        {
            get
            {
                if (!loaded)
                {
                    loaded = true;
                    try
                    {
                        if (File.Exists(ConfigPath))
                            nativeRoot = Normalize(File.ReadAllText(ConfigPath).Trim());
                    }
                    catch (Exception) { }
                }
                return nativeRoot;
            }
        }

        // Accepte le dossier ROMFS, le dossier nativeNX, ou le dossier qui contient le dossier RomFS.
        public static string Normalize(string p)
        {
            if (string.IsNullOrEmpty(p)) return null;
            try
            {
                if (!Directory.Exists(p)) return null;
                string direct = Path.Combine(p, "nativeNX");
                if (Directory.Exists(direct)) return direct;
                string name = Path.GetFileName(p.TrimEnd('\\', '/'));
                if (string.Equals(name, "nativeNX", StringComparison.OrdinalIgnoreCase)) return p;
                foreach (string sub in Directory.GetDirectories(p))
                {
                    string c = Path.Combine(sub, "nativeNX");
                    if (Directory.Exists(c)) return c;
                }
            }
            catch (Exception) { }
            return null;
        }

        static bool IsValid(string root)
        {
            if (root == null) return false;
            return File.Exists(Path.Combine(root, "table", "itemData.itm"));
        }

        // Chemin complet d'un fichier relatif à nativeNX (ex. "table\\itemData.itm").
        public static string PathOf(string relative)
        {
            string root = NativeRoot;
            if (root == null) return null;
            return Path.Combine(root, relative);
        }

        // Vrai si un dossier ROMFS valide est déjà mémorisé.
        public static bool HasFolder
        {
            get { return IsValid(NativeRoot); }
        }

        // S'assure que le dossier ROMFS est connu ; sinon le demande à la personne.
        public static bool Ensure(IWin32Window owner)
        {
            if (IsValid(NativeRoot)) return true;
            return Choose(owner);
        }

        // Demande un dossier ROMFS. Le dossier mémorisé ne change QUE si la personne en choisit un valable :
        // si elle annule, tout reste comme avant (les icônes déjà réglées ne sont pas perdues).
        public static bool Choose(IWin32Window owner)
        {
            while (true)
            {
                using (FolderBrowserDialog dlg = new FolderBrowserDialog())
                {
                    dlg.Description = L(
                        "Choose the ROMFS folder of Monster Hunter Generations Ultimate (the folder that contains \"nativeNX\").",
                        "Choisissez le dossier ROMFS de Monster Hunter Generations Ultimate (le dossier qui contient « nativeNX »).");
                    dlg.ShowNewFolderButton = false;
                    if (dlg.ShowDialog(owner) != DialogResult.OK) return false;
                    string root = Normalize(dlg.SelectedPath);
                    if (IsValid(root))
                    {
                        Set(root);
                        return true;
                    }
                    DialogResult r = MessageBox.Show(owner,
                        L("This folder does not look like the ROMFS (\"nativeNX\\table\\itemData.itm\" was not found). Try another folder?",
                          "Ce dossier ne ressemble pas au ROMFS (« nativeNX\\table\\itemData.itm » est introuvable). Essayer un autre dossier ?"),
                        "ROMFS", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (r != DialogResult.Yes) return false;
                }
            }
        }

        // Mémorise un dossier nativeNX (déjà vérifié).
        public static void Set(string root)
        {
            nativeRoot = root;
            loaded = true;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
                File.WriteAllText(ConfigPath, root);
            }
            catch (Exception) { }
        }

        // Pour « Oublier le dossier ROMFS enregistré » : on ne l'appelle que si la personne le demande.
        public static void Forget()
        {
            nativeRoot = null;
            loaded = true;
            try { if (File.Exists(ConfigPath)) File.Delete(ConfigPath); } catch (Exception) { }
        }
    }
}
