using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WinTools.App.Localization;
using WinTools.Core.Abstractions;
using WinTools.Core.Models;

namespace WinTools.App.ViewModels.Modules;

/// <summary>模块内的一个页签（对应 modules.json 的 section）。</summary>
public sealed partial class SectionItem : ObservableObject
{
    private readonly LocalizedText? _title;
    private readonly LocalizedText? _description;

    public SectionItem(string id, object content, LocalizedText? title, LocalizedText? description)
    {
        Id = id;
        Content = content;
        _title = title;
        _description = description;
    }

    public string Id { get; }

    public object Content { get; }

    public string Title
    {
        get
        {
            var resolved = _title?.Resolve(Loc.CurrentCulture);
            return string.IsNullOrWhiteSpace(resolved) ? Loc.T("Sec." + Id) : resolved;
        }
    }

    public string? Description
    {
        get
        {
            var resolved = _description?.Resolve(Loc.CurrentCulture);
            return string.IsNullOrWhiteSpace(resolved) ? null : resolved;
        }
    }

    public void Refresh()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Description));
    }
}

/// <summary>
/// 模块页基类：统一「标题 + 二级页签 + 内容」结构与侧边栏二级菜单的同步。
/// </summary>
public abstract partial class ModuleViewModelBase : ObservableObject, ILocalizable
{
    private readonly Dictionary<string, ModuleSection> _definitions;
    private bool _syncing;

    protected ModuleViewModelBase(IModuleCatalog catalog, string moduleId)
    {
        var module = catalog.Modules.FirstOrDefault(m =>
                         string.Equals(m.Id, moduleId, StringComparison.OrdinalIgnoreCase))
                     ?? new ModuleDefinition { Id = moduleId };

        ModuleId = module.Id;
        Glyph = module.Glyph;
        _definitions = module.Sections.ToDictionary(s => s.Id, StringComparer.OrdinalIgnoreCase);
    }

    public string ModuleId { get; }

    public string Glyph { get; }

    public ObservableCollection<SectionItem> Sections { get; } = [];

    [ObservableProperty]
    private SectionItem? _selectedSection;

    public bool HasSections => Sections.Count > 0;

    public string Title => Loc.T("Nav." + ModuleId);

    public string? SectionDescription => SelectedSection?.Description;

    /// <summary>当前页签的公共工具栏（搜索 / 计数 / 主次操作 / 刷新）；页签内容不使用公共工具栏时为 null。</summary>
    public SectionViewModelBase? Toolbar => SelectedSection?.Content as SectionViewModelBase;

    public bool HasToolbar => Toolbar is not null;

    /// <summary>页签切换时通知外壳同步侧边栏高亮。</summary>
    public event EventHandler<string>? SectionChanged;

    protected void Register(string sectionId, object content)
    {
        _definitions.TryGetValue(sectionId, out var definition);
        Sections.Add(new SectionItem(sectionId, content, definition?.Title, definition?.Description));
    }

    /// <summary>由外壳（侧边栏点击 / 记忆恢复）驱动页签切换，不反向通知。</summary>
    public void SelectSection(string? sectionId)
    {
        var target = Sections.FirstOrDefault(s => string.Equals(s.Id, sectionId, StringComparison.OrdinalIgnoreCase))
                     ?? Sections.FirstOrDefault();

        if (target is null || ReferenceEquals(target, SelectedSection))
        {
            return;
        }

        _syncing = true;
        SelectedSection = target;
        _syncing = false;
    }

    partial void OnSelectedSectionChanged(SectionItem? value)
    {
        OnPropertyChanged(nameof(SectionDescription));
        OnPropertyChanged(nameof(Toolbar));
        OnPropertyChanged(nameof(HasToolbar));

        if (!_syncing && value is not null)
        {
            SectionChanged?.Invoke(this, value.Id);
        }
    }

    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(SectionDescription));

        foreach (var section in Sections)
        {
            section.Refresh();

            if (section.Content is ILocalizable localizable)
            {
                localizable.RefreshLocalization();
            }
        }
    }
}

/// <summary>兜底模块页：modules.json 中出现了尚未注册页面类型的模块时使用。</summary>
public sealed class UnimplementedModuleViewModel : ModuleViewModelBase
{
    public UnimplementedModuleViewModel(ModuleDefinition module)
        : base(new SingleModuleCatalog(module), module.Id)
    {
    }

    private sealed class SingleModuleCatalog : IModuleCatalog
    {
        public SingleModuleCatalog(ModuleDefinition module) => Modules = [module];

        public IReadOnlyList<ModuleDefinition> Modules { get; }
    }
}
