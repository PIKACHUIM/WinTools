# 更新管理

三个页签：**已安装更新**、**可安装补丁**、**功能开关**。

## 已安装更新

列出系统已安装的更新历史（KB 号、标题、安装时间），支持按 KB 搜索与卸载。

![可安装补丁](/screenshots/updates-available.png)

**实现**：WUA（Windows Update Agent）COM 接口 —— `Microsoft.Update.Session` → `CreateUpdateSearcher()` → `Search("IsInstalled=1")`。卸载走 `IUpdate::Uninstall()`，返回码 `2/3` 表示成功（`3` 需重启）。

::: warning 卸载补丁有风险
累积更新被卸载后，系统可能重新自动安装；关键安全补丁卸载会降低防护能力。卸载前建议创建还原点。
:::

## 可安装补丁

在线扫描可用更新，勾选后批量下载安装。

| 步骤 | 说明 |
| --- | --- |
| 搜索 | `Search("IsInstalled=0 AND IsHidden=0")`，包含驱动更新与可选更新 |
| 勾选 | 列表显示 KB 号与下载体积，可按需挑选 |
| 安装 | 加入更新集合 → 下载 → 安装，进度与结果实时反馈 |
| 重启 | 需要重启时提示「需重启」，不会自动重启机器 |

搜索依赖 Windows Update 服务（`wuauserv`）与网络；若长期无结果，先检查系统更新是否能正常工作。

## 功能开关

对标 ViVeTool：直接读写系统的特性覆盖项，用于启用/禁用尚未在设置界面开放的功能。

![功能开关](/screenshots/updates-available.png)

**原理**：注册表 `HKLM\SYSTEM\CurrentControlSet\Control\FeatureManagement\Overrides\<FeatureId>`，关键值：

| 值 | 含义 |
| --- | --- |
| `EnabledState` | `0` 关闭 / `1` 开启 / `2` 系统默认 |
| `EnabledStateOptions` | 附加选项，开启或关闭时为 `1` |
| `Variant` / `VariantPayloadKind` | 变体号与载荷类型，一般为 `0` |

**用法**：

1. 列表显示当前所有覆盖项及其状态（绿色=开启，琥珀=关闭，灰=默认）。
2. 行尾「启用 / 禁用」直接切换单项。
3. 勾选多项后可用 **启用** 批量开启，**删除** 移除覆盖项（恢复系统默认）。
4. 在输入框填 **特性 ID**（纯数字）后点「添加」，可直接新建一条覆盖项。

::: tip 特性 ID 从哪来
特性 ID 需要自行查找（社区整理的特性列表通常给出十进制或十六进制 ID，本工具只接受十进制数字）。修改后**必须重启**才能生效，且系统更新后可能失效。
:::

::: danger
写入错误的特性 ID 一般只会无效，但若开启了不稳定的内部功能，可能导致资源管理器或开始菜单异常。删除覆盖项即可恢复。
:::
