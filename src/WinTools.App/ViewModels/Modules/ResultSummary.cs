using System.Windows.Input;
using System.Windows.Media;

namespace WinTools.App.ViewModels.Modules;

public enum ResultKind
{
    Success,
    Warning,
    Error
}

/// <summary>结果摘要里的一项数值。</summary>
public sealed record ResultMetric(string Label, string Value);

/// <summary>
/// 操作结果摘要：批量安装、卸载、备份等长任务完成后展示「做了什么、结果如何」，
/// 而不是只给一行转瞬即逝的提示。
/// </summary>
public sealed class ResultSummary
{
    public ResultSummary(
        string title,
        ResultKind kind,
        IReadOnlyList<ResultMetric> metrics,
        string? subtitle = null)
    {
        Title = title;
        Kind = kind;
        Metrics = metrics;
        Subtitle = subtitle;
    }

    public string Title { get; }

    public string? Subtitle { get; }

    public ResultKind Kind { get; }

    public IReadOnlyList<ResultMetric> Metrics { get; }

    public string? ActionLabel { get; init; }

    public ICommand? ActionCommand { get; init; }

    public bool HasSubtitle => !string.IsNullOrWhiteSpace(Subtitle);

    public bool HasAction => ActionCommand is not null && !string.IsNullOrWhiteSpace(ActionLabel);

    public string Glyph => Kind switch
    {
        ResultKind.Success => "\uE73E",
        ResultKind.Warning => "\uE7BA",
        _ => "\uEA39"
    };

    public Brush AccentBrush => Kind switch
    {
        ResultKind.Success => Lookup("App.Success"),
        ResultKind.Warning => Lookup("App.Warning"),
        _ => Lookup("App.Danger")
    };

    private static Brush Lookup(string key)
        => System.Windows.Application.Current?.TryFindResource(key) as Brush ?? Brushes.Gray;
}
