# AMZ.Resolution

À partir de la v0.7.0, le projet porte son nom définitif **AMZ.Resolution** : application, fenêtres, icône de notification, exécutable, solution, archives et dépôt GitHub. Les anciennes installations reçoivent une version de transition via l’ancien dépôt, conservé uniquement comme point de compatibilité pour les mises à jour. Cette transition peut conserver leur ancien nom de fichier exécutable ; les nouveaux téléchargements utilisent **AMZ.Resolution.exe**.

[English documentation](docs/README.en.md)

Un utilitaire Windows portable pour afficher une **résolution de jeu centrée**, prédéfinie ou personnalisée, sur un écran natif **2560 × 1440** connecté à une carte AMD Radeon. Le profil par défaut reste **2324 × 1306**. Quand la surveillance est activée, il crée le mode personnalisé via AMD ADLX si nécessaire et l’applique uniquement pendant qu’au moins un **jeu sélectionné** est ouvert.

**Vert = résolution appliquée en jeu. Gris = surveillance désactivée ou en attente du jeu. Double-clic gauche pour activer/désactiver la surveillance ; clic droit pour le menu.**

Sur une dalle 27 pouces, le profil correspond à environ **24,5 pouces**, avec 118 pixels de marge à gauche/droite et 67 pixels en haut/bas en affichage pixel pour pixel. La résolution n'est pas exactement 16:9 : aucun étirement n'est appliqué. Le préréglage 2272 × 1278 correspond à environ 24 pouces sur cette même dalle. L’aperçu adapte la zone et sa diagonale estimée au profil choisi.

## Télécharger et démarrer

1. Télécharger l'archive Windows x64 depuis les [versions publiées](https://github.com/4MZ31/AMZ.Resolution/releases).
2. Extraire toute l'archive dans un dossier permanent, puis ouvrir **AMZ.Resolution.exe**. Le runtime .NET est inclus dans la distribution portable.
3. Choisir l’écran 2560 × 1440, la résolution et le taux de rafraîchissement. **Fréquence du bureau** conserve la fréquence au moment de l’activation ; les autres choix sont détectés sur l’écran. Le profil initial reste 2324 × 1306 avec la fréquence du bureau.
4. Cliquer sur **Scanner les jeux**, puis cocher les jeux souhaités. Le scan détecte les bibliothèques Steam et Epic Games. **Ajouter un jeu…** permet de choisir un exécutable pour les autres installations. CS2 / CS:GO reste présélectionné lors de la mise à jour.
5. Cliquer sur **Enregistrer et activer**. Sans jeu sélectionné ouvert, le bureau reste inchangé et l’icône indique **En attente d’un jeu sélectionné**. À l’ouverture du jeu, le premier essai doit être confirmé dans les 15 secondes ; sinon l’affichage précédent est restauré.
6. La fermeture du dernier jeu sélectionné restaure automatiquement l’affichage précédent. La surveillance reste armée pour le prochain lancement. Un double-clic ou le menu clic droit permet de la désactiver et de restaurer le bureau immédiatement. Si l’icône est masquée, ouvrir le menu **^** de Windows.

La validation en jeu est mémorisée pour cet écran, ces dimensions, cette fréquence et cette version du pilote. Une modification de ces éléments déclenche une nouvelle confirmation. L'option de démarrage avec Windows lance l'utilitaire désactivé. Une fermeture anormale antérieure peut nécessiter une récupération.

Le menu **Désactiver la surveillance** et la commande **Quitter** restaurent le mode et la mise à l’échelle sauvegardés avant l’activation. La résolution personnalisée créée reste disponible dans le pilote AMD.

## Configuration nécessaire

- Windows 11 x64, carte AMD Radeon et pilote AMD Software: Adrenalin Edition avec ADLX.
- Un écran natif 2560 × 1440 connecté directement à la carte AMD, en orientation paysage.
- Le pilote et l’écran doivent accepter le mode exact à la fréquence choisie. Les fréquences proposées proviennent des modes Windows natifs progressifs de cet écran ; elles ne garantissent pas l’acceptation de chaque résolution personnalisée.
- Utiliser le mode **Étendre** en multi-écran. Les modes Dupliquer et Eyefinity ne sont pas pris en charge.
- Les autres écrans peuvent utiliser d'autres résolutions. L'écran cible est choisi explicitement ; les écrans Full HD ne sont pas proposés comme cibles.
- Deux écrans aux EDID identiques ne peuvent pas être distingués de manière sûre par cette version : leur sélection est refusée.

