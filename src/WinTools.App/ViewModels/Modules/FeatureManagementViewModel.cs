using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinTools.App.Localization;
using WinTools.App.Services;
using WinTools.Core.Abstractions;
using WinTools.Core.Models;

namespace WinTools.App.ViewModels.Modules;

/// <summary>功能管理：系统功能 + 版本渠道切换。</summary>
public sealed partial class FeatureManagementViewModel : ModuleViewModelBase
{
    public FeatureManagementViewModel(IModuleCatalog catalog, IFeatureService features)
        : base(catalog, "Features")
    {
        Register("SystemFeatures", new SystemFeaturesSectionViewModel(features));
        Register("EditionSwitch", new EditionSectionViewModel(features));
        SelectSection(Sections[0].Id);
    }
}

/// <summary>单个可选功能（一行 + 一个开关）。</summary>
public sealed partial class FeatureRowViewModel : ObservableObject
{
    public FeatureRowViewModel(OptionalFeature feature)
    {
        Name = feature.Name;
        IsInstalledState = feature.IsEnabled;
        Display = FeatureCatalog.Describe(feature.Name, out var description);
        Description = description;
        IsOn = feature.IsEnabled;
    }

    public string Name { get; }

    public string Display { get; }

    public string? Description { get; }

    public bool IsInstalledState { get; }

    [ObservableProperty]
    private bool _isOn;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isVisible = true;

    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    public bool HasChange => IsOn != IsInstalledState;

    partial void OnIsOnChanged(bool value)
    {
        OnPropertyChanged(nameof(HasChange));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? Changed;
}

/// <summary>功能分组（可折叠）。</summary>
public sealed partial class FeatureGroupViewModel : ObservableObject
{
    public FeatureGroupViewModel(string id, string title)
    {
        Id = id;
        Title = title;
    }

    public string Id { get; }

    public string Title { get; }

    public ObservableCollection<FeatureRowViewModel> Items { get; } = [];

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private string _summary = string.Empty;

    public int EnabledCount => Items.Count(i => i.IsInstalledState);

    public void RefreshSummary() => Summary = $"{EnabledCount}/{Items.Count}";

    public void SetAll(bool on)
    {
        foreach (var item in Items)
        {
            item.IsOn = on;
        }
    }
}

/// <summary>系统功能页签：分组折叠 + 开关 + 批量应用。工具栏与页面状态由 <see cref="SectionViewModelBase"/> 统一提供。</summary>
public sealed partial class SystemFeaturesSectionViewModel : SectionViewModelBase
{
    private readonly IFeatureService _features;

    public SystemFeaturesSectionViewModel(IFeatureService features)
    {
        _features = features;
        HasPrimary = true;
        PrimaryLabel = Loc.T("Common.Apply");
        ShowCheckboxes = true;
        _ = ReloadAsync();
    }

    public ObservableCollection<FeatureGroupViewModel> Groups { get; } = [];

    [ObservableProperty]
    private string _pendingText = string.Empty;

    [ObservableProperty]
    private int _totalCount;

    public bool HasPending => Groups.SelectMany(g => g.Items).Any(i => i.HasChange);

    public bool IsEmpty => TotalCount == 0;

    public int VisibleCount => Groups.Where(g => g.Items.Count > 0).Sum(g => g.Items.Count);

    public override bool CanPrimary => !IsBusy && HasPending;

    protected override bool HasData => TotalCount > 0;

    protected override async Task LoadAsync(CancellationToken cancellationToken)
    {
        var features = await _features.GetFeaturesAsync(cancellationToken).ConfigureAwait(true);
        Build(features);
    }
    private void Build(IReadOnlyList<OptionalFeature> features)
    {
        foreach (var group in Groups)
        {
            foreach (var item in group.Items)
            {
                item.Changed -= OnRowChanged;
            }
        }

        Groups.Clear();
        TotalCount = features.Count;

        foreach (var bucket in FeatureCatalog.Group(features))
        {
            var group = new FeatureGroupViewModel(bucket.Id, Loc.T("FeatGroup." + bucket.Id));

            foreach (var feature in bucket.Items.OrderBy(f => FeatureCatalog.Describe(f.Name, out _), StringComparer.CurrentCultureIgnoreCase))
            {
                var row = new FeatureRowViewModel(feature);
                row.Changed += OnRowChanged;
                group.Items.Add(row);
            }

            group.RefreshSummary();
            group.IsExpanded = Groups.Count == 0 && bucket.Items.Count > 0;
            Groups.Add(group);
        }

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(VisibleCount));
        UpdatePending();
    }



