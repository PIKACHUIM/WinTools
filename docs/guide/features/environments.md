# 运行环境

检测机器上的 **.NET 运行时** 与 **Visual C++ 运行库**，并支持卸载。

![.NET 环境](/screenshots/runtimes-dotnet.png)

## .NET 环境

按「运行时类型 + 架构」列出所有已安装版本：

| 类型 | 说明 |
| --- | --- |
| .NET Desktop Runtime | WPF / WinForms 程序需要 |
| .NET Runtime | 控制台与服务程序需要 |
| ASP.NET Core Runtime | Web 服务需要 |

**来源**：`HKLM\SOFTWARE\dotnet\Setup\InstalledVersions\{x64|x86}\sharedfx\<stack>`，值名即版本号。

::: info 为什么这里不能卸载
.NET 运行时由官方安装器（`dotnet-install` / MSI）管理，注册表里没有可靠的 `UninstallString`。强行删除目录会留下损坏状态。需要卸载请到「设置 → 应用」或使用官方安装器。
:::

## VC++ 环境

枚举 2005–2022 各版本 Visual C++ Redistributable（x86 / x64 分开显示），含版本号与架构标签。

| 操作 | 说明 |
| --- | --- |
| 卸载 | 解析 `UninstallString`，若为 MSI 则执行 `msiexec /x {GUID} /qb /norestart`（静默带进度，不弹完整向导） |
| 结果判定 | 返回码 `0` 成功、`3010` 成功但需重启、`1602` 用户取消或另一安装正在进行 |

**来源**：`Software\Microsoft\Windows\CurrentVersion\Uninstall` 下名称同时包含 `Visual C++` 与 `Redistributable` 的项（覆盖 HKLM 64 位、32 位与 HKCU）。

::: warning 卸载前确认
- 很多游戏与专业软件依赖特定年份的 VC++ 运行库，卸载会导致"缺少 xxx.dll"。
- 建议保留 **2015-2022 合并版（x64 + x86）**，这是目前绝大多数程序需要的。
- 卸载需要管理员权限，且部分版本会因被占用而要求重启。
:::

## 关于"安装"

本模块当前只做**检测与卸载**：运行库安装包体积较大且版本矩阵庞杂（不同年份、架构、语言），在线下载不可控。如需安装，建议从微软官方页面获取：

- [.NET 下载页](https://dotnet.microsoft.com/download/dotnet)
- [最新受支持的 Visual C++ 可再发行程序包](https://learn.microsoft.com/cpp/windows/latest-supported-vc-redist)
