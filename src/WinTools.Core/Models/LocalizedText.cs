namespace WinTools.Core.Models;

/// <summary>
/// 多语言文本。JSON 里直接写成 { "zh-Hans": "...", "en-US": "..." }。
/// 解析顺序：目标语言 → en-US → zh-Hans → 任意一个 → 空串。
/// </summary>
public sealed class LocalizedText : Dictionary<string, string>
{
    public LocalizedText() : base(StringComparer.OrdinalIgnoreCase) { }

    public LocalizedText(params (string Culture, string Text)[] entries) : this()
    {
        foreach (var (culture, text) in entries)
        {
            this[culture] = text;
        }
    }

    public string Resolve(string culture)
    {
        if (Count == 0)
        {
            return string.Empty;
        }

        if (!string.IsNullOrEmpty(culture) && TryGetValue(culture, out var exact) && !string.IsNullOrWhiteSpace(exact))
        {
            return exact;
        }

        // zh-Hant / zh-Hans 的语言父级（zh）不是我们想要的回退，单独处理
        if (!string.IsNullOrEmpty(culture) && culture.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
        {
            var sibling = culture.Contains("Hant", StringComparison.OrdinalIgnoreCase) ||
                          culture.Contains("TW", StringComparison.OrdinalIgnoreCase) ||
                          culture.Contains("HK", StringComparison.OrdinalIgnoreCase) ||
                          culture.Contains("MO", StringComparison.OrdinalIgnoreCase)
                ? "zh-Hant"
                : "zh-Hans";
            if (TryGetValue(sibling, out var zh) && !string.IsNullOrWhiteSpace(zh))
            {
                return zh;
            }
        }

        if (TryGetValue("en-US", out var en) && !string.IsNullOrWhiteSpace(en))
        {
            return en;
        }

        if (TryGetValue("zh-Hans", out var cn) && !string.IsNullOrWhiteSpace(cn))
        {
            return cn;
        }

        return Values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
    }

    public static LocalizedText Of(string zhHans, string enUs) => new(("zh-Hans", zhHans), ("en-US", enUs));
}
