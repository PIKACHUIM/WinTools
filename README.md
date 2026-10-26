<p align="center">
  <b>English</b> · <a href="README.zh-CN.md">简体中文</a> · <a href="README.zh-TW.md">繁體中文</a><br/>
  <sub>Windows system toolbox · cleanup / tweaks / components / drivers / imaging</sub>
</p>

<p align="center">
  <img src="docs/public/logo.png" width="88" height="88" alt="WinTools" />
</p>

<h1 align="center">WinTools</h1>

<p align="center">
  <a href="https://github.com/PIKACHUIM/WinTools/releases"><img src="https://img.shields.io/badge/version-0.1.0-orange" alt="Version"></a>
  <a href="https://www.microsoft.com/windows"><img src="https://img.shields.io/badge/Windows-10%20%2F%2011-0078D4" alt="Platform"></a>
  <a href="https://dotnet.microsoft.com"><img src="https://img.shields.io/badge/.NET-10.0-512BD4" alt=".NET"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-AGPL--3.0-blue" alt="License"></a>
  <a href="https://github.com/PIKACHUIM/WinTools/actions/workflows/build.yml"><img src="https://github.com/PIKACHUIM/WinTools/actions/workflows/build.yml/badge.svg" alt="Build"></a>
  <a href="https://wtools.opkg.cn/"><img src="https://img.shields.io/badge/docs-online-41B883" alt="Docs"></a>
</p>

## Introduction

WinTools puts common Windows maintenance tasks into one window: cleaning caches and update leftovers, disabling unwanted telemetry, removing a problematic update, checking what starts with the system, installing missing runtimes, exporting old drivers, and capturing an image before reinstalling.

It is a WPF application that runs locally. No background service, no driver, no scheduled task, no network calls. Close the window and nothing is left running.

Ten modules: **Cleanup, Optimize, Features, Updates, Startup, Components, Runtimes, Apps, Drivers, Backup.**

## Requirements

