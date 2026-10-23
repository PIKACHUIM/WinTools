using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinTools.App.Localization;
using WinTools.Core.Abstractions;
using WinTools.Core.Models;

namespace WinTools.App.ViewModels;

/// <summary>一条优化项。</summary>
public sealed partial class TweakRowViewModel : ObservableObject
{
    public TweakRowViewModel(TweakState state)
    {
        State = state;
        Name = state.Definition.Name.Resolve(Loc.CurrentCulture);
        Description = state.Definition.Description.Resolve(Loc.CurrentCulture);
        RiskKey = state.Definition.Risk switch
        {
            TweakRisk.Low => "S1",
            TweakRisk.Medium => "S2",
            _ => "S3"
        };
        RiskText = state.Definition.Risk switch
        {
            TweakRisk.Low => Loc.T("Risk.Low"),
            TweakRisk.Medium => Loc.T("Risk.Medium"),
            _ => Loc.T("Risk.High")
        };
        IsApplied = state.IsApplied;
        IsRecommended = state.Definition.Recommended;
        IsChecked = state.IsApplied;
        RequiresRestart = state.Definition.RequiresRestart;
    }

    public TweakState State { get; }

    public string Id => State.Definition.Id;

    public string Name { get; }

    public string Description { get; }

    public string RiskKey { get; }

    public string RiskText { get; }

    public bool IsApplied { get; }

    public bool IsRecommended { get; }

    public bool RequiresRestart { get; }

    [ObservableProperty]
    private bool _isChecked;

    [ObservableProperty]
    private bool _isBusy;

    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    public bool HasChange => IsChecked != IsApplied;

    partial void OnIsCheckedChanged(bool value)
    {
        OnPropertyChanged(nameof(HasChange));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? Changed;
}

/// <summary>优化分类（可折叠 + 全选）。</summary>
public sealed partial class TweakGroupViewModel : ObservableObject
{
    public TweakGroupViewModel(string id, string title, string glyph)
    {
        Id = id;
        Title = title;
        Glyph = glyph;
    }

    public string Id { get; }

    public string Title { get; }

    public string Glyph { get; }

    public ObservableCollection<TweakRowViewModel> Items { get; } = [];

    [ObservableProperty]
    private bool _isExpanded = true;

    [ObservableProperty]
    private string _summary = string.Empty;

    public void RefreshSummary() => Summary = $"{Items.Count(i => i.IsApplied)}/{Items.Count}";

    public void SetAll(bool value)
    {
        foreach (var item in Items)
        {
            item.IsChecked = value;
        }
    }
}

/// <summary>系统优化：分类勾选 → 一键应用 / 还原。</summary>
public sealed partial class OptimizeViewModel : ObservableObject, ILocalizable
{
    private readonly ITweakService _tweaks;

    public OptimizeViewModel(ITweakService tweaks)
    {
        _tweaks = tweaks;
        _ = DetectAsync();
    }

    public ObservableCollection<TweakGroupViewModel> Groups { get; } = [];

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private string _pendingText = string.Empty;

    [ObservableProperty]
    private string _filter = string.Empty;

    public string Title => Loc.T("Optimize.Title");

    public string RefreshLabel => Loc.T("Common.Refresh");

    public string ApplyLabel => Loc.T("Common.Apply");

    public string RevertLabel => Loc.T("Common.Revert");

    public string SelectAllLabel => Loc.T("Common.SelectAll");

    public string RecommendedLabel => Loc.T("Optimize.Recommended");

    public string SearchHint => Loc.T("Common.Search");

    public string AppliedLabel => Loc.T("Optimize.Applied");

    public int TotalCount => Groups.Sum(g => g.Items.Count);

    public bool IsEmpty => TotalCount == 0;

    public bool HasPending => Groups.SelectMany(g => g.Items).Any(i => i.HasChange);

    partial void OnFilterChanged(string value)
    {
        var keyword = value.Trim();

        foreach (var group in Groups)
        {
            foreach (var item in group.Items)
            {
                item.IsChecked = item.IsChecked;
            }

            group.RefreshSummary();
        }

        ApplyFilter(keyword);
    }

    private void ApplyFilter(string keyword)
    {
        foreach (var group in Groups)
        {
            foreach (var item in group.Items)
            {
                item.IsBusy = keyword.Length > 0 &&
                              !item.Name.Contains(keyword, StringComparison.CurrentCultureIgnoreCase) &&
                              !item.Description.Contains(keyword, StringComparison.CurrentCultureIgnoreCase);
            }
        }
    }

    private async Task DetectAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        Status = Loc.T("Optimize.Detecting");

