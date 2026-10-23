# 架构说明

> 本文是项目立项时的架构方案，记录了技术选型论证、分层设计与各模块底层路线。实际实现与方案的差异见每节末尾的说明。

## 一、需求还原

构建一款 Windows 系统工具箱（下称 WinTools），要求「性能优先、简洁、美观」，从零起步，工作区 `d:/Codes/UselessTools/WinToolsRepo` 目前仅有 `LICENSE`、`F盘缓存垃圾清理清单.csv`（105 条清理规则种子数据）和一个不适用的 C/C++ 风格 `.gitignore`。

完整功能蓝图（10 大模块）：

1. 垃圾清理：可配置扫描路径 + 按类型勾选扫描 + 勾选清理
2. 系统优化：按系统可选项勾选执行
3. 功能管理：Windows 可选功能、系统版本渠道切换（专业版/家庭版等）
4. 更新管理：已安装更新、可安装补丁、选择性安装、ViVe 式特性开关
5. 运行管理：启动项 / 计划任务 / 服务
6. 组件管理：安全中心、应用商店等
7. 运行环境：.NET / VC++ 运行库的安装与卸载
8. 应用管理：已安装程序、Appx
9. 驱动管理：系统驱动查看、备份、安装、删除
10. 备份还原：系统备份为 wim/esd/swm、增量备份、WIM 信息设置、镜像释放到系统

补充模块（用户已确认纳入路线图）：磁盘空间分析（Treemap）、系统修复工具箱（SFC/DISM 修复、网络重置、图标缓存重建、Hosts 编辑、右键菜单管理、休眠/虚拟内存设置）、进程/端口/硬件信息面板。

## 二、已确认的范围决策

| 决策项 | 结论 |
| --- | --- |
| 首版范围 | 分阶段：**完整架构骨架 + 垃圾清理 + 系统优化**，其余 8 模块留接口占位 |
| 技术栈 | WPF + WPF-UI（用户已选） |
| 分发形态 | 双套：绿色便携（self-contained 单文件）+ 框架依赖安装包 |
| 额外功能 | 磁盘分析、修复工具箱、进程/端口/硬件信息，全部纳入路线图 |


## 三、非功能性目标（可量化验收）

| 指标 | 目标 |
| --- | --- |
| 冷启动到可交互（Release + R2R，框架依赖版） | ≤ 350 ms（含 Mica 渲染） |
| 冷启动（便携单文件版，已解压缓存） | ≤ 500 ms |
| 首屏内存占用（工作集） | ≤ 120 MB |
| 常驻空闲 CPU | ≈ 0% |
| 扫描吞吐 | ≥ 8 万文件/秒（NVMe，多规则并行） |
| 框架依赖包体 | ≤ 12 MB（压缩后） |
| 便携单文件包体 | ≤ 100 MB |
| UI 卡顿 | 任何操作主线程阻塞 ≤ 50 ms；万级列表必须虚拟化 |


## 四、硬性约束

- 面向 Win10 1809+ / Win11，x64（预留 arm64 发布配置）
- 简体中文界面，资源结构预留多语言扩展
- 所有破坏性操作：二次确认 + 审计日志 + 可回滚/隔离区
- 严禁过度设计，能用 .NET 内建 API 就不引第三方包；首期第三方包控制在 3 个以内
- 严禁使用 `Win32_Product`（会触发 MSI 重配置）、严禁直接递归删除系统关键路径

## 一、技术栈结论

**最终选型：WPF + WPF-UI 4.x + CommunityToolkit.Mvvm + Microsoft.Extensions.DependencyInjection，目标框架 `net10.0-windows10.0.19041.0`。**

> 注：用户选择的是 .NET 9，但 .NET 9 已于 2026-05 结束支持（STS，18 个月）。本方案建议升级到 **.NET 10 LTS（支持至 2028-11）**，WPF-UI 4.x 同时支持 net8/9/10，升级零成本。

### 1.1 候选方案对比（结论依据）

| 维度 | WPF + WPF-UI | WinUI 3 + WindowsAppSDK | Avalonia 11 | Electron/Tauri |
| --- | --- | --- | --- | --- |
| 冷启动 | 200~350 ms | 500~900 ms（需 bootstrap WindowsAppSDK） | 300~500 ms | 1500 ms+ / 400 ms |
| 最小包体（依赖版） | ~8 MB | ~40 MB（含 WinAppSDK 运行时） | ~12 MB | 80 MB+ / 3 MB |
| 系统级 API 调用 | 极佳（P/Invoke、COM Interop、DllImport 直连 WIMGAPI/DISMAPI/WUA 无障碍） | 受限于打包模式与 MSIX 虚拟化，DISM/WIM 场景易踩坑 | 需自行封装，但可行 | 需 Native 桥，复杂 |
| 长列表性能（万级驱动/更新/进程） | 虚拟化成熟稳定 | ItemsRepeater 好但生态坑多 | 好 | 一般 |
| Fluent 观感 | WPF-UI 提供 Mica/Acrylic/Fluent 控件，成熟度高 | 原生最佳 | 需第三方主题 | 中 |
| NativeAOT | 不支持（WPF 无 AOT 支持） | 不支持 | 支持 | 支持 |
| 部署复杂度 | 低（xcopy/单文件） | 高（MSIX 或 unpackaged + 引导器） | 低 | 中 |
| 生态与排错 | 极成熟，20 年沉淀 | 较新，版本迭代破坏性变更多 | 中 | 高 |


