// MHXXSwitchSaveEditor – version française (modifiée). Licence : GNU GPL v3 (voir LICENSE).
// Fichier ajouté lors de la version 33 : écriture sûre des fichiers de sauvegarde.
using System;
using System.IO;

namespace MHXXSaveEditor.Util
{
    // Écrit d'abord un fichier temporaire à côté de la sauvegarde (même dossier, extension .tmp), vérifie qu'il est complet,
    // puis le met à la place de l'ancien fichier d'un seul coup. Si le PC plante pendant l'écriture,
    // l'ancien fichier reste intact (au pire il reste un fichier .tmp à supprimer).
    static class SafeFile
    {
        public static void WriteAllBytes(string path, byte[] data)
        {
            string tmp = path + ".tmp";
            try
            {
                using (FileStream fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    fs.Write(data, 0, data.Length);
                    fs.Flush(true); // force l'écriture réelle sur le disque
                }
                if (new FileInfo(tmp).Length != data.Length)
                    throw new IOException("Incomplete temporary file / fichier temporaire incomplet : " + tmp);

                if (File.Exists(path))
                {
                    try
                    {
                        File.Replace(tmp, path, null);
                    }
                    catch (Exception)
                    {
                        // Certains dossiers (réseau, émulateur...) n'acceptent pas le remplacement : on copie par-dessus.
                        File.Copy(tmp, path, true);
                        File.Delete(tmp);
                    }
                }
                else
                {
                    File.Move(tmp, path);
                }
            }
            catch (Exception)
            {
                try
                {
                    if (File.Exists(tmp)) File.Delete(tmp);
                }
                catch (Exception) { }
                throw;
            }
        }
    }
}
