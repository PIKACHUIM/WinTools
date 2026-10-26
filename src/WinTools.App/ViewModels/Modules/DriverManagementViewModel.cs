using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using WinTools.App.Localization;
using WinTools.App.Services;
using WinTools.Core.Abstractions;
using WinTools.Core.Models;

namespace WinTools.App.ViewModels.Modules;

/// <summary>驱动管理：单页表格 + 批量备份 / 安装 / 删除。</summary>
public sealed partial class DriverManagementViewModel : ModuleViewModelBase
{
    public DriverManagementViewModel(IModuleCatalog catalog, IDriverService drivers)
        : base(catalog, "Drivers")
    {
        Register("SystemDrivers", new DriverCenterSectionViewModel(drivers));
        SelectSection(Sections[0].Id);
    }
}

/// <summary>驱动中心：一页搞定查看、备份、安装与删除。</summary>
public sealed partial class DriverCenterSectionViewModel : ListSectionViewModel
{
    private readonly IDriverService _drivers;

    public DriverCenterSectionViewModel(IDriverService drivers)
    {
        _drivers = drivers;
        ShowCheckboxes = true;
        HasPrimary = true;
        PrimaryLabel = Loc.T("Drv.BackupSelected");
        HasSecondary = true;
        SecondaryLabel = Loc.T("Drv.RemoveSelected");

        ExtraActions.Add(new ToolbarAction(Loc.T("Drv.InstallFromFolder"), InstallCommand));

        _ = ReloadAsync();
    }

    public override bool CanPrimary => !IsBusy && IsReady && CheckedRows.Count > 0;

    protected override bool HasData => Rows.Count > 0;

    protected override async Task LoadAsync(CancellationToken cancellationToken)
    {
        var drivers = await _drivers.GetAsync(cancellationToken).ConfigureAwait(true);

        Rows.Clear();

        foreach (var driver in drivers)
        {
            var detail = string.Join(" · ", new[] { driver.ClassName, driver.Version, driver.Date }
                .Where(s => !string.IsNullOrWhiteSpace(s)));

            var row = new RowItem(
                driver.PublishedName,
                driver.OriginalName,
                driver.Provider,
                detail,
                driver.PublishedName,
                RowTone.Neutral)
            {
                Tag = driver,
                ShowIcon = false,
                SearchBlob = $"{driver.PublishedName} {driver.OriginalName} {driver.Provider} {driver.ClassName}"
            };

            row.ShowCheckbox = true;
            row.ShowAction = true;
            row.ActionLabel = Loc.T("Common.Delete");
            row.ActionCommand = new AsyncRelayCommand(() => RemoveOneAsync(row, driver));

            Rows.Add(row);
        }
    }

    // ------------------------------------------------------------------
    // 批量备份
    // ------------------------------------------------------------------

