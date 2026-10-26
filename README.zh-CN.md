<p align="center">
  <a href="README.md">English</a> · <b>简体中文</b> · <a href="README.zh-TW.md">繁體中文</a><br/>
  <sub>Windows 系统工具箱 · 清理 / 优化 / 组件 / 驱动 / 备份</sub>
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

## 简介

WinTools 把常见的系统维护操作放进一个窗口：清缓存和更新残留、关掉不需要的遥测、卸载出问题的补丁、查看开机加载了哪些东西、补装运行库、导出旧驱动、重装前打个镜像。

WPF 桌面程序，纯本地运行。不装服务、不加载驱动、不加开机自启、不联网，关掉窗口进程就结束。

十个模块：**垃圾清理、系统优化、功能管理、更新管理、运行管理、组件管理、运行环境、应用管理、驱动管理、备份还原**。

## 系统要求

- Windows 10 1809（17763）及以上，或 Windows 11，x64
- 框架依赖版需要 [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
- 需要管理员权限（清单里已声明），DISM、驱动、服务、镜像操作都依赖它
- 部分功能调用系统自带组件：DISM、Windows Update Agent、`pnputil`、WIMGAPI。精简版系统上可能已被移除

## 下载

| 版本 | 体积 | 说明 | 下载 |
| --- | --- | --- | --- |
| 便携版 | 约 70 MB | 自带运行时，解压即用 | [WinTools-*-portable-win-x64.exe](https://github.com/PIKACHUIM/WinTools/releases/latest) |
| 框架依赖版 | 约 22 MB | 需要 .NET 10 Desktop Runtime | [WinTools-*-framework-win-x64.zip](https://github.com/PIKACHUIM/WinTools/releases/latest) |
| Beta | 同上 | 每次推送到 `main` 自动重建 | [releases/tag/beta](https://github.com/PIKACHUIM/WinTools/releases/tag/beta) |

正式版带 Certum 云签名与时间戳，可用 `Get-AuthenticodeSignature` 校验。每个版本附带 `SHA256SUMS.txt`。

## 功能

### 系统概览

系统版本与内部版本号、CPU 与内存占用、各磁盘剩余空间、近期活动。数据全部本地读取。

![系统概览](docs/public/screenshots/dashboard.png)

### 垃圾清理

按磁盘扫描，23 个分类、1623 条规则，覆盖系统临时目录、更新缓存、日志与转储、浏览器缓存、开发工具缓存（npm、pip、NuGet、Gradle、Docker）以及 150 多款软件的缓存目录。

结果按分类折叠，标出大小和安全等级。被进程占用的文件会跳过并记录，不会卡住整轮清理。规则是纯 JSON，放在 `src/WinTools.Infrastructure/Rules/`，加路径不用改代码。

![垃圾清理](docs/public/screenshots/cleanup.png)

### 系统优化

153 条优化项，分 7 组：隐私与遥测、性能与响应、资源管理器、系统行为、网络、更新、游戏。

每条都支持检测、应用、回滚，改之前可以先看当前状态。推荐项只包含副作用明确的条目。

![系统优化](docs/public/screenshots/optimize.png)

### 功能管理

- **系统功能**：通过 DISM API 列出全部可选功能，按用途分组，带说明与当前状态，改完批量应用。
- **系统渠道**：读取当前 SKU、版本号与激活状态。切换版本走系统自带的 `changepk`，使用内置通用密钥，不会激活系统。

![系统功能](docs/public/screenshots/features-systemfeatures.png)

### 更新管理

- **已安装更新**：枚举并卸载单条更新。
- **可安装补丁**：手动触发 Windows Update 搜索，显示分类、KB、体积与日期，支持批量安装并给出结果汇总。
- **功能开关**：按 ID 管理 `FeatureManagement\Overrides`，含状态、优先级、变体，支持导入、导出与重置。

![可安装补丁](docs/public/screenshots/updates-available.png)

### 运行管理

启动项、计划任务、服务集中在一页。显示程序真实图标，启用状态用开关控制。服务可以行内切换启动类型（自动、延迟自动、手动、禁用）。

![运行管理](docs/public/screenshots/startup-services.png)

### 组件管理

- **安全中心**：14 项设置，含实时保护、云保护、行为监控、PUA、网络保护、受控文件夹、内存完整性、UAC、SmartScreen、三个防火墙配置文件，以及排除项。
- **系统组件**：内置应用（终端、计算器、记事本、画图、照片等）的安装、修复与卸载；WSL 发行版；安卓子系统本地包安装；应用商店修复。

![安全中心](docs/public/screenshots/components-securitycenter.png)

### 运行环境

列出已安装的 .NET 与 Visual C++ 运行时。可安装版本按需拉取：.NET 读官方 `releases-index.json` 并校验 SHA512，VC++ 用 `aka.ms` 链接并校验 Authenticode。两者都静默安装，也能卸载。

![运行环境](docs/public/screenshots/runtimes-dotnet.png)

### 应用管理

已安装程序与应用包，显示图标、版本、占用空间、安装日期与最近活动。卸载调用软件自带的卸载程序，应用包走部署接口。

![应用管理](docs/public/screenshots/apps-programs.png)

### 驱动管理

整页管理 DriverStore。勾选任意数量的驱动包后备份、删除，或从目录安装。批量操作有逐项进度和结果汇总。

![驱动管理](docs/public/screenshots/drivers-systemdrivers.png)

### 备份还原

WIM 捕获与释放。源分区用下拉选择，映像格式（wim / esd / swm）联动扩展名与压缩方式，分卷大小只对 swm 生效，捕获前会列出默认排除项。释放时可校验映像，并用 `bcdboot` 修复引导。

![备份还原](docs/public/screenshots/backup-capture.png)

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

架构说明、模块实现、规则格式、本地化与调试技巧见 **[wtools.opkg.cn](https://wtools.opkg.cn/)** 或 [docs/guide/development.md](docs/guide/development.md)。

## 参考项目

| 项目 | 借鉴内容 |
| --- | --- |
| [winutil](https://github.com/ChrisTitusTech/winutil) | 优化项分组、先选后应用的交互 |
| [Optimizer](https://github.com/hellzerg/optimizer) | 优化分类、风险提示措辞、启动项与服务组织 |
| [ZyperWinOptimize](https://github.com/ZyperRap/ZyperWinOptimize) | 中文环境下的优化清单与信息密度 |
| [Dism-Multi-language](https://github.com/Chuyu-Team/Dism-Multi-language) | DISM 用法与多语言资源组织 |
| [Geek Uninstaller](https://geekuninstaller.com/) | 已安装程序列表的字段取舍 |
| [ViVeTool](https://github.com/thebookisclosed/ViVe) | 特性开关的数据结构与三态语义 |

清理规则最初来自一份个人整理的缓存路径清单，之后陆续补齐。现在由 `tools/rules/*.txt` 维护，`tools/Generate-CleanupRules.ps1` 生成。

## 免责声明

- 工具会修改注册表、服务启动类型、系统功能与驱动。使用前请创建还原点或备份重要数据。
- 因使用本工具造成的数据丢失或系统损坏，作者不承担责任。
- 请勿用于绕过软件许可、激活或其他违反当地法律法规的用途。

## 开源许可

[AGPL-3.0](LICENSE) © WinTools
