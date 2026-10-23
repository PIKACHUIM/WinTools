using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinTools.App.Localization;
using WinTools.Core.Abstractions;
using WinTools.Core.Models;
using WinTools.Core.Utils;

namespace WinTools.App.ViewModels.Modules;

/// <summary>备份还原：系统备份 / 增量备份 / WIM 信息 / 释放镜像。</summary>
public sealed partial class BackupRestoreViewModel : ModuleViewModelBase
{
    public BackupRestoreViewModel(IModuleCatalog catalog, IWimService wim, IAppLogger logger)
        : base(catalog, "Backup")
    {
        Register("Capture", new BackupTaskViewModel(wim, logger, BackupMode.Capture));
        Register("Incremental", new BackupTaskViewModel(wim, logger, BackupMode.Append));
        Register("WimInfo", new WimInfoSectionViewModel(wim));
        Register("Apply", new BackupTaskViewModel(wim, logger, BackupMode.Apply));
        SelectSection(Sections[0].Id);
    }
}

public enum BackupMode
{
    Capture,
    Append,
    Apply
}

/// <summary>WIM 信息：读取映像索引与属性（备份时已写入名称与描述）。</summary>
public sealed partial class WimInfoSectionViewModel : ListSectionViewModel
{
    private readonly IWimService _wim;

    public WimInfoSectionViewModel(IWimService wim)
    {
        _wim = wim;
        HasInput = true;
        InputLabel = Loc.T("Wim.ImagePath");
        HasPrimary = true;
        PrimaryLabel = Loc.T("Common.Refresh");
    }

    protected override async Task LoadAsync(CancellationToken cancellationToken)
    {
        var path = InputText.Trim();
        if (path.Length == 0)
        {
            Status = Loc.T("Wim.ImagePath");
            return;
        }

        var images = await _wim.GetInfoAsync(path, cancellationToken).ConfigureAwait(true);

        Rows.Clear();
        foreach (var image in images)
        {
            Rows.Add(new RowItem(
                image.Index.ToString(),
                $"#{image.Index}  {image.Name}",
                image.Description,
                image.TotalBytes is null ? null : SizeFormatter.Format(image.TotalBytes.Value),
                image.Compression,
                RowTone.Accent));
        }

    }

    protected override Task PrimaryCoreAsync(CancellationToken cancellationToken)
        => ReloadAsync(cancellationToken);
}

/// <summary>备份/释放表单：三种模式共用同一视图。</summary>
public sealed partial class BackupTaskViewModel : ObservableObject, ILocalizable
{
    private readonly IWimService _wim;
    private readonly IAppLogger _logger;

    public BackupTaskViewModel(IWimService wim, IAppLogger logger, BackupMode mode)
    {
        _wim = wim;
        _logger = logger;
        Mode = mode;
    }

    public BackupMode Mode { get; }

    public bool IsCapture => Mode == BackupMode.Capture;

    public bool IsAppend => Mode == BackupMode.Append;

    public bool IsApply => Mode == BackupMode.Apply;

    public bool ShowSource => !IsApply;

    public bool ShowImageSettings => !IsApply;

    public bool ShowApplySettings => IsApply;

    [ObservableProperty] private string _sourcePath = string.Empty;
    [ObservableProperty] private string _imagePath = string.Empty;
    [ObservableProperty] private string _imageName = "WinTools Backup";
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private string _targetPath = string.Empty;
    [ObservableProperty] private double _imageIndex = 1;
    [ObservableProperty] private bool _verify;
    [ObservableProperty] private int _compressionIndex;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private bool _hasStatus;
    [ObservableProperty] private int _progressPercent;
    [ObservableProperty] private bool _showProgress;
    [ObservableProperty] private string _log = string.Empty;

    public ObservableCollection<string> CompressionOptions { get; } =
    [
        Loc.T("Wim.CompressNone"),
        Loc.T("Wim.CompressFast"),
        Loc.T("Wim.CompressMax")
    ];

    public string StartLabel => Mode switch
    {
        BackupMode.Capture => Loc.T("Wim.StartCapture"),
        BackupMode.Append => Loc.T("Wim.StartAppend"),
        _ => Loc.T("Wim.StartApply")
    };

    public string TitleText => Mode switch
    {
        BackupMode.Capture => Loc.T("Wim.CaptureTitle"),
        BackupMode.Append => Loc.T("Wim.AppendTitle"),
        _ => Loc.T("Wim.ApplyTitle")
    };

    public string SourceLabel => Loc.T("Wim.SourcePath");

    public string ImageLabel => Loc.T("Wim.ImagePath");

    public string TargetLabel => Loc.T("Wim.TargetPath");

    public string NameLabel => Loc.T("Wim.Name");

    public string DescriptionLabel => Loc.T("Wim.Description");

    public string CompressionLabel => Loc.T("Wim.Compression");

    public string IndexLabel => Loc.T("Wim.Index");

    public string VerifyLabel => Loc.T("Wim.Verify");

    public string LogLabel => Loc.T("Wim.Log");

    [RelayCommand]
    private async Task StartAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ShowProgress = true;
        ProgressPercent = 0;
        Log = string.Empty;

        try
        {
            var progress = new Progress<WimProgress>(p =>
            {
                ProgressPercent = p.Percent;
                Status = p.Text;
            });

            OperationResult result;

            if (IsApply)
            {
                result = await _wim.ApplyAsync(
                    new WimApplyRequest(
                        ImagePath.Trim(),
                        (int)Math.Max(1, Math.Round(ImageIndex)),
                        TargetPath.Trim(),
                        Verify),
                    progress,
                    CancellationToken.None).ConfigureAwait(true);
            }
            else
            {
                var compression = CompressionIndex switch
                {
                    0 => 0u,
                    1 => 1u,
                    _ => 2u
                };

                result = await _wim.CaptureAsync(
                    new WimCaptureRequest(
                        SourcePath.Trim(),
                        ImagePath.Trim(),
                        string.IsNullOrWhiteSpace(ImageName) ? "WinTools" : ImageName.Trim(),
                        Description.Trim(),
                        Append: IsAppend,
                        CompressionType: compression),
                    progress,
                    CancellationToken.None).ConfigureAwait(true);
            }

            Status = result.Message;
            AppendLog($"{DateTime.Now:HH:mm:ss} {result.Message}");

            if (result.RestartRequired)
            {
                Status += " · " + Loc.T("Common.RestartNeeded");
            }

            _logger.Audit("Wim.Task", Mode.ToString(), result.Message);
        }
        catch (Exception ex)
        {
            Status = ex.Message;
            AppendLog(ex.Message);
            _logger.Error("Wim", "备份任务失败", ex);
        }
        finally
        {
            IsBusy = false;
            ShowProgress = false;
        }
    }

    private void AppendLog(string line) => Log = string.IsNullOrEmpty(Log) ? line : Log + Environment.NewLine + line;

    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(StartLabel));
        OnPropertyChanged(nameof(TitleText));
        OnPropertyChanged(nameof(SourceLabel));
        OnPropertyChanged(nameof(ImageLabel));
        OnPropertyChanged(nameof(TargetLabel));
        OnPropertyChanged(nameof(NameLabel));
        OnPropertyChanged(nameof(DescriptionLabel));
        OnPropertyChanged(nameof(CompressionLabel));
        OnPropertyChanged(nameof(IndexLabel));
        OnPropertyChanged(nameof(VerifyLabel));
        OnPropertyChanged(nameof(LogLabel));
    }

    partial void OnStatusChanged(string value) => HasStatus = !string.IsNullOrWhiteSpace(value);
}
