using System.Runtime.InteropServices;
using System.Xml.Linq;
using WinTools.Core.Abstractions;
using WinTools.Core.Models;
using WinTools.Infrastructure.Common;

namespace WinTools.Infrastructure.Backup;

/// <summary>
/// 备份还原：WIMGAPI 捕获 / 追加 / 释放 / 读取与修改映像信息。
/// .esd 与 .swm 通过 DISM 做格式转换（WIMGAPI 不直接创建这两种目标格式）。
/// </summary>
public sealed class WimService : IWimService
{
    private readonly IAppLogger _logger;

    public WimService(IAppLogger logger) => _logger = logger;

    public Task<IReadOnlyList<WimImageInfo>> GetInfoAsync(string imagePath, CancellationToken cancellationToken)
        => Task.Run<IReadOnlyList<WimImageInfo>>(() =>
        {
            if (!File.Exists(imagePath))
            {
                return [];
            }

            var handle = WimgApi.WIMCreateFile(
                imagePath, WimgApi.GenericRead, WimgApi.OpenExisting, 0, 0, out _);

            if (handle == IntPtr.Zero)
            {
                _logger.Error("Wim", "打开映像失败: " + WimgApi.LastError());
                return [];
            }

            try
            {
                var xml = ReadXml(handle);
                if (xml is null)
                {
                    return [];
                }

                return ParseImages(xml);
            }
            finally
            {
                WimgApi.WIMCloseHandle(handle);
            }
        }, cancellationToken);

