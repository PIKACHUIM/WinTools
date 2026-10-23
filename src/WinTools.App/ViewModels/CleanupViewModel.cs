using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinTools.App.Controls;
using WinTools.App.Localization;
using WinTools.App.Services;
using WinTools.Core.Abstractions;
using WinTools.Core.Models;
using WinTools.Core.Utils;
using WinTools.Infrastructure.System;

namespace WinTools.App.ViewModels;

/// <summary>清理页的五个阶段：待扫描 → 扫描中 → 已扫描 → 清理中 → 已完成。</summary>
public enum CleanupPhase
{
    Idle,
    Scanning,
    Ready,
    Cleaning,
    Done
}

/// <summary>清理条目：一行代表一条规则。</summary>
public sealed partial class CleanupItemViewModel : ObservableObject
{
    public CleanupItemViewModel(CleanupRuleResult result)
    {
        Result = result;
        Name = result.Rule.Name.Resolve(Loc.CurrentCulture);
        SizeText = SizeFormatter.Format(result.TotalSize);
        FileText = Loc.F("Cleanup.FileCount", result.TotalFiles);
        SafetyKey = "S" + (int)result.Rule.Safety;
        SafetyText = result.Rule.Safety switch
        {
            SafetyLevel.S1 => Loc.T("Common.Low"),
            SafetyLevel.S2 => Loc.T("Common.Medium"),
            _ => Loc.T("Common.High")
        };
        IsAdmin = result.Rule.RequiresAdmin;
        IsCleanable = result.Rule.IsCleanable;
        IsChecked = result.Rule.IsDefaultSelected;
    }

    public CleanupRuleResult Result { get; }

    public string Name { get; }

    public string SizeText { get; }

    public string FileText { get; }

    public string SafetyKey { get; }

    public string SafetyText { get; }

    public bool IsAdmin { get; }

    public bool IsCleanable { get; }

    public long SizeBytes => Result.TotalSize;

    [ObservableProperty]
    private bool _isChecked;

    partial void OnIsCheckedChanged(bool value) => CheckedChanged?.Invoke(this, EventArgs.Empty);

    public event EventHandler? CheckedChanged;
}

/// <summary>清理分组：一个分类下的所有条目。</summary>
public sealed partial class CleanupGroupViewModel : ObservableObject
{
    public CleanupGroupViewModel(CleanupCategory? category, string id)
    {
        Id = id;
        // 分类名统一走 i18n 的 Cat.<id>，规则文件里不再冗余存语言；缺失时退回 id
        var localized = Loc.T("Cat." + id);
        Title = string.IsNullOrWhiteSpace(localized) || localized.StartsWith("Cat.", StringComparison.Ordinal)
            ? id
            : localized;
        Glyph = category?.Glyph ?? "\uE8B7";
    }

    public string Id { get; }

    public string Title { get; }

    public string Glyph { get; }

    public ObservableCollection<CleanupItemViewModel> Items { get; } = [];

    [ObservableProperty]
    private bool _isExpanded = true;

    [ObservableProperty]
    private string _summary = string.Empty;

    [ObservableProperty]
    private bool? _isAllChecked;

    public long SizeBytes => Items.Sum(i => i.SizeBytes);

    public long CheckedBytes => Items.Where(i => i.IsChecked).Sum(i => i.SizeBytes);

    public void RefreshSummary()
    {
        Summary = $"{Items.Count(i => i.IsChecked)}/{Items.Count} · {SizeFormatter.Format(CheckedBytes)}";

        var checkedCount = Items.Count(i => i.IsChecked);
        IsAllChecked = checkedCount == 0 ? false : checkedCount == Items.Count ? true : null;
    }

    /// <summary>分组级三态：全选 / 全不选。</summary>
    public void ToggleAll()
    {
        var target = Items.Any(i => !i.IsChecked);
        foreach (var item in Items.Where(i => i.IsCleanable))
        {
            item.IsChecked = target;
        }
    }
}

/// <summary>垃圾清理：磁盘占用 → 扫描 → 勾选 → 清理 → 结果总结。</summary>
public sealed partial class CleanupViewModel : ObservableObject, ILocalizable
{
    private static readonly Color Accent = Color.FromRgb(0x0B, 0x6B, 0xCB);
    private static readonly Color AccentSoft = Color.FromRgb(0x3F, 0xA9, 0xF5);
    private static readonly Color Success = Color.FromRgb(0x2E, 0x9E, 0x5B);
    private static readonly Color TrackColor = Color.FromRgb(0xD8, 0xDF, 0xE9);

