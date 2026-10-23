using CommunityToolkit.Mvvm.Input;
using WinTools.App.Localization;
using WinTools.Core.Abstractions;
using WinTools.Core.Models;

namespace WinTools.App.ViewModels.Modules;

/// <summary>运行管理：启动项 / 计划任务 / 服务管理。</summary>
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

/// <summary>启动项：勾选态即启用态，应用后写入 StartupApproved。</summary>
public sealed partial class StartupItemsSectionViewModel : ListSectionViewModel
{
    private readonly IStartupItemService _startup;
    private readonly Dictionary<string, bool> _original = new(StringComparer.OrdinalIgnoreCase);

    public StartupItemsSectionViewModel(IStartupItemService startup)
    {
        _startup = startup;
        ShowCheckboxes = true;
        HasPrimary = true;
        PrimaryLabel = Loc.T("Common.Apply");
        _ = ReloadAsync();
    }

    protected override async Task LoadAsync(CancellationToken cancellationToken)
    {
        var items = await _startup.GetAsync(cancellationToken).ConfigureAwait(true);

        Rows.Clear();
        _original.Clear();

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
                SearchBlob = $"{item.Name} {item.Command} {location}"
            };

            row.ShowCheckbox = true;
            row.IsChecked = item.IsEnabled;
            row.ShowAction = true;
            row.ActionLabel = Loc.T("Common.Delete");
            row.ActionCommand = new AsyncRelayCommand(() => DeleteAsync(row, item));

            _original[item.Id] = item.IsEnabled;
            Rows.Add(row);
        }

    }

    protected override async Task PrimaryCoreAsync(CancellationToken cancellationToken)
    {
        var changed = 0;

        foreach (var row in Rows)
        {
            if (row.Tag is not StartupEntry entry)
            {
                continue;
            }

            if (_original.TryGetValue(entry.Id, out var was) && was == row.IsChecked)
            {
                continue;
            }

            var result = await _startup.SetEnabledAsync(entry, row.IsChecked, cancellationToken)
                .ConfigureAwait(true);

            if (result.Success)
            {
                changed++;
            }
        }

        Status = changed == 0 ? Loc.T("Common.NothingSelected") : Loc.F("Common.TotalCount", changed);
        await LoadAsync(cancellationToken).ConfigureAwait(true);
    }

    private async Task DeleteAsync(RowItem row, StartupEntry entry)
    {
        row.IsActionEnabled = false;
        await ExecuteAsync(async ct =>
        {
            var result = await _startup.DeleteAsync(entry, ct).ConfigureAwait(true);
            Report(result);

            if (result.Success)
            {
                await LoadAsync(ct).ConfigureAwait(true);
            }
        }).ConfigureAwait(true);

        row.IsActionEnabled = true;
    }
}

/// <summary>计划任务：启用/禁用、立即运行、删除。</summary>
public sealed partial class ScheduledTasksSectionViewModel : ListSectionViewModel
{
    private readonly IScheduledTaskService _tasks;

    public ScheduledTasksSectionViewModel(IScheduledTaskService tasks)
    {
        _tasks = tasks;
        ShowCheckboxes = true;
        HasPrimary = true;
        PrimaryLabel = Loc.T("Common.Run");
        HasSecondary = true;
        SecondaryLabel = Loc.T("Common.Delete");
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

            row.ShowCheckbox = true;
            row.ShowAction = true;
            row.ActionLabel = task.IsEnabled ? Loc.T("Common.Disable") : Loc.T("Common.Enable");
            row.ActionCommand = new AsyncRelayCommand(() => ToggleAsync(row, task));
            Rows.Add(row);
        }

    }

    private async Task ToggleAsync(RowItem row, ScheduledTaskEntry entry)
    {
        row.IsActionEnabled = false;
        await ExecuteAsync(async ct =>
        {
            var result = await _tasks.SetEnabledAsync(entry, !entry.IsEnabled, ct).ConfigureAwait(true);
            Report(result);

            if (result.Success)
            {
                await LoadAsync(ct).ConfigureAwait(true);
            }
        }).ConfigureAwait(true);

        row.IsActionEnabled = true;
    }

    protected override async Task PrimaryCoreAsync(CancellationToken cancellationToken)
    {
        var selected = CheckedRows;
        foreach (var row in selected)
        {
            if (row.Tag is ScheduledTaskEntry entry)
            {
                await _tasks.RunAsync(entry, cancellationToken).ConfigureAwait(true);
            }
        }

        Status = Loc.F("Common.TotalCount", selected.Count);
    }

    protected override async Task SecondaryCoreAsync(CancellationToken cancellationToken)
    {
        var selected = CheckedRows;
        foreach (var row in selected)
        {
            if (row.Tag is ScheduledTaskEntry entry)
            {
                await _tasks.DeleteAsync(entry, cancellationToken).ConfigureAwait(true);
            }
        }

        await LoadAsync(cancellationToken).ConfigureAwait(true);
    }
}

