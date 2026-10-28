namespace WinTools.Core.Models;

/// <summary>迁移对象类型：系统已知文件夹 / 应用数据目录。</summary>
public enum MigrationKind
{
    KnownFolder,
    AppFolder
}

/// <summary>迁移方式。</summary>
public enum MigrationAction
{
    /// <summary>移动：复制后删除源，并切换系统路径或建立目录联接</summary>
    Move,
    /// <summary>复制：只复制一份，不动原目录</summary>
    Copy,
    /// <summary>不处理</summary>
    Leave
}

/// <summary>可迁移的目录（扫描结果）。</summary>
public sealed class MigrationTarget
{
    public required string Id { get; init; }
    public required MigrationKind Kind { get; init; }
    public LocalizedText Name { get; init; } = new();
    public LocalizedText? Description { get; init; }
    public required string CurrentPath { get; init; }

    /// <summary>系统默认路径（仅已知文件夹），用于「恢复默认路径」。</summary>
    public string? DefaultPath { get; init; }

    /// <summary>需要关闭的进程名（不带 .exe）。</summary>
    public string? ProcessName { get; init; }

    /// <summary>系统已知文件夹的 GUID；应用目录为 Guid.Empty。</summary>
    public Guid KnownFolderId { get; init; }

    public bool Exists { get; init; }
    public long SizeBytes { get; init; }
    public long FileCount { get; init; }

    public bool HasContent => SizeBytes > 0 || FileCount > 0;
}

/// <summary>单个目录的迁移请求。</summary>
public sealed record MigrationRequest(
    string Id,
    string SourcePath,
    string TargetPath,
    bool CopyOnly,
    bool LinkBack,
    Guid KnownFolderId);

/// <summary>迁移进度。</summary>
public sealed record MigrationProgress(
    string Id,
    string Stage,
    int Percent,
    long DoneBytes,
    long TotalBytes);

/// <summary>单个目录的迁移结果。</summary>
public sealed record MigrationItemResult(
    string Id,
    bool Success,
    string Message,
    long MovedBytes = 0,
    long Files = 0,
    bool RestartRequired = false);
