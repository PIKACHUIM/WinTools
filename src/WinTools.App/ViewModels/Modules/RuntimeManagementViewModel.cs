using CommunityToolkit.Mvvm.Input;
using WinTools.App.Localization;
using WinTools.App.Services;
using WinTools.Core.Abstractions;
using WinTools.Core.Models;

namespace WinTools.App.ViewModels.Modules;

/// <summary>运行管理：启动项 / 计划任务 / 服务。</summary>
public sealed partial class RuntimeManagementViewModel : ModuleViewModelBase
{
    public RuntimeManagementViewModel(
        IModuleCatalog catalog,
        IStartupItemService startup,
        IScheduledTaskService tasks,
        IWindowsServiceManager services)
        : base(catalog, "Startup")
    {
        Register("StartupItems", new StartupItemsSectionViewModel(startup));
        Register("ScheduledTasks", new ScheduledTasksSectionViewModel(tasks));
        Register("Services", new ServicesSectionViewModel(services));
        SelectSection(Sections[0].Id);
    }
}

/// <summary>启动项：显示程序图标，开关直接启用/禁用（写 StartupApproved）。</summary>
public sealed partial class StartupItemsSectionViewModel : ListSectionViewModel
{
    private readonly IStartupItemService _startup;

    public StartupItemsSectionViewModel(IStartupItemService startup)
    {
        _startup = startup;
        HasPrimary = false;
        _ = ReloadAsync();
    }

    protected override async Task LoadAsync(CancellationToken cancellationToken)
    {
        var items = await _startup.GetAsync(cancellationToken).ConfigureAwait(true);

        Rows.Clear();

        foreach (var item in items)
        {
            var location = item.LocationKey == "Folder" ? Loc.T("Startup.Folder") : item.LocationKey;
            var row = new RowItem(
                item.Id,
                item.Name,
                item.Command,
                location,
                item.IsEnabled ? Loc.T("Common.Enabled") : Loc.T("Common.Disabled"),
                item.IsEnabled ? RowTone.Success : RowTone.Neutral)
            {
                Tag = item,
                IconSource = item.Command,
                ShowIcon = true,
                SearchBlob = $"{item.Name} {item.Command} {location}"
            };

            // 启用/禁用一律用开关；删除仍留在行尾
            row.ShowToggle = item.CanToggle;
            row.SetOnSilently(item.IsEnabled);
            row.ToggleHandler = (_, on) => SetEnabledAsync(item, on);

            row.ShowAction = true;
            row.ActionLabel = Loc.T("Common.Delete");
            row.ActionCommand = new AsyncRelayCommand(() => DeleteAsync(row, item));

            Rows.Add(row);
        }
    }

    private async Task<bool> SetEnabledAsync(StartupEntry entry, bool enabled)
    {
        var ok = false;

        await ExecuteAsync(async ct =>
        {
            var result = await _startup.SetEnabledAsync(entry, enabled, ct).ConfigureAwait(true);
            Report(result);
            ok = result.Success;

            if (ok)
            {
                await RefreshQuietlyAsync(ct).ConfigureAwait(true);
            }
        }).ConfigureAwait(true);

        return ok;
    }

    private async Task DeleteAsync(RowItem row, StartupEntry entry)
    {
        var confirmed = await UI.Confirm.AskAsync(new ConfirmRequest(
            Loc.T("Common.Delete"),
            Loc.F("Startup.ConfirmDelete", entry.Name),
            [entry.Command],
            Loc.T("Common.Delete"),
            Danger: true)).ConfigureAwait(true);

        if (!confirmed)
        {
            return;
        }

        row.IsActionEnabled = false;
        await ExecuteAsync(async ct =>
        {
            var result = await _startup.DeleteAsync(entry, ct).ConfigureAwait(true);
            Report(result);

            if (result.Success)
            {
                await RefreshQuietlyAsync(ct).ConfigureAwait(true);
            }
        }).ConfigureAwait(true);
        row.IsActionEnabled = true;
    }
}

/// <summary>计划任务：开关控制启用状态，行尾提供运行与删除。</summary>
public sealed partial class ScheduledTasksSectionViewModel : ListSectionViewModel
{
    private readonly IScheduledTaskService _tasks;

    public ScheduledTasksSectionViewModel(IScheduledTaskService tasks)
    {
        _tasks = tasks;
        HasPrimary = false;
        HasSecondary = false;
        _ = ReloadAsync();
    }

