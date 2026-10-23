<div align="center">

<img src="docs/public/logo.png" width="96" height="96" alt="WinTools" />

# WinTools

**一个干净、快速、中文优先的 Windows 系统工具箱——清理、优化、功能、更新、运行、组件、环境、应用、驱动、备份还原，十项一站完成。**

[![Platform](https://img.shields.io/badge/Windows-10%20%2F%2011-0078D4?logo=windows&logoColor=white)](https://www.microsoft.com/windows)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com)
[![Version](https://img.shields.io/badge/version-0.1.0-orange)](https://github.com/PIKACHUIM/WinTools/releases)
[![License](https://img.shields.io/badge/license-AGPL--3.0-blue)](LICENSE)
[![Docs](https://img.shields.io/badge/docs-online-41B883?logo=vitepress&logoColor=white)](https://pikachuim.github.io/WinTools/)
[![Docs Build](https://github.com/PIKACHUIM/WinTools/actions/workflows/docs.yml/badge.svg)](https://github.com/PIKACHUIM/WinTools/actions/workflows/docs.yml)

</div>

![WinTools 概览](docs/public/screenshots/dashboard.png)

## 简介

WinTools 是一款面向 Windows 10/11 的系统工具箱，把日常维护中最常用的十类操作集中在一个界面里：**清理垃圾、系统优化、系统功能管理、更新管理、运行管理、组件管理、运行环境、应用管理、驱动管理、备份还原**。

设计上做了三个取舍：

- **快**：WPF 原生实现，框架依赖包 22 MB（其中 20 MB 是内置界面字体），热启动约 0.7 秒；列表全部虚拟化，三百多项服务、上百个驱动都能秒开。
- **稳**：所有破坏性操作走统一的审计日志（`%ProgramData%\WinTools\logs`），驱动删除、更新卸载、版本切换等高风险动作都保留系统原生向导或明确风险提示。
- **干净**：不装服务、不写驱动、不加计划任务、不驻留后台；绿色单文件版本删掉即彻底移除。

界面支持 **简体中文、繁體中文、English、日本語、한국어、Deutsch、Français、Español、Português、Italiano、Русский、العربية** 共 12 种语言，并提供明暗双主题。

## 下载

  版本   体积   适用场景   下载  
  ---   ---   ---   ---  
  **便携单文件**   ~73 MB   自带 .NET 运行时，解压即用，适合放到 U 盘 / PE 环境   [WinTools.exe](https://github.com/PIKACHUIM/WinTools/releases/latest)  
  **框架依赖版**   ~22 MB   体积小，需要机器已安装 [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)   [WinTools-framework.zip](https://github.com/PIKACHUIM/WinTools/releases/latest)  

- 程序以**管理员权限**运行（清单内已声明），因为涉及 DISM、驱动、服务、WIM 等系统级操作。
- 当前版本仅提供 `win-x64`，`arm64` 已预留构建参数。
- 哈希与更新日志见 [Releases](https://github.com/PIKACHUIM/WinTools/releases)。

## 功能介绍

  #   模块   能做什么  
  ---   ---   ---  
  1   **垃圾清理**   打开即显示各磁盘占用，点一次「扫描」全盘分析（19 类 / 1628 条规则：系统临时、更新缓存、日志、浏览器缓存、开发缓存、转储等），结果按分类折叠展示，按大小与安全等级勾选后一键清理  
  2   **系统优化**   51 项优化分 10 个分类（隐私与遥测、性能与响应、资源管理器、系统行为、网络、更新、游戏、任务栏、账户与安全、开机引导），支持按分类全选、一键「全选」与「推荐项」，应用前自动检测现状，全部可回滚  
  3   **功能管理**   **系统功能**：按用途分组折叠展示 135 项可选功能，带中文说明与开关，改完统一「应用」；**版本渠道切换**：读取当前 SKU、版本与激活状态，输入产品密钥调用系统向导切换版本  
  4   **更新管理**   **已安装更新**：按 KB 号检索历史更新并可卸载；**可安装补丁**：在线扫描、勾选安装；**功能开关**：ViVeTool 式的 `FeatureManagement\Overrides` 读写，支持三态与自定义特性 ID  
  5   **运行管理**   **启动项**：Run / RunOnce / 启动文件夹，启用状态与任务管理器一致（写 StartupApproved）；**计划任务**：递归枚举全部任务，可启停、立即运行、删除；**服务管理**：查看状态并调整启动类型，避免盲目停用系统服务  
  6   **组件管理**   **安全中心**：Defender 实时保护 / 行为监控 / 篡改保护状态，排除项增删；**应用商店**：组件健康检查、缓存重置、重新注册  
  7   **运行环境**   **.NET 环境**：按主版本列出 Desktop / Runtime / ASP.NET Core（x64 与 x86）；**VC++ 环境**：枚举 2005–2022 各版本运行库，支持静默卸载  
  8   **应用管理**   **已安装程序**：注册表 Uninstall 键（含 WOW6432Node 与当前用户），展示发布者、版本、占用空间，优先静默卸载；**Appx**：列出 UWP/MSIX 应用并移除  
  9   **驱动管理**   **系统驱动**：DriverStore 全量枚举（名称、提供者、类、版本、日期）；**备份驱动**：一键导出到目录；**安装驱动**：从目录或 inf 安装；**删除驱动**：按发布名删除并卸载  
  10   **备份还原**   **系统备份**：捕获分区为 wim / esd / swm，可设置映像名称、描述与压缩级别；**增量备份**：向已有映像追加；**WIM 信息**：查看索引与属性；**释放镜像**：把 wim/esd/swm 应用到目标目录  

<details>
<summary>更多截图</summary>

  垃圾清理   系统优化  
  :---:   :---:  
  ![垃圾清理](docs/public/screenshots/cleanup.png)   ![系统优化](docs/public/screenshots/optimize.png)  

  功能管理   更新管理  
  :---:   :---:  
  ![功能管理](docs/public/screenshots/features-systemfeatures.png)   ![更新管理](docs/public/screenshots/updates-available.png)  

  运行管理   组件管理  
  :---:   :---:  
  ![运行管理](docs/public/screenshots/startup-services.png)   ![组件管理](docs/public/screenshots/components-securitycenter.png)  

  运行环境   应用管理  
  :---:   :---:  
  ![运行环境](docs/public/screenshots/runtimes-dotnet.png)   ![应用管理](docs/public/screenshots/apps-programs.png)  

  驱动管理   备份还原  
  :---:   :---:  
  ![驱动管理](docs/public/screenshots/drivers-systemdrivers.png)   ![备份还原](docs/public/screenshots/backup-capture.png)  

![深色主题](docs/public/screenshots/dashboard-dark.png)

</details>

## 技术特点

  项   说明  
  ---   ---  
  技术栈   WPF + CommunityToolkit.Mvvm + Microsoft.Extensions.DependencyInjection，`net10.0-windows`  
  依赖   仅 3 个 NuGet 包；界面全部手写 ControlTemplate，不依赖第三方 UI 框架  
  系统能力   一律走 P/Invoke、COM 后期绑定（WUA / 计划任务 / WMI）与注册表，不引入 WinRT 投影与 `System.Management`  
  启动   启动阶段有打点（`WINTOOLS_TRACE=1`），语言包按需加载；热启动约 0.7 s（其中 WPF 首次建窗约 0.37 s 为框架下限）  
  体积   框架依赖包 2.1 MB / 21 文件；便携单文件 64 MB（自包含 + 压缩 + R2R）  
  长列表   `ListBox` + `VirtualizingStackPanel`（回收模式），307 个服务、155 个驱动流畅滚动  
  审计   破坏性操作写 JSONL 审计日志与运行日志，路径 `%ProgramData%\WinTools\logs`  
  菜单   数据驱动：`Rules/modules.json` 定义一级/二级菜单与 12 语言标题，新增模块无需改外壳代码  

## 环境要求

- Windows 10 1809（17763）及以上 / Windows 11，x64
- 框架依赖版需要 [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
- 部分功能依赖系统组件：DISM（功能管理、镜像转换）、WUA（更新管理）、`pnputil`（驱动管理）、WIMGAPI（备份还原）

## 从源码构建

```powershell
# 需要 .NET 10 SDK
dotnet build src/WinTools.App/WinTools.App.csproj -c Release

# 生成两种发行包 → artifacts/publish/{portable,framework}
powershell -ExecutionPolicy Bypass -File tools/publish.ps1 -Profile both
```

开发文档（架构、模块实现、规则格式、本地化、调试技巧）见 **[文档站](https://pikachuim.github.io/WinTools/)** 或 [docs/guide/development.md](docs/guide/development.md)。

## 参考项目

界面交互与功能取舍参考了这些优秀的开源项目，在此致谢：

  项目   借鉴点  
  ---   ---  
  [winutil](https://github.com/ChrisTitusTech/winutil)（ChrisTitusTech）   优化项的组织方式、配置预设思路  
  [Optimizer](https://github.com/hellzerg/optimizer)（hellzerg）   系统优化的条目划分与风险提示  
  [ZyperWinOptimize](https://github.com/ZyperRap/ZyperWinOptimize)   中文场景下的优化项与界面组织  
  [Dism-Multi-language](https://github.com/Chuyu-Team/Dism-Multi-language)（Chuyu-Team）   DISM 使用方式与多语言资源  
  [geek-uninstaller](https://geekuninstaller.com/)   已安装程序列表与卸载交互  
  [ViVeTool](https://github.com/thebookisclosed/ViVe)   特性开关（Feature Flags）的数据结构与三态语义  

清理规则由 `tools/rules/*.txt` 维护、`tools/Generate-CleanupRules.ps1` 生成，共 19 个分类 / 1628 条（覆盖 150+ 款软件的缓存目录与系统垃圾）。最初的 105 条种子清单保留在 `tools/rules/legacy/`。

## 免责声明

- 本工具会修改注册表、服务启动类型、系统功能与驱动，请在使用前**创建还原点或备份重要数据**。
- 作者不对因使用本工具造成的任何数据丢失或系统损坏负责。
- 请勿将本工具用于绕过软件许可、激活或其它违反当地法律法规的用途。

## 许可证

[AGPL-3.0](LICENSE) © WinTools
