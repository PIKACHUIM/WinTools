using System.Reflection;
using System.Text;
using System.Text.Json;
using WinTools.Core.Abstractions;
using WinTools.Core.Models;

namespace WinTools.Infrastructure.Rules;

/// <summary>规则仓储：内嵌 JSON 资源，启动时一次性加载（约 105 + 40 条，&lt;5ms）。</summary>
public sealed class RuleRepository : IModuleCatalog
{
    private readonly IAppLogger _logger;

    public RuleRepository(IAppLogger logger)
    {
        _logger = logger;
        Modules = (Load<List<ModuleDefinition>>("modules.json") ?? [])
            .OrderBy(m => m.Order).ToList();
        var baseRules = Load<List<CleanupRule>>("cleanup-rules.json") ?? [];
        var extraRules = Load<List<CleanupRule>>("cleanup-rules-extra.json") ?? [];
        Rules = baseRules.Concat(extraRules).ToList();
        Categories = (Load<List<CleanupCategory>>("cleanup-categories.json") ?? [])
            .OrderBy(c => c.Order).ToList();
        var baseTweaks = Load<List<TweakDefinition>>("tweaks.json") ?? [];
        var extraTweaks = Load<List<TweakDefinition>>("tweaks-extra.json") ?? [];
        // 由 tools/Sync-ReferenceTweaks.ps1 从预置的 winutil 快照生成
        var referenceTweaks = Load<List<TweakDefinition>>("tweaks-reference.json") ?? [];
        Tweaks = baseTweaks.Concat(extraTweaks).Concat(referenceTweaks).Where(t => t.Enabled).ToList();
        TweakGroups = (Load<List<TweakGroup>>("tweak-groups.json") ?? [])
            .OrderBy(g => g.Order).ToList();

        _logger.Info("Rules", $"已加载 {Modules.Count} 个模块 / {Rules.Count} 条清理规则 / {Categories.Count} 个分类 / {Tweaks.Count} 个优化项");
    }

    public IReadOnlyList<ModuleDefinition> Modules { get; }

    public IReadOnlyList<CleanupRule> Rules { get; }

    public IReadOnlyList<CleanupCategory> Categories { get; }

    public IReadOnlyList<TweakDefinition> Tweaks { get; }

    public IReadOnlyList<TweakGroup> TweakGroups { get; }

    private T? Load<T>(string fileName) where T : class
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));

            if (resourceName is null)
            {
                _logger.Error("Rules", $"内嵌资源缺失: {fileName}");
                return null;
            }

            using var stream = assembly.GetManifestResourceStream(resourceName)!;
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return JsonSerializer.Deserialize<T>(reader.ReadToEnd(), JsonConfig.Options);
        }
        catch (Exception ex)
        {
            _logger.Error("Rules", $"解析 {fileName} 失败", ex);
            return null;
        }
    }
}
