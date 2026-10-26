using CommunityToolkit.Mvvm.Input;
using WinTools.App.Localization;
using WinTools.App.Services;
using WinTools.Core.Abstractions;
using WinTools.Core.Models;

namespace WinTools.App.ViewModels.Modules;

/// <summary>组件管理：安全中心 + 应用商店。</summary>
public sealed partial class ComponentManagementViewModel : ModuleViewModelBase
{
    public ComponentManagementViewModel(
        IModuleCatalog catalog,
        ISecurityService security,
        IStoreService store)
        : base(catalog, "Components")
    {
        Register("SecurityCenter", new SecurityCenterSectionViewModel(security));
        Register("Store", new StoreSectionViewModel(store));
        SelectSection(Sections[0].Id);
    }
}

/// <summary>安全中心：Defender 状态、实时保护、排除项。</summary>
public sealed partial class SecurityCenterSectionViewModel : ListSectionViewModel
{
    private readonly ISecurityService _security;

    public SecurityCenterSectionViewModel(ISecurityService security)
    {
        _security = security;
        HasSearch = true;

        // 排除项仍在行内输入；开关型设置集中在列表里
        HasInput = true;
        InputLabel = Loc.T("Sec.ExclusionPath");
        _ = ReloadAsync();
    }

    protected override async Task LoadAsync(CancellationToken cancellationToken)
    {
        var status = await _security.GetAsync(cancellationToken).ConfigureAwait(true);
        var settings = await _security.GetSettingsAsync(cancellationToken).ConfigureAwait(true);

        Rows.Clear();

        await LoadSettingsAsync(settings, cancellationToken).ConfigureAwait(true);

        // 只读信息：杀毒产品与引擎版本
        Rows.Add(new RowItem("av", Loc.T("Sec.Antivirus"), Loc.T("Sec.Group.Virus"),
            status.AntivirusProduct ?? Loc.T("Common.None")));
        Rows.Add(new RowItem("engine", Loc.T("Sec.Engine"), Loc.T("Sec.Group.Virus"),
            status.EngineVersion ?? Loc.T("Common.None")));

        foreach (var exclusion in status.Exclusions)
        {
            var row = new RowItem("ex:" + exclusion, exclusion, Loc.T("Sec.Exclusions"),
                null, null, RowTone.Neutral);
            row.ShowAction = true;
            row.ActionLabel = Loc.T("Common.Remove");
            row.ActionCommand = new AsyncRelayCommand(() => RemoveAsync(exclusion));
            Rows.Add(row);
        }

        Status = string.Empty;
    }

    private async Task RemoveAsync(string path)
    {
        await ExecuteAsync(async ct =>
        {
            var result = await _security.RemoveExclusionAsync(path, ct).ConfigureAwait(true);
            Report(result);
            await RefreshQuietlyAsync(ct).ConfigureAwait(true);
        }).ConfigureAwait(true);
    }

    protected override async Task InputCoreAsync(CancellationToken cancellationToken)
    {
        var path = InputText.Trim();
        if (path.Length == 0)
        {
            return;
        }

        var result = await _security.AddExclusionAsync(path, cancellationToken).ConfigureAwait(true);
        Report(result);

        if (result.Success)
        {
            InputText = string.Empty;
            await RefreshQuietlyAsync(cancellationToken).ConfigureAwait(true);
        }
    }
}

/// <summary>系统组件：内置应用（终端/计算器/记事本…）、WSL 发行版与商店修复。</summary>
public sealed partial class StoreSectionViewModel : ListSectionViewModel
{
    private readonly IStoreService _store;



    public StoreSectionViewModel(IStoreService store)
    {
        _store = store;
        ShowCheckboxes = true;
        HasPrimary = true;
        PrimaryLabel = Loc.T("Comp.InstallSelected");
        HasSecondary = true;
        SecondaryLabel = Loc.T("Comp.UninstallSelected");

        _ = ReloadAsync();
    }

