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

            if (result.Success)
            {
                return OperationResult.Ok($"{driver.OriginalName} 已导出");
            }

            // 把 pnputil 的原话带出来：只报退出码无法判断是权限、路径还是驱动本身的问题
            var detail = result.Combined.Trim();
            if (detail.Length > 400)
            {
                detail = detail[..400] + "…";
            }

            return OperationResult.Fail(string.IsNullOrWhiteSpace(detail)
                ? $"{driver.OriginalName} 导出失败（退出码 {result.ExitCode}）"
                : $"{driver.OriginalName} 导出失败：{detail}");
        }
        catch (Exception ex)
        {
            _logger.Error("Driver", $"导出 {driver.PublishedName} 失败", ex);
            return OperationResult.Fail(ex.Message);
        }
    }
}
