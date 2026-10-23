using WinTools.Core.Abstractions;
using WinTools.Core.Models;
using WinTools.Infrastructure.Common;

namespace WinTools.Infrastructure.Drivers;

/// <summary>驱动管理：基于 pnputil 的枚举、导出、安装与删除。</summary>
public sealed partial class DriverService : IDriverService
{
    [global::System.Text.RegularExpressions.GeneratedRegex(@"^oem\d+\.inf$", global::System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial global::System.Text.RegularExpressions.Regex PublishedPattern();

    [global::System.Text.RegularExpressions.GeneratedRegex(@"^\d+\.\d+(\.\d+)*$")]
    private static partial global::System.Text.RegularExpressions.Regex VersionPattern();

    [global::System.Text.RegularExpressions.GeneratedRegex(@"^\d{1,4}[/-]\d{1,2}[/-]\d{1,4}$")]
    private static partial global::System.Text.RegularExpressions.Regex DatePattern();

    [global::System.Text.RegularExpressions.GeneratedRegex(
        @"^(?<date>\d{1,4}[/-]\d{1,2}[/-]\d{1,4})\s+(?<version>[\d\.]+)$")]
    private static partial global::System.Text.RegularExpressions.Regex VersionWithDatePattern();

    [global::System.Text.RegularExpressions.GeneratedRegex(@"^\{[0-9A-Fa-f\-]{36}\}$")]
    private static partial global::System.Text.RegularExpressions.Regex GuidPattern();

    private readonly IAppLogger _logger;

    public DriverService(IAppLogger logger) => _logger = logger;

    public async Task<IReadOnlyList<DriverPackage>> GetAsync(CancellationToken cancellationToken)
    {
        var result = await ProcessRunner.RunAsync("pnputil.exe", ["/enum-drivers"], 120_000, cancellationToken)
            .ConfigureAwait(false);

        if (!result.Success)
        {
            _logger.Error("Drivers", "枚举驱动失败: " + result.Combined);
            return [];
        }

        var list = new List<DriverPackage>();
        var block = new List<string>();

        void Flush()
        {
            if (block.Count < 3)
            {
                block.Clear();
                return;
            }

            // pnputil 输出为「标签: 值」形式且标签随系统语言变化，
            // 因此按值的特征识别字段，而不是依赖固定行号。
            var values = new List<string>();
            foreach (var line in block)
            {
                var index = line.IndexOf(':');
                if (index < 0)
                {
                    index = line.IndexOf('：');
                }

                if (index < 0 || index == line.Length - 1)
                {
                    continue;
                }

                var value = line[(index + 1)..].Trim();
                if (value.Length > 0)
                {
                    values.Add(value);
                }
            }

            var published = values.FirstOrDefault(v => PublishedPattern().IsMatch(v));
            if (published is null)
            {
                block.Clear();
                return;
            }

            var original = values.FirstOrDefault(v =>
                v.EndsWith(".inf", StringComparison.OrdinalIgnoreCase) && !PublishedPattern().IsMatch(v));

            // "Driver Version: 07/18/1968 10.1.45.10" —— 日期与版本在同一行
            string? version = null;
            string? date = null;
            string? versionLine = null;

            foreach (var value in values)
            {
                var match = VersionWithDatePattern().Match(value);
                if (match.Success)
                {
                    versionLine = value;
                    date = match.Groups["date"].Value;
                    version = match.Groups["version"].Value;
                    break;
                }
            }

            version ??= values.FirstOrDefault(v => VersionPattern().IsMatch(v));
            date ??= values.FirstOrDefault(v => DatePattern().IsMatch(v));

            // 其余字段顺序为：提供程序名称、类名、签名者姓名
            var rest = values
                .Where(v => v != published && v != original && v != version && v != date && v != versionLine)
                .Where(v => !GuidPattern().IsMatch(v))
                .ToList();

            list.Add(new DriverPackage(
                published,
                original ?? published,
                rest.ElementAtOrDefault(0),
                rest.ElementAtOrDefault(1),
                version,
                date));

            block.Clear();
        }

        foreach (var rawLine in result.StdOut.Split('\n'))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                Flush();
                continue;
            }

            if (line.Contains(':') || line.Contains('：'))
            {
                block.Add(line);
            }
        }

        Flush();

        return list;
    }

    public async Task<OperationResult> ExportAsync(
        string targetDirectory,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(targetDirectory))
        {
            return OperationResult.Fail("请先选择导出目录");
        }

        try
        {
            Directory.CreateDirectory(targetDirectory);
        }
        catch (Exception ex)
        {
            return OperationResult.Fail("无法创建目录: " + ex.Message);
        }

        progress?.Report("正在导出全部第三方驱动…");
        var result = await ProcessRunner.RunAsync(
            "pnputil.exe",
            ["/export-driver", "*", targetDirectory],
            900_000,
            cancellationToken).ConfigureAwait(false);

        _logger.Audit("Driver.Export", targetDirectory, result.Combined.Trim());

        if (!result.Success)
        {
            return OperationResult.Fail(result.Combined.Trim());
        }

        var count = Directory.Exists(targetDirectory)
            ? Directory.GetDirectories(targetDirectory).Length
            : 0;

        return OperationResult.Ok($"已导出 {count} 个驱动包到 {targetDirectory}", count);
    }

    public async Task<OperationResult> InstallAsync(string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path) || (!File.Exists(path) && !Directory.Exists(path)))
        {
            return OperationResult.Fail("路径不存在");
        }

        var arguments = new List<string> { "/add-driver", path, "/install" };
        if (Directory.Exists(path))
        {
            arguments.Add("/subdirs");
        }

        var result = await ProcessRunner.RunAsync("pnputil.exe", arguments, 900_000, cancellationToken)
            .ConfigureAwait(false);

        _logger.Audit("Driver.Install", path, result.Combined.Trim());

        if (result.Success)
        {
            return OperationResult.Ok("驱动安装完成", 1, restartRequired: true);
        }

        return OperationResult.Fail(string.IsNullOrWhiteSpace(result.Combined) ? "安装失败" : result.Combined.Trim());
    }

    public async Task<OperationResult> RemoveAsync(DriverPackage driver, CancellationToken cancellationToken)
    {
        var result = await ProcessRunner.RunAsync(
            "pnputil.exe",
            ["/delete-driver", driver.PublishedName, "/uninstall"],
            600_000,
            cancellationToken).ConfigureAwait(false);

        _logger.Audit("Driver.Remove", driver.PublishedName, result.Combined.Trim());

        return result.Success
            ? OperationResult.Ok($"{driver.PublishedName} 已删除", 1, restartRequired: true)
            : OperationResult.Fail(string.IsNullOrWhiteSpace(result.Combined) ? "删除失败" : result.Combined.Trim());
    }
}