    public override bool CanPrimary => !IsBusy && IsReady && CheckedRows.Count > 0;

    protected override async Task LoadAsync(CancellationToken cancellationToken)
    {
        var apps = await _store.GetAppsAsync(cancellationToken).ConfigureAwait(true);

        Rows.Clear();

        foreach (var app in apps)
        {
            var name = Loc.T("App." + app.Id);
            var row = new RowItem(
                app.Id,
                string.IsNullOrWhiteSpace(name) || name.StartsWith("App.", StringComparison.Ordinal) ? app.PackageName : name,
                app.IsInstalled ? app.Version ?? Loc.T("Common.Enabled") : Loc.T("Common.NotInstalled"),
                null,
                app.IsInstalled ? Loc.T("Comp.Installed") : Loc.T("Comp.Missing"),
                app.IsInstalled ? RowTone.Success : RowTone.Neutral)
            {
                Tag = app,
                ShowIcon = true,
                SearchBlob = $"{app.Id} {app.PackageName} {app.WingetId}"
            };

            row.ShowCheckbox = true;
            row.ShowAction = true;
            // 已安装 → 修复（重新注册清单）；未安装 → 安装。卸载统一走勾选 + 工具栏
            row.ActionLabel = app.IsInstalled ? Loc.T("Comp.Repair") : Loc.T("Common.Install");
            row.ActionCommand = app.IsInstalled
                ? new AsyncRelayCommand(() => RepairOneAsync(row, app))
                : new AsyncRelayCommand(() => ToggleInstallAsync(row, app, install: true));

            Rows.Add(row);
        }
        // 商店自身修复：等效「应用商店疑难解答」
        var storeRow = new RowItem("store-repair", Loc.T("Comp.RepairStore"), Loc.T("Comp.StoreRow"),
            null, null, RowTone.Neutral)
        {
            ShowIcon = false
        };
        storeRow.ShowAction = true;
        storeRow.ActionLabel = Loc.T("Comp.Repair");
        storeRow.ActionCommand = RepairStoreCommand;
        Rows.Add(storeRow);

        // Linux 发行版：联机清单较慢，做成一行按需触发
        var wslRow = new RowItem("wsl-load", Loc.T("Comp.LoadWsl"), Loc.T("Comp.WslDistro"),
            null, null, RowTone.Neutral)
        {
            ShowIcon = false
        };
        wslRow.ShowAction = true;
        wslRow.ActionLabel = Loc.T("Common.Load");
        wslRow.ActionCommand = LoadWslCommand;
        Rows.Add(wslRow);

        // 安卓子系统：官方已停服，只能装第三方打包版（用户自备 .msix）
        var wsaRow = new RowItem("wsa-install", Loc.T("Comp.WsaRow"), Loc.T("Comp.WsaHint"),
            null, null, RowTone.Neutral)
        {
            ShowIcon = false
        };
        wsaRow.ShowAction = true;
        wsaRow.ActionLabel = Loc.T("Comp.PickPackage");
        wsaRow.ActionCommand = InstallWsaCommand;
        Rows.Add(wsaRow);
    }

    /// <summary>按需加载 WSL 发行版（含联机清单），避免进页面就卡在网络上。</summary>
    [RelayCommand]
    private async Task LoadWslAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<WslDistro> wsl;
        try
        {
            wsl = await _store.GetWslAsync(includeOnline: true, cancellationToken).ConfigureAwait(true);
        }
        catch
        {
            return;
        }

