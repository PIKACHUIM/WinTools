# 驱动管理

四个页签覆盖驱动包（DriverStore）的完整生命周期：**查看 → 备份 → 安装 → 删除**。

![系统驱动](/screenshots/drivers-systemdrivers.png)

## 系统驱动

全量枚举 DriverStore 中的驱动包（实测约 155 个），每行显示：

| 字段 | 含义 |
| --- | --- |
| 原始名称 | `原厂 inf`，如 `alderlakepch-system.inf` |
| 提供者 | 驱动厂商（INTEL / Microsoft / Realtek…） |
| 类 · 版本 · 日期 | 设备类（System / Extension / Mouse…）、驱动版本、签名日期 |
| 发布名 | `oemNN.inf`，删除驱动时使用的标识 |

数据来自 `pnputil /enum-drivers`。

::: info 为什么按值特征解析而不是固定行号
`pnputil` 的输出标签随系统语言变化（中文系统是「发布名称:」），且扩展驱动的输出比普通驱动多一行 `Extension ID`。按行号取值会错位，因此解析器按值的特征（`oem\d+\.inf`、`X.Y.Z.W`、日期、GUID）识别字段，与语言和驱动类型无关。
:::

## 备份驱动

把第三方驱动导出到指定目录，用于重装系统后快速恢复。

1. 在输入框填写或粘贴导出目录（如 `D:\drivers-backup`）。
2. 点 **导出**，执行 `pnputil /export-driver * <目录>`。
3. 完成后每个驱动一个子目录（含 inf / sys / cat 文件），共 N 个。

::: tip 重装系统前做一次
Windows 自带的"驱动备份"在新系统上经常找不到，手动导出到数据盘最可靠 —— 尤其是网卡驱动（避免重装后连不上网）。
:::

## 安装驱动

从目录或单个 inf 文件安装驱动：

- 目录：`pnputil /add-driver <目录> /install /subdirs`（递归查找 inf）
- 单个文件：`pnputil /add-driver <file.inf> /install`

安装完成后部分驱动需要重启生效，界面会提示。

::: warning 先用系统"设备管理器"确认
`pnputil /install` 会把驱动加入 DriverStore 并尝试匹配当前设备，但不会像设备管理器那样引导你选择设备。如果是替换已有驱动，建议先在设备管理器里「更新驱动程序 → 浏览计算机」。
:::

## 删除驱动

列出全部驱动包，行尾「删除」执行 `pnputil /delete-driver <oemNN.inf> /uninstall`。

::: danger 这是本工具风险最高的操作之一
- 删除正在使用的驱动会导致**设备立即失效**（网卡/显卡/存储控制器尤其危险）。
- 删除网卡驱动后可能断网，建议先「备份驱动」。
- 删除后可通过「安装驱动」重新安装备份的 inf。
- 建议每次只删一个，确认系统正常后再继续。
:::

## 相关命令行速查

```powershell
pnputil /enum-drivers                          # 枚举
pnputil /export-driver * D:\drivers            # 导出全部
pnputil /add-driver D:\drivers /install /subdirs  # 安装目录下的驱动
pnputil /delete-driver oem12.inf /uninstall    # 删除指定驱动包
```
