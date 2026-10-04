# Publier une version / Releasing

## 1. Préparer

1. Mettre à jour `version` dans `manifest.json` et ajouter une section dans `CHANGELOG.md`.
2. Dans PowerShell, à la racine du projet : `powershell -ExecutionPolicy Bypass -File tools\package.ps1`
   → `release\StacklandAccess-<version>.zip` (et le dossier `release\StacklandAccess`).
3. `dotnet build` (sans option) installe aussi la version dans le dossier Mods du jeu pour un dernier test.

## 2. Steam Workshop

L'envoi se fait **depuis le jeu**, qui envoie tout le contenu du dossier installé `Mods\StacklandAccess` (créé par `dotnet build`). Le mod lit cet écran (les tags annoncent « sélectionné / non sélectionné »).

1. Menu principal > **Mods** > **Stackland Access** > **Upload**.
2. Première fois : une fenêtre propose **Create Item** (accord Steam Workshop) → la valider. Le jeu écrit `workshop.txt` (identifiant de l'objet) dans le dossier du mod : **le copier dans le dossier du projet** ; sans lui, une mise à jour créerait un deuxième objet.
3. **Select Tags** : activer **Quality of Life** et **Gameplay**, puis Back.
4. Laisser « Set preview image: Yes » (utilise `icon.png`), écrire les notes de version dans le champ de notes, puis **Upload** > **Upload**. Le `config.json` personnel est mis de côté automatiquement pendant l'envoi.
5. « Upload finished » : la page Steam s'ouvre. Y coller la description de `WORKSHOP.md` et passer la visibilité en **Public** (l'objet est créé caché/privé).
6. Mises à jour : changer la version, `dotnet build`, vérifier que `workshop.txt` est bien dans `Mods\StacklandAccess`, puis Upload.

Le dossier ne doit contenir que les fichiers du mod (pas de `bin`, `obj`, ni code décompilé) : c'est le cas du dossier installé.

## 3. GitHub

Première fois (le dépôt peut être public : le code décompilé est exclu par `.gitignore`) :

```powershell
cd C:\Users\jtmhd\source\repos\StacklandAccess
git status          # vérifier qu'aucun fichier de stackland_uncompiled n'apparaît
git add .
git commit -m "Stackland Access 0.5.0"
gh repo create StacklandAccess --public --source . --push
```

Sans l'outil `gh` : créer un dépôt vide sur github.com, puis `git remote add origin https://github.com/<compte>/StacklandAccess.git` et `git push -u origin main`.

Publier la version :

```powershell
git tag v0.5.0
git push origin v0.5.0
gh release create v0.5.0 release\StacklandAccess-0.5.0.zip --title "Stackland Access 0.5.0" --notes-file CHANGELOG.md
```

(ou sur github.com : Releases > Draft a new release, choisir le tag, joindre le zip.)
