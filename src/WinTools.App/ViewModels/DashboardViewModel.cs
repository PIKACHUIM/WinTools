using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinTools.App.Localization;
using WinTools.Core.Models;
using WinTools.Core.Utils;
using WinTools.Infrastructure.System;

namespace WinTools.App.ViewModels;

/// <summary>硬件信息中的一行（主值 + 可选副说明）。</summary>
public sealed record HardwareLine(string Primary, string? Secondary);

/// <summary>卡片顶部的小方阵：一个数值 + 一个说明。</summary>
public sealed record HardwareChip(string Value, string Label);

/// <summary>整机卡片里的一行：左侧标签、右侧值（可带补充说明）。</summary>
public sealed record SystemLine(string Label, string Value, string? Detail);

/// <summary>分区块：用固定宽度轨道 + 比例矩形绘制占用条（ProgressBar 放进 ItemsControl 会触发测量死循环）。</summary>
public sealed record VolumeRow(
    string Label,
    string SizeText,
    string PercentText,
    double BarWidth,
    bool IsCritical);

/// <summary>网卡一行：名称、状态、地址与速率。</summary>
public sealed record NetworkLine(
    string Name,
    bool IsConnected,
    string Status,
    string? Detail,
    string? Mac);

/// <summary>OEM 徽标：厂商字标 + 品牌色，Draw 出来的矢量图标，不依赖图片资源。</summary>
public sealed record OemBadge(string Monogram, string DisplayName, Brush Color, bool IsCustom);

/// <summary>系统概览：硬件档案 + 实时占用（仅在页面可见时采样）。</summary>
public sealed partial class DashboardViewModel : ObservableObject, ILocalizable
{
    private readonly HardwareInfoService _hardware;
    private readonly SystemMetricsService _metrics;
    private readonly ShellViewModel _shell;
    private readonly DispatcherTimer _timer;
    private bool _sampling;

    public DashboardViewModel(HardwareInfoService hardware, SystemMetricsService metrics, ShellViewModel shell)
    {
        _hardware = hardware;
        _metrics = metrics;
        _shell = shell;

        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _timer.Tick += async (_, _) => await SampleAsync().ConfigureAwait(true);

        _shell.CurrentPageChanged += (_, page) =>
        {
            if (ReferenceEquals(page, this))
            {
                StartSampling();
            }
            else
            {
                _timer.Stop();
            }
        };

        _ = ReloadAsync();
    }

    // ---------- 列表型硬件信息（每项一行，不再用 / 拼接）----------
    public ObservableCollection<HardwareLine> Graphics { get; } = [];
    public ObservableCollection<HardwareLine> MemoryModules { get; } = [];
    public ObservableCollection<HardwareLine> Disks { get; } = [];
    public ObservableCollection<VolumeRow> Volumes { get; } = [];
    public ObservableCollection<HardwareLine> SoundDevices { get; } = [];
    public ObservableCollection<NetworkLine> Networks { get; } = [];

    /// <summary>整机卡片里的键值行：计算机名 / 设备类型 / 系统 / 主板 / BIOS。</summary>
    public ObservableCollection<SystemLine> SystemLines { get; } = [];

    /// <summary>CPU 与内存卡片顶部的方阵。</summary>
    public ObservableCollection<HardwareChip> CpuChips { get; } = [];
    public ObservableCollection<HardwareChip> MemoryChips { get; } = [];

    /// <summary>骨架屏占位数量（6 张卡片）。</summary>
    public IReadOnlyList<int> SkeletonSlots { get; } = [0, 1, 2, 3, 4, 5];

    /// <summary>占用条的轨道宽度（固定值，避免绑定实际宽度导致布局反馈）。</summary>
    private const double BarTrackWidth = 300;

