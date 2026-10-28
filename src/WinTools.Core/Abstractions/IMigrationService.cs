using WinTools.Core.Models;

namespace WinTools.Core.Abstractions;

/// <summary>文件迁移：系统已知文件夹（桌面/文档/下载等）与应用数据目录。</summary>
public interface IMigrationService
{
    /// <summary>扫描可迁移目录（不统计大小，保证页面秒开）。</summary>
    Task<IReadOnlyList<MigrationTarget>> DetectAsync(CancellationToken cancellationToken);

    /// <summary>统计单个目录的文件数与占用。</summary>
    Task<(long Bytes, long Files)> MeasureAsync(MigrationTarget target, CancellationToken cancellationToken);

    /// <summary>迁移单个目录：复制 → 校验 → 删除源 → 切换系统路径 / 创建目录联接。</summary>
    Task<MigrationItemResult> MigrateAsync(
        MigrationRequest request,
        IProgress<MigrationProgress>? progress,
        CancellationToken cancellationToken);
}
