namespace WinTools.Core.Models;

/// <summary>通用操作结果：所有模块的统一返回，便于 UI 一致展示。</summary>
public sealed record OperationResult(bool Success, string Message, bool RestartRequired = false, int Affected = 0)
{
    public static OperationResult Ok(string message, int affected = 0, bool restartRequired = false)
        => new(true, message, restartRequired, affected);

    public static OperationResult Fail(string message)
        => new(false, message);
}

/// <summary>Windows 可选功能（功能管理）。</summary>
public sealed record OptionalFeature(string Name, string DisplayName, bool IsEnabled, bool RestartRequired);

/// <summary>系统版本与激活信息（版本渠道切换）。</summary>
public sealed record EditionInfo(
    string ProductName,
    string EditionId,
    string DisplayVersion,
    string Build,
    string InstallationType,
    bool IsActivated,
    string? PartialProductKey);

/// <summary>更新条目（更新管理）。</summary>
public sealed record UpdateEntry(
    string Id,
    string Title,
    string? Kb,
    DateTime? Date,
    long? SizeBytes,
    bool IsInstalled,
    string? Severity);

/// <summary>特性开关覆盖项（FeatureManagement\Overrides）。</summary>
public sealed record FeatureFlagEntry(
    string FeatureId,
    int EnabledState,
    int EnabledStateOptions,
    int Variant,
    int VariantPayloadKind);

/// <summary>启动项（运行管理）。</summary>
public sealed record StartupEntry(
    string Id,
    string Name,
    string Command,
    string LocationKey,
    string Scope,
    bool IsEnabled,
    bool CanToggle);

/// <summary>计划任务（运行管理）。</summary>
public sealed record ScheduledTaskEntry(
    string Path,
    string Name,
    string State,
    string? TriggerSummary,
    bool IsEnabled,
    string? Author);

/// <summary>Windows 服务（运行管理）。</summary>
public sealed record WindowsServiceEntry(
    string Name,
    string DisplayName,
    string Status,
    string StartType,
    string? Description,
    bool CanStop);

/// <summary>安全中心状态（组件管理）。</summary>
public sealed record SecurityStatus(
    bool DefenderPresent,
    bool RealTimeEnabled,
    bool BehaviorMonitorEnabled,
    bool TamperProtection,
    string? AntivirusProduct,
    string? EngineVersion,
    IReadOnlyList<string> Exclusions);

/// <summary>安全中心的一个可开关项。</summary>
public sealed record SecuritySetting(string Id, bool Enabled, bool Supported);

/// <summary>应用商店状态（组件管理）。</summary>
public sealed record StoreStatus(
    bool Installed,
    string? Version,
    bool ServiceRunning,
    string? InstallLocation);

/// <summary>已安装运行环境（运行环境）。</summary>
public sealed record InstalledRuntime(
    string Name,
    string Version,
    string Architecture,
    string? UninstallString,
    string? InstallLocation);

/// <summary>已安装程序（应用管理）。</summary>
public sealed record InstalledProgram(
    string Id,
    string Name,
    string? Publisher,
    string? Version,
    string? InstallDate,
    long? SizeBytes,
    string? UninstallString,
    string? QuietUninstallString,
    string Scope);

/// <summary>Appx / MSIX 包（应用管理）。</summary>
public sealed record AppxPackageEntry(
    string Name,
    string? Version,
    string? Publisher,
    string? PackageFullName,
    bool IsFramework,
    string? InstallLocation);

/// <summary>驱动包（驱动管理）。</summary>
public sealed record DriverPackage(
    string PublishedName,
    string OriginalName,
    string? Provider,
    string? ClassName,
    string? Version,
    string? Date);

/// <summary>WIM 映像索引信息（备份还原）。</summary>
public sealed record WimImageInfo(
    int Index,
    string Name,
    string? Description,
    long? TotalBytes,
    int? FileCount,
    string? Compression);

/// <summary>WIM 捕获请求。</summary>
public sealed record WimCaptureRequest(
    string SourcePath,
    string ImagePath,
    string ImageName,
    string? Description,
    bool Append,
    uint CompressionType);

/// <summary>WIM 释放请求。</summary>
public sealed record WimApplyRequest(
    string ImagePath,
    int ImageIndex,
    string TargetPath,
    bool Verify);

/// <summary>WIM 操作进度。</summary>
public sealed record WimProgress(long CurrentBytes, long TotalBytes, int Percent, string Stage)
{
    public string Text => $"{Stage} {Percent}%";
}
