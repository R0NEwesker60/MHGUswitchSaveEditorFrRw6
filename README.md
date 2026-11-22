# MHGUswitchSaveEditorFrRw6 (V2) – éditeur de sauvegarde Monster Hunter Generations Ultimate

> 🇫🇷 Français ci-dessous · 🇬🇧 [English version further down](#-english)

Éditeur de sauvegarde pour **Monster Hunter Generations Ultimate (MHGU, Switch)**, avec une **interface entièrement
en français** (ou en anglais, au choix) et les **noms français officiels du jeu**. Il ouvre aussi les sauvegardes
**MHXX** (3DS et Switch). C'est un **fork modifié** : voir [les crédits](#crédits-et-licence).

⚠️ **Faites toujours une copie de votre fichier `system` avant de l'ouvrir.** Le programme fait lui-même une copie de sécurité
avant chaque enregistrement, mais ce programme peut abîmer une sauvegarde (voir l'avertissement d'origine plus bas).

## Nouveautés de la V2 (version décrite : V 02.55)

**Coffres et recherche**
- Trois **coffres graphiques** (objets, équipement, équipement Palico) avec les vraies icônes du jeu, en grille de 100 cases par page.
- **Icônes HD** en option (pack de textures), lues sur place dans le dossier de votre pack HD (vous le choisissez une fois, menu Affichage) : rien n'est copié.
- **Recherche avancée** : par numéro et par noms français et anglais en même temps ; filtres par catégorie et par rareté.
- **Équipement modifiable depuis la grille** (double-clic : choix du type puis du nom, avec recherche en direct).
- Noms français de MHGU pour les objets, joyaux, armes, armures, talents, kinsectes et équipement des Palicos.

**Sauvegardes et sécurité**
- **Écriture sûre** : le fichier est d'abord écrit en temporaire, vérifié, puis mis à la place de l'ancien. Un plantage ne l'abîme pas.
- **Copies de sécurité automatiques** avant chaque enregistrement, rangées en dossiers datés, avec des **notes** et une **restauration** depuis une liste.
- **Avertissement** avant de quitter ou de charger un autre fichier avec des modifications non enregistrées.

**Copie automatique, sauvegardes manuelles et injection** *(pensé pour la version PC, par exemple MHGU Recomp ; marche avec n'importe quel dossier de sauvegarde)*
- **Copie automatique à la fermeture du jeu** : on choisit le programme du jeu (son `.exe`) et le dossier à copier (`save-0`). **7 secondes** après la fermeture du jeu, **tout le contenu du dossier** (sauvegardes, backup du jeu, mods…) est copié dans un dossier de copies datées, **10 copies** par défaut (réglable). Pas de copie si le jeu a tourné **moins de 15 secondes**, ni si rien n'a changé depuis la dernière copie (comparaison **octet par octet**). Désactivée par défaut.
- Quand elle est activée, la croix de la fenêtre **réduit l'éditeur dans la zone de notification** (icône couronne des 18 monstres EX) : double-clic pour rouvrir, clic droit > *Quitter* pour l'arrêter. **Une seule fenêtre** à la fois : relancer le programme réaffiche la fenêtre existante. Notification « copie faite » (désactivable dans *Options*).
- **Lancement au démarrage de Windows**, **sans fenêtre** (zone de notification seulement), avec un bouton pour changer l'emplacement du programme lancé. Sans droits administrateur.
- **Sauvegardes manuelles** du dossier `save-0` (choisi une fois, mémorisé) : une **fenêtre dédiée** avec **notes** (fichier `Note.txt` rangé dans la copie, jamais envoyé au jeu), **favoris** ★ (jamais supprimés automatiquement), limite de 10 copies (activable/désactivable et modifiable), **clic droit** sur une copie (favori, note, supprimer, injecter, ouvrir son dossier).
- **Injection** d'une copie (manuelle, automatique, transfert, avant enregistrement…) dans `save-0` : une copie « dossier » écrase **seulement les fichiers de la copie**, les autres sont gardés ; une copie « un seul fichier » ne remplace **que ce fichier** (avec avertissement). Refusée si le jeu est ouvert. Juste avant, les fichiers remplacés sont copiés dans un dossier **« Avant injection »** (10 par défaut) : on peut revenir en arrière.
- **Déchargement de la sauvegarde après « Enregistrer »** (option, activée par défaut, et aussi quand la fenêtre part dans la zone de notification) : l'éditeur revient comme au démarrage, pour qu'une vieille version gardée ouverte n'écrase jamais la progression du jeu.
- Choix des dossiers avec la **boîte de dialogue de l'Explorateur Windows** (accès rapide, recherche). Cinq dossiers de copies côte à côte (normales, transferts, automatiques, manuelles, avant injection), noms modifiables.
- Couronnes de chasseur **redessinées d'après les captures du jeu**.

**Personnages et fichiers**
- **Gestion des emplacements** (MHGU Switch) : supprimer, échanger et dupliquer des personnages.
- **Transfert entre deux sauvegardes (A / B)** : échanger, copier ou couper un personnage d'une sauvegarde à l'autre, avec copies de sécurité et note automatique.
- **Fichiers récents, fichiers favoris et trois boutons de destination** renommables : plus besoin de rechercher le fichier à chaque fois.
- **Glisser-déposer** d'un fichier de sauvegarde sur la fenêtre pour l'ouvrir.
- Boutons d'emplacement dans l'onglet **Général**, colorés selon le **rang de chasseur**, avec les **couronnes de chasseur** (monstres EX) affichées à côté des pseudos ; le format de la sauvegarde (3DS, MHXX Switch, MHGU Switch) est affiché.

**Confort**
- **Thème sombre**, fenêtre redimensionnable, zoom réglable, couleurs sur les menus et les onglets (désactivables).
- Un bouton **Enregistrer** juste à droite de *Fichier* ; raccourcis Ctrl+O et Ctrl+S.
- **Transmogrification des armes** en option (codes de triche).
- Menu **Language / Langue** (le programme redémarre pour appliquer la langue).

**Ce qui n'est pas là (pour l'instant)**
- L'**édition** des couronnes de chasseur (monstres EX) : elles sont seulement affichées pour l'instant.
- La **conversion entre MHXX (3DS) et MHGU (Switch)** : elle n'existe pas dans cet éditeur.
- Les couleurs du rang de chasseur viennent de seuils non vérifiés dans le jeu.
- La copie automatique, la zone de notification et le lancement au démarrage fonctionnent sous **Windows** (version PC) ; les copies automatiques et l'injection n'agissent que sur le dossier que vous choisissez.

## Utilisation

1. Extraire la sauvegarde de MHGU avec votre gestionnaire de sauvegardes (Checkpoint sur Switch, ou le dossier de sauvegarde de votre version PC ou de votre émulateur).
2. **Copier** le fichier `system` (copie de sécurité).
3. Ouvrir `system` : *Fichier > Charger*, ou le **glisser sur la fenêtre**. Modifier, puis cliquer sur **Enregistrer**.
4. Remettre `system` (et `system_backup`) à leur place et réimporter la sauvegarde. Ne pas renommer ces fichiers.

*Copie automatique (facultatif)* : menu **Sauvegarde de sécurité > Copie automatique à la fermeture du jeu…**, cocher *Activer*, choisir le programme du jeu (`.exe`) puis le dossier `save-0`. Le reste est automatique. Pour des copies à la main, des notes, des favoris ou une injection : **Sauvegarde de sécurité > Sauvegardes manuelles et injection…**

*Icônes du jeu (facultatif)* : sans dossier ROMFS, l'éditeur marche avec des listes. Pour les grilles à icônes, l'onglet **Général** affiche
un message jaune « Interface graphique non activée » avec un bouton **Comment l'activer ?**, qui explique quel dossier choisir.

## Compiler

Visual Studio, charge de travail « Développement de bureau .NET » avec **.NET Framework 4.8** : ouvrir `MHXXSaveEditor.sln`,
choisir **Release**, puis *Générer > Générer la solution*. Le programme est dans `MHXXSaveEditor\bin\Release\MHGUsaveEditorFrRw6.exe`.

## Crédits et licence

Ce dépôt est un **fork modifié**. Je ne suis pas l'auteur de l'éditeur :

1. **mineminemine** – [MHXXSaveEditor](https://github.com/mineminemine/MHXXSaveEditor/) (éditeur 3DS d'origine) ;
2. **Dawnshifter** – MHXXSwitchSaveEditor 420.69 (prise en charge des sauvegardes Switch) ;
3. **R0NEwesker60 (RW6)** – version française et V2 (tout ce qui est listé ci-dessus).

Merci aussi à Ukee (GBAtemp), APM (éditeur MHX/MHGen), Seth VanHeulen (structure du fichier de sauvegarde) et aux sujets de modification
hexadécimale MHX/MHGen/MHXX de GBAtemp.

Licence **GNU General Public License v3.0** (voir [`LICENSE`](LICENSE)), comme le projet d'origine. Les fichiers ajoutés ou modifiés sont
listés dans [`NOTICE.md`](NOTICE.md) et portent une mention de modification. Le code source complet est fourni ici.

Projet **non officiel**, sans lien avec Capcom ni Nintendo. *Monster Hunter* appartient à Capcom. Ce dépôt ne contient aucun fichier du jeu, aucune sauvegarde ni ROM.

Bugs et retours : ouvrir une **issue** sur ce dépôt.

---

# 🇬🇧 English

Save editor for **Monster Hunter Generations Ultimate (MHGU, Switch)**, with a **fully French interface** (or English, your choice) and the
**official French names of the game**. It also opens **MHXX** saves (3DS and Switch). This is a **modified fork**: see [credits](#credits-and-license).

⚠️ **Always make a copy of your `system` file before opening it.** The program makes its own safety copy before every save, but it can still
damage a save (see the original warning).

## What's new in V2 (version described: V 02.55)

**Chests and search**
- Three **graphic chests** (items, equipment, Palico equipment) with the game's real icons, in grids of 100 slots per page.
- Optional **HD icons** (texture pack), read in place from your HD pack folder (you choose it once, View menu): nothing is copied.
- **Advanced search**: by ID and by French and English names at the same time; filters by category and rarity.
- **Equipment editable straight from the grid** (double-click: pick the type, then the name, with live search).
- French names of MHGU for items, decorations, weapons, armor, skills, insect glaives and Palico equipment.

**Saves and safety**
- **Safe writing**: the file is written to a temporary file first, checked, then swapped in. A crash cannot corrupt it.
- **Automatic safety backups** before every save, kept in dated folders, with **notes** and **restore** from a list.
- **Warning** before quitting or loading another file with unsaved changes.

**Automatic copy, manual backups and injection** *(made for the PC version, for example MHGU Recomp; works with any save folder)*
- **Automatic copy when the game closes**: you pick the game's program (its `.exe`) and the folder to copy (`save-0`). **7 seconds** after the game closes, **the whole content of the folder** (saves, the game's backup, mods...) is copied into a dated backup folder, **10 copies** by default (adjustable). No copy if the game ran **less than 15 seconds**, or if nothing changed since the last copy (**byte-for-byte** comparison). Off by default.
- When it is on, the window's cross **sends the editor to the notification area** (crown icon of the 18 EX monsters): double-click to reopen, right-click > *Quit* to stop it. **Only one window** at a time: starting the program again brings the existing window back. A "copy done" notification (can be turned off in *Options*).
- **Start with Windows**, **with no window** (notification area only), with a button to change the location of the program that is started. No administrator rights needed.
- **Manual backups** of the `save-0` folder (chosen once, remembered): a **dedicated window** with **notes** (a `Note.txt` file kept inside the copy, never sent to the game), **favorites** ★ (never deleted automatically), a limit of 10 copies (can be turned on/off and changed), and a **right-click menu** on a copy (favorite, note, delete, inject, open its folder).
- **Injection** of a copy (manual, automatic, transfer, before-save...) into `save-0`: a "folder" copy overwrites **only the files of the copy**, the other files are kept; a "single file" copy replaces **only that file** (with a warning). Refused if the game is open. Just before, the replaced files are copied to a **"Before injection"** folder (10 by default) so you can go back.
- **Unloading the save after "Save"** (option, on by default, and also when the window goes into the notification area): the editor goes back to its start state, so an old version left open can never overwrite the game's progress.
- Folders are chosen with the **Windows Explorer dialog** (quick access, search). Five backup folders side by side (normal, transfers, automatic, manual, before injection), names can be changed.
- Hunter crowns **redrawn from game screenshots**.

**Characters and files**
- **Slot management** (MHGU Switch): delete, swap and duplicate characters.
- **Transfer between two saves (A / B)**: swap, copy or cut a character from one save to another, with safety copies and an automatic note.
- **Recent files, favorite files and three destination buttons** you can rename: no more hunting for the file every time.
- **Drag and drop** a save file onto the window to open it.
- Slot buttons on the **General** tab, coloured by **hunter rank**, with the **hunter crowns** (EX monsters) shown next to the names; the save format (3DS, MHXX Switch, MHGU Switch) is shown.

**Comfort**
- **Dark theme**, resizable window, adjustable zoom, colours on menus and tabs (can be turned off).
- A **Save** button right next to *File*; Ctrl+O and Ctrl+S shortcuts.
- Optional **weapon transmogrification** (cheat codes).
- **Language / Langue** menu (the program restarts to apply the language).

**Not there (yet)**
- **Editing** hunter crowns (EX monsters): for now they are only displayed.
- **Conversion between MHXX (3DS) and MHGU (Switch)**: not available in this editor.
- Hunter-rank colours come from thresholds that have not been verified in the game.
- Automatic copy, the notification area and start with Windows work on **Windows** (PC version); automatic copies and injection only act on the folder you choose.

## Usage

1. Extract the MHGU save with your save manager (Checkpoint on Switch, or the save folder of your PC version or emulator).
2. **Copy** the `system` file (safety copy).
3. Open `system`: *File > Load*, or **drag it onto the window**. Edit, then click **Save**.
4. Put `system` (and `system_backup`) back and re-import the save. Do not rename these files.

*Automatic copy (optional)*: menu **Safety backup > Automatic copy when the game closes...**, tick *Turn on*, pick the game's program (`.exe`) then the `save-0` folder. The rest is automatic. For hand-made copies, notes, favorites or injection: **Safety backup > Manual backups and injection...**

*Game icons (optional)*: without a ROMFS folder the editor works with lists. For the icon grids, the **General** tab shows a yellow
"Graphic interface not enabled" message with a **How to turn it on?** button that explains which folder to pick.

## Build

Visual Studio with the ".NET desktop development" workload and **.NET Framework 4.8**: open `MHXXSaveEditor.sln`, choose **Release**,
then *Build > Build Solution*. The program is in `MHXXSaveEditor\bin\Release\MHGUsaveEditorFrRw6.exe`.

## Credits and license

This repository is a **modified fork**. I am not the author of the editor:

1. **mineminemine** – [MHXXSaveEditor](https://github.com/mineminemine/MHXXSaveEditor/) (original 3DS editor);
2. **Dawnshifter** – MHXXSwitchSaveEditor 420.69 (Switch save support);
3. **R0NEwesker60 (RW6)** – French version and V2 (everything listed above).

Thanks also to Ukee (GBAtemp), APM (MHX/MHGen Save Editor), Seth VanHeulen (save file structure) and the MHX/MHGen/MHXX hex editing threads on GBAtemp.

Licensed under the **GNU General Public License v3.0** (see [`LICENSE`](LICENSE)), like the original project. Added and modified files are listed in
[`NOTICE.md`](NOTICE.md) and carry a modification notice. The full source code is provided here.

**Unofficial** project, not affiliated with Capcom or Nintendo. *Monster Hunter* belongs to Capcom. This repository contains no game files, no save and no ROM.

Bugs and feedback: open an **issue** on this repository.
<img width="1397" height="1438" alt="Capture d&#39;écran 2026-11-22 110224" src="https://github.com/user-attachments/assets/52e6d3a9-29ed-443e-8ed5-54cbe28d7bf9" />
<img width="1400" height="1439" alt="Capture d&#39;écran 2026-11-22 110232" src="https://github.com/user-attachments/assets/1fc148b9-c49e-41f8-8206-e92dc0ba66b7" />
<img width="1401" height="1438" alt="Capture d&#39;écran 2026-11-22 110239" src="https://github.com/user-attachments/assets/66ced316-e211-4dab-b449-305400373999" />
<img width="1399" height="1439" alt="Capture d&#39;écran 2026-11-22 110249" src="https://github.com/user-attachments/assets/0c4864da-67bb-4ebf-b2cb-4bfc348bf342" />
<img width="1399" height="1437" alt="Capture d&#39;écran 2026-11-22 110253" src="https://github.com/user-attachments/assets/46ab3b56-04f7-442f-ac3c-af78485b9f6a" />
<img width="1396" height="1437" alt="Capture d&#39;écran 2026-11-22 110405" src="https://github.com/user-attachments/assets/4b5b976d-a033-40df-9c18-1080df4c49ea" />
<img width="999" height="393" alt="Capture d&#39;écran 2026-11-22 110418" src="https://github.com/user-attachments/assets/f79965d0-1691-4efc-a151-d14f3af0088b" />
<img width="1163" height="835" alt="Capture d&#39;écran 2026-11-22 110427" src="https://github.com/user-attachments/assets/862f78ce-666e-4b2e-a845-77d765ee5aee" />
<img width="143" height="47" alt="Capture d&#39;écran 2026-11-22 110347" src="https://github.com/user-attachments/assets/189b9cd1-7d88-4017-b2af-4e8a14a5e240" />
<img width="250" height="97" alt="Capture d&#39;écran 2026-11-22 110352" src="https://github.com/user-attachments/assets/1e020155-06fd-483c-919c-546ea6f4c521" />
