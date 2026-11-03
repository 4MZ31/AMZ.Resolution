# AMD Resolution Tray

[English documentation](docs/README.en.md)

Un utilitaire Windows portable pour afficher une zone **2324 × 1306 centrée** sur un écran natif **2560 × 1440** connecté à une carte AMD Radeon. Quand la surveillance est activée, il crée le mode personnalisé via AMD ADLX si nécessaire et l’applique uniquement pendant que **CS2 ou CS:GO** est ouvert.

**Vert = résolution appliquée en jeu. Gris = surveillance désactivée ou en attente du jeu. Double-clic gauche pour activer/désactiver la surveillance ; clic droit pour le menu.**

Sur une dalle 27 pouces, le profil correspond à environ **24,5 pouces**, avec 118 pixels de marge à gauche/droite et 67 pixels en haut/bas en affichage pixel pour pixel. La résolution n'est pas exactement 16:9 : aucun étirement n'est appliqué. Pour exactement 24 pouces, une autre résolution serait nécessaire ; cette version propose uniquement 2324 × 1306.

## Télécharger et démarrer

1. Télécharger l'archive Windows x64 depuis les [versions publiées](https://github.com/4MZ31/amd-resolution-tray/releases).
2. Extraire toute l'archive dans un dossier permanent, puis ouvrir **ResolutionTray.exe**. Le runtime .NET est inclus dans la distribution portable.
3. Choisir l'écran 2560 × 1440. Sa fréquence actuelle sera conservée.
4. Cliquer sur **Enregistrer et surveiller**. Sans jeu ouvert, le bureau reste inchangé et l’icône indique **En attente de CS2 / CS:GO**. À l’ouverture du jeu, le premier essai doit être confirmé dans les 15 secondes ; sinon l’affichage précédent est restauré.
5. La fermeture du dernier processus CS2 / CS:GO restaure automatiquement l’affichage précédent. La surveillance reste armée pour le prochain lancement. Un double-clic ou le menu clic droit permet de la désactiver et de restaurer le bureau immédiatement. Si l’icône est masquée, ouvrir le menu **^** de Windows.

La validation est mémorisée pour cet écran, cette fréquence et cette version du pilote. Une modification de ces éléments déclenche une nouvelle confirmation. L'option de démarrage avec Windows lance l'utilitaire désactivé. Une fermeture anormale antérieure peut nécessiter une récupération.

Le menu **Désactiver la surveillance** et la commande **Quitter** restaurent le mode et la mise à l’échelle sauvegardés avant l’activation. La résolution personnalisée créée reste disponible dans le pilote AMD.

## Configuration nécessaire

- Windows 11 x64, carte AMD Radeon et pilote AMD Software: Adrenalin Edition avec ADLX.
- Un écran natif 2560 × 1440 connecté directement à la carte AMD, en orientation paysage.
- Le pilote et l'écran doivent accepter le mode exact à la fréquence actuelle.
- Utiliser le mode **Étendre** en multi-écran. Les modes Dupliquer et Eyefinity ne sont pas pris en charge.
- Les autres écrans peuvent utiliser d'autres résolutions. L'écran cible est choisi explicitement ; les écrans Full HD ne sont pas proposés comme cibles.
- Deux écrans aux EDID identiques ne peuvent pas être distingués de manière sûre par cette version : leur sélection est refusée.

Aucun compte, accès administrateur, serveur ou service Windows n'est nécessaire. L'interface de cette première version est en français. L'exécutable n'est pas signé numériquement.

## CS2 / CS:GO

Activer la surveillance avant ou après avoir lancé le jeu. La présence de **cs2.exe** ou **csgo.exe** dans la session Windows courante est vérifiée environ une fois par seconde. Le profil reste appliqué si le jeu est réduit ou en arrière-plan, et il est restauré quand aucun de ces deux processus ne tourne. Le plein écran peut sélectionner une autre résolution et le mode sans bordure peut suivre le bureau : vérifier le résultat avec les réglages utilisés dans CS2. Si le jeu ou un autre programme change le mode ou la mise à l'échelle, l'icône revient au gris ; la restauration reste disponible. L'utilitaire ne force pas une résolution en boucle et ne s'intègre pas au processus du jeu. Une confirmation refusée ne déclenche pas de nouvelles tentatives pendant le même lancement : relancer le jeu ou désactiver/réactiver la surveillance pour réessayer. Si le jeu se ferme pendant une bascule ou sa confirmation, le profil est annulé et le bureau est restauré.

La lecture des réglages Windows/AMD ne suffit pas à mesurer la taille physique de l'image ou à confirmer le comportement de tous les jeux. Vérifier visuellement le centrage, les bandes noires et la fréquence lors du premier essai.

## Fonctionnement et récupération

