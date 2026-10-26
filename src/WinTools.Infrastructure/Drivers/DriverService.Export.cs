using WinTools.Core.Models;
using WinTools.Infrastructure.Common;

namespace WinTools.Infrastructure.Drivers;

/// <summary>单个驱动包的导出：pnputil /export-driver 不支持只导出选中项之外的方式，逐个调用即可。</summary>
public sealed partial class DriverService
{
    public async Task<OperationResult> ExportAsync(
        DriverPackage driver,
        string targetDirectory,
        CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(targetDirectory);

            var result = await ProcessRunner.RunAsync(
                "pnputil.exe",
                ["/export-driver", driver.PublishedName, targetDirectory],
                300_000,
                cancellationToken).ConfigureAwait(false);

            _logger.Audit("Driver.Export", driver.PublishedName, $"exit={result.ExitCode} → {targetDirectory}");

            return result.Success
                ? OperationResult.Ok($"{driver.OriginalName} 已导出")
                : OperationResult.Fail($"{driver.OriginalName} 导出失败（退出码 {result.ExitCode}）");
        }
        catch (Exception ex)
        {
            _logger.Error("Driver", $"导出 {driver.PublishedName} 失败", ex);
            return OperationResult.Fail(ex.Message);
        }
    }
}
