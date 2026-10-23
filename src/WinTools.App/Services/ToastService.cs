using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WinTools.App.Services;

public enum ToastKind
{
    Info,
    Success,
    Warning,
    Error
}

/// <summary>一条轻提示。</summary>
public sealed class ToastItem : ObservableObject
{
    public ToastItem(string message, ToastKind kind)
    {
        Message = message;
        Kind = kind;
    }

    public string Message { get; }

    public ToastKind Kind { get; }

    /// <summary>Segoe Fluent Icons 字形。</summary>
    public string Glyph => Kind switch
    {
        ToastKind.Success => "\uE73E",
        ToastKind.Warning => "\uE7BA",
        ToastKind.Error => "\uEA39",
        _ => "\uE946"
    };
}

/// <summary>全局轻提示：自动消失，最多同时显示 4 条。线程安全，可从任意线程调用。</summary>
public sealed class ToastService
{
    private const int MaxVisible = 4;

    public ObservableCollection<ToastItem> Items { get; } = [];

    public void Info(string message) => Show(message, ToastKind.Info);

    public void Success(string message) => Show(message, ToastKind.Success);

    public void Warning(string message) => Show(message, ToastKind.Warning, 5000);

    public void Error(string message) => Show(message, ToastKind.Error, 6500);

    public void Show(string message, ToastKind kind = ToastKind.Info, int durationMs = 3200)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        if (!dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(() => Show(message, kind, durationMs));
            return;
        }

        var item = new ToastItem(message, kind);
        Items.Add(item);

        while (Items.Count > MaxVisible)
        {
            Items.RemoveAt(0);
        }

        var timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(durationMs)
        };

        timer.Tick += (_, _) =>
        {
            timer.Stop();
            Items.Remove(item);
        };

        timer.Start();
    }

    public void Dismiss(ToastItem item) => Items.Remove(item);
}
