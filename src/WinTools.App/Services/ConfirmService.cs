using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinTools.App.Localization;

namespace WinTools.App.Services;

/// <summary>确认对话框请求。</summary>
/// <param name="Title">标题。</param>
/// <param name="Message">说明。</param>
/// <param name="Items">受影响的对象（最多展示前 50 条，其余折叠为"另有 N 项"）。</param>
/// <param name="ConfirmLabel">确认按钮文字，默认「确认」。</param>
/// <param name="Danger">危险操作：确认按钮用警示色。</param>
/// <param name="RequireAcknowledge">必须先勾选「我已了解风险」才能确认。</param>
public sealed record ConfirmRequest(
    string Title,
    string Message,
    IReadOnlyList<string>? Items = null,
    string? ConfirmLabel = null,
    bool Danger = false,
    bool RequireAcknowledge = false);

/// <summary>对话框视图模型。</summary>
public sealed partial class ConfirmDialogViewModel : ObservableObject
{
    private const int MaxShownItems = 50;
    private readonly TaskCompletionSource<bool> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ConfirmDialogViewModel(ConfirmRequest request)
    {
        Title = request.Title;
        Message = request.Message;
        Danger = request.Danger;
        RequireAcknowledge = request.RequireAcknowledge;
        ConfirmLabel = request.ConfirmLabel ?? Loc.T("Common.Confirm");

        var items = request.Items ?? [];
        var shown = items.Take(MaxShownItems).ToList();
        if (items.Count > MaxShownItems)
        {
            shown.Add("…  +" + (items.Count - MaxShownItems));
        }

        Items = shown;
        ItemsHeader = Loc.F("Common.AffectedItems", items.Count);
    }

    public string Title { get; }

    public string Message { get; }

    public IReadOnlyList<string> Items { get; }

    public string ItemsHeader { get; }

    public bool HasItems => Items.Count > 0;

    public bool Danger { get; }

    public bool RequireAcknowledge { get; }

    public string ConfirmLabel { get; }

    public string CancelLabel => Loc.T("Common.Cancel");

    public string AcknowledgeText => Loc.T("Common.Acknowledge");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConfirm))]
    private bool _isAcknowledged;

    public bool CanConfirm => !RequireAcknowledge || IsAcknowledged;

    internal Task<bool> Completion => _completion.Task;

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private void Confirm() => _completion.TrySetResult(true);

    [RelayCommand]
    public void Cancel() => _completion.TrySetResult(false);

    partial void OnIsAcknowledgedChanged(bool value) => ConfirmCommand.NotifyCanExecuteChanged();
}

/// <summary>应用内模态确认：渲染在主窗口浮层里，不弹系统窗口，保证风格一致。</summary>
public sealed partial class ConfirmService : ObservableObject
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOpen))]
    private ConfirmDialogViewModel? _current;

    public bool IsOpen => Current is not null;

    /// <summary>弹出确认框并等待用户选择。多个请求会排队依次展示。</summary>
    public async Task<bool> AskAsync(ConfirmRequest request)
    {
        await _gate.WaitAsync().ConfigureAwait(true);
        try
        {
            var dialog = new ConfirmDialogViewModel(request);
            Current = dialog;
            return await dialog.Completion.ConfigureAwait(true);
        }
        finally
        {
            Current = null;
            _gate.Release();
        }
    }

    /// <summary>Esc 或点击遮罩时调用。</summary>
    public void CancelCurrent() => Current?.Cancel();
}
