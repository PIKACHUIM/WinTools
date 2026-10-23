using WinTools.Core.Abstractions;
using WinTools.Core.Models;
using WinTools.Infrastructure.Common;

namespace WinTools.Infrastructure.Components;

/// <summary>应用商店：健康检查、缓存重置与重新注册。</summary>
public sealed class StoreService : IStoreService
{
    private readonly IAppLogger _logger;

    public StoreService(IAppLogger logger) => _logger = logger;

    public Task<StoreStatus> GetAsync(CancellationToken cancellationToken) => Task.Run(() =>
    {
        var windowsApps = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "WindowsApps");

        string? version = null;
        string? location = null;

        try
        {
            if (Directory.Exists(windowsApps))
            {
                var storeDir = Directory.EnumerateDirectories(windowsApps, "Microsoft.WindowsStore_*")
                    .OrderByDescending(d => d, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();

                if (storeDir is not null)
                {
                    location = storeDir;
                    var folder = Path.GetFileName(storeDir);
                    var parts = folder.Split('_');
                    if (parts.Length > 1)
                    {
                        version = parts[1];
                    }
                }
            }
        }
        catch
        {
            // 无权限时忽略
        }

        var services = Wmi.Query(
            "SELECT Name, State FROM Win32_Service WHERE Name='ClipSVC' OR Name='InstallService' OR Name='AppXSvc'");

        var running = services.Count > 0 && services.All(r =>
            r.TryGetValue("State", out var state) &&
            string.Equals(state?.ToString(), "Running", StringComparison.OrdinalIgnoreCase));

        cancellationToken.ThrowIfCancellationRequested();

        return new StoreStatus(version is not null, version, running, location);
    }, cancellationToken);

    public async Task<OperationResult> ResetCacheAsync(CancellationToken cancellationToken)
    {
        var result = await ProcessRunner.RunAsync("wsreset.exe", ["-i"], 60_000, cancellationToken)
            .ConfigureAwait(false);

        _logger.Audit("Store.ResetCache", "wsreset -i", result.Combined.Trim());

        return result.Success || result.TimedOut
            ? OperationResult.Ok("已重置商店缓存", 1)
            : OperationResult.Fail(result.Combined.Trim());
    }

    public async Task<OperationResult> ReRegisterAsync(CancellationToken cancellationToken)
    {
        const string script =
            "Get-AppxPackage -AllUsers Microsoft.WindowsStore | ForEach-Object { " +
            "Add-AppxPackage -DisableDevelopmentMode -Register \"$($_.InstallLocation)\\AppXManifest.xml\" }";

        var result = await ProcessRunner.RunPowerShellAsync(script, 180_000, cancellationToken)
            .ConfigureAwait(false);

        _logger.Audit("Store.ReRegister", "Microsoft.WindowsStore", result.Combined.Trim());

        return result.Success
            ? OperationResult.Ok("已重新注册应用商店", 1)
            : OperationResult.Fail(string.IsNullOrWhiteSpace(result.Combined) ? "重新注册失败" : result.Combined.Trim());
    }
}