    private readonly ICleanupService _cleanup;
    private readonly HardwareInfoService _hardware;
    private readonly ISettingsService _settings;

    private long _scannedTotal;
    private long _selectedTotal;
    private double _lastChartPercent = -1;

    public CleanupViewModel(ICleanupService cleanup, HardwareInfoService hardware, ISettingsService settings)
    {
        _cleanup = cleanup;
        _hardware = hardware;
        _settings = settings;

        DiskSlices = BuildDiskSlices(0, 0);
        ScanSlices = BuildProgressSlices(0);
        OuterRing = [];
        InnerRing = [];

        _ = LoadVolumesAsync();
    }

    public ObservableCollection<CleanupGroupViewModel> Groups { get; } = [];

    public ObservableCollection<VolumeInfo> Volumes { get; } = [];

    // ------------------------------------------------------------------
    // 阶段
    // ------------------------------------------------------------------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdlePhase))]
    [NotifyPropertyChangedFor(nameof(IsScanningPhase))]
    [NotifyPropertyChangedFor(nameof(IsReadyPhase))]
    [NotifyPropertyChangedFor(nameof(IsCleaningPhase))]
    [NotifyPropertyChangedFor(nameof(IsDonePhase))]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    private CleanupPhase _phase = CleanupPhase.Idle;

    public bool IsIdlePhase => Phase == CleanupPhase.Idle;

    public bool IsScanningPhase => Phase == CleanupPhase.Scanning;

    public bool IsReadyPhase => Phase == CleanupPhase.Ready;

    public bool IsCleaningPhase => Phase == CleanupPhase.Cleaning;

    public bool IsDonePhase => Phase == CleanupPhase.Done;

    public bool IsBusy => Phase is CleanupPhase.Scanning or CleanupPhase.Cleaning;

    [ObservableProperty]
    private double _progressPercent;

    [ObservableProperty]
    private string _progressText = string.Empty;

    [ObservableProperty]
    private string _totalText = "0 B";

    [ObservableProperty]
    private string _selectedText = "0 B";

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private int _totalFiles;

    [ObservableProperty]
    private long _scanFoundBytes;

    [ObservableProperty]
    private string _scanFoundText = "0 B";

    // ------------------------------------------------------------------
    // 环形图数据
    // ------------------------------------------------------------------

    [ObservableProperty]
    private IReadOnlyList<DonutSlice> _diskSlices;

    [ObservableProperty]
    private IReadOnlyList<DonutSlice> _scanSlices;

    /// <summary>结果图外环：可清理 vs 其它已用。</summary>
    [ObservableProperty]
    private IReadOnlyList<DonutSlice> _outerRing;

    /// <summary>结果图内环：已勾选 vs 未勾选。</summary>
    [ObservableProperty]
    private IReadOnlyList<DonutSlice> _innerRing;

    [ObservableProperty]
    private string _diskCenterValue = "—";

    [ObservableProperty]
    private string _diskCenterLabel = string.Empty;

    [ObservableProperty]
    private string _resultCenterValue = "0 B";

    [ObservableProperty]
    private string _resultCenterLabel = string.Empty;

    public byte TrackR { get; } = TrackColor.R;

    public byte TrackG { get; } = TrackColor.G;

    public byte TrackB { get; } = TrackColor.B;

    // ------------------------------------------------------------------
    // 结果总结
    // ------------------------------------------------------------------

    [ObservableProperty]
    private string _resultFreedText = "0 B";

    [ObservableProperty]
    private string _resultFilesText = "0";

    [ObservableProperty]
    private string _resultSkippedText = "0";

    [ObservableProperty]
    private string _resultElapsedText = "0.0s";

    [ObservableProperty]
    private string _resultRemainingText = string.Empty;

    [ObservableProperty]
    private bool _hasRemaining;

    // ------------------------------------------------------------------
    // 文案
    // ------------------------------------------------------------------

    public string Title => Loc.T("Cleanup.Title");

    public string ScanLabel => Loc.T("Common.Scan");

    public string RescanLabel => Loc.T("Cleanup.Rescan");

