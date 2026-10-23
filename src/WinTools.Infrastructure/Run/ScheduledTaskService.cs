using WinTools.Core.Abstractions;
using WinTools.Core.Models;

namespace WinTools.Infrastructure.Run;

/// <summary>计划任务：通过 Schedule.Service COM 枚举与操作。</summary>
public sealed class ScheduledTaskService : IScheduledTaskService
{
    private readonly IAppLogger _logger;

    public ScheduledTaskService(IAppLogger logger) => _logger = logger;

    public Task<IReadOnlyList<ScheduledTaskEntry>> GetAsync(CancellationToken cancellationToken) => Task.Run(() =>
    {
        var list = new List<ScheduledTaskEntry>();

        try
        {
            dynamic service = CreateService();
            dynamic root = service.GetFolder("\\");
            Walk(root, list, cancellationToken, depth: 0);
        }
        catch (Exception ex)
        {
            _logger.Error("Tasks", "枚举计划任务失败", ex);
        }

        return (IReadOnlyList<ScheduledTaskEntry>)list;
    }, cancellationToken);

    public Task<OperationResult> SetEnabledAsync(
        ScheduledTaskEntry entry,
        bool enabled,
        CancellationToken cancellationToken) => Task.Run(() =>
    {
        try
        {
            dynamic task = GetTask(entry);
            task.Enabled = enabled;
            _logger.Audit(enabled ? "Task.Enable" : "Task.Disable", entry.Path + "\\" + entry.Name);
            return OperationResult.Ok(enabled ? "任务已启用" : "任务已禁用", 1);
        }
        catch (Exception ex)
        {
            _logger.Error("Tasks", "修改计划任务失败", ex);
            return OperationResult.Fail(ex.Message);
        }
    }, cancellationToken);

    public Task<OperationResult> RunAsync(ScheduledTaskEntry entry, CancellationToken cancellationToken) => Task.Run(() =>
    {
        try
        {
            dynamic task = GetTask(entry);
            task.Run(null);
            _logger.Audit("Task.Run", entry.Path + "\\" + entry.Name);
            return OperationResult.Ok("任务已触发", 1);
        }
        catch (Exception ex)
        {
            _logger.Error("Tasks", "运行计划任务失败", ex);
            return OperationResult.Fail(ex.Message);
        }
    }, cancellationToken);

    public Task<OperationResult> DeleteAsync(ScheduledTaskEntry entry, CancellationToken cancellationToken) => Task.Run(() =>
    {
        try
        {
            dynamic service = CreateService();
            dynamic folder = service.GetFolder(entry.Path);
            folder.DeleteTask(entry.Name, 0);
            _logger.Audit("Task.Delete", entry.Path + "\\" + entry.Name);
            return OperationResult.Ok("任务已删除", 1);
        }
        catch (Exception ex)
        {
            _logger.Error("Tasks", "删除计划任务失败", ex);
            return OperationResult.Fail(ex.Message);
        }
    }, cancellationToken);

    private static dynamic CreateService()
    {
        var type = Type.GetTypeFromProgID("Schedule.Service")
                   ?? throw new InvalidOperationException("任务计划服务不可用");
        dynamic service = Activator.CreateInstance(type)!;
        service.Connect();
        return service;
    }

    private static dynamic GetTask(ScheduledTaskEntry entry)
    {
        dynamic service = CreateService();
        dynamic folder = service.GetFolder(entry.Path);
        return folder.GetTask(entry.Name);
    }

    private static void Walk(dynamic folder, List<ScheduledTaskEntry> list, CancellationToken cancellationToken, int depth)
    {
        if (depth > 6 || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        string folderPath = "\\";
        try
        {
            folderPath = Convert.ToString(folder.Path) ?? "\\";
        }
        catch
        {
            // 忽略
        }

        try
        {
            dynamic tasks = folder.GetTasks(1);
            foreach (dynamic task in tasks)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                try
                {
                    var name = Convert.ToString(task.Name) ?? string.Empty;
                    var enabled = Convert.ToBoolean(task.Enabled);
                    var state = Convert.ToInt32(task.State);

                    string? author = null;
                    var triggerCount = 0;
                    try
                    {
                        author = Convert.ToString(task.Definition.RegistrationInfo.Author);
                        triggerCount = Convert.ToInt32(task.Definition.Triggers.Count);
                    }
                    catch
                    {
                        // 部分任务无权读取定义
                    }

                    list.Add(new ScheduledTaskEntry(
                        folderPath,
                        name,
                        StateText(state),
                        triggerCount > 0 ? $"{triggerCount} 个触发器" : null,
                        enabled,
                        string.IsNullOrWhiteSpace(author) ? null : author));
                }
                catch
                {
                    // 跳过单个读取失败的任务
                }
            }
        }
        catch
        {
            // 目录不可枚举
        }

        try
        {
            dynamic subFolders = folder.GetFolders(0);
            foreach (dynamic sub in subFolders)
            {
                Walk(sub, list, cancellationToken, depth + 1);
            }
        }
        catch
        {
            // 忽略
        }
    }

    private static string StateText(int state) => state switch
    {
        1 => "已禁用",
        2 => "排队中",
        3 => "就绪",
        4 => "运行中",
        _ => "未知"
    };
}