    public Task<OperationResult> CaptureAsync(
        WimCaptureRequest request,
        IProgress<WimProgress>? progress,
        CancellationToken cancellationToken) => Task.Run(() =>
    {
        if (!Directory.Exists(request.SourcePath))
        {
            return OperationResult.Fail("源目录不存在");
        }

        var target = request.ImagePath.Trim();
        var extension = Path.GetExtension(target).ToLowerInvariant();

        var actualTarget = extension is ".esd" or ".swm"
            ? Path.Combine(Path.GetDirectoryName(target)!, Path.GetFileNameWithoutExtension(target) + ".tmp.wim")
            : target;

        try
        {
            var directory = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var compression = request.CompressionType switch
            {
                0 => WimgApi.CompressNone,
                1 => WimgApi.CompressXpress,
                3 => WimgApi.CompressLzms,
                _ => WimgApi.CompressLzx
            };

            var access = WimgApi.GenericWrite | WimgApi.GenericRead;
            var creation = request.Append && File.Exists(actualTarget)
                ? WimgApi.OpenAlways
                : WimgApi.CreateNew;

            if (creation == WimgApi.CreateNew && File.Exists(actualTarget))
            {
                File.Delete(actualTarget);
            }

            var handle = WimgApi.WIMCreateFile(actualTarget, access, creation, 0, compression, out _);
            if (handle == IntPtr.Zero)
            {
                return OperationResult.Fail("创建映像失败: " + WimgApi.LastError());
            }

            try
            {
                WimgApi.WIMSetTemporaryPath(handle, Path.GetTempPath());

                using var callback = new CaptureCallback(handle, progress, cancellationToken, request.ImageName);
                var xml = BuildImageXml(request.ImageName, request.Description);
                WimgApi.WIMSetImageInformation(handle, xml, (uint)xml.Length);

                progress?.Report(new WimProgress(0, 0, 0, "正在捕获"));
                var index = WimgApi.WIMCaptureImage(handle, request.SourcePath, WimgApi.FlagVerify);
                if (index <= 0)
                {
                    var error = callback.Error ?? WimgApi.LastError();
                    return OperationResult.Fail("捕获失败: " + error);
                }

                _logger.Audit("Wim.Capture", target, $"索引 {index}");

                if (extension is ".esd" or ".swm")
                {
                    var convert = ConvertAsync(actualTarget, target, extension, request.SplitSizeMb, cancellationToken)
                        .GetAwaiter().GetResult();

                    if (!convert.Success)
                    {
                        return convert;
                    }

                    TryDelete(actualTarget);
                }

                return OperationResult.Ok($"已生成 {Path.GetFileName(target)}（索引 {index}）", 1);
            }
            finally
            {
                WimgApi.WIMCloseHandle(handle);
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Wim", "捕获映像失败", ex);
            return OperationResult.Fail(ex.Message);
        }
    }, cancellationToken);

    public Task<OperationResult> ApplyAsync(
        WimApplyRequest request,
        IProgress<WimProgress>? progress,
        CancellationToken cancellationToken) => Task.Run(() =>
    {
        if (!File.Exists(request.ImagePath))
        {
            return OperationResult.Fail("映像文件不存在");
        }

        if (string.IsNullOrWhiteSpace(request.TargetPath))
        {
            return OperationResult.Fail("请选择释放目标目录");
        }

        var extension = Path.GetExtension(request.ImagePath).ToLowerInvariant();

        // 分卷 / ESD 交给 DISM 释放更稳妥
        if (extension is ".esd" or ".swm")
        {
            return ApplyWithDism(request, cancellationToken).GetAwaiter().GetResult();
        }

        var handle = WimgApi.WIMCreateFile(
            request.ImagePath, WimgApi.GenericRead, WimgApi.OpenExisting, 0, 0, out _);

        if (handle == IntPtr.Zero)
        {
            return OperationResult.Fail("打开映像失败: " + WimgApi.LastError());
        }

        try
        {
            WimgApi.WIMSetTemporaryPath(handle, Path.GetTempPath());

            var image = WimgApi.WIMLoadImage(handle, (uint)request.ImageIndex);
            if (image == IntPtr.Zero)
            {
                return OperationResult.Fail("加载映像索引失败: " + WimgApi.LastError());
            }

            try
            {
                using var callback = new CaptureCallback(image, progress, cancellationToken, "正在释放");
                var flags = request.Verify ? WimgApi.FlagVerify : 0;

                Directory.CreateDirectory(request.TargetPath);
                progress?.Report(new WimProgress(0, 0, 0, "正在释放"));

                if (!WimgApi.WIMApplyImage(image, request.TargetPath, flags))
                {
                    return OperationResult.Fail("释放失败: " + (callback.Error ?? WimgApi.LastError()));
                }

                _logger.Audit("Wim.Apply", request.ImagePath, $"索引 {request.ImageIndex} -> {request.TargetPath}");
                return OperationResult.Ok("映像已释放完成", 1);
            }
            finally
            {
                WimgApi.WIMCloseHandle(image);
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Wim", "释放映像失败", ex);
            return OperationResult.Fail(ex.Message);
        }
        finally
        {
            WimgApi.WIMCloseHandle(handle);
        }
    }, cancellationToken);

    public Task<OperationResult> SetImageInfoAsync(
        string imagePath,
        int imageIndex,
        string name,
        string? description,
        CancellationToken cancellationToken) => Task.Run(() =>
    {
        var handle = WimgApi.WIMCreateFile(
            imagePath,
            WimgApi.GenericRead | WimgApi.GenericWrite,
            WimgApi.OpenExisting,
            0,
            0,
            out _);

        if (handle == IntPtr.Zero)
        {
            return OperationResult.Fail("打开映像失败: " + WimgApi.LastError());
        }

        try
        {
            var xml = ReadXml(handle);
            if (xml is null)
            {
                return OperationResult.Fail("读取映像信息失败");
            }

            var document = XDocument.Parse(xml);
            var image = document.Root?
                .Elements("IMAGE")
                .FirstOrDefault(e => (string?)e.Attribute("INDEX") == imageIndex.ToString());

            if (image is null)
            {
                return OperationResult.Fail("找不到指定索引");
            }

            image.SetElementValue("NAME", name);
            image.SetElementValue("DESCRIPTION", description ?? string.Empty);

            var bytes = global::System.Text.Encoding.Unicode.GetBytes(document.ToString());
            if (!WimgApi.WIMSetImageInformation(handle, bytes, (uint)bytes.Length))
            {
                return OperationResult.Fail("写入映像信息失败: " + WimgApi.LastError());
            }

            _logger.Audit("Wim.SetInfo", imagePath, $"索引 {imageIndex} → {name}");
            return OperationResult.Ok("映像信息已更新", 1);
        }
        catch (Exception ex)
        {
            _logger.Error("Wim", "修改映像信息失败", ex);
            return OperationResult.Fail(ex.Message);
        }
        finally
        {
            WimgApi.WIMCloseHandle(handle);
        }
    }, cancellationToken);

    private static byte[] BuildImageXml(string name, string? description)
    {
        var document = new XDocument(
            new XElement("WIM",
                new XElement("IMAGE",
                    new XAttribute("INDEX", "1"),
                    new XElement("NAME", name),
                    new XElement("DESCRIPTION", description ?? string.Empty))));

        var text = document.ToString();
        return global::System.Text.Encoding.Unicode.GetBytes(text);
    }

    private static string? ReadXml(IntPtr handle)
    {
        if (!WimgApi.WIMGetImageInformation(handle, out var pointer, out var size) || pointer == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            return Marshal.PtrToStringUni(pointer, (int)(size / 2));
        }
        finally
        {
            Marshal.FreeHGlobal(pointer);
        }
    }

    private static IReadOnlyList<WimImageInfo> ParseImages(string xml)
    {
        var list = new List<WimImageInfo>();

        try
        {
            var document = XDocument.Parse(xml);
            foreach (var image in document.Root?.Elements("IMAGE") ?? [])
            {
                var index = int.TryParse((string?)image.Attribute("INDEX"), out var parsed) ? parsed : 0;
                var totalBytes = long.TryParse((string?)image.Element("TOTALBYTES"), out var bytes) ? bytes : (long?)null;
                var fileCount = int.TryParse((string?)image.Element("FILECOUNT"), out var files) ? files : (int?)null;

                list.Add(new WimImageInfo(
                    index,
                    (string?)image.Element("NAME") ?? $"映像 {index}",
                    (string?)image.Element("DESCRIPTION"),
                    totalBytes,
                    fileCount,
                    (string?)image.Element("COMPRESSION")));
            }
        }
        catch
        {
            // 解析失败返回空列表
        }

        return list;
    }

    private async Task<OperationResult> ConvertAsync(
        string wimPath,
        string targetPath,
        string extension,
        int splitSizeMb,
        CancellationToken cancellationToken)
    {
        ProcessResult result;

        if (extension == ".esd")
        {
            result = await ProcessRunner.RunAsync(
                "dism.exe",
                ["/English", "/Export-Image", "/SourceImageFile:" + wimPath, "/SourceIndex:1",
                 "/DestinationImageFile:" + targetPath, "/Compress:recovery", "/CheckIntegrity"],
                3_600_000,
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            result = await ProcessRunner.RunAsync(
                "dism.exe",
                ["/English", "/Split-Image", "/ImageFile:" + wimPath,
                 "/SWMFile:" + targetPath, "/FileSize:" + (splitSizeMb > 0 ? splitSizeMb : 4096), "/CheckIntegrity"],
                3_600_000,
                cancellationToken).ConfigureAwait(false);
        }

        if (result.ExitCode is 0 or 3010)
        {
            return OperationResult.Ok("格式转换完成", 1);
        }

        return OperationResult.Fail("格式转换失败: " + result.Combined.Trim());
    }

    private async Task<OperationResult> ApplyWithDism(WimApplyRequest request, CancellationToken cancellationToken)
    {
        var arguments = new List<string>
        {
            "/English",
            "/Apply-Image",
            "/ImageFile:" + request.ImagePath,
            "/Index:" + request.ImageIndex,
            "/ApplyDir:" + request.TargetPath
        };

        if (request.Verify)
        {
            arguments.Add("/Verify");
        }

        var result = await ProcessRunner.RunAsync(
            "dism.exe",
            arguments,
            7_200_000,
            cancellationToken).ConfigureAwait(false);

        _logger.Audit("Wim.ApplyDism", request.ImagePath, $"索引 {request.ImageIndex} -> {request.TargetPath}");

        if (result.ExitCode is not (0 or 3010))
        {
            return OperationResult.Fail("释放失败: " + result.Combined.Trim());
        }

        // 释放完成后可选修复引导：bcdboot 是系统里唯一的引导写入入口
        if (request.RepairBoot)
        {
            var windowsDir = Path.Combine(request.TargetPath, "Windows");
            if (!Directory.Exists(windowsDir))
            {
                return OperationResult.Ok("映像已释放，但目标分区没有 Windows 目录，已跳过引导修复", 1);
            }

            var boot = await ProcessRunner.RunAsync(
                "bcdboot.exe",
                [windowsDir, "/f", "ALL"],
                180_000,
                cancellationToken).ConfigureAwait(false);

            _logger.Audit("Wim.BootRepair", request.TargetPath, $"exit={boot.ExitCode}");

            return boot.ExitCode == 0
                ? OperationResult.Ok("映像已释放，引导已修复", 1)
                : OperationResult.Ok("映像已释放，但引导修复失败：" + boot.Combined.Trim(), 1);
        }

        return OperationResult.Ok("映像已释放完成", 1);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // 忽略
        }
    }

    /// <summary>WIMGAPI 进度回调：把百分比转成 WimProgress，并在取消时中止操作。</summary>
    private sealed class CaptureCallback : IDisposable
    {
        private readonly IntPtr _handle;
        private readonly IProgress<WimProgress>? _progress;
        private readonly CancellationToken _token;
        private readonly string _stage;
        private readonly WimgApi.MessageCallback _callback;

        public CaptureCallback(IntPtr handle, IProgress<WimProgress>? progress, CancellationToken token, string stage)
        {
            _handle = handle;
            _progress = progress;
            _token = token;
            _stage = stage;
            _callback = OnMessage;
            WimgApi.WIMRegisterMessageCallback(handle, _callback, IntPtr.Zero);
        }

        public string? Error { get; private set; }

        private uint OnMessage(uint messageId, IntPtr wParam, IntPtr lParam, IntPtr userData)
        {
            if (_token.IsCancellationRequested)
            {
                return 1;
            }

            switch (messageId)
            {
                case WimgApi.MsgPercent:
                    var percent = (int)(wParam.ToInt64() & 0x7FFFFFFF);
                    _progress?.Report(new WimProgress(0, 0, Math.Clamp(percent, 0, 100), _stage));
                    break;

                case WimgApi.MsgError:
                    var code = (uint)lParam.ToInt64();
                    if (code != 0)
                    {
                        Error = $"错误 {code}";
                    }

                    break;
            }

            return 0;
        }

        public void Dispose()
        {
            try
            {
                WimgApi.WIMUnregisterMessageCallback(_handle, _callback);
            }
            catch
            {
                // 忽略
            }
        }
    }
}
