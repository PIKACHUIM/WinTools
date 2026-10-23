using WinTools.Core.Abstractions;
using WinTools.Core.Models;
using WinTools.Infrastructure.Rules;

namespace WinTools.Infrastructure.Cleanup;

public sealed class CleanupService : ICleanupService
{
    private readonly RuleRepository _repository;
    private readonly CleanupScanner _scanner;
    private readonly CleanupExecutor _executor;
    private readonly ISettingsService _settings;

    public CleanupService(
        RuleRepository repository,
        CleanupScanner scanner,
        CleanupExecutor executor,
        ISettingsService settings)
    {
        _repository = repository;
        _scanner = scanner;
        _executor = executor;
        _settings = settings;
    }

    public IReadOnlyList<CleanupCategory> Categories => _repository.Categories;

    public IReadOnlyList<CleanupRule> Rules => _repository.Rules;

    public Task<CleanupScanSummary> ScanAsync(
        IReadOnlyCollection<string> enabledRuleIds,
        IReadOnlyList<string>? scopeRoots,
        IProgress<CleanupProgress>? progress,
        CancellationToken cancellationToken)
    {
        var ids = enabledRuleIds as ISet<string> ?? new HashSet<string>(enabledRuleIds, StringComparer.OrdinalIgnoreCase);
        var rules = _repository.Rules
            .Where(r => r.IsCleanable && ids.Contains(r.Id))
            .ToList();

        var concurrency = _settings.Current.ScanConcurrency > 0
            ? _settings.Current.ScanConcurrency
            : Math.Clamp(Environment.ProcessorCount / 2, 2, 4);

        return _scanner.ScanAsync(rules, concurrency, scopeRoots, progress, cancellationToken);
    }

    public Task<CleanupReport> CleanAsync(
        CleanupRequest request,
        IProgress<CleanupProgress>? progress,
        CancellationToken cancellationToken)
        => Task.Run(() => _executor.Execute(request, progress, cancellationToken), cancellationToken);
}
