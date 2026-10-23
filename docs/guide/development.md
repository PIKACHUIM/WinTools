# 开发指引

## 环境要求

| 工具 | 版本 | 用途 |
| --- | --- | --- |
| [.NET SDK](https://dotnet.microsoft.com/download/dotnet/10.0) | 10.0+ | 编译主程序 |
| Visual Studio 2022 / Rider | 最新 | 可选，调试 WPF |
| [Node.js](https://nodejs.org/) | 20+ | 仅构建本文档站 |

## 仓库结构

```
WinTools/
├─ src/
│  ├─ WinTools.Core/            # 纯领域层：模型、契约、工具（不引用 WPF）
│  │  ├─ Models/                #   模块定义、更新/驱动/备份等记录类型、LocalizedText
│  │  ├─ Abstractions/          #   服务契约（ICleanupService、IWimService…）
│  │  └─ Utils/                 #   路径模板、安全校验、大小格式化
│  ├─ WinTools.Infrastructure/  # 系统能力：P/Invoke、COM、注册表、命令行封装
│  │  ├─ Rules/                 #   内嵌 JSON：cleanup-rules / tweaks / modules
│  │  ├─ Cleanup|Tweaks|...     #   各模块服务实现
│  │  └─ Backup/WimgApi.cs      #   WIMGAPI 互操作声明
│  └─ WinTools.App/             # WPF 表现层
│     ├─ ViewModels/Modules/    #   模块页与页签 ViewModel
│     ├─ Views/                 #   ShellWindow + Pages + Sections
│     ├─ Themes/                #   Dark.xaml / Light.xaml / Controls.xaml
│     ├─ Assets/i18n/           #   12 个语言包
│     └─ Diagnostics/           #   启动打点、离屏截图工具
├─ docs/                        # 本文档站（VitePress）
├─ tools/                       # 构建/发布辅助脚本
└─ Directory.Build.props        # 统一编译选项
```

依赖方向严格单向：`App → Infrastructure → Core`，`Core` 不引用任何其它项目。

## 构建与发布

```powershell
# 调试构建
dotnet build src/WinTools.App/WinTools.App.csproj

# Release（开启 R2R）
dotnet build src/WinTools.App/WinTools.App.csproj -c Release

# 两种发行包 → artifacts/publish/{portable,framework}
powershell -ExecutionPolicy Bypass -File tools/publish.ps1 -Profile both

# 只出框架依赖包（约 2 MB）
powershell -ExecutionPolicy Bypass -File tools/publish.ps1 -Profile framework
```

::: tip 发布脚本会先清空输出目录
`dotnet publish` 不会删除上一版的残留文件，直接覆盖会导致体积统计失真（曾出现"多出 24 MB"的假象）。`tools/publish.ps1` 里已加清理。
:::

## 架构约定

### 菜单是数据

一级/二级菜单定义在 `src/WinTools.Infrastructure/Rules/modules.json`：

```json
{
  "id": "Startup",
  "glyph": "\uE768",
  "order": 5,
  "page": "RuntimeManagement",
  "sections": [
    {
      "id": "StartupItems",
      "title": { "zh-Hans": "启动项", "en-US": "Startup items" },
      "description": { "zh-Hans": "…", "en-US": "…" },
      "items": [{ "zh-Hans": "…", "en-US": "…" }]
    }
  ]
}
```

`page` 决定 DI 里用哪个 ViewModel 渲染；`sections` 决定二级页签。外壳只负责按 `order` 排序、按 `sections` 生成子菜单，不认识任何具体模块。

### 新增一个模块

1. 在 `modules.json` 里加条目（`id` / `glyph` / `order` / `page` / `sections`）。
2. 新建 ViewModel，继承 `ModuleViewModelBase`：

```csharp
public sealed partial class MyModuleViewModel : ModuleViewModelBase
{
    public MyModuleViewModel(IModuleCatalog catalog, IMyService service)
        : base(catalog, "MyModule")            // 必须与 modules.json 的 id 一致
    {
        Register("TabOne", new MyTabViewModel(service));   // 与 sections[].id 一致
        Register("TabTwo", new MyOtherTabViewModel(service));
        SelectSection(Sections[0].Id);
    }
}
```

3. 在 `App.xaml.cs` 注册单例，并在 `ShellViewModel.GetPage` 的 `switch` 里加一个分支。
4. 给页签标题补各语言文案（`modules.json` 的 `title` 已支持 12 语言）。

### 列表型页签：只写数据逻辑

90% 的页签都是"一个列表 + 几个操作"，直接继承 `ListSectionViewModel`，视图复用 `ListSectionView`，不需要写 XAML：

```csharp
public sealed partial class MyTabViewModel : ListSectionViewModel
{
    private readonly IMyService _service;

    public MyTabViewModel(IMyService service)
    {
        _service = service;
        ShowCheckboxes = true;                       // 显示勾选框
        HasPrimary = true;
        PrimaryLabel = Loc.T("Common.Install");      // 主按钮文案
        _ = ReloadAsync();                           // 首次进入即加载
    }

    protected override async Task LoadAsync(CancellationToken ct)
    {
        var items = await _service.GetAsync(ct).ConfigureAwait(true);

        Rows.Clear();
        foreach (var item in items)
        {
            var row = new RowItem(
                item.Id,
                item.Name,
                subtitle: item.Description,
                detail: item.Version,
                badge: item.Enabled ? Loc.T("Common.Enabled") : Loc.T("Common.Disabled"),
                tone: item.Enabled ? RowTone.Success : RowTone.Warning)
            {
                Tag = item,                          // 存原始对象
                SearchBlob = $"{item.Name} {item.Version}"   // 参与搜索的文本
            };

            row.ShowAction = true;
            row.ActionLabel = Loc.T("Common.Delete");
            row.ActionCommand = new AsyncRelayCommand(() => DeleteAsync(row, item));
            Rows.Add(row);
        }

        Status = Loc.F("Common.TotalCount", Rows.Count);
    }

    protected override async Task PrimaryCoreAsync(CancellationToken ct)
    {
        // 批量操作：CheckedRows 是已勾选行
        if (CheckedRows.Count == 0) { Status = Loc.T("Common.NothingSelected"); return; }
        // …
    }
}
```

基类已经处理了：忙碌态互斥、异常兜底、搜索过滤、全选、进度条、状态文案。

### 性能守则

| 约定 | 原因 |
| --- | --- |
| UI 线程不做 IO、不跑命令行 | 所有 `LoadAsync` 都在后台线程，回到 UI 线程只做赋值 |
| 列表必须虚拟化 | `ListBox` + `VirtualizingStackPanel`（回收模式），307 项服务滚动无压力 |
| 页面懒加载 | `Func<object>` 工厂，只有点开的页面才创建 |
| 不在 `ShellViewModel` 构造函数里解析页面 | 页面反向依赖外壳，会形成 DI 循环依赖并导致启动挂死 |
| 长任务给 `IProgress<T>` | 扫描、DISM、WIMGAPI 都有进度回调 |

## 数据文件格式

### 清理规则

`src/WinTools.Infrastructure/Rules/cleanup-rules.json`：

```json
{
  "id": "SYS-TEMP-01",
  "category": "SystemTemp",
  "name": { "zh-Hans": "Windows 系统临时目录", "en-US": "Windows temp" },
  "paths": ["%SystemRoot%\\Temp", "C:\\Windows\\Temp"],
  "match": "DirectoryChildren",
  "strategy": "Delete",
  "safety": "Safe",
  "requiresAdmin": true,
  "minAgeDays": 0,
  "note": { "zh-Hans": "需管理员", "en-US": "Requires admin" }
}
```

- `paths` 支持 `%ENV%`、`{Drive}`、`{AllUsers}`、`*`
- `match`：`DirectoryChildren` / `Recursive` / `FilePattern`
- `strategy`：`Delete` / `RecycleBin` / `EmptyFolder`
- `safety`：`Safe` / `Caution` / `Risky` / `Forbidden`（四级，界面用颜色区分）

### 优化条目

`tweaks.json` 中每条由若干动作组成：

```json
{
  "id": "perf-power-plan",
  "group": "Performance",
  "risk": "Low",
  "name": { "zh-Hans": "切换到高性能电源计划" },
  "actions": [
    { "type": "Command", "command": "powercfg", "arguments": "/setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c" }
  ]
}
```

支持的动作：`RegSet`、`RegDeleteValue`、`RegDeleteKey`、`ServiceStart`、`Command`。

## 本地化

- 所有界面文案走 `Loc.T("Key")`（或带格式化的 `Loc.F`），**不要硬编码字符串**。
- 语言包在 `src/WinTools.App/Assets/i18n/<culture>.json`，12 个文件键集合必须一致。
- 新增语言：复制任意语言包 → 改名为标准文化名（如 `nl-NL.json`）→ 在 `LocalizationService.AvailableLanguages` 里加一行。
- 简体中文与阿拉伯语需要额外注意 RTL：`BoolToFlowDirection` 会自动把窗口 `FlowDirection` 设为 `RightToLeft`。
- 菜单/页签标题放在 `modules.json` 的 `title` 里（同样多语言），避免与语言包重复维护。

## 主题

`Themes/Dark.xaml` 与 `Themes/Light.xaml` 定义同一组 25 个画刷键（`App.Background`、`App.Text.Primary`、`App.Accent`…）。`ThemeService.Apply()` 只替换合并字典里的第一个主题字典，其余控件模板引用 `DynamicResource` 自动跟随。

::: warning 窗口背景要自己画
`WindowChrome` 下窗口客户区不绘制背景。背景画在 `ShellWindow` 根 `Border` 上（`App.Background`），不要依赖 `Window.Background`。
:::

## 调试

| 手段 | 用法 |
| --- | --- |
| 启动打点 | 设环境变量 `WINTOOLS_TRACE=1`，日志输出到 `%LOCALAPPDATA%\WinTools\boot-trace.log`，含每个阶段耗时 |
| 页面截图 | `WinTools.exe --shots <输出目录>`（DEBUG 构建），离屏渲染全部页签为 PNG，适合无人值守环境自检 |
| 运行日志 | `%ProgramData%\WinTools\logs\app-YYYYMMDD.log` |
| 审计日志 | `%ProgramData%\WinTools\logs\audit-YYYYMMDD.jsonl` |

## 文档站

```powershell
npm install          # 首次
npm run docs:dev     # 本地预览 http://localhost:5173
npm run docs:build   # 产物在 docs/.vitepress/dist
```

文档站由 GitHub Actions 自动构建并部署到 GitHub Pages：`.github/workflows/docs.yml`，推送到 `main` 且改动 `docs/**` 时触发。

## 提交规范

```
feat(cleanup): 支持按修改时间过滤
fix(updates): 修复 KB 号为空时的崩溃
docs(guide): 补充备份格式说明
```

模块名用目录名小写（cleanup / optimize / features / updates / runtime / components / runtimes / apps / drivers / backup / docs）。

## 界面字体

全局字体为 **Maple Mono NL NF CN Regular**，内嵌在 `WinTools.dll` 里（`Assets/Fonts/*.ttf` 以 `<Resource>` 打包，约 20 MB）。

::: warning WPF 不支持 WOFF2
上游发布的是 Web 字体（`.woff2`），WPF 只认 TrueType/OpenType。仓库里的 TTF 由脚本转换而来，换字体时重新执行：

```powershell
py -m pip install fonttools brotli
py tools/Convert-Woff2ToTtf.py 新字体.woff2 src\WinTools.App\Assets\Fonts\新字体.ttf
```
:::

引用方式（`App.xaml`）：

```xml
<FontFamily x:Key="App.FontFamily">
  pack://application:,,,/Assets/Fonts/#Maple Mono NL NF CN, Microsoft YaHei UI, Segoe UI
</FontFamily>
```

- `#` 后面是**字体族名**（不是文件名），可用 `py tools/Convert-Woff2ToTtf.py` 打印；
- 逗号后面是回退链，用于字体缺字时（例如生僻符号、Emoji）；
- 图标仍然使用 `Segoe Fluent Icons`（`App.IconFontFamily`），不要被全局字体覆盖；
- 新增字体需同时更新 `THIRD-PARTY-NOTICES.md` 的许可声明。

## CI 构建与发布

`.github/workflows/build.yml` 一个文件覆盖两条发布路径：

| 触发方式 | 行为 | 产物去向 |
| --- | --- | --- |
| 推送到 `main` | 自动构建（`docs/**`、`*.md` 变更不触发） | 覆盖更新 **beta** 预发布（滚动标签 `beta`） |
| 手动 `workflow_dispatch`，channel=`release` | 填版本号 → 构建 → **签名** → 校验 | 新建 `v<版本>` 正式 Release |
| 手动 `workflow_dispatch`，channel=`beta` | 同推送路径，用于演练流水线 | 覆盖更新 beta |

每次构建产出三个文件：

- `WinTools-<版本>-portable-win-x64.exe` —— 便携单文件（自包含）
- `WinTools-<版本>-framework-win-x64.zip` —— 框架依赖包（内含 `BUILD_INFO.txt`）
- `SHA256SUMS.txt` —— 校验和（在签名之后生成，所以与下载文件一致）

版本号会写进程序集（`FileVersion` / `ProductVersion`），手动发布时不需要改 `Directory.Build.props`。

### 代码签名

正式版必须带 **Certum 云签名 + 时间戳**，缺凭据时 release 构建直接失败（beta 只警告）。
签名实现放在 `tools/signing/`，细节见该目录的 `README.md`，需要三个 Repository Secrets：

```
CERTUM_USERNAME
CERTUM_OTP_URI
CERTUM_CERTIFICATE_SHA1
```

流水线里的顺序是：构建 → 装 SimplySign 并 TOTP 登录 → 签名 → `Get-AuthenticodeSignature` 逐个校验 → 打包 → 计算校验和。
签名会改变文件哈希，所以**必须先签名再算校验和**，否则用户下载后对不上。

### 手动发一版

```text
Actions → Build → Run workflow
  channel: release
  version: 1.2.3
  draft:   false
```

已存在的 `v1.2.3` 会被 `prepare` 作业拒绝（`gh release view` 与 `git ls-remote --tags` 双重检查），不会覆盖历史版本。
想先看效果再公开，把 `draft` 勾上，Release 会以草稿创建。
