<p align="center">
  <b>简体中文</b> · <a href="docs/readme/README.zh-TW.md">繁體中文</a> · <a href="docs/readme/README.en.md">English</a> · <a href="docs/readme/README.ja.md">日本語</a> · <a href="docs/readme/README.ko.md">한국어</a> · <a href="docs/readme/README.de.md">Deutsch</a><br/>
  <a href="docs/readme/README.fr.md">Français</a> · <a href="docs/readme/README.es.md">Español</a> · <a href="docs/readme/README.pt.md">Português</a> · <a href="docs/readme/README.it.md">Italiano</a> · <a href="docs/readme/README.ru.md">Русский</a> · <a href="docs/readme/README.ar.md">العربية</a>
</p>

<p align="center">
  <img src="docs/public/logo.png" width="88" height="88" alt="WinTools" />
</p>

<h1 align="center">WinTools</h1>

<p align="center">
  <a href="https://github.com/PIKACHUIM/WinTools/releases"><img src="https://img.shields.io/badge/version-0.1.0-orange" alt="版本"></a>
  <a href="https://www.microsoft.com/windows"><img src="https://img.shields.io/badge/Windows-10%20%2F%2011-0078D4" alt="平台"></a>
  <a href="https://dotnet.microsoft.com"><img src="https://img.shields.io/badge/.NET-10.0-512BD4" alt=".NET"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-AGPL--3.0-blue" alt="许可"></a>
  <a href="https://github.com/PIKACHUIM/WinTools/actions/workflows/build.yml"><img src="https://github.com/PIKACHUIM/WinTools/actions/workflows/build.yml/badge.svg" alt="构建"></a>
  <a href="https://wtools.opkg.cn/"><img src="https://img.shields.io/badge/docs-online-41B883" alt="文档"></a>
</p>

## 项目简介

WinTools 是一个 Windows 系统工具箱，把常用的维护功能收在一个程序里，共十个模块、上百项可执行操作：

**垃圾清理**（23 个分类、1623 条规则）、**系统优化**（153 条优化项）、**功能管理**（系统功能与版本渠道）、**更新管理**（补丁卸载、可安装更新、功能开关）、**运行管理**（启动项、计划任务、服务）、**组件管理**（安全中心 14 项设置、内置应用、WSL、安卓子系统）、**运行环境**（.NET 与 VC++ 在线安装）、**应用管理**（程序与应用包）、**驱动管理**（批量备份/安装/删除）、**备份还原**（WIM 捕获与释放）。

WPF 桌面程序，不在后台常驻，也不自行联网。只有搜索更新、下载运行库或查询 WSL 在线发行版时才会连接网络；关掉窗口，程序就退出。

支持**垃圾清理、系统优化、功能管理、更新管理、运行管理、组件管理、运行环境、应用管理、驱动管理、备份还原**。

## 系统要求

