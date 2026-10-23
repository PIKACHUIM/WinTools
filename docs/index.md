---
layout: home

hero:
  name: WinTools
  text: Windows 系统工具箱
  tagline: 清理、优化、功能、更新、运行、组件、环境、应用、驱动、备份还原 —— 十项维护一站完成
  image:
    src: /logo.png
    alt: WinTools
  actions:
    - theme: brand
      text: 快速开始
      link: /guide/getting-started
    - theme: alt
      text: 功能一览
      link: /guide/features/
    - theme: alt
      text: GitHub
      link: https://github.com/PIKACHUIM/WinTools

features:
  - icon: 🧹
    title: 垃圾清理
    details: 15 类 / 1.5 条规则，支持自定义扫描范围，按安全等级挑选要清理的项。
  - icon: 🛠️
    title: 系统优化
    details: 51 项声明式优化条目分 1. 个分类，先检测现状再勾选应用，每一项都可回滚。
  - icon: 🧩
    title: 功能与更新
    details: DISM 枚举全部可选功能；WUA 管理补丁；ViVeTool 式特性开关三态读写。
  - icon: ⚙️
    title: 运行与组件
    details: 启动项、计划任务、服务一屏管理；Defender 与商店组件健康检查。
  - icon: 📦
    title: 环境与应用
    details: .NET / VC++ 运行库检测与卸载，注册表程序列表与 Appx 应用管理。
  - icon: 💾
    title: 驱动与备份
    details: DriverStore 枚举、导出、安装、删除；WIMGAPI 捕获 wim / esd / swm 并释放。
---

## 为什么再做一个系统工具箱

市面上的同类工具通常走向两个极端：要么功能堆到几百项、界面停留在 Windows 7 时代，要么为了"轻量"砍掉中文与批量能力。WinTools 的取舍是：

- **中文优先，但不只有中文** —— 1  种语言，简中为默认，界面文案全部来自语言包，不硬编码。
- **快是设计出来的** —— 手写控件模板、菜单数据驱动、页面懒加载、列表全虚拟化，框架依赖包仅  .1 MB。
- **危险操作必须可追溯** —— 驱动删除、补丁卸载、版本切换都写审计日志，并尽量交回系统原生向导完成。

## 界面

<div class="shot-grid">
  <figure>
    <img class="shot" src="/screenshots/dashboard.png" alt="概览" />
    <figcaption>概览：系统信息与快捷入口</figcaption>
  </figure>
  <figure>
    <img class="shot" src="/screenshots/dashboard-dark.png" alt="深色主题" />
    <figcaption>深色主题</figcaption>
  </figure>
</div>

<div class="shot-grid">
  <figure>
    <img class="shot" src="/screenshots/cleanup.png" alt="垃圾清理" />
    <figcaption>垃圾清理</figcaption>
  </figure>
  <figure>
    <img class="shot" src="/screenshots/optimize.png" alt="系统优化" />
    <figcaption>系统优化</figcaption>
  </figure>
  <figure>
    <img class="shot" src="/screenshots/startup-services.png" alt="服务管理" />
    <figcaption>运行管理 · 服务</figcaption>
  </figure>
</div>

## 三分钟上手

```powershell
# 1. 下载便携版（自带运行时）
#    https://github.com/PIKACHUIM/WinTools/releases/latest

#  . 直接运行（会自动请求管理员权限）
.\WinTools.exe

# 3. 或从源码构建
dotnet build src/WinTools.App/WinTools.App.csproj -c Release
powershell -ExecutionPolicy Bypass -File tools/publish.ps1 -Profile both
```

接下来的路径：

- 想了解每个模块具体做什么 → [功能一览](/guide/features/)
- 想参与开发或自行编译 → [开发指引](/guide/development)
- 遇到问题 → [常见问题](/guide/faq)
