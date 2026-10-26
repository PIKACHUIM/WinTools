<p align="center">
  <a href="../../README.md">简体中文</a> · <a href="README.zh-TW.md">繁體中文</a> · <b>English</b> · <a href="README.ja.md">日本語</a> · <a href="README.ko.md">한국어</a> · <a href="README.de.md">Deutsch</a><br/>
  <a href="README.fr.md">Français</a> · <a href="README.es.md">Español</a> · <a href="README.pt.md">Português</a> · <a href="README.it.md">Italiano</a> · <a href="README.ru.md">Русский</a> · <a href="README.ar.md">العربية</a>
</p>

<p align="center">
  <img src="../public/logo.png" width="88" height="88" alt="WinTools" />
</p>

<h1 align="center">WinTools</h1>

<p align="center">
  <a href="https://github.com/PIKACHUIM/WinTools/releases"><img src="https://img.shields.io/badge/version-0.1.0-orange" alt="Version"></a>
  <a href="https://www.microsoft.com/windows"><img src="https://img.shields.io/badge/Windows-10%20%2F%2011-0078D4" alt="Platform"></a>
  <a href="https://dotnet.microsoft.com"><img src="https://img.shields.io/badge/.NET-10.0-512BD4" alt=".NET"></a>
  <a href="https://github.com/PIKACHUIM/WinTools/blob/main/LICENSE"><img src="https://img.shields.io/badge/license-AGPL--3.0-blue" alt="License"></a>
  <a href="https://github.com/PIKACHUIM/WinTools/actions/workflows/build.yml"><img src="https://github.com/PIKACHUIM/WinTools/actions/workflows/build.yml/badge.svg" alt="Build"></a>
  <a href="https://wtools.opkg.cn/"><img src="https://img.shields.io/badge/docs-online-41B883" alt="Docs"></a>
</p>

## Introduction

WinTools puts common system maintenance into one window: clearing caches and update leftovers, turning off telemetry you do not need, removing a problematic update, checking what starts with the system, installing missing runtimes, exporting old drivers, and capturing an image before reinstalling.

A WPF desktop application that runs entirely locally. No service, no driver, no startup entry, no network access. Close the window and it is gone.

Ten modules: **Cleanup, Optimize, Features, Updates, Startup, Components, Runtimes, Apps, Drivers, Backup.**

## Requirements