/// <summary>服务：状态、启停与启动类型调整。</summary>
public sealed partial class ServicesSectionViewModel : ListSectionViewModel
{
    private readonly IWindowsServiceManager _services;

    public ServicesSectionViewModel(IWindowsServiceManager services)
    {
        _services = services;
        ShowCheckboxes = true;
        HasPrimary = true;
        PrimaryLabel = Loc.T("Svc.SetAuto");
        HasSecondary = true;
        SecondaryLabel = Loc.T("Svc.SetManual");
        _ = ReloadAsync();
    }

    protected override async Task LoadAsync(CancellationToken cancellationToken)
    {
        var services = await _services.GetAsync(cancellationToken).ConfigureAwait(true);

        Rows.Clear();
        foreach (var service in services)
        {
            var startType = service.StartType switch
            {
                "Auto" => Loc.T("Svc.Auto"),
                "Manual" => Loc.T("Svc.Manual"),
                "Disabled" => Loc.T("Svc.Disabled"),
                _ => service.StartType
            };

            var running = service.Status.Equals("Running", StringComparison.OrdinalIgnoreCase);
            var stateText = running ? Loc.T("Svc.Running") : Loc.T("Svc.Stopped");

            var row = new RowItem(
                service.Name,
                service.DisplayName,
                service.Name + " · " + startType,
                null,
                stateText,
                running ? RowTone.Success : RowTone.Neutral)
            {
                Tag = service,
                SearchBlob = $"{service.Name} {service.DisplayName} {service.Description}"
            };

            row.ShowCheckbox = true;
            row.ShowAction = service.CanStop || !running;
            row.ActionLabel = running ? Loc.T("Common.Stop") : Loc.T("Common.Start");
            row.ActionCommand = new AsyncRelayCommand(() => ToggleAsync(row, service, running));
            Rows.Add(row);
        }

    }

    private async Task ToggleAsync(RowItem row, WindowsServiceEntry entry, bool running)
    {
        row.IsActionEnabled = false;
        await ExecuteAsync(async ct =>
        {
            var result = running
                ? await _services.StopAsync(entry, ct).ConfigureAwait(true)
                : await _services.StartAsync(entry, ct).ConfigureAwait(true);

            Report(result);
            await LoadAsync(ct).ConfigureAwait(true);
        }).ConfigureAwait(true);

        row.IsActionEnabled = true;
    }

    protected override async Task PrimaryCoreAsync(CancellationToken cancellationToken)
        => await SetStartTypeAsync("Auto", cancellationToken).ConfigureAwait(true);

    protected override async Task SecondaryCoreAsync(CancellationToken cancellationToken)
        => await SetStartTypeAsync("Manual", cancellationToken).ConfigureAwait(true);

    private async Task SetStartTypeAsync(string startType, CancellationToken cancellationToken)
    {
        var selected = CheckedRows;
        if (selected.Count == 0)
        {
            Status = Loc.T("Common.NothingSelected");
            return;
        }

        foreach (var row in selected)
        {
            if (row.Tag is WindowsServiceEntry entry)
            {
                await _services.SetStartTypeAsync(entry, startType, cancellationToken).ConfigureAwait(true);
            }
        }

        await LoadAsync(cancellationToken).ConfigureAwait(true);
    }
}
