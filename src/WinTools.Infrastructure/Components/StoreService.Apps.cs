using System.Text.Json;
using System.Text.RegularExpressions;
using WinTools.Core.Abstractions;
using WinTools.Core.Models;
using WinTools.Infrastructure.Common;

namespace WinTools.Infrastructure.Components;

/// <summary>组件中心：内置应用（终端/计算器/记事本…）与 WSL 发行版。</summary>
public sealed partial class StoreService
{
    /// <summary>内置应用清单：包名用于检测，winget Id 用于安装。</summary>
    private static readonly (string Id, string Package, string Winget)[] BuiltinApps =
    [
        ("Terminal", "Microsoft.WindowsTerminal", "Microsoft.WindowsTerminal"),
        ("Calculator", "Microsoft.WindowsCalculator", "Microsoft.WindowsCalculator"),
        ("Notepad", "Microsoft.WindowsNotepad", "Microsoft.WindowsNotepad"),
        ("Paint", "Microsoft.Paint", "Microsoft.Paint"),
        ("Photos", "Microsoft.Windows.Photos", "Microsoft.Windows.Photos"),
        ("Camera", "Microsoft.WindowsCamera", "Microsoft.WindowsCamera"),
        ("Clock", "Microsoft.WindowsAlarms", "Microsoft.WindowsAlarms"),
        ("MediaPlayer", "Microsoft.ZuneMusic", "Microsoft.ZuneMusic"),
        ("StickyNotes", "Microsoft.MicrosoftStickyNotes", "Microsoft.MicrosoftStickyNotes"),
        ("Store", "Microsoft.WindowsStore", "Microsoft.WindowsStore")
    ];

    public Task<IReadOnlyList<StoreApp>> GetAppsAsync(CancellationToken cancellationToken) => Task.Run(async () =>
    {
        var installed = await GetInstalledPackagesAsync(cancellationToken).ConfigureAwait(false);
        var list = new List<StoreApp>();

        foreach (var (id, package, winget) in BuiltinApps)
        {
            installed.TryGetValue(package, out var version);
            list.Add(new StoreApp(id, string.Empty, package, winget, version is not null, version, "App"));
        }

        return (IReadOnlyList<StoreApp>)list;
    }, cancellationToken);

    public async Task<OperationResult> InstallAppAsync(StoreApp app, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(app.WingetId))
        {
            return OperationResult.Fail("该组件不支持自动安装");
        }

        var result = await ProcessRunner.RunAsync(
            "winget",
            [
                "install", "--id", app.WingetId, "--exact", "--silent",
                "--accept-package-agreements", "--accept-source-agreements",
                "--disable-interactivity"
            ],
            timeoutMs: 600_000,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (result.TimedOut)
        {
            return OperationResult.Fail("安装超时");
        }

        _logger.Audit("Component.Install", app.PackageName, $"exit={result.ExitCode}");

        return result.Success
            ? OperationResult.Ok("已安装")
            : OperationResult.Fail(Summarize(result.Combined, "安装失败，请确认系统已安装应用安装程序（winget）"));
    }

