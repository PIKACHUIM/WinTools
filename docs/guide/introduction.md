# 项目介绍

WinTools 是一个面向 Windows 10 / 11 的系统工具箱，把日常维护中最常用的十类操作收进同一个界面：**垃圾清理、系统优化、功能管理、更新管理、运行管理、组件管理、运行环境、应用管理、驱动管理、备份还原**。

![概览](/screenshots/dashboard.png)

## 使用上的几个特点

| 关注点 | 具体做法 |
| --- | --- |
| **操作效率** | 页面按需加载，长列表采用虚拟化；语言资源也按需读取。 |
| **系统兼容** | 通过 DISM、Windows Update Agent、WIMGAPI、`pnputil` 等 Windows 接口执行相关操作，不注入其他进程或安装驱动。 |
| **便于检查** | 修改系统设置前可先查看当前状态；高风险操作会提示影响，执行结果和破坏性操作会写入日志。 |
| **少留后台项** | 程序不安装服务、不添加计划任务，也不会常驻后台；退出程序后即结束运行。 |
| **多语言界面** | 提供 12 种语言，默认使用简体中文。 |

## 适用系统

- Windows 10 1809（17763）及以上
- Windows 11 全部版本
- 架构：`x64`（`arm64` 已预留构建参数）
- 框架依赖版需安装 [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)

::: tip 为什么必须要管理员权限
功能管理走 DISM、驱动管理写 DriverStore、运行管理改服务启动类型、备份还原读写分区镜像 —— 这些都要求提升权限。程序在清单里已声明 `requireAdministrator`，启动时会由系统直接提权，不需要右键"以管理员身份运行"。
:::

## 十个模块

| 模块 | 一句话说明 |
| --- | --- |
| 垃圾清理 | 按类型扫描磁盘垃圾，按大小与安全等级勾选清理 |
| 系统优化 | 声明式优化条目，勾选应用、随时回滚 |
| 功能管理 | 启用/禁用 Windows 可选功能；切换系统版本 |
| 更新管理 | 已安装补丁、可安装补丁、特性开关 |
| 运行管理 | 启动项、计划任务、服务 |
| 组件管理 | Defender 状态与排除项、应用商店修复 |
| 运行环境 | .NET / VC++ 运行库检测与卸载 |
| 应用管理 | 已安装程序、Appx 应用 |
| 驱动管理 | 查看 / 备份 / 安装 / 删除驱动 |
| 备份还原 | 捕获 wim / esd / swm，增量追加，释放镜像 |

详细说明见 [功能一览](/guide/features/)。

## 安全模型

软件把所有"可能让人后悔"的操作分成三级，在界面上用颜色区分：

| 级别 | 含义 | 例子 |
| --- | --- | --- |
| 安全 | 只删可再生成的缓存，随时可重建 | 系统临时文件、浏览器缓存 |
| 谨慎 | 会丢失配置或需重新登录 | 部分应用缓存、缩略图数据库 |
| 高风险 | 影响系统功能或需要重装软件 | 卸载更新、删除驱动、切换版本 |

执行前后都会写日志：

- 运行日志：`%ProgramData%\WinTools\logs\app-YYYYMMDD.log`
- 审计日志：`%ProgramData%\WinTools\logs\audit-YYYYMMDD.jsonl`（一行一条，含动作、目标、参数）
- 用户设置：`%ProgramData%\WinTools\settings.json`（语言、主题、窗口尺寸、上次停留页面）

## 技术栈

```
WPF + CommunityToolkit.Mvvm + Microsoft.Extensions.DependencyInjection
TargetFramework: net10.0-windows
NuGet 依赖：3 个（不含间接依赖），界面 100% 手写 ControlTemplate
```

系统能力全部通过 P/Invoke、COM 后期绑定（WUA / 计划任务 / WMI）与注册表访问，**不引入 WinRT 投影**（可省下约 24 MB 的 `Microsoft.Windows.SDK.NET`）。细节见 [开发指引](/guide/development)。

## 接下来

- 想直接使用 → [安装与首次使用](/guide/getting-started)
- 想了解每个按钮的作用 → [功能一览](/guide/features/)
- 想参与开发 → [开发指引](/guide/development)
