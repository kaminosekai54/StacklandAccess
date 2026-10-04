# Stackland Access

Mod d'accessibilité pour **Stacklands**, pour les joueurs aveugles : tout passe par le lecteur d'écran (NVDA, JAWS… via Tolk, SAPI en secours) et le clavier.
Le mod parle la langue du jeu : anglais, français, néerlandais, allemand, italien, espagnol, polonais, portugais du Brésil, japonais, coréen, chinois simplifié et traditionnel.

*[English version](README.md)*

## Installation (joueurs)

- **Steam Workshop** : s'abonner à « Stackland Access », puis lancer le jeu.
- **Manuel** : télécharger `StacklandAccess-x.y.z.zip` dans les releases GitHub et le décompresser dans
  `%USERPROFILE%\AppData\LocalLow\sokpop\Stacklands\Mods\` (il doit en résulter un dossier `Mods\StacklandAccess` contenant `StacklandAccess.dll`).

Un lecteur d'écran (NVDA, JAWS…) est conseillé ; sans lui, la voix Windows (SAPI) est utilisée.

## Compilation (développeurs)

1. Compiler le projet (Visual Studio ou `dotnet build`). La compilation copie automatiquement le mod dans
   `%USERPROFILE%\AppData\LocalLow\sokpop\Stacklands\Mods\StacklandAccess` :
   `StacklandAccess.dll`, `manifest.json` et le dossier `native` (Tolk.dll, nvdaControllerClient64.dll, SAAPI64.dll).
2. Lancer le jeu. Au démarrage : « Stackland Access prêt. F1 pour l'aide. »
3. Le chemin du jeu se règle dans `StacklandAccess.csproj.user` (propriété `stacklands`).

## Menus

| Touche | Action |
|---|---|
| Flèches haut / bas, Tab | élément précédent / suivant |
| Début / Fin | premier / dernier élément |
| Entrée | activer (bouton, case, champ de texte) |
| Gauche / droite | régler un curseur (volume…) ou une liste |
| I | description de l'élément (infobulle, carte de l'encyclopédie) |
| R | lire tout le texte de l'écran |
| Échap | revenir (géré par le jeu) |

Les fenêtres (pop-up), les écrans et les cinématiques (fin de lune, événements) sont annoncés automatiquement.
Dans une cinématique, Entrée ou Espace continue.

## Plateau

Les objets du plateau (piles de cartes, paquets, boîtes d'achat, boîte de vente) sont rangés par catégories :
Tout, Personnes, Nourriture, Ressources, Bâtiments, Ennemis, Animaux, Idées, Lieux, Équipement, Paquets et boutique, Autres.

| Touche | Action |
|---|---|
| Gauche / droite | pile précédente / suivante |
| Page préc. / Page suiv. | catégorie précédente / suivante (les catégories vides sont sautées) ; chaque catégorie revient sur la dernière pile visitée |
| Début / Fin | première / dernière pile |
| Haut / bas | cartes de la pile, du bas vers le haut |
| Entrée | prendre la carte (et celles au-dessus), puis Entrée sur la cible : pile, boîte de vente, paquet à acheter |
| Entrée sur un paquet | l'ouvrir (une carte par appui) |
| Entrée sur un paquet à acheter | payer automatiquement avec vos pièces |
| Retour arrière | sortir de la pile la carte sélectionnée et celles au-dessus (sur la carte du bas : la sortir seule) ; en tenant une carte : la poser seule |
| Échap | annuler la prise |
| Suppr (deux fois) | vendre la carte et celles au-dessus |
| C | cliquer la carte (ex. sortir des pièces d'un coffre) |
| I | détails de la carte sous forme de liste (flèches pour parcourir) : prix de vente, valeur de la pile, santé, combat, effets, équipement, action en cours, description. Flèche droite : inventaire (Entrée prend l'objet, puis Entrée sur un villageois pour le lui donner ou Retour arrière pour le retirer) |
| P / B / H / F / G / X / A / O / K / U / J / Y | aller directement aux personnes, à la boutique (paquets, achat, vente, marchands ambulants), aux bâtiments, à la nourriture, aux ressources, aux ennemis, aux animaux, à l'équipement, aux idées, aux lieux, aux autres, à tout ; appuyer encore passe au suivant |
| Entrée sur un marchand ambulant | payer automatiquement (3 pièces par objet) |
| M | la carte en main (celle que vous tenez) et sa pile |
| Ctrl+P | liste des villageois et de leur occupation, inactifs d'abord ; Entrée va sur le villageois |
| Ctrl+Entrée | déplacer la pile sur le plateau avec les flèches (I : ce qui est autour), Entrée ou Échap pour finir |
| F5 | regrouper les ressources et la nourriture identiques en piles pleines |
| R | ressources : pièces, nourriture, cartes… |
| T | lune, temps restant, vitesse |
| Q | quêtes (liste) |
| L | recettes, par pages (gauche / droite) : possibles maintenant, bâtiments de production (Entrée y pose les cartes disponibles), puis une page par groupe d'idées, les possibles d'abord ; Entrée met la recette en place, I décrit le résultat. Les cartes déjà utilisées par une autre fabrication ne sont jamais prises |
| N | historique des messages |
| F1 | aide |
| F2 | répéter le dernier message |
| F3 | activer / couper l'annonce des nouvelles cartes |
| F4 | activer / couper l'empilement automatique (ressources et nourriture identiques inutilisées ; les piles faites à la main ne sont jamais touchées) |
| - / + (ou pavé numérique) | ralentir / accélérer : pause, 0,5, 1, 2, 5 |
| Pavé numérique 0 à 4 | pause, 0,5, 1, 2, 5 directement |
| Espace, Tab, 1, 2, 3 | pause et vitesse (touches du jeu) |

Les lettres suivent les caractères imprimés sur le clavier (AZERTY compris) : Q est bien la touche Q.

Dans les listes (détails, recettes, quêtes, historique, aide) : haut / bas pour parcourir, gauche / droite pour changer de page, Entrée pour utiliser, I pour l'information, Échap pour fermer.

Si l'option du jeu « pause automatique en clavier-souris » (Options > Accessibilité) est activée, le jeu se met en pause tant que vous tenez une carte.

## Annonces automatiques

Nouvelles cartes (regroupées), ennemis, nouvelles idées, combats (début, chaque coup avec les points de vie restants, fin), ennemis vaincus, personnes perdues,
petit carillon montant quand un villageois devient libre (fichier `Sounds/villager_free.wav`, remplaçable), portail étrange, recette qui démarre après un dépôt (avec sa durée) et quand elle se termine, effets de statut (empoisonné, malade, en feu…), changement de vue (DLC Cités), quêtes terminées, changement de vitesse,
alerte avant la fin de la lune, dépassement du nombre de cartes, textes des cinématiques.

Réglages dans **Options > Mods > Stackland Access** : annonces des nouvelles cartes, des combats, de chaque coup,
des recettes, délai de l'alerte de fin de lune, caméra qui suit la sélection, empilement automatique, tri des piles par nom, son quand un villageois devient libre.

## Traduction

Les textes du mod sont dans `StacklandAccess_strings.tsv` (installé avec le mod), une colonne par langue, nommée comme dans le jeu
(`German`, `Brazilian Portuguese`, `Chinese (Simplified)`…). Les traductions autres que le français et l'anglais ont été faites par IA :
corrections bienvenues, il suffit de modifier la cellule. Les lignes en `_one` sont le singulier, les lignes en `_few` le pluriel polonais de 2 à 4.

## Publication

Voir `RELEASE.md` (Workshop et GitHub) et `WORKSHOP.md` (texte de présentation et tags).

## Licences

Code du mod : licence MIT (`LICENSE`). Tolk : LGPL 3 ; NVDA Controller Client : LGPL 2.1 ; son `villager_free.wav` : Kenney, CC0. Détails dans `THIRD_PARTY.md`.

## Notes techniques

- Les erreurs du mod sont écrites dans `Player.log` avec le préfixe `[StacklandAccess]`. Au démarrage, le mod vérifie que les parties internes du jeu qu'il utilise existent toujours et prévient sinon.
- Cible `netstandard2.1` comme le modèle officiel (Unity/Mono ne charge pas `net9.0`).
- Les touches du jeu qui utilisent les touches du mod (flèches pour la caméra, etc.) sont désactivées par le mod au démarrage ;
  la liste complète des raccourcis du jeu est écrite dans le journal (`Player.log`).
- Le code décompilé du jeu (`stackland_uncompiled`) ne doit pas être publié (règle du wiki de modding).
