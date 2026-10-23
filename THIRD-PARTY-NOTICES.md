# 第三方资源声明

## Maple Mono（界面字体）

- 文件：`src/WinTools.App/Assets/Fonts/MapleMonoNL-NF-CN-Regular.ttf`
- 上游项目：<https://github.com/subframe7536/maple-font>
- 许可：SIL Open Font License 1.1（OFL-1.1）

界面全局字体使用 **Maple Mono NL NF CN Regular**。原始文件为 WOFF2（Web 格式），WPF 只支持 TrueType/OpenType，因此通过 `tools/Convert-Woff2ToTtf.py` 解包为 TTF 后内嵌到程序集中：

```powershell
py -m pip install fonttools brotli
py tools/Convert-Woff2ToTtf.py 源文件.woff2 src\WinTools.App\Assets\Fonts\MapleMonoNL-NF-CN-Regular.ttf
```

按 OFL-1.1 要求：

- 该字体可自由使用、修改与再分发，但**不得单独售卖字体本身**；
- 修改后的字体不得继续使用保留字体名（Reserved Font Name）；
- 分发时需附带本许可声明。

字体中的 Nerd Font 图标字形（Powerline、Devicons 等）同样遵循 OFL-1.1；界面图标仍使用系统自带的 `Segoe Fluent Icons`，与字体无关。

## 图标字体

`Segoe Fluent Icons` / `Segoe MDL2 Assets` 为 Windows 系统自带字体，不随本程序分发。

## NuGet 依赖

| 包 | 许可 |
| --- | --- |
| CommunityToolkit.Mvvm | MIT |
| Microsoft.Extensions.DependencyInjection | MIT |
| LiveChartsCore.SkiaSharpView.WPF（2.0.5） | MIT |
| SkiaSharp（随 LiveCharts 引入） | MIT |
| HarfBuzzSharp（随 SkiaSharp 引入） | MIT |
| OpenTK（随 LiveCharts 引入） | MIT |

## 文档站

| 组件 | 许可 |
| --- | --- |
| VitePress | MIT |