    public string CleanLabel => Loc.T("Cleanup.Clean");

    public string SelectAllLabel => Loc.T("Common.SelectAll");

    public string DiskUsageLabel => Loc.T("Cleanup.DiskUsage");

    public string TotalLabel => Loc.T("Common.Total");

    public string SelectedLabel => Loc.T("Common.Selected");

    public string ScanIdleHint => Loc.T("Cleanup.ScanIdle");

    public string EmptyText => Loc.T("Cleanup.NothingFound");

    public string ScanningTitle => Loc.T("Cleanup.Scanning");

    public string CleaningTitle => Loc.T("Cleanup.Cleaning");

    public string DoneTitle => Loc.T("Cleanup.DoneTitle");

    public string ContinueLabel => Loc.T("Cleanup.Continue");

    public string FinishLabel => Loc.T("Common.Done");

    public string DeepScanLabel => Loc.T("Cleanup.DeepScan");

    public string FreedLabel => Loc.T("Cleanup.Freed");

    public string SkippedLabel => Loc.T("Common.Skipped");

    public string ElapsedLabel => Loc.T("Common.Elapsed").Replace("{0}", string.Empty).Trim();

    public string FilesLabel => Loc.T("Common.Files");

    public string SelectedOfTotal => Loc.T("Cleanup.SelectedOfTotal");

    // ------------------------------------------------------------------
    // 磁盘占用
    // ------------------------------------------------------------------

    private async Task LoadVolumesAsync()
    {
        var volumes = await Task.Run(_hardware.GetVolumes).ConfigureAwait(true);

        Volumes.Clear();
        foreach (var volume in volumes.Where(v => v.IsReady))
        {
            Volumes.Add(volume);
        }

        var used = Volumes.Sum(v => v.UsedBytes);
        var total = Volumes.Sum(v => v.TotalBytes);

        DiskSlices = BuildDiskSlices(used, Math.Max(0, total - used));
        DiskCenterValue = total > 0 ? $"{used * 100d / total:0}%" : "—";
        DiskCenterLabel = total > 0
            ? $"{SizeFormatter.Format(used)} / {SizeFormatter.Format(total)}"
            : string.Empty;
    }

    private static IReadOnlyList<DonutSlice> BuildDiskSlices(long used, long free)
        => used <= 0 && free <= 0
            ? []
            : [new DonutSlice(Math.Max(1, used), Accent), new DonutSlice(Math.Max(1, free), TrackColor)];

    private static IReadOnlyList<DonutSlice> BuildProgressSlices(double percent)
    {
        var done = Math.Clamp(percent, 0, 100);
        return
        [
            new DonutSlice(Math.Max(0.001, done), AccentSoft),
            new DonutSlice(Math.Max(0.001, 100 - done), TrackColor)
        ];
    }

    // ------------------------------------------------------------------
    // 扫描
    // ------------------------------------------------------------------

    [RelayCommand]
    private Task ScanAsync() => ScanCoreAsync(deep: false);

    /// <summary>深度扫描：把影音、开发、驱动残留等全部规则都跑一遍，耗时长。</summary>
    [RelayCommand]
    private Task ScanDeepAsync() => ScanCoreAsync(deep: true);

