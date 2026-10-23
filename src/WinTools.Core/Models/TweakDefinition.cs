namespace WinTools.Core.Models;

public enum TweakRisk
{
    Low = 1,
    Medium = 2,
    High = 3
}

public enum TweakOpKind
{
    /// <summary>写注册表值</summary>
    RegSet,
    /// <summary>删除注册表值</summary>
    RegDeleteValue,
    /// <summary>删除注册表项（含子项）</summary>
    RegDeleteKey,
    /// <summary>设置服务启动类型</summary>
    ServiceStart,
    /// <summary>执行命令（可执行文件 + 参数）</summary>
    Command
}

public enum RegValueKind
{
    DWord,
    QWord,
    String,
    ExpandString,
    Binary,
    MultiString
}

/// <summary>一条原子操作。检测、应用、回滚都用同一套结构。</summary>
public sealed class TweakOperation
{
    public TweakOpKind Type { get; set; }
    /// <summary>HKLM / HKCU / HKCR / HKU / HKCC</summary>
    public string? Hive { get; set; }
    public string? Path { get; set; }
    public string? Name { get; set; }
    public RegValueKind Kind { get; set; } = RegValueKind.DWord;
    /// <summary>统一用字符串表达，按 Kind 解析。</summary>
    public string? Value { get; set; }
    /// <summary>ServiceStart 用：服务名</summary>
    public string? Service { get; set; }
    /// <summary>ServiceStart 用：auto / manual / disabled / boot / system</summary>
    public string? StartMode { get; set; }
    /// <summary>Command 用</summary>
    public string? FileName { get; set; }
    public string? Arguments { get; set; }
    /// <summary>RegSet 前是否需要先创建缺失的键（默认 true）</summary>
    public bool CreateKey { get; set; } = true;
}

public sealed class TweakDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public LocalizedText Name { get; set; } = new();
    public LocalizedText Description { get; set; } = new();
    public TweakRisk Risk { get; set; } = TweakRisk.Low;
    public bool RequiresRestart { get; set; }
    public bool RequiresAdmin { get; set; } = true;
    /// <summary>是否属于「推荐项」（一键推荐会勾选这些）。</summary>
    public bool Recommended { get; set; }
    /// <summary>全部命中即视为「已应用」。</summary>
    public List<TweakOperation> Detect { get; set; } = new();
    public List<TweakOperation> Apply { get; set; } = new();
    public List<TweakOperation> Revert { get; set; } = new();
    public bool Enabled { get; set; } = true;
}

public sealed class TweakGroup
{
    public string Id { get; set; } = string.Empty;
    public LocalizedText Name { get; set; } = new();
    public string Glyph { get; set; } = "\uE713";
    public int Order { get; set; }
}

public sealed class TweakState
{
    public required TweakDefinition Definition { get; init; }
    public bool IsApplied { get; set; }
    public string? Error { get; set; }
}

public sealed class TweakApplyReport
{
    public int Applied { get; set; }
    public int Reverted { get; set; }
    public int Failed { get; set; }
    public bool RestartRequired { get; set; }
    public List<string> Errors { get; } = new();
}
