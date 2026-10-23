namespace WinTools.Core.Models;

/// <summary>一个具体路径的扫描结果。</summary>
public sealed class CleanupTarget
{
    public string Path { get; init; } = string.Empty;
    public long SizeBytes { get; set; }
    public long FileCount { get; set; }
    public long DirectoryCount { get; set; }
    public bool AccessDenied { get; set; }
    public bool Exists { get; set; }
}

/// <summary>单条规则的扫描结果。</summary>
public sealed class CleanupRuleResult
{
    public required CleanupRule Rule { get; init; }
    public List<CleanupTarget> Targets { get; } = new();
    public List<string> Errors { get; } = new();

    public long TotalSize => Targets.Sum(t => t.SizeBytes);
    public long TotalFiles => Targets.Sum(t => t.FileCount);
    public bool HasContent => TotalSize > 0 || TotalFiles > 0;
}

public sealed class CleanupScanSummary
{
    public List<CleanupRuleResult> Rules { get; } = new();
    public TimeSpan Duration { get; set; }
    public int ScannedRuleCount { get; set; }

    public long TotalSize => Rules.Sum(r => r.TotalSize);
    public long TotalFiles => Rules.Sum(r => r.TotalFiles);
}

public sealed class CleanupProgress
{
    public string Stage { get; init; } = string.Empty;
    public int CompletedRules { get; init; }
    public int TotalRules { get; init; }
    public string? CurrentPath { get; init; }
    public long BytesSoFar { get; init; }

    public double Percent => TotalRules <= 0 ? 0 : Math.Min(100d, CompletedRules * 100d / TotalRules);
}

public sealed class CleanupReport
{
    public long FreedBytes { get; set; }
    public long DeletedFiles { get; set; }
    public long DeletedDirectories { get; set; }
    public int SkippedItems { get; set; }
    public List<string> Errors { get; } = new();
    public TimeSpan Duration { get; set; }
    public bool Succeeded => Errors.Count == 0;
}

public sealed class CleanupRequest
{
    /// <summary>要清理的规则（通常来自扫描结果中用户勾选的项）。</summary>
    public List<CleanupRuleResult> Rules { get; init; } = new();
    /// <summary>是否使用回收站（更安全，但慢）。</summary>
    public bool UseRecycleBin { get; init; }
    /// <summary>被占用文件是否安排重启后删除。</summary>
    public bool ScheduleLockedForReboot { get; init; } = true;
}
