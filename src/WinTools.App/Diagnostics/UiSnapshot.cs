using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WinTools.App.Services;
using WinTools.App.ViewModels;
using WinTools.App.ViewModels.Modules;
using WinTools.App.Views;
using WinTools.Core.Models;

namespace WinTools.App.Diagnostics;

/// <summary>
/// 离屏渲染界面快照：把每个菜单页渲染成 PNG，便于在无交互桌面环境下自检视觉效果。
/// 用法：WinTools.exe --shots &lt;输出目录&gt;（仅 DEBUG 构建）。
/// </summary>
internal static class UiSnapshot
{
    /// <summary>页面名带 :scan 后缀时，截图前先触发一次扫描。</summary>
    private static bool _scanShot;

    private static readonly string[] Pages =
    [
        "Dashboard",
        "Cleanup",
        "Migrate",
        "Optimize",
        "Features.SystemFeatures",
        "Features.EditionSwitch",
        "Updates.Installed",
        "Updates.Available",
        "Updates.FeatureFlags",
        "Startup.StartupItems",
        "Startup.ScheduledTasks",
        "Startup.Services",
        "Components.SecurityCenter",
        "Components.Store",
        "Runtimes.DotNet",
        "Runtimes.VcRuntime",
        "Apps.Programs",
        "Apps.Appx",
        "Drivers.SystemDrivers",
        "Drivers.DriverBackup",
        "Drivers.DriverInstall",
        "Drivers.DriverRemove",
        "Backup.Capture",
        "Backup.Incremental",
        "Backup.WimInfo",
        "Backup.Apply",
        "Settings"
    ];

    public static void Run(ShellWindow window, string directory, ThemeService theme, string? only = null)
    {
        Directory.CreateDirectory(directory);

        if (window.DataContext is not ShellViewModel shell)
        {
            return;
        }

        // 先吃掉 Loaded 里的 Initialize（否则它会在后续消息泵里把页面切回"上次停留页"）
        DoEvents(120);
        shell.Initialize();
        DoEvents(60);

        // 调试用：临时改变窗口尺寸，便于检查不同宽度下的布局与长页面
        if (int.TryParse(Environment.GetEnvironmentVariable("WINTOOLS_SHOT_WIDTH"), out var shotWidth) && shotWidth > 600)
        {
            window.MinWidth = 600;
            window.MaxWidth = 4000;
            window.Width = shotWidth;
        }

        if (int.TryParse(Environment.GetEnvironmentVariable("WINTOOLS_SHOT_HEIGHT"), out var shotHeight) && shotHeight > 400)
        {
            window.MinHeight = 400;
            window.MaxHeight = 4000;
            window.Height = shotHeight;
        }

        DoEvents(120);

        if (only is not null && only.EndsWith(":scan", StringComparison.OrdinalIgnoreCase))
        {
            _scanShot = true;
            only = only[..^5];
        }

        var pages = string.IsNullOrWhiteSpace(only)
            ? Pages
            : Pages.Where(p => p.Equals(only, StringComparison.OrdinalIgnoreCase)).ToArray();

        foreach (var key in pages)
        {
            Capture(window, shell, key, Path.Combine(directory, Sanitize(key) + ".png"));
        }

        // 深色主题再抓：固定两张 + 本次指定的页面
        theme.Apply(AppTheme.Dark);
        ShellViewModel_Refresh(shell);

        var darkPages = new List<string> { "Dashboard" };
        if (!string.IsNullOrWhiteSpace(only))
        {
            darkPages.Add(only);
        }

        darkPages.Add("Features.SystemFeatures");

        foreach (var key in darkPages.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            Capture(window, shell, key, Path.Combine(directory, Sanitize(key) + "-dark.png"));
        }
    }

    private static void ShellViewModel_Refresh(ShellViewModel shell)
    {
        // 主题切换后触发布局与资源刷新
        shell.Navigate(shell.SelectedNav?.Key);
    }

