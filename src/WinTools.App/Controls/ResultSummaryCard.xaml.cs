using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WinTools.App.Controls;

/// <summary>操作结果摘要卡。DataContext 是 <c>ResultSummary</c>；关闭按钮通过 <see cref="DismissCommand"/> 回到宿主。</summary>
public partial class ResultSummaryCard : UserControl
{
    public static readonly DependencyProperty DismissCommandProperty =
        DependencyProperty.Register(
            nameof(DismissCommand),
            typeof(ICommand),
            typeof(ResultSummaryCard),
            new PropertyMetadata(null, (d, e) => ((ResultSummaryCard)d).DismissButton.Command = e.NewValue as ICommand));

    public ResultSummaryCard() => InitializeComponent();

    public ICommand? DismissCommand
    {
        get => (ICommand?)GetValue(DismissCommandProperty);
        set => SetValue(DismissCommandProperty, value);
    }
}
