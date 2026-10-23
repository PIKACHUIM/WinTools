using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using WinTools.App.Localization;
using WinTools.App.ViewModels.Modules;
using WinTools.Core.Abstractions;
using WinTools.Core.Models;

namespace WinTools.App.ViewModels;

public sealed partial class ShellViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly LocalizationService _localization;
    private readonly IServiceProvider _provider;
    private readonly Dictionary<string, object> _pages = new(StringComparer.OrdinalIgnoreCase);
    private bool _initialized;

    public ShellViewModel(
        ISettingsService settings,
        LocalizationService localization,
        IServiceProvider provider,
        IModuleCatalog catalog)
    {
        _settings = settings;
        _localization = localization;
        _provider = provider;

        foreach (var module in catalog.Modules.OrderBy(m => m.Order))
        {
            var definition = module;
            var item = new NavItemViewModel(
                definition.GetNavKey(),
                definition.Glyph,
                () => GetPage(definition));

            foreach (var section in definition.Sections)
            {
                var sectionId = section.Id;
                item.Children.Add(new NavItemViewModel(
                    definition.GetNavKey() + "." + sectionId,
                    string.Empty,
                    () => GetPage(definition),
                    section.Title,
                    sectionId,
                    depth: 1));
            }

            if (definition.IsFooter)
            {
                FooterNavItems.Add(item);
            }
            else
            {
                NavItems.Add(item);
            }
        }

        _localization.LanguageChanged += (_, _) => OnLanguageChanged();
        IsRightToLeft = _localization.IsRightToLeft;

        // 注意：此处不能立即选中首项 —— 页面会反向依赖 ShellViewModel，
        // 构造期解析页面会触发 DI 循环依赖。首页选择放在 Initialize()（窗口 Loaded 后）。
    }

    public ObservableCollection<NavItemViewModel> NavItems { get; } = [];

    public ObservableCollection<NavItemViewModel> FooterNavItems { get; } = [];

    [ObservableProperty]
    private NavItemViewModel? _selectedNav;

    [ObservableProperty]
    private object? _currentPage;

    [ObservableProperty]
    private bool _isRightToLeft;

    /// <summary>页面切换通知（供需要"可见才刷新"的页面订阅，例如概览页实时采样）。</summary>
    public event EventHandler<object>? CurrentPageChanged;

    public string AppTitle => Loc.T("App.Name");

    public string AppSubtitle => Loc.T("App.Subtitle");

    private IEnumerable<NavItemViewModel> AllItems =>
        NavItems.Concat(FooterNavItems).SelectMany(i => new[] { i }.Concat(i.Children));

    partial void OnSelectedNavChanged(NavItemViewModel? value)
    {
        if (value is null)
        {
            return;
        }

        foreach (var item in AllItems)
        {
            item.IsSelected = ReferenceEquals(item, value);
        }

        var parent = FindParent(value);
        if (parent is not null)
        {
            parent.IsExpanded = true;
        }

        var page = value.Page;
        CurrentPage = page;
        CurrentPageChanged?.Invoke(this, page);

        // 侧边栏二级菜单 → 页面内页签
        if (value.SectionId is not null && page is ModuleViewModelBase module)
        {
            module.SelectSection(value.SectionId);
        }

        _settings.Current.LastPage = value.Key;
    }

    public void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;

        var last = _settings.Current.LastPage;
        var item = string.IsNullOrEmpty(last)
            ? null
            : AllItems.FirstOrDefault(i => string.Equals(i.Key, last, StringComparison.OrdinalIgnoreCase));

        SelectedNav = item ?? NavItems.FirstOrDefault();
    }

    /// <summary>导航到指定菜单项（一级 Key 或 "Module.Section"）。</summary>
    [RelayCommand]
    public void Navigate(string? key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        var item = AllItems.FirstOrDefault(i => string.Equals(i.Key, key, StringComparison.OrdinalIgnoreCase));
        if (item is null)
        {
            return;
        }

        // 点击一级菜单：切换展开状态
        if (item.HasChildren && !item.IsSelected)
        {
            item.IsExpanded = true;
        }
        else if (item.HasChildren)
        {
            item.IsExpanded = !item.IsExpanded;
        }

        SelectedNav = item;
    }

    /// <summary>页面内页签切换后回同步侧边栏高亮。</summary>
    private void NavigateToSection(string moduleKey, string sectionId)
        => Navigate(moduleKey + "." + sectionId);

    private NavItemViewModel? FindParent(NavItemViewModel child)
        => NavItems.Concat(FooterNavItems).FirstOrDefault(p => p.Children.Contains(child));

    private object GetPage(ModuleDefinition module)
    {
        if (_pages.TryGetValue(module.Id, out var cached))
        {
            return cached;
        }

        object page = module.Page switch
        {
            "Dashboard" => _provider.GetRequiredService<DashboardViewModel>(),
            "Cleanup" => _provider.GetRequiredService<CleanupViewModel>(),
            "Optimize" => _provider.GetRequiredService<OptimizeViewModel>(),
            "Settings" => _provider.GetRequiredService<SettingsViewModel>(),
            "FeatureManagement" => _provider.GetRequiredService<FeatureManagementViewModel>(),
            "UpdateManagement" => _provider.GetRequiredService<UpdateManagementViewModel>(),
            "RuntimeManagement" => _provider.GetRequiredService<RuntimeManagementViewModel>(),
            "ComponentManagement" => _provider.GetRequiredService<ComponentManagementViewModel>(),
            "RuntimeEnvironment" => _provider.GetRequiredService<RuntimeEnvironmentViewModel>(),
            "AppManagement" => _provider.GetRequiredService<AppManagementViewModel>(),
            "DriverManagement" => _provider.GetRequiredService<DriverManagementViewModel>(),
            "BackupRestore" => _provider.GetRequiredService<BackupRestoreViewModel>(),
            _ => new UnimplementedModuleViewModel(module)
        };

        if (page is ModuleViewModelBase moduleViewModel)
        {
            var moduleKey = module.GetNavKey();
            moduleViewModel.SectionChanged += (_, sectionId) => NavigateToSection(moduleKey, sectionId);
        }

        _pages[module.Id] = page;
        return page;
    }

    private void OnLanguageChanged()
    {
        foreach (var item in NavItems.Concat(FooterNavItems))
        {
            item.RefreshTitle();
        }

        OnPropertyChanged(nameof(AppTitle));
        OnPropertyChanged(nameof(AppSubtitle));
        IsRightToLeft = _localization.IsRightToLeft;

        if (CurrentPage is ILocalizable localizable)
        {
            localizable.RefreshLocalization();
        }
    }
}
