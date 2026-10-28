using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinTools.App.Localization;
using WinTools.App.Services;
using WinTools.Core.Abstractions;
using WinTools.Core.Models;
using WinTools.Core.Utils;
using HardwareService = WinTools.Infrastructure.System.HardwareInfoService;

namespace WinTools.App.ViewModels;

/// <summary>迁移列表的一行。</summary>
public sealed partial class MigrationRowViewModel : ObservableObject
{
    public MigrationRowViewModel(MigrationTarget target)
    {
        Target = target;
        Name = target.Name.Resolve(Loc.CurrentCulture);
        Description = target.Description?.Resolve(Loc.CurrentCulture) ?? string.Empty;
        CurrentPath = target.CurrentPath;
        KindText = Loc.T(target.Kind == MigrationKind.KnownFolder ? "Migrate.Kind.System" : "Migrate.Kind.App");
        IsSelected = true;
        ActionIndex = 0;
    }

    public MigrationTarget Target { get; }

    public string Name { get; }

    public string Description { get; }

    public string CurrentPath { get; }

    public string KindText { get; }

    public bool HasContent => true;

    [ObservableProperty]
    private long _sizeBytes;

    [ObservableProperty]
    private long _fileCount;

    [ObservableProperty]
    private bool _isMeasuring = true;

    public string SizeText => IsMeasuring
        ? Loc.T("Migrate.Measuring")
        : SizeBytes > 0 ? SizeFormatter.Format(SizeBytes) : "—";

    public string FileText => IsMeasured ? Loc.F("Migrate.FileCount", FileCount) : string.Empty;

    partial void OnSizeBytesChanged(long value) => OnPropertyChanged(nameof(SizeText));

    partial void OnFileCountChanged(long value) => OnPropertyChanged(nameof(FileText));

    public bool IsMeasured => !IsMeasuring && SizeBytes > 0;

    partial void OnIsMeasuringChanged(bool value)
    {
        OnPropertyChanged(nameof(SizeText));
        OnPropertyChanged(nameof(FileText));
        OnPropertyChanged(nameof(IsMeasured));
    }

    public Guid KnownFolderId => Target.KnownFolderId;

    public bool LinkBack => Target.Kind == MigrationKind.AppFolder;

    public bool CanRestoreDefault => !string.IsNullOrWhiteSpace(Target.DefaultPath);

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private string _targetPath = string.Empty;

    /// <summary>0 = 移动，1 = 复制，2 = 不处理。</summary>
    [ObservableProperty]
    private int _actionIndex;

    [ObservableProperty]
    private string _statusText = string.Empty;

    public bool HasStatus => !string.IsNullOrEmpty(StatusText);

    partial void OnStatusTextChanged(string value) => OnPropertyChanged(nameof(HasStatus));
}

/// <summary>文件迁移：把系统文件夹或应用数据从 C 盘搬到其它分区。</summary>
public sealed partial class FileMigrationViewModel : ObservableObject, ILocalizable
{
    private readonly IMigrationService _migration;
    private readonly HardwareService _hardware;

    private string _lastVolumeRoot = string.Empty;

    public FileMigrationViewModel(IMigrationService migration, HardwareService hardware)
    {
        _migration = migration;
        _hardware = hardware;

        Actions = [Loc.T("Migrate.Action.Move"), Loc.T("Migrate.Action.Copy"), Loc.T("Migrate.Action.Leave")];
        _ = InitializeAsync();
    }

    public ObservableCollection<MigrationRowViewModel> Rows { get; } = [];

    public ObservableCollection<VolumeInfo> Volumes { get; } = [];

    public ObservableCollection<string> Actions { get; }

    [ObservableProperty]
    private VolumeInfo? _selectedVolume;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _isScanning;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private int _progressPercent;

    [ObservableProperty]
    private string _progressText = string.Empty;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private bool _hasStatus;

    [ObservableProperty]
    private string _selectionSummary = string.Empty;

    public bool IsIdle => !IsScanning && !IsBusy;

    public bool HasRows => Rows.Count > 0;

    public bool IsEmpty => !IsScanning && Rows.Count == 0;

    public string Title => Loc.T("Migrate.Title");

    public string Subtitle => Loc.T("Migrate.Subtitle");

