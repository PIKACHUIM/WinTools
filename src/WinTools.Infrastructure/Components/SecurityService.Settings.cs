using Microsoft.Win32;
using WinTools.Core.Models;

namespace WinTools.Infrastructure.Components;

/// <summary>安全中心的可开关项：直接读写系统策略/组件键值，不依赖命令行。</summary>
public sealed partial class SecurityService
{
    private sealed record SettingDef(
        string Id,
        string Path,
        string ValueName,
        bool Invert = false,
        bool IsString = false,
        bool ReadOnly = false,
        string? OnText = null);

    private static readonly SettingDef[] Definitions =
    [
        new("RealTime", @"SOFTWARE\Microsoft\Windows Defender\Real-Time Protection", "DisableRealtimeMonitoring", Invert: true),
        new("Behavior", @"SOFTWARE\Microsoft\Windows Defender\Real-Time Protection", "DisableBehaviorMonitoring", Invert: true),
        new("ScriptScan", @"SOFTWARE\Microsoft\Windows Defender\Real-Time Protection", "DisableScriptScanning", Invert: true),
        new("Cloud", @"SOFTWARE\Microsoft\Windows Defender", "SpynetReporting"),
        new("Pua", @"SOFTWARE\Microsoft\Windows Defender", "PUAProtection"),
        new("NetworkProtection", @"SOFTWARE\Microsoft\Windows Defender\Windows Defender Exploit Guard\Network Protection", "EnableNetworkProtection"),
        new("FolderAccess", @"SOFTWARE\Microsoft\Windows Defender\Windows Defender Exploit Guard\Controlled Folder Access", "EnableControlledFolderAccess"),
        new("SmartScreen", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer", "SmartScreenEnabled", IsString: true, OnText: "Warn"),
        new("Uac", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "EnableLUA"),
        new("FirewallDomain", @"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\DomainProfile", "EnableFirewall"),
        new("FirewallPrivate", @"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\StandardProfile", "EnableFirewall"),
        new("FirewallPublic", @"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\PublicProfile", "EnableFirewall"),
        new("Hvci", @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity", "Enabled"),
        new("Tamper", @"SOFTWARE\Microsoft\Windows Defender\Features", "TamperProtection", ReadOnly: true)
    ];

    public Task<IReadOnlyList<SecuritySetting>> GetSettingsAsync(CancellationToken cancellationToken) => Task.Run(() =>
    {
        var list = new List<SecuritySetting>();

        foreach (var def in Definitions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                    .OpenSubKey(def.Path);

                if (key is null)
                {
                    list.Add(new SecuritySetting(def.Id, false, false));
                    continue;
                }

                var raw = key.GetValue(def.ValueName);
                var enabled = def.IsString
                    ? string.Equals(raw as string, def.OnText ?? "On", StringComparison.OrdinalIgnoreCase)
                    : ReadInt(key, def.ValueName) != 0;

                list.Add(new SecuritySetting(def.Id, def.Invert ? !enabled : enabled, true));
            }
            catch (Exception ex)
            {
                _logger.Warn("Security", $"读取设置 {def.Id} 失败：{ex.Message}");
                list.Add(new SecuritySetting(def.Id, false, false));
            }
        }

        return (IReadOnlyList<SecuritySetting>)list;
    }, cancellationToken);

    public Task<OperationResult> SetSettingAsync(string id, bool enabled, CancellationToken cancellationToken) => Task.Run(() =>
    {
        var def = Definitions.FirstOrDefault(d => string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase));
        if (def is null)
        {
            return OperationResult.Fail($"未知的设置项：{id}");
        }

        if (def.ReadOnly)
        {
            return OperationResult.Fail("篡改保护由系统管理，需要先在 Windows 安全中心关闭");
        }

        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var key = baseKey.CreateSubKey(def.Path, writable: true);
            if (key is null)
            {
                return OperationResult.Fail("无法写入系统设置（需要管理员权限）");
            }

            var value = def.Invert ? !enabled : enabled;

            if (def.IsString)
            {
                key.SetValue(def.ValueName, value ? def.OnText ?? "On" : "Off", RegistryValueKind.String);
            }
            else
            {
                key.SetValue(def.ValueName, value ? 1 : 0, RegistryValueKind.DWord);
            }

            _logger.Audit("Security.Set", id, $"enabled={enabled}");

            return OperationResult.Ok("已保存", restartRequired: id is "Uac" or "Hvci");
        }
        catch (Exception ex)
        {
            _logger.Error("Security", $"写入设置 {id} 失败", ex);
            return OperationResult.Fail(ex.Message);
        }
    }, cancellationToken);
}
