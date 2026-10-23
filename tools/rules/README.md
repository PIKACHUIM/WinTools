# 清理规则源

`src/WinTools.Infrastructure/Rules/` 下的两个 JSON 是**构建产物**，不要直接改。真正的规则写在 `tools/rules/*.txt`，由生成器合并、去重后输出。

## 目录结构

```
tools/rules/
├─ system.txt            # 系统垃圾：临时文件、更新缓存、日志、转储、缩略图、商店/Defender 残留
├─ apps-browser.txt      # 浏览器：Chrome/Edge/Firefox/IE 与国产双核浏览器
├─ apps-media.txt        # 影音：视频与音乐客户端缓存
├─ apps-software.txt     # 常用软件、安全软件、驱动安装残留
├─ apps-dev.txt          # 开发工具：npm/pip/NuGet/Gradle/IDE/引擎缓存
├─ vendor-wildcards.txt  # 厂商通配规则（一个厂商覆盖多款产品）
├─ products.txt          # 产品清单：自动展开 Cache/Temp/Log/Update/Download 等候选路径
└─ legacy/
   └─ F盘缓存垃圾清理清单.csv   # 最初的 105 条种子清单，仅作历史参考
```

## 行格式

```
id|名称|分类|安全等级|匹配方式|清理策略|路径1;路径2;路径3|说明
```

| 字段 | 取值 |
| --- | --- |
| 安全等级 | `S1` 安全 / `S2` 谨慎 / `S3` 影响数据 / `S4` 禁止（`S4` 只展示不可清理） |
| 匹配方式 | `DirectoryRecursive` 递归目录 / `GlobFile` 通配文件 / `SingleFile` 单文件 |
| 清理策略 | `DeleteContents` 清空内容 / `DeleteDirectory` 删目录 / `DeleteFile` 删文件 |
| 路径 | 支持 `%ENV%`、`{Drive}`、`*`（通配一层目录），用 `;` 分隔 |

## 生成

```powershell
powershell -ExecutionPolicy Bypass -File tools/Generate-CleanupRules.ps1
```

会输出：

- `src/WinTools.Infrastructure/Rules/cleanup-rules-extra.json`（每条路径一条规则，便于逐项勾选）

生成器会做三件事：

1. **按路径拆分**：一行里写 4 个路径，会生成 4 条可单独勾选的规则；
2. **全局去重**：与 `cleanup-rules.json` 里已有路径比对，避免重复扫描；
3. **产品展开**：`products.txt` 里每行产品按 8 种常见子目录（Cache / Cache2 / Temp / Log / Logs / Update / Download / Backup）展开成候选路径。

## 规模

| 指标 | 数量 |
| --- | --- |
| 规则总数 | 约 1600 条（105 条种子 + 生成规则） |
| 覆盖产品 | 150+ 款（浏览器、影音、办公、IM、安全软件、开发工具、模拟器…） |
| 分类 | 19 个 |
| 候选路径 | 约 1600 个（路径不存在时扫描会直接跳过，开销可忽略） |

## 维护建议

- 新增一款软件：优先加一行到 `products.txt`（只写根目录），生成器会自动补齐常见缓存子目录。
- 需要精确控制的（例如某软件把缓存拆在多个位置）：写到对应的 `apps-*.txt`。
- 安全等级不要"图省事全写 S1"：删除会导致重新登录、配置丢失、需要重新下载的，一律 `S2` 以上。
