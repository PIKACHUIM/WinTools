using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
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

    /// <summary>占用条的轨道宽度（固定值，避免绑定实际宽度导致布局反馈）。</summary>
    private const double BarTrackWidth = 300;
    public ObservableCollection<HardwareLine> SoundDevices { get; } = [];
    public ObservableCollection<NetworkLine> Networks { get; } = [];

    [ObservableProperty] private HardwareProfile? _profile;
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
    [ObservableProperty] private bool _isBusy;

    /// <summary>已完成采样次数：第一次只取基准值，界面要有第二次才有真实速率。</summary>
    public int SampleCount { get; private set; }

    // ---------- 静态文案 ----------
    public string Title => Loc.T("Dashboard.Title");
    public string HardwareSection => Loc.T("Dashboard.Hardware");
    public string RefreshLabel => Loc.T("Common.Refresh");
    public string CpuLabel => Loc.T("Dashboard.Cpu");
    public string MemoryLabel => Loc.T("Dashboard.Memory");
    public string DiskLabel => Loc.T("Dashboard.DiskUsage");
    public string GpuLabel => Loc.T("Dashboard.Gpu");
    public string NetworkSection => Loc.T("Dashboard.Network");
    public string SystemLabel => Loc.T("Dashboard.System");
    public string BoardLabel => Loc.T("Dashboard.Board");
    public string GraphicsLabel => Loc.T("Dashboard.Graphics");
    public string MemoryModuleLabel => Loc.T("Dashboard.MemoryModules");
    public string DiskLabel2 => Loc.T("Dashboard.Disks");
    public string VolumeLabel => Loc.T("Dashboard.Volume");
    public string SoundLabel => Loc.T("Dashboard.Sound");
    public string VersionLabel => Loc.T("Settings.Version");

    // ---------- 单值硬件信息 ----------
    public string OsText => Profile is null
        ? "—"
        : $"{Profile.OsName} {Profile.OsVersion}（Build {Profile.OsBuild}，{Profile.Architecture}）";

    public string OsDetailText => Profile is null
        ? string.Empty
        : Join(Profile.ComputerName, "BIOS", Join(Profile.BiosVendor, Profile.BiosVersion));

    public string BoardText => Profile is null ? "—" : Join(Profile.MotherboardVendor, Profile.MotherboardProduct);

    public string CpuText => Profile is null ? "—" : CleanName(Profile.CpuName);

    public string CpuDetailText => Profile is null
        ? string.Empty
        : Join(
            $"{Profile.PhysicalCores}C / {Profile.LogicalCores}T",
            Profile.CpuClockText,
            Profile.CpuSocket);

    public string MemorySummary => Profile is null
        ? "—"
        : SizeFormatter.Format(Profile.TotalMemoryBytes)
          + (Profile.MemoryModules.Count > 0 ? $"  ·  {Profile.MemoryModules.Count} 条" : string.Empty);

    public string DiskSummaryText => Profile is null || Profile.Disks.Count == 0
        ? "—"
        : $"{Profile.Disks.Count} 块";

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

        CpuSummary = ShortVendor(CleanName(profile.CpuName));
        GpuSummary = Graphics.Count > 0 ? ShortVendor(Graphics[0].Primary) : string.Empty;
        NetSummary = profile.NetworkAdapters.Count(a => a.IsConnected) is var count && count > 0
            ? Loc.F("Dashboard.NetworkOnline", count)
            : Loc.T("Dashboard.Disconnected");
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

    /// <summary>卡片副标题宽度有限：去掉厂商标识，只留型号。</summary>
    private static string ShortVendor(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        string[] prefixes = ["Intel Core", "Intel", "AMD Ryzen", "AMD Radeon", "AMD", "NVIDIA GeForce", "NVIDIA", "Qualcomm"];
        foreach (var prefix in prefixes)
        {
            if (name.StartsWith(prefix + " ", StringComparison.OrdinalIgnoreCase))
            {
                return name[(prefix.Length + 1)..].Trim();
            }
        }

        return name;
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

    public void RefreshLocalization()
    {
        OnPropertyChanged(string.Empty);
    }
}
