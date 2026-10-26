using System.Text.Json;
using WinTools.Core.Abstractions;
using WinTools.Core.Models;
using WinTools.Infrastructure.Common;

namespace WinTools.Infrastructure.Apps;

/// <summary>
/// Appx / MSIX 包：通过 PowerShell 的 Appx 模块读取与移除
/// （避免为少数接口引入 WinRT 投影 24 MB 依赖）。
/// </summary>
public sealed class AppxService : IAppxService
{
    private readonly IAppLogger _logger;

    public AppxService(IAppLogger logger) => _logger = logger;

    public async Task<IReadOnlyList<AppxPackageEntry>> GetAsync(CancellationToken cancellationToken)
    {
        const string script =
            "Get-AppxPackage | Select-Object Name,Version,Publisher,PackageFullName,IsFramework,InstallLocation " +
            "| ConvertTo-Json -Compress -Depth 3";

        var result = await ProcessRunner.RunPowerShellAsync(script, 180_000, cancellationToken)
            .ConfigureAwait(false);

        if (!result.Success || string.IsNullOrWhiteSpace(result.StdOut))
        {
            _logger.Error("Appx", "读取 Appx 失败: " + result.Combined);
            return [];
        }

        var list = new List<AppxPackageEntry>();
        var watch = global::System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var json = result.StdOut.Trim();
            if (json.StartsWith('{'))
            {
                json = "[" + json + "]";
            }

            using var document = JsonDocument.Parse(json);
            foreach (var element in document.RootElement.EnumerateArray())
            {
                var location = Get(element, "InstallLocation");
                var (size, installed, lastUsed, icon) = InspectPackage(location, watch);

                list.Add(new AppxPackageEntry(
                    Get(element, "Name") ?? "—",
                    Get(element, "Version"),
                    ShortPublisher(Get(element, "Publisher")),
                    Get(element, "PackageFullName"),
                    element.TryGetProperty("IsFramework", out var framework) && framework.ValueKind == JsonValueKind.True,
                    location,
                    size,
                    installed,
                    lastUsed,
                    icon));
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Appx", "解析 Appx 列表失败", ex);
        }

        return list
            .Where(p => !p.IsFramework)
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Publisher 是 "CN=..., O=Microsoft Corporation, L=..." 这种 DN，只留公司名。</summary>
    private static string? ShortPublisher(string? publisher)
    {
        if (string.IsNullOrWhiteSpace(publisher))
        {
            return publisher;
        }

        foreach (var part in publisher.Split(','))
        {
            var trimmed = part.Trim();
            if (trimmed.StartsWith("O=", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed[2..].Trim('"');
            }
        }

        return publisher;
    }

    /// <summary>
    /// 补全安装目录信息：大小、安装日期、最后活动时间、方图图标。
    /// 目录遍历昂贵，整体 4 秒预算内能算多少算多少，超出的留空由界面显示「—」。
    /// </summary>
    private static (long? Size, string? Installed, string? LastUsed, string? Icon) InspectPackage(
        string? location,
        global::System.Diagnostics.Stopwatch watch)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            return (null, null, null, null);
        }

        try
        {
            var dir = new DirectoryInfo(location);
            if (!dir.Exists)
            {
                return (null, null, null, null);
            }

            var installed = dir.CreationTime.ToString("yyyy-MM-dd");
            var lastUsed = dir.LastWriteTime.ToString("yyyy-MM-dd");

            string? icon = null;
            try
            {
                icon = Directory
                    .EnumerateFiles(location, "*.png", SearchOption.AllDirectories)
                    .Where(f => Path.GetFileName(f).Contains("Square44x44Logo", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(f => f.Contains("scale-100", StringComparison.OrdinalIgnoreCase))
                    .FirstOrDefault();
            }
            catch
            {
                // 目录不可读就跳过图标
            }

            long? size = null;
            if (watch.ElapsedMilliseconds < 4000)
            {
                try
                {
                    long total = 0;
                    foreach (var file in Directory.EnumerateFiles(location, "*", SearchOption.AllDirectories))
                    {
                        total += new FileInfo(file).Length;
                        if (watch.ElapsedMilliseconds > 4000)
                        {
                            break;
                        }
                    }

                    size = total > 0 ? total : null;
                }
                catch
                {
                    // 部分系统目录无权限，忽略
                }
            }

            return (size, installed, lastUsed, icon);
        }
        catch
        {
            return (null, null, null, null);
        }
    }

    public async Task<OperationResult> RemoveAsync(AppxPackageEntry package, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(package.PackageFullName))
        {
            return OperationResult.Fail("缺少包全名，无法移除");
        }

        var escaped = package.PackageFullName.Replace("'", "''");
        var result = await ProcessRunner.RunPowerShellAsync(
            $"Remove-AppxPackage -Package '{escaped}'",
            300_000,
            cancellationToken).ConfigureAwait(false);

        _logger.Audit("Appx.Remove", package.PackageFullName, result.Combined.Trim());

        return result.Success
            ? OperationResult.Ok($"{package.Name} 已移除", 1)
            : OperationResult.Fail(string.IsNullOrWhiteSpace(result.Combined) ? "移除失败" : result.Combined.Trim());
    }

    private static string? Get(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.Null => null,
            _ => value.ToString()
        };
    }
}
