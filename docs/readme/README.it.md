<p align="center">
  <a href="../../README.md">简体中文</a> · <a href="README.zh-TW.md">繁體中文</a> · <a href="README.en.md">English</a> · <a href="README.ja.md">日本語</a> · <a href="README.ko.md">한국어</a> · <a href="README.de.md">Deutsch</a><br/>
  <a href="README.fr.md">Français</a> · <a href="README.es.md">Español</a> · <a href="README.pt.md">Português</a> · <b>Italiano</b> · <a href="README.ru.md">Русский</a> · <a href="README.ar.md">العربية</a>
</p>

<p align="center">
  <img src="../public/logo.png" width="88" height="88" alt="WinTools" />
</p>

<h1 align="center">WinTools</h1>

<p align="center">
  <a href="https://github.com/PIKACHUIM/WinTools/releases"><img src="https://img.shields.io/badge/version-0.1.0-orange" alt="Versione"></a>
  <a href="https://www.microsoft.com/windows"><img src="https://img.shields.io/badge/Windows-10%20%2F%2011-0078D4" alt="Piattaforma"></a>
  <a href="https://dotnet.microsoft.com"><img src="https://img.shields.io/badge/.NET-10.0-512BD4" alt=".NET"></a>
  <a href="https://github.com/PIKACHUIM/WinTools/blob/main/LICENSE"><img src="https://img.shields.io/badge/license-AGPL--3.0-blue" alt="Licenza"></a>
  <a href="https://github.com/PIKACHUIM/WinTools/actions/workflows/build.yml"><img src="https://github.com/PIKACHUIM/WinTools/actions/workflows/build.yml/badge.svg" alt="Build"></a>
  <a href="https://wtools.opkg.cn/"><img src="https://img.shields.io/badge/docs-online-41B883" alt="Documentazione"></a>
</p>

## Panoramica

Prima di reinstallare Windows conviene salvare i driver; a volte un vecchio programma richiede un runtime e la cache degli aggiornamenti continua a crescere. Per queste piccole operazioni si finisce spesso per saltare da uno strumento di sistema all'altro. WinTools riunisce le più comuni in un'unica app desktop.

È un'app WPF: non resta in esecuzione in background e non si aggiunge all'avvio. Si collega alla rete solo per cercare aggiornamenti, scaricare runtime o consultare l'elenco online delle distribuzioni WSL. Chiudi la finestra e il processo termina.

Dieci moduli: **Pulizia, Ottimizzazione, Funzionalità, Aggiornamenti, Avvio, Componenti, Runtime, Applicazioni, Driver, Backup.**

## Requisiti

