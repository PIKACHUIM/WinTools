<p align="center">
  <a href="../../README.md">简体中文</a> · <a href="README.zh-TW.md">繁體中文</a> · <a href="README.en.md">English</a> · <a href="README.ja.md">日本語</a> · <a href="README.ko.md">한국어</a> · <a href="README.de.md">Deutsch</a><br/>
  <b>Français</b> · <a href="README.es.md">Español</a> · <a href="README.pt.md">Português</a> · <a href="README.it.md">Italiano</a> · <a href="README.ru.md">Русский</a> · <a href="README.ar.md">العربية</a>
</p>

<p align="center">
  <img src="../public/logo.png" width="88" height="88" alt="WinTools" />
</p>

<h1 align="center">WinTools</h1>

<p align="center">
  <a href="https://github.com/PIKACHUIM/WinTools/releases"><img src="https://img.shields.io/badge/version-0.1.0-orange" alt="Version"></a>
  <a href="https://www.microsoft.com/windows"><img src="https://img.shields.io/badge/Windows-10%20%2F%2011-0078D4" alt="Plateforme"></a>
  <a href="https://dotnet.microsoft.com"><img src="https://img.shields.io/badge/.NET-10.0-512BD4" alt=".NET"></a>
  <a href="https://github.com/PIKACHUIM/WinTools/blob/main/LICENSE"><img src="https://img.shields.io/badge/license-AGPL--3.0-blue" alt="Licence"></a>
  <a href="https://github.com/PIKACHUIM/WinTools/actions/workflows/build.yml"><img src="https://github.com/PIKACHUIM/WinTools/actions/workflows/build.yml/badge.svg" alt="Build"></a>
  <a href="https://wtools.opkg.cn/"><img src="https://img.shields.io/badge/docs-online-41B883" alt="Docs"></a>
</p>

## Présentation

Avant une réinstallation, il faut sauvegarder les pilotes ; un ancien logiciel peut réclamer un runtime, et les caches de mise à jour finissent par s'accumuler. Ces petites tâches obligent souvent à passer d'un outil Windows à l'autre. WinTools réunit les plus courantes dans une seule application de bureau.

WinTools est une application WPF. Elle ne reste pas en arrière-plan et ne s'ajoute pas au démarrage. Elle se connecte uniquement pour rechercher des mises à jour, télécharger un runtime ou consulter la liste en ligne des distributions WSL. Fermer la fenêtre termine le processus.

Dix modules : **Nettoyage, Optimisation, Fonctionnalités, Mises à jour, Démarrage, Composants, Runtimes, Applications, Pilotes, Sauvegarde.**

## Configuration requise

