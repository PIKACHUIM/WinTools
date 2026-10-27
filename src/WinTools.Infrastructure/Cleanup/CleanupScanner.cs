using System.Diagnostics;
using System.IO.Enumeration;
using WinTools.Core.Abstractions;
using WinTools.Core.Models;
using WinTools.Core.Utils;

namespace WinTools.Infrastructure.Cleanup;

/// <summary>
/// 高性能扫描器：
/// 1) 先做路径解析 + 全局去重（避免 BRW-09/BRW-10 这类重叠规则重复统计）
/// 2) 再按并发度并行统计体积（IO 密集，并发 2~4 最佳）
/// 3) 体积统计走 FileSystemEnumerable（复用 FindFirstFileEx 的数据，避免逐文件 stat）
/// </summary>
public sealed class CleanupScanner
{
    /// <summary>每条规则保留几个代表文件（展开明细用）。</summary>
    private const int SampleLimit = 8;

    private readonly IAppLogger _logger;

    public CleanupScanner(IAppLogger logger) => _logger = logger;

    public async Task<CleanupScanSummary> ScanAsync(
        IReadOnlyList<CleanupRule> rules,
        int concurrency,
        IReadOnlyList<string>? scopeRoots,
        IProgress<CleanupProgress>? progress,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var summary = new CleanupScanSummary { ScannedRuleCount = rules.Count };

        // ---- 阶段 1：解析路径并全局去重（很快，纯目录枚举） ----
        var plan = new List<(CleanupRule Rule, List<CleanupTarget> Targets)>(rules.Count);
        var claimed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rule in rules)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var targets = new List<CleanupTarget>();
            var lastIsFile = rule.Match is CleanupMatchKind.GlobFile
                or CleanupMatchKind.SingleFile
                or CleanupMatchKind.SystemFile;

            foreach (var template in rule.Paths)
            {
                IReadOnlyList<string> expanded;
                try
                {
                    expanded = PathTemplate.Expand(template, lastIsFile, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.Warn("Cleanup", $"展开模板失败 {template}: {ex.Message}");
                    continue;
                }

                foreach (var path in expanded)
                {
                    if (!InScope(path, scopeRoots))
                    {
                        continue;
                    }

                    if (claimed.Add(path))
                    {
                        targets.Add(new CleanupTarget { Path = path });
                    }
                }
            }

            plan.Add((rule, targets));
        }

        progress?.Report(new CleanupProgress
        {
            Stage = "resolved",
            TotalRules = plan.Count,
            CompletedRules = 0
        });