- Windows 10 1809 (17763) or later, or Windows 11, x64
- Framework-dependent build requires [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
- Administrator rights (declared in the manifest) for DISM, drivers, services and imaging
- Some features rely on system components shipped with Windows: DISM, Windows Update Agent, `pnputil`, WIMGAPI. They may be missing on heavily trimmed images

## Download

| Build | Size | Notes | Link |
| --- | --- | --- | --- |
| Portable | ~70 MB | Self-contained, unzip and run | [WinTools-*-portable-win-x64.exe](https://github.com/PIKACHUIM/WinTools/releases/latest) |
| Framework-dependent | ~22 MB | Needs .NET 10 Desktop Runtime | [WinTools-*-framework-win-x64.zip](https://github.com/PIKACHUIM/WinTools/releases/latest) |
| Beta | same | Rebuilt on every push to `main` | [releases/tag/beta](https://github.com/PIKACHUIM/WinTools/releases/tag/beta) |

Release builds are signed with a Certum cloud certificate and timestamped. Verify with `Get-AuthenticodeSignature`. A `SHA256SUMS.txt` is attached to each release.

## Features

### Overview

System version and build, CPU and memory usage, per-disk free space, and recent activity. All data is read locally.

![Overview](docs/public/screenshots/dashboard.png)

### Cleanup

Scans whole disks with 23 categories and 1623 rules covering system temp folders, Windows Update cache, logs and dumps, browser caches, developer caches (npm, pip, NuGet, Gradle, Docker) and the cache directories of 150+ applications.

Results are grouped by category with size and safety level. Files locked by running processes are skipped and reported. Rules are plain JSON under `src/WinTools.Infrastructure/Rules/`, so paths can be added without touching code.

![Cleanup](docs/public/screenshots/cleanup.png)

### Optimize

153 tweaks in 7 groups: privacy and telemetry, performance, File Explorer, system behaviour, network, updates, gaming.

Each entry supports detect, apply and rollback, so the current state can be checked before changing anything. Recommended items are limited to entries with well-known, low-risk effects.

![Optimize](docs/public/screenshots/optimize.png)

### Features

- **System features**: lists all optional features through the DISM API, grouped by purpose, with descriptions and current state. Changes are applied in one batch.
- **Edition channel**: reads the current SKU, version and activation state. Switching editions uses the built-in `changepk` flow with a generic key and does not activate Windows.

![System features](docs/public/screenshots/features-systemfeatures.png)

### Updates

- **Installed updates**: enumerate and uninstall individual updates.
- **Available updates**: manual search against Windows Update, with category, KB, size and date, batch install and a result summary.
- **Feature flags**: manage `FeatureManagement\Overrides` by ID with state, priority and variant, plus import, export and reset.

![Available updates](docs/public/screenshots/updates-available.png)

### Startup

Startup entries, scheduled tasks and services in one place. Entries show their real program icon and use switches instead of checkboxes. Services can change their start type inline (Automatic, Automatic delayed, Manual, Disabled).

![Startup](docs/public/screenshots/startup-services.png)

### Components

- **Security center**: 14 settings including real-time protection, cloud protection, behaviour monitoring, PUA, network protection, controlled folder access, memory integrity, UAC, SmartScreen and the three firewall profiles, plus exclusions.
- **System components**: built-in apps (Terminal, Calculator, Notepad, Paint, Photos and others) with install, repair and uninstall; WSL distributions; Android subsystem from a local package; Store repair.

![Security center](docs/public/screenshots/components-securitycenter.png)

### Runtimes

Lists installed .NET and Visual C++ runtimes. Available versions are fetched on demand: .NET from the official `releases-index.json` with SHA512 verification, VC++ from `aka.ms` links with Authenticode verification. Both install silently and can be uninstalled.

![Runtimes](docs/public/screenshots/runtimes-dotnet.png)

### Apps

Installed programs and Appx packages with icon, version, size, install date and last activity. Uninstall runs the vendor's own uninstaller; Appx packages are removed through the deployment API.

![Apps](docs/public/screenshots/apps-programs.png)

### Drivers

Single page for the whole DriverStore. Select any number of packages and back them up, remove them, or install from a folder. Every batch shows per-item progress and a result summary.

![Drivers](docs/public/screenshots/drivers-systemdrivers.png)

### Backup

WIM capture and apply. Source volume is chosen from a dropdown, the image format (wim / esd / swm) drives the file extension and compression, split size applies to swm, and a default exclusion list is shown before capture. Apply can verify the image and repair the bootloader with `bcdboot`.

![Backup](docs/public/screenshots/backup-capture.png)

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

Architecture notes, module internals, rule format and localization are documented at **[wtools.opkg.cn](https://wtools.opkg.cn/)** or in [docs/guide/development.md](docs/guide/development.md).

## References

| Project | Used for |
| --- | --- |
| [winutil](https://github.com/ChrisTitusTech/winutil) | Tweak grouping and the select-then-apply flow |
| [Optimizer](https://github.com/hellzerg/optimizer) | Tweak categories, risk wording, startup and service layout |
| [ZyperWinOptimize](https://github.com/ZyperRap/ZyperWinOptimize) | Tweak list and information density for Chinese users |
| [Dism-Multi-language](https://github.com/Chuyu-Team/Dism-Multi-language) | DISM usage and multilingual resource layout |
| [Geek Uninstaller](https://geekuninstaller.com/) | Fields shown in the installed programs list |
| [ViVeTool](https://github.com/thebookisclosed/ViVe) | Feature flag data model and three-state semantics |

Cleanup rules started from a personal list of cache paths and grew from there. They are maintained as `tools/rules/*.txt` and generated by `tools/Generate-CleanupRules.ps1`.

## Disclaimer

- The tool modifies the registry, service start types, system features and drivers. Create a restore point or back up important data first.
- The author is not responsible for data loss or system damage caused by using this tool.
- Do not use it to bypass software licensing, activation, or any other purpose that violates local law.

## License

[AGPL-3.0](LICENSE) © WinTools
