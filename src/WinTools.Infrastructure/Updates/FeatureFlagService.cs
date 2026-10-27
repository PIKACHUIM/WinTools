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

            // 两种布局都要认：
            //   旧版 Overrides\<特性ID>
            //   新版 Overrides\<优先级>\<特性ID>（24H2 起常见）
            var byId = new Dictionary<string, FeatureFlagEntry>(StringComparer.OrdinalIgnoreCase);

            foreach (var containerName in root.GetSubKeyNames())
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var container = root.OpenSubKey(containerName);
                if (container is null)
                {
                    continue;
                }

                if (container.GetValue("EnabledState") is not null)
                {
                    Add(container, containerName, ReadInt(container, "Priority", 0));
                    continue;
                }

                var containerPriority = int.TryParse(containerName, out var priority) ? priority : 0;

                foreach (var featureId in container.GetSubKeyNames())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    using var key = container.OpenSubKey(featureId);
                    if (key?.GetValue("EnabledState") is null)
                    {
                        continue;
                    }

                    Add(key, featureId, ReadInt(key, "Priority", containerPriority));
                }
            }

            void Add(RegistryKey key, string featureId, int priority)
            {
                var entry = new FeatureFlagEntry(
                    featureId,
                    ReadInt(key, "EnabledState", 2),
                    ReadInt(key, "EnabledStateOptions", 0),
                    ReadInt(key, "Variant", 0),
                    ReadInt(key, "VariantPayloadKind", 0),
                    priority,
                    FeatureNames.GetName(featureId),
                    FeatureNames.GetDescription(featureId));

                // 同一特性可能挂在多个优先级下，保留优先级最高的那条
                if (!byId.TryGetValue(featureId, out var existing) || priority >= existing.Priority)
                {
                    byId[featureId] = entry;
                }
            }

            list.AddRange(byId.Values);
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
            using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                .OpenSubKey(OverridesPath, writable: true);

            using var key = root is null ? null : OpenFeatureKey(root, entry.FeatureId, writable: true);

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

    public Task RefreshNamesAsync(CancellationToken cancellationToken) => FeatureNames.RefreshAsync(cancellationToken);

    public Task<OperationResult> SetPriorityAsync(
        FeatureFlagEntry entry,
        int priority,
        CancellationToken cancellationToken) => Task.Run(() =>
    {
        try
        {
            using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                .OpenSubKey(OverridesPath, writable: true);

            using var key = root is null ? null : OpenFeatureKey(root, entry.FeatureId, writable: true);

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

            // 旧版直接挂在 Overrides 下；新版挂在优先级子键下
            if (parent.OpenSubKey(entry.FeatureId) is not null)
            {
                parent.DeleteSubKeyTree(entry.FeatureId, throwOnMissingSubKey: false);
            }
            else
            {
                foreach (var containerName in parent.GetSubKeyNames())
                {
                    using var container = parent.OpenSubKey(containerName, writable: true);
                    if (container?.OpenSubKey(entry.FeatureId) is null)
                    {
                        continue;
                    }

                    container.DeleteSubKeyTree(entry.FeatureId, throwOnMissingSubKey: false);
                    break;
                }
            }

            _logger.Audit("FeatureFlag.Remove", entry.FeatureId);
            return OperationResult.Ok("已删除，重启后恢复系统默认", 1, restartRequired: true);
        }
        catch (Exception ex)
        {
            _logger.Error("FeatureFlags", "删除特性开关失败", ex);
            return OperationResult.Fail(ex.Message);
        }
    }, cancellationToken);

    /// <summary>按 ID 找到特性覆盖项：先找旧版直挂位置，再找新版的优先级子键。</summary>
    private static RegistryKey? OpenFeatureKey(RegistryKey root, string featureId, bool writable)
    {
        var direct = root.OpenSubKey(featureId, writable);
        if (direct is not null)
        {
            if (direct.GetValue("EnabledState") is not null)
            {
                return direct;
            }

            direct.Dispose();
        }

        foreach (var containerName in root.GetSubKeyNames())
        {
            if (string.Equals(containerName, featureId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            using var container = root.OpenSubKey(containerName);
            var candidate = container?.OpenSubKey(featureId, writable);
            if (candidate is not null && candidate.GetValue("EnabledState") is not null)
            {
                return candidate;
            }

            candidate?.Dispose();
        }

        return null;
    }

    private static int ReadInt(RegistryKey key, string name, int fallback)
    {
        var value = key.GetValue(name);
        return value is null ? fallback : Convert.ToInt32(value);
    }
}
