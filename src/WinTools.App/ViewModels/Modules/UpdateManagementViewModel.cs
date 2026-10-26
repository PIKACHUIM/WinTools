using CommunityToolkit.Mvvm.Input;
using WinTools.App.Localization;
using WinTools.App.Services;
using WinTools.Core.Abstractions;
using WinTools.Core.Models;
using WinTools.Core.Utils;

namespace WinTools.App.ViewModels.Modules;

/// <summary>更新管理：已安装更新 / 可安装补丁 / 功能开关。</summary>
public sealed partial class UpdateManagementViewModel : ModuleViewModelBase
{
    public UpdateManagementViewModel(
        IModuleCatalog catalog,
        IUpdateService updates,
        IFeatureFlagService flags)
        : base(catalog, "Updates")
    {
        Register("Installed", new InstalledUpdatesSectionViewModel(updates));
        Register("Available", new AvailableUpdatesSectionViewModel(updates));
        Register("FeatureFlags", new FeatureFlagsSectionViewModel(flags));
        SelectSection(Sections[0].Id);
    }
}

/// <summary>已安装更新：列出历史更新并可卸载。</summary>
public sealed partial class InstalledUpdatesSectionViewModel : ListSectionViewModel
{
    private readonly IUpdateService _updates;

    public InstalledUpdatesSectionViewModel(IUpdateService updates)
    {
        _updates = updates;
        _ = ReloadAsync();
    }

    protected override async Task LoadAsync(CancellationToken cancellationToken)
    {
        var updates = await _updates.GetInstalledAsync(cancellationToken).ConfigureAwait(true);

        Rows.Clear();
        foreach (var update in updates)
        {
            var date = update.Date?.ToString("yyyy-MM-dd") ?? string.Empty;
            var row = new RowItem(
                update.Id,
                string.IsNullOrWhiteSpace(update.Title) ? update.Id : update.Title,
                update.Kb,
                date,
                null,
                RowTone.Neutral)
            {
                Tag = update,
                SearchBlob = $"{update.Title} {update.Kb}"
            };

            row.ShowAction = true;
            row.ActionLabel = Loc.T("Common.Uninstall");
            row.ActionCommand = new AsyncRelayCommand(() => UninstallAsync(row, update));
            Rows.Add(row);
        }

    }

    private async Task UninstallAsync(RowItem row, UpdateEntry update)
    {
        row.IsActionEnabled = false;
        await ExecuteAsync(async ct =>
        {
            var result = await _updates.UninstallAsync(update.Id, ct).ConfigureAwait(true);
            Report(result);

            if (result.Success)
            {
                await LoadAsync(ct).ConfigureAwait(true);
            }
        }).ConfigureAwait(true);

        row.IsActionEnabled = true;
    }
}

/// <summary>可安装补丁：在线搜索 + 勾选安装。</summary>
public sealed partial class AvailableUpdatesSectionViewModel : ListSectionViewModel
{
    private readonly IUpdateService _updates;

    public AvailableUpdatesSectionViewModel(IUpdateService updates)
    {
        _updates = updates;
        ShowCheckboxes = true;
        HasPrimary = true;
        PrimaryLabel = Loc.T("Common.InstallSelected");

        // 联网搜索耗时较长且会占用 Windows Update 服务：进入页面不自动搜索，由用户主动触发
        State = PageState.Idle;
        IdleText = Loc.T("Updates.SearchIdle");
        IdleActionLabel = Loc.T("Updates.SearchAction");
    }

    public override bool CanPrimary => !IsBusy && IsReady && Rows.Any(r => r.IsChecked);

    protected override async Task LoadAsync(CancellationToken cancellationToken)
    {
        LoadingText = Loc.T("Updates.Searching");
        var progress = new Progress<string>(text => LoadingText = text);
        var updates = await _updates.SearchAvailableAsync(progress, cancellationToken).ConfigureAwait(true);

        Rows.Clear();
        foreach (var update in updates)
        {
            var row = new RowItem(
                update.Id,
                string.IsNullOrWhiteSpace(update.Title) ? update.Id : update.Title,
                update.Kb,
                update.SizeBytes is null ? null : SizeFormatter.Format(update.SizeBytes.Value),
                null,
                RowTone.Accent)
            {
                Tag = update,
                SearchBlob = $"{update.Title} {update.Kb}"
            };

            row.ShowCheckbox = true;
            row.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(RowItem.IsChecked))
                {
                    OnPropertyChanged(nameof(CanPrimary));
                }
            };