Aucun compte, accès administrateur, serveur ou service Windows n'est nécessaire. L'interface de cette première version est en français. L'exécutable n'est pas signé numériquement.

## Interface et apparence

La v0.6.0 réorganise les paramètres en trois espaces, accessibles depuis la navigation latérale :

- **Jeux** : scan Steam/Epic, ajout manuel, recherche, source et filtre **Sélectionnés**. Choisir une ligne puis **Choisir le fichier…** ou **Configurer…** pour régler ses exécutables. La touche Espace coche un jeu et F2 ouvre sa configuration.
- **Affichage** : écran cible, résolution, fréquence et test de compatibilité, avec aperçu de la zone centrée, estimation sur une dalle 27 pouces et marges en pixels. Les dimensions ne sont affichées que pour **Personnalisée…**.
- **Application** : apparence, démarrage avec Windows et mises à jour automatiques. Le **mode sombre est activé par défaut**, y compris lors d’une mise à jour depuis une ancienne version. Désactiver **Mode sombre** pour passer au mode clair.

Le changement de thème est immédiat dans la fenêtre. **Enregistrer** ou **Enregistrer et activer** mémorise ce choix ; **Annuler** rétablit le thème enregistré. Les couleurs de contraste élevé de Windows ont priorité sur la palette de l’application.

À partir de la **v0.6.1**, les vues principales utilisent des barres de défilement fines et des menus déroulants dessinés dans le thème actif. Les menus affichent une coche sur la valeur choisie ; les flèches, Début/Fin et Page précédente/suivante déplacent le choix, Entrée valide et Échap annule. Les longues listes défilent avec la même poignée. L’en-tête du tableau couvre toute sa largeur, y compris à droite, sans bandeau système blanc. Les séparateurs des colonnes permettent d’ajuster leur répartition.

La **v0.6.2** corrige le conflit de dimensionnement introduit dans les panneaux défilants de l’onglet Affichage : les sélecteurs et l’aperçu occupent à nouveau la largeur disponible. La hauteur et le défilement se recalculent lors de l’affichage des dimensions personnalisées, du redimensionnement de la fenêtre et du changement de thème.

La recherche possède son propre champ avec icône, focus visible et bouton **Effacer la recherche**. Échap efface d’abord une saisie non vide ; quand la recherche est vide, Échap garde son rôle de fermeture des paramètres. Effacer la recherche ou changer le filtre ne décoche pas les jeux.

Le pied de fenêtre reste visible dans chaque espace : résolution/fréquence, modifications non enregistrées ou raison bloquant l’enregistrement. **Voir** ramène au réglage à terminer. Les choix de jeux et d’affichage sont conservés quand on change de page ou de filtre. **Ctrl+1/2/3** change d’espace, **Ctrl+F** ouvre la recherche de jeux et **Ctrl+S** enregistre les réglages valides.

