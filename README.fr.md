# Stackland Access

**Stackland Access** rend [Stacklands](https://store.steampowered.com/app/1948280/Stacklands/) jouable pour les personnes aveugles et malvoyantes.
On joue uniquement au clavier, et tout est lu par votre lecteur d'écran (NVDA, JAWS…). Sans lecteur d'écran, la voix de Windows est utilisée.

*[English version](README.md)*

- **Steam Workshop :** https://steamcommunity.com/sharedfiles/filedetails/?id=3813252816
- **Site de la liste des cartes :** https://kaminosekai54.github.io/StacklandAccess/ (toutes les cartes, recettes, paquets et butins, avec recherche, adapté aux lecteurs d'écran)
- **Version actuelle :** 0.6.0 (voir [CHANGELOG.md](CHANGELOG.md))

## État du projet

- Le jeu de base est entièrement jouable : menus, plateau, achat et vente, recettes, combats, fin de lune.
- **Multijoueur :** fonctionne avec le mod [Stacklands Together](https://steamcommunity.com/sharedfiles/filedetails/?id=3778278988) (voir [Multijoueur](#multijoueur)). Pas encore testé en vraie partie.
- **Le contenu des DLC (par exemple Cités) n'a pas encore été testé.** Il peut fonctionner en partie, mais des annonces peuvent manquer ou des problèmes apparaître. Vos retours sont les bienvenus.
- Les textes anglais et français sont écrits à la main. Les autres langues ont été traduites par IA et peuvent contenir des erreurs.

## Installation

**Avec Steam (conseillé) :** ouvrez la [page Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3813252816), cliquez sur S'abonner, puis lancez le jeu.

**À la main :**
1. Téléchargez `StacklandAccess-x.y.z.zip` dans la page Releases de GitHub.
2. Décompressez-le dans `%USERPROFILE%\AppData\LocalLow\sokpop\Stacklands\Mods\`.
3. Vérifiez que le fichier `Mods\StacklandAccess\StacklandAccess.dll` existe.

Au démarrage du jeu, vous entendez « Stackland Access prêt. F1 pour l'aide. »

La première fois, une fenêtre du jeu demande si vous voulez la narration. « Non » coupe tout ce que le mod dit et joue (pour un joueur voyant) ; Ctrl+F2 la remet.

## Premiers pas

1. Dans le menu principal, utilisez les flèches haut et bas et Entrée pour lancer une partie.
2. Sur le plateau, gauche et droite parcourent les cartes, Page préc. / Page suiv. changent de catégorie.
3. Entrée sur une carte la prend ; allez sur une autre carte et appuyez de nouveau sur Entrée pour la poser dessus. C'est ainsi que tout se fabrique dans Stacklands.
4. L donne les recettes possibles maintenant ; Entrée laisse le mod la mettre en place.
5. F1 à tout moment donne la liste des touches.

## Commandes

Les lettres suivent les caractères imprimés sur le clavier : elles fonctionnent aussi en AZERTY.

Toutes les touches ci-dessous se changent dans les options (F7), sauf les flèches, Entrée, Échap, Début, Fin, Page préc., Page suiv. et Tab. Cette page donne les touches par défaut.

### Partout

- **F1** : aide (liste des touches).
- **F2** : répéter le dernier message.
- **F3** : activer ou couper l'annonce des nouvelles cartes.
- **F4** : activer ou couper l'empilement automatique.
- **F6** : créer une page web avec toutes les cartes du jeu et l'ouvrir dans votre navigateur (voir [Liste des cartes](#liste-des-cartes)).
- **F7** : options du mod (fonctions activées ou non, touches).
- **Ctrl+F2** : narration activée ou non.
- **F8** : panneau multijoueur (seulement avec le mod Stacklands Together).

### Menus

- **Flèches haut / bas** ou **Tab** : élément précédent / suivant.
- **Début / Fin** : premier / dernier élément.
- **Entrée** : activer le bouton, la case ou le champ de texte.
- **Flèches gauche / droite** : régler un curseur (volume…) ou un choix.
- **I** : description de l'élément.
- **R** : lire tout le texte de l'écran.
- **Échap** : revenir.
- **Entrée** ou **Espace** dans une cinématique : continuer.

### Plateau : se déplacer

- **Flèches gauche / droite** : pile précédente / suivante dans la catégorie.
- **Page préc. / Page suiv.** : catégorie précédente / suivante. Chaque catégorie revient sur la dernière pile visitée.
- **Début / Fin** : première / dernière pile de la catégorie.
- **Flèches haut / bas** : cartes de la pile, du bas vers le haut.

### Plateau : aller directement à une catégorie

Appuyer de nouveau sur la lettre passe à la pile suivante de cette catégorie.

- **P** : personnes et villageois.
- **B** : boutique (paquets, boîtes d'achat, boîte de vente, marchands ambulants).
- **H** : bâtiments.
- **F** : nourriture.
- **G** : ressources.
- **X** : ennemis.
- **A** : animaux.
- **O** : équipement.
- **K** : idées.
- **U** : lieux (dont le portail étrange).
- **J** : autres cartes.
- **Y** : tout.
- **M** : la carte que vous tenez.

### Plateau : cartes

- **Entrée** : prendre la carte sélectionnée et celles au-dessus.
- **Entrée** en tenant des cartes : les poser sur la pile sélectionnée, la boîte de vente, un paquet à acheter ou un villageois (pour l'équiper).
- **Échap** en tenant des cartes : annuler.
- **Retour arrière** : sortir de la pile la carte sélectionnée et celles au-dessus. Sur la carte du bas, seule cette carte sort.
- **Retour arrière** en tenant des cartes : les poser seules sur le plateau.
- **Suppr**, deux fois : vendre la carte sélectionnée et celles au-dessus.
- **C** : cliquer la carte (par exemple sortir des pièces d'un coffre).
- **Ctrl+Entrée** : déplacer la pile sur le plateau avec les flèches.
- **I** pendant le déplacement : ce qui se trouve autour.
- **E** pendant le déplacement : où sont les cartes reliées à la pile.
- **Entrée** ou **Échap** pendant le déplacement : terminer.
- **F5** : regrouper toutes les cartes identiques inutilisées en piles pleines. Les cartes d'une fabrication en cours, et celles qui fabriquent quelque chose une fois empilées ensemble, ne bougent pas.
- **Ctrl+F5** : pareil que F5, seulement pour la catégorie que vous parcourez.

### Plateau : boutique

- **Entrée** sur un paquet : l'ouvrir, une carte par appui.
- **Entrée** sur un paquet à acheter : payer automatiquement avec vos pièces.
- **Entrée** sur un marchand ambulant : payer automatiquement.

### Plateau : informations

- **I** : détails de la carte sous forme de liste (prix de vente, santé, combat, effets, action en cours, description).
- **Flèche droite** dans les détails d'un villageois : son inventaire et son équipement.
- **R** : ressources (pièces, nourriture, nombre de cartes…).
- **T** : lune, temps restant et vitesse.
- **Q** : liste des quêtes.
- **L** : liste des recettes.
- **N** : historique des messages.
- **Ctrl+P** : liste des villageois et de leur occupation, les inactifs d'abord.
- **E** : connexions de la pile (câbles électriques, égouts, flèches de transport) : ce qu'elles lui apportent et où sont les cartes reliées. Entrée sur une connexion libre liste les cartes auxquelles la relier, les plus proches d'abord ; Entrée sur une connexion reliée la débranche. Flèche droite : toutes les connexions du plateau, puis les cartes qui ont un problème d'énergie ou d'égout.

### Listes (détails, recettes, quêtes, historique, villageois, aide)

- **Flèches haut / bas** : ligne précédente / suivante.
- **Flèches gauche / droite** : page précédente / suivante.
- **Entrée** : utiliser la ligne (mettre une recette en place, aller sur un villageois, prendre un objet…).
- **I** : plus d'informations sur la ligne.
- **Échap** : fermer la liste.

La liste des recettes (L) a plusieurs pages :
1. Recettes possibles maintenant.
2. Bâtiments de production (scierie, briqueterie, composteur, champ). Entrée pose les cartes disponibles sur le bâtiment.
3. Une page par groupe d'idées, les possibles d'abord.

Le mod ne prend jamais des cartes déjà utilisées par une autre fabrication.

### Vitesse du jeu

- **Moins / Plus** (aussi au pavé numérique) : ralentir / accélérer (pause, 0,5, 1, 2, 5).
- **Pavé numérique 0** : pause.
- **Pavé numérique 1 à 4** : vitesse 0,5, 1, 2 ou 5.
- **Espace**, **Tab**, **1**, **2**, **3** : touches de pause et de vitesse du jeu lui-même.

Avec NVDA, gardez le Verr. Num activé pour utiliser le pavé numérique.

## Liste des cartes

Appuyez sur **F6** (dans un menu ou sur le plateau) pour créer `Stacklands - cartes.html` dans votre dossier Documents et l'ouvrir dans votre navigateur. La page est fabriquée à partir de votre propre jeu, dans sa langue :

- un champ de recherche (nom de carte, description, recette ou ingrédient) et un filtre par catégorie ;
- une grille de cartes dessinées comme dans le jeu (couleurs et icône) ; chacune donne ses informations et comment l'obtenir (recette, paquet ou butin), lu par le lecteur d'écran avec le nom de la carte, ou affiché au survol de la souris ;
- Entrée ou un clic ouvre toutes ses informations : catégorie, prix de vente, nourriture, combat, description, comment la fabriquer, paquets, où on la trouve, recettes qui l'utilisent, ce qu'elle donne ;
- « Seulement les cartes découvertes » (coché par défaut) cache les cartes et idées que vous n'avez pas encore trouvées.

- les paquets (catégorie Paquets) : prix, où ils sont vendus, comment les débloquer, valeur moyenne, et toutes les cartes qu'ils peuvent donner avec leur chance par tirage ;
- des filtres : catégorie, type de carte, contenu (jeu de base, île, forêt sombre, esprits, Cités…), obtention (recette, paquet, butin) ; tri par nom, prix de vente, nourriture ou attaque.

Appuyez de nouveau sur F6 après de nouvelles découvertes pour la mettre à jour.

**Maj+F6** fabrique la même page en site public, dans toutes les langues du jeu et sans données de votre partie, dans `Documents\Stacklands cards site`. `tools\update-site.ps1` la copie dans `docs/` pour GitHub Pages.

## Multijoueur

Avec le mod [Stacklands Together](https://steamcommunity.com/sharedfiles/filedetails/?id=3778278988) installé, appuyez sur **F8** (ou choisissez Multiplayer dans le menu principal) pour ouvrir le panneau multijoueur parlé. Flèches gauche et droite pour changer de page :

- **Session** : état de la connexion, votre nom de joueur, joueurs connectés, quitter la session.
- **Steam** : nom du salon, mot de passe, public ou amis seulement, créer un salon, chercher des salons et en rejoindre un, inviter des amis.
- **IP directe** : adresse de l'hôte, port, héberger, rejoindre.
- **Outils** : synchroniser le monde, progression partagée, réinitialiser idées ou quêtes (hôte seulement).

Sur une ligne modifiable, appuyez sur **Entrée**, tapez le texte (chaque caractère est lu), puis **Entrée** pour valider ou **Échap** pour annuler. **Retour arrière** efface, **flèche haut** relit le texte.

La connexion se fait depuis le menu principal ; l'hôte lance ensuite ou charge le monde. Pendant une session :
- l'hôte fait tourner le jeu : en tant qu'invité, tout ce que vous faites est envoyé à l'hôte, comme un glisser à la souris ;
- seul l'hôte peut changer la vitesse ou mettre en pause ;
- l'empilement automatique (F4) est suspendu pour ne jamais déplacer les cartes des autres joueurs (F5 reste disponible) ;
- les arrivées et départs de joueurs et l'état de la connexion sont annoncés ;
- en tant qu'hôte, vous entendez aussi ce que font les autres joueurs, avec leur nom : paquets achetés et ouverts, cartes vendues, cartes posées sur une pile, fabrications lancées et terminées. Désactivable dans les réglages.

## Ce que le mod annonce tout seul

- Les nouvelles cartes, regroupées.
- Les ennemis, le début du combat, chaque coup avec les points de vie restants, la fin du combat.
- Les morts et les cartes perdues.
- Les nouvelles idées et les quêtes terminées.
- Les recettes qui démarrent (avec leur durée) et qui se terminent.
- Un petit carillon montant quand un villageois devient libre.
- Le portail étrange quand il apparaît.
- Les effets de statut (empoisonné, malade, en feu…).
- L'alerte avant la fin de la lune, le trop-plein de cartes.
- Les changements de vitesse, les textes des cinématiques, les fenêtres et pop-up.

## Réglages

**F7** ouvre les options du mod, partout dans le jeu :
- **Fonctions** : Entrée active ou désactive chacune (liste ci-dessous), ou change le délai de l'alerte de fin de lune.
- **Touches** (trois pages : partout, plateau, listes et menus) : Entrée sur une action, puis appuyez sur sa nouvelle touche, avec Ctrl ou Maj si vous voulez ; Échap annule. Si la touche servait déjà, l'autre action prend l'ancienne touche. I donne la touche par défaut. La dernière ligne remet toutes les touches par défaut (Entrée deux fois).

Les touches sont enregistrées dans `StacklandAccess_keys.txt`, à côté des sauvegardes du jeu.

Les réglages activé/désactivé sont aussi dans **Options > Mods > Stackland Access** :
- la narration (voix et sons du mod) ;
- l'affichage des menus du mod à l'écran ;
- l'annonce des nouvelles cartes ;
- l'annonce des combats ;
- chaque coup en combat ;
- l'annonce des recettes ;
- le délai de l'alerte de fin de lune ;
- la caméra qui suit la sélection ;
- l'empilement automatique ;
- le tri des piles par nom ;
- le son « villageois libre » ;
- les actions des autres joueurs (hôte multijoueur).

Les menus du mod (listes, options, champs de texte) s'affichent aussi à l'écran dans le style du jeu, avec des boutons cliquables, pour les personnes qui regardent. Le jeu continue de tourner pendant qu'ils sont ouverts.

Vous pouvez remplacer ce son par votre propre fichier WAV : `Mods\StacklandAccess\Sounds\villager_free.wav`.

Si l'option du jeu « pause automatique en clavier-souris » (Options > Accessibilité) est activée, le jeu se met en pause tant que vous tenez une carte.

## Traductions

Tous les textes du mod sont dans `StacklandAccess_strings.tsv`, une colonne par langue. Pour corriger une traduction, modifiez la cellule et proposez une pull request.

## Compiler le mod

1. Installer le SDK .NET.
2. Créer `StacklandAccess.csproj.user` avec le dossier du jeu :
   ```xml
   <Project><PropertyGroup><stacklands>C:\Program Files (x86)\Steam\steamapps\common\Stacklands</stacklands></PropertyGroup></Project>
   ```
3. Lancer `dotnet build` : le mod est installé dans le dossier Mods du jeu.
4. Lancer `powershell -ExecutionPolicy Bypass -File tools\package.ps1` pour créer le zip de release.

Le code décompilé du jeu n'est jamais inclus dans ce dépôt (règle du wiki de modding). Pour publier une version, voir [RELEASE.md](RELEASE.md).

## Licence

Le code du mod est sous licence MIT (voir [LICENSE](LICENSE)). Les fichiers tiers fournis sont listés dans [THIRD_PARTY.md](THIRD_PARTY.md).