            Rows.Add(row);
        }
    }

    protected override void OnLoaded()
    {
        base.OnLoaded();
        OnPropertyChanged(nameof(CanPrimary));
    }


    protected override async Task SecondaryCoreAsync(CancellationToken cancellationToken)
        => await ReloadAsync(cancellationToken).ConfigureAwait(true);

    protected override async Task PrimaryCoreAsync(CancellationToken cancellationToken)
    {
        var selected = CheckedRows;
        if (selected.Count == 0)
        {
            Status = Loc.T("Common.NothingSelected");
            return;
        }

        var progress = new Progress<string>(text => Status = text);
        ShowProgress = true;
        ProgressPercent = 0;

        var ids = selected.Select(r => r.Key).ToList();
        var result = await _updates.InstallAsync(ids, progress, cancellationToken).ConfigureAwait(true);
        Report(result);

        if (result.Success)
        {
            foreach (var row in selected)
            {
                row.IsChecked = false;
            }
        }
    }
}

/// <summary>功能开关：FeatureManagement\Overrides 的读写。</summary>
public sealed partial class FeatureFlagsSectionViewModel : ListSectionViewModel
{
    private readonly IFeatureFlagService _flags;

    public FeatureFlagsSectionViewModel(IFeatureFlagService flags)
    {
        _flags = flags;
        ShowCheckboxes = true;
        HasPrimary = true;
        PrimaryLabel = Loc.T("Common.Enable");
        HasSecondary = true;
        SecondaryLabel = Loc.T("Common.Delete");
        HasInput = true;
        InputLabel = Loc.T("Flag.FeatureId");
        _ = ReloadAsync();
    }

    protected override async Task LoadAsync(CancellationToken cancellationToken)
    {
        // 先刷新在线名称表（8 秒超时、失败静默），再读覆盖项，列表里就能带出名称与说明
        await _flags.RefreshNamesAsync(cancellationToken).ConfigureAwait(true);
        var entries = await _flags.GetOverridesAsync(cancellationToken).ConfigureAwait(true);

        Rows.Clear();
        foreach (var entry in entries)
        {
            var stateText = entry.EnabledState switch
            {
                0 => Loc.T("Common.Disabled"),
                1 => Loc.T("Common.Enabled"),
                _ => Loc.T("Flag.StateDefault")
            };

            // 状态交给开关、优先级交给下拉，行里不再重复显示，避免右侧挤在一起
            var row = new RowItem(
                entry.FeatureId,
                string.IsNullOrWhiteSpace(entry.Name)
                    ? Loc.F("Flag.NameUnknown", entry.FeatureId)
                    : entry.Name,
                string.IsNullOrWhiteSpace(entry.Description)
                    ? $"ID {entry.FeatureId} · " + Loc.F("Flag.Variant", entry.Variant)
                    : entry.Description,
                null,
                null,
                entry.EnabledState switch
                {
                    1 => RowTone.Success,
                    0 => RowTone.Warning,
                    _ => RowTone.Neutral
                })
            {
                Tag = entry,
                SearchBlob = $"{entry.FeatureId} {stateText}"
            };

            // 状态用开关（ViVe 的 EnabledState），优先级用行内下拉
            row.ShowCheckbox = true;
            row.ShowToggle = true;
            row.SetOnSilently(entry.EnabledState == 1);
            row.ToggleHandler = (_, on) => SetStateAsync(entry, on);

            row.ShowPicker = true;
            row.SetPickerItems(PriorityLabels);
            row.SetPickerSilently(PriorityLabel(entry.Priority));
            row.PickerHandler = (_, label) => SetPriorityAsync(entry, PriorityValue(label));

            // 删除走「勾选 + 工具栏删除」，行内只留优先级与状态开关
            Rows.Add(row);
        }
    }

    // VIVE 的优先级：1 = User，2 = Device，3 = Fallback，4 = Service
    private static IReadOnlyList<string> PriorityLabels =>
    [
        Loc.T("Flag.Priority.User"),
        Loc.T("Flag.Priority.Device"),
        Loc.T("Flag.Priority.Fallback"),
        Loc.T("Flag.Priority.Service")
    ];

    private static string PriorityLabel(int priority) => priority switch
    {
        2 => Loc.T("Flag.Priority.Device"),
        3 => Loc.T("Flag.Priority.Fallback"),
        4 => Loc.T("Flag.Priority.Service"),
        _ => Loc.T("Flag.Priority.User")
    };