**结论**：本产品的核心价值在「系统级能力 + 快速响应」，而非「跨平台」或「最新 UI 框架」。WPF 在 COM/P-Invoke 互操作、xcopy 部署、长列表虚拟化、调试可预期性上全面占优；WPF-UI 补齐 Fluent/Mica 观感，满足「简洁但美观」。WinUI 3 的包体与启动开销与用户「对启动速度有要求」直接冲突，排除。Avalonia 仅在跨平台需求出现时才值得重估。

### 1.2 依赖清单（刻意最小化）

| 包 | 用途 | 说明 |
| --- | --- | --- |
| `WPF-UI` | Fluent 控件、NavigationView、Mica、主题 | 唯一 UI 依赖 |
| `CommunityToolkit.Mvvm` | `[ObservableProperty]`、`[RelayCommand]`、`ObservableObject` | 源生成器，零运行时开销 |
| `Microsoft.Extensions.DependencyInjection` | 轻量 DI 容器 | **不引入** `Microsoft.Extensions.Hosting`（Host 启动链路额外 20~60 ms 且引入配置/日志全家桶） |
| 其余 | 全部使用 BCL | `System.Text.Json`（source-gen）、`System.IO.Pipelines`/`Channel`、`ServiceController`、`Microsoft.Win32.TaskScheduler` 不引入，直接 COM Interop |


中央包管理：`Directory.Packages.props` + `Directory.Build.props` 统一 `LangVersion=latest`、`Nullable=enable`、`TreatWarningsAsErrors`（非 UI 项目）、`InvariantGlobalization=false`（中文必需）。

### 1.3 各模块底层技术路线（全局选型表，供后续阶段直接落地）

| 模块 | 首选技术 | 备选/注意 |
| --- | --- | --- |
| 垃圾清理 | `FindFirstFileExW` + `FIND_FIRST_EX_LARGE_FETCH`、`SHFileOperation`、`RestartManager`、`MoveFileEx(DELAY_UNTIL_REBOOT)` | 不用 `Directory.EnumerateFiles`（慢 2~5 倍） |
| 系统优化 | 注册表 / `ChangeServiceConfig` / 计划任务 COM / 内嵌命令 | 声明式 JSON + C# 处理器混合 |
| Windows 功能 | `DismApi`（DISM API，需 `DismApi.dll` 动态加载） | 兜底 `dism.exe` 进程调用并解析输出 |
| 版本渠道切换 | `changepk.exe` + `Windows.ProductKey` 注册表 + `SKU` 校验 | 需管理员，操作前提示激活风险 |
| 更新管理 | WUA COM（`Microsoft.Update.Session` / `IUpdateSearcher` / `IUpdateInstaller`） | 用 `dynamic`/手写 COM 接口，避免引 WUApiLib PIA |
| 特性开关（ViVe） | `FeatureManagement\Overrides` 注册表 + 特性 ID 哈希算法复刻 | 需重启/`RtlSetFeatureUsage` |
| 启动项 | `Run`/`RunOnce`/`StartupFolder`/`StartupApproved` + 任务计划 | 必须同步写 `StartupApproved` 才是真正禁用 |
| 计划任务 | Task Scheduler COM（`ITaskService`） | 不引 `Microsoft.Win32.TaskScheduler` |
| 服务 | `ServiceController` + `ChangeServiceConfig`（StartMode） | 只改启动类型，不随意停用系统服务 |
| 组件管理 | `PackageManager`（Appx）、`Windows Defender` WMI、`DismApi` | 安全中心/商店操作需谨慎白名单 |
| 运行环境 | 内置离线安装包清单 + `VC_redist`/`dotnet-install` 校验 | 卸载走注册表 Uninstall 键 |
| 应用管理 | 注册表 `Uninstall` 键（HKLM/HKCU + WOW6432Node） | 卸载后扫残留（geek 式） |
| 驱动管理 | `pnputil.exe` / SetupAPI / `DriverStore` 枚举 | 备份用 `pnputil /export-driver` |
| 备份还原 | WIMGAPI（`WIMGAPI.dll`）—— `WIMCreateFile`/`WIMCaptureImage`/`WIMApplyImage`/`WIMSetImageInformation`/`WIMExportImage` | swm 分割用 `WIMSplitFile`；esd 用 `WIM_FLAG_COMPRESS_RECOVERY` |
| 磁盘分析 | 自研 Treemap 控件（`DrawingVisual` 直绘 + 虚拟化） | 不引图表库 |
| 进程/端口 | `NtQuerySystemInformation` 或 `Process.GetProcesses` + `GetExtendedTcpTable` | 端口映射用 IP Helper API |