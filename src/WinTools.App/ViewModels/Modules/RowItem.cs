using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinTools.App.Services;

namespace WinTools.App.ViewModels.Modules;

/// <summary>列表行的状态色语义（由模板映射到主题画刷）。</summary>
public enum RowTone
{
    Neutral,
    Success,
    Warning,
    Danger,
    Accent
}

/// <summary>
/// 通用列表行：各模块共用的展示 + 勾选 + 开关 + 单行操作 + 程序图标。
/// </summary>
public sealed partial class RowItem : ObservableObject
{
    private static readonly (Color Fore, Color Back)[] TilePalette =
    [
        (Color.FromRgb(0x3F, 0x8E, 0xF5), Color.FromArgb(0x26, 0x3F, 0x8E, 0xF5)),
        (Color.FromRgb(0x2E, 0xB8, 0x8A), Color.FromArgb(0x26, 0x2E, 0xB8, 0x8A)),
        (Color.FromRgb(0xE0, 0x8A, 0x1E), Color.FromArgb(0x26, 0xE0, 0x8A, 0x1E)),
        (Color.FromRgb(0xD9, 0x5F, 0x8C), Color.FromArgb(0x26, 0xD9, 0x5F, 0x8C)),
        (Color.FromRgb(0x8A, 0x6B, 0xE0), Color.FromArgb(0x26, 0x8A, 0x6B, 0xE0)),
        (Color.FromRgb(0x2F, 0xA8, 0xC4), Color.FromArgb(0x26, 0x2F, 0xA8, 0xC4)),
        (Color.FromRgb(0xC9, 0x6A, 0x3E), Color.FromArgb(0x26, 0xC9, 0x6A, 0x3E)),
        (Color.FromRgb(0x6E, 0x9E, 0x3B), Color.FromArgb(0x26, 0x6E, 0x9E, 0x3B))
    ];

    private bool _silent;

    public RowItem(
        string key,
        string title,
        string? subtitle = null,
        string? detail = null,
        string? badge = null,
        RowTone tone = RowTone.Neutral)
    {
        Key = key;
        Title = title;
        Subtitle = subtitle;
        Detail = detail;
        Badge = badge;
        Tone = tone;

        var seed = Math.Abs(StringComparer.OrdinalIgnoreCase.GetHashCode(title));
        var palette = TilePalette[seed % TilePalette.Length];
        TileForeground = Freeze(palette.Fore);
        TileBackground = Freeze(palette.Back);
        Initial = ResolveInitial(title);
    }

    public string Key { get; }

    public string Title { get; }

    /// <summary>次要说明（灰色小字）。</summary>
    public string? Subtitle { get; }

    /// <summary>右侧数值/时间等。</summary>
    public string? Detail { get; }

    /// <summary>状态徽标文字（可空）。</summary>
    public string? Badge { get; }

    public RowTone Tone { get; }

    public bool HasBadge => !string.IsNullOrWhiteSpace(Badge);

    public bool HasSubtitle => !string.IsNullOrWhiteSpace(Subtitle);

    // ---- 图标 ----

    /// <summary>图标来源：exe / lnk / ico / png 路径，或「路径,索引」。为空则只显示首字母方块。</summary>
    public string? IconSource { get; init; }