La refonte suit les neuf passes d’[Unslop](https://github.com/heydamir/unslop), créé par **Damir Sokolovsky (@heydamir)** : rythme d’espacement de 4 px, palettes graphite sombre/clair, glyphes cohérents, libellés directs, typographie Segoe UI, navigation par tâche, focus et états explicites, aperçu de dalle dessiné en code et mouvement réservé au scan ou au compte à rebours. La méthode est adaptée à WinForms ; aucune image générée ni animation décorative n’est utilisée. Attribution et licences des éléments adaptés : [notices tierces](docs/THIRD-PARTY-NOTICES.txt).

## Résolution et taux de rafraîchissement

Le panneau **Votre zone de jeu** propose neuf préréglages : 2324 × 1306 (défaut), 2304 × 1296, 2272 × 1278, 2240 × 1260, 1920 × 1080, 1920 × 1440, 1600 × 1200, 1280 × 960 et 2560 × 1440 (native). Tous utilisent le centrage sans agrandissement, y compris les formats 4:3. Les marges et la taille physique varient selon le choix.

La fréquence est un choix commun à tous les jeux cochés. Seules les fréquences détectées en 2560 × 1440 sont proposées, avec un choix pour conserver celle du bureau. Une fréquence enregistrée devenue indisponible bloque l’enregistrement au lieu d’être remplacée silencieusement. Le mode exact reste testé et vérifié à l’activation ; un changement de résolution, de fréquence ou de pilote demande une nouvelle confirmation en jeu.

Pour ajouter une résolution :

1. Choisir **Personnalisée…**, saisir la largeur et la hauteur, puis choisir la fréquence.
2. Fermer les jeux sélectionnés et cliquer sur **Vérifier la compatibilité**. Les dimensions doivent rester entre 640 × 480 et 2560 × 1440, sans dépasser la dalle.
3. Le test sauvegarde le bureau, arme le processus de récupération, crée le mode AMD si nécessaire et teste puis applique le mode Windows exact. L’écran change temporairement ; confirmer **L’image est correcte** dans les 15 secondes si le résultat convient.
4. Le bureau et sa fréquence sont restaurés même si le test est accepté. En cas de refus, de délai dépassé ou d’erreur, la résolution ne devient pas validée. Une restauration incomplète conserve le journal de récupération.
5. Après un test réussi, la résolution rejoint la liste des choix personnels. Cliquer sur **Enregistrer** pour conserver cette liste et le profil. Une nouvelle combinaison personnalisée de dimensions, fréquence, écran ou pilote doit être testée avant enregistrement.

Le contrôle confirme l’acceptation par le pilote et Windows, puis demande une validation visuelle. Il ne garantit pas le rendu de tous les jeux, du HDR ou de FreeSync. Le bouton de test fonctionne aussi avec les préréglages. Les modes créés restent enregistrés dans AMD, même après un test annulé ou la fermeture sans enregistrement des paramètres. Le choix **Annuler** ne modifie pas les préférences de l’application.

## Bibliothèque de jeux

Le bouton **Scanner les jeux** lit les installations connues de Steam (y compris les bibliothèques sur plusieurs disques) et d’Epic Games. Le premier affichage après la mise à jour lance un scan automatiquement. Le scan reste dans les bibliothèques déclarées et les dossiers de jeux, peut être annulé et ne lance aucun exécutable.

- La recherche et le filtre de source permettent de retrouver un jeu. Les cases cochées restent conservées après un filtre ou un nouveau scan.
- **Prêt** : un exécutable a été reconnu ou choisi. Les jeux sélectionnés utilisent tous la résolution et la fréquence choisies.
- **À configurer** : sélectionner la ligne, ouvrir **Choisir le fichier…** ou **Configurer…** et cocher le fichier du jeu utilisé pendant une partie. Il est possible de parcourir le disque si le scan ne l’a pas trouvé. Le profil ne peut pas être armé avec un jeu coché non configuré.
- Les autres lanceurs (GOG, EA, Ubisoft, Xbox…) ne sont pas scannés automatiquement dans cette version. Un exécutable accessible peut être ajouté manuellement.
- Le scan peut conserver une ancienne entrée si son disque ou son lanceur est temporairement absent. Les choix manuels restent conservés ; une installation déplacée est mise à jour lors du prochain scan.
- Les jeux scannés et ajoutés manuellement sont reconnus par leur chemin d’exécutable. CS2 / CS:GO reste reconnu par son nom tant que l’ancien réglage n’a pas été remplacé par un scan.

**Enregistrer** sauvegarde les choix. **Enregistrer et activer** sauvegarde et arme la surveillance. Décocher tous les jeux puis enregistrer arrête la surveillance. Annuler ferme la fenêtre sans enregistrer les changements.

## Activation automatique

Activer la surveillance avant ou après avoir lancé le jeu. La présence des exécutables des jeux cochés dans la session Windows courante est vérifiée environ une fois par seconde. Le profil reste appliqué si un jeu est réduit ou en arrière-plan. Si plusieurs jeux sélectionnés sont ouverts, le bureau est restauré seulement après la fermeture du dernier. Le plein écran peut sélectionner une autre résolution et le mode sans bordure peut suivre le bureau : vérifier le résultat avec les réglages utilisés dans CS2. Si le jeu ou un autre programme change le mode ou la mise à l'échelle, l'icône revient au gris ; la restauration reste disponible. L'utilitaire ne force pas une résolution en boucle et ne s'intègre pas au processus du jeu. Une confirmation refusée ne déclenche pas de nouvelles tentatives pendant le même lancement : relancer le jeu ou désactiver/réactiver la surveillance pour réessayer. Si le jeu se ferme pendant une bascule ou sa confirmation, le profil est annulé et le bureau est restauré.

La lecture des réglages Windows/AMD ne suffit pas à mesurer la taille physique de l'image ou à confirmer le comportement de tous les jeux. Vérifier visuellement le centrage, les bandes noires et la fréquence lors du premier essai.

## Fonctionnement et récupération

Avant tout changement du pilote, l’application sauvegarde le mode actuel et les réglages de mise à l’échelle, puis attend que son processus de récupération soit prêt. Elle réutilise un mode exact existant. Pour créer un mode AMD absent, elle prépare si nécessaire un mode natif 2560 × 1440 à la fréquence choisie, puis conserve les totaux et l’horloge de ce signal. Cette préparation fait partie de la session protégée ; le mode et la fréquence antérieurs sont restaurés en cas d’échec ou à la fin du jeu/test. Elle active le centrage GPU, désactive la mise à l'échelle entière si disponible, teste le mode Windows et vérifie le résultat après application.

La création automatique conserve les timings du mode courant pour éviter un arrondi de la largeur 2324 par certains générateurs CVT. Cela ne garantit pas l'acceptation par tous les écrans ou pilotes. Aucun mode approchant ou changement silencieux de fréquence n'est utilisé.

Un second processus attend la fermeture du processus principal et tente une restauration si une session reste active. Il ne peut pas intervenir pendant un arrêt complet de Windows ou une panne du pilote. Si la restauration est impossible, la sauvegarde est conservée et proposée au prochain lancement. Ne pas supprimer recovery.json avant d'avoir rétabli l'affichage.

Fichiers locaux : **%LOCALAPPDATA%\AmdResolutionTray** (identifiant historique conservé pour retrouver les paramètres et les données de récupération après le renommage) :

- settings.json : écran choisi, dimensions, fréquence, résolutions personnelles, validation du test, bibliothèque, jeux cochés, exécutables, démarrage, thème sombre/clair et option de mise à jour.
- recovery.json : sauvegarde de l'affichage pendant une session.
- app.log : erreurs locales, avec rotation limitée.

L'application n'envoie aucune télémétrie. Ces fichiers, les EDID et les identifiants de moniteurs ne sont pas inclus dans la distribution ni publiés dans le dépôt.

## Dépannage

- **Aucun écran proposé** : vérifier sa résolution native, la connexion AMD directe, l'installation du pilote et le mode Étendre.
- **Création AMD refusée** : choisir une autre fréquence détectée ou une autre résolution. Si nécessaire, créer le mode exact dans Adrenalin avec des timings acceptés par votre écran, puis relancer le test. [Procédure officielle AMD](https://www.amd.com/en/resources/support-articles/faqs/DH3-032.html).
- **Image agrandie** : contrôler le mode Centré d'AMD et les réglages de mise à l'échelle propres à l'écran/au jeu. L'utilitaire vérifie les réglages AMD, mais un traitement supplémentaire par l'écran reste possible.
- **Écran débranché pendant une session** : le reconnecter et demander la restauration depuis le menu ou au prochain lancement.
- **Arrêt manuel** : utiliser Quitter pour restaurer l'affichage. Pour désinstaller, désactiver le lancement avec Windows, quitter l'application puis supprimer son dossier. La résolution AMD peut être supprimée séparément dans Adrenalin.

Pour obtenir un diagnostic local en lecture seule depuis PowerShell :

~~~powershell
.\AMZ.Resolution.exe --diagnose "$env:TEMP\amz-resolution.json"
~~~

Ce fichier contient les identifiants de vos moniteurs. Retirer ces identifiants avant de le partager dans une issue publique.

Les commandes --settings et --quit ouvrent les paramètres de l'instance existante ou demandent une fermeture avec restauration. --scan-games <fichier.json> exporte le résultat d’un scan en lecture seule ; cet export contient les noms et chemins des installations et reste local.

## Mises à jour automatiques

À partir de la **v0.4.0**, la version portable vérifie les publications de ce dépôt GitHub au démarrage puis toutes les six heures. Les versions publiées numérotées, y compris expérimentales, sont prises en compte. Les brouillons et versions plus anciennes sont ignorés. Une panne réseau laisse l’application fonctionner ; une nouvelle vérification sera tentée plus tard.

- **Mises à jour automatiques**, dans les paramètres, est activé par défaut et peut être désactivé.
- **Mettre à jour maintenant…**, dans le menu de l’icône, permet de rechercher et installer une version sans attendre.
- Le téléchargement est vérifié avec le SHA-256 et, lorsqu’il est disponible, l’empreinte fournie par GitHub. Le contenu et la version de l’exécutable Windows x64 sont également contrôlés.
- L’installation attend la fermeture des jeux sélectionnés et des fenêtres de configuration. Le bureau est restauré avant la fermeture de l’application.
- Un processus séparé remplace l’exécutable dans son dossier actuel puis redémarre l’application. Si son démarrage n’est pas confirmé, l’ancienne version est restaurée et relancée. Les réglages ne sont pas remplacés ; la surveillance précédemment armée reprend au redémarrage.
- Aucune élévation administrateur n’est demandée. Le dossier contenant l’application doit être accessible en écriture (par exemple un dossier utilisateur). Un dossier protégé peut nécessiter de déplacer l’application.

**Installation initiale depuis une version antérieure à v0.4.0 :** quitter l’ancienne application, télécharger et extraire une dernière fois la nouvelle archive, puis remplacer AMZ.Resolution.exe. Les mises à jour suivantes sont automatiques. Les builds de développement ne s’auto-remplacent pas.

Les téléchargements temporaires sont placés dans **%LOCALAPPDATA%\AmdResolutionTray\Updates**. Les anciennes préparations sont nettoyées lors des vérifications suivantes. Les requêtes contactent GitHub et ses serveurs de téléchargement ; la liste de jeux et les réglages restent locaux. Le SHA-256 contrôle l’intégrité du téléchargement et ne constitue pas une signature de l’éditeur.

## Développer

Installer le SDK .NET 10 et utiliser Windows x64. Aucune bibliothèque native AMD n'est redistribuée ; l'application charge celle du pilote installé. Les liaisons C# suivent les signatures du [SDK ADLX officiel](https://github.com/GPUOpen-LibrariesAndSDKs/ADLX/tree/32b5a740d42295c5dfe9026b9f52683da0f3af91/SDK/Include).

~~~powershell
dotnet build AMZ.Resolution.slnx -c Release
dotnet run --project tests/AMZ.Resolution.Tests -c Release
dotnet run --project tests/AMZ.Resolution.UiTests -c Release
.\build.ps1
~~~

Fermer l'application avant de recompiler dans le même dossier de sortie. build.ps1 exécute les tests, produit l'exécutable autonome, l'archive ZIP et son empreinte SHA-256 dans artifacts/. GitHub Actions réalise les mêmes vérifications sur Windows, puis teste le redémarrage réel et le retour arrière de l’installateur sur des copies isolées.

- src/AMZ.Resolution.Core : orchestration des changements et récupération, indépendante du matériel.
- src/AMZ.Resolution : interface WinForms, appels Windows et AMD ADLX, stockage et processus de récupération.
- tests/AMZ.Resolution.Tests : 127 tests déterministes des profils, jeux, archives, versions et remplacements réversibles, sans modifier l’écran.
- tests/AMZ.Resolution.UiTests : contrôles WinForms de navigation, sélection, filtres, géométrie minimale, validation, dialogues, thèmes et conservation des préférences. Les tests du bouton de compatibilité utilisent un substitut, sans appel AMD ni changement d’affichage.

## État de validation

Première version expérimentale. Un essai réel a vérifié la création/application de 2324 × 1306 à 144 Hz et le centrage AMD, puis le retour automatique à 2560 × 1440 à 144 Hz sur une RX 7800 XT avec un MSI MAG272CQR. Les réglages des deux autres écrans connectés étaient identiques après restauration. Cela ne constitue pas une validation de tous les modèles d'écrans, du HDR, de FreeSync, ni du rendu dans CS2. La sélection multiple est couverte par les tests automatisés. Un scan réel des bibliothèques Steam et Epic, la recherche, les cases à cocher et leur conservation après un rescan ont été vérifiés. Le comportement des nouveaux jeux sélectionnés reste à valider sur chaque installation. La v0.5.0 ajoute les résolutions et fréquences sélectionnables et le test temporaire personnalisé ; leur orchestration et restauration sont testées automatiquement, et les contrôles de saisie, l’aperçu et la liste réelle des fréquences ont été vérifiés en lecture seule. Les nouvelles combinaisons matérielles doivent être essayées avec le bouton de compatibilité sur chaque installation.

## Licence

[MIT](LICENSE). Projet indépendant, sans affiliation avec AMD ou Valve. AMD, Radeon et les autres marques appartiennent à leurs propriétaires respectifs.
