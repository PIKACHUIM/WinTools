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

    public bool IsEditable => !IsBusy;

    partial void OnIsOnChanged(bool value)
    {
        OnPropertyChanged(nameof(HasChange));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? Changed;
}

/// <summary>功能族：同一组件下的子功能（如 Hyper-V 的各个子项），父开关三态。</summary>
public sealed partial class FeatureFamilyViewModel : ObservableObject
{
    public FeatureFamilyViewModel(string title, bool isExplicit)
    {
        Title = title;
        IsExplicit = isExplicit;
    }

    public string Title { get; }

    /// <summary>是否由「已知功能族」匹配而来（否则是单条目，不显示父行）。</summary>
    public bool IsExplicit { get; }

    public ObservableCollection<FeatureRowViewModel> Items { get; } = [];

    [ObservableProperty]
    private bool _isExpanded = true;

    [ObservableProperty]
    private string _summary = string.Empty;

    public bool ShowHeader => IsExplicit || Items.Count > 1;

    public bool AllOn => Items.Count > 0 && Items.All(i => i.IsOn);

    public bool AnyOn => Items.Any(i => i.IsOn);

    /// <summary>父开关为「半开」：子项有开有关。</summary>
    public bool IsMixed => AnyOn && !AllOn;

    public void RefreshSummary()
    {
        Summary = $"{Items.Count(i => i.IsOn)}/{Items.Count}";
        OnPropertyChanged(nameof(AllOn));
        OnPropertyChanged(nameof(AnyOn));
        OnPropertyChanged(nameof(IsMixed));
    }

    public void SetAll(bool on)
    {
        foreach (var item in Items)
        {
            item.IsOn = on;
        }
    }
}

/// <summary>功能分组（用途分类，可折叠），内含若干功能族。</summary>
public sealed partial class FeatureGroupViewModel : ObservableObject
{
    public FeatureGroupViewModel(string id, string title)
    {
        Id = id;
        Title = title;
    }

    public string Id { get; }

    public string Title { get; }

    public ObservableCollection<FeatureFamilyViewModel> Families { get; } = [];

    /// <summary>展平后的全部条目，供筛选、批量应用与计数使用。</summary>
    public IEnumerable<FeatureRowViewModel> Items => Families.SelectMany(f => f.Items);

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private string _summary = string.Empty;

    public int EnabledCount => Items.Count(i => i.IsInstalledState);

    public void RefreshSummary() => Summary = $"{EnabledCount}/{Items.Count()}";

    public void SetAll(bool on)
    {
        foreach (var item in Items)
        {
            item.IsOn = on;
        }
    }
}

/// <summary>系统功能页签：用途分组 → 功能族 → 子功能，父开关三态、子项独立开关，统一应用。</summary>
public sealed partial class SystemFeaturesSectionViewModel : SectionViewModelBase
{
    private readonly IFeatureService _features;

    public SystemFeaturesSectionViewModel(IFeatureService features)
    {
        _features = features;
        HasPrimary = true;
        PrimaryLabel = Loc.T("Common.Apply");
        _ = ReloadAsync();
    }

    public ObservableCollection<FeatureGroupViewModel> Groups { get; } = [];

    public string MixedLabel => Loc.T("Feat.Mixed");

    [ObservableProperty]
    private string _pendingText = string.Empty;

    [ObservableProperty]
    private int _totalCount;

    public bool HasPending => Groups.SelectMany(g => g.Items).Any(i => i.HasChange);

    public bool IsEmpty => TotalCount == 0;

    public int VisibleCount => Groups.Sum(g => g.Items.Count(i => i.IsVisible));

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

            // 先按「功能族」聚类：Microsoft-Hyper-V-* 这类同族功能收进一个父节点
            var families = bucket.Items
                .GroupBy(f => FeatureFamilies.Resolve(f.Name) ?? f.Name, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => FeatureFamilies.Resolve(g.First().Name) is null ? 1 : 0)
                .ThenBy(g => FeatureCatalog.Describe(g.First().Name, out _), StringComparer.CurrentCultureIgnoreCase);

