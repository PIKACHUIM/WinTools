namespace WinTools.Core.Models;

public enum AppTheme
{
    System,
    Light,
    Dark
}

public sealed class AppSettings
{
    /// <summary>空字符串表示跟随系统。</summary>
    public string Language { get; set; } = string.Empty;
    public AppTheme Theme { get; set; } = AppTheme.System;
    /// <summary>额外扫描根目录（用户自定义，例如只扫某个盘）。</summary>
    public List<string> ExtraScanRoots { get; set; } = new();
    /// <summary>清理时是否走回收站。</summary>
    public bool UseRecycleBin { get; set; }
    /// <summary>扫描并发度，0 = 自动。</summary>
    public int ScanConcurrency { get; set; }
    /// <summary>窗口尺寸记忆。</summary>
    public double WindowWidth { get; set; } = 1180;
    public double WindowHeight { get; set; } = 780;
    public bool WindowMaximized { get; set; }
    public string? LastPage { get; set; }
}
