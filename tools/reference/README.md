# 参考优化项来源与再生成

这里存放第三方优化工具的**配置来源**与白名单，用于离线生成 `src/WinTools.Infrastructure/Rules/tweaks-*.json`。
应用运行时不会联网读取任何外部源码，只有维护时才用下面的脚本重新生成。

| 输出 | 来源 | 上游许可证 | 说明 |
| --- | --- | --- | --- |
| `tweaks-reference.json`（19 项） | [ChrisTitusTech/winutil](https://github.com/ChrisTitusTech/winutil) `config/tweaks.json` | MIT | 原文件已完整预置为 `winutil-tweaks.json`；只转换白名单内的语义明确项 |
| `tweaks-zyperwin.json`（100 项） | [ZyperWave/ZyperWinOptimize](https://github.com/ZyperWave/ZyperWinOptimize) v4.1 发布包中的 `Bin/ZyperData.xml` | 仓库未附许可证 | 只提交提取后的注册表/服务操作，不提交上游源码；条目名称为功能性描述 |
| `tweaks-optimizer.json`（9 项） | [hellzerg/optimizer](https://github.com/hellzerg/optimizer) `Optimizer/OptimizeHelper.cs` | GPL-3.0 | 只提交提取后的注册表操作，不提交上游源码；仅取有成对 Disable/Enable 的方法 |

上游数据指纹（2026-10-27 抓取）：

- `ZyperWin++4.1.zip`：SHA256 `6A2B2E2116197BADB3C7A88198865F022F4929DD8542EE1D63EEC822FFF33452`（1,408,799 B）
- `ZyperData.xml`：SHA256 `1AA60815D02F6A1E6AD7E3BA0744D70F4AB3DFB59BABC91F8C308DB0895E7D45`（103,330 B）
- `OptimizeHelper.cs`：SHA256 `DF20C682818A0E7FA1BE326C220A55636B0AA75864A7B9840EA566BA9B1EE973`（143,736 B）

## 再生成顺序

三个脚本都会跳过已被其它规则文件覆盖的键，因此必须按下面的顺序执行（后面的脚本会读取前面产物的结果）：

```powershell
powershell -File tools/Sync-ReferenceTweaks.ps1
powershell -File tools/Sync-ZyperTweaks.ps1 -Xml <解压目录>\Release\Bin\ZyperData.xml
powershell -File tools/Sync-OptimizerTweaks.ps1 -Source <仓库>\Optimizer\OptimizeHelper.cs
dotnet build src/WinTools.App/WinTools.App.csproj -c Release
```

## 维护规则

1. 新增条目先改 `winutil-meta.json` / `zyper-meta.json` / `optimizer-meta.json`，再重跑脚本。
2. 排除项写在 meta 的 `excludeNames` / `excludeCategories`（ZyperWin 默认排除了整类“安全”开关，以及幽灵/熔断补丁、Exploit Protection、TSX、不安全下载警告）。
3. 生成后检查：ID 全局唯一、`group` 属于 7 个分组、每条至少有一个 `apply` 操作。
4. 上游项目没有机读配置时（Optimizer 为 C# 源码），只提取注册表/服务这类可验证的事实，不复制其实现代码。
