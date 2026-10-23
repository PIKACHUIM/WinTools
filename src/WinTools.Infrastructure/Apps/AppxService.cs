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
                list.Add(new AppxPackageEntry(
                    Get(element, "Name") ?? "—",
                    Get(element, "Version"),
                    Get(element, "Publisher"),
                    Get(element, "PackageFullName"),
                    element.TryGetProperty("IsFramework", out var framework) && framework.ValueKind == JsonValueKind.True,
                    Get(element, "InstallLocation")));
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
