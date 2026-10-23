using System.Diagnostics;
using WinTools.Core.Abstractions;
using WinTools.Core.Models;
using WinTools.Core.Utils;
using WinTools.Infrastructure.Native;

namespace WinTools.Infrastructure.Cleanup;

/// <summary>
/// 清理执行器：容错优先——单个文件失败不影响整体，被占用文件可安排重启后删除。
/// </summary>
public sealed class CleanupExecutor
{
    private readonly IAppLogger _logger;

    public CleanupExecutor(IAppLogger logger) => _logger = logger;

    public CleanupReport Execute(
        CleanupRequest request,
        IProgress<CleanupProgress>? progress,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var report = new CleanupReport();

        var work = request.Rules.Where(r => r.Rule.IsCleanable && r.HasContent).ToList();
        var index = 0;

        foreach (var ruleResult in work)
        {
            cancellationToken.ThrowIfCancellationRequested();
            index++;

            _logger.Audit("cleanup.start", ruleResult.Rule.Id,
                $"{ruleResult.Rule.Name.Resolve(string.Empty)} | {SizeFormatter.Format(ruleResult.TotalSize)}");

            foreach (var target in ruleResult.Targets)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!target.Exists)
                {
                    continue;
                }

                if (SafetyGuard.IsProtected(target.Path, out var reason))
                {
                    report.SkippedItems++;
                    report.Errors.Add($"[protected] {target.Path} ({reason})");
                    _logger.Warn("Cleanup", $"跳过受保护路径 {target.Path} ({reason})");
                    continue;
                }

                try
                {
                    if (target.Path.EndsWith("$RECYCLE.BIN", StringComparison.OrdinalIgnoreCase))
                    {
                        EmptyRecycleBin(target.Path);
                        report.FreedBytes += target.SizeBytes;
                        report.DeletedFiles += target.FileCount;
                        continue;
                    }

                    ProcessTarget(ruleResult.Rule, target, request, report, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    report.Errors.Add($"{target.Path}: {ex.Message}");
                    _logger.Warn("Cleanup", $"清理失败 {target.Path}: {ex.Message}");
                }
            }

            progress?.Report(new CleanupProgress
            {
                Stage = "clean",
                CompletedRules = index,
                TotalRules = work.Count,
                CurrentPath = ruleResult.Rule.Name.Resolve(string.Empty),
                BytesSoFar = report.FreedBytes
            });
        }