    private void OnRowChanged(object? sender, EventArgs e)
    {
        UpdatePending();
        RefreshAllGroups();
    }

    private void RefreshAllGroups()
    {
        foreach (var group in Groups)
        {
            group.RefreshSummary();
        }
    }

    private void UpdatePending()
    {
        var changed = Groups.SelectMany(g => g.Items).Count(i => i.HasChange);
        PendingText = changed == 0 ? string.Empty : Loc.F("Feat.PendingChanges", changed);
        CountText = TotalCount == 0
            ? string.Empty
            : changed == 0 ? TotalCount.ToString() : $"{PendingText} · {TotalCount}";

        OnPropertyChanged(nameof(HasPending));
        OnPropertyChanged(nameof(CanPrimary));
    }

    protected override void ApplyFilter(string text)
    {
        var keyword = text.Trim();
        var matched = 0;

        foreach (var group in Groups)
        {
            foreach (var item in group.Items)
            {
                item.IsVisible = keyword.Length == 0 ||
                                 item.Display.Contains(keyword, StringComparison.CurrentCultureIgnoreCase) ||
                                 item.Name.Contains(keyword, StringComparison.CurrentCultureIgnoreCase);

                if (item.IsVisible)
                {
                    matched++;
                }
            }

            group.RefreshSummary();

            if (keyword.Length > 0 && group.Items.Any(i => i.IsVisible))
            {
                group.IsExpanded = true;
            }
        }

        ShowNoMatch = TotalCount > 0 && matched == 0;
    }

    [RelayCommand]
    private void ToggleGroup(FeatureGroupViewModel? group)
    {
        if (group is null)
        {
            return;
        }

        group.IsExpanded = !group.IsExpanded;
    }

    [RelayCommand]
    private void ToggleGroupAll(FeatureGroupViewModel? group)
    {
        if (group is null)
        {
            return;
        }

        var target = group.Items.Any(i => !i.IsOn);
        group.SetAll(target);
        UpdatePending();
    }

    protected override void ToggleAllCore()
    {
        var all = Groups.SelectMany(g => g.Items).ToList();
        var target = all.Any(i => !i.IsOn);

        foreach (var item in all)
        {
            item.IsOn = target;
        }

        UpdatePending();
    }

    protected override async Task PrimaryCoreAsync(CancellationToken cancellationToken)
    {
        var changed = Groups.SelectMany(g => g.Items).Where(i => i.HasChange).ToList();
        if (changed.Count == 0)
        {
            return;
        }

        var confirmed = await UI.Confirm.AskAsync(new ConfirmRequest(
            Loc.T("Common.Apply"),
            Loc.F("Feat.ConfirmApply", changed.Count),
            changed.Select(c => (c.IsOn ? "+ " : "- ") + c.Display).ToList(),
            Loc.T("Common.Apply"))).ConfigureAwait(true);

        if (!confirmed)
        {
            return;
        }

        var done = 0;
        var failed = new List<string>();
        var restart = false;
        var watch = System.Diagnostics.Stopwatch.StartNew();

        ShowProgress = true;

        foreach (var row in changed)
        {
            cancellationToken.ThrowIfCancellationRequested();

            row.IsBusy = true;
            ProgressPercent = (int)((done + failed.Count) * 100d / changed.Count);
            Status = Loc.F("Feat.Applying", done + failed.Count + 1, changed.Count) + " · " + row.Display;

            var result = await _features.SetFeatureAsync(row.Name, row.IsOn, cancellationToken).ConfigureAwait(true);
            row.IsBusy = false;

            if (result.Success)
            {
                done++;
                restart |= result.RestartRequired;
            }
            else
            {
                failed.Add($"{row.Display}: {result.Message}");
            }
        }

        watch.Stop();
        Status = string.Empty;

        // 先按结果刷新状态，再展示摘要
        await RefreshQuietlyAsync(cancellationToken).ConfigureAwait(true);

        Result = new ResultSummary(
            failed.Count == 0 ? Loc.T("Common.Completed") : Loc.T("Common.ResultSummary"),
            failed.Count == 0 ? ResultKind.Success : done > 0 ? ResultKind.Warning : ResultKind.Error,
            [
                new ResultMetric(Loc.T("Common.Succeeded"), done.ToString()),
                new ResultMetric(Loc.T("Common.Failed"), failed.Count.ToString()),
                new ResultMetric(Loc.T("Common.Elapsed").Replace("{0}", string.Empty).Trim(), $"{watch.Elapsed.TotalSeconds:0.0}s")
            ],
            restart ? Loc.T("Common.RestartNeeded") : failed.FirstOrDefault());
    }

