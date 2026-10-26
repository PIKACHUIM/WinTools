using System.Text.RegularExpressions;
using Microsoft.Win32;
using WinTools.Core.Abstractions;
using WinTools.Core.Models;
using WinTools.Infrastructure.Common;

namespace WinTools.Infrastructure.Features;

/// <summary>
/// 功能管理：Windows 可选功能（DISM）+ 版本渠道切换（注册表 / WMI 激活状态 / changepk）。
/// </summary>
public sealed partial class FeatureService : IFeatureService
{
    private const string Dism = "dism.exe";

    private readonly IAppLogger _logger;

    public FeatureService(IAppLogger logger) => _logger = logger;

    public async Task<IReadOnlyList<OptionalFeature>> GetFeaturesAsync(CancellationToken cancellationToken)
    {
        var result = await ProcessRunner.RunAsync(
            Dism,
            ["/English", "/Online", "/Get-Features", "/Format:Table"],
            180_000,
            cancellationToken).ConfigureAwait(false);

        if (!result.Success)
        {
            _logger.Error("Features", "枚举可选功能失败: " + result.Combined);
            return [];
        }

        var features = new List<OptionalFeature>(256);
        foreach (var rawLine in result.StdOut.Split('\n'))
        {
            var line = rawLine.Trim();
            var separator = line.IndexOf('|');
            if (separator <= 0)
            {
                continue;
            }

            var name = line[..separator].Trim();
            var state = line[(separator + 1)..].Trim();

            if (name.Length == 0 || state.Length == 0 ||
                name.Equals("Feature Name", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // 表头/分隔线（如 "------|------"）不是功能项
            if (name.All(c => c == '-') || state.All(c => c == '-'))
            {
                continue;
            }

            features.Add(new OptionalFeature(
                name,
                name,
                state.StartsWith("Enabled", StringComparison.OrdinalIgnoreCase),
                state.Contains("Pending", StringComparison.OrdinalIgnoreCase)));
        }

        return features;
    }

    public async Task<OperationResult> SetFeatureAsync(string featureName, bool enable, CancellationToken cancellationToken)
    {
        var arguments = new List<string>
        {
            "/English",
            "/Online",
            enable ? "/Enable-Feature" : "/Disable-Feature",
            "/FeatureName:" + featureName,
            "/NoRestart"
        };

        if (enable)
        {
            arguments.Add("/All");
        }

        var result = await ProcessRunner.RunAsync(Dism, arguments, 900_000, cancellationToken).ConfigureAwait(false);
        _logger.Audit(enable ? "Feature.Enable" : "Feature.Disable", featureName, result.Combined.Trim());

        if (result.ExitCode is 0 or 3010)
        {
            return OperationResult.Ok(
                enable ? "功能已启用" : "功能已禁用",
                affected: 1,
                restartRequired: result.ExitCode == 3010);
        }

        var message = ExtractDismError(result.Combined);
        return OperationResult.Fail(message);
    }

    public Task<EditionInfo> GetEditionAsync(CancellationToken cancellationToken) => Task.Run(() =>
    {
        using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
            .OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");

        var productName = key?.GetValue("ProductName") as string ?? "Windows";
        var editionId = key?.GetValue("EditionID") as string ?? "Unknown";
        var displayVersion = key?.GetValue("DisplayVersion") as string ?? string.Empty;
        var build = key?.GetValue("CurrentBuild") as string ?? Environment.OSVersion.Version.Build.ToString();
        var ubr = key?.GetValue("UBR");

        // 注册表里的 ProductName 从 Windows 11 起一直是「Windows 10 ...」，
        // 微软没有更新过这个值，只能按构建号纠正，否则渠道页会把 Win11 显示成 Win10。
        if (int.TryParse(build, out var buildNumber) && buildNumber >= 22000)
        {
            productName = productName.Replace("Windows 10", "Windows 11", StringComparison.OrdinalIgnoreCase);

            if (!productName.Contains("Windows 11", StringComparison.OrdinalIgnoreCase))
            {
                productName = "Windows 11 " + productName;
            }
        }
        var installationType = key?.GetValue("InstallationType") as string ?? "Client";

        var licensing = Wmi.Query(
            "SELECT LicenseStatus, PartialProductKey, Name FROM SoftwareLicensingProduct " +
            "WHERE PartialProductKey IS NOT NULL AND ApplicationId LIKE '55c92734%'");

        var status = licensing.FirstOrDefault(r =>
            r.TryGetValue("LicenseStatus", out var v) && Convert.ToInt32(v) == 1) is not null;

        var partialKey = Wmi.FirstString(licensing, "PartialProductKey");

        var buildText = ubr is null ? build : $"{build}.{ubr}";
        return new EditionInfo(
            productName,
            editionId,
            displayVersion,
            buildText,
            installationType,
            status,
            partialKey);
    }, cancellationToken);

    public Task<OperationResult> ChangeEditionAsync(string productKey, CancellationToken cancellationToken) => Task.Run(() =>
    {
        var normalized = productKey.Trim().ToUpperInvariant();
        if (!ProductKeyPattern().IsMatch(normalized))
        {
            return OperationResult.Fail("产品密钥格式不正确");
        }

        _logger.Audit("Edition.Change", normalized[..5] + "-*****", "调用 changepk 切换版本");

        // changepk 会弹出系统自带向导，故以 Shell 方式启动
        var started = ProcessRunner.Launch("changepk.exe", "/ProductKey " + normalized);
        return started
            ? OperationResult.Ok("已启动系统版本切换向导，请在弹出的窗口中完成后续步骤", 1, restartRequired: true)
            : OperationResult.Fail("无法启动 changepk.exe");
    }, cancellationToken);

    [GeneratedRegex(@"^[A-Z0-9]{5}(-[A-Z0-9]{5}){4}$")]
    private static partial Regex ProductKeyPattern();

    private static string ExtractDismError(string output)
    {
        foreach (var line in output.Split('\n'))
        {
            var text = line.Trim();
            if (text.StartsWith("Error", StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("错误", StringComparison.Ordinal))
            {
                return text;
            }
        }

        return string.IsNullOrWhiteSpace(output) ? "DISM 执行失败" : output.Trim();
    }
}
