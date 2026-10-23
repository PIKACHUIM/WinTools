using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using WinTools.App.Localization;
using WinTools.App.Services;
using WinTools.Core.Abstractions;
using WinTools.Core.Models;

namespace WinTools.App.ViewModels.Modules;

/// <summary>页面数据状态。任何页面第一帧都不应是「无数据」，而是 Loading。</summary>
public enum PageState
{
    /// <summary>尚未开始（例如需要用户点击「搜索更新」）。</summary>
    Idle,

    /// <summary>正在加载：显示骨架屏。</summary>
    Loading,

    /// <summary>有数据。</summary>
    Ready,

    /// <summary>加载完成但确实没有数据。</summary>
    Empty,

    /// <summary>加载失败：显示原因与重试。</summary>
    Error
}

/// <summary>
/// 模块页签内容的公共基类：统一页面四态、工具栏（搜索 / 计数 / 主次操作 / 刷新）、
/// 忙碌态、操作结果提示与异常兜底。工具栏由 <c>ModulePage</c> 渲染在页签同一行。
/// </summary>
public abstract partial class SectionViewModelBase : ObservableObject, ILocalizable
{
    private int _loadGeneration;

    // ------------------------------------------------------------------
    // 页面状态
    // ------------------------------------------------------------------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    [NotifyPropertyChangedFor(nameof(IsLoading))]
    [NotifyPropertyChangedFor(nameof(IsReady))]
    [NotifyPropertyChangedFor(nameof(IsEmptyState))]
    [NotifyPropertyChangedFor(nameof(IsError))]
    [NotifyPropertyChangedFor(nameof(IsOverlayVisible))]
    private PageState _state = PageState.Loading;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _loadingText = Loc.T("Common.Loading");

    [ObservableProperty]
    private string _idleText = string.Empty;

    [ObservableProperty]
    private string _idleActionLabel = string.Empty;

    /// <summary>有数据，但被搜索条件全部过滤掉。</summary>
    [ObservableProperty]
    private bool _showNoMatch;

    public bool IsIdle => State == PageState.Idle;

    public bool IsLoading => State == PageState.Loading;

    public bool IsReady => State == PageState.Ready;

    public bool IsEmptyState => State == PageState.Empty;

    public bool IsError => State == PageState.Error;

    /// <summary>非 Ready 时由状态浮层盖住内容区。</summary>
    public bool IsOverlayVisible => State != PageState.Ready;

    public virtual string EmptyText => Loc.T("Common.NoData");

    public string NoMatchText => Loc.T("Common.NoMatch");

    public string RetryLabel => Loc.T("Common.Retry");

    public string LoadFailedText => Loc.T("Common.LoadFailed");

    // ------------------------------------------------------------------
    // 工具栏
    // ------------------------------------------------------------------

    /// <summary>是否显示搜索框。</summary>
    [ObservableProperty]
    private bool _hasSearch = true;

    [ObservableProperty]
    private string _filter = string.Empty;

    [ObservableProperty]
    private string _countText = string.Empty;

    [ObservableProperty]
    private bool _hasRefresh = true;

    [ObservableProperty]
    private bool _hasPrimary;

    [ObservableProperty]
    private string _primaryLabel = string.Empty;

    [ObservableProperty]
    private bool _hasSecondary;

    [ObservableProperty]
    private string _secondaryLabel = string.Empty;

    [ObservableProperty]
    private bool _showCheckboxes;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private bool _hasStatus;

    [ObservableProperty]
    private int _progressPercent;

    [ObservableProperty]
    private bool _showProgress;

    [ObservableProperty]
    private ResultSummary? _result;

    public string SearchHint => Loc.T("Common.Search");

    public string RefreshLabel => Loc.T("Common.Refresh");

    public string ToggleAllText => Loc.T("Common.SelectAll");

    /// <summary>主操作按钮是否可用；子类按选中项等条件重写。</summary>
    public virtual bool CanPrimary => !IsBusy;

    partial void OnStatusChanged(string value) => HasStatus = !string.IsNullOrWhiteSpace(value);