    [ObservableProperty] private HardwareProfile? _profile;
    [ObservableProperty] private OemBadge? _oem;
    [ObservableProperty] private double _cpuPercent;
    [ObservableProperty] private double _memoryPercent;
    [ObservableProperty] private double _diskPercent;
    [ObservableProperty] private double _diskUsagePercent;
    [ObservableProperty] private double _gpuPercent;
    [ObservableProperty] private double _netDown;
    [ObservableProperty] private double _netUp;
    [ObservableProperty] private string _memoryText = string.Empty;
    [ObservableProperty] private string _uptimeText = string.Empty;
    [ObservableProperty] private string _diskSummary = string.Empty;
    [ObservableProperty] private string _cpuSummary = string.Empty;
    [ObservableProperty] private string _gpuSummary = string.Empty;
    [ObservableProperty] private string _netSummary = string.Empty;
    [ObservableProperty] private string _soundSummary = string.Empty;
    [ObservableProperty] private string _modelText = string.Empty;
    [ObservableProperty] private string _headerSubtitle = string.Empty;
    [ObservableProperty] private bool _isBusy;

    /// <summary>是否已经拿到第一份硬件档案：没有时只显示骨架屏，避免首帧闪一下「无数据」。</summary>
    [ObservableProperty] private bool _hasProfile;

    /// <summary>已完成采样次数：第一次只取基准值，界面要有第二次才有真实速率。</summary>
    public int SampleCount { get; private set; }

    // ---------- 静态文案 ----------
    public string Title => Loc.T("Dashboard.Title");
    public string RefreshLabel => Loc.T("Common.Refresh");
    public string CopyHint => Loc.T("Common.Copy");
    public string CopyAllLabel => Loc.T("Common.CopyAll");

    public string DeviceSection => Loc.T("Dashboard.Device");
    public string DeviceTypeLabel => Loc.T("Dashboard.DeviceType");
    public string ComputerNameLabel => Loc.T("Dashboard.ComputerName");
    public string OsLabel => Loc.T("Dashboard.Os");
    public string SystemLabel => Loc.T("Dashboard.System");
    public string BoardLabel => Loc.T("Dashboard.Board");
    public string BiosLabel => Loc.T("Dashboard.Bios");
    public string CustomBuildLabel => Loc.T("Dashboard.CustomBuild");

    public string CpuLabel => Loc.T("Dashboard.Cpu");
    public string MemoryLabel => Loc.T("Dashboard.Memory");
    public string GraphicsLabel => Loc.T("Dashboard.Graphics");
    public string GpuLabel => Loc.T("Dashboard.Gpu");
    public string StorageLabel => Loc.T("Dashboard.Storage");
    public string NetworkSection => Loc.T("Dashboard.Network");
    public string SoundLabel => Loc.T("Dashboard.Sound");

    public string CoresLabel => Loc.T("Dashboard.Cores");
    public string ClockLabel => Loc.T("Dashboard.Clock");
    public string SocketLabel => Loc.T("Dashboard.Socket");
    public string CapacityLabel => Loc.T("Dashboard.Capacity");
    public string ModuleCountLabel => Loc.T("Dashboard.ModuleCountLabel");
    public string FrequencyLabel => Loc.T("Dashboard.Frequency");

    public string VolumeLabel => Loc.T("Dashboard.Volume");
    public string DiskLabel2 => Loc.T("Dashboard.Disks");
    public string PhysicalDisksLabel => Loc.T("Dashboard.PhysicalDisks");
    public string DownloadLabel => Loc.T("Dashboard.Download");
    public string UploadLabel => Loc.T("Dashboard.Upload");
    public string ReadingLabel => Loc.T("Dashboard.Reading");
    public string LiveUsageLabel => Loc.T("Dashboard.LiveUsage");
    public string VersionLabel => Loc.T("Settings.Version");

    // ---------- 单值硬件信息 ----------
    public string OsText => Profile is null ? string.Empty : Profile.OsName;

    public string OsDetailText => Profile is null
        ? string.Empty
        : Join($"Build {Profile.OsBuild}", Profile.Architecture);

