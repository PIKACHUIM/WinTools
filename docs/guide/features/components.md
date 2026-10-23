# 组件管理

两个页签：**安全中心**（Defender）与 **应用商店**（Microsoft Store）。

## 安全中心

![安全中心](/screenshots/components-securitycenter.png)

显示以下状态，并允许调整其中两项：

| 项 | 来源 | 可修改 |
| --- | --- | --- |
| 实时保护 | `...\Windows Defender\Real-Time Protection\DisableRealtimeMonitoring` | ✅ 开启 / 关闭 |
| 行为监控 | 同上 `DisableBehaviorMonitoring` | 只读展示 |
| 篡改保护 | `...\Windows Defender\Features\TamperProtection`（≥4 视为开启） | 只读展示 |
| 防病毒产品 | WMI `root\SecurityCenter2` → `AntiVirusProduct.displayName` | 只读 |
| 引擎版本 | `AMEngineVersion` | 只读 |
| 排除项 | `...\Windows Defender\Exclusions\Paths` 下的值名 | ✅ 添加 / 移除 |

::: warning 篡改保护开启时无法修改实时保护
若系统开启了"篡改保护"（Win11 默认开启），任何程序（含本工具）写入 Defender 设置都会被拒绝。此时界面会显示写入失败的原因 —— 需要先在「Windows 安全中心 → 病毒和威胁防护 → 管理设置」里关闭篡改保护，或直接在那里操作。
:::

**关于排除项**：添加目录排除后，Defender 不再扫描该目录 —— 这是性能优化的常见手段，但也会降低安全性。建议只对明确的开发/构建目录使用。

## 应用商店

检查商店组件是否健康，并提供两个修复动作。

| 行 | 含义 |
| --- | --- |
| 组件状态 | 在 `C:\Program Files\WindowsApps` 下能否找到 `Microsoft.WindowsStore_*` 包 |
| 版本 | 从包目录名解析出的版本号 |
| 相关服务 | `ClipSVC` / `InstallService` / `AppXSvc` 是否都在运行 |
| 安装位置 | 商店包的物理目录 |

| 动作 | 做什么 | 何时用 |
| --- | --- | --- |
| **重置缓存** | 执行 `wsreset.exe -i` | 商店打不开、页面一直转圈、下载卡在 0% |
| **重新注册** | 对 `Microsoft.WindowsStore` 执行 `Add-AppxPackage -Register` | 商店应用被误删/损坏、开始菜单里消失 |

::: tip 顺序建议
先「重置缓存」，无效再「重新注册」。重新注册只是修复注册信息，不会删除已安装的应用。
:::
