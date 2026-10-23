using System.Diagnostics;
using System.IO;

namespace WinTools.App.Diagnostics;

/// <summary>
/// 启动阶段打点：设置环境变量 WINTOOLS_TRACE=1 后在 %LOCALAPPDATA%\WinTools\boot-trace.log
/// 输出各阶段耗时（未开启时开销为一次静态布尔判断）。
/// </summary>
internal static class BootTrace
{
    private static readonly bool Enabled =
        Environment.GetEnvironmentVariable("WINTOOLS_TRACE") == "1";

    private static readonly Stopwatch Clock = Stopwatch.StartNew();

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WinTools",
        "boot-trace.log");

    public static void Log(string stage)
    {
        if (!Enabled)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.AppendAllText(FilePath, $"{Clock.ElapsedMilliseconds,6} ms  {stage}{Environment.NewLine}");
        }
        catch
        {
            // 打点失败不影响启动
        }
    }

    public static void Reset()
    {
        if (!Enabled)
        {
            return;
        }

        try
        {
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }
        }
        catch
        {
            // 忽略
        }
    }

    /// <summary>计时块，用于给单个操作打点（using 结束即记录耗时）。</summary>
    public static IDisposable Scope(string stage) => new ScopeImpl(stage);

    private sealed class ScopeImpl : IDisposable
    {
        private readonly string _stage;
        private readonly Stopwatch _watch = Stopwatch.StartNew();

        public ScopeImpl(string stage) => _stage = stage;

        public void Dispose()
        {
            if (Enabled)
            {
                Log($"{_stage} 耗时 {_watch.ElapsedMilliseconds} ms");
            }
        }
    }
}