    protected override async Task LoadAsync(CancellationToken cancellationToken)
    {
        var tasks = await _tasks.GetAsync(cancellationToken).ConfigureAwait(true);

        Rows.Clear();

        foreach (var task in tasks)
        {
            var row = new RowItem(
                task.Path + "\\" + task.Name,
                task.Name,
                task.Path,
                task.TriggerSummary,
                task.State,
                task.IsEnabled ? RowTone.Neutral : RowTone.Warning)
            {
                Tag = task,
                SearchBlob = $"{task.Name} {task.Path} {task.Author}"
            };

            row.ShowToggle = true;
            row.SetOnSilently(task.IsEnabled);
            row.ToggleHandler = (_, on) => SetEnabledAsync(task, on);

            row.ShowAction = true;
            row.ActionLabel = Loc.T("Common.Run");
            row.ActionCommand = new AsyncRelayCommand(() => RunAsync(row, task));

            Rows.Add(row);
        }
    }

    private async Task<bool> SetEnabledAsync(ScheduledTaskEntry entry, bool enabled)
    {
        var ok = false;

        await ExecuteAsync(async ct =>
        {
            var result = await _tasks.SetEnabledAsync(entry, enabled, ct).ConfigureAwait(true);
            Report(result);
            ok = result.Success;

            if (ok)
            {
                await RefreshQuietlyAsync(ct).ConfigureAwait(true);
            }
        }).ConfigureAwait(true);

        return ok;
    }

    private async Task RunAsync(RowItem row, ScheduledTaskEntry entry)
    {
        row.IsActionEnabled = false;
        await ExecuteAsync(async ct =>
        {
            var result = await _tasks.RunAsync(entry, ct).ConfigureAwait(true);
            Report(result);
        }).ConfigureAwait(true);
        row.IsActionEnabled = true;
    }
}

/// <summary>服务：开关控制运行/停止，行内下拉选择启动类型。</summary>
public sealed partial class ServicesSectionViewModel : ListSectionViewModel
{
    private readonly IWindowsServiceManager _services;

    public ServicesSectionViewModel(IWindowsServiceManager services)
    {
        _services = services;
        HasPrimary = false;
        _ = ReloadAsync();
    }

    private static IReadOnlyList<string> StartTypeLabels =>
    [
        Loc.T("Svc.Auto"),
        Loc.T("Svc.DelayedAuto"),
        Loc.T("Svc.Manual"),
        Loc.T("Svc.Disabled")
    ];

    protected override async Task LoadAsync(CancellationToken cancellationToken)
    {
        var services = await _services.GetAsync(cancellationToken).ConfigureAwait(true);

        Rows.Clear();

        foreach (var service in services)
        {
            var running = string.Equals(service.Status, "Running", StringComparison.OrdinalIgnoreCase);

            var row = new RowItem(
                service.Name,
                service.DisplayName,
                service.Name,
                service.Description,
                service.Status,
                running ? RowTone.Success : RowTone.Neutral)
            {
                Tag = service,
                SearchBlob = $"{service.DisplayName} {service.Name} {service.Description}"
            };

            // 开关 = 运行 / 停止
            row.ShowToggle = true;
            row.SetOnSilently(running);
            row.ToggleHandler = (_, on) => ToggleRunningAsync(service, on);

            // 启动类型：行内下拉
            row.ShowPicker = true;
            row.SetPickerItems(StartTypeLabels);
            row.SetPickerSilently(LabelOf(service.StartType));
            row.PickerHandler = (_, label) => SetStartTypeAsync(service, label);

            Rows.Add(row);
        }
    }

    private static string LabelOf(string startType) => startType switch
    {
        "Auto" => Loc.T("Svc.Auto"),
        "DelayedAuto" => Loc.T("Svc.DelayedAuto"),
        "Manual" => Loc.T("Svc.Manual"),
        "Disabled" => Loc.T("Svc.Disabled"),
        _ => startType
    };

    private static string ValueOf(string label)
    {
        if (label == Loc.T("Svc.Auto"))
        {
            return "Auto";
        }

        if (label == Loc.T("Svc.DelayedAuto"))
        {
            return "DelayedAuto";
        }

        return label == Loc.T("Svc.Disabled") ? "Disabled" : "Manual";
    }

    private async Task<bool> ToggleRunningAsync(WindowsServiceEntry entry, bool start)
    {
        var ok = false;

        await ExecuteAsync(async ct =>
        {
            var result = start
                ? await _services.StartAsync(entry, ct).ConfigureAwait(true)
                : await _services.StopAsync(entry, ct).ConfigureAwait(true);

            Report(result);
            ok = result.Success;

            if (ok)
            {
                await RefreshQuietlyAsync(ct).ConfigureAwait(true);
            }
        }).ConfigureAwait(true);

        return ok;
    }

    private async Task<bool> SetStartTypeAsync(WindowsServiceEntry entry, string label)
    {
        var ok = false;
        var value = ValueOf(label);

        await ExecuteAsync(async ct =>
        {
            var result = await _services.SetStartTypeAsync(entry, value, ct).ConfigureAwait(true);
            Report(result);
            ok = result.Success;

            if (ok)
            {
                await RefreshQuietlyAsync(ct).ConfigureAwait(true);
            }
        }).ConfigureAwait(true);

        return ok;
    }
}