        report.Duration = stopwatch.Elapsed;
        _logger.Audit("cleanup.done", $"{work.Count} rules",
            $"freed={report.FreedBytes} files={report.DeletedFiles} errors={report.Errors.Count} elapsed={report.Duration.TotalSeconds:0.00}s");
        return report;
    }

    private void ProcessTarget(
        CleanupRule rule,
        CleanupTarget target,
        CleanupRequest request,
        CleanupReport report,
        CancellationToken cancellationToken)
    {
        var path = target.Path;

        if (File.Exists(path))
        {
            DeleteFileSafe(path, request, report);
            return;
        }

        if (!Directory.Exists(path))
        {
            return;
        }

        switch (rule.Strategy)
        {
            case CleanupStrategy.DeleteDirectory:
                DeleteTree(path, request, report, deleteRoot: true, cancellationToken);
                break;

            case CleanupStrategy.DeleteByAge:
                DeleteByAge(path, rule.AgeDays, request, report, cancellationToken);
                break;

            default:
                DeleteTree(path, request, report, deleteRoot: false, cancellationToken);
                break;
        }
    }

    private void DeleteTree(
        string directory,
        CleanupRequest request,
        CleanupReport report,
        bool deleteRoot,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string[] files;
        string[] subdirectories;
        try
        {
            files = Directory.GetFiles(directory);
            subdirectories = Directory.GetDirectories(directory);
        }
        catch (Exception ex)
        {
            report.Errors.Add($"{directory}: {ex.Message}");
            return;
        }

        foreach (var file in files)
        {
            DeleteFileSafe(file, request, report);
        }

        foreach (var sub in subdirectories)
        {
            // 不进入符号链接/联接点
            try
            {
                var attributes = File.GetAttributes(sub);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    continue;
                }
            }
            catch
            {
                continue;
            }

            DeleteTree(sub, request, report, deleteRoot: true, cancellationToken);
        }

        if (!deleteRoot)
        {
            return;
        }

        try
        {
            var attributes = File.GetAttributes(directory);
            if ((attributes & FileAttributes.ReadOnly) != 0)
            {
                File.SetAttributes(directory, attributes & ~FileAttributes.ReadOnly);
            }

            Directory.Delete(directory, recursive: false);
            report.DeletedDirectories++;
        }
        catch (Exception ex)
        {
            report.Errors.Add($"{directory}: {ex.Message}");
        }
    }

    private void DeleteByAge(
        string directory,
        int ageDays,
        CleanupRequest request,
        CleanupReport report,
        CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddDays(-Math.Max(1, ageDays));

        foreach (var file in SafeEnumerateFiles(directory, cancellationToken))
        {
            try
            {
                var info = new FileInfo(file);
                if (!info.Exists || info.LastWriteTimeUtc >= cutoff)
                {
                    continue;
                }

                DeleteFileSafe(file, request, report);
            }
            catch (Exception ex)
            {
                report.Errors.Add($"{file}: {ex.Message}");
            }
        }

        PruneEmptyDirectories(directory);
    }

    private IEnumerable<string> SafeEnumerateFiles(string directory, CancellationToken cancellationToken)
    {
        var stack = new Stack<string>();
        stack.Push(directory);

        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = stack.Pop();

            string[] files;
            string[] subdirectories;
            try
            {
                files = Directory.GetFiles(current);
                subdirectories = Directory.GetDirectories(current);
            }
            catch
            {
                continue;
            }

            foreach (var file in files)
            {
                yield return file;
            }

            foreach (var sub in subdirectories)
            {
                try
                {
                    if ((File.GetAttributes(sub) & FileAttributes.ReparsePoint) != 0)
                    {
                        continue;
                    }
                }
                catch
                {
                    continue;
                }

                stack.Push(sub);
            }
        }
    }

    private void PruneEmptyDirectories(string root)
    {
        try
        {
            foreach (var directory in Directory.GetDirectories(root))
            {
                PruneEmptyDirectories(directory);
                try
                {
                    if (!Directory.EnumerateFileSystemEntries(directory).Any())
                    {
                        Directory.Delete(directory, recursive: false);
                    }
                }
                catch
                {
                    // 忽略
                }
            }
        }
        catch
        {
            // 忽略
        }
    }

    private void DeleteFileSafe(string path, CleanupRequest request, CleanupReport report)
    {
        try
        {
            var attributes = File.GetAttributes(path);
            if ((attributes & (FileAttributes.ReadOnly | FileAttributes.Hidden | FileAttributes.System)) != 0)
            {
                File.SetAttributes(path, FileAttributes.Normal);
            }

            var length = new FileInfo(path).Length;
            File.Delete(path);
            report.FreedBytes += length;
            report.DeletedFiles++;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (request.ScheduleLockedForReboot &&
                NativeMethods.MoveFileEx(path, null, NativeMethods.MOVEFILE_DELAY_UNTIL_REBOOT))
            {
                report.SkippedItems++;
                _logger.Info("Cleanup", $"已安排重启后删除: {path}");
                return;
            }

            report.SkippedItems++;
            report.Errors.Add($"{path}: {ex.Message}");
        }
    }

    private void EmptyRecycleBin(string path)
    {
        var root = Path.GetPathRoot(path);
        var result = NativeMethods.SHEmptyRecycleBin(
            IntPtr.Zero,
            root,
            NativeMethods.SHERB_NOCONFIRMATION | NativeMethods.SHERB_NOPROGRESSUI | NativeMethods.SHERB_NOSOUND);

        _logger.Info("Cleanup", $"清空回收站 {root} => 0x{result:X}");
    }
}