    private async Task ScanCoreAsync(bool deep)
    {
        if (IsBusy)
        {
            return;
        }

        Phase = CleanupPhase.Scanning;
        ProgressPercent = 0;
        ScanFoundBytes = 0;
        ScanFoundText = "0 B";
        ProgressText = Loc.T("Cleanup.Scanning");
        StatusText = string.Empty;
        ScanSlices = BuildProgressSlices(0);
        _lastChartPercent = -1;

        try
        {
            // 实测 1600+ 条规则全盘扫描不到 1 秒，不需要再做分档
            var ruleIds = _cleanup.Rules.Where(r => r.Enabled).Select(r => r.Id).ToList();

            var progress = new Progress<CleanupProgress>(p =>
            {
                ProgressPercent = p.Percent;
                ProgressText = string.IsNullOrWhiteSpace(p.CurrentPath) ? p.Stage : p.CurrentPath!;
                ScanFoundBytes = p.BytesSoFar;
                ScanFoundText = SizeFormatter.Format(p.BytesSoFar);

                // 图表每 5% 才重建一次，避免每帧都重绘
                if (Math.Abs(p.Percent - _lastChartPercent) >= 5 || p.Percent >= 100)
                {
                    _lastChartPercent = p.Percent;
                    ScanSlices = BuildProgressSlices(p.Percent);
                }
            });

            var summary = await _cleanup.ScanAsync(ruleIds, null, progress, CancellationToken.None)
                .ConfigureAwait(true);

            BuildGroups(summary);
            _scannedTotal = summary.TotalSize;
            TotalText = SizeFormatter.Format(summary.TotalSize);
            TotalFiles = (int)summary.TotalFiles;
            RecalculateSelected();

            if (Groups.Count == 0)
            {
                StatusText = Loc.T("Cleanup.NothingFound");
                Phase = CleanupPhase.Idle;
                UI.Toast.Info(StatusText);
            }
            else
            {
                Phase = CleanupPhase.Ready;
            }
        }
        catch (Exception ex)
        {
            StatusText = Loc.T("Common.Failed") + ": " + ex.Message;
            Phase = CleanupPhase.Idle;
            UI.Toast.Error(StatusText);
        }
        finally
        {
            ProgressText = string.Empty;
            ProgressPercent = 0;
            await LoadVolumesAsync().ConfigureAwait(true);
        }
    }

