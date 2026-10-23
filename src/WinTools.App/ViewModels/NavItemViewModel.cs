using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using WinTools.App.Localization;
using WinTools.Core.Models;

namespace WinTools.App.ViewModels;

/// <summary>侧边栏菜单项：一级模块可含二级页签，点击即导航。</summary>
public sealed partial class NavItemViewModel : ObservableObject
{
    private readonly Func<object> _factory;
    private readonly LocalizedText? _titleText;
    private object? _page;

    public NavItemViewModel(
        string key,
        string glyph,
        Func<object> factory,
        LocalizedText? titleText = null,
        string? sectionId = null,
        int depth = 0)
    {
        Key = key;
        Glyph = glyph;
        _factory = factory;
        _titleText = titleText;
        SectionId = sectionId;
        Depth = depth;
    }

    public string Key { get; }

    public string Glyph { get; }

    /// <summary>0 = 一级菜单，1 = 二级页签。</summary>
    public int Depth { get; }

    /// <summary>二级菜单对应的模块内页签 Id；一级菜单为 null。</summary>
    public string? SectionId { get; }

    public ObservableCollection<NavItemViewModel> Children { get; } = [];

    public bool HasChildren => Children.Count > 0;

    public bool ShowGlyph => Depth == 0;

    public bool ShowChevron => HasChildren;

    public Thickness RowPadding => Depth == 0 ? new Thickness(12, 0, 10, 0) : new Thickness(32, 0, 10, 0);

    public double TitleFontSize => Depth == 0 ? 13d : 12.5d;

    public string Title
    {
        get
        {
            if (_titleText is not null)
            {
                var resolved = _titleText.Resolve(Loc.CurrentCulture);
                if (!string.IsNullOrWhiteSpace(resolved))
                {
                    return resolved;
                }
            }

            return Loc.T("Nav." + Key);
        }
    }

    public object Page => _page ??= _factory();

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isExpanded;

    public void RefreshTitle()
    {
        OnPropertyChanged(nameof(Title));
        foreach (var child in Children)
        {
            child.RefreshTitle();
        }
    }
}

/// <summary>语言切换后需要刷新文案的页面。</summary>
public interface ILocalizable
{
    void RefreshLocalization();
}
