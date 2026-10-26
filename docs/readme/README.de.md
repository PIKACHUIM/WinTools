<p align="center">
  <a href="../../README.md">简体中文</a> · <a href="README.zh-TW.md">繁體中文</a> · <a href="README.en.md">English</a> · <a href="README.ja.md">日本語</a> · <a href="README.ko.md">한국어</a> · <b>Deutsch</b><br/>
  <a href="README.fr.md">Français</a> · <a href="README.es.md">Español</a> · <a href="README.pt.md">Português</a> · <a href="README.it.md">Italiano</a> · <a href="README.ru.md">Русский</a> · <a href="README.ar.md">العربية</a>
</p>

<p align="center">
  <img src="../public/logo.png" width="88" height="88" alt="WinTools" />
</p>

<h1 align="center">WinTools</h1>

<p align="center">
  <a href="https://github.com/PIKACHUIM/WinTools/releases"><img src="https://img.shields.io/badge/version-0.1.0-orange" alt="Version"></a>
  <a href="https://www.microsoft.com/windows"><img src="https://img.shields.io/badge/Windows-10%20%2F%2011-0078D4" alt="Plattform"></a>
  <a href="https://dotnet.microsoft.com"><img src="https://img.shields.io/badge/.NET-10.0-512BD4" alt=".NET"></a>
  <a href="https://github.com/PIKACHUIM/WinTools/blob/main/LICENSE"><img src="https://img.shields.io/badge/license-AGPL--3.0-blue" alt="Lizenz"></a>
  <a href="https://github.com/PIKACHUIM/WinTools/actions/workflows/build.yml"><img src="https://github.com/PIKACHUIM/WinTools/actions/workflows/build.yml/badge.svg" alt="Build"></a>
  <a href="https://wtools.opkg.cn/"><img src="https://img.shields.io/badge/docs-online-41B883" alt="Doku"></a>
</p>

## Überblick

Vor einer Neuinstallation müssen Treiber gesichert werden; ältere Programme brauchen manchmal eine Runtime, und Update-Caches wachsen mit der Zeit. Für solche Aufgaben wechselt man sonst zwischen mehreren Windows-Werkzeugen. WinTools bündelt die häufigsten davon in einer Desktop-App.

WinTools ist eine WPF-App. Sie läuft nicht dauerhaft im Hintergrund und trägt sich nicht in den Autostart ein. Eine Netzwerkverbindung wird nur bei einer Update-Suche, einem Runtime-Download oder der Online-Liste für WSL hergestellt. Beim Schließen endet der Prozess.

Zehn Module: **Bereinigung, Optimierung, Funktionen, Updates, Autostart, Komponenten, Runtimes, Apps, Treiber, Sicherung.**

## Voraussetzungen

