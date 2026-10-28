using Microsoft.Extensions.DependencyInjection;
using WinTools.Core.Abstractions;
using WinTools.Infrastructure.Apps;
using WinTools.Infrastructure.Backup;
using WinTools.Infrastructure.Cleanup;
using WinTools.Infrastructure.Components;
using WinTools.Infrastructure.Drivers;
using WinTools.Infrastructure.Features;
using WinTools.Infrastructure.Logging;
using WinTools.Infrastructure.Migration;
using WinTools.Infrastructure.Rules;
using WinTools.Infrastructure.Run;
using WinTools.Infrastructure.Runtimes;
using WinTools.Infrastructure.Settings;
using WinTools.Infrastructure.System;
using WinTools.Infrastructure.Tweaks;
using WinTools.Infrastructure.Updates;

namespace WinTools.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddWinToolsInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IAppLogger, FileLogger>();
        services.AddSingleton<ISettingsService, SettingsService>();

        services.AddSingleton<RuleRepository>();
        services.AddSingleton<IModuleCatalog>(sp => sp.GetRequiredService<RuleRepository>());

        // 清理 / 优化
        services.AddSingleton<CleanupScanner>();
        services.AddSingleton<CleanupExecutor>();
        services.AddSingleton<ICleanupService, CleanupService>();
        services.AddSingleton<ITweakService, TweakService>();
        services.AddSingleton<SystemInfoService>();
        services.AddSingleton<System.HardwareInfoService>();
        services.AddSingleton<SystemMetricsService>();

        // 功能管理
        services.AddSingleton<IFeatureService, FeatureService>();

        // 更新管理
        services.AddSingleton<IUpdateService, UpdateService>();
        services.AddSingleton<IFeatureFlagService, FeatureFlagService>();

        // 运行管理
        services.AddSingleton<IStartupItemService, StartupItemService>();
        services.AddSingleton<IScheduledTaskService, ScheduledTaskService>();
        services.AddSingleton<IWindowsServiceManager, WindowsServiceManager>();

        // 组件管理
        services.AddSingleton<ISecurityService, SecurityService>();
        services.AddSingleton<IStoreService, StoreService>();

        // 运行环境 / 应用 / 驱动
        services.AddSingleton<IRuntimeService, RuntimeService>();
        services.AddSingleton<IProgramService, ProgramService>();
        services.AddSingleton<IAppxService, AppxService>();
        services.AddSingleton<IDriverService, DriverService>();

        // 备份还原
        services.AddSingleton<IWimService, WimService>();
    services.AddSingleton<IMigrationService, MigrationService>();

        return services;
    }
}
