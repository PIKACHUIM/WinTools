using CommunityToolkit.Mvvm.Input;
using WinTools.App.Localization;
using WinTools.App.Services;
using WinTools.Core.Models;

namespace WinTools.App.ViewModels.Modules;

/// <summary>.NET 运行时：在线版本清单与安装。</summary>
public sealed partial class DotNetSectionViewModel
{
    protected override void OnLoaded()
    {
        base.OnLoaded();

        if (ExtraActions.Count == 0)
        {
            ExtraActions.Add(new ToolbarAction(Loc.T("Rt.CheckAvailable"), LoadAvailableCommand));
        }
    }

    [RelayCommand]
    private async Task LoadAvailableAsync()
    {
        await ExecuteAsync(async ct =>
        {
            LoadingText = Loc.T("Rt.Fetching");
            ShowProgress = true;

            var packages = await _runtimes.GetAvailableAsync("dotnet", ct).ConfigureAwait(true);

            if (packages.Count == 0)
            {
                UI.Toast.Warning(Loc.T("Rt.NoNetwork"));
                return;
            }

            foreach (var package in packages)
            {
                var row = new RowItem(
                    "pkg:" + package.Id,
                    package.DisplayName,
                    Loc.T("Rt.Available"),
                    Loc.F("Rt.Version", package.Version),
                    package.Architecture,
                    RowTone.Accent)
                {
                    Tag = package,
                    SearchBlob = $"{package.DisplayName} {package.Version} {package.Architecture}"
                };

                row.ShowAction = true;
                row.ActionLabel = Loc.T("Common.Install");
                row.ActionCommand = new AsyncRelayCommand(() => InstallAsync(row, package));

                // 插到已安装列表前面，方便直接看到能装什么
                Rows.Insert(0, row);
            }

            UI.Toast.Success(Loc.F("Common.TotalCount", packages.Count));
        }).ConfigureAwait(true);
    }

    private async Task InstallAsync(RowItem row, RuntimePackage package)
    {
        row.IsActionEnabled = false;
        await ExecuteAsync(async ct =>
        {
            ShowProgress = true;
            LoadingText = Loc.F("Rt.Downloading", package.DisplayName);

            var progress = new Progress<double>(p =>
            {
                ProgressPercent = (int)Math.Clamp(p, 0, 100);
                if (p >= 95)
                {
                    LoadingText = Loc.T("Rt.Installing");
                }
            });

            var result = await _runtimes.InstallAsync(package, progress, ct).ConfigureAwait(true);
            Report(result);

            if (result.Success)
            {
                await RefreshQuietlyAsync(ct).ConfigureAwait(true);
            }
        }).ConfigureAwait(true);
        row.IsActionEnabled = true;
    }
}

/// <summary>VC++ 运行库：在线版本清单与安装。</summary>
public sealed partial class VcSectionViewModel
{
    protected override void OnLoaded()
    {
        base.OnLoaded();

        if (ExtraActions.Count == 0)
        {
            ExtraActions.Add(new ToolbarAction(Loc.T("Rt.CheckAvailable"), LoadAvailableCommand));
        }
    }

    [RelayCommand]
    private async Task LoadAvailableAsync()
    {
        await ExecuteAsync(async ct =>
        {
            LoadingText = Loc.T("Rt.Fetching");
            ShowProgress = true;

            var packages = await _runtimes.GetAvailableAsync("vcpp", ct).ConfigureAwait(true);

            foreach (var package in packages)
            {
                var row = new RowItem(
                    "pkg:" + package.Id,
                    package.DisplayName,
                    Loc.T("Rt.Available"),
                    Loc.F("Rt.Version", package.Version),
                    package.Architecture,
                    RowTone.Accent)
                {
                    Tag = package,
                    SearchBlob = $"{package.DisplayName} {package.Architecture}"
                };

                row.ShowAction = true;
                row.ActionLabel = Loc.T("Common.Install");
                row.ActionCommand = new AsyncRelayCommand(() => InstallAsync(row, package));

                Rows.Insert(0, row);
            }

            UI.Toast.Success(Loc.F("Common.TotalCount", packages.Count));
        }).ConfigureAwait(true);
    }

    private async Task InstallAsync(RowItem row, RuntimePackage package)
    {
        row.IsActionEnabled = false;
        await ExecuteAsync(async ct =>
        {
            ShowProgress = true;
            LoadingText = Loc.F("Rt.Downloading", package.DisplayName);

            var progress = new Progress<double>(p =>
            {
                ProgressPercent = (int)Math.Clamp(p, 0, 100);
                if (p >= 95)
                {
                    LoadingText = Loc.T("Rt.Installing");
                }
            });

            var result = await _runtimes.InstallAsync(package, progress, ct).ConfigureAwait(true);
            Report(result);

            if (result.Success)
            {
                await RefreshQuietlyAsync(ct).ConfigureAwait(true);
            }
        }).ConfigureAwait(true);
        row.IsActionEnabled = true;
    }
}
