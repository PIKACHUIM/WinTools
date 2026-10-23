using CommunityToolkit.Mvvm.Input;
using WinTools.App.Localization;
using WinTools.Core.Abstractions;

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

/// <summary>应用商店：健康检查、重置缓存、重新注册。</summary>
public sealed partial class StoreSectionViewModel : ListSectionViewModel
{
    private readonly IStoreService _store;

    public StoreSectionViewModel(IStoreService store)
    {
        _store = store;
        HasPrimary = true;
        PrimaryLabel = Loc.T("Store.ResetCache");
        HasSecondary = true;
        SecondaryLabel = Loc.T("Store.ReRegister");
        _ = ReloadAsync();
    }

    protected override async Task LoadAsync(CancellationToken cancellationToken)
    {
        var status = await _store.GetAsync(cancellationToken).ConfigureAwait(true);

        Rows.Clear();
        Rows.Add(new RowItem(
            "installed",
            Loc.T("Sec.Installed"),
            status.Installed ? Loc.T("Common.Done") : Loc.T("Common.Failed"),
            null,
            status.Installed ? Loc.T("Common.Done") : Loc.T("Common.Failed"),
            status.Installed ? RowTone.Success : RowTone.Danger));

        Rows.Add(new RowItem("version", Loc.T("Settings.Version"), status.Version ?? Loc.T("Common.None")));
        Rows.Add(StateRow("services", Loc.T("Store.Services"), status.ServiceRunning));
        Rows.Add(new RowItem("location", Loc.T("Store.Location"), status.InstallLocation ?? Loc.T("Common.None")));

        Status = status.Installed ? Loc.T("Store.Hint") : Loc.T("Common.NoData");
    }

    private static RowItem StateRow(string key, string title, bool ok) => new(
        key,
        title,
        ok ? Loc.T("Common.Enabled") : Loc.T("Common.Disabled"),
        null,
        ok ? Loc.T("Common.Enabled") : Loc.T("Common.Disabled"),
        ok ? RowTone.Success : RowTone.Warning);

    protected override async Task PrimaryCoreAsync(CancellationToken cancellationToken)
    {
        var result = await _store.ResetCacheAsync(cancellationToken).ConfigureAwait(true);
        Report(result);
        await LoadAsync(cancellationToken).ConfigureAwait(true);
    }

    protected override async Task SecondaryCoreAsync(CancellationToken cancellationToken)
    {
        var result = await _store.ReRegisterAsync(cancellationToken).ConfigureAwait(true);
        Report(result);
        await LoadAsync(cancellationToken).ConfigureAwait(true);
    }
}
