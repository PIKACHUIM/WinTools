using WinTools.Core.Abstractions;
using WinTools.Core.Models;
using WinTools.Infrastructure.Common;

namespace WinTools.Infrastructure.Run;

/// <summary>Windows 服务：WMI 读取状态，sc.exe 调整启动类型与启停（与 SCM 协商，优于直接改注册表）。</summary>
public sealed class WindowsServiceManager : IWindowsServiceManager
{
    private readonly IAppLogger _logger;

    public WindowsServiceManager(IAppLogger logger) => _logger = logger;

    public Task<IReadOnlyList<WindowsServiceEntry>> GetAsync(CancellationToken cancellationToken) => Task.Run(() =>
    {
        var list = new List<WindowsServiceEntry>();

        var rows = Wmi.Query(
            "SELECT Name, DisplayName, State, StartMode, Description FROM Win32_Service",
            @"root\cimv2");

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string Get(string key) => row.TryGetValue(key, out var v) ? v?.ToString() ?? string.Empty : string.Empty;

            var name = Get("Name");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var state = Get("State");
            var startMode = Get("StartMode");
            var description = Get("Description");

            list.Add(new WindowsServiceEntry(
                name,
                string.IsNullOrWhiteSpace(Get("DisplayName")) ? name : Get("DisplayName"),
                state,
                NormalizeStartMode(startMode),
                string.IsNullOrWhiteSpace(description) ? null : description,
                state.Equals("Running", StringComparison.OrdinalIgnoreCase)));
        }

        return (IReadOnlyList<WindowsServiceEntry>)list
            .OrderBy(s => s.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }, cancellationToken);

    public Task<OperationResult> SetStartTypeAsync(
        WindowsServiceEntry entry,
        string startType,
        CancellationToken cancellationToken) => Task.Run(async () =>
    {
        var scValue = startType switch
        {
            "Auto" => "auto",
            "Manual" => "demand",
            "Disabled" => "disabled",
            _ => "demand"
        };

        var result = await ProcessRunner.RunAsync(
            "sc.exe",
            ["config", entry.Name, "start=", scValue],
            30_000,
            cancellationToken).ConfigureAwait(false);

        _logger.Audit("Service.StartType", entry.Name, $"{entry.StartType} -> {startType}");

        return result.Success
            ? OperationResult.Ok($"启动类型已设为 {startType}", 1)
            : OperationResult.Fail(result.Combined.Trim());
    }, cancellationToken);

    public Task<OperationResult> StartAsync(WindowsServiceEntry entry, CancellationToken cancellationToken)
        => ControlAsync(entry, "start", cancellationToken);

    public Task<OperationResult> StopAsync(WindowsServiceEntry entry, CancellationToken cancellationToken)
        => ControlAsync(entry, "stop", cancellationToken);

    private async Task<OperationResult> ControlAsync(
        WindowsServiceEntry entry,
        string action,
        CancellationToken cancellationToken)
    {
        var result = await ProcessRunner.RunAsync(
            "sc.exe",
            [action, entry.Name],
            60_000,
            cancellationToken).ConfigureAwait(false);

        _logger.Audit("Service." + action, entry.Name, result.Combined.Trim());

        return result.Success
            ? OperationResult.Ok(action == "start" ? "服务已启动" : "服务已停止", 1)
            : OperationResult.Fail(result.Combined.Trim());
    }

    private static string NormalizeStartMode(string startMode) => startMode.ToLowerInvariant() switch
    {
        "auto" => "Auto",
        "manual" => "Manual",
        "disabled" => "Disabled",
        "boot" or "system" => "Boot",
        _ => startMode
    };
}
