using Microsoft.Win32;
using WinTools.Core.Abstractions;
using WinTools.Core.Models;

namespace WinTools.Infrastructure.Run;

/// <summary>
/// 启动项：Run / RunOnce（HKLM、HKCU、WOW6432Node）+ 启动文件夹。
/// 启用/禁用通过 StartupApproved 二进制值实现（与任务管理器一致）。
/// </summary>
public sealed class StartupItemService : IStartupItemService
{
    private const string RunPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string RunOncePath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce";
    private const string ApprovedRunPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string ApprovedRunOncePath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32";
    private const string ApprovedFolderPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";

    private readonly IAppLogger _logger;

    public StartupItemService(IAppLogger logger) => _logger = logger;

    public Task<IReadOnlyList<StartupEntry>> GetAsync(CancellationToken cancellationToken) => Task.Run(() =>
    {
        var list = new List<StartupEntry>();

        ReadRunKey(RegistryHive.LocalMachine, RegistryView.Registry64, RunPath, "HKLM", "Run", list);
        ReadRunKey(RegistryHive.LocalMachine, RegistryView.Registry64, RunOncePath, "HKLM", "RunOnce", list);
        ReadRunKey(RegistryHive.LocalMachine, RegistryView.Registry32, RunPath, "HKLM", "Run", list);
        ReadRunKey(RegistryHive.CurrentUser, RegistryView.Registry64, RunPath, "HKCU", "Run", list);
        ReadRunKey(RegistryHive.CurrentUser, RegistryView.Registry64, RunOncePath, "HKCU", "RunOnce", list);

        ReadStartupFolder(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup),
            "Common", list);

        ReadStartupFolder(
            Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            "User", list);

        cancellationToken.ThrowIfCancellationRequested();

        return (IReadOnlyList<StartupEntry>)list
            .OrderBy(e => e.Scope)
            .ThenBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }, cancellationToken);

    public Task<OperationResult> SetEnabledAsync(
        StartupEntry entry,
        bool enabled,
        CancellationToken cancellationToken) => Task.Run(() =>
    {
        try
        {
            var valueName = entry.Id;
            var (hive, view, keyPath) = entry.LocationKey switch
            {
                "HKLM" => (RegistryHive.LocalMachine, RegistryView.Registry64, ApprovedRunPath),
                "HKCU" => (RegistryHive.CurrentUser, RegistryView.Registry64, ApprovedRunPath),
                "Folder" => (RegistryHive.CurrentUser, RegistryView.Registry64, ApprovedFolderPath),
                _ => (RegistryHive.CurrentUser, RegistryView.Registry64, ApprovedRunPath)
            };

            if (entry.LocationKey == "HKLM" && entry.Scope == "32")
            {
                view = RegistryView.Registry32;
            }

            if (entry.LocationKey == "HKCU" && entry.Command.EndsWith("RunOnce", StringComparison.OrdinalIgnoreCase))
            {
                keyPath = ApprovedRunOncePath;
            }

            using var key = RegistryKey.OpenBaseKey(hive, view).CreateSubKey(keyPath, writable: true);
            if (key is null)
            {
                return OperationResult.Fail("无法写入 StartupApproved");
            }

            var data = new byte[12];
            data[0] = (byte)(enabled ? 0x02 : 0x03);
            key.SetValue(valueName, data, RegistryValueKind.Binary);

            _logger.Audit(enabled ? "Startup.Enable" : "Startup.Disable", valueName, entry.Command);
            return OperationResult.Ok(enabled ? "已启用" : "已禁用", 1);
        }
        catch (Exception ex)
        {
            _logger.Error("Startup", "修改启动项状态失败", ex);
            return OperationResult.Fail(ex.Message);
        }
    }, cancellationToken);

    public Task<OperationResult> DeleteAsync(StartupEntry entry, CancellationToken cancellationToken) => Task.Run(() =>
    {
        try
        {
            if (entry.LocationKey == "Folder")
            {
                var file = entry.Command;
                if (File.Exists(file))
                {
                    File.Delete(file);
                    _logger.Audit("Startup.Delete", file);
                    return OperationResult.Ok("已删除启动文件夹快捷方式", 1);
                }

                return OperationResult.Fail("文件不存在");
            }

            var hive = entry.LocationKey == "HKLM" ? RegistryHive.LocalMachine : RegistryHive.CurrentUser;
            var view = entry.LocationKey == "HKLM" && entry.Scope == "32" ? RegistryView.Registry32 : RegistryView.Registry64;
            var isRunOnce = entry.Command.Length == 0;

            // Id 形如 "Run\名称" / "RunOnce\名称"
            var parts = entry.Id.Split('\\', 2);
            var keyName = parts[0];
            var valueName = parts.Length > 1 ? parts[1] : entry.Id;

            var path = keyName.Equals("RunOnce", StringComparison.OrdinalIgnoreCase) ? RunOncePath : RunPath;

            using var key = RegistryKey.OpenBaseKey(hive, view).OpenSubKey(path, writable: true);
            if (key is null)
            {
                return OperationResult.Fail("找不到注册表项");
            }

            key.DeleteValue(valueName, throwOnMissingValue: false);
            _logger.Audit("Startup.Delete", $"{entry.LocationKey}\\{path}\\{valueName}");
            _ = isRunOnce;
            return OperationResult.Ok("已删除启动项", 1);
        }
        catch (Exception ex)
        {
            _logger.Error("Startup", "删除启动项失败", ex);
            return OperationResult.Fail(ex.Message);
        }
    }, cancellationToken);

    private void ReadRunKey(
        RegistryHive hive,
        RegistryView view,
        string path,
        string hiveLabel,
        string kind,
        List<StartupEntry> list)
    {
        try
        {
            using var key = RegistryKey.OpenBaseKey(hive, view).OpenSubKey(path);
            if (key is null)
            {
                return;
            }

            var approved = ReadApproved(hive, view,
                kind == "RunOnce" ? ApprovedRunOncePath : ApprovedRunPath);

            var scope = view == RegistryView.Registry32 ? "32" : "64";

            foreach (var name in key.GetValueNames())
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var command = key.GetValue(name)?.ToString() ?? string.Empty;
                var id = kind + "\\" + name;
                var uniqueKey = name;

                list.Add(new StartupEntry(
                    id,
                    name,
                    command,
                    hiveLabel,
                    scope,
                    approved.TryGetValue(uniqueKey, out var enabled) ? enabled : true,
                    true));
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Startup", $"读取启动项失败 {hiveLabel}\\{path}", ex);
        }
    }

    private void ReadStartupFolder(string folder, string scope, List<StartupEntry> list)
    {
        try
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                return;
            }

            var approved = ReadApproved(RegistryHive.CurrentUser, RegistryView.Registry64, ApprovedFolderPath);

            foreach (var file in Directory.EnumerateFiles(folder))
            {
                var name = Path.GetFileName(file);
                if (name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                list.Add(new StartupEntry(
                    name,
                    Path.GetFileNameWithoutExtension(name),
                    file,
                    "Folder",
                    scope,
                    approved.TryGetValue(name, out var enabled) ? enabled : true,
                    true));
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Startup", "读取启动文件夹失败", ex);
        }
    }

    private static Dictionary<string, bool> ReadApproved(RegistryHive hive, RegistryView view, string path)
    {
        var map = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var key = RegistryKey.OpenBaseKey(hive, view).OpenSubKey(path);
            if (key is null)
            {
                return map;
            }

            foreach (var name in key.GetValueNames())
            {
                if (key.GetValue(name) is byte[] data && data.Length > 0)
                {
                    // 0x02 / 0x06 = 启用，0x03 = 禁用
                    map[name] = data[0] is 0x02 or 0x06;
                }
            }
        }
        catch
        {
            // 忽略
        }

        return map;
    }
}
