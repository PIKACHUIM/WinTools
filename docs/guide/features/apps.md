# 应用管理

两个页签：**已安装程序**（Win32）与 **应用（Appx）**（UWP/MSIX）。

## 已安装程序

![已安装程序](/screenshots/apps-programs.png)

读取注册表 `Uninstall` 键，合并三处来源：

| 来源 | 覆盖范围 |
| --- | --- |
| `HKLM\...\Uninstall`（64 位视图） | 常规 64 位程序 |
| `HKLM\...\Uninstall`（32 位视图） | 安装在 WOW6432Node 的 32 位程序 |
| `HKCU\...\Uninstall` | 仅当前用户安装的程序 |

**过滤规则**（避免列表被噪音污染）：

- 没有 `DisplayName` 的项跳过
- `SystemComponent = 1` 的系统组件跳过
- 有 `ParentKeyName` 的补丁子项跳过
- `ReleaseType` 为 `Update` / `Hotfix` 的跳过
- 同名同版本去重

**展示**：名称、发布者、版本、占用空间（`EstimatedSize`）、安装日期，右侧「卸载」。

**卸载策略**：

```
有 QuietUninstallString → 静默执行并等待结果，成功后自动刷新列表
只有 UninstallString    → 启动程序自带的卸载向导（可能是人工交互）
```

> 不使用 `Win32_Product` —— 它会触发 MSI 重新配置，既慢又有副作用。

## 应用（Appx）

列出当前用户已安装的 UWP / MSIX 包（已过滤框架包），显示包名、发布者、版本，可逐个移除。

**实现**：通过 PowerShell 的 Appx 模块取数据并转 JSON，再用 `Remove-AppxPackage` 移除。

```powershell
Get-AppxPackage |
  Select-Object Name,Version,Publisher,PackageFullName,IsFramework,InstallLocation |
  ConvertTo-Json -Compress
```

::: info 为什么绕一层 PowerShell
直接调 `Windows.Management.Deployment.PackageManager` 需要 WinRT 投影程序集（`Microsoft.Windows.SDK.NET`，约 24 MB）。为了这一个模块给整个程序增加 24 MB 不值得，而 PowerShell 的 Appx 模块系统自带、调用成本可接受。
:::

::: warning 移除系统应用的注意事项
- 被移除的应用可以在商店重新安装（部分系统应用如"照片""计算器"可用 `Add-AppxPackage -Register` 修复）。
- 移除「开始菜单体验主机」「ShellExperienceHost」等核心包会导致界面异常，不要动。
- 部分预装应用在下次系统更新后可能重新出现。
:::
