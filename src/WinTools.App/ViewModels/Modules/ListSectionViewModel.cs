using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinTools.App.Localization;

namespace WinTools.App.ViewModels.Modules;

/// <summary>
/// 列表型页签基类：在公共工具栏与页面四态之上，提供行集合、过滤、勾选与可选输入框。
/// 各模块只需要实现数据加载与操作逻辑，视图复用同一套模板。
/// </summary>
public abstract partial class ListSectionViewModel : SectionViewModelBase
{
    protected ListSectionViewModel()
    {
        RowsView = CollectionViewSource.GetDefaultView(Rows);
        RowsView.Filter = MatchFilter;
        Rows.CollectionChanged += OnRowsChanged;
    }

    public ObservableCollection<RowItem> Rows { get; } = [];

    public ICollectionView RowsView { get; }

    [ObservableProperty]
    private bool _hasInput;

    [ObservableProperty]
    private string _inputLabel = string.Empty;

    [ObservableProperty]
    private string _inputText = string.Empty;

    [ObservableProperty]
    private bool _isInputMultiline;

    [ObservableProperty]
    private string _inputActionLabel = Loc.T("Common.Add");

    public bool IsEmpty => Rows.Count == 0;

    protected override bool HasData => Rows.Count > 0;

    protected override void ApplyFilter(string text)
    {
        RowsView.Refresh();
        UpdateCount();
    }

    private bool MatchFilter(object item)
    {
        if (string.IsNullOrWhiteSpace(Filter))
        {
            return true;
        }

        return item is RowItem row &&
               row.SearchBlob.Contains(Filter.Trim(), StringComparison.CurrentCultureIgnoreCase);
    }

    private void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(IsEmpty));
        UpdateCount();
    }

    /// <summary>刷新工具栏计数：筛选后显示「命中 / 总数」。</summary>
    protected void UpdateCount()
    {
        var total = Rows.Count;
        var shown = total;

        if (!string.IsNullOrWhiteSpace(Filter))
        {
            shown = Rows.Count(row => MatchFilter(row));
        }

        CountText = total == 0
            ? string.Empty
            : shown == total ? total.ToString() : $"{shown} / {total}";

        ShowNoMatch = total > 0 && shown == 0;
    }

    protected override void OnLoaded()
    {
        UpdateCount();

        foreach (var row in Rows)
        {
            _ = row.LoadIconAsync();
        }
    }

    [RelayCommand]
    private async Task InputAsync() => await ExecuteAsync(InputCoreAsync).ConfigureAwait(true);

    /// <summary>全选 / 取消全选。</summary>
    protected override void ToggleAllCore()
    {
        var target = Rows.Any(r => !r.IsChecked);
        foreach (var row in Rows)
        {
            row.IsChecked = target;
        }
    }

    protected virtual Task InputCoreAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    protected IReadOnlyList<RowItem> CheckedRows => Rows.Where(r => r.IsChecked).ToList();
}