- Windows 10 1809（17763）及以上，或 Windows 11，x64
- 框架依赖版需要 [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
- 需要管理员权限（清单里已声明），DISM、驱动、服务、镜像操作都依赖它
- 部分功能调用系统自带组件：DISM、Windows Update Agent、`pnputil`、WIMGAPI。精简版系统上可能已被移除

## 下载地址

| 版本 | 说明 | 下载 |
| --- | --- | --- |
| 便携版 | 自带运行时，解压即用 | [WinTools-*-portable-win-x64.exe](https://github.com/PIKACHUIM/WinTools/releases/latest) |
| 框架依赖版 | 需要 .NET 10 Desktop Runtime | [WinTools-*-framework-win-x64.zip](https://github.com/PIKACHUIM/WinTools/releases/latest) |
| Beta | 每次推送到 `main` 自动重建 | [Beta 预发布](https://github.com/PIKACHUIM/WinTools/releases/tag/beta) |

各版本体积以 Releases 页面里的实际附件为准。

正式版带 Certum 云签名与时间戳，可用 `Get-AuthenticodeSignature` 校验。每个版本附带 `SHA256SUMS.txt`。

## 功能介绍

### 系统概览

打开后可以快速看系统版本、CPU 和内存占用，以及各磁盘还剩多少空间。

<details>
<summary>截图</summary>

![系统概览](docs/public/screenshots/dashboard.png)

</details>

### 垃圾清理

选择磁盘后开始扫描。目前有 23 个分类、1623 条规则，包含系统临时文件、更新缓存、日志、浏览器和开发工具缓存，以及 150 多款软件的缓存。

扫描结果按类别列出，可查看大小和安全等级，再勾选要清理的项目。正在使用的文件会跳过并记入结果。

<details>
<summary>截图</summary>

![垃圾清理](docs/public/screenshots/cleanup.png)

</details>

### 系统优化

共 153 条优化项，分为隐私与遥测、性能、资源管理器、系统、网络、更新和游戏 7 组。

每条都能先检查当前状态，再选择应用或回滚；不需要的项目可以单独跳过。

<details>
<summary>截图</summary>

![系统优化](docs/public/screenshots/optimize.png)

</details>

### 功能管理

- **系统功能**：通过 DISM API 列出全部可选功能，按用途分组，带说明与当前状态，改完批量应用。
- **系统渠道**：读取当前 SKU、版本号与激活状态。切换版本走系统自带的 `changepk`，使用内置通用密钥，不会激活系统。

<details>
<summary>截图</summary>

![系统功能](docs/public/screenshots/features-systemfeatures.png)

</details>

### 更新管理

- **已安装更新**：枚举并卸载单条更新。
- **可安装补丁**：手动触发 Windows Update 搜索，显示分类、KB、体积与日期，支持批量安装并给出结果汇总。
- **功能开关**：按 ID 管理 `FeatureManagement\Overrides`，含状态、优先级、变体，支持导入、导出与重置。

<details>
<summary>截图</summary>

![可安装补丁](docs/public/screenshots/updates-available.png)

</details>

### 运行管理

启动项、计划任务、服务集中在一页。显示程序真实图标，启用状态用开关控制。服务可以行内切换启动类型（自动、延迟自动、手动、禁用）。

<details>
<summary>截图</summary>

![运行管理](docs/public/screenshots/startup-services.png)

</details>

### 组件管理

- **安全中心**：14 项设置，含实时保护、云保护、行为监控、PUA、网络保护、受控文件夹、内存完整性、UAC、SmartScreen、三个防火墙配置文件，以及排除项。
- **系统组件**：内置应用（终端、计算器、记事本、画图、照片等）的安装、修复与卸载；WSL 发行版；安卓子系统本地包安装；应用商店修复。

<details>
<summary>截图</summary>

![安全中心](docs/public/screenshots/components-securitycenter.png)

</details>

### 运行环境

列出已安装的 .NET 与 Visual C++ 运行时。可安装版本按需拉取：.NET 读官方 `releases-index.json` 并校验 SHA512，VC++ 用 `aka.ms` 链接并校验 Authenticode。两者都静默安装，也能卸载。

<details>
<summary>截图</summary>

![运行环境](docs/public/screenshots/runtimes-dotnet.png)

</details>

### 应用管理

列表显示已安装程序和应用包的图标、版本、大小及安装信息。卸载程序时会调用软件自带的卸载程序；Appx 包通过 Windows 自带的应用包管理工具处理。

<details>
<summary>截图</summary>

![应用管理](docs/public/screenshots/apps-programs.png)

</details>

### 驱动管理

整页管理 DriverStore。勾选任意数量的驱动包后备份、删除，或从目录安装。批量操作有逐项进度和结果汇总。

<details>
<summary>截图</summary>

![驱动管理](docs/public/screenshots/drivers-systemdrivers.png)

</details>

### 备份还原

源分区用下拉选择，映像格式（wim / esd / swm）联动扩展名与压缩方式，分卷大小只对 swm 生效，捕获前会列出默认排除项。释放时可校验映像，并用 `bcdboot` 修复引导。

<details>
<summary>截图</summary>

![备份还原](docs/public/screenshots/backup-capture.png)

</details>

## 开发构建

需要 .NET 10 SDK。

```powershell
# 调试构建
dotnet build src/WinTools.App/WinTools.App.csproj

# Release 构建
dotnet build src/WinTools.App/WinTools.App.csproj -c Release

# 生成两种发行包 → artifacts/publish/{portable,framework}
powershell -ExecutionPolicy Bypass -File tools/publish.ps1 -Profile both
```

开发文档和构建说明见 **[wtools.opkg.cn](https://wtools.opkg.cn/)** 或 [docs/guide/development.md](docs/guide/development.md)。

## 参考项目

| 项目 | 借鉴内容 |
| --- | --- |
| [winutil](https://github.com/ChrisTitusTech/winutil) | 优化项分组、先选后应用的交互 |
| [Optimizer](https://github.com/hellzerg/optimizer) | 优化分类、风险提示措辞、启动项与服务组织 |
| [ZyperWinOptimize](https://github.com/ZyperRap/ZyperWinOptimize) | 中文环境下的优化清单与信息密度 |
| [Dism-Multi-language](https://github.com/Chuyu-Team/Dism-Multi-language) | DISM 用法与多语言资源组织 |
| [Geek Uninstaller](https://geekuninstaller.com/) | 已安装程序列表的字段取舍 |
| [ViVeTool](https://github.com/thebookisclosed/ViVe) | 特性开关的数据结构与三态语义 |

清理规则由 `tools/rules/*.txt` 维护，并通过 `tools/Generate-CleanupRules.ps1` 生成。

## 免责声明

- 工具会修改注册表、服务启动类型、系统功能与驱动。使用前请创建还原点或备份重要数据。
- 因使用本工具造成的数据丢失或系统损坏，作者不承担责任。
- 请勿用于绕过软件许可、激活或其他违反当地法律法规的用途。

## 开源许可

[AGPL-3.0](LICENSE) © WinTools

### 第三方组件

| 组件 | 许可 | 用途 |
| --- | --- | --- |
| [Maple Mono NF CN](https://github.com/subframe7536/maple-font) | SIL OFL 1.1 | 界面字体，以 TTF 内嵌 |
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) 8.4.2 | MIT | MVVM 源生成器 |
| [Microsoft.Extensions.DependencyInjection](https://github.com/dotnet/runtime) 10.0.13 | MIT | 依赖注入 |
| [VitePress](https://vitepress.dev/) | MIT | 仅用于文档站 |