            foreach (var familyGroup in families)
            {
                var familyTitle = FeatureFamilies.Resolve(familyGroup.First().Name);
                var family = new FeatureFamilyViewModel(
                    familyTitle ?? FeatureCatalog.Describe(familyGroup.First().Name, out _),
                    familyTitle is not null);

                foreach (var feature in familyGroup.OrderBy(f => FeatureCatalog.Describe(f.Name, out _), StringComparer.CurrentCultureIgnoreCase))
                {
                    var row = new FeatureRowViewModel(feature);
                    row.Changed += OnRowChanged;
                    family.Items.Add(row);
                }

                family.RefreshSummary();
                group.Families.Add(family);
            }

            group.RefreshSummary();
            group.IsExpanded = Groups.Count == 0 && bucket.Items.Count > 0;
            Groups.Add(group);
        }

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(VisibleCount));
        UpdatePending();
    }

    /// <summary>级联带出/关闭期间忽略逐行事件，避免递归。</summary>
    private bool _cascade;

    private IEnumerable<FeatureRowViewModel> AllRows => Groups.SelectMany(g => g.Items);

    private async void OnRowChanged(object? sender, EventArgs e)
    {
        if (_cascade || sender is not FeatureRowViewModel row)
        {
            return;
        }

        if (row.IsOn)
        {
            var auto = new List<FeatureRowViewModel>();
            _cascade = true;
            try
            {
                auto = EnableDependencies([row]);
            }
            finally
            {
                _cascade = false;
            }

            if (auto.Count > 0)
            {
                UI.Toast.Info(Loc.F("Feat.AutoEnabled", auto.Count));
            }
        }
        else
        {
            var dependents = FindDependents([row]);
            if (dependents.Count > 0)
            {
                var confirmed = await UI.Confirm.AskAsync(new ConfirmRequest(
                    Loc.T("Feat.DependencyTitle"),
                    Loc.F("Feat.DependencyOff", row.Display, dependents.Count),
                    dependents.Select(d => d.Display).ToList(),
                    Loc.T("Feat.DisableAll"))).ConfigureAwait(true);

                _cascade = true;
                try
                {
                    if (confirmed)
                    {
                        foreach (var dependent in dependents)
                        {
                            dependent.IsOn = false;
                        }
                    }
                    else
                    {
                        row.IsOn = true;
                    }
                }
                finally
                {
                    _cascade = false;
                }
            }
        }

        RefreshAggregates();
    }

    private void RefreshAggregates()
    {
        foreach (var group in Groups)
        {
            foreach (var family in group.Families)
            {
                family.RefreshSummary();
            }

            group.RefreshSummary();
        }

        UpdatePending();
    }

    /// <summary>递归开启前置功能，返回被自动打开的行。</summary>
    private List<FeatureRowViewModel> EnableDependencies(IEnumerable<FeatureRowViewModel> seeds)
    {
        var enabled = new List<FeatureRowViewModel>();
        var queue = new Queue<FeatureRowViewModel>(seeds);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            foreach (var requirement in FeatureDependencies.RequirementsOf(current.Name))
            {
                foreach (var candidate in AllRows)
                {
                    if (candidate.IsOn || candidate.IsBusy ||
                        !FeatureDependencies.Provides(candidate.Name, requirement))
                    {
                        continue;
                    }

                    candidate.IsOn = true;
                    enabled.Add(candidate);
                    queue.Enqueue(candidate);
                }
            }
        }

        return enabled;
    }

    /// <summary>找出依赖这些行、且当前已开启的功能（关闭时需要一起处理）。</summary>
    private List<FeatureRowViewModel> FindDependents(IReadOnlyCollection<FeatureRowViewModel> offRows)
    {
        var names = offRows.Select(r => r.Name).ToList();

        return AllRows
            .Where(r => r.IsOn && !offRows.Contains(r))
            .Where(r => FeatureDependencies.RequirementsOf(r.Name)
                .Any(requirement => names.Any(name => FeatureDependencies.Provides(name, requirement))))
            .ToList();
    }

    private Task<bool> ConfirmDisableAsync(string subject, IReadOnlyList<FeatureRowViewModel> dependents)
        => UI.Confirm.AskAsync(new ConfirmRequest(
            Loc.T("Feat.DependencyTitle"),
            Loc.F("Feat.DependencyOff", subject, dependents.Count),
            dependents.Select(d => d.Display).ToList(),
            Loc.T("Feat.DisableAll")));

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
            foreach (var family in group.Families)
            {
                var anyVisible = false;

                foreach (var item in family.Items)
                {
                    item.IsVisible = keyword.Length == 0 ||
                                     item.Display.Contains(keyword, StringComparison.CurrentCultureIgnoreCase) ||
                                     item.Name.Contains(keyword, StringComparison.CurrentCultureIgnoreCase) ||
                                     family.Title.Contains(keyword, StringComparison.CurrentCultureIgnoreCase);

                    if (item.IsVisible)
                    {
                        matched++;
                        anyVisible = true;
                    }
                }

                if (keyword.Length > 0 && anyVisible)
                {
                    family.IsExpanded = true;
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

    /// <summary>父开关：整族开 / 关；开启时带出前置功能，关闭时提示会连带关闭谁。</summary>
    [RelayCommand]
    private async Task ToggleFamilyAsync(FeatureFamilyViewModel? family)
    {
        if (family is null)
        {
            return;
        }

        var turnOn = !family.AllOn;

        if (!turnOn)
        {
            var dependents = FindDependents(family.Items.Where(i => i.IsOn).ToList());
            if (dependents.Count > 0)
            {
                if (!await ConfirmDisableAsync(family.Title, dependents).ConfigureAwait(true))
                {
                    return;
                }

                _cascade = true;
                try
                {
                    foreach (var dependent in dependents)
                    {
                        dependent.IsOn = false;
                    }
                }
                finally
                {
                    _cascade = false;
                }
            }
        }

        var auto = new List<FeatureRowViewModel>();
        _cascade = true;
        try
        {
            family.SetAll(turnOn);
            if (turnOn)
            {
                auto = EnableDependencies(family.Items);
            }
        }
        finally
        {
            _cascade = false;
        }

        family.RefreshSummary();
        RefreshAggregates();

        if (auto.Count > 0)
        {
            UI.Toast.Info(Loc.F("Feat.AutoEnabled", auto.Count));
        }
    }

    [RelayCommand]
    private void ToggleFamilyExpand(FeatureFamilyViewModel? family)
    {
        if (family is not null)
        {
            family.IsExpanded = !family.IsExpanded;
        }
    }

    [RelayCommand]
    private async Task ToggleGroupAllAsync(FeatureGroupViewModel? group)
    {
        if (group is null)
        {
            return;
        }

        var target = group.Items.Any(i => !i.IsOn);

        if (!target)
        {
            var dependents = FindDependents(group.Items.Where(i => i.IsOn).ToList());
            if (dependents.Count > 0)
            {
                if (!await ConfirmDisableAsync(group.Title, dependents).ConfigureAwait(true))
                {
                    return;
                }

                _cascade = true;
                try
                {
                    foreach (var dependent in dependents)
                    {
                        dependent.IsOn = false;
                    }
                }
                finally
                {
                    _cascade = false;
                }
            }
        }

        var auto = new List<FeatureRowViewModel>();
        _cascade = true;
        try
        {
            group.SetAll(target);
            if (target)
            {
                auto = EnableDependencies(group.Items);
            }
        }
        finally
        {
            _cascade = false;
        }

        RefreshAggregates();

        if (auto.Count > 0)
        {
            UI.Toast.Info(Loc.F("Feat.AutoEnabled", auto.Count));
        }
    }

    protected override void ToggleAllCore()
    {
        var all = Groups.SelectMany(g => g.Items).ToList();
        var target = all.Any(i => !i.IsOn);

        _cascade = true;
        try
        {
            foreach (var item in all)
            {
                item.IsOn = target;
            }

            if (target)
            {
                EnableDependencies(all);
            }
        }
        finally
        {
            _cascade = false;
        }

        RefreshAggregates();
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

/// <summary>系统渠道：读取当前 SKU 与激活状态，下拉选择目标渠道并调用系统向导切换（不会激活）。</summary>
public sealed partial class EditionSectionViewModel : SectionViewModelBase
{
    private readonly IFeatureService _features;

    public EditionSectionViewModel(IFeatureService features)
    {
        _features = features;
        Editions = [.. EditionCatalog.All];
        HasPrimary = true;
        PrimaryLabel = Loc.T("Feat.ChangeEdition");
        _ = ReloadAsync();
    }

    public IReadOnlyList<EditionOption> Editions { get; }

    [ObservableProperty]
    private EditionOption? _selectedEdition;

    [ObservableProperty]
    private string _currentEdition = "—";

    [ObservableProperty]
    private string _currentVersion = "—";

    [ObservableProperty]
    private string _currentBuild = "—";

    [ObservableProperty]
    private string _activationText = "—";

    [ObservableProperty]
    private bool _isActivated;

    [ObservableProperty]
    private string _partialKey = string.Empty;

    [ObservableProperty]
    private string _selectedKey = string.Empty;

    public string EditionLabel => Loc.T("Feat.Edition");

    public string VersionLabel => Loc.T("Settings.Version");

    public string BuildLabel => Loc.T("Feat.Build");

    public string ActivationLabel => Loc.T("Feat.Activation");

    public string ProductKeyLabel => Loc.T("Feat.ProductKey");

    public string NoActivationHint => Loc.T("Feat.NoActivationHint");

    public string PreferEditionLabel => Loc.T("Feat.PreferEdition");

    public override bool CanPrimary => !IsBusy && SelectedEdition is not null;

    partial void OnSelectedEditionChanged(EditionOption? value)
        => SelectedKey = value?.Key ?? string.Empty;

    protected override async Task LoadAsync(CancellationToken cancellationToken)
    {
        var edition = await _features.GetEditionAsync(cancellationToken).ConfigureAwait(true);

        CurrentEdition = edition.ProductName;
        CurrentVersion = edition.DisplayVersion;
        CurrentBuild = edition.Build;
        IsActivated = edition.IsActivated;
        ActivationText = edition.IsActivated ? Loc.T("Feat.Activated") : Loc.T("Feat.NotActivated");
        PartialKey = edition.PartialProductKey ?? string.Empty;

        SelectedEdition ??= EditionCatalog.All.FirstOrDefault(e =>
            edition.EditionId.Contains(e.Id, StringComparison.OrdinalIgnoreCase)) ?? EditionCatalog.All[3];
    }

    protected override async Task PrimaryCoreAsync(CancellationToken cancellationToken)
    {
        if (SelectedEdition is not { } target)
        {
            return;
        }

        var confirmed = await UI.Confirm.AskAsync(new ConfirmRequest(
            Loc.T("Feat.ChangeEdition"),
            Loc.F("Feat.ChangeConfirm", Loc.T("Edition." + target.Id)),
            [Loc.T("Feat.NoActivationHint"), $"{Loc.T("Feat.ProductKey")}: {target.Key}"],
            Loc.T("Feat.ChangeEdition"),
            Danger: true,
            RequireAcknowledge: true)).ConfigureAwait(true);

        if (!confirmed)
        {
            return;
        }

        var result = await _features.ChangeEditionAsync(target.Key, cancellationToken).ConfigureAwait(true);
        Report(result);

        if (result.Success)
        {
            UI.Toast.Success(Loc.T("Feat.ChangeStarted"));
        }

        await RefreshQuietlyAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>功能族识别：把同族功能（如 Hyper-V 的各个子项）聚到同一个父节点下。</summary>
internal static class FeatureFamilies
{
    private static readonly (string Title, string[] Prefixes)[] Rules =
    [
        ("Hyper-V", ["Microsoft-Hyper-V", "Microsoft-Hyper-V-All"]),
        (".NET Framework", ["NetFx", "Microsoft-NetFx"]),
        ("IIS Web 服务器", ["IIS-", "Microsoft-Windows-IIS"]),
        ("打印与传真", ["Printing-", "Fax", "Print-"]),
        ("Linux 子系统", ["Microsoft-Windows-Subsystem-Linux"]),
        ("容器", ["Containers", "Microsoft-Hyper-V-Containers"]),
        ("媒体功能", ["Media-", "WindowsMediaPlayer", "Microsoft-Windows-Media"]),
        ("PowerShell", ["MicrosoftWindowsPowerShell", "Microsoft-Windows-PowerShell"]),
        ("SMB 文件共享", ["SMB1", "SMB-", "Microsoft-Windows-SMB"]),
        ("传统网络工具", ["TelnetClient", "TFTP", "SimpleTCP", "Microsoft-Windows-Telnet"]),
        ("工作文件夹", ["WorkFolders"]),
        ("旧版游戏组件", ["DirectPlay"]),
        ("备份", ["MicrosoftWindowsBackup", "WindowsBackup"]),
        ("Defender", ["Windows-Defender", "Microsoft-Windows-Defender"]),
        ("BitLocker", ["BitLocker"]),
        ("Windows Hello", ["MicrosoftWindowsHello", "Hello-Face", "Hello-Pin"]),
        ("远程协助", ["RemoteAssistance", "Remote-Desktop"]),
        ("SNMP", ["SNMP"]),
        ("NFS", ["NFS"]),
        ("无线网络", ["Wireless", "WiFi", "Wlan"]),
        ("虚拟化平台", ["HypervisorPlatform", "VirtualMachinePlatform"])
    ];

    /// <summary>返回功能所属族名；没有匹配返回 null（按单条处理）。</summary>
    public static string? Resolve(string name)
    {
        foreach (var (title, prefixes) in Rules)
        {
            if (prefixes.Any(p => name.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            {
                return title;
            }
        }

        return null;
    }
}

/// <summary>
/// 功能依赖表：开启左侧功能时自动勾选右侧前置项（前缀匹配，兼容不同版本的 DISM 名称）。
/// WSL 与沙盒都建立在虚拟机平台 / Hyper-V 监控程序之上，单独勾选它们通常会在应用时报错。
/// </summary>
internal static class FeatureDependencies
{
    private static readonly (string Feature, string[] Requires)[] Rules =
    [
        ("Microsoft-Windows-Subsystem-Linux", ["VirtualMachinePlatform", "Microsoft-Hyper-V-Hypervisor"]),
        ("VirtualMachinePlatform", ["Microsoft-Hyper-V-Hypervisor"]),
        ("Containers-DisposableClientVM", ["VirtualMachinePlatform", "Containers"]),
        ("Microsoft-Defender-ApplicationGuard", ["VirtualMachinePlatform", "Containers"]),
        ("Containers", ["Microsoft-Hyper-V-Hypervisor"]),
        ("Microsoft-Hyper-V-All", ["Microsoft-Hyper-V-Hypervisor"])
    ];

    /// <summary>该功能需要哪些前置功能（返回前缀）。</summary>
    public static IReadOnlyList<string> RequirementsOf(string name)
    {
        var requirements = new List<string>();

        foreach (var (feature, requires) in Rules)
        {
            if (name.StartsWith(feature, StringComparison.OrdinalIgnoreCase))
            {
                requirements.AddRange(requires);
            }
        }

        return requirements.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>功能名是否满足某个前置要求（双向前缀匹配）。</summary>
    public static bool Provides(string featureName, string requirement)
        => featureName.StartsWith(requirement, StringComparison.OrdinalIgnoreCase) ||
           requirement.StartsWith(featureName, StringComparison.OrdinalIgnoreCase);
}