using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinTools.App.Localization;
using WinTools.App.Services;
using WinTools.Core.Abstractions;
using WinTools.Core.Models;
using WinTools.Infrastructure.Logging;

namespace WinTools.App.ViewModels;

public sealed partial class ThemeOption : ObservableObject
{
    public required AppTheme Value { get; init; }
    public required string Key { get; init; }
    public string Display => Loc.T(Key);

    public void Refresh() => OnPropertyChanged(nameof(Display));
}

public sealed partial class SettingsViewModel : ObservableObject, ILocalizable
{
    private readonly ISettingsService _settings;
    private readonly LocalizationService _localization;
    private readonly ThemeService _theme;
    private readonly IAppLogger _logger;
    private bool _loading;

    public SettingsViewModel(
        ISettingsService settings,
        LocalizationService localization,
        ThemeService theme,
        IAppLogger logger)
    {
        _settings = settings;
        _localization = localization;
        _theme = theme;
        _logger = logger;

        ThemeOptions =
        [
            new() { Value = AppTheme.System, Key = "Settings.ThemeSystem" },
            new() { Value = AppTheme.Light, Key = "Settings.ThemeLight" },
            new() { Value = AppTheme.Dark, Key = "Settings.ThemeDark" }
        ];

        _loading = true;
        SelectedTheme = ThemeOptions.FirstOrDefault(t => t.Value == _settings.Current.Theme) ?? ThemeOptions[0];
        SelectedLanguage = LocalizationService.AvailableLanguages.FirstOrDefault(l =>
                                l.Culture.Equals(_settings.Current.Language, StringComparison.OrdinalIgnoreCase))
                            ?? LocalizationService.AvailableLanguages.First(l =>
                                l.Culture.Equals(LocalizationService.ResolveSystemDefault(), StringComparison.OrdinalIgnoreCase));
        UseRecycleBin = _settings.Current.UseRecycleBin;
        ScanConcurrency = _settings.Current.ScanConcurrency;
        _loading = false;
    }

    public ObservableCollection<ThemeOption> ThemeOptions { get; }

    public IReadOnlyList<LanguageOption> Languages => LocalizationService.AvailableLanguages;

    [ObservableProperty] private ThemeOption? _selectedTheme;
    [ObservableProperty] private LanguageOption? _selectedLanguage;
    [ObservableProperty] private bool _useRecycleBin;
    [ObservableProperty] private int _scanConcurrency;

    public string Title => Loc.T("Settings.Title");

    public string AppearanceLabel => Loc.T("Settings.Appearance");

    public string ThemeLabel => Loc.T("Settings.Theme");

    public string LanguageLabel => Loc.T("Settings.Language");

    public string LanguageHint => Loc.T("Settings.LanguageHint");

    public string BehaviorLabel => Loc.T("Settings.Behavior");

    public string RecycleBinLabel => Loc.T("Settings.UseRecycleBin");

    public string ConcurrencyLabel => Loc.T("Settings.Concurrency");

    public string StorageLabel => Loc.T("Settings.Storage");

    public string DataDirLabel => Loc.T("Settings.DataDir");

    public string LogDirLabel => Loc.T("Settings.LogDir");

    public string OpenFolderLabel => Loc.T("Settings.OpenFolder");

    public string AboutLabel => Loc.T("Settings.About");

    public string VersionLabel => Loc.T("Settings.Version");

    public string DataDirectory => AppPaths.DataDirectory;

    public string LogDirectory => _logger.LogDirectory;

    public string VersionText => typeof(SettingsViewModel).Assembly.GetName().Version?.ToString(3) ?? "0.1.0";

    public IReadOnlyList<int> ConcurrencyOptions { get; } = [0, 1, 2, 4, 8];

    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(AppearanceLabel));
        OnPropertyChanged(nameof(ThemeLabel));
        OnPropertyChanged(nameof(LanguageLabel));
        OnPropertyChanged(nameof(LanguageHint));
        OnPropertyChanged(nameof(BehaviorLabel));
        OnPropertyChanged(nameof(RecycleBinLabel));
        OnPropertyChanged(nameof(ConcurrencyLabel));
        OnPropertyChanged(nameof(StorageLabel));
        OnPropertyChanged(nameof(DataDirLabel));
        OnPropertyChanged(nameof(LogDirLabel));
        OnPropertyChanged(nameof(OpenFolderLabel));
        OnPropertyChanged(nameof(AboutLabel));
        OnPropertyChanged(nameof(VersionLabel));

        foreach (var option in ThemeOptions)
        {
            option.Refresh();
        }
    }

    partial void OnSelectedThemeChanged(ThemeOption? value)
    {
        if (_loading || value is null)
        {
            return;
        }

        _settings.Current.Theme = value.Value;
        _theme.Apply(value.Value);
        _settings.Save();
    }

    partial void OnSelectedLanguageChanged(LanguageOption? value)
    {
        if (_loading || value is null)
        {
            return;
        }

        _settings.Current.Language = value.Culture;
        _localization.SetLanguage(value.Culture);
        _settings.Save();
        RefreshLocalization();
    }

    partial void OnUseRecycleBinChanged(bool value)
    {
        if (_loading)
        {
            return;
        }

        _settings.Current.UseRecycleBin = value;
        _settings.Save();
    }

    partial void OnScanConcurrencyChanged(int value)
    {
        if (_loading)
        {
            return;
        }

        _settings.Current.ScanConcurrency = value;
        _settings.Save();
    }

    [RelayCommand]
    private void OpenDataDirectory() => OpenFolder(AppPaths.DataDirectory);

    [RelayCommand]
    private void OpenLogDirectory() => OpenFolder(_logger.LogDirectory);

    private void OpenFolder(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger.Warn("Settings", "打开目录失败: " + ex.Message);
        }
    }
}