    public override void RefreshLocalization()
    {
        base.RefreshLocalization();
        OnPropertyChanged(string.Empty);
    }
}


/// <summary>可选功能的分组与友好名称（DISM 只给机器名，这里补人话）。</summary>
internal static class FeatureCatalog
{
    private static readonly (string Id, string[] Keywords)[] Rules =
    [
        ("Virtualization", ["Hyper-V", "VirtualMachine", "Containers", "HypervisorPlatform", "Sandbox", "Linux", "Windows-Sandbox"]),
        ("Server", ["Server", "IIS", "DirectoryServices", "Failover", "Clustering", "iSCSI", "DHCP", "DNS", "Web-Server", "SNMP", "SMTP", "NFS", "SMB1"]),
        ("Dev", ["NetFx", ".NET", "PowerShell", "DotNet", "WorkFolders", "WindowsPowerShell"]),
        ("Print", ["Print", "Fax", "XPS"]),
        ("Media", ["Media", "Photo", "Optical", "PlayReady", "MPEG", "Video", "Sound", "Player"]),
        ("Security", ["BitLocker", "Defender", "TPM", "Encryption", "Credential", "Hello"]),
        ("Network", ["Telnet", "TFTP", "SMB", "Network", "RIP", "DirectPlay", "WINS", "BranchCache", "Wireless", "WiFi", "VPN", "TCPIP"])
    ];