    partial void OnFilterChanged(string value) => ApplyFilter(value);

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(CanPrimary));

    // ------------------------------------------------------------------
    // 命令
    // ------------------------------------------------------------------

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsBusy)
        {
            return;
        }

        await ReloadAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task PrimaryAsync() => await ExecuteAsync(PrimaryCoreAsync).ConfigureAwait(true);

    [RelayCommand]
    private async Task SecondaryAsync() => await ExecuteAsync(SecondaryCoreAsync).ConfigureAwait(true);

    /// <summary>「待开始」状态下页面中央的大按钮（例如「搜索更新」）。</summary>
    [RelayCommand]
    private async Task IdleActionAsync() => await IdleActionCoreAsync().ConfigureAwait(true);

    protected virtual Task IdleActionCoreAsync() => ReloadAsync();

    [RelayCommand]
    private void ToggleAll() => ToggleAllCore();

    [RelayCommand]
    private void DismissResult() => Result = null;

    // ------------------------------------------------------------------
    // 子类扩展点
    // ------------------------------------------------------------------

    /// <summary>加载数据。抛出异常会让页面进入 Error 状态。</summary>
    protected virtual Task LoadAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>加载成功后用于判断是 Ready 还是 Empty。</summary>
    protected virtual bool HasData => true;

    /// <summary>加载成功后的收尾（例如开始异步加载图标）。</summary>
    protected virtual void OnLoaded()
    {
    }

    protected virtual void ApplyFilter(string text)
    {
    }

    protected virtual void ToggleAllCore()
    {
    }

    protected virtual Task PrimaryCoreAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    protected virtual Task SecondaryCoreAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    // ------------------------------------------------------------------
    // 加载与执行
    // ------------------------------------------------------------------

    /// <summary>完整重新加载：先进入 Loading（骨架屏），成功后 Ready / Empty，失败进入 Error。</summary>
    protected Task ReloadAsync(CancellationToken cancellationToken = default)
        => LoadCoreAsync(cancellationToken, soft: false);

    /// <summary>静默刷新：保持当前内容不闪烁，用于安装、卸载等操作之后同步最新状态。</summary>
    protected Task RefreshQuietlyAsync(CancellationToken cancellationToken = default)
        => LoadCoreAsync(cancellationToken, soft: true);

    private async Task LoadCoreAsync(CancellationToken cancellationToken, bool soft)
    {
        var generation = ++_loadGeneration;
        var alreadyBusy = IsBusy;

        IsBusy = true;
        if (!soft)
        {
            ErrorMessage = string.Empty;
            LoadingText = Loc.T("Common.Loading");
            State = PageState.Loading;
        }

        try
        {
            await LoadAsync(cancellationToken).ConfigureAwait(true);

            if (generation != _loadGeneration)
            {
                return;
            }

            State = HasData ? PageState.Ready : PageState.Empty;
            OnLoaded();
        }
        catch (OperationCanceledException)
        {
            if (generation == _loadGeneration && !soft)
            {
                State = HasData ? PageState.Ready : PageState.Idle;
            }
        }
        catch (Exception ex)
        {
            Logger?.Error(GetType().Name, "加载失败", ex);

            if (generation == _loadGeneration)
            {
                ErrorMessage = ex.Message;
                if (soft)
                {
                    UI.Toast.Error(Loc.T("Common.LoadFailed") + ": " + ex.Message);
                }
                else
                {
                    State = PageState.Error;
                }
            }
        }
        finally
        {
            if (!alreadyBusy)
            {
                IsBusy = false;
            }
        }
    }

    /// <summary>统一忙碌态与异常兜底，避免 UI 线程崩掉。</summary>
    protected async Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await action(cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            Status = Loc.T("Common.Cancel");
        }
        catch (Exception ex)
        {
            Logger?.Error(GetType().Name, "操作失败", ex);
            UI.Toast.Error(Loc.T("Common.Failed") + ": " + ex.Message);
        }
        finally
        {
            IsBusy = false;
            ShowProgress = false;
        }
    }

    /// <summary>把操作结果以轻提示呈现；失败同时写入状态行，便于查看完整原因。</summary>
    protected void Report(OperationResult result)
    {
        var suffix = result.RestartRequired ? " · " + Loc.T("Common.RestartNeeded") : string.Empty;

        if (result.Success)
        {
            Status = string.Empty;
            UI.Toast.Success(result.Message + suffix);
            return;
        }

        var text = Loc.T("Common.Failed") + ": " + result.Message;
        Status = text;
        UI.Toast.Error(text);
    }

    protected void ReportError(Exception ex)
    {
        Logger?.Error(GetType().Name, "操作失败", ex);
        var text = Loc.T("Common.Failed") + ": " + ex.Message;
        Status = text;
        UI.Toast.Error(text);
    }

    protected static IAppLogger? Logger
        => App.Services?.GetService<IAppLogger>();

    public virtual void RefreshLocalization()
    {
        OnPropertyChanged(nameof(SearchHint));
        OnPropertyChanged(nameof(RefreshLabel));
        OnPropertyChanged(nameof(ToggleAllText));
        OnPropertyChanged(nameof(EmptyText));
        OnPropertyChanged(nameof(NoMatchText));
        OnPropertyChanged(nameof(RetryLabel));
        OnPropertyChanged(nameof(LoadFailedText));
    }
}
