using CommunityToolkit.Mvvm.Input;
using WinTools.App.Localization;
using WinTools.Core.Abstractions;
using WinTools.Core.Models;

namespace WinTools.App.ViewModels.Modules;

/// <summary>驱动管理：查看 / 备份 / 安装 / 删除。</summary>
public sealed partial class DriverManagementViewModel : ModuleViewModelBase
{
    public DriverManagementViewModel(IModuleCatalog catalog, IDriverService drivers)
        : base(catalog, "Drivers")
    {
        Register("SystemDrivers", new SystemDriversSectionViewModel(drivers));
        Register("DriverBackup", new DriverBackupSectionViewModel(drivers));
        Register("DriverInstall", new DriverInstallSectionViewModel(drivers));
        Register("DriverRemove", new DriverRemoveSectionViewModel(drivers));
        SelectSection(Sections[0].Id);
    }
}

/// <summary>系统驱动：DriverStore 枚举（只读）。</summary>
public sealed partial class SystemDriversSectionViewModel : ListSectionViewModel
{
    private readonly IDriverService _drivers;

    public SystemDriversSectionViewModel(IDriverService drivers)
    {
        _drivers = drivers;
        _ = ReloadAsync();
    }

    protected override async Task LoadAsync(CancellationToken cancellationToken)
    {
        var drivers = await _drivers.GetAsync(cancellationToken).ConfigureAwait(true);

        Rows.Clear();
        foreach (var driver in drivers)
        {
            var detail = string.Join(" · ", new[] { driver.ClassName, driver.Version, driver.Date }
                .Where(s => !string.IsNullOrWhiteSpace(s)));

            Rows.Add(new RowItem(
                driver.PublishedName,
                driver.OriginalName,
                driver.Provider,
                detail,
                driver.PublishedName,
                RowTone.Neutral)
            {
                SearchBlob = $"{driver.PublishedName} {driver.OriginalName} {driver.Provider} {driver.ClassName}"
            });
        }

    }
}

/// <summary>备份驱动：导出到指定目录。</summary>
public sealed partial class DriverBackupSectionViewModel : ListSectionViewModel
{
    private readonly IDriverService _drivers;

    public DriverBackupSectionViewModel(IDriverService drivers)
    {
        _drivers = drivers;
        HasInput = true;
        InputLabel = Loc.T("Drv.Directory");
        HasPrimary = true;
        PrimaryLabel = Loc.T("Common.Export");
    }

    protected override async Task LoadAsync(CancellationToken cancellationToken)
    {
        var drivers = await _drivers.GetAsync(cancellationToken).ConfigureAwait(true);
        Rows.Clear();

        foreach (var group in drivers.GroupBy(d => d.ClassLabel() ?? "—").OrderBy(g => g.Key))
        {
            Rows.Add(new RowItem(
                group.Key,
                group.Key,
                null,
                Loc.F("Common.TotalCount", group.Count()),
                null,
                RowTone.Neutral));
        }

    }

    protected override async Task PrimaryCoreAsync(CancellationToken cancellationToken)
    {
        var target = InputText.Trim();
        if (target.Length == 0)
        {
            Status = Loc.T("Drv.Directory");
            return;
        }

        ShowProgress = true;
        ProgressPercent = 0;

        var progress = new Progress<string>(text => Status = text);
        var result = await _drivers.ExportAsync(target, progress, cancellationToken).ConfigureAwait(true);
        Report(result);
    }
}

/// <summary>安装驱动：从目录或 inf 文件安装。</summary>
public sealed partial class DriverInstallSectionViewModel : ListSectionViewModel
{
    private readonly IDriverService _drivers;

    public DriverInstallSectionViewModel(IDriverService drivers)
    {
        _drivers = drivers;
        HasInput = true;
        InputLabel = Loc.T("Drv.Path");
        HasPrimary = true;
        PrimaryLabel = Loc.T("Common.Install");
    }

    protected override Task LoadAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    protected override async Task PrimaryCoreAsync(CancellationToken cancellationToken)
    {
        var path = InputText.Trim();
        if (path.Length == 0)
        {
            Status = Loc.T("Drv.Path");
            return;
        }

        ShowProgress = true;
        ProgressPercent = 0;

        var result = await _drivers.InstallAsync(path, cancellationToken).ConfigureAwait(true);
        Report(result);
    }
}

/// <summary>删除驱动：逐项删除 DriverStore 包。</summary>
public sealed partial class DriverRemoveSectionViewModel : ListSectionViewModel
{
    private readonly IDriverService _drivers;

    public DriverRemoveSectionViewModel(IDriverService drivers)
    {
        _drivers = drivers;
        _ = ReloadAsync();
    }

    protected override async Task LoadAsync(CancellationToken cancellationToken)
    {
        var drivers = await _drivers.GetAsync(cancellationToken).ConfigureAwait(true);

        Rows.Clear();
        foreach (var driver in drivers)
        {
            var row = new RowItem(
                driver.PublishedName,
                driver.OriginalName,
                $"{driver.PublishedName} · {driver.Provider}",
                driver.Version,
                null,
                RowTone.Warning)
            {
                Tag = driver,
                SearchBlob = $"{driver.PublishedName} {driver.OriginalName}"
            };

            row.ShowAction = true;
            row.ActionLabel = Loc.T("Common.Delete");
            row.ActionCommand = new AsyncRelayCommand(() => RemoveAsync(row, driver));
            Rows.Add(row);
        }

    }

    private async Task RemoveAsync(RowItem row, DriverPackage driver)
    {
        row.IsActionEnabled = false;
        await ExecuteAsync(async ct =>
        {
            var result = await _drivers.RemoveAsync(driver, ct).ConfigureAwait(true);
            Report(result);

            if (result.Success)
            {
                await LoadAsync(ct).ConfigureAwait(true);
            }
        }).ConfigureAwait(true);

        row.IsActionEnabled = true;
    }
}

internal static class DriverPackageExtensions
{
    public static string? ClassLabel(this DriverPackage driver)
        => string.IsNullOrWhiteSpace(driver.ClassName) ? null : driver.ClassName;
}