    private static void Capture(Window window, ShellViewModel shell, string key, string path)
    {
        Diag($"before {key}: sel={shell.SelectedNav?.Key} page={shell.CurrentPage?.GetType().Name}");
        shell.Navigate(key);
        Diag($"after  {key}: sel={shell.SelectedNav?.Key} page={shell.CurrentPage?.GetType().Name}");

        if (window.Content is not FrameworkElement content)
        {
            return;
        }

        var width = window.ActualWidth > 16 ? window.ActualWidth : window.Width;
        var height = window.ActualHeight > 16 ? window.ActualHeight : window.Height;

        // 调试覆盖：启动阶段窗口没有真正重新布局，ActualWidth 不会变化，这里直接按指定尺寸测量
        if (int.TryParse(Environment.GetEnvironmentVariable("WINTOOLS_SHOT_WIDTH"), out var overrideWidth) && overrideWidth > 600)
        {
            width = overrideWidth;
        }

        if (int.TryParse(Environment.GetEnvironmentVariable("WINTOOLS_SHOT_HEIGHT"), out var overrideHeight) && overrideHeight > 400)
        {
            height = overrideHeight;
        }

        // OnStartup 阶段窗口还未参与渲染，需手动完成一次测量/排列
        content.Measure(new Size(width, height));
        content.Arrange(new Rect(0, 0, width, height));
        Pump(window);
        WaitForData(window, shell);

        // 功能依赖联动：WINTOOLS_SHOT_DEPS=on 勾选 WSL 看自动带出；=off 关闭 Hyper-V 看连带提示
        if (Environment.GetEnvironmentVariable("WINTOOLS_SHOT_DEPS") is { Length: > 0 } depsMode &&
            shell.CurrentPage is ModuleViewModelBase module &&
            module.SelectedSection?.Content is SystemFeaturesSectionViewModel features)
        {
            var rows = features.Groups.SelectMany(g => g.Items).ToList();
            var wsl = rows.FirstOrDefault(r => r.Name.Contains("Subsystem-Linux", StringComparison.OrdinalIgnoreCase));

            if (wsl is not null)
            {
                wsl.IsOn = true;
                Thread.Sleep(400);
            }

            if (depsMode.Equals("off", StringComparison.OrdinalIgnoreCase))
            {
                var hyperV = rows.FirstOrDefault(r =>
                    r.IsOn && r.Name.StartsWith("Microsoft-Hyper-V", StringComparison.OrdinalIgnoreCase));

                if (hyperV is not null)
                {
                    hyperV.IsOn = false;
                }
            }

            Thread.Sleep(700);
        }

        // 需要看「扫描完成」样式的页面：WINTOOLS_SHOT_SCAN=1 时自动触发一次扫描
        if (_scanShot && shell.CurrentPage is CleanupViewModel cleanup && cleanup.IsIdlePhase)
        {
            cleanup.ScanCommand.Execute(null);
            var scanDeadline = Environment.TickCount64 + 60_000;
            while (Environment.TickCount64 < scanDeadline && !cleanup.IsReadyPhase)
            {
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
                Thread.Sleep(120);
            }

            // 展开第一条带文件明细的规则，方便肉眼检查「top N 路径 + 大小」
            var detail = cleanup.Groups
                .SelectMany(g => g.Items)
                .FirstOrDefault(i => i.SampleFiles.Count > 0);
            if (detail is not null)
            {
                detail.IsExpanded = true;
            }

            // WINTOOLS_SHOT_RISK=1：勾选一条高风险规则，截下二次确认弹窗
            if (Environment.GetEnvironmentVariable("WINTOOLS_SHOT_RISK") == "1")
            {
                var risky = cleanup.Groups
                    .SelectMany(g => g.Items)
                    .FirstOrDefault(i => i.RequiresRiskConfirm);
                if (risky is not null)
                {
                    risky.IsChecked = true;
                }
            }

            Thread.Sleep(900);
        }

        // 等待期间页面内容可能已切换，需重新布局一次再渲染
        content.Measure(new Size(width, height));
        content.Arrange(new Rect(0, 0, width, height));
        Pump(window);

        var pixelWidth = (int)Math.Ceiling(content.ActualWidth);
        var pixelHeight = (int)Math.Ceiling(content.ActualHeight);

        // 页面模板刚切换时 ActualWidth 可能还是 0，补几次布局再放弃
        for (var attempt = 0; attempt < 3 && (pixelWidth <= 0 || pixelHeight <= 0); attempt++)
        {
            Pump(window);
            Thread.Sleep(200);
            content.Measure(new Size(width, height));
            content.Arrange(new Rect(0, 0, width, height));
            pixelWidth = (int)Math.Ceiling(content.ActualWidth);
            pixelHeight = (int)Math.Ceiling(content.ActualHeight);
        }

        if (pixelWidth <= 0 || pixelHeight <= 0)
        {
            BootTrace.Log($"skipped empty snapshot: {key}");
            return;
        }

        // 先空跑一次渲染：ContentControl 换模板后视觉树会滞后一帧，第一次渲染用于同步
        var warmup = new RenderTargetBitmap(pixelWidth, pixelHeight, 96, 96, PixelFormats.Pbgra32);
        warmup.Render(content);
        Pump(window);

        // 窗口 Background 不在 Content 内，需先铺底色再贴内容，否则深色主题会渲染成透明
        var contentBitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, 96, 96, PixelFormats.Pbgra32);
        contentBitmap.Render(content);