    public string BoardText => Profile is null
        ? string.Empty
        : Join(Profile.MotherboardVendor, Profile.MotherboardProduct);

    public string CpuText => Profile is null ? string.Empty : CleanName(Profile.CpuName);

    public string CpuDetailText => Profile is null
        ? string.Empty
        : Join(
            $"{Profile.PhysicalCores}C / {Profile.LogicalCores}T",
            Profile.CpuClockText,
            Profile.CpuSocket);

    public string MemorySummary => Profile is null
        ? string.Empty
        : SizeFormatter.Format(Profile.TotalMemoryBytes)
          + (Profile.MemoryModules.Count > 0 ? $"  ·  {Profile.MemoryModules.Count} 条" : string.Empty);

    public string DeviceTypeText => Profile is null
        ? string.Empty
        : Profile.PcSystemType switch
        {
            1 => Loc.T("Dashboard.DeviceDesktop"),
            2 => Loc.T("Dashboard.DeviceLaptop"),
            3 => Loc.T("Dashboard.DeviceWorkstation"),
            _ => Loc.T("Dashboard.DeviceOther")
        };

    [RelayCommand]
    private Task RefreshAsync() => ReloadAsync();

    private async Task ReloadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var profile = await Task.Run(_hardware.GetProfile).ConfigureAwait(true);
            Profile = profile;
            FillLists(profile);
            DiskSummary = BuildDiskSummary(profile);

            var totalBytes = profile.Volumes.Sum(v => v.TotalBytes);
            DiskUsagePercent = totalBytes > 0 ? profile.Volumes.Sum(v => v.UsedBytes) * 100d / totalBytes : 0;

            HasProfile = true;
            OnPropertyChanged(string.Empty);
            await SampleAsync().ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void FillLists(HardwareProfile profile)
    {
        Oem = BuildBadge(profile);
        ModelText = CleanModel(profile.SystemModel) is { Length: > 0 } model
            ? model
            : Join(profile.MotherboardVendor, profile.MotherboardProduct);
        HeaderSubtitle = Join(profile.ComputerName, $"{profile.OsName} · Build {profile.OsBuild}");

        SystemLines.Clear();
        SystemLines.Add(new SystemLine(ComputerNameLabel, profile.ComputerName, null));
        SystemLines.Add(new SystemLine(DeviceTypeLabel, DeviceTypeText, CleanVendor(profile.SystemVendor)));
        SystemLines.Add(new SystemLine(OsLabel, OsText, OsDetailText));
        SystemLines.Add(new SystemLine(BoardLabel, BoardText, null));
        SystemLines.Add(new SystemLine(BiosLabel, Join(profile.BiosVendor, profile.BiosVersion), null));

        FillChips(profile);

        Graphics.Clear();
        foreach (var gpu in profile.Graphics)
        {
            Graphics.Add(new HardwareLine(CleanName(gpu.Name), Join(gpu.MemoryText, gpu.DriverVersion, gpu.Resolution)));
        }

        if (Graphics.Count == 0)
        {
            Graphics.Add(new HardwareLine(Loc.T("Common.None"), null));
        }

        MemoryModules.Clear();
        foreach (var module in profile.MemoryModules)
        {
            var speed = string.IsNullOrWhiteSpace(module.Speed) ? null : module.Speed;
            var slot = string.IsNullOrWhiteSpace(module.Slot) ? null : module.Slot;
            MemoryModules.Add(new HardwareLine(Join(SizeFormatter.Format(module.CapacityBytes), speed, slot), null));
        }

        Disks.Clear();
        foreach (var disk in profile.Disks)
        {
            Disks.Add(new HardwareLine(disk.Model, Join(SizeFormatter.Format(disk.SizeBytes), disk.InterfaceType)));
        }

        Volumes.Clear();
        foreach (var volume in profile.Volumes
                     .Where(v => v.IsReady)
                     .OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase))
        {
            Volumes.Add(new VolumeRow(
                string.IsNullOrWhiteSpace(volume.Label) ? volume.Name.TrimEnd('\\') : volume.Label,
                $"{SizeFormatter.Format(volume.UsedBytes)} / {SizeFormatter.Format(volume.TotalBytes)}",
                $"{volume.UsedPercent:0}%",
                Math.Round(BarTrackWidth * Math.Clamp(volume.UsedPercent, 0, 100) / 100d, 1),
                volume.UsedPercent >= 90));
        }

