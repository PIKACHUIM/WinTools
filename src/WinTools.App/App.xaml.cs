using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using WinTools.App.Diagnostics;
using WinTools.App.Localization;
using WinTools.App.Services;
using WinTools.App.ViewModels;
using WinTools.App.ViewModels.Modules;
using WinTools.App.Views;
using WinTools.Core.Abstractions;
using WinTools.Core.Models;
using WinTools.Infrastructure;

namespace WinTools.App;

public partial class App : Application
{
    private static Mutex? _singleInstanceMutex;
    private ServiceProvider? _services;

    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        BootTrace.Reset();
        BootTrace.Log("OnStartup");

        // 单实例：已有实例在跑就直接退出。
        // 诊断模式（截图/扫描探针）不参与单实例：否则残留实例会让自动截图全部静默失败。
        var isDiagnostic = e.Args.Contains("--shots") || e.Args.Contains("--scan") || e.Args.Contains("--components") || e.Args.Contains("--runtimes");
        if (!isDiagnostic)
        {
            _singleInstanceMutex = new Mutex(initiallyOwned: true, "WinTools.SingleInstance", out var isNew);
            if (!isNew)
            {
                Shutdown();
                return;
            }
        }

        base.OnStartup(e);

        var collection = new ServiceCollection();
        collection.AddWinToolsInfrastructure();
        collection.AddSingleton<LocalizationService>();
        collection.AddSingleton<ThemeService>();
        collection.AddSingleton<ShellViewModel>();
        collection.AddSingleton<DashboardViewModel>();
        collection.AddSingleton<CleanupViewModel>();
        collection.AddSingleton<OptimizeViewModel>();
        collection.AddSingleton<SettingsViewModel>();

        // 八大模块页（懒加载：首次导航时才实例化）
        collection.AddSingleton<FeatureManagementViewModel>();
        collection.AddSingleton<UpdateManagementViewModel>();
        collection.AddSingleton<RuntimeManagementViewModel>();
        collection.AddSingleton<ComponentManagementViewModel>();
        collection.AddSingleton<RuntimeEnvironmentViewModel>();
        collection.AddSingleton<AppManagementViewModel>();
        collection.AddSingleton<DriverManagementViewModel>();
        collection.AddSingleton<BackupRestoreViewModel>();

        collection.AddSingleton<ShellWindow>();

        _services = collection.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
        Services = _services;

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

        BootTrace.Log("DI built");

        var settings = _services.GetRequiredService<ISettingsService>();
        settings.Load();
        BootTrace.Log("settings loaded");

        var localization = _services.GetRequiredService<LocalizationService>();
        localization.SetLanguage(settings.Current.Language);
        BootTrace.Log("language set");

        var theme = _services.GetRequiredService<ThemeService>();
        theme.Apply(settings.Current.Theme);
        BootTrace.Log("theme applied");

        try
        {
        // ---- 诊断入口（无界面）：--components 输出组件服务耗时 ----
        if (e.Args.Contains("--components"))
        {
            var store = _services.GetRequiredService<IStoreService>();
            var log = new List<string>();
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                var apps = Task.Run(() => store.GetAppsAsync(CancellationToken.None)).GetAwaiter().GetResult();
                log.Add($"apps={apps.Count} installed={apps.Count(a => a.IsInstalled)} elapsed={sw.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                log.Add("apps FAILED: " + ex.Message);
            }

            sw.Restart();
            try
            {
                var task = Task.Run(() => store.GetWslAsync(false, CancellationToken.None));
                log.Add(task.Wait(TimeSpan.FromSeconds(20))
                    ? $"wsl={task.Result.Count} elapsed={sw.ElapsedMilliseconds}ms"
                    : "wsl TIMEOUT>20s");
            }
            catch (Exception ex)
            {
                log.Add("wsl FAILED: " + ex.Message);
            }

            File.WriteAllText(
                Path.Combine(Path.GetTempPath(), "wintools-components.txt"),
                string.Join(Environment.NewLine, log));

            Shutdown(0);
            return;
        }

        // ---- 诊断入口（无界面）：--runtimes 验证在线版本清单 ----
        if (e.Args.Contains("--runtimes"))
        {
            var rt = _services.GetRequiredService<IRuntimeService>();
            var log = new List<string>();

            foreach (var kind in new[] { "dotnet", "vcpp" })
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    var packages = Task.Run(() => rt.GetAvailableAsync(kind, CancellationToken.None)).GetAwaiter().GetResult();
                    log.Add($"{kind}: {packages.Count} 项 / {sw.ElapsedMilliseconds}ms");
                    foreach (var pkg in packages.Take(6))
                    {
                        log.Add($"  {pkg.Id} {pkg.Version} {pkg.Architecture} {(pkg.SizeBytes > 0 ? $"{pkg.SizeBytes / 1024 / 1024}MB" : "?")} hash={(string.IsNullOrEmpty(pkg.Sha512) ? "sig" : "sha512")}");
                    }
                }
                catch (Exception ex)
                {
                    log.Add($"{kind} FAILED: {ex.Message}");
                }
            }

            File.WriteAllText(Path.Combine(Path.GetTempPath(), "wintools-runtimes.txt"), string.Join(Environment.NewLine, log));
            Shutdown(0);
            return;
        }

        // ---- 诊断入口（无界面）：--scan 输出全盘扫描耗时 ----
        if (e.Args.Contains("--scan"))
        {
            var cleanup = _services.GetRequiredService<ICleanupService>();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var ids = cleanup.Rules.Where(r => r.Enabled).Select(r => r.Id).ToList();

            // 放到线程池里同步等待：UI 线程上直接 GetResult 会与 ConfigureAwait(true) 形成死锁
            var summary = Task.Run(() => cleanup.ScanAsync(ids, null, null, CancellationToken.None))
                .GetAwaiter().GetResult();
            watch.Stop();

            File.WriteAllText(
                Path.Combine(Path.GetTempPath(), "wintools-scan.txt"),
                $"rules={ids.Count} hits={summary.Rules.Count(r => r.HasContent)} files={summary.TotalFiles} size={summary.TotalSize} elapsed={watch.ElapsedMilliseconds}ms");

            Shutdown(0);
            return;
        }
        var window = _services.GetRequiredService<ShellWindow>();
            BootTrace.Log("window resolved");
            MainWindow = window;
            window.Show();
            BootTrace.Log("window shown");

#if DEBUG
            var shotsIndex = Array.IndexOf(e.Args, "--shots");
            if (shotsIndex >= 0 && shotsIndex + 1 < e.Args.Length)
            {
                UiSnapshot.Run(window, e.Args[shotsIndex + 1], theme, shotsIndex + 2 < e.Args.Length ? e.Args[shotsIndex + 2] : null);
                Shutdown(0);
            }
#endif
        }
        catch (Exception ex)
        {
            // 启动期致命错误：记录并退出，避免留下无窗口的僵死进程
            BootTrace.Log("startup failed: " + ex);
            Log(ex);
            MessageBox.Show(ex.ToString(), "WinTools 启动失败", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _services?.GetService<ISettingsService>()?.Save();
        }
        catch
        {
            // 忽略
        }

        _services?.Dispose();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log(e.Exception);
        MessageBox.Show(e.Exception.Message, "WinTools", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            Log(ex);
        }
    }

    private void Log(Exception ex)
    {
        try
        {
            _services?.GetService<IAppLogger>()?.Error("App", "未处理异常", ex);
        }
        catch
        {
            // 忽略
        }
    }
}