        // ---- 阶段 2：并行统计体积 ----
        var results = new CleanupRuleResult[plan.Count];
        var completed = 0;
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Clamp(concurrency, 1, 8),
            CancellationToken = cancellationToken
        };

        await Parallel.ForEachAsync(Enumerable.Range(0, plan.Count), options, (index, token) =>
        {
            var (rule, targets) = plan[index];
            var result = new CleanupRuleResult { Rule = rule };

            foreach (var target in targets)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    Measure(rule, target, token);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"{target.Path}: {ex.Message}");
                    _logger.Warn("Cleanup", $"统计失败 {target.Path}: {ex.Message}");
                }

                result.Targets.Add(target);
            }

            // 多条路径的代表文件合并成一条规则的 top N
            foreach (var target in targets)
            {
                foreach (var sample in target.SampleFiles)
                {
                    AddSample(result.SampleFiles, sample);
                }
            }

            results[index] = result;

            var done = Interlocked.Increment(ref completed);
            progress?.Report(new CleanupProgress
            {
                Stage = "scan",
                CompletedRules = done,
                TotalRules = plan.Count,
                CurrentPath = rule.Name.Resolve(string.Empty)
            });

            return ValueTask.CompletedTask;
        });

        foreach (var result in results)
        {
            if (result is not null)
            {
                summary.Rules.Add(result);
            }
        }

        summary.Duration = stopwatch.Elapsed;
        _logger.Info("Cleanup",
            $"扫描完成：{summary.Rules.Count} 条规则，共 {SizeFormatter.Format(summary.TotalSize)}，耗时 {summary.Duration.TotalSeconds:0.00}s");
        return summary;
    }

    /// <summary>用户限定了扫描范围时，只保留落在范围内的路径。</summary>
    private static bool InScope(string path, IReadOnlyList<string>? scopeRoots)
    {
        if (scopeRoots is null || scopeRoots.Count == 0)
        {
            return true;
        }

        foreach (var root in scopeRoots)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            var normalized = root.TrimEnd('\\', '/');
            if (path.Equals(normalized, StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(normalized + "\\", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void Measure(CleanupRule rule, CleanupTarget target, CancellationToken token)
    {
        if (rule.Match is CleanupMatchKind.GlobFile or CleanupMatchKind.SingleFile or CleanupMatchKind.SystemFile)
        {
            var file = new FileInfo(target.Path);
            if (file.Exists)
            {
                target.Exists = true;
                target.SizeBytes = file.Length;
                target.FileCount = 1;
                target.SampleFiles.Add(new CleanupSampleFile(file.FullName, file.Length));
            }

            return;
        }

        if (!Directory.Exists(target.Path))
        {
            return;
        }

        target.Exists = true;
        var cutoff = rule.Strategy == CleanupStrategy.DeleteByAge
            ? DateTime.UtcNow.AddDays(-Math.Max(1, rule.AgeDays))
            : (DateTime?)null;

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            ReturnSpecialDirectories = false,
            AttributesToSkip = FileAttributes.ReparsePoint,
            MatchType = MatchType.Simple
        };

        long size = 0;
        long files = 0;
        long directories = 0;
        var counter = 0;

        // 只记体积最大的 N 个：路径字符串按需生成，普通文件不产生额外分配
        var samples = new List<CleanupSampleFile>(SampleLimit);
        var threshold = 0L;

        var enumerable = new FileSystemEnumerable<(long Size, string? SamplePath)>(
            target.Path,
            (ref FileSystemEntry entry) =>
            {
                if (entry.IsDirectory)
                {
                    return (-1, null);
                }

                if (cutoff.HasValue && entry.LastWriteTimeUtc >= cutoff.Value)
                {
                    return (-2, null);
                }

                var length = entry.Length;
                return samples.Count < SampleLimit || length > threshold
                    ? (length, entry.ToFullPath())
                    : (length, null);
            },
            options);

        foreach (var (value, samplePath) in enumerable)
        {
            if ((++counter & 0x7FF) == 0)
            {
                token.ThrowIfCancellationRequested();
            }

            if (value < 0)
            {
                if (value == -1)
                {
                    directories++;
                }

                continue;
            }

            size += value;
            files++;

            if (samplePath is not null)
            {
                AddSample(samples, new CleanupSampleFile(samplePath, value));
                if (samples.Count >= SampleLimit)
                {
                    threshold = samples[^1].SizeBytes;
                }
            }
        }

        target.SizeBytes = size;
        target.FileCount = files;
        target.DirectoryCount = directories;
        target.SampleFiles.AddRange(samples);
    }

    /// <summary>把候选文件按体积插入 top N（N 很小，线性插入足够）。</summary>
    private static void AddSample(List<CleanupSampleFile> samples, CleanupSampleFile sample)
    {
        if (samples.Count >= SampleLimit && sample.SizeBytes <= samples[^1].SizeBytes)
        {
            return;
        }

        var index = samples.FindIndex(s => s.SizeBytes < sample.SizeBytes);
        if (index < 0)
        {
            samples.Add(sample);
        }
        else
        {
            samples.Insert(index, sample);
        }

        if (samples.Count > SampleLimit)
        {
            samples.RemoveAt(samples.Count - 1);
        }
    }
}