    private static readonly Dictionary<string, (string Name, string Description)> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Microsoft-Hyper-V"] = ("Hyper-V", "运行虚拟机、虚拟交换机与管理工具"),
        ["Microsoft-Hyper-V-All"] = ("Hyper-V 全部组件", "虚拟机平台与完整管理工具集"),
        ["Microsoft-Hyper-V-Hypervisor"] = ("Hyper-V 虚拟机监控程序", "虚拟化底层运行环境"),
        ["Containers"] = ("Windows 容器", "运行 Windows 容器镜像"),
        ["VirtualMachinePlatform"] = ("虚拟机平台", "为 WSL 2 与沙盒提供虚拟化基础"),
        ["Microsoft-Windows-Subsystem-Linux"] = ("适用于 Linux 的 Windows 子系统", "在 Windows 上直接运行 Linux 发行版"),
        ["Sandbox"] = ("Windows 沙盒", "临时独立的干净桌面环境"),
        ["NetFx3"] = (".NET Framework 3.5", "兼容老旧 .NET 程序与老游戏"),
        ["NetFx4-AdvSrvs"] = (".NET Framework 4 高级服务", "WCF、工作流等完整框架组件"),
        ["MicrosoftWindowsPowerShellV2Root"] = ("PowerShell 2.0 引擎", "老脚本需要的旧版运行环境"),
        ["Printing-Foundation-Features"] = ("打印基础功能", "打印服务与打印机管理"),
        ["Printing-XPSServices-Features"] = ("XPS 打印服务", "XPS 文档打印支持"),
        ["Printing-Foundation-InternetPrinting-Client"] = ("Internet 打印客户端", "通过 HTTP 使用网络打印机"),
        ["FaxService"] = ("传真服务", "收发传真（老设备/办公场景）"),
        ["TelnetClient"] = ("Telnet 客户端", "命令行远程登录老式设备"),
        ["TFTP"] = ("TFTP 客户端", "简单文件传输，常见于网络设备刷机"),
        ["SMB1Protocol"] = ("SMB 1.0 文件共享", "兼容老 NAS、老打印机，安全性较低"),
        ["Internet-Explorer-Optional-amd64"] = ("Internet Explorer 11", "老旧内网系统兼容模式"),
        ["WindowsMediaPlayer"] = ("Windows Media Player", "经典媒体播放器"),
        ["MediaPlayback"] = ("媒体功能", "系统内置音视频解码支持"),
        ["WorkFolders-Client"] = ("工作文件夹客户端", "企业工作文件夹同步"),
        ["IIS-WebServerRole"] = ("IIS Web 服务器", "作为 Web 服务器承载网站"),
        ["Microsoft-Defender-ApplicationGuard"] = ("Defender 应用程序防护", "在隔离容器中打开不受信任的站点"),
        ["MicrosoftWindowsClientFeatures"] = ("客户端功能包", "系统客户端组件集合")
    };

    public static List<(string Id, List<OptionalFeature> Items)> Group(IReadOnlyList<OptionalFeature> features)
    {
        var buckets = new Dictionary<string, List<OptionalFeature>>(StringComparer.OrdinalIgnoreCase);
        var order = new List<string>();

        foreach (var feature in features)
        {
            var id = Match(feature.Name);
            if (!buckets.TryGetValue(id, out var list))
            {
                list = [];
                buckets[id] = list;
                order.Add(id);
            }

            list.Add(feature);
        }

        if (buckets.TryGetValue("Other", out var other) && !order.Contains("Other"))
        {
            order.Add("Other");
        }

        if (other is not null && other.Count == 0)
        {
            buckets.Remove("Other");
            order.Remove("Other");
        }

        var sorted = order
            .OrderBy(id => id == "Other" ? 99 : Array.FindIndex(Rules, r => r.Id == id))
            .ToList();

        return sorted.Select(id => (id, buckets[id])).ToList();
    }

    public static string Describe(string name, out string? description)
    {
        if (Known.TryGetValue(name, out var info))
        {
            description = info.Description;
            return info.Name;
        }

        // 不做模糊匹配：Microsoft-Hyper-V-* 这类同族功能会被映射成同一个名字，
        // 列表里出现一堆「Hyper-V」反而更难看懂，未知项直接显示 DISM 原始名。
        description = null;
        return name;
    }

    private static string Match(string name)
    {
        foreach (var (id, keywords) in Rules)
        {
            if (keywords.Any(k => name.Contains(k, StringComparison.OrdinalIgnoreCase)))
            {
                return id;
            }
        }

        return "Other";
    }
}

/// <summary>版本渠道切换：读取 SKU/激活状态，输入产品密钥切换版本。</summary>
public sealed partial class EditionSectionViewModel : ListSectionViewModel
{
    private readonly IFeatureService _features;

    public EditionSectionViewModel(IFeatureService features)
    {
        _features = features;
        HasInput = true;
        InputLabel = Loc.T("Feat.ProductKey");
        InputActionLabel = Loc.T("Feat.ChangeEdition");
        HasPrimary = false;
        _ = ReloadAsync();
    }

    protected override async Task LoadAsync(CancellationToken cancellationToken)
    {
        var edition = await _features.GetEditionAsync(cancellationToken).ConfigureAwait(true);

        Rows.Clear();
        Rows.Add(new RowItem("product", Loc.T("Feat.Edition"), edition.ProductName, edition.EditionId));
        Rows.Add(new RowItem("version", Loc.T("Settings.Version"), edition.DisplayVersion, edition.Build));
        Rows.Add(new RowItem("type", Loc.T("Feat.InstallType"), edition.InstallationType));
        Rows.Add(new RowItem(
            "activation",
            Loc.T("Feat.Activation"),
            edition.IsActivated ? Loc.T("Feat.Activated") : Loc.T("Feat.NotActivated"),
            edition.PartialProductKey,
            edition.IsActivated ? Loc.T("Feat.Activated") : Loc.T("Feat.NotActivated"),
            edition.IsActivated ? RowTone.Success : RowTone.Warning));

        Status = string.Empty;
    }

    protected override async Task InputCoreAsync(CancellationToken cancellationToken)
    {
        var key = InputText.Trim();
        if (key.Length == 0)
        {
            Status = Loc.T("Feat.ProductKey");
            return;
        }

        var result = await _features.ChangeEditionAsync(key, cancellationToken).ConfigureAwait(true);
        Report(result);

        if (result.Success)
        {
            InputText = string.Empty;
        }
    }
}