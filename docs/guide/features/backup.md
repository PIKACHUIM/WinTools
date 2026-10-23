# 备份还原

基于 Windows 自带的 **WIMGAPI**（`wimgapi.dll`）操作 wim 系列镜像，四个页签分别对应捕获、追加、查看与释放。

![系统备份](/screenshots/backup-capture.png)

## 系统备份（Capture）

把目录或分区捕获成镜像文件。

| 字段 | 说明 |
| --- | --- |
| 源目录 | 要备份的目录；备份整机分区时填盘符根目录（如 `D:\`） |
| 映像文件 | 输出路径，扩展名决定格式：`.wim` / `.esd` / `.swm` |
| 映像名称 | 写入 `<NAME>`，释放时用于识别（默认 `WinTools Backup`） |
| 映像描述 | 写入 `<DESCRIPTION>`，可留空 |
| 压缩级别 | 不压缩 / 快速（XPRESS）/ 最大压缩（LZX） |

点击「开始备份」后显示百分比进度，可随时取消（回调返回中止信号，WIMGAPI 会清理临时文件）。

### 三种格式的区别

| 格式 | 压缩 | 特点 | 适用 |
| --- | --- | --- | --- |
| `.wim` | XPRESS / LZX | 可随机读取、可追加多映像、可修改元信息 | 日常备份首选 |
| `.esd` | LZMS（recovery） | 体积最小，**只读**，不能追加 | 归档、分发 |
| `.swm` | 与 wim 相同 | 分卷（每卷 4 GB），用于 FAT32 U 盘 | 大镜像转移 |

::: info .esd / .swm 是怎么来的
WIMGAPI 不直接创建这两种目标格式。本工具先捕获到临时 `.wim`，再用 DISM 转换：

```powershell
dism /Export-Image /SourceImageFile:a.wim /SourceIndex:1 /DestinationImageFile:b.esd /Compress:recovery
dism /Split-Image  /ImageFile:a.wim /SWMFile:b.swm /FileSize:4096
```
:::

## 增量备份（Append）

向**已有**的 `.wim` 追加一个新的映像索引，多个时间点的备份放在同一个文件里。

- 只有 `.wim` 支持追加；`.esd` 是只读格式，`.swm` 需要重新分卷。
- 追加前会打开现有映像（`WIM_OPEN_ALWAYS`），新索引自动递增。
- 释放时在「释放镜像」里选择对应索引即可还原到某个时间点。

## WIM 信息

查看指定镜像的全部索引与属性：

| 字段 | 说明 |
| --- | --- |
| 索引 / 名称 / 描述 | 来自 `<IMAGE INDEX="n">` 节点 |
| 大小 | `<TOTALBYTES>` |
| 压缩方式 | `<COMPRESSION>` |

填写映像路径后点「刷新」读取。备份时设置的名称与描述会显示在这里。

## 释放镜像

把镜像应用到目标目录。

| 字段 | 说明 |
| --- | --- |
| 映像文件 | `.wim` / `.esd` / `.swm` 均可 |
| 映像索引 | 从 1 开始；多索引镜像需要选对 |
| 目标目录 | 释放位置；目录不存在会自动创建 |
| 释放时校验 | 开启后在应用阶段校验哈希（更慢但更可靠） |

`.wim` 走 `WIMLoadImage` + `WIMApplyImage`；`.esd` / `.swm` 交给 `dism /Apply-Image` 更稳妥。

::: danger 不要直接把镜像释放到运行中的系统分区
本工具只做**目录级**释放，不处理引导修复、BCD 重建、驱动注入。整机还原请用 WinPE 环境（如微 PE、Windows 安装盘）离线操作。释放到非系统分区（如 `D:\restore`）是安全的。
:::

## 空间与时间预估

| 场景 | 体积 | 耗时（NVMe，参考） |
| --- | --- | --- |
| 100 GB 已用空间 → wim（快速压缩） | 约 60–70 GB | 8–15 分钟 |
| 同上 → esd（recovery） | 约 40–50 GB | 20–40 分钟 |
| 增量追加（改动 10 GB） | 与改动量相关 | 3–6 分钟 |

镜像文件不要放在被备份的分区里（会把镜像自己捕获进去）。