- Windows 10 1809 (17763) ou ultérieur, ou Windows 11, x64
- La version dépendante du framework nécessite [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
- Droits administrateur (déclarés dans le manifeste) ; DISM, pilotes, services et images en dépendent
- Certaines fonctions appellent des composants Windows : DISM, Windows Update Agent, `pnputil`, WIMGAPI. Ils peuvent manquer sur une image allégée

## Téléchargement

| Version | Remarque | Lien |
| --- | --- | --- |
| Portable | Runtime inclus, décompresser et lancer | [WinTools-*-green-win-x64.exe](https://github.com/PIKACHUIM/WinTools/releases/latest) |
| Dépendante du framework | Nécessite .NET 10 Desktop Runtime | [WinTools-*-frame-win-x64.zip](https://github.com/PIKACHUIM/WinTools/releases/latest) |
| Beta | Reconstruite à chaque push sur `main` | [Préversion Beta](https://github.com/PIKACHUIM/WinTools/releases/tag/beta) |

Les versions officielles portent une signature cloud Certum avec horodatage, vérifiable via `Get-AuthenticodeSignature`. Un `SHA256SUMS.txt` accompagne chaque version.

## Fonctionnalités

### Vue d'ensemble

Version du système et numéro de build, utilisation CPU et mémoire, espace libre par disque, activité récente. Toutes les données sont lues localement.

<details>
<summary>Capture d'écran</summary>

![Vue d'ensemble](../public/screenshots/dashboard.png)

</details>

### Nettoyage

Analyse les disques entiers et couvre les dossiers temporaires système, le cache des mises à jour, journaux et vidages, caches de navigateurs, caches d'outils de développement (npm, pip, NuGet, Gradle, Docker) et les dossiers de cache de plus de 150 applications.

Les résultats sont groupés par catégorie avec taille et niveau de sécurité. Les fichiers verrouillés par un processus sont ignorés et journalisés au lieu de bloquer l'analyse. Les règles sont du JSON dans `src/WinTools.Infrastructure/Rules/` ; ajouter un chemin ne demande aucune modification de code.

<details>
<summary>Capture d'écran</summary>

![Nettoyage](../public/screenshots/cleanup.png)

</details>

### Optimisation

Confidentialité et télémétrie, performances, Explorateur de fichiers, comportement du système, réseau, mises à jour, jeux.

Chaque entrée gère détection, application et retour arrière : l'état actuel est visible avant toute modification. Les éléments recommandés se limitent aux effets bien établis.

<details>
<summary>Capture d'écran</summary>

![Optimisation](../public/screenshots/optimize.png)

</details>

### Fonctionnalités

- **Fonctionnalités système** : liste toutes les fonctionnalités facultatives via l'API DISM, groupées par usage, avec description et état ; les changements sont appliqués en une fois.
- **Canal d'édition** : lit le SKU, la version et l'état d'activation. Le changement utilise `changepk` avec une clé générique et n'active pas Windows.

<details>
<summary>Capture d'écran</summary>

![Fonctionnalités système](../public/screenshots/features-systemfeatures.png)

</details>

### Mises à jour

- **Mises à jour installées** : énumérer et désinstaller individuellement.
- **Mises à jour disponibles** : lancer manuellement une recherche Windows Update avec catégorie, KB, taille et date, installation par lot et récapitulatif.
- **Indicateurs de fonctionnalité** : gérer `FeatureManagement\Overrides` par ID avec état, priorité et variante, plus import, export et réinitialisation.

<details>
<summary>Capture d'écran</summary>

![Mises à jour disponibles](../public/screenshots/updates-available.png)

</details>

### Démarrage

Tout sur une page. Icônes réelles des programmes, interrupteurs pour l'état. Les services changent leur type de démarrage dans la ligne (Automatique, Automatique différé, Manuel, Désactivé).

<details>
<summary>Capture d'écran</summary>

![Démarrage](../public/screenshots/startup-services.png)

</details>

### Composants

- **Centre de sécurité** : 14 réglages couvrant la protection en temps réel, la protection cloud, la surveillance du comportement, les PUA, la protection réseau, l'accès contrôlé aux dossiers, l'intégrité de la mémoire, l'UAC, SmartScreen, les trois profils de pare-feu et les exclusions.
- **Composants système** : applications intégrées (Terminal, Calculatrice, Bloc-notes, Paint, Photos, etc.) à installer, réparer ou désinstaller ; distributions WSL ; sous-système Android depuis un paquet local ; réparation du Store.

<details>
<summary>Capture d'écran</summary>

![Centre de sécurité](../public/screenshots/components-securitycenter.png)

</details>

### Runtimes

Liste les runtimes .NET et Visual C++ installés. Les versions disponibles sont récupérées à la demande : .NET depuis `releases-index.json` officiel avec vérification SHA512, VC++ via les liens `aka.ms` avec vérification Authenticode. Les deux s'installent en silence et peuvent être désinstallés.

<details>
<summary>Capture d'écran</summary>

![Runtimes](../public/screenshots/runtimes-dotnet.png)

</details>

### Applications

Affiche les icônes, les versions, la taille estimée et les informations d'installation. La désinstallation d'un programme lance son propre désinstalleur ; les paquets Appx sont gérés avec les outils intégrés à Windows.

<details>
<summary>Capture d'écran</summary>

![Applications](../public/screenshots/apps-programs.png)

</details>

### Pilotes

Gère tout le DriverStore. Sélectionnez autant de paquets que voulu pour les sauvegarder, les supprimer ou installer depuis un dossier. Les traitements par lot affichent la progression et un récapitulatif.

<details>
<summary>Capture d'écran</summary>

![Pilotes](../public/screenshots/drivers-systemdrivers.png)

</details>

### Sauvegarde

Le volume source se choisit dans une liste ; le format d'image (wim / esd / swm) pilote l'extension et la compression ; la taille de fractionnement ne concerne que swm ; la liste d'exclusions par défaut est affichée avant la capture. À l'application, l'image peut être vérifiée et le chargeur d'amorçage réparé avec `bcdboot`.

<details>
<summary>Capture d'écran</summary>

![Sauvegarde](../public/screenshots/backup-capture.png)

</details>

## Compilation

Nécessite le SDK .NET 10.

```powershell
# Build de débogage
dotnet build src/WinTools.App/WinTools.App.csproj

# Build Release
dotnet build src/WinTools.App/WinTools.App.csproj -c Release

# Génère les deux paquets dans artifacts/publish/{portable,framework}
powershell -ExecutionPolicy Bypass -File tools/publish.ps1 -Profile both
```

Architecture, détails des modules, format des règles, localisation et débogage sont documentés sur **[wtools.opkg.cn](https://wtools.opkg.cn/)** ou dans [docs/guide/development.md](../guide/development.md).

## Références

| Projet | Éléments repris |
| --- | --- |
| [winutil](https://github.com/ChrisTitusTech/winutil) | Regroupement des réglages, flux sélection puis application |
| [Optimizer](https://github.com/hellzerg/optimizer) | Catégories, formulation des risques, organisation du démarrage et des services |
| [ZyperWinOptimize](https://github.com/ZyperRap/ZyperWinOptimize) | Liste de réglages et densité d'information pour le public chinois |
| [Dism-Multi-language](https://github.com/Chuyu-Team/Dism-Multi-language) | Usage de DISM et organisation multilingue des ressources |
| [Geek Uninstaller](https://geekuninstaller.com/) | Choix des colonnes de la liste des programmes |
| [ViVeTool](https://github.com/thebookisclosed/ViVe) | Modèle de données des indicateurs et sémantique à trois états |

Les règles de nettoyage viennent au départ d'une liste personnelle de chemins de cache, complétée au fil du temps. Elles sont maintenues dans `tools/rules/*.txt` et générées par `tools/Generate-CleanupRules.ps1`.

## Avertissement

- L'outil modifie le registre, les types de démarrage des services, les fonctionnalités système et les pilotes. Créez un point de restauration ou sauvegardez vos données avant usage.
- L'auteur décline toute responsabilité en cas de perte de données ou de dommage système lié à l'utilisation.
- Ne pas utiliser pour contourner une licence, une activation ou toute autre fin contraire à la loi locale.

## Licence

[AGPL-3.0](../../LICENSE) © WinTools

### Composants tiers

| Composant | Licence | Usage |
| --- | --- | --- |
| [Maple Mono NF CN](https://github.com/subframe7536/maple-font) | SIL OFL 1.1 | Police de l'interface, intégrée en TTF |
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) 8.4.2 | MIT | Générateurs de source MVVM |
| [Microsoft.Extensions.DependencyInjection](https://github.com/dotnet/runtime) 10.0.13 | MIT | Injection de dépendances |
| [VitePress](https://vitepress.dev/) | MIT | Site de documentation uniquement |