    private void BuildGroups(CleanupScanSummary summary)
    {
        foreach (var group in Groups)
        {
            foreach (var item in group.Items)
            {
                item.CheckedChanged -= OnItemCheckedChanged;
            }
        }

        Groups.Clear();

        var categories = _cleanup.Categories.ToDictionary(c => c.Id, c => c, StringComparer.OrdinalIgnoreCase);

        foreach (var group in summary.Rules
                     .Where(r => r.HasContent)
                     .GroupBy(r => r.Rule.Category, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(g => categories.TryGetValue(g.Key, out var c) ? c.Order : int.MaxValue))
        {
            categories.TryGetValue(group.Key, out var category);
            var vm = new CleanupGroupViewModel(category, group.Key);

            foreach (var result in group.OrderByDescending(r => r.TotalSize))
            {
                var item = new CleanupItemViewModel(result);
                item.CheckedChanged += OnItemCheckedChanged;
                vm.Items.Add(item);
            }

            vm.RefreshSummary();
            Groups.Add(vm);
        }
    }

    private void OnItemCheckedChanged(object? sender, EventArgs e)
    {
        RecalculateSelected();

        if (sender is CleanupItemViewModel item)
        {
            var owner = Groups.FirstOrDefault(g => g.Items.Contains(item));
            owner?.RefreshSummary();
        }
    }

    private void RecalculateSelected()
    {
        var items = Groups.SelectMany(g => g.Items).ToList();
        var selected = items.Where(i => i.IsChecked).Sum(i => i.SizeBytes);

        _selectedTotal = selected;
        SelectedText = SizeFormatter.Format(selected);
        ResultCenterValue = SizeFormatter.Format(selected);
        ResultCenterLabel = Loc.T("Cleanup.Releasable");

        var used = Volumes.Sum(v => v.UsedBytes);

        OuterRing =
        [
            new DonutSlice(Math.Max(1, _scannedTotal), AccentSoft),
            new DonutSlice(Math.Max(1, used - _scannedTotal), TrackColor)
        ];

        InnerRing =
        [
            new DonutSlice(Math.Max(1, selected), Success),
            new DonutSlice(Math.Max(1, _scannedTotal - selected), Color.FromRgb(0xC7, 0xD2, 0xE0))
        ];
    }

    [RelayCommand]
    private void ToggleGroup(CleanupGroupViewModel? group)
    {
        if (group is null)
        {
            return;
        }

        group.ToggleAll();
        group.RefreshSummary();
        RecalculateSelected();
    }

    [RelayCommand]
    private void SelectAll()
    {
        var target = Groups.SelectMany(g => g.Items).Any(i => !i.IsChecked);

        foreach (var group in Groups)
        {
            foreach (var item in group.Items.Where(i => i.IsCleanable))
            {
                item.IsChecked = target;
            }

            group.RefreshSummary();
        }

        RecalculateSelected();
    }

    [RelayCommand]
    private void ToggleExpanded(CleanupGroupViewModel? group)
    {
        if (group is not null)
        {
            group.IsExpanded = !group.IsExpanded;
        }
    }

    // ------------------------------------------------------------------
    // 清理
    // ------------------------------------------------------------------

    [RelayCommand]
    private async Task CleanAsync()
    {
        if (IsBusy || Phase != CleanupPhase.Ready)
        {
            return;
        }

        var selected = Groups.SelectMany(g => g.Items).Where(i => i.IsChecked && i.IsCleanable).ToList();
        if (selected.Count == 0)
        {
            UI.Toast.Warning(Loc.T("Common.NothingSelected"));
            return;
        }

        var confirmed = await UI.Confirm.AskAsync(new ConfirmRequest(
            Loc.T("Cleanup.Clean"),
            Loc.F("Cleanup.ConfirmClean", SizeFormatter.Format(selected.Sum(i => i.SizeBytes)), selected.Count),
            selected.Take(20).Select(i => $"{i.Name}  {i.SizeText}").ToList(),
            Loc.T("Cleanup.Clean"))).ConfigureAwait(true);

        if (!confirmed)
        {
            return;
        }

        Phase = CleanupPhase.Cleaning;
        ProgressPercent = 0;
        ProgressText = Loc.T("Cleanup.Cleaning");
        ScanSlices = BuildProgressSlices(0);
        _lastChartPercent = -1;

        try
        {
            var request = new CleanupRequest
            {
                Rules = selected.Select(i => i.Result).ToList(),
                UseRecycleBin = _settings.Current.UseRecycleBin,
                ScheduleLockedForReboot = true
            };

            var progress = new Progress<CleanupProgress>(p =>
            {
                ProgressPercent = p.Percent;
                ProgressText = p.CurrentPath ?? p.Stage;
                ScanFoundBytes = p.BytesSoFar;
                ScanFoundText = SizeFormatter.Format(p.BytesSoFar);

                if (Math.Abs(p.Percent - _lastChartPercent) >= 5 || p.Percent >= 100)
                {
                    _lastChartPercent = p.Percent;
                    ScanSlices = BuildProgressSlices(p.Percent);
                }
            });

            var report = await _cleanup.CleanAsync(request, progress, CancellationToken.None)
                .ConfigureAwait(true);

            ResultFreedText = SizeFormatter.Format(report.FreedBytes);
            ResultFilesText = report.DeletedFiles.ToString("N0");
            ResultSkippedText = report.SkippedItems.ToString("N0");
            ResultElapsedText = $"{report.Duration.TotalSeconds:0.0}s";

            // 再扫一遍，得到「还能继续清理多少」
            await ScanAsync().ConfigureAwait(true);

            ResultRemainingText = Groups.Count == 0
                ? string.Empty
                : Loc.F("Cleanup.Remaining",
                    SizeFormatter.Format(Groups.SelectMany(g => g.Items).Sum(i => i.SizeBytes)));

            HasRemaining = Groups.Count > 0;
            Phase = CleanupPhase.Done;
        }
        catch (Exception ex)
        {
            StatusText = Loc.T("Common.Failed") + ": " + ex.Message;
            UI.Toast.Error(StatusText);
            Phase = CleanupPhase.Ready;
        }
        finally
        {
            ProgressText = string.Empty;
            ProgressPercent = 0;
        }
    }

    /// <summary>结果页：继续处理剩余项。</summary>
    [RelayCommand]
    private void Continue()
    {
        ResultFreedText = "0 B";
        ResultFilesText = "0";
        ResultSkippedText = "0";
        ResultElapsedText = "0.0s";
        Phase = HasRemaining ? CleanupPhase.Ready : CleanupPhase.Idle;
    }

    /// <summary>结果页：完成，回到待扫描。</summary>
    [RelayCommand]
    private void Finish()
    {
        Groups.Clear();
        Phase = CleanupPhase.Idle;
        TotalText = "0 B";
        SelectedText = "0 B";
        ResultFreedText = "0 B";
        ResultFilesText = "0";
        ResultSkippedText = "0";
        ResultElapsedText = "0.0s";
        ResultRemainingText = string.Empty;
        HasRemaining = false;
        StatusText = string.Empty;
        _scannedTotal = 0;
        RecalculateSelected();
        _ = LoadVolumesAsync();
    }

    public void RefreshLocalization() => OnPropertyChanged(string.Empty);
}