- Windows 10 1809 (17763) or later, or Windows 11, x64
- The framework-dependent build needs [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
- Administrator rights (declared in the manifest); DISM, drivers, services and imaging all need it
- Some features call system components shipped with Windows: DISM, Windows Update Agent, `pnputil`, WIMGAPI. They may be missing on trimmed images

## Download

| Build | Size | Notes | Link |
| --- | --- | --- | --- |
| Portable | ~70 MB | Self-contained, unzip and run | [WinTools-*-portable-win-x64.exe](https://github.com/PIKACHUIM/WinTools/releases/latest) |
| Framework-dependent | ~22 MB | Needs .NET 10 Desktop Runtime | [WinTools-*-framework-win-x64.zip](https://github.com/PIKACHUIM/WinTools/releases/latest) |
| Beta | same | Rebuilt on every push to `main` | [releases/tag/beta](https://github.com/PIKACHUIM/WinTools/releases/tag/beta) |

Release builds carry a Certum cloud signature and timestamp; verify with `Get-AuthenticodeSignature`. Each release ships a `SHA256SUMS.txt`.

## Features

### Overview

System version and build number, CPU and memory usage, free space per disk, recent activity. All read locally.

<details>
<summary>Screenshot</summary>

![Overview](../public/screenshots/dashboard.png)

</details>

### Cleanup

Scans whole disks and covers system temp folders, update cache, logs and dumps, browser caches, developer caches (npm, pip, NuGet, Gradle, Docker) and the cache directories of 150+ applications.

Results are grouped by category with size and safety level. Files held by running processes are skipped and recorded instead of stalling the run. Rules are plain JSON under `src/WinTools.Infrastructure/Rules/`; adding a path needs no code change.

<details>
<summary>Screenshot</summary>

![Cleanup](../public/screenshots/cleanup.png)

</details>

### Optimize

Privacy and telemetry, performance, File Explorer, system behaviour, network, updates, gaming.

Every entry supports detect, apply and rollback, so the current state can be checked before changing anything. Recommended items are limited to entries with well-known effects.

<details>
<summary>Screenshot</summary>

![Optimize](../public/screenshots/optimize.png)

</details>

### Features

- **System features**: lists every optional feature through the DISM API, grouped by purpose, with descriptions and current state; changes are applied in one batch.
- **Edition channel**: reads the current SKU, version and activation state. Switching editions uses the built-in `changepk` flow with a generic key and does not activate Windows.

<details>
<summary>Screenshot</summary>

![System features](../public/screenshots/features-systemfeatures.png)

</details>

### Updates

- **Installed updates**: enumerate and uninstall individual updates.
- **Available updates**: manually trigger a Windows Update search showing category, KB, size and date, with batch install and a result summary.
- **Feature flags**: manage `FeatureManagement\Overrides` by ID with state, priority and variant, plus import, export and reset.

<details>
<summary>Screenshot</summary>

![Available updates](../public/screenshots/updates-available.png)

</details>

### Startup

All on one page. Real program icons, switches for enable state. Services can change their start type inline (Automatic, Automatic delayed, Manual, Disabled).

<details>
<summary>Screenshot</summary>

![Startup](../public/screenshots/startup-services.png)

</details>

### Components

- **Security center**: 14 settings covering real-time protection, cloud protection, behaviour monitoring, PUA, network protection, controlled folder access, memory integrity, UAC, SmartScreen, the three firewall profiles, and exclusions.
- **System components**: built-in apps (Terminal, Calculator, Notepad, Paint, Photos and more) with install, repair and uninstall; WSL distributions; Android subsystem from a local package; Store repair.

<details>
<summary>Screenshot</summary>

![Security center](../public/screenshots/components-securitycenter.png)

</details>

### Runtimes

Lists installed .NET and Visual C++ runtimes. Available versions are fetched on demand: .NET from the official `releases-index.json` with SHA512 verification, VC++ from `aka.ms` links with Authenticode verification. Both install silently and can be uninstalled.

<details>
<summary>Screenshot</summary>

![Runtimes](../public/screenshots/runtimes-dotnet.png)

</details>

### Apps

Shows icon, version, size, install date and recent activity. Uninstall runs the vendor's own uninstaller; packages go through the deployment API.

<details>
<summary>Screenshot</summary>

![Apps](../public/screenshots/apps-programs.png)

</details>

### Drivers

Manages the whole DriverStore. Select any number of packages and back them up, remove them, or install from a folder. Batch runs show per-item progress and a result summary.

<details>
<summary>Screenshot</summary>

![Drivers](../public/screenshots/drivers-systemdrivers.png)

</details>

### Backup

Source volume is chosen from a dropdown; the image format (wim / esd / swm) drives the extension and compression; split size applies to swm only; a default exclusion list is shown before capture. Apply can verify the image and repair the bootloader with `bcdboot`.

<details>
<summary>Screenshot</summary>

![Backup](../public/screenshots/backup-capture.png)

</details>

## Build

Requires the .NET 10 SDK.

```powershell
# Debug build
dotnet build src/WinTools.App/WinTools.App.csproj

# Release build
dotnet build src/WinTools.App/WinTools.App.csproj -c Release

# Produce both packages under artifacts/publish/{portable,framework}
powershell -ExecutionPolicy Bypass -File tools/publish.ps1 -Profile both
```

Architecture notes, module internals, rule format, localization and debugging tips are at **[wtools.opkg.cn](https://wtools.opkg.cn/)** or in [docs/guide/development.md](../guide/development.md).

## References

| Project | What was borrowed |
| --- | --- |
| [winutil](https://github.com/ChrisTitusTech/winutil) | Tweak grouping, select-then-apply flow |
| [Optimizer](https://github.com/hellzerg/optimizer) | Tweak categories, risk wording, startup and service layout |
| [ZyperWinOptimize](https://github.com/ZyperRap/ZyperWinOptimize) | Tweak list and information density for Chinese users |
| [Dism-Multi-language](https://github.com/Chuyu-Team/Dism-Multi-language) | DISM usage and multilingual resource layout |
| [Geek Uninstaller](https://geekuninstaller.com/) | Fields shown in the installed programs list |
| [ViVeTool](https://github.com/thebookisclosed/ViVe) | Feature flag data model and three-state semantics |

Cleanup rules started from a personal list of cache paths and were extended over time. They are maintained as `tools/rules/*.txt` and generated by `tools/Generate-CleanupRules.ps1`.

## Disclaimer

- The tool modifies the registry, service start types, system features and drivers. Create a restore point or back up important data first.
- The author is not liable for data loss or system damage caused by using this tool.
- Do not use it to bypass software licensing, activation, or any purpose that violates local law.

## License

[AGPL-3.0](../../LICENSE) © WinTools

### Third-party components

| Component | License | Used for |
| --- | --- | --- |
| [Maple Mono NF CN](https://github.com/subframe7536/maple-font) | SIL OFL 1.1 | UI font, embedded as TTF |
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) 8.4.2 | MIT | MVVM source generators |
| [Microsoft.Extensions.DependencyInjection](https://github.com/dotnet/runtime) 10.0.13 | MIT | Dependency injection |
| [VitePress](https://vitepress.dev/) | MIT | Documentation site only |
