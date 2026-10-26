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

WinTools raccoglie la manutenzione ordinaria del sistema in un'unica finestra: pulire cache e residui degli aggiornamenti, disattivare telemetria superflua, rimuovere un aggiornamento problematico, controllare cosa parte all'avvio, installare runtime mancanti, esportare driver datati, creare un'immagine prima di reinstallare.

Applicazione WPF, completamente locale. Nessun servizio, nessun driver, nessun avvio automatico, nessuna connessione di rete. Chiudendo la finestra il processo termina.

Dieci moduli: **Pulizia, Ottimizzazione, Funzionalità, Aggiornamenti, Avvio, Componenti, Runtime, Applicazioni, Driver, Backup.**

## Requisiti

- Windows 10 1809 (17763) o successivo, oppure Windows 11, x64
- La versione dipendente dal framework richiede [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
- Permessi di amministratore (dichiarati nel manifest); DISM, driver, servizi e immagini ne dipendono
- Alcune funzioni usano componenti di Windows: DISM, Windows Update Agent, `pnputil`, WIMGAPI. Possono mancare nelle immagini ridotte

## Download

| Versione | Dimensione | Note | Link |
| --- | --- | --- | --- |
| Portatile | ~70 MB | Runtime incluso, scompatta ed esegui | [WinTools-*-portable-win-x64.exe](https://github.com/PIKACHUIM/WinTools/releases/latest) |
| Dipendente dal framework | ~22 MB | Richiede .NET 10 Desktop Runtime | [WinTools-*-framework-win-x64.zip](https://github.com/PIKACHUIM/WinTools/releases/latest) |
| Beta | uguale | Ricompilata a ogni push su `main` | [releases/tag/beta](https://github.com/PIKACHUIM/WinTools/releases/tag/beta) |

Le versioni ufficiali hanno firma cloud Certum con marca temporale, verificabile con `Get-AuthenticodeSignature`. Ogni versione include `SHA256SUMS.txt`.

## Funzioni

Clicca un titolo per espandere il modulo; chiuso occupa una riga.

<details open>
<summary><b>Panoramica di sistema</b> — prima schermata</summary>

Versione e numero di build, uso di CPU e memoria, spazio libero per disco, attività recente. Tutti i dati sono letti localmente.

![Panoramica di sistema](../public/screenshots/dashboard.png)

</details>

<details>
<summary><b>Pulizia</b> — 23 categorie, 1623 regole</summary>

Analizza interi dischi e copre cartelle temporanee di sistema, cache degli aggiornamenti, log e dump, cache dei browser, cache degli strumenti di sviluppo (npm, pip, NuGet, Gradle, Docker) e le cartelle cache di oltre 150 applicazioni.

I risultati sono raggruppati per categoria con dimensione e livello di sicurezza. I file in uso vengono saltati e registrati invece di bloccare l'analisi. Le regole sono JSON in `src/WinTools.Infrastructure/Rules/`; aggiungere un percorso non richiede modifiche al codice.

![Pulizia](../public/screenshots/cleanup.png)

</details>

<details>
<summary><b>Ottimizzazione</b> — 153 voci, 7 gruppi</summary>

Privacy e telemetria, prestazioni, Esplora file, comportamento del sistema, rete, aggiornamenti, giochi.

Ogni voce supporta rilevamento, applicazione e ripristino, così lo stato attuale è visibile prima di ogni modifica. Le voci consigliate si limitano a effetti ben noti.

![Ottimizzazione](../public/screenshots/optimize.png)

</details>

<details>
<summary><b>Funzionalità</b> — funzionalità di sistema, canale edizione</summary>

- **Funzionalità di sistema**: elenca tutte le funzionalità facoltative tramite l'API DISM, raggruppate per uso, con descrizione e stato attuale; le modifiche vengono applicate in blocco.
- **Canale edizione**: legge SKU, versione e stato di attivazione. Il cambio usa `changepk` con chiave generica e non attiva Windows.

![Funzionalità di sistema](../public/screenshots/features-systemfeatures.png)

</details>

<details>
<summary><b>Aggiornamenti</b> — installati, disponibili, flag</summary>

- **Installati**: elenco e disinstallazione singola.
- **Disponibili**: avvia manualmente la ricerca di Windows Update con categoria, KB, dimensione e data, installazione in blocco e riepilogo.
- **Flag di funzionalità**: gestisci `FeatureManagement\Overrides` per ID con stato, priorità e variante, oltre a importazione, esportazione e ripristino.

![Aggiornamenti disponibili](../public/screenshots/updates-available.png)

</details>

<details>
<summary><b>Avvio</b> — elementi di avvio, attività pianificate, servizi</summary>

Tutto in una pagina. Icone reali dei programmi, interruttori per lo stato. I servizi cambiano il tipo di avvio nella riga stessa (Automatico, Automatico ritardato, Manuale, Disabilitato).

![Avvio](../public/screenshots/startup-services.png)

</details>

<details>
<summary><b>Componenti</b> — centro sicurezza, componenti di sistema</summary>

- **Centro sicurezza**: 14 impostazioni su protezione in tempo reale, protezione cloud, monitoraggio del comportamento, PUA, protezione di rete, accesso controllato alle cartelle, integrità della memoria, UAC, SmartScreen, i tre profili firewall e le esclusioni.
- **Componenti di sistema**: app integrate (Terminale, Calcolatrice, Blocco note, Paint, Foto e altre) da installare, riparare o rimuovere; distribuzioni WSL; sottosistema Android da pacchetto locale; riparazione dello Store.

![Centro sicurezza](../public/screenshots/components-securitycenter.png)

</details>

<details>
<summary><b>Runtime</b> — installazione online di .NET e VC++</summary>

Elenca i runtime .NET e Visual C++ installati. Le versioni disponibili vengono recuperate su richiesta: .NET dal `releases-index.json` ufficiale con verifica SHA512, VC++ dai link `aka.ms` con verifica Authenticode. Entrambi si installano in silenzio e possono essere rimossi.

![Runtime](../public/screenshots/runtimes-dotnet.png)

</details>

<details>
<summary><b>Applicazioni</b> — programmi installati, pacchetti</summary>

Mostra icona, versione, dimensione, data di installazione e ultima attività. La disinstallazione avvia il disinstallatore del produttore; i pacchetti passano dall'API di distribuzione.

![Applicazioni](../public/screenshots/apps-programs.png)

</details>

<details>
<summary><b>Driver</b> — operazioni in blocco su una pagina</summary>

Gestisce l'intero DriverStore. Seleziona qualsiasi numero di pacchetti per eseguirne il backup, rimuoverli o installarli da una cartella. Le operazioni in blocco mostrano il progresso per voce e un riepilogo.

![Driver](../public/screenshots/drivers-systemdrivers.png)

</details>

<details>
<summary><b>Backup</b> — acquisizione e applicazione WIM</summary>

Il volume di origine si sceglie da un elenco; il formato (wim / esd / swm) determina estensione e compressione; la dimensione di suddivisione vale solo per swm; prima dell'acquisizione viene mostrato l'elenco predefinito delle esclusioni. In applicazione è possibile verificare l'immagine e riparare l'avvio con `bcdboot`.

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