    private static int PriorityValue(string label)
    {
        if (label == Loc.T("Flag.Priority.Device"))
        {
            return 2;
        }

        if (label == Loc.T("Flag.Priority.Fallback"))
        {
            return 3;
        }

        return label == Loc.T("Flag.Priority.Service") ? 4 : 1;
    }

    private async Task<bool> SetStateAsync(FeatureFlagEntry entry, bool enabled)
    {
        var ok = false;

        await ExecuteAsync(async ct =>
        {
            var result = await _flags.SetEnabledStateAsync(entry, enabled ? 1 : 0, ct).ConfigureAwait(true);
            Report(result);
            ok = result.Success;

            if (ok)
            {
                await LoadAsync(ct).ConfigureAwait(true);
            }
        }).ConfigureAwait(true);

        return ok;
    }

    private async Task<bool> SetPriorityAsync(FeatureFlagEntry entry, int priority)
    {
        var ok = false;

        await ExecuteAsync(async ct =>
        {
            var result = await _flags.SetPriorityAsync(entry, priority, ct).ConfigureAwait(true);
            Report(result);
            ok = result.Success;

            if (ok)
            {
                await LoadAsync(ct).ConfigureAwait(true);
            }
        }).ConfigureAwait(true);

        return ok;
    }

    private async Task DeleteOneAsync(RowItem row, FeatureFlagEntry entry)
    {
        var confirmed = await UI.Confirm.AskAsync(new ConfirmRequest(
            Loc.T("Common.Delete"),
            Loc.F("Flag.ConfirmDelete", entry.FeatureId),
            [entry.FeatureId],
            Loc.T("Common.Delete"),
            Danger: true)).ConfigureAwait(true);

        if (!confirmed)
        {
            return;
        }

        row.IsActionEnabled = false;
        await ExecuteAsync(async ct =>
        {
            var result = await _flags.RemoveAsync(entry, ct).ConfigureAwait(true);
            Report(result);

            if (result.Success)
            {
                await LoadAsync(ct).ConfigureAwait(true);
            }
        }).ConfigureAwait(true);
        row.IsActionEnabled = true;
    }

    private async Task ToggleAsync(RowItem row, FeatureFlagEntry entry)
    {
        row.IsActionEnabled = false;
        await ExecuteAsync(async ct =>
        {
            var target = entry.EnabledState == 1 ? 0 : 1;
            var result = await _flags.SetEnabledStateAsync(entry, target, ct).ConfigureAwait(true);
            Report(result);

            if (result.Success)
            {
                await LoadAsync(ct).ConfigureAwait(true);
            }
        }).ConfigureAwait(true);

        row.IsActionEnabled = true;
    }

    protected override async Task PrimaryCoreAsync(CancellationToken cancellationToken)
    {
        var selected = CheckedRows;
        if (selected.Count == 0)
        {
            Status = Loc.T("Common.NothingSelected");
            return;
        }

        var affected = 0;
        foreach (var row in selected)
        {
            if (row.Tag is not FeatureFlagEntry entry)
            {
                continue;
            }

            var result = await _flags.SetEnabledStateAsync(entry, 1, cancellationToken).ConfigureAwait(true);
            if (result.Success)
            {
                affected++;
            }
        }

        Status = Loc.F("Common.TotalCount", affected);
        await LoadAsync(cancellationToken).ConfigureAwait(true);
    }

    protected override async Task SecondaryCoreAsync(CancellationToken cancellationToken)
    {
        var selected = CheckedRows;
        if (selected.Count == 0)
        {
            Status = Loc.T("Common.NothingSelected");
            return;
        }

        foreach (var row in selected)
        {
            if (row.Tag is FeatureFlagEntry entry)
            {
                await _flags.RemoveAsync(entry, cancellationToken).ConfigureAwait(true);
            }
        }

        await LoadAsync(cancellationToken).ConfigureAwait(true);
    }

    protected override async Task InputCoreAsync(CancellationToken cancellationToken)
    {
        var id = InputText.Trim();
        if (id.Length == 0)
        {
            return;
        }

        var result = await _flags.AddAsync(id, 1, cancellationToken).ConfigureAwait(true);
        Report(result);

        if (result.Success)
        {
            InputText = string.Empty;
            await LoadAsync(cancellationToken).ConfigureAwait(true);
        }
    }
}
