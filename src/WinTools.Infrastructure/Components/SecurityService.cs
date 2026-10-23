using Microsoft.Win32;
using WinTools.Core.Abstractions;
using WinTools.Core.Models;
using WinTools.Infrastructure.Common;

namespace WinTools.Infrastructure.Components;

/// <summary>安全中心：Defender 状态、实时保护开关与排除项。</summary>
public sealed class SecurityService : ISecurityService
{
    private const string DefenderPath = @"SOFTWARE\Microsoft\Windows Defender";
    private const string RealtimePath = @"SOFTWARE\Microsoft\Windows Defender\Real-Time Protection";
    private const string FeaturesPath = @"SOFTWARE\Microsoft\Windows Defender\Features";
    private const string ExclusionsPath = @"SOFTWARE\Microsoft\Windows Defender\Exclusions\Paths";

    private readonly IAppLogger _logger;

    public SecurityService(IAppLogger logger) => _logger = logger;

    public Task<SecurityStatus> GetAsync(CancellationToken cancellationToken) => Task.Run(() =>
    {
        var present = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
            .OpenSubKey(DefenderPath) is not null;

        using var realtime = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
            .OpenSubKey(RealtimePath);

        var realtimeEnabled = !ReadBool(realtime, "DisableRealtimeMonitoring");
        var behaviorEnabled = !ReadBool(realtime, "DisableBehaviorMonitoring");

        using var features = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
            .OpenSubKey(FeaturesPath);
        var tamper = ReadInt(features, "TamperProtection") >= 4;

        var avRows = Wmi.Query(
            "SELECT displayName, productState FROM AntiVirusProduct",
            @"root\SecurityCenter2");
        var antivirus = Wmi.FirstString(avRows, "displayName");

        using var engine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
            .OpenSubKey(DefenderPath);
        var engineVersion = engine?.GetValue("AMEngineVersion")?.ToString();

        var exclusions = new List<string>();
        using (var paths = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                   .OpenSubKey(ExclusionsPath))
        {
            if (paths is not null)
            {
                exclusions.AddRange(paths.GetValueNames().Where(n => n.Length > 0));
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        return new SecurityStatus(
            present,
            realtimeEnabled,
            behaviorEnabled,
            tamper,
            antivirus,
            engineVersion,
            exclusions);
    }, cancellationToken);

    public Task<OperationResult> SetRealTimeAsync(bool enabled, CancellationToken cancellationToken) => Task.Run(() =>
    {
        try
        {
            using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                .CreateSubKey(RealtimePath, writable: true);

            if (key is null)
            {
                return OperationResult.Fail("无法打开实时保护注册表项");
            }

            key.SetValue(
                "DisableRealtimeMonitoring",
                enabled ? 0 : 1,
                RegistryValueKind.DWord);

            _logger.Audit("Defender.RealTime", enabled ? "Enable" : "Disable");
            return OperationResult.Ok(
                enabled ? "已请求开启实时保护" : "已请求关闭实时保护",
                1);
        }
        catch (Exception ex)
        {
            _logger.Error("Security", "修改实时保护失败", ex);
            return OperationResult.Fail("写入失败（篡改保护可能已开启）: " + ex.Message);
        }
    }, cancellationToken);

    public Task<OperationResult> AddExclusionAsync(string path, CancellationToken cancellationToken) => Task.Run(() =>
    {
        var value = path.Trim();
        if (value.Length == 0)
        {
            return OperationResult.Fail("路径不能为空");
        }

        try
        {
            using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                .CreateSubKey(ExclusionsPath, writable: true);

            if (key is null)
            {
                return OperationResult.Fail("无法打开排除项注册表项");
            }

            key.SetValue(value, 0, RegistryValueKind.DWord);
            _logger.Audit("Defender.Exclusion.Add", value);
            return OperationResult.Ok("已添加排除项", 1);
        }
        catch (Exception ex)
        {
            _logger.Error("Security", "添加排除项失败", ex);
            return OperationResult.Fail(ex.Message);
        }
    }, cancellationToken);

    public Task<OperationResult> RemoveExclusionAsync(string path, CancellationToken cancellationToken) => Task.Run(() =>
    {
        try
        {
            using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                .OpenSubKey(ExclusionsPath, writable: true);

            key?.DeleteValue(path, throwOnMissingValue: false);
            _logger.Audit("Defender.Exclusion.Remove", path);
            return OperationResult.Ok("已移除排除项", 1);
        }
        catch (Exception ex)
        {
            _logger.Error("Security", "移除排除项失败", ex);
            return OperationResult.Fail(ex.Message);
        }
    }, cancellationToken);

    private static bool ReadBool(RegistryKey? key, string name)
        => key?.GetValue(name) is int value && value != 0;

    private static int ReadInt(RegistryKey? key, string name)
        => key?.GetValue(name) is int value ? value : 0;
}