Avant tout changement du pilote, l'application sauvegarde le mode actuel et les réglages de mise à l'échelle, puis attend que son processus de récupération soit prêt. Elle réutilise un mode exact existant, ou crée un mode AMD à partir des timings courants, en conservant les totaux et l'horloge du signal. Elle active le centrage GPU, désactive la mise à l'échelle entière si disponible, teste le mode Windows et vérifie le résultat après application.

La création automatique conserve les timings du mode courant pour éviter un arrondi de la largeur 2324 par certains générateurs CVT. Cela ne garantit pas l'acceptation par tous les écrans ou pilotes. Aucun mode approchant ou changement silencieux de fréquence n'est utilisé.

Un second processus attend la fermeture du processus principal et tente une restauration si une session reste active. Il ne peut pas intervenir pendant un arrêt complet de Windows ou une panne du pilote. Si la restauration est impossible, la sauvegarde est conservée et proposée au prochain lancement. Ne pas supprimer recovery.json avant d'avoir rétabli l'affichage.

Fichiers locaux : **%LOCALAPPDATA%\AmdResolutionTray** :

- settings.json : écran choisi, validation du profil et option de démarrage.
- recovery.json : sauvegarde de l'affichage pendant une session.
- app.log : erreurs locales, avec rotation limitée.

L'application n'envoie aucune télémétrie. Ces fichiers, les EDID et les identifiants de moniteurs ne sont pas inclus dans la distribution ni publiés dans le dépôt.

## Dépannage

- **Aucun écran proposé** : vérifier sa résolution native, la connexion AMD directe, l'installation du pilote et le mode Étendre.
- **Création AMD refusée** : revenir en 2560 × 1440 à la fréquence souhaitée. Si nécessaire, créer 2324 × 1306 dans Adrenalin avec des timings acceptés par votre écran, puis réessayer. [Procédure officielle AMD](https://www.amd.com/en/resources/support-articles/faqs/DH3-032.html).
- **Image agrandie** : contrôler le mode Centré d'AMD et les réglages de mise à l'échelle propres à l'écran/au jeu. L'utilitaire vérifie les réglages AMD, mais un traitement supplémentaire par l'écran reste possible.
- **Écran débranché pendant une session** : le reconnecter et demander la restauration depuis le menu ou au prochain lancement.
- **Arrêt manuel** : utiliser Quitter pour restaurer l'affichage. Pour désinstaller, désactiver le lancement avec Windows, quitter l'application puis supprimer son dossier. La résolution AMD peut être supprimée séparément dans Adrenalin.

Pour obtenir un diagnostic local en lecture seule depuis PowerShell :

~~~powershell
.\ResolutionTray.exe --diagnose "$env:TEMP\resolution-tray.json"
~~~

Ce fichier contient les identifiants de vos moniteurs. Retirer ces identifiants avant de le partager dans une issue publique.

Les commandes --settings et --quit ouvrent les paramètres de l'instance existante ou demandent une fermeture avec restauration.

## Développer

Installer le SDK .NET 10 et utiliser Windows x64. Aucune bibliothèque native AMD n'est redistribuée ; l'application charge celle du pilote installé. Les liaisons C# suivent les signatures du [SDK ADLX officiel](https://github.com/GPUOpen-LibrariesAndSDKs/ADLX/tree/32b5a740d42295c5dfe9026b9f52683da0f3af91/SDK/Include).

~~~powershell
dotnet build ResolutionTray.slnx -c Release
dotnet run --project tests/ResolutionTray.Tests -c Release
.\build.ps1
~~~

Fermer l'application avant de recompiler dans le même dossier de sortie. build.ps1 exécute les tests, produit l'exécutable autonome, l'archive ZIP et son empreinte SHA-256 dans artifacts/. GitHub Actions réalise les mêmes vérifications sur Windows.

- src/ResolutionTray.Core : orchestration des changements et récupération, indépendante du matériel.
- src/ResolutionTray : interface WinForms, appels Windows et AMD ADLX, stockage et processus de récupération.
- tests/ResolutionTray.Tests : 37 tests déterministes des transitions, restaurations, accès concurrents et cycles de lancement/fermeture du jeu, sans modifier l’écran.

## État de validation

Première version expérimentale. Un essai réel a vérifié la création/application de 2324 × 1306 à 144 Hz et le centrage AMD, puis le retour automatique à 2560 × 1440 à 144 Hz sur une RX 7800 XT avec un MSI MAG272CQR. Les réglages des deux autres écrans connectés étaient identiques après restauration. Cela ne constitue pas une validation de tous les modèles d'écrans, du HDR, de FreeSync, ni du rendu dans CS2. La surveillance CS2 / CS:GO est couverte par les tests automatisés ; un essai de lancement/fermeture avec un jeu réel reste à effectuer.

## Licence

[MIT](LICENSE). Projet indépendant, sans affiliation avec AMD ou Valve. AMD, Radeon et les autres marques appartiennent à leurs propriétaires respectifs.
