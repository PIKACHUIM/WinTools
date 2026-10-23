namespace WinTools.Core.Models;

/// <summary>
/// 可切换的系统渠道（SKU）。
/// Key 为微软公开的「通用安装密钥」（KMS client setup key）：只能用于切换版本，
/// <b>不能激活系统</b>，激活仍需用户自己的授权。
/// </summary>
public sealed record EditionOption(string Id, string Key);

public static class EditionCatalog
{
    /// <summary>按版本从低到高排列，供下拉展示。</summary>
    public static IReadOnlyList<EditionOption> All { get; } =
    [
        new("Home", "TX9XD-98N7V-6WMQ6-BX7FG-H8Q99"),
        new("HomeSingleLanguage", "7HNRX-D7KGG-3K4RQ-4WPJ4-YTDFH"),
        new("HomeChina", "PVMJN-6DFY6-9CCP6-7BKTT-D3WVR"),
        new("Professional", "VK7JG-NPHTM-C97JM-9MPGT-3V66T"),
        new("ProfessionalWorkstation", "DXG7C-N36C4-C4HTG-X4T3X-2YV77"),
        new("Enterprise", "NPPR9-FWDCX-D2C8J-H872K-2YT43"),
        new("Education", "YNMGQ-8RYV3-4PGQ3-C8XTP-7CFBY")
    ];

    public static EditionOption? Find(string id)
        => All.FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase));
}
