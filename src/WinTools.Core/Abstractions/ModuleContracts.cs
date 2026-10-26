namespace WinTools.Core.Abstractions;

using WinTools.Core.Models;

/// <summary>功能管理：Windows 可选功能 + 版本渠道。</summary>
public interface IFeatureService
{
    Task<IReadOnlyList<OptionalFeature>> GetFeaturesAsync(CancellationToken cancellationToken);
    Task<OperationResult> SetFeatureAsync(string featureName, bool enable, CancellationToken cancellationToken);
    Task<EditionInfo> GetEditionAsync(CancellationToken cancellationToken);
    Task<OperationResult> ChangeEditionAsync(string productKey, CancellationToken cancellationToken);
}

/// <summary>更新管理：已安装更新 / 可安装补丁。</summary>
public interface IUpdateService
{
    Task<IReadOnlyList<UpdateEntry>> GetInstalledAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<UpdateEntry>> SearchAvailableAsync(IProgress<string>? progress, CancellationToken cancellationToken);
    Task<OperationResult> InstallAsync(IReadOnlyCollection<string> updateIds, IProgress<string>? progress, CancellationToken cancellationToken);
    Task<OperationResult> UninstallAsync(string updateId, CancellationToken cancellationToken);
}

/// <summary>特性开关（ViVe 式）。</summary>
public interface IFeatureFlagService
{
    Task<IReadOnlyList<FeatureFlagEntry>> GetOverridesAsync(CancellationToken cancellationToken);
    Task<OperationResult> SetEnabledStateAsync(FeatureFlagEntry entry, int enabledState, CancellationToken cancellationToken);
    Task<OperationResult> AddAsync(string featureId, int enabledState, CancellationToken cancellationToken);
    Task<OperationResult> RemoveAsync(FeatureFlagEntry entry, CancellationToken cancellationToken);
}

/// <summary>运行管理：启动项。</summary>
public interface IStartupItemService
{
    Task<IReadOnlyList<StartupEntry>> GetAsync(CancellationToken cancellationToken);
    Task<OperationResult> SetEnabledAsync(StartupEntry entry, bool enabled, CancellationToken cancellationToken);
    Task<OperationResult> DeleteAsync(StartupEntry entry, CancellationToken cancellationToken);
}

/// <summary>运行管理：计划任务。</summary>
public interface IScheduledTaskService
{
    Task<IReadOnlyList<ScheduledTaskEntry>> GetAsync(CancellationToken cancellationToken);
    Task<OperationResult> SetEnabledAsync(ScheduledTaskEntry entry, bool enabled, CancellationToken cancellationToken);
    Task<OperationResult> RunAsync(ScheduledTaskEntry entry, CancellationToken cancellationToken);
    Task<OperationResult> DeleteAsync(ScheduledTaskEntry entry, CancellationToken cancellationToken);
}

/// <summary>运行管理：服务。</summary>
public interface IWindowsServiceManager
{
    Task<IReadOnlyList<WindowsServiceEntry>> GetAsync(CancellationToken cancellationToken);
    Task<OperationResult> SetStartTypeAsync(WindowsServiceEntry entry, string startType, CancellationToken cancellationToken);
    Task<OperationResult> StartAsync(WindowsServiceEntry entry, CancellationToken cancellationToken);
    Task<OperationResult> StopAsync(WindowsServiceEntry entry, CancellationToken cancellationToken);
}

/// <summary>组件管理：安全中心。</summary>
public interface ISecurityService
{
    Task<SecurityStatus> GetAsync(CancellationToken cancellationToken);
    Task<OperationResult> SetRealTimeAsync(bool enabled, CancellationToken cancellationToken);
    /// <summary>全部可开关的安全设置（实时保护、攻击面、防火墙、UAC、内核隔离…）。</summary>
    Task<IReadOnlyList<SecuritySetting>> GetSettingsAsync(CancellationToken cancellationToken);

    Task<OperationResult> SetSettingAsync(string id, bool enabled, CancellationToken cancellationToken);