- Windows 10 1809 (17763) oder neuer, oder Windows 11, x64
- Die framework-abhängige Variante benötigt die [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
- Administratorrechte (im Manifest deklariert); DISM, Treiber, Dienste und Abbilder benötigen sie
- Einige Funktionen nutzen Windows-Komponenten: DISM, Windows Update Agent, `pnputil`, WIMGAPI. In abgespeckten Images können sie fehlen

## Download

| Variante | Hinweis | Link |
| --- | --- | --- |
| Portable | Laufzeit enthalten, entpacken und starten | [WinTools-*-green-win-x64.exe](https://github.com/PIKACHUIM/WinTools/releases/latest) |
| Framework-abhängig | Benötigt .NET 10 Desktop Runtime | [WinTools-*-frame-win-x64.zip](https://github.com/PIKACHUIM/WinTools/releases/latest) |
| Beta | Wird bei jedem Push auf `main` neu gebaut | [Beta-Vorabversion](https://github.com/PIKACHUIM/WinTools/releases/tag/beta) |

Release-Builds tragen eine Certum-Cloud-Signatur mit Zeitstempel; prüfbar mit `Get-AuthenticodeSignature`. Jede Version enthält eine `SHA256SUMS.txt`.

## Funktionen

### Systemübersicht

Systemversion und Buildnummer, CPU- und Speicherauslastung, freier Speicher je Datenträger, letzte Aktivitäten. Alle Daten werden lokal gelesen.

<details>
<summary>Screenshot</summary>

![Systemübersicht](../public/screenshots/dashboard.png)

</details>

### Bereinigung

Durchsucht ganze Datenträger und deckt temporäre Systemordner, Update-Cache, Protokolle und Dumps, Browsercaches, Entwickler-Caches (npm, pip, NuGet, Gradle, Docker) sowie die Cacheordner von über 150 Anwendungen ab.

Ergebnisse sind nach Kategorie gruppiert, mit Größe und Sicherheitsstufe. Dateien, die von laufenden Prozessen gehalten werden, werden übersprungen und protokolliert, statt den Lauf zu blockieren. Regeln sind reines JSON unter `src/WinTools.Infrastructure/Rules/`; neue Pfade brauchen keine Codeänderung.

<details>
<summary>Screenshot</summary>

![Bereinigung](../public/screenshots/cleanup.png)

</details>

### Optimierung

Datenschutz und Telemetrie, Leistung, Datei-Explorer, Systemverhalten, Netzwerk, Updates, Spiele.

Jeder Eintrag unterstützt Erkennen, Anwenden und Zurücknehmen, sodass der aktuelle Zustand vorher sichtbar ist. Empfohlene Einträge beschränken sich auf gut belegte Wirkungen.

<details>
<summary>Screenshot</summary>

![Optimierung](../public/screenshots/optimize.png)

</details>

### Funktionen

- **Systemfunktionen**: listet alle optionalen Features über die DISM-API, nach Zweck gruppiert, mit Beschreibung und aktuellem Zustand; Änderungen werden gesammelt angewendet.
- **Editionskanal**: liest SKU, Version und Aktivierungsstatus. Der Wechsel nutzt den systemeigenen `changepk`-Ablauf mit generischem Schlüssel und aktiviert Windows nicht.

<details>
<summary>Screenshot</summary>

![Systemfunktionen](../public/screenshots/features-systemfeatures.png)

</details>

### Updates

- **Installierte Updates**: auflisten und einzeln deinstallieren.
- **Verfügbare Updates**: Windows-Update-Suche manuell starten, mit Kategorie, KB, Größe und Datum, Sammelinstallation und Ergebnistext.
- **Feature-Flags**: `FeatureManagement\Overrides` nach ID verwalten, mit Zustand, Priorität und Variante sowie Import, Export und Zurücksetzen.

<details>
<summary>Screenshot</summary>

![Verfügbare Updates](../public/screenshots/updates-available.png)

</details>

### Autostart

Alles auf einer Seite. Echte Programmsymbole, Schalter für den Zustand. Dienste ändern ihren Starttyp direkt in der Zeile (Automatisch, Automatisch verzögert, Manuell, Deaktiviert).

<details>
<summary>Screenshot</summary>

![Autostart](../public/screenshots/startup-services.png)

</details>

### Komponenten

- **Sicherheitscenter**: 14 Einstellungen zu Echtzeitschutz, Cloudschutz, Verhaltensüberwachung, PUA, Netzwerkschutz, überwachtem Ordnerzugriff, Speicherintegrität, UAC, SmartScreen, den drei Firewall-Profilen und Ausschlüssen.
- **Systemkomponenten**: integrierte Apps (Terminal, Rechner, Editor, Paint, Fotos und weitere) installieren, reparieren, entfernen; WSL-Distributionen; Android-Subsystem aus lokaler Datei; Store-Reparatur.

<details>
<summary>Screenshot</summary>

![Sicherheitscenter](../public/screenshots/components-securitycenter.png)

</details>

### Runtimes

Listet installierte .NET- und Visual-C++-Runtimes. Verfügbare Versionen werden bei Bedarf geladen: .NET aus der offiziellen `releases-index.json` mit SHA512-Prüfung, VC++ über `aka.ms`-Links mit Authenticode-Prüfung. Beide installieren still und lassen sich deinstallieren.

<details>
<summary>Screenshot</summary>

![Runtimes](../public/screenshots/runtimes-dotnet.png)

</details>

### Apps

Zeigt Symbole, Versionen, geschätzte Größe und Installationsangaben. Bei normalen Programmen wird das mitgelieferte Deinstallationsprogramm gestartet; Appx-Pakete verwaltet Windows mit seinen eigenen Paketwerkzeugen.

<details>
<summary>Screenshot</summary>

![Apps](../public/screenshots/apps-programs.png)

</details>

### Treiber

Verwaltet den gesamten DriverStore. Beliebig viele Pakete auswählen und sichern, entfernen oder aus einem Ordner installieren. Sammelläufe zeigen Fortschritt je Eintrag und eine Zusammenfassung.

<details>
<summary>Screenshot</summary>

![Treiber](../public/screenshots/drivers-systemdrivers.png)

</details>

### Sicherung

Das Quellvolume wird per Dropdown gewählt; das Abbildformat (wim / esd / swm) bestimmt Endung und Kompression; die Teilgröße gilt nur für swm; vor dem Erstellen wird die Standardausschlussliste angezeigt. Beim Anwenden lässt sich das Abbild prüfen und der Bootloader mit `bcdboot` reparieren.

<details>
<summary>Screenshot</summary>

![Sicherung](../public/screenshots/backup-capture.png)

</details>

## Build

Erfordert das .NET 10 SDK.

```powershell
# Debug-Build
dotnet build src/WinTools.App/WinTools.App.csproj

# Release-Build
dotnet build src/WinTools.App/WinTools.App.csproj -c Release

# Beide Pakete unter artifacts/publish/{portable,framework} erzeugen
powershell -ExecutionPolicy Bypass -File tools/publish.ps1 -Profile both
```

Architektur, Modulinterna, Regelformat, Lokalisierung und Debugging stehen unter **[wtools.opkg.cn](https://wtools.opkg.cn/)** oder in [docs/guide/development.md](../guide/development.md).

## Referenzen

| Projekt | Übernommen |
| --- | --- |
| [winutil](https://github.com/ChrisTitusTech/winutil) | Gruppierung der Tweaks, Auswählen-dann-Anwenden |
| [Optimizer](https://github.com/hellzerg/optimizer) | Kategorien, Formulierung der Risiken, Aufbau von Autostart und Diensten |
| [ZyperWinOptimize](https://github.com/ZyperRap/ZyperWinOptimize) | Tweaks und Informationsdichte für chinesische Nutzer |
| [Dism-Multi-language](https://github.com/Chuyu-Team/Dism-Multi-language) | DISM-Nutzung und mehrsprachige Ressourcen |
| [Geek Uninstaller](https://geekuninstaller.com/) | Feldauswahl der Programmliste |
| [ViVeTool](https://github.com/thebookisclosed/ViVe) | Datenmodell der Feature-Flags und Drei-Zustands-Logik |

Die Bereinigungsregeln begannen als private Liste von Cache-Pfaden und wurden seither ergänzt. Gepflegt in `tools/rules/*.txt`, erzeugt von `tools/Generate-CleanupRules.ps1`.

## Haftungsausschluss

- Das Werkzeug ändert Registry, Dienststarttypen, Systemfunktionen und Treiber. Vorher einen Wiederherstellungspunkt setzen oder wichtige Daten sichern.
- Für Datenverlust oder Systemschäden durch die Nutzung wird keine Haftung übernommen.
- Nicht verwenden, um Lizenzierung oder Aktivierung zu umgehen oder geltendes Recht zu verletzen.

## Lizenz

[AGPL-3.0](../../LICENSE) © WinTools

### Drittanbieter-Komponenten

| Komponente | Lizenz | Zweck |
| --- | --- | --- |
| [Maple Mono NF CN](https://github.com/subframe7536/maple-font) | SIL OFL 1.1 | UI-Schrift, als TTF eingebettet |
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) 8.4.2 | MIT | MVVM-Quellgeneratoren |
| [Microsoft.Extensions.DependencyInjection](https://github.com/dotnet/runtime) 10.0.13 | MIT | Dependency Injection |
| [VitePress](https://vitepress.dev/) | MIT | Nur für die Dokumentationsseite |
