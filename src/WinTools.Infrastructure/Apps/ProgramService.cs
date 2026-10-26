using Microsoft.Win32;
using WinTools.Core.Abstractions;
using WinTools.Core.Models;
using WinTools.Infrastructure.Common;

namespace WinTools.Infrastructure.Apps;

/// <summary>已安装程序：读取 Uninstall 注册表键（HKLM 64/32 位 + HKCU）。</summary>
public sealed class ProgramService : IProgramService
{
    private readonly IAppLogger _logger;

    public ProgramService(IAppLogger logger) => _logger = logger;

    public Task<IReadOnlyList<InstalledProgram>> GetAsync(CancellationToken cancellationToken) => Task.Run(() =>
    {
        var list = new List<InstalledProgram>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (hive, view, scope) in new[]
                 {
                     (RegistryHive.LocalMachine, RegistryView.Registry64, "machine"),
                     (RegistryHive.LocalMachine, RegistryView.Registry32, "machine-x86"),
                     (RegistryHive.CurrentUser, RegistryView.Registry64, "user")
                 })
        {
            Read(hive, view, scope, list, seen, cancellationToken);
        }

        return (IReadOnlyList<InstalledProgram>)list
            .OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }, cancellationToken);

    public async Task<OperationResult> UninstallAsync(InstalledProgram program, CancellationToken cancellationToken)
    {
        var silent = program.QuietUninstallString;
        var command = string.IsNullOrWhiteSpace(silent) ? program.UninstallString : silent;

        if (string.IsNullOrWhiteSpace(command))
        {
            return OperationResult.Fail("该程序未提供卸载命令");
        }

        _logger.Audit("Program.Uninstall", program.Name, command);

        // 有静默参数时后台执行并等待，便于立即反馈结果
        if (!string.IsNullOrWhiteSpace(silent))
        {
            var (file, arguments) = SplitCommandLine(silent);
            if (file.Length > 0)
            {
                var result = await ProcessRunner.RunAsync(file, arguments, 900_000, cancellationToken)
                    .ConfigureAwait(false);

                return result.ExitCode is 0 or 3010
                    ? OperationResult.Ok($"{program.Name} 已卸载", 1, result.ExitCode == 3010)
                    : OperationResult.Fail($"卸载程序返回 {result.ExitCode}：{result.Combined.Trim()}");
            }
        }

        var (exe, args) = SplitCommandLine(command);
        if (!ProcessRunner.Launch(exe, string.Join(' ', args)))
        {
            return OperationResult.Fail("无法启动卸载程序");
        }

        return OperationResult.Ok("已启动卸载程序，请在弹出窗口中完成", 1);
    }

    private void Read(
        RegistryHive hive,
        RegistryView view,
        string scope,
        List<InstalledProgram> list,
        HashSet<string> seen,
        CancellationToken cancellationToken)
    {
        try
        {
            using var root = RegistryKey.OpenBaseKey(hive, view)
                .OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");

            if (root is null)
            {
                return;
            }

            foreach (var keyName in root.GetSubKeyNames())
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    using var key = root.OpenSubKey(keyName);
                    if (key is null)
                    {
                        continue;
                    }

                    var name = key.GetValue("DisplayName")?.ToString()?.Trim();
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    // 跳过系统组件与补丁、仅更新的子项
                    if (Convert.ToInt32(key.GetValue("SystemComponent") ?? 0) == 1)
                    {
                        continue;
                    }

                    if (key.GetValue("ParentKeyName") is not null ||
                        key.GetValue("ReleaseType") is string release &&
                        (release.Contains("Update", StringComparison.OrdinalIgnoreCase) ||
                         release.Contains("Hotfix", StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    var version = key.GetValue("DisplayVersion")?.ToString();
                    var identity = name + "|" + version;
                    if (!seen.Add(identity))
                    {
                        continue;
                    }

                    long? size = key.GetValue("EstimatedSize") is int kb ? kb * 1024L : null;

                    // 图标：DisplayIcon 通常是「路径,索引」，直接交给 IconService 解析
                    var icon = key.GetValue("DisplayIcon")?.ToString();

                    // 最后使用：安装目录的最后写入时间（没有 UserAssist 依赖，读一次即可）
                    string? lastUsed = null;
                    var location = key.GetValue("InstallLocation")?.ToString();
                    if (!string.IsNullOrWhiteSpace(location))
                    {
                        try
                        {
                            var trimmed = location.Trim('"');
                            if (Directory.Exists(trimmed))
                            {
                                lastUsed = Directory.GetLastWriteTime(trimmed).ToString("yyyy-MM-dd");
                            }
                        }
                        catch
                        {
                            // 目录不可读就留空
                        }
                    }

                    list.Add(new InstalledProgram(
                        keyName,
                        name,
                        key.GetValue("Publisher")?.ToString(),
                        version,
                        key.GetValue("InstallDate")?.ToString(),
                        size,
                        key.GetValue("UninstallString")?.ToString(),
                        key.GetValue("QuietUninstallString")?.ToString(),
                        scope,
                        icon,
                        lastUsed));
                }
                catch
                {
                    // 忽略单个损坏项
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Programs", "读取已安装程序失败", ex);
        }
    }

    /// <summary>拆分 "C:\path\app.exe" /X {GUID} 形式的命令行。</summary>
    private static (string File, List<string> Arguments) SplitCommandLine(string commandLine)
    {
        var text = commandLine.Trim();
        if (text.Length == 0)
        {
            return (string.Empty, []);
        }

        string file;
        string rest;

        if (text[0] == '"')
        {
            var end = text.IndexOf('"', 1);
            if (end < 0)
            {
                return (text.Trim('"'), []);
            }

            file = text[1..end];
            rest = text[(end + 1)..];
        }
        else
        {
            var space = text.IndexOf(' ');
            file = space < 0 ? text : text[..space];
            rest = space < 0 ? string.Empty : text[space..];
        }

        var arguments = new List<string>();
        var buffer = new global::System.Text.StringBuilder();
        var inQuotes = false;

        foreach (var ch in rest)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(ch) && !inQuotes)
            {
                if (buffer.Length > 0)
                {
                    arguments.Add(buffer.ToString());
                    buffer.Clear();
                }

                continue;
            }

            buffer.Append(ch);
        }

        if (buffer.Length > 0)
        {
            arguments.Add(buffer.ToString());
        }

        return (file, arguments);
    }
}
