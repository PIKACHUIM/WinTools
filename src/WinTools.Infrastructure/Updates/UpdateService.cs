using WinTools.Core.Abstractions;
using WinTools.Core.Models;

namespace WinTools.Infrastructure.Updates;

/// <summary>
/// 更新管理：通过 WUA（Microsoft.Update.Session）COM 查询与安装更新。
/// 使用后期绑定 COM，避免引入 COM 互操作程序集。
/// </summary>
public sealed class UpdateService : IUpdateService
{
    private readonly IAppLogger _logger;
    private readonly Dictionary<string, object> _availableCache = new(StringComparer.OrdinalIgnoreCase);

    public UpdateService(IAppLogger logger) => _logger = logger;

    public Task<IReadOnlyList<UpdateEntry>> GetInstalledAsync(CancellationToken cancellationToken) => Task.Run(() =>
    {
        var list = new List<UpdateEntry>();

        try
        {
            dynamic session = CreateSession();
            dynamic searcher = session.CreateUpdateSearcher();
            dynamic result = searcher.Search("IsInstalled=1 AND Type='Software'");

            foreach (dynamic update in result.Updates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                list.Add(Map(update, isInstalled: true));
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Updates", "查询已安装更新失败", ex);
        }

        return (IReadOnlyList<UpdateEntry>)list
            .OrderByDescending(u => u.Date ?? DateTime.MinValue)
            .ToList();
    }, cancellationToken);

    public Task<IReadOnlyList<UpdateEntry>> SearchAvailableAsync(
        IProgress<string>? progress,
        CancellationToken cancellationToken) => Task.Run(() =>
    {
        var list = new List<UpdateEntry>();
        _availableCache.Clear();

        try
        {
            progress?.Report("正在连接 Windows 更新服务…");
            dynamic session = CreateSession();
            dynamic searcher = session.CreateUpdateSearcher();
            searcher.Online = true;

            progress?.Report("正在搜索可用更新（可能需要数分钟）…");
            dynamic result = searcher.Search("IsInstalled=0 AND IsHidden=0 AND Type='Software'");

            foreach (dynamic update in result.Updates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var entry = Map(update, isInstalled: false);
                list.Add(entry);

                try
                {
                    _availableCache[entry.Id] = update;
                }
                catch
                {
                    // 缓存失败不影响列表
                }
            }

            progress?.Report($"搜索完成，共 {list.Count} 个可用更新");
            _logger.Info("Updates", $"搜索到 {list.Count} 个可用更新");
        }
        catch (Exception ex)
        {
            _logger.Error("Updates", "搜索可用更新失败", ex);
            progress?.Report("搜索失败: " + ex.Message);
        }

        return (IReadOnlyList<UpdateEntry>)list;
    }, cancellationToken);

    public Task<OperationResult> InstallAsync(
        IReadOnlyCollection<string> updateIds,
        IProgress<string>? progress,
        CancellationToken cancellationToken) => Task.Run<OperationResult>(() =>
    {
        try
        {
            dynamic session = CreateSession();
            dynamic collection = session.CreateUpdateCollection();

            var missing = 0;
            foreach (var id in updateIds)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!_availableCache.TryGetValue(id, out var cached))
                {
                    missing++;
                    continue;
                }

                collection.Add(cached);
            }

            var count = collection.Count;
            if (count == 0)
            {
                return OperationResult.Fail("没有可安装的更新，请先重新搜索");
            }

            progress?.Report($"正在下载 {count} 个更新…");
            dynamic downloader = session.CreateUpdateDownloader();
            downloader.Updates = collection;
            var downloadResult = downloader.Download();
            if (Convert.ToInt32(downloadResult.ResultCode) is not (2 or 3))
            {
                return OperationResult.Fail($"下载失败，结果码 {downloadResult.ResultCode}");
            }

            progress?.Report("正在安装…");
            dynamic installer = session.CreateUpdateInstaller();
            installer.Updates = collection;
            dynamic installResult = installer.Install();

            var code = Convert.ToInt32(installResult.ResultCode);
            var reboot = Convert.ToBoolean(installResult.RebootRequired);
            _logger.Audit("Update.Install", string.Join(",", updateIds), $"结果码 {code}");

            if (code is 2 or 3)
            {
                return OperationResult.Ok(
                    missing > 0 ? $"已安装 {count} 个更新（{missing} 个需重新搜索）" : $"已安装 {count} 个更新",
                    count,
                    reboot);
            }

            return OperationResult.Fail($"安装失败，结果码 {code}");
        }
        catch (Exception ex)
        {
            _logger.Error("Updates", "安装更新失败", ex);
            return OperationResult.Fail(ex.Message);
        }
    }, cancellationToken);

    public Task<OperationResult> UninstallAsync(string updateId, CancellationToken cancellationToken) => Task.Run<OperationResult>(() =>
    {
        try
        {
            dynamic session = CreateSession();
            dynamic searcher = session.CreateUpdateSearcher();
            dynamic result = searcher.Search($"UpdateID='{updateId}' AND IsInstalled=1");

            dynamic updates = result.Updates;
            if (updates is null || Convert.ToInt32(updates.Count) == 0)
            {
                return OperationResult.Fail("未找到该更新");
            }

            dynamic update = updates!.Item(0);
            dynamic uninstallResult = update.Uninstall();
            var code = Convert.ToInt32(uninstallResult.ResultCode);
            var reboot = Convert.ToBoolean(uninstallResult.RebootRequired);

            _logger.Audit("Update.Uninstall", updateId, $"结果码 {code}");

            return code is 2 or 3
                ? OperationResult.Ok("更新已卸载", 1, reboot)
                : OperationResult.Fail($"卸载失败，结果码 {code}");
        }
        catch (Exception ex)
        {
            _logger.Error("Updates", "卸载更新失败", ex);
            return OperationResult.Fail(ex.Message);
        }
    }, cancellationToken);

    private static dynamic CreateSession()
    {
        var type = Type.GetTypeFromProgID("Microsoft.Update.Session")
                   ?? throw new InvalidOperationException("Windows Update 服务不可用");
        return Activator.CreateInstance(type)!;
    }

    private static UpdateEntry Map(dynamic update, bool isInstalled)
    {
        string title = "Update";
        string id = Guid.NewGuid().ToString();
        string? kb = null;
        DateTime? date = null;
        long? size = null;
        string? severity = null;

        try
        {
            title = Convert.ToString(update.Title) ?? title;
            id = Convert.ToString(update.Identity.UpdateID) ?? id;

            var kbIds = update.KBArticleIDs;
            if (kbIds is not null && Convert.ToInt32(kbIds.Count) > 0)
            {
                kb = "KB" + Convert.ToString(kbIds!.Item(0));
            }

            date = Convert.ToDateTime(update.LastDeploymentChangeTime);
            size = Convert.ToInt64(update.MaxDownloadSize);
        }
        catch
        {
            // 部分字段不可用时保持默认
        }

        return new UpdateEntry(id, title, kb, date, size, isInstalled, severity);
    }
}
