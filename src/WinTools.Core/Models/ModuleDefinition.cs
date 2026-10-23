namespace WinTools.Core.Models;

/// <summary>一级菜单（模块）定义，来自内嵌 modules.json，菜单结构完全数据驱动。</summary>
public sealed class ModuleDefinition
{
    public string Id { get; set; } = string.Empty;

    /// <summary>Segoe Fluent Icons 字形。</summary>
    public string Glyph { get; set; } = string.Empty;

    public int Order { get; set; }

    /// <summary>i18n 键后缀（Nav.{NavKey}），缺省等于 Id。</summary>
    public string? NavKey { get; set; }

    /// <summary>页面类型：Dashboard / Cleanup / Optimize / Settings / Placeholder。</summary>
    public string Page { get; set; } = "Placeholder";

    /// <summary>是否放到侧边栏底部（辅助项，如设置）。</summary>
    public bool IsFooter { get; set; }

    public List<ModuleSection> Sections { get; set; } = [];

    public string GetNavKey() => string.IsNullOrWhiteSpace(NavKey) ? Id : NavKey!;
}

/// <summary>二级菜单（模块内的页签）。</summary>
public sealed class ModuleSection
{
    public string Id { get; set; } = string.Empty;

    public LocalizedText Title { get; set; } = new();

    public LocalizedText? Description { get; set; }

    /// <summary>该页签规划中的能力清单（占位页展示）。</summary>
    public List<LocalizedText> Items { get; set; } = [];
}