    public string TargetVolumeLabel => Loc.T("Migrate.TargetVolume");

    public string SelectAllLabel => Loc.T("Common.SelectAll");

    public string ClearSelectionLabel => Loc.T("Migrate.ClearSelection");

    public string RestoreDefaultLabel => Loc.T("Migrate.RestoreDefault");

    public string RefreshLabel => Loc.T("Common.Refresh");

    public string StartLabel => Loc.T("Migrate.Start");

    public string ScanningText => Loc.T("Migrate.Scanning");

    public string EmptyText => Loc.T("Migrate.Empty");

    public string VolumeHint => Loc.T("Migrate.VolumeHint");

    public string AppHint => Loc.T("Migrate.AppHint");

    public string NameHeader => Loc.T("Migrate.Column.Name");

    public string CurrentHeader => Loc.T("Migrate.Column.Current");

    public string TargetHeader => Loc.T("Migrate.Column.Target");

    public string ActionHeader => Loc.T("Migrate.Column.Action");

    public string SizeHeader => Loc.T("Migrate.Column.Size");

    public string StatusHeader => Loc.T("Migrate.Column.Status");

    private async Task InitializeAsync()
    {
        var volumes = await Task.Run(_hardware.GetVolumes).ConfigureAwait(true);

        Volumes.Clear();
        foreach (var volume in volumes.Where(v => v.IsReady))
        {
            Volumes.Add(volume);
        }

        var systemRoot = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
        SelectedVolume = Volumes
            .Where(v => !string.Equals(v.Name, systemRoot, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(v => v.FreeBytes)
            .FirstOrDefault() ?? Volumes.FirstOrDefault();

        await ScanAsync().ConfigureAwait(true);
    }

    partial void OnSelectedVolumeChanged(VolumeInfo? value)
    {
        if (value is null)
        {
            return;
        }

        foreach (var row in Rows)
        {
            var root = string.IsNullOrWhiteSpace(row.TargetPath) ? string.Empty : Path.GetPathRoot(row.TargetPath);
            if (row.TargetPath.Length == 0 || string.Equals(root, _lastVolumeRoot, StringComparison.OrdinalIgnoreCase))
            {
                ApplySuggestion(row, value);
            }
        }

        _lastVolumeRoot = value.Name.TrimEnd('\\');
        Recalculate();
        _ = MeasureSizesAsync();
    }

    private string Suggest(MigrationRowViewModel row)
    {
        if (SelectedVolume is null)
        {
            return string.Empty;
        }

        var leaf = Path.GetFileName(row.CurrentPath.TrimEnd('\\'));
        return Path.Combine(SelectedVolume.Name, string.IsNullOrWhiteSpace(leaf) ? row.Name : leaf);
    }

    /// <summary>目标与源在同一分区时直接标为“已在目标分区”，不勾选也不允许迁移。</summary>
    private void ApplySuggestion(MigrationRowViewModel row, VolumeInfo volume)
    {
        var suggestion = Suggest(row);
        var used = new HashSet<string>(Rows.Where(r => r != row).Select(r => r.TargetPath), StringComparer.OrdinalIgnoreCase);
        var suffix = 2;
        var candidate = suggestion;
        while (used.Contains(candidate))
        {
            candidate = suggestion + "-" + suffix++;
        }

        row.TargetPath = candidate;

        var sameDrive = string.Equals(
            Path.GetPathRoot(row.CurrentPath)?.TrimEnd('\\'),
            volume.Name.TrimEnd('\\'),
            StringComparison.OrdinalIgnoreCase);

        row.IsSelected = !sameDrive;
        row.ActionIndex = sameDrive ? 2 : 0;
        row.StatusText = sameDrive ? Loc.T("Migrate.AlreadyOnTarget") : string.Empty;

        if (sameDrive)
        {
            row.IsMeasuring = false;
        }
        else if (!row.IsMeasured)
        {
            row.IsMeasuring = true;
        }
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        if (IsBusy || IsScanning)
        {
            return;
        }

        IsScanning = true;
        StatusText = string.Empty;
        HasStatus = false;

        try
        {
            foreach (var row in Rows)
            {
                row.PropertyChanged -= OnRowChanged;
            }

            Rows.Clear();

            var targets = await _migration.DetectAsync(CancellationToken.None).ConfigureAwait(true);

            // 系统盘上的目录排在最前面，已经在其它分区的放到后面
            var systemRoot = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
            var ordered = targets
                .OrderBy(t => string.Equals(Path.GetPathRoot(t.CurrentPath), systemRoot, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(t => t.Name.Resolve(Loc.CurrentCulture), StringComparer.CurrentCultureIgnoreCase);

            foreach (var target in ordered)
            {
                var row = new MigrationRowViewModel(target);
                row.PropertyChanged += OnRowChanged;
                Rows.Add(row);

                if (SelectedVolume is not null)
                {
                    ApplySuggestion(row, SelectedVolume);
                }
            }

            _lastVolumeRoot = SelectedVolume?.Name.TrimEnd('\\') ?? string.Empty;
            Recalculate();

            // 目录大小在后台统计，页面先出来
            _ = MeasureSizesAsync();
        }
        catch (Exception ex)
        {
            StatusText = Loc.T("Common.Failed") + ": " + ex.Message;
            HasStatus = true;
            UI.Toast.Error(StatusText);
        }
        finally
        {
            IsScanning = false;
            OnPropertyChanged(nameof(IsIdle));
            OnPropertyChanged(nameof(HasRows));
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    private async Task MeasureSizesAsync()
    {
        foreach (var row in Rows.Where(r => r.IsSelected).ToList())
        {
            await MeasureRowAsync(row).ConfigureAwait(true);
        }

        Recalculate();
    }

    private async Task MeasureRowAsync(MigrationRowViewModel row)
    {
        if (!row.IsMeasuring)
        {
            return;
        }

        try
        {
            var (bytes, files) = await _migration.MeasureAsync(row.Target, CancellationToken.None)
                .ConfigureAwait(true);
            row.SizeBytes = bytes;
            row.FileCount = files;
        }
        catch
        {
            // 统计失败就保持 0，不阻塞迁移
        }
        finally
        {
            row.IsMeasuring = false;
        }
    }

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MigrationRowViewModel.IsSelected) or nameof(MigrationRowViewModel.ActionIndex))
        {
            Recalculate();
        }
    }

    private void Recalculate()
    {
        var selected = Rows.Where(r => r.IsSelected && r.ActionIndex != 2).ToList();
        var bytes = selected.Sum(r => r.SizeBytes);
        SelectionSummary = Loc.F("Migrate.SelectedSummary", selected.Count, SizeFormatter.Format(bytes));

        OnPropertyChanged(nameof(IsIdle));
        MigrateCommand.NotifyCanExecuteChanged();
    }

    private bool CanStart() => !IsBusy && !IsScanning && Rows.Any(r => r.IsSelected && r.ActionIndex != 2);

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var row in Rows)
        {
            row.IsSelected = row.HasContent;
        }

        Recalculate();
    }

    [RelayCommand]
    private void ClearSelection()
    {
        foreach (var row in Rows)
        {
            row.IsSelected = false;
        }

        Recalculate();
    }

    [RelayCommand]
    private void RestoreDefault()
    {
        foreach (var row in Rows.Where(r => r.CanRestoreDefault))
        {
            row.TargetPath = row.Target.DefaultPath!;
            row.ActionIndex = 0;
            row.IsSelected = true;
        }

        Recalculate();
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task MigrateAsync()
    {
        var selected = Rows.Where(r => r.IsSelected && r.ActionIndex != 2).ToList();
        if (selected.Count == 0)
        {
            UI.Toast.Warning(Loc.T("Migrate.NothingSelected"));
            return;
        }

        // 还没统计完的条目先补上大小，确认框和进度才有依据
        foreach (var row in selected.Where(r => r.IsMeasuring))
        {
            await MeasureRowAsync(row).ConfigureAwait(true);
        }

        foreach (var row in selected)
        {
            var error = Validate(row);
            if (error is not null)
            {
                ShowStatus(error);
                UI.Toast.Warning(error);
                return;
            }
        }

        var totalBytes = selected.Sum(r => r.SizeBytes);
        var confirmed = await UI.Confirm.AskAsync(new ConfirmRequest(
            Loc.T("Migrate.ConfirmTitle"),
            Loc.F("Migrate.ConfirmMessage", selected.Count, SizeFormatter.Format(totalBytes)),
            selected.Take(20).Select(r => $"{r.Name}  →  {r.TargetPath}").ToList(),
            Loc.T("Migrate.Start"),
            Danger: true,
            RequireAcknowledge: true)).ConfigureAwait(true);

        if (!confirmed)
        {
            return;
        }

        IsBusy = true;
        ProgressPercent = 0;
        HasStatus = false;

        var success = 0;
        var failed = 0;
        long freed = 0;
        long completedBytes = 0;

        try
        {
            foreach (var row in selected)
            {
                row.StatusText = Loc.T("Migrate.Status.Running");
                ProgressText = Loc.F("Migrate.Running", row.Name, 0);

                var request = new MigrationRequest(
                    row.Target.Id,
                    row.Target.CurrentPath,
                    row.TargetPath.Trim(),
                    CopyOnly: row.ActionIndex == 1,
                    LinkBack: row.LinkBack && row.ActionIndex == 0,
                    row.KnownFolderId);

                var rowBytes = row.SizeBytes;
                var rowBase = completedBytes;

                var progress = new Progress<MigrationProgress>(p =>
                {
                    var percent = rowBytes <= 0
                        ? 100
                        : (int)Math.Min(100, (rowBase + rowBytes * p.Percent / 100d) * 100 / Math.Max(1, totalBytes));
                    ProgressPercent = percent;
                    ProgressText = Loc.F("Migrate.Running", row.Name, p.Percent);
                });

                var result = await _migration.MigrateAsync(request, progress, CancellationToken.None)
                    .ConfigureAwait(true);

                completedBytes += rowBytes;
                row.StatusText = result.Message;

                if (result.Success)
                {
                    success++;
                    freed += result.MovedBytes;
                    row.IsSelected = false;
                }
                else
                {
                    failed++;
                }

                ProgressPercent = (int)Math.Min(100, completedBytes * 100 / Math.Max(1, totalBytes));
            }

            var summary = Loc.F("Migrate.Done", success, failed, SizeFormatter.Format(freed));
            ShowStatus(summary);

            if (failed == 0)
            {
                UI.Toast.Success(summary);
            }
            else
            {
                UI.Toast.Warning(summary);
            }
        }
        catch (Exception ex)
        {
            ShowStatus(Loc.T("Common.Failed") + ": " + ex.Message);
            UI.Toast.Error(ex.Message);
        }
        finally
        {
            IsBusy = false;
            ProgressText = string.Empty;
            ProgressPercent = 0;
            Recalculate();
        }
    }

    private string? Validate(MigrationRowViewModel row)
    {
        var target = row.TargetPath.Trim();
        if (target.Length == 0)
        {
            return Loc.F("Migrate.Invalid.Empty", row.Name);
        }

        string full;
        try
        {
            full = Path.GetFullPath(target);
        }
        catch
        {
            return Loc.F("Migrate.Invalid.Path", row.Name);
        }

        var sourceRoot = Path.GetPathRoot(row.Target.CurrentPath);
        var targetRoot = Path.GetPathRoot(full);
        if (string.Equals(sourceRoot, targetRoot, StringComparison.OrdinalIgnoreCase))
        {
            return Loc.F("Migrate.Invalid.SameDrive", row.Name);
        }

        if (string.Equals(
                Path.TrimEndingDirectorySeparator(row.Target.CurrentPath),
                Path.TrimEndingDirectorySeparator(full),
                StringComparison.OrdinalIgnoreCase))
        {
            return Loc.F("Migrate.Invalid.SamePath", row.Name);
        }

        if (Directory.Exists(full) && Directory.EnumerateFileSystemEntries(full).Any())
        {
            return Loc.F("Migrate.Invalid.TargetNotEmpty", row.Name);
        }

        return null;
    }

    private void ShowStatus(string text)
    {
        StatusText = text;
        HasStatus = true;
    }

    public void RefreshLocalization()
    {
        var move = Loc.T("Migrate.Action.Move");
        var copy = Loc.T("Migrate.Action.Copy");
        var leave = Loc.T("Migrate.Action.Leave");

        if (Actions.Count >= 3)
        {
            Actions[0] = move;
            Actions[1] = copy;
            Actions[2] = leave;
        }

        OnPropertyChanged(string.Empty);
    }
}
