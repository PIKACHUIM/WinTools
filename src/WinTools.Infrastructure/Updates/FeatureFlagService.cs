using Microsoft.Win32;
using WinTools.Core.Abstractions;
using WinTools.Core.Models;

namespace WinTools.Infrastructure.Updates;

/// <summary>
/// 特性开关（ViVe 风格）：读写
/// HKLM\SYSTEM\CurrentControlSet\Control\FeatureManagement\Overrides\&lt;FeatureId&gt;。
/// EnabledState：0 = 关闭，1 = 开启，2 = 系统默认。
/// </summary>
public sealed class FeatureFlagService : IFeatureFlagService
{
    private const string OverridesPath =
        @"SYSTEM\CurrentControlSet\Control\FeatureManagement\Overrides";

    private readonly IAppLogger _logger;

    public FeatureFlagService(IAppLogger logger) => _logger = logger;

    public Task<IReadOnlyList<FeatureFlagEntry>> GetOverridesAsync(CancellationToken cancellationToken) => Task.Run(() =>
    {
        var list = new List<FeatureFlagEntry>();

        try
        {
            using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                .OpenSubKey(OverridesPath);

            if (root is null)
            {
                return (IReadOnlyList<FeatureFlagEntry>)list;
            }

            foreach (var featureId in root.GetSubKeyNames())
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var key = root.OpenSubKey(featureId);
                if (key is null)
                {
                    continue;
                }

                list.Add(new FeatureFlagEntry(
                    featureId,
                    ReadInt(key, "EnabledState", 2),
                    ReadInt(key, "EnabledStateOptions", 0),
                    ReadInt(key, "Variant", 0),
                    ReadInt(key, "VariantPayloadKind", 0),
                    ReadInt(key, "Priority", 0),
                    FeatureNames.GetValueOrDefault(featureId)));
            }
        }
        catch (Exception ex)
        {
            _logger.Error("FeatureFlags", "读取特性开关失败", ex);
        }

        return (IReadOnlyList<FeatureFlagEntry>)list.OrderBy(e => e.FeatureId).ToList();
    }, cancellationToken);

    public Task<OperationResult> SetEnabledStateAsync(
        FeatureFlagEntry entry,
        int enabledState,
        CancellationToken cancellationToken) => Task.Run(() =>
    {
        try
        {
            using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                .OpenSubKey(OverridesPath, writable: true)
                ?.OpenSubKey(entry.FeatureId, writable: true);

            if (key is null)
            {
                return OperationResult.Fail("找不到该特性的覆盖项");
            }

            key.SetValue("EnabledState", enabledState, RegistryValueKind.DWord);
            key.SetValue("EnabledStateOptions", enabledState == 2 ? 0 : 1, RegistryValueKind.DWord);

            _logger.Audit("FeatureFlag.Set", entry.FeatureId, $"EnabledState={enabledState}");
            return OperationResult.Ok("已写入，重启后生效", 1, restartRequired: true);
        }
        catch (Exception ex)
        {
            _logger.Error("FeatureFlags", "写入特性开关失败", ex);
            return OperationResult.Fail(ex.Message);
        }
    }, cancellationToken);

    public Task<OperationResult> SetPriorityAsync(
        FeatureFlagEntry entry,
        int priority,
        CancellationToken cancellationToken) => Task.Run(() =>
    {
        try
        {
            using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                .OpenSubKey(OverridesPath, writable: true)
                ?.OpenSubKey(entry.FeatureId, writable: true);

            if (key is null)
            {
                return OperationResult.Fail("找不到该特性的覆盖项");
            }

            key.SetValue("Priority", priority, RegistryValueKind.DWord);
            _logger.Audit("FeatureFlag.Priority", entry.FeatureId, $"Priority={priority}");
            return OperationResult.Ok("已保存，重启后生效", restartRequired: true);
        }
        catch (Exception ex)
        {
            _logger.Error("FeatureFlags", "写入优先级失败", ex);
            return OperationResult.Fail(ex.Message);
        }
    }, cancellationToken);

    public Task<OperationResult> AddAsync(string featureId, int enabledState, CancellationToken cancellationToken) => Task.Run(() =>
    {
        var id = featureId.Trim();
        if (id.Length == 0 || !id.All(char.IsDigit))
        {
            return OperationResult.Fail("特性 ID 必须是数字");
        }

        try
        {
            using var parent = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                .CreateSubKey(OverridesPath, writable: true);

            using var key = parent?.CreateSubKey(id, writable: true);
            if (key is null)
            {
                return OperationResult.Fail("无法创建注册表项");
            }

            key.SetValue("EnabledState", enabledState, RegistryValueKind.DWord);
            key.SetValue("EnabledStateOptions", enabledState == 2 ? 0 : 1, RegistryValueKind.DWord);
            key.SetValue("Variant", 0, RegistryValueKind.DWord);
            key.SetValue("VariantPayloadKind", 0, RegistryValueKind.DWord);
            key.SetValue("Priority", 1, RegistryValueKind.DWord);

            _logger.Audit("FeatureFlag.Add", id, $"EnabledState={enabledState}");
            return OperationResult.Ok("已添加，重启后生效", 1, restartRequired: true);
        }
        catch (Exception ex)
        {
            _logger.Error("FeatureFlags", "添加特性开关失败", ex);
            return OperationResult.Fail(ex.Message);
        }
    }, cancellationToken);

    public Task<OperationResult> RemoveAsync(FeatureFlagEntry entry, CancellationToken cancellationToken) => Task.Run(() =>
    {
        try
        {
            using var parent = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                .OpenSubKey(OverridesPath, writable: true);

            if (parent is null)
            {
                return OperationResult.Fail("找不到覆盖项根键");
            }

            parent.DeleteSubKeyTree(entry.FeatureId, throwOnMissingSubKey: false);
            _logger.Audit("FeatureFlag.Remove", entry.FeatureId);
            return OperationResult.Ok("已删除，重启后恢复系统默认", 1, restartRequired: true);
        }
        catch (Exception ex)
        {
            _logger.Error("FeatureFlags", "删除特性开关失败", ex);
            return OperationResult.Fail(ex.Message);
        }
    }, cancellationToken);

    private static int ReadInt(RegistryKey key, string name, int fallback)
    {
        var value = key.GetValue(name);
        return value is null ? fallback : Convert.ToInt32(value);
    }
}
