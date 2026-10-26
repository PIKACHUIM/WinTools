---
layout: home

hero:
  name: WinTools
  text: Windows 系统工具箱
  tagline: 把常用的 Windows 维护功能放在一个桌面程序里
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
    details: 按类别扫描文件，可自定义扫描范围，并查看大小与安全等级后再清理。
  - icon: 🛠️
    title: 系统优化
    details: 先检查当前设置，再选择要应用的项目；支持按记录回滚。
  - icon: 🧩
    title: 功能与更新
    details: 管理 Windows 可选功能、已安装更新、可安装补丁和系统特性开关。
  - icon: ⚙️
    title: 运行与组件
    details: 管理启动项、计划任务和服务，并查看 Defender 与系统组件状态。
  - icon: 📦
    title: 环境与应用
    details: 查看和管理 .NET、VC++ 运行库、已安装程序与 Appx 应用。
  - icon: 💾
    title: 驱动与备份
    details: 备份、安装或删除驱动；创建并还原 WIM 系列系统映像。
---

## 常用维护功能，按模块整理

WinTools 面向 Windows 10 和 Windows 11，把清理、优化、更新、启动项、服务、驱动和备份等操作集中在一个桌面程序中。操作前可以查看当前状态；清理项目会标注安全等级，高风险操作也会记录在日志中。

- **界面支持 12 种语言**，默认使用简体中文。
- 页面首次访问时再加载，长列表按需呈现。
- 涉及系统更改时调用 Windows 提供的接口；部分高风险操作会交由系统工具处理。

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

## 开始使用

```powershell
# 1. 下载便携版（自带运行时）
#    https://github.com/PIKACHUIM/WinTools/releases/latest

# 2. 直接运行（会自动请求管理员权限）
.\WinTools.exe

# 3. 或从源码构建
dotnet build src/WinTools.App/WinTools.App.csproj -c Release
powershell -ExecutionPolicy Bypass -File tools/publish.ps1 -Profile both
```

接下来可以：

- 想了解每个模块具体做什么 → [功能一览](/guide/features/)
- 想参与开发或自行编译 → [开发指引](/guide/development)
- 遇到问题 → [常见问题](/guide/faq)