        SoundDevices.Clear();
        foreach (var device in profile.SoundDevices)
        {
            SoundDevices.Add(new HardwareLine(device, null));
        }

        SoundSummary = profile.SoundDevices.Count == 0
            ? Loc.T("Common.None")
            : string.Join("  ·  ", profile.SoundDevices);

        Networks.Clear();
        foreach (var adapter in profile.NetworkAdapters
                     .Where(a => a.IsConnected || !IsVirtualAdapter(a.Name))
                     .OrderByDescending(a => a.IsConnected)
                     .ThenBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            Networks.Add(new NetworkLine(
                adapter.Name,
                adapter.IsConnected,
                adapter.IsConnected ? Loc.T("Dashboard.Connected") : Loc.T("Dashboard.Disconnected"),
                Join(adapter.LinkSpeed, adapter.Ipv4),
                adapter.MacAddress));
        }

        CpuSummary = CpuText;
        GpuSummary = Graphics.Count > 0 ? Graphics[0].Primary : string.Empty;
        NetSummary = profile.NetworkAdapters.Count(a => a.IsConnected) is var count && count > 0
            ? Loc.F("Dashboard.NetworkOnline", count)
            : Loc.T("Dashboard.Disconnected");
    }

    private void FillChips(HardwareProfile profile)
    {
        CpuChips.Clear();
        CpuChips.Add(new HardwareChip($"{profile.PhysicalCores} / {profile.LogicalCores}", CoresLabel));
        if (!string.IsNullOrWhiteSpace(profile.CpuClockText))
        {
            CpuChips.Add(new HardwareChip(profile.CpuClockText!, ClockLabel));
        }

        if (!string.IsNullOrWhiteSpace(profile.CpuSocket))
        {
            CpuChips.Add(new HardwareChip(profile.CpuSocket!, SocketLabel));
        }

        MemoryChips.Clear();
        MemoryChips.Add(new HardwareChip(SizeFormatter.Format(profile.TotalMemoryBytes), CapacityLabel));
        if (profile.MemoryModules.Count > 0)
        {
            MemoryChips.Add(new HardwareChip(
                Loc.F("Dashboard.ModuleCount", profile.MemoryModules.Count),
                ModuleCountLabel));
        }

        var speed = profile.MemoryModules
            .Select(m => m.Speed)
            .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));
        if (speed is not null)
        {
            MemoryChips.Add(new HardwareChip(speed, FrequencyLabel));
        }
    }

    private void StartSampling()
    {
        if (!_timer.IsEnabled)
        {
            _timer.Start();
        }

        _ = SampleAsync();
    }

    private async Task SampleAsync()
    {
        if (_sampling)
        {
            return;
        }

        _sampling = true;
        try
        {
            var sample = await Task.Run(_metrics.Sample).ConfigureAwait(true);

            CpuPercent = sample.CpuPercent;
            MemoryPercent = sample.MemoryPercent;
            DiskPercent = sample.DiskActivePercent;
            GpuPercent = sample.GpuPercent;
            NetDown = sample.NetworkDownBytesPerSecond;
            NetUp = sample.NetworkUpBytesPerSecond;

            MemoryText = ShortSize(sample.MemoryUsedBytes, sample.MemoryTotalBytes);
            UptimeText = FormatUptime(TimeSpan.FromSeconds(sample.UptimeSeconds));
            SampleCount++;
        }
        finally
        {
            _sampling = false;
        }
    }

    /// <summary>去掉 (R)/(TM)/CPU @ 之类的冗长后缀，卡片里才放得下。</summary>
    private static string CleanName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "—";
        }

        var value = name
            .Replace("(R)", string.Empty)
            .Replace("(TM)", string.Empty)
            .Replace("(r)", string.Empty)
            .Replace("(tm)", string.Empty);

        value = Regex.Replace(value, @"\s*CPU\s*@.*$", string.Empty, RegexOptions.IgnoreCase);
        value = Regex.Replace(value, @"\s{2,}", " ").Trim();
        return value.Length == 0 ? name.Trim() : value;
    }

    /// <summary>虚拟网卡（VMware / Hyper-V / VPN 客户端等）不占列表，避免把真实网卡挤掉。</summary>
    private static bool IsVirtualAdapter(string name)
    {
        string[] keywords =
        [
            "VMware", "Virtual", "Hyper-V", "Loopback", "Bluetooth", "TAP-", "TUN",
            "VPN", "AnyConnect", "NGClient", "Sangfor", "Wintun", "WireGuard", "ZeroTier"
        ];

        return keywords.Any(k => name.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>「30.5 / 128 GB」：单位只出现一次，大于 100 时不留小数位。</summary>
    private static string ShortSize(long used, long total)
    {
        const double gb = 1024d * 1024 * 1024;
        const double tb = gb * 1024;

        static string Trim(double value, string format) => value >= 100 ? value.ToString("0") : value.ToString(format);

        return total >= tb
            ? $"{Trim(used / tb, "0.00")} / {Trim(total / tb, "0.00")} TB"
            : $"{Trim(used / gb, "0.0")} / {Trim(total / gb, "0.0")} GB";
    }

    private static string Join(params string?[] parts)
        => string.Join(" · ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));

    private static string FormatUptime(TimeSpan uptime)
    {
        if (uptime.TotalDays >= 1)
        {
            return $"{(int)uptime.TotalDays} 天 {uptime.Hours} 小时";
        }

        return uptime.TotalHours >= 1
            ? $"{(int)uptime.TotalHours} 小时 {uptime.Minutes} 分"
            : $"{uptime.Minutes} 分 {uptime.Seconds} 秒";
    }

    private static string BuildDiskSummary(HardwareProfile profile)
    {
        var total = profile.Volumes.Sum(v => v.TotalBytes);
        var used = profile.Volumes.Sum(v => v.UsedBytes);

        return total <= 0 ? string.Empty : ShortSize(used, total);
    }

    /// <summary>厂商串里的占位值（组装机常见）不算品牌。</summary>
    private static readonly string[] PlaceholderVendors =
    [
        "system manufacturer", "system product name", "to be filled", "default string",
        "oem", "not specified", "unknown", "none", "system version", "chassis manufacture"
    ];

    private static readonly (string Keyword, string Monogram, string Color)[] KnownVendors =
    [
        ("alienware", "ALIENWARE", "#FF1A1A1A"),
        ("dell", "DELL", "#FF0F6CBD"),
        ("lenovo", "LENOVO", "#FFC8102E"),
        ("thinkpad", "LENOVO", "#FFC8102E"),
        ("hewlett", "HP", "#FF0E8FD6"),
        ("hp", "HP", "#FF0E8FD6"),
        ("asustek", "ASUS", "#FF00539B"),
        ("asus", "ASUS", "#FF00539B"),
        ("micro-star", "MSI", "#FFB02418"),
        ("msi", "MSI", "#FFB02418"),
        ("gigabyte", "GIGABYTE", "#FFC05621"),
        ("asrock", "ASRock", "#FF2E5E9E"),
        ("acer", "Acer", "#FF3F8F1F"),
        ("microsoft", "Microsoft", "#FF0067B8"),
        ("huawei", "HUAWEI", "#FFB3122C"),
        ("honor", "HONOR", "#FF0A6FBF"),
        ("xiaomi", "Xiaomi", "#FFD9480F"),
        ("razer", "RAZER", "#FF2E7D32"),
        ("samsung", "SAMSUNG", "#FF1428A0"),
        ("toshiba", "TOSHIBA", "#FFC8102E"),
        ("dynabook", "dynabook", "#FF00838F"),
        ("fujitsu", "FUJITSU", "#FFC8102E"),
        ("sony", "SONY", "#FF2B2B2B"),
        ("panasonic", "Panasonic", "#FF1B3C8C"),
        ("intel", "INTEL", "#FF0068B5"),
        ("supermicro", "Supermicro", "#FF1F3864"),
        ("biostar", "BIOSTAR", "#FF1F6F3E"),
        ("colorful", "COLORFUL", "#FFB3122C"),
        ("maxsun", "MAXSUN", "#FF1B5E9E"),
        ("hasee", "Hasee", "#FFC8102E"),
        ("mechrevo", "MECHREVO", "#FFB3122C"),
        ("haier", "Haier", "#FF0F4C81"),
        ("framework", "FRAMEWORK", "#FF2B2B2B"),
        ("system76", "System76", "#FF37474F"),
        ("chuwi", "CHUWI", "#FF1565C0"),
        ("teclast", "TECLAST", "#FF0D47A1"),
        ("vmware", "VMware", "#FF607078"),
        ("qemu", "QEMU", "#FF6B4A2F"),
        ("parallels", "Parallels", "#FF1F5FA8"),
        ("innotek", "VirtualBox", "#FF2E5E9E"),
        ("amazon", "AWS", "#FF232F3E"),
        ("google", "Google", "#FF1A73E8")
    ];

    private OemBadge BuildBadge(HardwareProfile profile)
    {
        var vendor = CleanVendor(profile.SystemVendor);
        if (vendor.Length == 0)
        {
            vendor = CleanVendor(profile.MotherboardVendor);
        }

        if (vendor.Length == 0)
        {
            return new OemBadge("PC", CustomBuildLabel, BrushFrom("#FF0F6CBD"), true);
        }

        foreach (var (keyword, monogram, color) in KnownVendors)
        {
            if (vendor.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return new OemBadge(monogram, vendor, BrushFrom(color), false);
            }
        }

        var firstWord = vendor.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        var label = firstWord.Length <= 8 ? firstWord.ToUpperInvariant() : firstWord[..8].ToUpperInvariant();
        return new OemBadge(label, vendor, BrushFrom("#FF4B5563"), false);
    }

    /// <summary>去掉「Inc.」「Co., Ltd.」之类尾巴，卡片抬头只留品牌名。</summary>
    private static string CleanVendor(string? vendor)
    {
        if (string.IsNullOrWhiteSpace(vendor))
        {
            return string.Empty;
        }

        var value = vendor.Trim();
        if (PlaceholderVendors.Any(p => value.Contains(p, StringComparison.OrdinalIgnoreCase)))
        {
            return string.Empty;
        }

        string[] suffixes = [" Corporation", " Corporaton", " Inc.", " Inc", " Co., Ltd.", " Co.,Ltd.", " Ltd.", " GmbH", " Technology", " Computer"];
        foreach (var suffix in suffixes)
        {
            if (value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                value = value[..^suffix.Length].Trim();
            }
        }

        return value.TrimEnd(',', '.', ' ');
    }

    private static string CleanModel(string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return string.Empty;
        }

        var value = model.Trim();
        if (PlaceholderVendors.Any(p => value.Contains(p, StringComparison.OrdinalIgnoreCase)))
        {
            return string.Empty;
        }

        return value;
    }

    private static Brush BrushFrom(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }

    public void RefreshLocalization()
    {
        OnPropertyChanged(string.Empty);
    }
}