- Windows 10 1809 (17763) o successivo, oppure Windows 11, x64
- La versione dipendente dal framework richiede [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
- Permessi di amministratore (dichiarati nel manifest); DISM, driver, servizi e immagini ne dipendono
- Alcune funzioni usano componenti di Windows: DISM, Windows Update Agent, `pnputil`, WIMGAPI. Possono mancare nelle immagini ridotte

## Download

| Versione | Note | Link |
| --- | --- | --- |
| Portatile | Runtime incluso, scompatta ed esegui | [WinTools-*-green-win-x64.exe](https://github.com/PIKACHUIM/WinTools/releases/latest) |
| Dipendente dal framework | Richiede .NET 10 Desktop Runtime | [WinTools-*-frame-win-x64.zip](https://github.com/PIKACHUIM/WinTools/releases/latest) |
| Beta | Ricompilata a ogni push su `main` | [Pre-release Beta](https://github.com/PIKACHUIM/WinTools/releases/tag/beta) |

Le versioni ufficiali hanno firma cloud Certum con marca temporale, verificabile con `Get-AuthenticodeSignature`. Ogni versione include `SHA256SUMS.txt`.

## Funzioni

### Panoramica di sistema

Versione e numero di build, uso di CPU e memoria, spazio libero per disco, attività recente. Tutti i dati sono letti localmente.

<details>
<summary>Schermata</summary>

![Panoramica di sistema](../public/screenshots/dashboard.png)

</details>

### Pulizia

Analizza interi dischi e copre cartelle temporanee di sistema, cache degli aggiornamenti, log e dump, cache dei browser, cache degli strumenti di sviluppo (npm, pip, NuGet, Gradle, Docker) e le cartelle cache di oltre 150 applicazioni.

I risultati sono raggruppati per categoria con dimensione e livello di sicurezza. I file in uso vengono saltati e registrati invece di bloccare l'analisi. Le regole sono JSON in `src/WinTools.Infrastructure/Rules/`; aggiungere un percorso non richiede modifiche al codice.

<details>
<summary>Schermata</summary>

![Pulizia](../public/screenshots/cleanup.png)

</details>

### Ottimizzazione

Privacy e telemetria, prestazioni, Esplora file, comportamento del sistema, rete, aggiornamenti, giochi.

Ogni voce supporta rilevamento, applicazione e ripristino, così lo stato attuale è visibile prima di ogni modifica. Le voci consigliate si limitano a effetti ben noti.

<details>
<summary>Schermata</summary>

![Ottimizzazione](../public/screenshots/optimize.png)

</details>

### Funzionalità

- **Funzionalità di sistema**: elenca tutte le funzionalità facoltative tramite l'API DISM, raggruppate per uso, con descrizione e stato attuale; le modifiche vengono applicate in blocco.
- **Canale edizione**: legge SKU, versione e stato di attivazione. Il cambio usa `changepk` con chiave generica e non attiva Windows.

<details>
<summary>Schermata</summary>

![Funzionalità di sistema](../public/screenshots/features-systemfeatures.png)

</details>

### Aggiornamenti

- **Installati**: elenco e disinstallazione singola.
- **Disponibili**: avvia manualmente la ricerca di Windows Update con categoria, KB, dimensione e data, installazione in blocco e riepilogo.
- **Flag di funzionalità**: gestisci `FeatureManagement\Overrides` per ID con stato, priorità e variante, oltre a importazione, esportazione e ripristino.

<details>
<summary>Schermata</summary>

![Aggiornamenti disponibili](../public/screenshots/updates-available.png)

</details>

### Avvio

Tutto in una pagina. Icone reali dei programmi, interruttori per lo stato. I servizi cambiano il tipo di avvio nella riga stessa (Automatico, Automatico ritardato, Manuale, Disabilitato).

<details>
<summary>Schermata</summary>

![Avvio](../public/screenshots/startup-services.png)

</details>

### Componenti

- **Centro sicurezza**: 14 impostazioni su protezione in tempo reale, protezione cloud, monitoraggio del comportamento, PUA, protezione di rete, accesso controllato alle cartelle, integrità della memoria, UAC, SmartScreen, i tre profili firewall e le esclusioni.
- **Componenti di sistema**: app integrate (Terminale, Calcolatrice, Blocco note, Paint, Foto e altre) da installare, riparare o rimuovere; distribuzioni WSL; sottosistema Android da pacchetto locale; riparazione dello Store.

<details>
<summary>Schermata</summary>

![Centro sicurezza](../public/screenshots/components-securitycenter.png)

</details>

### Runtime

Elenca i runtime .NET e Visual C++ installati. Le versioni disponibili vengono recuperate su richiesta: .NET dal `releases-index.json` ufficiale con verifica SHA512, VC++ dai link `aka.ms` con verifica Authenticode. Entrambi si installano in silenzio e possono essere rimossi.

<details>
<summary>Schermata</summary>

![Runtime](../public/screenshots/runtimes-dotnet.png)

</details>

### Applicazioni

Mostra icone, versioni, dimensioni stimate e informazioni d'installazione. Per i programmi avvia il loro disinstallatore; i pacchetti Appx sono gestiti dagli strumenti inclusi in Windows.

<details>
<summary>Schermata</summary>

![Applicazioni](../public/screenshots/apps-programs.png)

</details>

### Driver

Gestisce l'intero DriverStore. Seleziona qualsiasi numero di pacchetti per eseguirne il backup, rimuoverli o installarli da una cartella. Le operazioni in blocco mostrano il progresso per voce e un riepilogo.

<details>
<summary>Schermata</summary>

![Driver](../public/screenshots/drivers-systemdrivers.png)

</details>

### Backup

Il volume di origine si sceglie da un elenco; il formato (wim / esd / swm) determina estensione e compressione; la dimensione di suddivisione vale solo per swm; prima dell'acquisizione viene mostrato l'elenco predefinito delle esclusioni. In applicazione è possibile verificare l'immagine e riparare l'avvio con `bcdboot`.

<details>
<summary>Schermata</summary>

![Backup](../public/screenshots/backup-capture.png)

</details>

## Compilazione

Richiede l'SDK .NET 10.

```powershell
# Build di debug
dotnet build src/WinTools.App/WinTools.App.csproj

# Build Release
dotnet build src/WinTools.App/WinTools.App.csproj -c Release

# Genera i due pacchetti in artifacts/publish/{portable,framework}
powershell -ExecutionPolicy Bypass -File tools/publish.ps1 -Profile both
```

Architettura, dettagli dei moduli, formato delle regole, localizzazione e debug sono su **[wtools.opkg.cn](https://wtools.opkg.cn/)** o in [docs/guide/development.md](../guide/development.md).

## Riferimenti

| Progetto | Cosa è stato ripreso |
| --- | --- |
| [winutil](https://github.com/ChrisTitusTech/winutil) | Raggruppamento delle ottimizzazioni, flusso seleziona-e-applica |
| [Optimizer](https://github.com/hellzerg/optimizer) | Categorie, formulazione dei rischi, organizzazione di avvio e servizi |
| [ZyperWinOptimize](https://github.com/ZyperRap/ZyperWinOptimize) | Elenco di ottimizzazioni e densità informativa per il pubblico cinese |
| [Dism-Multi-language](https://github.com/Chuyu-Team/Dism-Multi-language) | Uso di DISM e organizzazione delle risorse multilingua |
| [Geek Uninstaller](https://geekuninstaller.com/) | Scelta dei campi nell'elenco dei programmi |
| [ViVeTool](https://github.com/thebookisclosed/ViVe) | Modello dati dei flag e semantica a tre stati |

Le regole di pulizia nascono da un elenco personale di percorsi cache, ampliato nel tempo. Sono mantenute in `tools/rules/*.txt` e generate da `tools/Generate-CleanupRules.ps1`.

## Avvertenze

- Lo strumento modifica registro, tipi di avvio dei servizi, funzionalità di sistema e driver. Prima dell'uso crea un punto di ripristino o esegui un backup.
- L'autore non è responsabile di perdite di dati o danni al sistema derivanti dall'uso.
- Non usarlo per aggirare licenze, attivazione o per scopi contrari alle leggi locali.

## Licenza

[AGPL-3.0](../../LICENSE) © WinTools

### Componenti di terze parti

| Componente | Licenza | Uso |
| --- | --- | --- |
| [Maple Mono NF CN](https://github.com/subframe7536/maple-font) | SIL OFL 1.1 | Font dell'interfaccia, incorporato come TTF |
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) 8.4.2 | MIT | Generatori di sorgenti MVVM |
| [Microsoft.Extensions.DependencyInjection](https://github.com/dotnet/runtime) 10.0.13 | MIT | Iniezione delle dipendenze |
| [VitePress](https://vitepress.dev/) | MIT | Solo per il sito di documentazione |
