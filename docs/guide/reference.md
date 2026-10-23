# 参考项目与致谢

WinTools 的功能取舍、交互组织与技术路线参考了以下开源项目与文档。没有它们，这个工具的很多设计会是拍脑袋决定的。

## 界面与功能参考

| 项目 | 作者 | 借鉴内容 |
| --- | --- | --- |
| [winutil](https://github.com/ChrisTitusTech/winutil) | Chris Titus Tech | 优化项的分组方式、"先选后应用"的交互节奏、配置预设思路 |
| [Optimizer](https://github.com/hellzerg/optimizer) | hellzerg | 优化条目划分与风险提示文案、启动项与服务的组织方式 |
| [ZyperWinOptimize](https://github.com/ZyperRap/ZyperWinOptimize) | ZyperRap | 中文场景下的优化项清单与界面信息密度 |
| [Dism-Multi-language](https://github.com/Chuyu-Team/Dism-Multi-language) | Chuyu-Team | DISM 组件与多语言资源的组织方式 |
| [geek-uninstaller](https://geekuninstaller.com/) | Geek Uninstaller | 已安装程序列表的字段选择与卸载交互 |
| [ViVeTool](https://github.com/thebookisclosed/ViVe) | thebookisclosed | 特性开关（Feature Flags）的数据结构、三态语义与哈希思路 |

## 技术文档

| 来源 | 用途 |
| --- | --- |
| [WIMGAPI 文档（Win32 apps）](https://learn.microsoft.com/windows/win32/api/wimgapi/) | 镜像捕获/释放的函数签名与消息回调 |
| [DISM API 参考](https://learn.microsoft.com/windows-hardware/manufacture/desktop/dism/dism-api-reference) | 可选功能与镜像操作的参数约定 |
| [Windows Update Agent API](https://learn.microsoft.com/windows/win32/wua_sdk/portal-client) | 补丁搜索、下载、安装与卸载的接口语义 |
| [pnputil 命令参考](https://learn.microsoft.com/windows-hardware/drivers/devtest/pnputil-command-syntax) | 驱动枚举、导出、安装与删除 |
| [Task Scheduler 接口](https://learn.microsoft.com/windows/win32/taskschd/task-scheduler-start-page) | 计划任务 COM 对象模型 |
| [WPF 文档](https://learn.microsoft.com/dotnet/desktop/wpf/) | 控件模板、虚拟化与渲染性能 |
| [VitePress](https://vitepress.dev/) | 本文档站本身 |

## 数据来源

- **清理规则种子**：仓库内 `docs/F盘缓存垃圾清理清单.csv`（105 条，含路径模板与说明），另外补充了常见系统缓存、浏览器缓存与开发工具缓存路径。
- **优化条目**：参考上述项目公开的注册表项，逐条在 Windows 11 上验证检测-应用-回滚闭环后写入 `tweaks.json`。

## 与参考项目的差异

| 维度 | 常见做法 | WinTools 的选择 |
| --- | --- | --- |
| 多语言 | 多数工具只做中/英 | 12 种语言，界面文案零硬编码 |
| 系统能力 | 依赖 `System.Management`、WinRT 投影等重型依赖 | 一律 P/Invoke / COM 后期绑定 / 注册表，3 个 NuGet 包 |
| 界面 | 使用第三方 UI 框架或 WinForms | 手写 WPF 控件模板，不引入 UI 框架 |
| 优化项 | 一键"全家桶"应用 | 逐项检测、勾选应用、可回滚 |
| 服务治理 | 提供"一键禁用服务"列表 | 只给状态与启动类型调整，不批量停用 |

## 致谢

感谢上述项目的作者把经验公开出来。如果你觉得 WinTools 有用，也请给这些上游项目点个 Star —— 它们的积累是这个工具的地基。

## 许可

WinTools 以 [AGPL-3.0](https://github.com/PIKACHUIM/WinTools/blob/main/LICENSE) 发布。
