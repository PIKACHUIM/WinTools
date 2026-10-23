using CommunityToolkit.Mvvm.Input;
using WinTools.App.Localization;
using WinTools.Core.Abstractions;
using WinTools.Core.Models;
using WinTools.Core.Utils;

namespace WinTools.App.ViewModels.Modules;

/// <summary>应用管理：已安装程序 + Appx 应用。</summary>
public sealed partial class AppManagementViewModel : ModuleViewModelBase
{
    public AppManagementViewModel(IModuleCatalog catalog, IProgramService programs, IAppxService appx)
        : base(catalog, "Apps")
    {
        Register("Programs", new ProgramsSectionViewModel(programs));
        Register("Appx", new AppxSectionViewModel(appx));
        SelectSection(Sections[0].Id);
    }
}

/// <summary>已安装程序：读取 Uninstall 注册表键，支持静默/交互卸载。</summary>
public sealed partial class ProgramsSectionViewModel : ListSectionViewModel
{
    private readonly IProgramService _programs;

    public ProgramsSectionViewModel(IProgramService programs)
    {
        _programs = programs;
        _ = ReloadAsync();
    }

    protected override async Task LoadAsync(CancellationToken cancellationToken)
    {
        var programs = await _programs.GetAsync(cancellationToken).ConfigureAwait(true);

        Rows.Clear();
        foreach (var program in programs)
        {
            var detail = string.Join(" · ", new[]
            {
                program.Version,
                program.SizeBytes is null ? null : SizeFormatter.Format(program.SizeBytes.Value),
                program.InstallDate
            }.Where(s => !string.IsNullOrWhiteSpace(s)));

            var row = new RowItem(
                program.Scope + "|" + program.Id,
                program.Name,
                program.Publisher,
                detail,
                null,
                RowTone.Neutral)
            {
                Tag = program,
                SearchBlob = $"{program.Name} {program.Publisher} {program.Version}"
            };

            row.ShowAction = !string.IsNullOrWhiteSpace(program.UninstallString) ||
                             !string.IsNullOrWhiteSpace(program.QuietUninstallString);
            row.ActionLabel = Loc.T("Common.Uninstall");
            row.ActionCommand = new AsyncRelayCommand(() => UninstallAsync(row, program));
            Rows.Add(row);
        }

    }

    private async Task UninstallAsync(RowItem row, InstalledProgram program)
    {
        row.IsActionEnabled = false;
        await ExecuteAsync(async ct =>
        {
            var result = await _programs.UninstallAsync(program, ct).ConfigureAwait(true);
            Report(result);

            if (result.Success)
            {
                await LoadAsync(ct).ConfigureAwait(true);
            }
        }).ConfigureAwait(true);

        row.IsActionEnabled = true;
    }
}

/// <summary>Appx：通过 PowerShell 读取与移除。</summary>
public sealed partial class AppxSectionViewModel : ListSectionViewModel
{
    private readonly IAppxService _appx;

    public AppxSectionViewModel(IAppxService appx)
    {
        _appx = appx;
        _ = ReloadAsync();
    }

    protected override async Task LoadAsync(CancellationToken cancellationToken)
    {
        var packages = await _appx.GetAsync(cancellationToken).ConfigureAwait(true);

        Rows.Clear();
        foreach (var package in packages)
        {
            var row = new RowItem(
                package.PackageFullName ?? package.Name,
                package.Name,
                package.Publisher,
                package.Version,
                null,
                RowTone.Neutral)
            {
                Tag = package,
                SearchBlob = $"{package.Name} {package.Publisher} {package.Version}"
            };

            row.ShowAction = !string.IsNullOrWhiteSpace(package.PackageFullName);
            row.ActionLabel = Loc.T("Common.Remove");
            row.ActionCommand = new AsyncRelayCommand(() => RemoveAsync(row, package));
            Rows.Add(row);
        }

    }

    private async Task RemoveAsync(RowItem row, AppxPackageEntry package)
    {
        row.IsActionEnabled = false;
        await ExecuteAsync(async ct =>
        {
            var result = await _appx.RemoveAsync(package, ct).ConfigureAwait(true);
            Report(result);

            if (result.Success)
            {
                await LoadAsync(ct).ConfigureAwait(true);
            }
        }).ConfigureAwait(true);

        row.IsActionEnabled = true;
    }
}
