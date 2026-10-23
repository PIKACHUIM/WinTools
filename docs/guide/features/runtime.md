# 运行管理

管理"开机自动跑什么"：**启动项**、**计划任务**、**服务管理**三个页签。

![服务管理](/screenshots/startup-services.png)

## 启动项

覆盖三处来源，并读取与任务管理器一致的启用状态：

| 来源 | 位置 |
| --- | --- |
| 注册表 Run | `HKLM` / `HKCU` / `WOW6432Node` 下的 `...\CurrentVersion\Run` |
| 注册表 RunOnce | 同上路径的 `RunOnce` |
| 启动文件夹 | `%ProgramData%\Microsoft\Windows\Start Menu\Programs\StartUp` 与用户目录 |

**勾选态即启用态**：勾选=开机启动，取消勾选=禁用。点「应用」后写入 `StartupApproved` 二进制值（`0x02/0x06` 启用，`0x03` 禁用），这与任务管理器的"已禁用"是同一份数据，两处不会打架。

行尾「删除」会真正移除注册表值或快捷方式文件，相比"禁用"更彻底。

## 计划任务

递归枚举 `\` 下全部任务（含子目录，最多 6 层），显示任务名、路径、状态与触发器数量。

| 操作 | 说明 |
| --- | --- |
| 启用 / 禁用 | 改 `ITask::Enabled`，立即生效 |
| 运行所选 | `ITask::Run()` 立即触发一次 |
| 删除 | `ITaskFolder::DeleteTask()`，不可恢复 |

::: tip 哪些任务可以关
安全软件、输入法、驱动更新助手、厂商"电脑管家"类任务通常可以禁用；`\Microsoft\Windows\` 下的系统维护任务（如磁盘碎片整理、系统还原点创建）建议保留。
:::

实现走 Task Scheduler 的 `Schedule.Service` COM 接口（后期绑定），不依赖 `TaskScheduler` 等第三方库。

## 服务管理

列出全部服务（实测约 307 项），显示显示名、服务名、启动类型、运行状态。

| 操作 | 说明 |
| --- | --- |
| 启动 / 停止 | 行尾按钮，按当前状态切换；调用 `sc.exe` 的 `start` / `stop`，与 SCM 协商而非直接改注册表 |
| 设为自动 | 对勾选的服务批量设置 `start= auto` |
| 设为手动 | 对勾选的服务批量设置 `start= demand`（推荐用于"不确定要不要"的服务） |

::: danger 不要盲目停用系统服务
停用（Disabled）与"手动"不同：手动模式下系统按需启动，最安全；禁用后依赖它的功能会直接失败。常见误伤项：

- `wuauserv`（Windows Update）→ 系统无法更新
- `ClipSVC` / `InstallService` → 商店应用无法安装
- `Audiosrv` → 没有声音
- `BFE`（Base Filtering Engine）→ 防火墙与部分 VPN 失效

本工具不会"一键优化服务"，只提供手动调整，避免误伤。
:::

::: info 为什么改启动类型要用 sc.exe
直接写注册表 `Start` 值不会通知 SCM，需重启才生效，且可能与"延迟启动"标志冲突。`sc.exe config` 会同时处理 SCM 状态与 `DelayedAutoStart`，是最稳妥的方式。
:::