    public async Task<OperationResult> UninstallAppAsync(StoreApp app, CancellationToken cancellationToken)
    {
        var result = await ProcessRunner.RunPowerShellAsync(
            $"Get-AppxPackage -Name '{app.PackageName}' | Remove-AppxPackage -ErrorAction Stop",
            timeoutMs: 300_000,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        _logger.Audit("Component.Uninstall", app.PackageName, $"exit={result.ExitCode}");

        return result.Success
            ? OperationResult.Ok("已卸载")
            : OperationResult.Fail(Summarize(result.Combined, "卸载失败：可能有其他用户仍在使用该应用"));
    }

    public async Task<OperationResult> RepairAppAsync(StoreApp app, CancellationToken cancellationToken)
    {
        // $$ 前缀：单花括号是脚本内容，{{ }} 才是插值
        var script = $$"""
            $pkg = Get-AppxPackage -Name '{{app.PackageName}}' -ErrorAction Stop
            if (-not $pkg) { throw '未找到已安装的包' }
            Add-AppxPackage -DisableDevelopmentMode -Register "$($pkg.InstallLocation)\AppxManifest.xml" -ErrorAction Stop
            """;

        var result = await ProcessRunner.RunPowerShellAsync(
            script, timeoutMs: 300_000, cancellationToken: cancellationToken).ConfigureAwait(false);

        _logger.Audit("Component.Repair", app.PackageName, $"exit={result.ExitCode}");

        return result.Success
            ? OperationResult.Ok("已修复")
            : OperationResult.Fail(Summarize(result.Combined, "修复失败"));
    }

    public Task<IReadOnlyList<WslDistro>> GetWslAsync(CancellationToken cancellationToken) => Task.Run(async () =>
    {
        var installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var defaultName = string.Empty;

        var list = await ProcessRunner.RunAsync(
            "wsl.exe", ["--list", "--quiet"], timeoutMs: 30_000, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (list.Success)
        {
            foreach (var line in CleanWslOutput(list.StdOut))
            {
                if (line.Length > 0)
                {
                    installed.Add(line);
                }
            }
        }

        var online = await ProcessRunner.RunAsync(
            "wsl.exe", ["--list", "--online"], timeoutMs: 60_000, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var names = new List<string>();

        if (online.Success)
        {
            foreach (var line in CleanWslOutput(online.StdOut).Skip(1))
            {
                var name = line.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(name) && !name.StartsWith('*'))
                {
                    names.Add(name);
                }
            }
        }

        // 已安装但不在在线清单里的（自定义发行版）也要显示
        foreach (var name in installed.Where(n => !names.Contains(n, StringComparer.OrdinalIgnoreCase)))
        {
            names.Add(name);
        }

        var result = names
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(n => new WslDistro(n, n, installed.Contains(n),
                string.Equals(n, defaultName, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(d => d.IsInstalled)
            .ThenBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return (IReadOnlyList<WslDistro>)result;
    }, cancellationToken);

    public async Task<OperationResult> InstallWslAsync(WslDistro distro, CancellationToken cancellationToken)
    {
        var result = await ProcessRunner.RunAsync(
            "wsl.exe",
            ["--install", "-d", distro.Name, "--no-launch"],
            timeoutMs: 1_800_000,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        _logger.Audit("Component.InstallWsl", distro.Name, $"exit={result.ExitCode}");

        if (result.TimedOut)
        {
            return OperationResult.Fail("安装超时（发行版下载可能较慢）");
        }

        return result.Success
            ? OperationResult.Ok("已安装", restartRequired: true)
            : OperationResult.Fail(Summarize(result.Combined, "安装失败：请确认已启用「虚拟机平台」并重启"));
    }

    // ------------------------------------------------------------------

    private static async Task<Dictionary<string, string>> GetInstalledPackagesAsync(CancellationToken cancellationToken)
    {
        var names = string.Join(",", BuiltinApps.Select(a => $"'{a.Package}'"));

        // 这里用普通字符串拼接：脚本里大量 @{...} 与插值花括号混在一起，原始字符串反而更难读
        var script =
            "$names = @(" + names + ")\r\n" +
            "Get-AppxPackage | Where-Object { $names -contains $_.Name } | \r\n" +
            "  Select-Object @{n='Name';e={$_.Name}}, @{n='Version';e={$_.Version.ToString()}} | \r\n" +
            "  ConvertTo-Json -Compress";

        var result = await ProcessRunner.RunPowerShellAsync(
            script, timeoutMs: 60_000, cancellationToken: cancellationToken).ConfigureAwait(false);

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!result.Success || string.IsNullOrWhiteSpace(result.StdOut))
        {
            return map;
        }

        try
        {
            var json = result.StdOut.Trim();
            if (!json.StartsWith('['))
            {
                json = "[" + json + "]";
            }

            foreach (var item in JsonSerializer.Deserialize<List<PackageJson>>(json) ?? [])
            {
                if (!string.IsNullOrWhiteSpace(item.Name))
                {
                    map[item.Name] = item.Version ?? string.Empty;
                }
            }
        }
        catch
        {
            // 解析失败按未安装处理
        }

        return map;
    }

    /// <summary>wsl.exe 的输出是 UTF-16，按行切分后去掉 BOM 与零宽字符。</summary>
    private static IEnumerable<string> CleanWslOutput(string text)
        => text
            .Replace("\0", string.Empty)
            .Replace("\uFEFF", string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.Contains("NAME", StringComparison.OrdinalIgnoreCase));

    /// <summary>把命令行输出压成一句可读的错误说明。</summary>
    private static string Summarize(string text, string fallback)
    {
        var line = text
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .LastOrDefault(l => l.Length > 0);

        if (string.IsNullOrWhiteSpace(line))
        {
            return fallback;
        }

        line = Regex.Replace(line, @"\s{2,}", " ");
        return line.Length > 200 ? fallback : line;
    }

    private sealed record PackageJson(string? Name, string? Version);
}