    /// <summary>是否保留图标列（有图标来源，或希望显示首字母方块）。</summary>
    public bool ShowIcon { get; init; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasIcon))]
    [NotifyPropertyChangedFor(nameof(ShowInitial))]
    private ImageSource? _icon;

    public bool HasIcon => Icon is not null;

    public bool ShowInitial => Icon is null;

    public string Initial { get; }

    public Brush TileForeground { get; }

    public Brush TileBackground { get; }

    /// <summary>异步加载图标；重复调用安全。</summary>
    public async Task LoadIconAsync()
    {
        if (Icon is not null || string.IsNullOrWhiteSpace(IconSource))
        {
            return;
        }

        var image = await UI.Icons.LoadAsync(IconSource).ConfigureAwait(true);
        if (image is not null)
        {
            Icon = image;
        }
    }

    // ---- 勾选 ----

    [ObservableProperty]
    private bool _isChecked;

    [ObservableProperty]
    private bool _showCheckbox;

    // ---- 开关（启用 / 禁用类状态一律用开关，不用勾选框）----

    [ObservableProperty]
    private bool _showToggle;

    [ObservableProperty]
    private bool _isOn;

    [ObservableProperty]
    private bool _isToggleEnabled = true;

    /// <summary>
    /// 用户拨动开关时调用。返回 false 表示失败，开关会自动回弹到原状态。
    /// </summary>
    public Func<RowItem, bool, Task<bool>>? ToggleHandler { get; set; }

    /// <summary>程序内部同步状态时使用，不触发 <see cref="ToggleHandler"/>。</summary>
    public void SetOnSilently(bool value)
    {
        _silent = true;
        try
        {
            IsOn = value;
        }
        finally
        {
            _silent = false;
        }
    }

    partial void OnIsOnChanged(bool value)
    {
        if (_silent || ToggleHandler is null)
        {
            return;
        }

        _ = RunToggleAsync(value);
    }

    private async Task RunToggleAsync(bool value)
    {
        IsToggleEnabled = false;
        var ok = false;

        try
        {
            ok = await ToggleHandler!(this, value).ConfigureAwait(true);
        }
        catch
        {
            ok = false;
        }
        finally
        {
            if (!ok)
            {
                SetOnSilently(!value);
            }

            IsToggleEnabled = true;
        }
    }

    // ---- 单行操作按钮 ----

    [ObservableProperty]
    private bool _showAction;

    [ObservableProperty]
    private string _actionLabel = string.Empty;

    [ObservableProperty]
    private bool _isActionEnabled = true;

    public IAsyncRelayCommand? ActionCommand { get; set; }

    /// <summary>行绑定的业务对象（更新、驱动、程序等）。</summary>
    public object? Tag { get; init; }

    public string SearchBlob { get; init; } = string.Empty;

    /// <summary>状态徽标底色（按语义色取主题画刷）。</summary>
    public Brush BadgeBackground => Tone switch
    {
        RowTone.Success => Resource("App.Success"),
        RowTone.Warning => Resource("App.Warning"),
        RowTone.Danger => Resource("App.Danger"),
        RowTone.Accent => Resource("App.Accent"),
        _ => Resource("App.BorderStrong")
    };

    public Brush BadgeForeground => Resource("App.OnSafety");

    private static Brush Resource(string key)
        => Application.Current?.TryFindResource(key) as Brush ?? Brushes.Gray;

    private static Brush Freeze(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static string ResolveInitial(string title)
    {
        foreach (var ch in title)
        {
            if (char.IsLetterOrDigit(ch))
            {
                return char.ToUpperInvariant(ch).ToString();
            }
        }

        return "·";
    }

    // ---- 行内下拉（服务的启动类型）----

    [ObservableProperty]
    private bool _showPicker;

    public IReadOnlyList<string> PickerItems { get; private set; } = [];

    /// <summary>填充下拉候选（需要在行创建之后设置）。</summary>
    public void SetPickerItems(IReadOnlyList<string> items) => PickerItems = items;

    [ObservableProperty]
    private string? _selectedPicker;

    private bool _pickerSilent;
    private string? _committedPicker;

    /// <summary>用户改下拉时调用；返回 false 表示失败并回退到原值。</summary>
    public Func<RowItem, string, Task<bool>>? PickerHandler { get; set; }

    /// <summary>程序同步状态时使用，不触发 <see cref="PickerHandler"/>。</summary>
    public void SetPickerSilently(string? value)
    {
        _pickerSilent = true;
        try
        {
            SelectedPicker = value;
            _committedPicker = value;
        }
        finally
        {
            _pickerSilent = false;
        }
    }

    partial void OnSelectedPickerChanged(string? value)
    {
        if (_pickerSilent || PickerHandler is null || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        _ = RunPickerAsync(value);
    }

    private async Task RunPickerAsync(string value)
    {
        var ok = false;

        try
        {
            ok = await PickerHandler!(this, value).ConfigureAwait(true);
        }
        catch
        {
            ok = false;
        }
        finally
        {
            if (ok)
            {
                _committedPicker = value;
            }
            else
            {
                SetPickerSilently(_committedPicker);
            }
        }
    }}
