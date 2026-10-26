using System.Text.RegularExpressions;
using Microsoft.Win32;
using WinTools.Core.Abstractions;
using WinTools.Core.Models;
using WinTools.Infrastructure.Common;

namespace WinTools.Infrastructure.Runtimes;

/// <summary>运行环境：.NET 运行时（注册表 sharedfx）与 VC++ 运行库（Uninstall 键）。</summary>
public sealed partial class RuntimeService : IRuntimeService
{
    private static readonly (string Stack, string Label)[] DotNetStacks =
    [
        ("Microsoft.WindowsDesktop.App", ".NET Desktop Runtime"),
        ("Microsoft.NETCore.App", ".NET Runtime"),
        ("Microsoft.AspNetCore.App", "ASP.NET Core Runtime")
    ];

    private readonly IAppLogger _logger;

    public RuntimeService(IAppLogger logger) => _logger = logger;

    public Task<IReadOnlyList<InstalledRuntime>> GetDotNetAsync(CancellationToken cancellationToken) => Task.Run(() =>
    {
        var list = new List<InstalledRuntime>();

        foreach (var (stack, label) in DotNetStacks)
        {
            foreach (var (view, arch) in new[] { (RegistryView.Registry64, "x64"), (RegistryView.Registry32, "x86") })
            {
                var path = $@"SOFTWARE\dotnet\Setup\InstalledVersions\{arch}\sharedfx\{stack}";
                try
                {
                    using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view).OpenSubKey(path);
                    if (key is null)
                    {
                        continue;
                    }

                    foreach (var version in key.GetValueNames())
                    {
                        if (string.IsNullOrWhiteSpace(version) || !VersionPattern().IsMatch(version))
                        {
                            continue;
                        }

                        list.Add(new InstalledRuntime($"{label}", version, arch, null, null));
                    }
                }
                catch
                {
                    // 忽略
                }
            }
        }

        // 新版安装器不再写 sharedfx 注册表项，直接扫共享框架目录（Program Files\dotnet\shared\<stack>\<版本>）
        foreach (var (stack, label) in DotNetStacks)
        {
            foreach (var (root, arch) in new[]
                     {
                         (Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "x64"),
                         (Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "x86")
                     })
            {
                var dir = Path.Combine(root, "dotnet", "shared", stack);
                if (!Directory.Exists(dir))
                {
                    continue;
                }

                try
                {
                    foreach (var version in Directory.EnumerateDirectories(dir).Select(Path.GetFileName))
                    {
                        if (version is not null && VersionPattern().IsMatch(version))
                        {
                            list.Add(new InstalledRuntime(label, version, arch, null, null));
                        }
                    }
                }
                catch
                {
                    // 目录不可读时忽略
                }
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        return (IReadOnlyList<InstalledRuntime>)list
            .DistinctBy(r => $"{r.Name}|{r.Version}|{r.Architecture}")
            .OrderBy(r => r.Name)
            .ThenByDescending(r => r.Version, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }, cancellationToken);

    public Task<IReadOnlyList<InstalledRuntime>> GetVcRuntimeAsync(CancellationToken cancellationToken) => Task.Run(() =>
    {
        var list = new List<InstalledRuntime>();

        foreach (var (hive, view, scope) in new[]
                 {
                     (RegistryHive.LocalMachine, RegistryView.Registry64, "x64"),
                     (RegistryHive.LocalMachine, RegistryView.Registry32, "x86"),
                     (RegistryHive.CurrentUser, RegistryView.Registry64, "user")
                 })
        {
            foreach (var entry in ReadUninstallEntries(hive, view))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(entry.Name) ||
                    !entry.Name.Contains("Visual C++", StringComparison.OrdinalIgnoreCase) ||
                    !entry.Name.Contains("Redistributable", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                list.Add(new InstalledRuntime(
                    entry.Name,
                    entry.Version ?? "—",
                    scope,
                    entry.UninstallString,
                    null));
            }
        }

        return (IReadOnlyList<InstalledRuntime>)list
            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }, cancellationToken);

    public async Task<OperationResult> UninstallAsync(InstalledRuntime runtime, CancellationToken cancellationToken)
    {
        var command = runtime.UninstallString;
        if (string.IsNullOrWhiteSpace(command))
        {
            return OperationResult.Fail(".NET 运行时请通过安装程序或 dotnet 安装器卸载；此条目没有卸载命令");
        }

        _logger.Audit("Runtime.Uninstall", runtime.Name + " " + runtime.Version, command);

        // MSI 卸载：/qb 静默带进度，不弹完整向导
        if (command.Contains("msiexec", StringComparison.OrdinalIgnoreCase) &&
            GuidPattern().Match(command) is { Success: true } match)
        {
            var result = await ProcessRunner.RunAsync(
                "msiexec.exe",
                ["/x", match.Value, "/qb", "/norestart"],
                600_000,
                cancellationToken).ConfigureAwait(false);

            if (result.ExitCode is 0 or 3010)
            {
                return OperationResult.Ok($"{runtime.Name} 已卸载", 1, result.ExitCode == 3010);
            }

            if (result.ExitCode == 1602)
            {
                return OperationResult.Fail("用户取消或另一安装正在进行");
            }

            return OperationResult.Fail($"卸载失败，返回码 {result.ExitCode}");
        }

        ProcessRunner.Launch(command);
        return OperationResult.Ok("已启动卸载程序，请在弹出窗口中完成", 1);
    }

    private static List<(string? Name, string? Version, string? UninstallString)> ReadUninstallEntries(
        RegistryHive hive,
        RegistryView view)
    {
        var result = new List<(string?, string?, string?)>();

        try
        {
            using var root = RegistryKey.OpenBaseKey(hive, view)
                .OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");

            if (root is null)
            {
                return result;
            }

            foreach (var name in root.GetSubKeyNames())
            {
                try
                {
                    using var sub = root.OpenSubKey(name);
                    if (sub is null)
                    {
                        continue;
                    }

                    result.Add((
                        sub.GetValue("DisplayName")?.ToString(),
                        sub.GetValue("DisplayVersion")?.ToString(),
                        sub.GetValue("UninstallString")?.ToString()));
                }
                catch
                {
                    // 忽略单个损坏项
                }
            }
        }
        catch
        {
            // 忽略
        }

        return result;
    }

    [GeneratedRegex(@"^\d+\.\d+\.\d+")]
    private static partial Regex VersionPattern();

    [GeneratedRegex(@"\{[0-9A-Fa-f\-]{36}\}")]
    private static partial Regex GuidPattern();
}
