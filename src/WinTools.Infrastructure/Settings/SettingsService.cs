using System.Text.Json;
using WinTools.Core.Abstractions;
using WinTools.Core.Models;
using WinTools.Infrastructure.Logging;

namespace WinTools.Infrastructure.Settings;

public sealed class SettingsService : ISettingsService
{
    private readonly IAppLogger? _logger;
    private AppSettings _current = new();

    public SettingsService(IAppLogger? logger = null) => _logger = logger;

    public AppSettings Current => _current;

    public string SettingsDirectory => AppPaths.DataDirectory;

    public void Load()
    {
        try
        {
            var path = AppPaths.SettingsFile;
            if (!File.Exists(path))
            {
                _current = new AppSettings();
                return;
            }

            var json = File.ReadAllText(path);
            _current = JsonSerializer.Deserialize<AppSettings>(json, JsonConfig.Options) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            _logger?.Warn("Settings", "读取配置失败，使用默认值: " + ex.Message);
            _current = new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.DataDirectory);
            File.WriteAllText(AppPaths.SettingsFile, JsonSerializer.Serialize(_current, JsonConfig.PrettyOptions));
        }
        catch (Exception ex)
        {
            _logger?.Warn("Settings", "保存配置失败: " + ex.Message);
        }
    }
}
