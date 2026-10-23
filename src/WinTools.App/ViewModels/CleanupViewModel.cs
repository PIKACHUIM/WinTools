using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinTools.App.Localization;
using WinTools.Core.Abstractions;
using WinTools.Core.Models;
using WinTools.Core.Utils;
using WinTools.Infrastructure.System;

namespace WinTools.App.ViewModels;

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
        IsAdmin = result.Rule.RequiresAdmin;
        IsCleanable = result.Rule.IsCleanable;
        IsChecked = result.Rule.IsDefaultSelected;
    }

    public CleanupRuleResult Result { get; }

    public string Name { get; }

    public string SizeText { get; }

    public string FileText { get; }

    public string SafetyKey { get; }

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
        Title = category is null ? id : category.Name.Resolve(Loc.CurrentCulture);
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

    public void RefreshSummary()
    {
        Summary = $"{Items.Count(i => i.IsChecked)}/{Items.Count} · {SizeFormatter.Format(Items.Where(i => i.IsChecked).Sum(i => i.SizeBytes))}";

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

/// <summary>垃圾清理：先扫描，再勾选，最后清理。</summary>
public sealed partial class CleanupViewModel : ObservableObject, ILocalizable
{
    private readonly ICleanupService _cleanup;
    private readonly HardwareInfoService _hardware;
    private readonly ISettingsService _settings;

    public CleanupViewModel(ICleanupService cleanup, HardwareInfoService hardware, ISettingsService settings)
    {
        _cleanup = cleanup;
        _hardware = hardware;
        _settings = settings;

        _ = LoadVolumesAsync();
    }

    public ObservableCollection<CleanupGroupViewModel> Groups { get; } = [];

    public ObservableCollection<VolumeInfo> Volumes { get; } = [];

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private bool _isCleaning;

    [ObservableProperty]
    private bool _hasScanned;

    [ObservableProperty]
    private bool _hasResult;

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

    public bool IsBusy => IsScanning || IsCleaning;

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

    private async Task LoadVolumesAsync()
    {
        var volumes = await Task.Run(_hardware.GetVolumes).ConfigureAwait(true);

        Volumes.Clear();
        foreach (var volume in volumes)
        {
            Volumes.Add(volume);
        }
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsScanning = true;
        HasScanned = true;
        HasResult = false;
        ProgressPercent = 0;
        StatusText = Loc.T("Cleanup.Scanning");

        try
        {
            var ruleIds = _cleanup.Rules.Where(r => r.Enabled).Select(r => r.Id).ToList();

            var progress = new Progress<CleanupProgress>(p =>
            {
                ProgressPercent = p.Percent;
                ProgressText = string.IsNullOrWhiteSpace(p.CurrentPath) ? p.Stage : p.CurrentPath!;
            });

            var summary = await _cleanup.ScanAsync(ruleIds, null, progress, CancellationToken.None)
                .ConfigureAwait(true);

            BuildGroups(summary);

            TotalText = SizeFormatter.Format(summary.TotalSize);
            TotalFiles = (int)summary.TotalFiles;
            HasResult = Groups.Count > 0;
            StatusText = HasResult ? string.Empty : Loc.T("Cleanup.NothingFound");
            RecalculateSelected();
        }
        catch (Exception ex)
        {
            StatusText = Loc.T("Common.Failed") + ": " + ex.Message;
        }
        finally
        {
            IsScanning = false;
            ProgressText = string.Empty;
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
        var selected = Groups.SelectMany(g => g.Items).Where(i => i.IsChecked).ToList();
        SelectedText = SizeFormatter.Format(selected.Sum(i => i.SizeBytes));
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

    [RelayCommand]
    private async Task CleanAsync()
    {
        if (IsBusy || !HasResult)
        {
            return;
        }

        var selected = Groups.SelectMany(g => g.Items).Where(i => i.IsChecked && i.IsCleanable).ToList();
        if (selected.Count == 0)
        {
            StatusText = Loc.T("Common.NothingSelected");
            return;
        }

        IsCleaning = true;
        ProgressPercent = 0;
        StatusText = Loc.T("Cleanup.Cleaning");

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
            });

            var report = await _cleanup.CleanAsync(request, progress, CancellationToken.None)
                .ConfigureAwait(true);

            StatusText = Loc.F("Cleanup.CleanDone", SizeFormatter.Format(report.FreedBytes), report.DeletedFiles);
            await ScanAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusText = Loc.T("Common.Failed") + ": " + ex.Message;
        }
        finally
        {
            IsCleaning = false;
            ProgressText = string.Empty;
        }
    }

    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(ScanLabel));
        OnPropertyChanged(nameof(RescanLabel));
        OnPropertyChanged(nameof(CleanLabel));
        OnPropertyChanged(nameof(SelectAllLabel));
        OnPropertyChanged(nameof(DiskUsageLabel));
        OnPropertyChanged(nameof(TotalLabel));
        OnPropertyChanged(nameof(SelectedLabel));
        OnPropertyChanged(nameof(ScanIdleHint));
        OnPropertyChanged(nameof(EmptyText));
    }
}