    Task<OperationResult> AddExclusionAsync(string path, CancellationToken cancellationToken);
    Task<OperationResult> RemoveExclusionAsync(string path, CancellationToken cancellationToken);
}

/// <summary>组件管理：应用商店。</summary>
public interface IStoreService
{
    Task<StoreStatus> GetAsync(CancellationToken cancellationToken);
    Task<OperationResult> ResetCacheAsync(CancellationToken cancellationToken);
    Task<OperationResult> ReRegisterAsync(CancellationToken cancellationToken);

    /// <summary>内置应用与可选组件的清单（含安装状态与版本）。</summary>
    Task<IReadOnlyList<StoreApp>> GetAppsAsync(CancellationToken cancellationToken);

    Task<OperationResult> InstallAppAsync(StoreApp app, CancellationToken cancellationToken);

    Task<OperationResult> UninstallAppAsync(StoreApp app, CancellationToken cancellationToken);

    /// <summary>修复：重新注册包清单。</summary>
    Task<OperationResult> RepairAppAsync(StoreApp app, CancellationToken cancellationToken);

    /// <summary>WSL 发行版（已装 + 可在线安装）。</summary>
    /// <summary>WSL 发行版。includeOnline=true 时联机拉取可安装清单（慢，按需调用）。</summary>
    Task<IReadOnlyList<WslDistro>> GetWslAsync(bool includeOnline, CancellationToken cancellationToken);

    Task<OperationResult> InstallWslAsync(WslDistro distro, CancellationToken cancellationToken);
}

/// <summary>运行环境：.NET / VC++。</summary>
public interface IRuntimeService
{
    Task<IReadOnlyList<InstalledRuntime>> GetDotNetAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<InstalledRuntime>> GetVcRuntimeAsync(CancellationToken cancellationToken);
    Task<OperationResult> UninstallAsync(InstalledRuntime runtime, CancellationToken cancellationToken);

    /// <summary>可在线安装的版本清单（kind：dotnet / vcpp）。需要联网，按需调用。</summary>
    Task<IReadOnlyList<RuntimePackage>> GetAvailableAsync(string kind, CancellationToken cancellationToken);

    /// <summary>下载 → 校验 → 静默安装。</summary>
    Task<OperationResult> InstallAsync(
        RuntimePackage package,
        IProgress<double>? progress,
        CancellationToken cancellationToken);
}

/// <summary>应用管理：已安装程序。</summary>
public interface IProgramService
{
    Task<IReadOnlyList<InstalledProgram>> GetAsync(CancellationToken cancellationToken);
    Task<OperationResult> UninstallAsync(InstalledProgram program, CancellationToken cancellationToken);
}

/// <summary>应用管理：Appx / MSIX。</summary>
public interface IAppxService
{
    Task<IReadOnlyList<AppxPackageEntry>> GetAsync(CancellationToken cancellationToken);
    Task<OperationResult> RemoveAsync(AppxPackageEntry package, CancellationToken cancellationToken);
}

/// <summary>驱动管理。</summary>
public interface IDriverService
{
    Task<IReadOnlyList<DriverPackage>> GetAsync(CancellationToken cancellationToken);
    Task<OperationResult> ExportAsync(string targetDirectory, IProgress<string>? progress, CancellationToken cancellationToken);
    Task<OperationResult> InstallAsync(string path, CancellationToken cancellationToken);
    Task<OperationResult> RemoveAsync(DriverPackage driver, CancellationToken cancellationToken);
}

/// <summary>备份还原：WIMGAPI 封装。</summary>
public interface IWimService
{
    Task<IReadOnlyList<WimImageInfo>> GetInfoAsync(string imagePath, CancellationToken cancellationToken);
    Task<OperationResult> CaptureAsync(WimCaptureRequest request, IProgress<WimProgress>? progress, CancellationToken cancellationToken);
    Task<OperationResult> ApplyAsync(WimApplyRequest request, IProgress<WimProgress>? progress, CancellationToken cancellationToken);
    Task<OperationResult> SetImageInfoAsync(string imagePath, int imageIndex, string name, string? description, CancellationToken cancellationToken);
}
