# 竞品对照矩阵

本页记录 WinTools 与六个参考项目的逐项对照：它们有什么、WinTools 现状、缺口、以及准备怎么做。所有数据来自对各项目仓库内容的实际读取，来源列出在每节末尾。

## 一、参考项目概况

| 项目 | 类型 | 可读取的一手资料 | 对 WinTools 的主要价值 |
| --- | --- | --- | --- |
| [winutil](https://github.com/christitustech/winutil) | PowerShell 优化工具 | `config/tweaks.json`（68 项）、`config/feature.json`（33 项） | 优化项的分类、注册表/服务动作、可选功能清单、修复入口 |
| [Optimizer](https://github.com/hellzerg/optimizer) | C# 隐私与性能工具 | README 功能清单（项目已停止维护，转为 OptimizerNXT） | 隐私/服务/DNS/Hosts/启动项/命令行模板自动化 |
| [ZyperWinOptimize](https://github.com/ZyperWave/ZyperWinOptimize) | C# 国内向优化工具 | README 功能清单 | 批量勾选优化、导入导出配置、优化项介绍、Appx 管理、Edge 强力卸载 |
| [Dism-Multi-language](https://github.com/Chuyu-Team/Dism-Multi-language) | Dism++ 的规则与多语言资源 | `Data.xml`（452 KB）：清理 73 项、系统优化 163 项 | 清理规则与系统优化项的权威数据来源；功能划分参考 |
| Geek Uninstaller | 闭源卸载器 | 公开功能说明（无源码） | 程序列表字段、强制卸载、残留扫描、Store 应用 |
| [ViVe / ViVeTool](https://github.com/thebookisclosed/ViVe) | C# 特性开关库与命令行 | `FeatureManager.cs`、`Program.cs` | 通过 ntdll 的特性配置接口读写特性，而非直接改注册表 |

## 二、关键技术发现

### 1. ViVe 不是改注册表，而是调 ntdll

ViVe 的 `FeatureManager` 通过 ntdll 导出函数 `RtlQueryAllFeatureConfigurations` 读取特性配置（`RTL_FEATURE_CONFIGURATION_TYPE` 区分 Runtime 与 Boot），特性按优先级叠加，源码中明确列为不可修改的有 `ImageDefault`、`EKB`、`ImageDefaultEditionOverride`、`Security`、`ImageOverride` 五级。启动持久化与"最后已知良好"管理由库自己实现；特性使用订阅写入 `HKLM\SYSTEM\CurrentControlSet\Control\FeatureManagement\UsageSubscriptions\<混淆后的特性 ID>`，混淆算法在 `ObfuscationHelpers.ObfuscateFeatureId`。

**影响**：WinTools 现有的「功能开关」直接写 `FeatureManagement\Overrides\<id>` 注册表值，只能覆盖其中一类。要对标 ViVeTool，需要：

- 用 `RtlQueryAllFeatureConfigurations` 读取当前生效的全部特性与状态；
- 用 ntdll 的特性配置写入接口提交修改，并同步 Boot 持久化（具体签名在实现阶段以 ViVe 的 `NativeMethods` 为准逐项核对）；
- 展示优先级、状态（默认/开启/关闭）、变体与变体载荷、是否有使用订阅；
- 要求 Windows 10 build 18963 及以上。

### 2. Dism++ 的规则文件就是现成的规则库

`Data.xml` 包含：

| 集合 | 数量 | 说明 |
| --- | --- | --- |
| `CleanCollection4` | 73 项 | 分组：缓存文件 19、临时文件 18、过期文件 14、系统相关 11、应用程序 7、备份文件 4 |
| `SystemOptimization` | 163 项 | 分 21 个小节：任务栏、主题、安全、开始菜单与体验、资源管理器、桌面图标、导航窗格图标、右键菜单、Edge、IE、输入法、Windows Update、记事本、网络、服务、WinRAR、媒体播放器、照片查看器等 |
| `DefaultFilterList` | 32 项 | 默认排除项 |

每个清理项带有风险等级（`Level`）、适用条件（系统版本、文件是否存在、镜像标志）和激活动作（`General` 枚举目录、`Custom` 调用内置过程如组件清理）。

**影响**：规则的路径、分组、风险等级可以直接转换为 WinTools 的声明式规则；需要注意其中带 `#` 前缀的是它自己的本地化键，名称本身即简体中文。

### 3. winutil 的优化项有明确的分层

| 分类 | 数量 | 示例 |
| --- | --- | --- |
| 基础优化（Essential） | 18 | 活动历史、休眠、小组件、位置跟踪、服务设为手动、遥测、传递优化、消费者功能、还原点、任务栏右键结束任务 |
| 高级优化（Advanced，需谨慎） | 23 | svchost 拆分、Edge 移除/去臃肿、OneDrive 移除、UTC 时间、视觉效果最佳性能、保留存储、Windows AI 禁用 |
| 个性化（Customize Preferences） | 25 | 深色模式、搜索按钮、任务视图、小组件按钮、显示隐藏文件/扩展名、粘滞键、鼠标加速等 |
| 性能计划 | 2 | 终极性能计划（不适用笔记本）等 |
| 功能页 | 33 | 可选功能 10、修复 6、旧版控制面板入口 14、远程访问 1、PowerShell 配置 2 |

## 三、逐项对照：14 条需求

图例：已具备 / 部分具备 / 缺失。

| # | 需求 | 参考来源 | WinTools 现状 | 缺口 | 实现方式 |
| --- | --- | --- | --- | --- | --- |
| 1 | 垃圾清理：图表、动画、总结 | 电脑管家模式；Dism++ 的分组与风险等级 | 部分：有扫描与清理，无图表、无阶段、无总结 | 五阶段状态机、环形图、结果总结、继续清理 | LiveCharts2 环形图；`CleanupPhase` 状态机；规则扩展到 Dism++ 的 73 项 |
| 2 | 系统概览逐条复制 | — | 缺失 | 单条复制、全部复制 | `IClipboardService` + 行内复制按钮 |
| 3 | 系统功能树状 | winutil 功能页（平铺）；Dism++ 可选功能 | 部分：按用途分组，不是父子树，存在同族重复 | 父子关系、三态、去重 | DISM API 读取 + 父子表；父项三态开关 |
| 4 | 页签与搜索同行 | — | 缺失：搜索在页签下一行 | 统一工具栏 | `ModulePage` 渲染 `IToolbarHost` |
| 5 | 系统渠道 | Dism++ 无；Windows `changepk` | 部分：需手输密钥 | 下拉、内置通用密钥 | `editions.json` + `changepk`，明示不激活 |
| 6 | 加载态与 API 优先 | — | 缺失：只有空列表；命令行解析 | 四态、骨架屏；DISM/SetupAPI/WinRT | `PageState`；`DismApi.dll`、SetupAPI、WinRT |
| 7 | 补丁搜索与特性开关 | ViVeTool | 部分：补丁自动搜索；开关改注册表 | 手动搜索、分类；ntdll 接口、优先级、变体、导入导出 | WUA COM；`Rtl*FeatureConfigurations` |
| 8 | 启动项/计划任务/服务：图标与开关 | Optimizer 启动项；winutil 服务 | 部分：无图标，用按钮 | 图标、开关、启动类型下拉 | `IIconService`；开关控件 |
| 9 | 安全中心全量设置 | Optimizer 的 Defender 控制；Zyper 的 Defender 开关 | 部分：仅实时/行为/篡改 | 攻击面、防火墙、UAC、SmartScreen、BitLocker 等 | `Set-MpPreference`、防火墙 COM、注册表 |
| 10 | 系统组件中心 | winutil 功能页（WSL、Sandbox、NFS、OpenSSH）；Zyper Appx 管理 | 部分：仅商店重置 | 内置应用、WSL、WSA、可选组件 | PackageManager、`wsl` 命令、第三方 WSA 包、DISM Capability |
| 11 | .NET/VC++ 可安装 | winutil 应用安装；Optimizer 应用下载 | 部分：只能列出和卸载 | 在线版本矩阵、下载、校验、静默安装 | 官方 `releases-index.json`、aka.ms 链接、`WinVerifyTrust` |
| 12 | 程序/Appx 字段 | Geek Uninstaller | 部分：无图标、无使用日期 | 图标、大小、安装日期、最后使用、残留扫描 | `DisplayIcon`、`UserAssist`、PackageManager |
| 13 | 驱动单页批量 | Dism++ 驱动管理 | 部分：四个页签 | 批量勾选备份/安装/删除 | 单页表格 + 工具栏 |
| 14 | 备份还原 | Dism++ | 部分：基础 WIM | 分区下拉、压缩/分卷/排除/校验/导出/引导修复 | WIMGAPI + DISM API |

## 四、系统优化缺口

WinTools 的优化项按类别整理。与参考项目的差距如下：

| 来源 | 参考数量 | 与现有重叠 | 需补充的方向 |
| --- | --- | --- | --- |
| winutil | 68 | 待逐项比对 | 视觉效果、保留存储、Windows AI/Recall、Edge/OneDrive 去臃肿、UTC、右键结束任务、粘滞键、鼠标加速、性能计划、svchost 拆分 |
| Dism++ | 163 | 待逐项比对 | 任务栏（秒级时钟、小图标、透明度）、资源管理器（快捷方式箭头、文件扩展名、六个文件夹）、右键菜单管理、桌面图标管理、输入法、记事本、Windows Update 策略、服务优化 |
| Optimizer | README 列出 27 项能力 | 待逐项比对 | Office 遥测、Cortana/Copilot、HPET、DNS 切换、Hosts 编辑、文件句柄、右键菜单扩展 |
| ZyperWin++ | README 列出的能力 | 待逐项比对 | 导入导出配置、优化后自动保存配置并可还原、优化项介绍 |

"待逐项比对"在优化对标任务中完成：把四个来源的每一项映射到 WinTools 的声明式条目，输出重叠与缺口清单后再补充。补充原则：每项必须有检测、应用、回滚三个动作；高风险项默认不勾选，并标注影响。

## 五、LiveCharts2 兼容性验证

在 .NET 10 / Windows 下做了实际渲染探针（环形图，3 个切片）：

| 项目 | 结果 |
| --- | --- |
| Debug 构建渲染 | 通过，输出完整环形图 |
| Release 构建渲染 | 通过 |
| ReadyToRun 框架依赖发布 | 通过 |
| 单文件自包含发布 | 通过 |
| 首个图表对象的冷启动构造耗时 | 约 0.5 至 0.8 秒（一次性，含 SkiaSharp 原生库加载与 JIT） |
| 依赖版本 | LiveChartsCore.SkiaSharpView.WPF 2.0.5，SkiaSharp 3.119 |

体积变化（相对上一版）：

| 形态 | 之前 | 现在 | 增量构成 |
| --- | --- | --- | --- |
| 框架依赖包 | 22.1 MB | 73.5 MB | Windows SDK 投影 23.7 MB（已确认接受）、libSkiaSharp 10.9 MB、OpenTK 8.3 MB、HarfBuzz 1.7 MB、LiveCharts 约 2.4 MB |
| 便携单文件 | 73.2 MB | 100.0 MB | 同上，经压缩 |

结论：可用。冷启动构造耗时通过"窗口显示后在空闲时预热图表"消除；OpenTK 的 8.3 MB 是传递依赖，后续评估能否排除。

## 六、清理扫描性能的真实数据

此前记录的"477 条规则全盘扫描超过 150 秒"是误判，根因是探针在界面线程上同步等待异步任务，与 `ConfigureAwait(true)` 形成死锁，并非扫描器慢。改为线程池同步等待后实测：

| 规则数 | 命中规则 | 文件数 | 可清理体积 | 耗时 |
| --- | --- | --- | --- | --- |
| 474 | 47 | 115,374 | 24.1 GB | 455 毫秒 |

规则扩展到 1600 条以上后将重新测量。

## 七、没有一手资料的说明

- Geek Uninstaller 为闭源软件，上表只依据其公开功能说明，不涉及源码。
- Optimizer 已停止维护并转向 OptimizerNXT，其能力清单以 README 为准。
- ZyperWinOptimize 本次只读取了 README，未检查其发布包与规则，只作功能参考。