    protected override async Task PrimaryCoreAsync(CancellationToken cancellationToken)
    {
        var targets = CheckedRows.Select(r => r.Tag).OfType<DriverPackage>().ToList();
        if (targets.Count == 0)
        {
            UI.Toast.Warning(Loc.T("Common.NothingSelected"));
            return;
        }

        var dialog = new OpenFolderDialog
        {
            Title = Loc.T("Drv.PickBackupFolder"),
            Multiselect = false
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        await ExecuteAsync(async ct =>
        {
            var ok = 0;
            var failed = 0;
            var watch = System.Diagnostics.Stopwatch.StartNew();

            ShowProgress = true;

            for (var i = 0; i < targets.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                ProgressPercent = (int)(i * 100d / targets.Count);
                LoadingText = Loc.F("Drv.BackingUp", i + 1, targets.Count, targets[i].OriginalName);

                var result = await _drivers.ExportAsync(targets[i], dialog.FolderName, ct).ConfigureAwait(true);
                if (result.Success)
                {
                    ok++;
                }
                else
                {
                    failed++;
                }
            }

            watch.Stop();

            Result = new ResultSummary(
                failed == 0 ? Loc.T("Drv.BackupDone") : Loc.T("Common.ResultSummary"),
                failed == 0 ? ResultKind.Success : ok > 0 ? ResultKind.Warning : ResultKind.Error,
                [
                    new ResultMetric(Loc.T("Common.Succeeded"), ok.ToString()),
                    new ResultMetric(Loc.T("Common.Failed"), failed.ToString()),
                    new ResultMetric(Loc.T("Common.Elapsed").Replace("{0}", string.Empty).Trim(), $"{watch.Elapsed.TotalSeconds:0.0}s")
                ],
                failed == 0 ? dialog.FolderName : null);
        }).ConfigureAwait(true);
    }

    // ------------------------------------------------------------------
    // 批量删除
    // ------------------------------------------------------------------

    protected override async Task SecondaryCoreAsync(CancellationToken cancellationToken)
    {
        var targets = CheckedRows.Select(r => r.Tag).OfType<DriverPackage>().ToList();
        if (targets.Count == 0)
        {
            UI.Toast.Warning(Loc.T("Common.NothingSelected"));
            return;
        }

        var confirmed = await UI.Confirm.AskAsync(new ConfirmRequest(
            Loc.T("Drv.RemoveSelected"),
            Loc.F("Drv.RemoveConfirm", targets.Count),
            targets.Take(15).Select(t => $"{t.OriginalName}  ({t.Version})").ToList(),
            Loc.T("Common.Delete"),
            Danger: true,
            RequireAcknowledge: true)).ConfigureAwait(true);

        if (!confirmed)
        {
            return;
        }

        await ExecuteAsync(async ct =>
        {
            var ok = 0;
            var failed = 0;
            var restart = false;
            var watch = System.Diagnostics.Stopwatch.StartNew();

            ShowProgress = true;

            for (var i = 0; i < targets.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                ProgressPercent = (int)(i * 100d / targets.Count);
                LoadingText = Loc.F("Drv.Removing", i + 1, targets.Count, targets[i].OriginalName);

                var result = await _drivers.RemoveAsync(targets[i], ct).ConfigureAwait(true);
                if (result.Success)
                {
                    ok++;
                    restart |= result.RestartRequired;
                }
                else
                {
                    failed++;
                }
            }

            watch.Stop();
            await RefreshQuietlyAsync(ct).ConfigureAwait(true);

            Result = new ResultSummary(
                failed == 0 ? Loc.T("Drv.RemoveDone") : Loc.T("Common.ResultSummary"),
                failed == 0 ? ResultKind.Success : ok > 0 ? ResultKind.Warning : ResultKind.Error,
                [
                    new ResultMetric(Loc.T("Common.Succeeded"), ok.ToString()),
                    new ResultMetric(Loc.T("Common.Failed"), failed.ToString()),
                    new ResultMetric(Loc.T("Common.Elapsed").Replace("{0}", string.Empty).Trim(), $"{watch.Elapsed.TotalSeconds:0.0}s")
                ],
                restart ? Loc.T("Common.RestartNeeded") : null);
        }).ConfigureAwait(true);
    }

    private async Task RemoveOneAsync(RowItem row, DriverPackage driver)
    {
        var confirmed = await UI.Confirm.AskAsync(new ConfirmRequest(
            Loc.T("Common.Delete"),
            Loc.F("Drv.RemoveOneConfirm", driver.OriginalName),
            [driver.PublishedName],
            Loc.T("Common.Delete"),
            Danger: true)).ConfigureAwait(true);

        if (!confirmed)
        {
            return;
        }

        row.IsActionEnabled = false;
        await ExecuteAsync(async ct =>
        {
            var result = await _drivers.RemoveAsync(driver, ct).ConfigureAwait(true);
            Report(result);

            if (result.Success)
            {
                await RefreshQuietlyAsync(ct).ConfigureAwait(true);
            }
        }).ConfigureAwait(true);
        row.IsActionEnabled = true;
    }

    /// <summary>从目录安装：选择一个含 .inf 的文件夹，交给 pnputil /add-driver 批量安装。</summary>
    [RelayCommand]
    private async Task InstallAsync()
    {
        var dialog = new OpenFolderDialog
        {
            Title = Loc.T("Drv.PickDriverFolder"),
            Multiselect = false
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        await ExecuteAsync(async ct =>
        {
            ShowProgress = true;
            LoadingText = Loc.T("Drv.Installing");

            var result = await _drivers.InstallAsync(dialog.FolderName, ct).ConfigureAwait(true);
            Report(result);

            if (result.Success)
            {
                await RefreshQuietlyAsync(ct).ConfigureAwait(true);
            }
        }).ConfigureAwait(true);
    }
}
