namespace WinTools.App.Services;

/// <summary>
/// 界面层公共服务入口。这些服务是进程级基础设施（提示、确认、剪贴板、图标），
/// 视图模型随处要用，用静态入口比到处注入更直接；单元测试时可替换实例。
/// </summary>
public static class UI
{
    public static ToastService Toast { get; set; } = new();

    public static ConfirmService Confirm { get; set; } = new();

    public static ClipboardService Clipboard { get; set; }

    public static IconService Icons { get; set; } = new();

    static UI() => Clipboard = new ClipboardService(Toast);
}
