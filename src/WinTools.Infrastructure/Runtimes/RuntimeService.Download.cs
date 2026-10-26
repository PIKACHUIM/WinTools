using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using WinTools.Core.Models;
using WinTools.Infrastructure.Common;

namespace WinTools.Infrastructure.Runtimes;

/// <summary>运行库在线下载与静默安装：.NET 读官方 releases-index.json，VC++ 用 aka.ms 固定链接。</summary>
public sealed partial class RuntimeService
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromMinutes(10)
    };

    /// <summary>VC++ 2015-2022 合并运行库（微软 aka.ms 永久链接）。</summary>
    private static readonly (string Id, string Name, string Arch, string Url)[] VcPackages =
    [
        ("vcpp-x64", "Visual C++ 2015-2022 Redistributable", "x64",
            "https://aka.ms/vs/17/release/vc_redist.x64.exe"),
        ("vcpp-x86", "Visual C++ 2015-2022 Redistributable", "x86",
            "https://aka.ms/vs/17/release/vc_redist.x86.exe"),
        ("vcpp-arm64", "Visual C++ 2015-2022 Redistributable", "arm64",
            "https://aka.ms/vs/17/release/vc_redist.arm64.exe")
    ];

    public async Task<IReadOnlyList<RuntimePackage>> GetAvailableAsync(string kind, CancellationToken cancellationToken)
    {
        if (string.Equals(kind, "vcpp", StringComparison.OrdinalIgnoreCase))
        {
            return VcPackages
                .Select(v => new RuntimePackage(v.Id, v.Name, "14.x", v.Arch, v.Url, null, 0, "vcpp"))
                .ToList();
        }

        return await GetDotNetPackagesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<OperationResult> InstallAsync(
        RuntimePackage package,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var file = Path.Combine(Path.GetTempPath(), $"wintools-{package.Id}-{Guid.NewGuid():N}.exe");

        try
        {
            progress?.Report(0);

            using (var response = await Http.GetAsync(package.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                       .ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();

                var total = response.Content.Headers.ContentLength ?? package.SizeBytes;
                await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                await using var target = File.Create(file);

                var buffer = new byte[81920];
                long copied = 0;
                int read;

                while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    copied += read;

                    if (total > 0)
                    {
                        progress?.Report(copied * 90d / total);
                    }
                }
            }

            // 校验：.NET 用官方 SHA512，VC++ 用 Authenticode 签名
            if (!string.IsNullOrWhiteSpace(package.Sha512))
            {
                var actual = await ComputeSha512Async(file, cancellationToken).ConfigureAwait(false);
                if (!string.Equals(actual, package.Sha512, StringComparison.OrdinalIgnoreCase))
                {
                    return OperationResult.Fail("安装包校验失败（SHA512 不匹配），已中止");
                }
            }
            else if (!IsMicrosoftSigned(file))
            {
                return OperationResult.Fail("安装包没有有效的微软签名，已中止");
            }

            progress?.Report(95);

            var result = await ProcessRunner.RunAsync(
                file,
                ["/install", "/quiet", "/norestart"],
                timeoutMs: 900_000,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            // VC++ 的安装器只认 /quiet，不认 /install
            if (!result.Success && !result.TimedOut &&
                result.ExitCode is not 0 and not 1638 and not 3010)
            {
                result = await ProcessRunner.RunAsync(
                    file,
                    ["/quiet", "/norestart"],
                    timeoutMs: 900_000,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            progress?.Report(100);

            _logger.Audit("Runtime.Install", package.Id, $"exit={result.ExitCode}");

            if (result.TimedOut)
            {
                return OperationResult.Fail("安装超时");
            }

            return result.ExitCode switch
            {
                0 => OperationResult.Ok("已安装"),
                1638 or 3010 => OperationResult.Ok("已安装", restartRequired: true),
                _ => OperationResult.Fail($"安装失败（退出码 {result.ExitCode}）")
            };
        }
        catch (Exception ex)
        {
            _logger.Error("Runtime", $"安装 {package.Id} 失败", ex);
            return OperationResult.Fail(ex.Message);
        }
        finally
        {
            try
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            catch
            {
                // 临时文件删不掉就留给系统清理
            }
        }
    }

    // ------------------------------------------------------------------

    /// <summary>读取官方版本索引，取最新 LTS 与最新 STS 的桌面运行时（win-x64）。</summary>
    private async Task<IReadOnlyList<RuntimePackage>> GetDotNetPackagesAsync(CancellationToken cancellationToken)
    {
        var list = new List<RuntimePackage>();

        try
        {
            using var index = JsonDocument.Parse(await Http
                .GetStringAsync("https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/releases-index.json",
                    cancellationToken).ConfigureAwait(false));

            var channels = index.RootElement.GetProperty("releases-index").EnumerateArray().Take(3).ToList();

            foreach (var channel in channels)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var major = channel.GetProperty("channel-version").GetString() ?? "?";

                // 只接受已 GA 的频道（active / maintenance），预览频道跳过
                var support = channel.TryGetProperty("support-phase", out var phase) ? phase.GetString() : null;
                if (string.Equals(support, "preview", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var metadataUrl = channel.GetProperty("releases.json").GetString();
                if (string.IsNullOrWhiteSpace(metadataUrl))
                {
                    continue;
                }

                using var releaseDoc = JsonDocument.Parse(
                    await Http.GetStringAsync(metadataUrl, cancellationToken).ConfigureAwait(false));

                var release = releaseDoc.RootElement.GetProperty("releases")[0];
                var version = release.GetProperty("release-version").GetString() ?? major;

                // 预览版（带 -rc / -preview）不提供一键安装，避免用户装到非正式版本
                if (version.Contains('-'))
                {
                    continue;
                }

                foreach (var stack in new[] { "windowsdesktop", "runtime" })
                {
                    if (!release.TryGetProperty(stack, out var node) ||
                        !node.TryGetProperty("files", out var files))
                    {
                        continue;
                    }

                    foreach (var file in files.EnumerateArray())
                    {
                        var rid = file.TryGetProperty("rid", out var r) ? r.GetString() : null;
                        var name = file.TryGetProperty("name", out var n) ? n.GetString() : null;

                        if (rid != "win-x64" || name is null || !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var size = file.TryGetProperty("size", out var s) && s.TryGetInt64(out var bytes) ? bytes : 0;
                        var hash = file.TryGetProperty("hash", out var h) ? h.GetString() : null;
                        var url = file.GetProperty("url").GetString()!;
                        var label = stack == "windowsdesktop"
                            ? $".NET Desktop Runtime {major}"
                            : $".NET Runtime {major}";

                        list.Add(new RuntimePackage(
                            $"dotnet-{stack}-{major}", label, version, "x64", url, hash, size, "dotnet"));
                    }
                }

                // 只取 LTS 与最新两个频道，避免列表过长
                if (list.Count >= 4)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Warn("Runtime", $"获取 .NET 版本索引失败：{ex.Message}");
        }

        return list;
    }

    private static async Task<string> ComputeSha512Async(string file, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(file);
        var hash = await SHA512.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>校验 Authenticode 签名者包含 Microsoft。</summary>
    private static bool IsMicrosoftSigned(string file)
    {
        try
        {
            var cert = X509Certificate.CreateFromSignedFile(file);
            using var cert2 = new X509Certificate2(cert);
            return cert2.Subject.Contains("Microsoft", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
