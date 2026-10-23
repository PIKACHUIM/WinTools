namespace WinTools.Core.Abstractions;

using WinTools.Core.Models;

/// <summary>结构化审计日志：所有破坏性操作都要落盘。</summary>
public interface IAppLogger
{
    string LogDirectory { get; }
    void Info(string category, string message);
    void Warn(string category, string message);
    void Error(string category, string message, Exception? ex = null);
    /// <summary>记录一次可撤销/需审计的操作明细。</summary>
    void Audit(string action, string target, string? detail = null);
}

public interface ISettingsService
{
    AppSettings Current { get; }
    string SettingsDirectory { get; }
    void Load();
    void Save();
}

public interface ICleanupService
{
    IReadOnlyList<CleanupCategory> Categories { get; }
    IReadOnlyList<CleanupRule> Rules { get; }

    Task<CleanupScanSummary> ScanAsync(
        IReadOnlyCollection<string> enabledRuleIds,
        IReadOnlyList<string>? scopeRoots,
        IProgress<CleanupProgress>? progress,
        CancellationToken cancellationToken);

    Task<CleanupReport> CleanAsync(
        CleanupRequest request,
        IProgress<CleanupProgress>? progress,
        CancellationToken cancellationToken);
}

/// <summary>一级/二级菜单目录（数据驱动，见 Rules/modules.json）。</summary>
public interface IModuleCatalog
{
    IReadOnlyList<ModuleDefinition> Modules { get; }
}

public interface ITweakService
{
    IReadOnlyList<TweakGroup> Groups { get; }
    IReadOnlyList<TweakDefinition> Tweaks { get; }

    Task<IReadOnlyList<TweakState>> DetectAsync(CancellationToken cancellationToken);

    Task<TweakApplyReport> ApplyAsync(IReadOnlyCollection<string> tweakIds, CancellationToken cancellationToken);

    Task<TweakApplyReport> RevertAsync(IReadOnlyCollection<string> tweakIds, CancellationToken cancellationToken);
}

public sealed record SystemSnapshot(
    string OsName,
    string OsVersion,
    string CpuName,
    int LogicalCores,
    long TotalMemoryBytes,
    long AvailableMemoryBytes,
    string SystemDrive,
    long SystemDriveTotalBytes,
    long SystemDriveFreeBytes,
    bool IsElevated);
