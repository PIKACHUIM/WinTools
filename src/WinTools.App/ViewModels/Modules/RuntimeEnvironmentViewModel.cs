using CommunityToolkit.Mvvm.Input;
using WinTools.App.Localization;
using WinTools.Core.Abstractions;
using WinTools.Core.Models;

namespace WinTools.App.ViewModels.Modules;

/// <summary>运行环境：.NET 与 VC++ 运行库的检测与卸载。</summary>
public sealed partial class RuntimeEnvironmentViewModel : ModuleViewModelBase
{
    public RuntimeEnvironmentViewModel(IModuleCatalog catalog, IRuntimeService runtimes)
        : base(catalog, "Runtimes")
    {
        Register("DotNet", new DotNetSectionViewModel(runtimes));
        Register("VcRuntime", new VcSectionViewModel(runtimes));
        SelectSection(Sections[0].Id);
    }
}

/// <summary>.NET 运行时：按主版本展示，卸载交由官方安装器处理。</summary>
public sealed partial class DotNetSectionViewModel : ListSectionViewModel
{
    private readonly IRuntimeService _runtimes;

    public DotNetSectionViewModel(IRuntimeService runtimes)
    {
        _runtimes = runtimes;
        _ = ReloadAsync();
    }

    protected override async Task LoadAsync(CancellationToken cancellationToken)
    {
        var runtimes = await _runtimes.GetDotNetAsync(cancellationToken).ConfigureAwait(true);

        Rows.Clear();
        foreach (var runtime in runtimes)
        {
            var major = runtime.Version.Split('.')[0];

            Rows.Add(new RowItem(
                $"{runtime.Name}|{runtime.Version}|{runtime.Architecture}",
                runtime.Name,
                runtime.Version,
                runtime.Architecture,
                ".NET " + major,
                RowTone.Accent)
            {
                SearchBlob = $"{runtime.Name} {runtime.Version} {runtime.Architecture}"
            });
        }

    }
}

/// <summary>VC++ 运行库：检测并支持静默卸载。</summary>
public sealed partial class VcSectionViewModel : ListSectionViewModel
{
    private readonly IRuntimeService _runtimes;

    public VcSectionViewModel(IRuntimeService runtimes)
    {
        _runtimes = runtimes;
        _ = ReloadAsync();
    }

    protected override async Task LoadAsync(CancellationToken cancellationToken)
    {
        var runtimes = await _runtimes.GetVcRuntimeAsync(cancellationToken).ConfigureAwait(true);

        Rows.Clear();
        foreach (var runtime in runtimes)
        {
            var row = new RowItem(
                $"{runtime.Name}|{runtime.Architecture}",
                runtime.Name,
                runtime.Version,
                runtime.Architecture,
                null,
                RowTone.Neutral)
            {
                Tag = runtime,
                SearchBlob = $"{runtime.Name} {runtime.Version}"
            };

            row.ShowAction = !string.IsNullOrWhiteSpace(runtime.UninstallString);
            row.ActionLabel = Loc.T("Common.Uninstall");
            row.ActionCommand = new AsyncRelayCommand(() => UninstallAsync(row, runtime));
            Rows.Add(row);
        }

    }

    private async Task UninstallAsync(RowItem row, InstalledRuntime runtime)
    {
        row.IsActionEnabled = false;
        await ExecuteAsync(async ct =>
        {
            var result = await _runtimes.UninstallAsync(runtime, ct).ConfigureAwait(true);
            Report(result);

            if (result.Success)
            {
                await LoadAsync(ct).ConfigureAwait(true);
            }
        }).ConfigureAwait(true);

        row.IsActionEnabled = true;
    }
}
