using CommunityToolkit.Mvvm.Input;
using WinTools.App.Localization;
using WinTools.Core.Models;

namespace WinTools.App.ViewModels.Modules;

/// <summary>安全中心的设置清单：分组、开关与只读项。</summary>
public sealed partial class SecurityCenterSectionViewModel
{
    /// <summary>设置项分组与展示顺序：id → 分组文案键。</summary>
    private static readonly (string Id, string Group)[] Layout =
    [
        ("RealTime", "Sec.Group.Virus"),
        ("Cloud", "Sec.Group.Virus"),
        ("Behavior", "Sec.Group.Virus"),
        ("ScriptScan", "Sec.Group.Virus"),
        ("Pua", "Sec.Group.Virus"),
        ("NetworkProtection", "Sec.Group.Attack"),
        ("FolderAccess", "Sec.Group.Attack"),
        ("Hvci", "Sec.Group.Attack"),
        ("Uac", "Sec.Group.System"),
        ("SmartScreen", "Sec.Group.System"),
        ("FirewallDomain", "Sec.Group.Firewall"),
        ("FirewallPrivate", "Sec.Group.Firewall"),
        ("FirewallPublic", "Sec.Group.Firewall"),
        ("Tamper", "Sec.Group.System")
    ];

    private static string NameOf(string id) => Loc.T("Sec." + id);

    private static string GroupOf(string id)
        => Layout.FirstOrDefault(l => l.Id == id).Group is { } key ? Loc.T(key) : string.Empty;

    private async Task LoadSettingsAsync(IReadOnlyList<SecuritySetting> settings, CancellationToken cancellationToken)
    {
        var map = settings.ToDictionary(s => s.Id, s => s, StringComparer.OrdinalIgnoreCase);

        foreach (var (id, _) in Layout)
        {
            map.TryGetValue(id, out var setting);

            var row = new RowItem(
                id,
                NameOf(id),
                GroupOf(id),
                setting is null || !setting.Supported
                    ? Loc.T("Sec.NotSupported")
                    : setting.Enabled ? Loc.T("Common.Enabled") : Loc.T("Common.Disabled"),
                null,
                setting is { Enabled: true } ? RowTone.Success : RowTone.Neutral)
            {
                IconSource = id == "Tamper" || !string.IsNullOrEmpty(id) ? null : null,
                ShowIcon = false,
                SearchBlob = $"{NameOf(id)} {GroupOf(id)}"
            };

            var readOnly = id == "Tamper";
            var supported = setting?.Supported ?? false;

            row.ShowToggle = supported && !readOnly;
            row.SetOnSilently(setting?.Enabled ?? false);

            if (!readOnly && supported)
            {
                row.ToggleHandler = (_, on) => ApplyAsync(id, on);
            }

            if (!supported)
            {
                row.IsToggleEnabled = false;
            }

            Rows.Add(row);
        }

        await Task.CompletedTask.ConfigureAwait(true);
    }

    private async Task<bool> ApplyAsync(string id, bool enabled)
    {
        var ok = false;

        await ExecuteAsync(async ct =>
        {
            var result = await _security.SetSettingAsync(id, enabled, ct).ConfigureAwait(true);
            Report(result);
            ok = result.Success;

            if (ok)
            {
                await RefreshQuietlyAsync(ct).ConfigureAwait(true);
            }
        }).ConfigureAwait(true);

        return ok;
    }
}