        var area = new Rect(0, 0, pixelWidth, pixelHeight);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(window.Background ?? Brushes.White, null, area);
            dc.DrawImage(contentBitmap, area);
        }

        var bitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        BootTrace.Log($"bg={Describe(window.Background)}");

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);

        BootTrace.Log($"snapshot {key} -> {pixelWidth}x{pixelHeight}");
    }

    /// <summary>等待列表页签的异步加载完成（最多 12 秒），否则快照会拍到空列表。</summary>
    private static void WaitForData(Window window, ShellViewModel shell)
    {
        var deadline = Environment.TickCount64 + 12_000;

        while (Environment.TickCount64 < deadline)
        {
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);

            if (shell.CurrentPage is DashboardViewModel dashboard)
            {
                if (dashboard.Profile is not null && dashboard.SampleCount >= 2)
                {
                    break;
                }
            }
            else if (shell.CurrentPage is ModuleViewModelBase module &&
                     module.SelectedSection?.Content is SystemFeaturesSectionViewModel features)
            {
                if (!features.IsBusy && features.TotalCount > 0)
                {
                    break;
                }
            }
            else if (shell.CurrentPage is ModuleViewModelBase module2 &&
                     module2.SelectedSection?.Content is SectionViewModelBase section)
            {
                // 任何页签：等到不再是 Loading 再截图（空态与错误态也值得拍）
                if (section.State != PageState.Loading)
                {
                    break;
                }
            }
            else if (shell.CurrentPage is CleanupViewModel { IsIdlePhase: true })
            {
                // 环形图有入场动画，等它画完再截图，否则拍到空环
                Thread.Sleep(3200);
                break;
            }
            else
            {
                break;
            }

            Thread.Sleep(120);
        }

        Pump(window);
    }

    /// <summary>在 UI 线程上推进一段时间的消息泵（等价 DoEvents，但用帧而不是嵌套 Invoke，避免死锁）。</summary>
    private static void DoEvents(int milliseconds)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(milliseconds)
        };

        timer.Tick += (_, _) =>
        {
            timer.Stop();
            frame.Continue = false;
        };

        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    private static string Describe(Brush? brush) => brush switch
    {
        null => "null",
        SolidColorBrush s => "solid " + s.Color,
        LinearGradientBrush g => "gradient " + string.Join("/", g.GradientStops.Select(x => x.Color.ToString())),
        _ => brush.GetType().Name
    };

    /// <summary>让绑定、布局与渲染管线在离屏模式下跑完。</summary>
    private static void Pump(Window window)
    {
        window.UpdateLayout();
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
        window.UpdateLayout();
    }

    private static void Diag(string message)
    {
        try
        {
            File.AppendAllText(
                Path.Combine(Path.GetTempPath(), "wintools-shots.log"),
                message + Environment.NewLine);
        }
        catch
        {
            // 忽略
        }
    }

    private static string Sanitize(string key) => key.Replace('.', '-').ToLowerInvariant();
}
