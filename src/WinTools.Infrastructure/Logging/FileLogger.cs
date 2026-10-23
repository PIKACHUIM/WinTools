using System.Text;
using WinTools.Core.Abstractions;

namespace WinTools.Infrastructure.Logging;

/// <summary>轻量文件日志：普通日志 + 审计日志分离，按天滚动。</summary>
public sealed class FileLogger : IAppLogger
{
    private readonly object _gate = new();
    private readonly string _logDir;

    public FileLogger(string? logDirectory = null)
    {
        _logDir = logDirectory ?? Path.Combine(AppPaths.DataDirectory, "logs");
        try
        {
            Directory.CreateDirectory(_logDir);
        }
        catch
        {
            _logDir = Path.Combine(Path.GetTempPath(), "WinTools", "logs");
            Directory.CreateDirectory(_logDir);
        }
    }

    public string LogDirectory => _logDir;

    public void Info(string category, string message) => Write("INFO", category, message);

    public void Warn(string category, string message) => Write("WARN", category, message);

    public void Error(string category, string message, Exception? ex = null)
        => Write("ERROR", category, ex is null ? message : $"{message} :: {ex.GetType().Name}: {ex.Message}");

    public void Audit(string action, string target, string? detail = null)
    {
        var line = $"{{\"ts\":\"{DateTime.Now:O}\",\"action\":{Q(action)},\"target\":{Q(target)},\"detail\":{Q(detail ?? string.Empty)}}}";
        Append("audit", line);
    }

    private void Write(string level, string category, string message)
        => Append("app", $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] [{category}] {message}");

    private void Append(string prefix, string line)
    {
        try
        {
            lock (_gate)
            {
                File.AppendAllText(
                    Path.Combine(_logDir, $"{prefix}-{DateTime.Now:yyyyMMdd}.log"),
                    line + Environment.NewLine,
                    Encoding.UTF8);
            }
        }
        catch
        {
            // 日志失败不能影响主流程
        }
    }

    private static string Q(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}

/// <summary>应用数据目录：优先 ProgramData，失败时退回 LocalAppData。</summary>
public static class AppPaths
{
    private static readonly Lazy<string> DataDir = new(ResolveDataDirectory, isThreadSafe: true);

    public static string DataDirectory => DataDir.Value;

    public static string SettingsFile => Path.Combine(DataDirectory, "settings.json");

    private static string ResolveDataDirectory()
    {
        var programData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "WinTools");
        try
        {
            Directory.CreateDirectory(programData);
            var probe = Path.Combine(programData, ".writable");
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            return programData;
        }
        catch
        {
            var local = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinTools");
            Directory.CreateDirectory(local);
            return local;
        }
    }
}
