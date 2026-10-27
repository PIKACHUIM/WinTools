# 参考优化项快照

这里存放第三方优化工具的**配置快照**，用于离线生成 `src/WinTools.Infrastructure/Rules/tweaks-reference.json`。
应用运行时不会联网读取任何外部源码，构建/维护时才用 `tools/Sync-ReferenceTweaks.ps1` 重新生成。

| 文件 | 来源 | 说明 |
| --- | --- | --- |
| `winutil-tweaks.json` | [ChrisTitusTech/winutil](https://github.com/ChrisTitusTech/winutil) `config/tweaks.json`（MIT） | 2026-10 快照，原样保存，便于追溯每一条注册表操作 |
| `winutil-meta.json` | 本项目维护 | 白名单：只转换语义明确、方向是“优化”的条目，并补齐中文名、分组、风险与推荐标记 |

更新流程：

1. 覆盖 `winutil-tweaks.json`（保持原文件结构）。
2. 需要新增条目时，在 `winutil-meta.json` 加一行（`winutilId` 必须与快照键名一致）。
3. 运行 `powershell -File tools/Sync-ReferenceTweaks.ps1`，再编译验证。
4. 已有 `tweaks.json` / `tweaks-extra.json` 覆盖的键会自动跳过，不会重复上架。

Optimizer 与 ZyperWin++ 的配置写在 C# 源码里，没有可机读的配置表，暂未自动导入；
如需补充，可按同样的思路先提取成 JSON 快照再加白名单。
