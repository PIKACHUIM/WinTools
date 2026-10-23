using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace WinTools.App.Localization;

public sealed class LanguageOption
{
    public required string Culture { get; init; }
    public required string DisplayName { get; init; }
    public bool IsRightToLeft { get; init; }

    public override string ToString() => DisplayName;
}

/// <summary>XAML 绑定源：{Binding [Nav.Cleanup], Source={x:Static loc:Loc.Instance}}</summary>
public sealed class LocSource : INotifyPropertyChanged
{
    internal LocalizationService? Service { get; set; }

    public string this[string key] => Service?.Get(key) ?? key;

    public event PropertyChangedEventHandler? PropertyChanged;

    internal void RaiseAll() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
}

public static class Loc
{
    public static LocSource Instance { get; } = new();

    /// <summary>当前语言代码，供 LocalizedText.Resolve 使用。</summary>
    public static string CurrentCulture { get; internal set; } = FallbackCultureKey;

    private const string FallbackCultureKey = "en-US";

    public static string T(string key) => Instance[key];

    public static string F(string key, params object?[] args)
    {
        var template = T(key);
        try
        {
            return string.Format(CultureInfo.CurrentCulture, template, args);
        }
        catch (FormatException)
        {
            return template;
        }
    }
}

public sealed class LocalizationService
{
    public const string FallbackCulture = "en-US";

    private readonly Dictionary<string, Dictionary<string, string>> _catalogs = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _catalogDirectory = Path.Combine(AppContext.BaseDirectory, "Assets", "i18n");
    private string _current = FallbackCulture;

    public static IReadOnlyList<LanguageOption> AvailableLanguages { get; } =
    [
        new() { Culture = "en-US", DisplayName = "English" },
        new() { Culture = "zh-Hans", DisplayName = "简体中文" },
        new() { Culture = "zh-Hant", DisplayName = "繁體中文" },
        new() { Culture = "ja-JP", DisplayName = "日本語" },
        new() { Culture = "ko-KR", DisplayName = "한국어" },
        new() { Culture = "de-DE", DisplayName = "Deutsch" },
        new() { Culture = "fr-FR", DisplayName = "Français" },
        new() { Culture = "es-ES", DisplayName = "Español" },
        new() { Culture = "pt-BR", DisplayName = "Português" },
        new() { Culture = "it-IT", DisplayName = "Italiano" },
        new() { Culture = "ru-RU", DisplayName = "Русский" },
        new() { Culture = "ar-SA", DisplayName = "العربية", IsRightToLeft = true }
    ];

    public LocalizationService()
    {
        Loc.Instance.Service = this;
    }

    public string CurrentCulture => _current;

    public bool IsRightToLeft =>
        AvailableLanguages.FirstOrDefault(l => l.Culture.Equals(_current, StringComparison.OrdinalIgnoreCase))?.IsRightToLeft
        ?? false;

    public event EventHandler? LanguageChanged;

    public static string ResolveSystemDefault()
    {
        var name = CultureInfo.CurrentUICulture.Name;
        var match = AvailableLanguages.FirstOrDefault(l =>
            l.Culture.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
        {
            return match.Culture;
        }

        // 语言父级匹配（例如 zh-CN → zh-Hans，pt-PT → pt-BR）
        var twoLetter = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        var parent = AvailableLanguages.FirstOrDefault(l =>
            l.Culture.StartsWith(twoLetter + "-", StringComparison.OrdinalIgnoreCase));
        if (parent is not null)
        {
            return parent.Culture;
        }

        return twoLetter switch
        {
            "zh" => CultureInfo.CurrentUICulture.Name.Contains("TW") ||
                    CultureInfo.CurrentUICulture.Name.Contains("HK") ||
                    CultureInfo.CurrentUICulture.Name.Contains("MO")
                ? "zh-Hant"
                : "zh-Hans",
            _ => FallbackCulture
        };
    }

    public void SetLanguage(string? culture)
    {
        var target = string.IsNullOrWhiteSpace(culture) ? ResolveSystemDefault() : culture!;
        if (GetCatalog(target) is null)
        {
            target = FallbackCulture;
            GetCatalog(target);
        }

        _current = target;
        Loc.CurrentCulture = target;

        try
        {
            var info = CultureInfo.GetCultureInfo(target);
            CultureInfo.DefaultThreadCurrentUICulture = info;
            CultureInfo.DefaultThreadCurrentCulture = info;
        }
        catch
        {
            // 忽略区域性设置失败
        }

        Loc.Instance.RaiseAll();
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public string Get(string key)
    {
        var catalog = GetCatalog(_current);
        if (catalog is not null && catalog.TryGetValue(key, out var value))
        {
            return value;
        }

        if (!_current.Equals(FallbackCulture, StringComparison.OrdinalIgnoreCase))
        {
            var fallback = GetCatalog(FallbackCulture);
            if (fallback is not null && fallback.TryGetValue(key, out var en))
            {
                return en;
            }
        }

        return key;
    }

    /// <summary>按需加载语言包：只解析当前语言（及兜底语言），避免启动期 12 次文件解析。</summary>
    private Dictionary<string, string>? GetCatalog(string culture)
    {
        if (_catalogs.TryGetValue(culture, out var cached))
        {
            return cached;
        }

        try
        {
            var file = Path.Combine(_catalogDirectory, culture + ".json");
            if (!File.Exists(file))
            {
                return null;
            }

            var map = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file));
            if (map is null)
            {
                return null;
            }

            var catalog = new Dictionary<string, string>(map, StringComparer.Ordinal);
            _catalogs[culture] = catalog;
            return catalog;
        }
        catch
        {
            // 单个语言包损坏不影响启动
            return null;
        }
    }
}