        try
        {
            var states = await _tweaks.DetectAsync(CancellationToken.None).ConfigureAwait(true);
            Build(states);
            Status = string.Empty;
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Build(IReadOnlyList<TweakState> states)
    {
        foreach (var group in Groups)
        {
            foreach (var item in group.Items)
            {
                item.Changed -= OnRowChanged;
            }
        }

        Groups.Clear();

        var byId = states.GroupBy(s => s.Definition.Group, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var definition in _tweaks.Groups)
        {
            if (!byId.TryGetValue(definition.Id, out var items) || items.Count == 0)
            {
                continue;
            }

            var group = new TweakGroupViewModel(
                definition.Id,
                definition.Name.Resolve(Loc.CurrentCulture) is { Length: > 0 } title ? title : Loc.T("Grp." + definition.Id),
                definition.Glyph);

            foreach (var state in items.OrderByDescending(s => s.Definition.Recommended).ThenBy(s => s.Definition.Id, StringComparer.Ordinal))
            {
                var row = new TweakRowViewModel(state);
                row.Changed += OnRowChanged;
                group.Items.Add(row);
            }

            group.RefreshSummary();
            Groups.Add(group);
        }

        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(IsEmpty));
        UpdatePending();
    }

    private void OnRowChanged(object? sender, EventArgs e)
    {
        UpdatePending();

        if (sender is TweakRowViewModel row)
        {
            Groups.FirstOrDefault(g => g.Items.Contains(row))?.RefreshSummary();
        }
    }

    private void UpdatePending()
    {
        var count = Groups.SelectMany(g => g.Items).Count(i => i.HasChange);
        PendingText = count == 0 ? string.Empty : Loc.F("Optimize.Pending", count);
        OnPropertyChanged(nameof(HasPending));
    }

    [RelayCommand]
    private Task Refresh() => DetectAsync();

    [RelayCommand]
    private void ToggleGroup(TweakGroupViewModel? group)
    {
        if (group is not null)
        {
            group.IsExpanded = !group.IsExpanded;
        }
    }

    [RelayCommand]
    private void ToggleGroupAll(TweakGroupViewModel? group)
    {
        if (group is null)
        {
            return;
        }

        group.SetAll(group.Items.Any(i => !i.IsChecked));
        UpdatePending();
    }

    [RelayCommand]
    private void SelectAll()
    {
        var all = Groups.SelectMany(g => g.Items).ToList();
        var target = all.Any(i => !i.IsChecked);

        foreach (var item in all)
        {
            item.IsChecked = target;
        }

        UpdatePending();
    }

    [RelayCommand]
    private void SelectRecommended()
    {
        foreach (var item in Groups.SelectMany(g => g.Items))
        {
            item.IsChecked = item.IsRecommended || item.IsApplied;
        }

        UpdatePending();
    }

    [RelayCommand]
    private async Task ApplyAsync()
    {
        var targets = Groups.SelectMany(g => g.Items)
            .Where(i => i.IsChecked && !i.IsApplied)
            .Select(i => i.Id)
            .ToList();

        if (targets.Count == 0)
        {
            Status = Loc.T("Common.NothingSelected");
            return;
        }

        await RunAsync(targets, apply: true).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RevertAsync()
    {
        var targets = Groups.SelectMany(g => g.Items)
            .Where(i => !i.IsChecked && i.IsApplied)
            .Select(i => i.Id)
            .ToList();

        if (targets.Count == 0)
        {
            Status = Loc.T("Common.NothingSelected");
            return;
        }

        await RunAsync(targets, apply: false).ConfigureAwait(true);
    }

    private async Task RunAsync(IReadOnlyCollection<string> ids, bool apply)
    {
        IsBusy = true;
        Status = Loc.T(apply ? "Optimize.Applying" : "Optimize.Reverting");

        try
        {
            var report = apply
                ? await _tweaks.ApplyAsync(ids, CancellationToken.None).ConfigureAwait(true)
                : await _tweaks.RevertAsync(ids, CancellationToken.None).ConfigureAwait(true);

            var done = apply ? report.Applied : report.Reverted;
            Status = Loc.F("Common.TotalCount", done);

            if (report.Failed > 0)
            {
                Status += $" · {Loc.T("Common.Failed")}: {report.Failed}";
            }

            if (report.RestartRequired)
            {
                Status += " · " + Loc.T("Common.RestartNeeded");
            }

            await DetectAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Status = Loc.T("Common.Failed") + ": " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(RefreshLabel));
        OnPropertyChanged(nameof(ApplyLabel));
        OnPropertyChanged(nameof(RevertLabel));
        OnPropertyChanged(nameof(SelectAllLabel));
        OnPropertyChanged(nameof(RecommendedLabel));
        OnPropertyChanged(nameof(SearchHint));
    }
}
