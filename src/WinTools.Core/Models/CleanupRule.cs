namespace WinTools.Core.Models;

public enum CleanupMatchKind
{
    /// <summary>目录递归（统计并清理目录内容）</summary>
    DirectoryRecursive,
    /// <summary>单文件</summary>
    SingleFile,
    /// <summary>通配文件（如 thumbcache_*.db）</summary>
    GlobFile,
    /// <summary>系统文件（pagefile.sys 之类，通常禁止清理）</summary>
    SystemFile,
    /// <summary>按扩展名/目录名扫描</summary>
    ExtensionScan
}

public enum CleanupStrategy
{
    /// <summary>删除目录内容，保留目录本身</summary>
    DeleteContents,
    /// <summary>整目录删除</summary>
    DeleteDirectory,
    /// <summary>删除文件</summary>
    DeleteFile,
    /// <summary>按时间阈值删除（保留最近 N 天）</summary>
    DeleteByAge,
    /// <summary>禁止清理（仅展示，不可勾选）</summary>
    Disabled
}

/// <summary>安全等级：S1 安全 → S4 危险/禁止</summary>
public enum SafetyLevel
{
    S1 = 1,
    S2 = 2,
    S3 = 3,
    S4 = 4
}

public sealed class CleanupRule
{
    public string Id { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public LocalizedText Name { get; set; } = new();
    public LocalizedText? Note { get; set; }
    /// <summary>「这类文件是什么」的说明；缺省时沿用分类说明。</summary>
    public LocalizedText? Description { get; set; }
    /// <summary>路径模板，支持 %ENV%、{Drive}、{AllUsers}、* 通配。</summary>
    public List<string> Paths { get; set; } = new();
    public CleanupMatchKind Match { get; set; } = CleanupMatchKind.DirectoryRecursive;
    public CleanupStrategy Strategy { get; set; } = CleanupStrategy.DeleteContents;
    public SafetyLevel Safety { get; set; } = SafetyLevel.S1;
    public bool RequiresAdmin { get; set; }
    public bool RequiresProcessStop { get; set; }
    /// <summary>清理前需二次确认（数据可能有用）。</summary>
    public bool RequiresConfirm { get; set; }
    /// <summary>DeleteByAge 时保留的天数。</summary>
    public int AgeDays { get; set; } = 7;
    /// <summary>扩展名过滤（ExtensionScan 用），如 [".pyc"]。</summary>
    public List<string>? Extensions { get; set; }
    /// <summary>目录名过滤（ExtensionScan 用），如 ["__pycache__"]。</summary>
    public List<string>? DirectoryNames { get; set; }
    public bool Enabled { get; set; } = true;

    /// <summary>S4 与 Disabled 策略一律不可清理。</summary>
    public bool IsCleanable => Enabled && Safety != SafetyLevel.S4 && Strategy != CleanupStrategy.Disabled;

    /// <summary>推荐项：S1 且不需要二次确认，扫描完成后默认勾选。</summary>
    public bool IsRecommended => IsCleanable && !RequiresConfirm && Safety == SafetyLevel.S1;

    /// <summary>默认是否勾选（只勾推荐项，S2 及以上留给用户自己判断）。</summary>
    public bool IsDefaultSelected => IsRecommended;

    /// <summary>高风险：S3/S4 或规则声明需要二次确认。</summary>
    public bool IsHighRisk => IsCleanable && (Safety >= SafetyLevel.S3 || RequiresConfirm);
}

public sealed class CleanupCategory
{
    public string Id { get; set; } = string.Empty;
    public LocalizedText Name { get; set; } = new();
    /// <summary>分类说明：展开清理项时告诉用户「这类文件是什么」。</summary>
    public LocalizedText? Description { get; set; }
    public string Glyph { get; set; } = "\uE8B7";
    public int Order { get; set; }
}
