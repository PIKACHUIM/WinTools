using System.Diagnostics;
using System.Text;

namespace WinTools.Infrastructure.Common;

public sealed record ProcessResult(int ExitCode, string StdOut, string StdErr, bool TimedOut)
{
    public bool Success => !TimedOut && ExitCode == 0;

    public string Combined => string.IsNullOrWhiteSpace(StdErr)
        ? StdOut
        : string.IsNullOrWhiteSpace(StdOut) ? StdErr : StdOut + Environment.NewLine + StdErr;
}

/// <summary>
/// 隐藏窗口执行外部命令（dism / pnputil / powershell 等），统一超时与输出编码处理。
/// </summary>
public static class ProcessRunner
{
    public static async Task<ProcessResult> RunAsync(
        string fileName,
        IEnumerable<string>? arguments = null,
        int timeoutMs = 120_000,
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        foreach (var argument in arguments ?? [])
        {
            startInfo.ArgumentList.Add(argument);
        }

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        using var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                lock (stdout)
                {
                    stdout.AppendLine(e.Data);
                }
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                lock (stderr)
                {
                    stderr.AppendLine(e.Data);
                }
            }
        };

        try
        {
            if (!process.Start())
            {
                return new ProcessResult(-1, string.Empty, $"无法启动进程: {fileName}", false);
            }
        }
        catch (Exception ex)
        {
            return new ProcessResult(-1, string.Empty, ex.Message, false);
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(timeoutMs);

        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // 忽略
            }

            return new ProcessResult(-1, stdout.ToString(), stderr.ToString(), true);
        }

        return new ProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString(), false);
    }

    private static readonly string PowerShellPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.System),
        @"WindowsPowerShell\v1.0\powershell.exe");

    /// <summary>用 Windows PowerShell 执行脚本（仅在有现成 cmdlet 时使用，如 Appx）。</summary>
    public static Task<ProcessResult> RunPowerShellAsync(
        string script,
        int timeoutMs = 120_000,
        CancellationToken cancellationToken = default)
        => RunAsync(
            PowerShellPath,
            ["-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command", script],
            timeoutMs,
            cancellationToken);

    /// <summary>以 Shell 方式启动（打开目录、启动卸载程序等），不等待退出。</summary>
    public static bool Launch(string fileName, string? arguments = null)
    {
        try
        {
            var startInfo = new ProcessStartInfo(fileName)
            {
                UseShellExecute = true
            };

            if (!string.IsNullOrWhiteSpace(arguments))
            {
                startInfo.Arguments = arguments;
            }

            Process.Start(startInfo);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
