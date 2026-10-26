using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using WinTools.App.Diagnostics;
using WinTools.App.Services;
using WinTools.App.ViewModels;
using WinTools.Core.Abstractions;

namespace WinTools.App.Views;

public partial class ShellWindow
{
    private const string MaximizeGlyph = "\uE922";
    private const string RestoreGlyph = "\uE923";

    private readonly ShellViewModel _viewModel;
    private readonly ISettingsService _settings;

    public ShellWindow(ShellViewModel viewModel, ISettingsService settings)
    {
        _viewModel = viewModel;
        _settings = settings;

        BootTrace.Log("ShellWindow ctor");
        using (BootTrace.Scope("InitializeComponent"))
        {
            InitializeComponent();
        }

        BootTrace.Log("InitializeComponent done");
        DataContext = viewModel;

        // 大屏上开得更大，小屏则收在可用工作区内，避免窗口超出屏幕
        var work = SystemParameters.WorkArea;
        var width = Math.Max(settings.Current.WindowWidth, 1380);
        var height = Math.Max(settings.Current.WindowHeight, 880);

        Width = Math.Min(width, work.Width * 0.94);
        Height = Math.Min(height, work.Height * 0.94);

        if (settings.Current.WindowMaximized)
        {
            WindowState = WindowState.Maximized;
        }

        PreviewKeyDown += OnPreviewKeyDown;
        Loaded += OnLoaded;
        Closing += OnClosing;
        StateChanged += OnStateChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => _viewModel.Initialize();

    /// <summary>点击遮罩等同于取消。</summary>
    private void OnScrimClick(object sender, MouseButtonEventArgs e) => UI.Confirm.CancelCurrent();

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && UI.Confirm.IsOpen)
        {
            UI.Confirm.CancelCurrent();
            e.Handled = true;
        }
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximizeClick(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (MaximizeButton is null)
        {
            return;
        }

        var maximized = WindowState == WindowState.Maximized;
        MaximizeButton.Content = maximized ? RestoreGlyph : MaximizeGlyph;

        // 最大化时窗口会盖住系统调整边框，补偿一圈内边距避免内容被裁掉
        var pad = maximized ? SystemParameters.WindowResizeBorderThickness : default;
        RootBorder.Margin = maximized
            ? new Thickness(pad.Left, pad.Top + 0, pad.Right, pad.Bottom)
            : default;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            _settings.Current.WindowMaximized = true;
        }
        else
        {
            _settings.Current.WindowMaximized = false;
            _settings.Current.WindowWidth = ActualWidth;
            _settings.Current.WindowHeight = ActualHeight;
        }
    }
}