        foreach (var distro in wsl)
        {
            var row = new RowItem(
                "wsl:" + distro.Name,
                distro.Name,
                Loc.T("Comp.WslDistro"),
                null,
                distro.IsInstalled ? Loc.T("Comp.Installed") : Loc.T("Comp.Missing"),
                distro.IsInstalled ? RowTone.Success : RowTone.Neutral)
            {
                Tag = distro,
                ShowIcon = true,
                SearchBlob = distro.Name + " wsl"
            };

            row.ShowCheckbox = distro.IsInstalled;
            row.ShowAction = !distro.IsInstalled;
            row.ActionLabel = Loc.T("Common.Install");
            row.ActionCommand = new AsyncRelayCommand(() => InstallWslAsync(row, distro));

            Rows.Add(row);
        }
    }

    private async Task RepairOneAsync(RowItem row, StoreApp app)
    {
        row.IsActionEnabled = false;
        await ExecuteAsync(async ct =>
        {
            var result = await _store.RepairAppAsync(app, ct).ConfigureAwait(true);
            Report(result);
        }).ConfigureAwait(true);
        row.IsActionEnabled = true;
    }

    private async Task ToggleInstallAsync(RowItem row, StoreApp app, bool install)
    {
        if (!install)
        {
            var confirmed = await UI.Confirm.AskAsync(new ConfirmRequest(
                Loc.T("Common.Uninstall"),
                Loc.F("Comp.ConfirmUninstall", Loc.T("App." + app.Id)),
                [app.PackageName],
                Loc.T("Common.Uninstall"),
                Danger: true)).ConfigureAwait(true);

            if (!confirmed)
            {
                return;
            }
        }

        row.IsActionEnabled = false;
        await ExecuteAsync(async ct =>
        {
            var result = install
                ? await _store.InstallAppAsync(app, ct).ConfigureAwait(true)
                : await _store.UninstallAppAsync(app, ct).ConfigureAwait(true);

            Report(result);

            if (result.Success)
            {
                await RefreshQuietlyAsync(ct).ConfigureAwait(true);
            }
        }).ConfigureAwait(true);
        row.IsActionEnabled = true;
    }

    private async Task InstallWslAsync(RowItem row, WslDistro distro)
    {
        row.IsActionEnabled = false;
        await ExecuteAsync(async ct =>
        {
            var result = await _store.InstallWslAsync(distro, ct).ConfigureAwait(true);
            Report(result);

            if (result.Success)
            {
                await RefreshQuietlyAsync(ct).ConfigureAwait(true);
            }
        }).ConfigureAwait(true);
        row.IsActionEnabled = true;
    }

    /// <summary>从本地 .msix/.appx 安装（安卓子系统第三方打包版走这里）。</summary>
    [RelayCommand]
    private async Task InstallWsaAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = Loc.T("Comp.PickPackage"),
            Filter = "Appx 包 (*.msix;*.appx;*.msixbundle)|*.msix;*.appx;*.msixbundle|所有文件 (*.*)|*.*",
            Multiselect = false
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        await ExecuteAsync(async ct =>
        {
            ShowProgress = true;
            LoadingText = Loc.T("Common.Installing");

            var result = await _store.InstallPackageFileAsync(dialog.FileName, ct).ConfigureAwait(true);
            Report(result);

            if (result.Success)
            {
                await RefreshQuietlyAsync(ct).ConfigureAwait(true);
            }
        }).ConfigureAwait(true);
    }

    /// <summary>修复所选：已安装的包重新注册清单。</summary>
    [RelayCommand]
    private async Task RepairSelectedAsync()
    {
        var targets = CheckedRows.Select(r => r.Tag).OfType<StoreApp>().Where(a => a.IsInstalled).ToList();
        if (targets.Count == 0)
        {
            UI.Toast.Warning(Loc.T("Common.NothingSelected"));
            return;
        }

        await ExecuteAsync(async ct =>
        {
            var ok = 0;
            var failed = 0;
            ShowProgress = true;

            for (var i = 0; i < targets.Count; i++)
            {
                ProgressPercent = (int)(i * 100d / targets.Count);
                var result = await _store.RepairAppAsync(targets[i], ct).ConfigureAwait(true);

                if (result.Success)
                {
                    ok++;
                }
                else
                {
                    failed++;
                }
            }

            await RefreshQuietlyAsync(ct).ConfigureAwait(true);
            Result = new ResultSummary(
                Loc.T("Comp.RepairDone"),
                failed == 0 ? ResultKind.Success : ok > 0 ? ResultKind.Warning : ResultKind.Error,
                [
                    new ResultMetric(Loc.T("Common.Succeeded"), ok.ToString()),
                    new ResultMetric(Loc.T("Common.Failed"), failed.ToString())
                ]);
        }).ConfigureAwait(true);
    }

    /// <summary>商店修复：重新注册 Store 包（等效于系统「应用商店疑难解答」）。</summary>
    [RelayCommand]
    private async Task RepairStoreAsync()
    {
        await ExecuteAsync(async ct =>
        {
            var result = await _store.ReRegisterAsync(ct).ConfigureAwait(true);
            Report(result);
            await RefreshQuietlyAsync(ct).ConfigureAwait(true);
        }).ConfigureAwait(true);
    }

    protected override async Task PrimaryCoreAsync(CancellationToken cancellationToken)
    {
        var targets = CheckedRows.Select(r => r.Tag).OfType<StoreApp>().Where(a => !a.IsInstalled).ToList();
        if (targets.Count == 0)
        {
            UI.Toast.Warning(Loc.T("Comp.NothingToInstall"));
            return;
        }

        await RunBatchAsync(targets, install: true, cancellationToken).ConfigureAwait(true);
    }

    protected override async Task SecondaryCoreAsync(CancellationToken cancellationToken)
    {
        var targets = CheckedRows.Select(r => r.Tag).OfType<StoreApp>().Where(a => a.IsInstalled).ToList();
        if (targets.Count == 0)
        {
            UI.Toast.Warning(Loc.T("Common.NothingSelected"));
            return;
        }

        var confirmed = await UI.Confirm.AskAsync(new ConfirmRequest(
            Loc.T("Common.Uninstall"),
            Loc.F("Comp.ConfirmUninstallCount", targets.Count),
            targets.Select(t => Loc.T("App." + t.Id)).ToList(),
            Loc.T("Common.Uninstall"),
            Danger: true)).ConfigureAwait(true);

        if (!confirmed)
        {
            return;
        }

        await RunBatchAsync(targets, install: false, cancellationToken).ConfigureAwait(true);
    }

    private async Task RunBatchAsync(List<StoreApp> targets, bool install, CancellationToken cancellationToken)
    {
        var ok = 0;
        var failed = 0;
        var restart = false;
        var watch = System.Diagnostics.Stopwatch.StartNew();

        ShowProgress = true;

        for (var i = 0; i < targets.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ProgressPercent = (int)(i * 100d / targets.Count);
            LoadingText = (install ? Loc.T("Common.Installing") : Loc.T("Common.Uninstalling")) + " · " + Loc.T("App." + targets[i].Id);

            var result = install
                ? await _store.InstallAppAsync(targets[i], cancellationToken).ConfigureAwait(true)
                : await _store.UninstallAppAsync(targets[i], cancellationToken).ConfigureAwait(true);

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
        await RefreshQuietlyAsync(cancellationToken).ConfigureAwait(true);

        Result = new ResultSummary(
            failed == 0 ? Loc.T("Common.Completed") : Loc.T("Common.ResultSummary"),
            failed == 0 ? ResultKind.Success : ok > 0 ? ResultKind.Warning : ResultKind.Error,
            [
                new ResultMetric(Loc.T("Common.Succeeded"), ok.ToString()),
                new ResultMetric(Loc.T("Common.Failed"), failed.ToString()),
                new ResultMetric(Loc.T("Common.Elapsed").Replace("{0}", string.Empty).Trim(), $"{watch.Elapsed.TotalSeconds:0.0}s")
            ],
            restart ? Loc.T("Common.RestartNeeded") : null);
    }
}
