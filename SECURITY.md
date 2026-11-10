# Sécurité

Ne publiez pas de secret ni de preuve d'exploitation contenant des données personnelles dans une issue.
Utilisez le signalement privé GitHub s'il est disponible, sinon contactez le propriétaire avant de partager les détails sensibles.

## Mises à jour 0.8.0

Une installation automatique exige une signature Authenticode valide et le même certificat SHA-256 que l'application en cours, lui-même validé par Windows.
Les builds non signés restent utilisables et téléchargeables manuellement ; ils ne téléchargent/installent pas automatiquement un exécutable non authentifié.
Un changement de certificat exige une installation manuelle de confiance. Les builds CI de test sont signés avec un certificat éphémère exclusivement sur le runner ; ce certificat ne signe jamais la release publique.

Les seules versions candidates à la mise à jour sont les versions stables numériques, sans rétrogradation.
L'ancienne 0.7.0 conserve son ancien protocole jusqu'à son remplacement : installer manuellement 0.8.0 est recommandé.

## Publication et maintenance

Le job de publication dépend de tous les tests Windows et publie l'artefact exact vérifié, avec SHA-256, informations SDK/runtime et attestation GitHub.
Les permissions d'écriture ne sont accordées qu'au job de publication exécuté sur main, jamais à une PR.
Le workflow manuel permet de reprendre une publication échouée sans changer les assets d'une release déjà publiée.

À configurer par un administrateur : protection de main avec check `windows`, protection des tags `v*`, releases immuables, limitation des contournements et protection de la clé de signature. Ces réglages ne sont pas activés par le simple ajout de ce fichier.

Le runtime .NET est embarqué : vérifier les avis Microsoft à chaque mise à jour de sécurité et republier une version supérieure après reconstruction. Le workflow hebdomadaire valide le build mais ne publie pas automatiquement.
Le SDK est sélectionné par global.json ; modifier sa version via PR et refaire les tests Windows. Les versions réellement utilisées figurent dans build-info.json.

## Limites

Le watchdog récupère après arrêt du parent ou absence de progression (30 s sans pulsation UI, 60 s avant confirmation). Il arrête le parent avant toute restauration pour ne pas concurrencer un appel pilote. Une pause de la machine redonne un délai de grâce. Un pilote globalement bloqué peut empêcher la récupération.
Les tests automatiques utilisent des substituts : ils ne certifient pas HDR, FreeSync, veille/reprise, déconnexion ou compatibilité de chaque écran AMD.
Les fichiers locaux ne constituent pas une frontière de sécurité contre un processus malveillant du même utilisateur. Ne pas lancer l'application avec élévation.

## Reprise d'une mise à jour interrompue

Fermer l'application. Conserver le fichier adjacent `AMZ.Resolution.exe.update.json` et ses sauvegardes ; ne pas les modifier.
Dans `%LOCALAPPDATA%\AmdResolutionTray\Updates\<id>`, le helper de l'ancienne version est conservé lorsqu'une transaction est inachevée.
Exécuter ce helper depuis PowerShell :

```powershell
& 'C:\chemin\AMZ.Resolution.Updater.exe' --recover-update 'C:\chemin\AMZ.Resolution.exe'
```

Il vérifie les hashes avant restauration. Une opération répétée est sans effet après réussite. Ne jamais utiliser le nouveau binaire cible comme son propre helper.
Une release non signée ne lance pas de nouvelle transaction automatique, mais cette récupération protège les futures builds signées.
