using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using WinTools.Core.Abstractions;
using WinTools.Core.Models;
using WinTools.Infrastructure.Common;

namespace WinTools.Infrastructure.Migration;

/// <summary>
/// 文件迁移：
/// 1) 系统已知文件夹（桌面/文档/下载/收藏/音乐/图片/视频）——复制、校验、删除源、切换系统路径；
/// 2) 应用数据目录（微信/QQ/WorkBuddy/TraceWork 等）——复制、校验、删除源，在原位置建立目录联接。
/// 所有删除都发生在复制校验通过之后；只复制模式不会改动源目录。
/// </summary>
public sealed class MigrationService : IMigrationService
{
    private const uint WmSettingChange = 0x001A;
    private static readonly IntPtr HwndBroadcast = new(0xFFFF);

    private readonly IAppLogger _logger;

    public MigrationService(IAppLogger logger) => _logger = logger;

    // ---------------------------------------------------------------- 检测

    private static readonly KnownFolderSpec[] KnownFolders =
    [
        new("kf-desktop", new Guid("B4BFCC3A-DB2C-424C-B029-7FE99A87C641"), "Desktop", "Desktop",
            new LocalizedText(("zh-Hans", "桌面"), ("zh-Hant", "桌面"), ("en-US", "Desktop"))),
        new("kf-documents", new Guid("FDD39AD0-238F-46AF-ADB4-6C85480369C7"), "Documents", "Personal",
            new LocalizedText(("zh-Hans", "文档"), ("zh-Hant", "文件"), ("en-US", "Documents"))),
        new("kf-downloads", new Guid("374DE290-123F-4565-9164-39C4925E467B"), "Downloads", "{374DE290-123F-4565-9164-39C4925E467B}",
            new LocalizedText(("zh-Hans", "下载"), ("zh-Hant", "下載"), ("en-US", "Downloads"))),
        new("kf-favorites", new Guid("1777F761-68AD-4D8A-87BD-30B759FA33DD"), "Favorites", "Favorites",
            new LocalizedText(("zh-Hans", "收藏夹"), ("zh-Hant", "我的最愛"), ("en-US", "Favorites"))),
        new("kf-music", new Guid("4BD8D571-6D19-48D3-BE97-422220080E43"), "Music", "My Music",
            new LocalizedText(("zh-Hans", "音乐"), ("zh-Hant", "音樂"), ("en-US", "Music"))),
        new("kf-pictures", new Guid("33E28130-4E1E-4676-835A-98395C3BC3BB"), "Pictures", "My Pictures",
            new LocalizedText(("zh-Hans", "图片"), ("zh-Hant", "圖片"), ("en-US", "Pictures"))),
        new("kf-videos", new Guid("18989B1D-99B5-455B-841C-AB7C74E4DDFC"), "Videos", "My Video",
            new LocalizedText(("zh-Hans", "视频"), ("zh-Hant", "影片"), ("en-US", "Videos")))
    ];

    public Task<IReadOnlyList<MigrationTarget>> DetectAsync(CancellationToken cancellationToken) => Task.Run<IReadOnlyList<MigrationTarget>>(() =>
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var list = new List<MigrationTarget>();

        foreach (var spec in KnownFolders)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var current = GetKnownFolderPath(spec.Guid) ?? Path.Combine(userProfile, spec.Leaf);
            // 已经是目录联接/符号链接的不要重复迁移
            if (!Directory.Exists(current) || IsReparsePoint(current))
            {
                continue;
            }

            list.Add(new MigrationTarget
            {
                Id = spec.Id,
                Kind = MigrationKind.KnownFolder,
                Name = spec.Name,
                Description = new LocalizedText(("zh-Hans", "系统文件夹"), ("en-US", "System folder")),
                CurrentPath = current,
                DefaultPath = Path.Combine(userProfile, spec.Leaf),
                KnownFolderId = spec.Guid,
                Exists = true
            });
        }

        foreach (var app in AppFolders(userProfile))
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 只列出真实存在的应用目录；已迁走并留下目录联接的也跳过
            if (Directory.Exists(app.CurrentPath) && !IsReparsePoint(app.CurrentPath))
            {
                list.Add(new MigrationTarget
                {
                    Id = app.Id,
                    Kind = app.Kind,
                    Name = app.Name,
                    Description = app.Description,
                    CurrentPath = app.CurrentPath,
                    ProcessName = app.ProcessName,
                    Exists = true
                });
            }
        }

        return list;
    }, cancellationToken);

    public Task<(long Bytes, long Files)> MeasureAsync(MigrationTarget target, CancellationToken cancellationToken)
        => Task.Run(() =>
        {
            if (!Directory.Exists(target.CurrentPath))
            {
                return (0L, 0L);
            }

            return Measure(target.CurrentPath, cancellationToken);
        }, cancellationToken);

    private static IEnumerable<MigrationTarget> AppFolders(string userProfile)
    {
        var documents = Path.Combine(userProfile, "Documents");
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        // 微信：老版 WeChat Files 与 4.x xwechat_files 并存
        yield return AppTarget("app-wechat-legacy", "微信聊天记录（WeChat Files）", "WeChat",
            Path.Combine(documents, "WeChat Files"));
        yield return AppTarget("app-wechat-v4", "微信聊天记录（xwechat_files）", "WeChat",
            Path.Combine(documents, "xwechat_files"));

        // QQ：经典版与 NT 版都写 Tencent Files
        yield return AppTarget("app-qq-files", "QQ 聊天记录（Tencent Files）", "QQ",
            Path.Combine(documents, "Tencent Files"));

        foreach (var path in FindAppFolders("WorkBuddy", userProfile, appData, localAppData, documents))
        {
            yield return AppTarget("app-workbuddy-" + Slug(path), "WorkBuddy 数据", "WorkBuddy", path);
        }

        foreach (var path in FindAppFolders("TraceWork", userProfile, appData, localAppData, documents))
        {
            yield return AppTarget("app-tracework-" + Slug(path), "TraceWork 数据", "TraceWork", path);
        }
    }

    private static List<string> FindAppFolders(
        string appName,
        string userProfile,
        string appData,
        string localAppData,
        string documents)
    {
        var candidates = new[]
        {
            Path.Combine(localAppData, appName),
            Path.Combine(appData, appName),
            Path.Combine(documents, appName),
            Path.Combine(userProfile, appName)
        };

        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in candidates)
        {
            if (Directory.Exists(path) && seen.Add(path))
            {
                result.Add(path);
            }
        }

        // 名称可能带后缀（如 WorkBuddy-Data），只在 LocalAppData 浅扫一层
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(localAppData, "*" + appName + "*", SearchOption.TopDirectoryOnly))
            {
                // 只认以应用名开头的目录，避免把 genieworkbuddy-desktop-updater 这类更新器也列进来
                var leaf = Path.GetFileName(dir);
                if (leaf.StartsWith(appName, StringComparison.OrdinalIgnoreCase) && seen.Add(dir))
                {
                    result.Add(dir);
                }
            }
        }
        catch
        {
            // 访问被拒绝时忽略
        }

        return result;
    }

    private static MigrationTarget AppTarget(string id, string name, string processName, string path) => new()
    {
        Id = id,
        Kind = MigrationKind.AppFolder,
        Name = new LocalizedText(("zh-Hans", name), ("en-US", name)),
        Description = new LocalizedText(("zh-Hans", "应用数据目录"), ("en-US", "App data folder")),
        CurrentPath = path,
        ProcessName = processName
    };

    private static string Slug(string path)
    {
        var leaf = Path.GetFileName(path.TrimEnd('\\'));
        var chars = leaf.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-').ToArray();
        return new string(chars).Trim('-');
    }

    private static MigrationTarget WithSize(MigrationTarget target, CancellationToken token)
    {
        if (!Directory.Exists(target.CurrentPath))
        {
            return target;
        }

        try
        {
            var (bytes, files) = Measure(target.CurrentPath, token);
            return new MigrationTarget
            {
                Id = target.Id,
                Kind = target.Kind,
                Name = target.Name,
                Description = target.Description,
                CurrentPath = target.CurrentPath,
                DefaultPath = target.DefaultPath,
                ProcessName = target.ProcessName,
                KnownFolderId = target.KnownFolderId,
                Exists = true,
                SizeBytes = bytes,
                FileCount = files
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return new MigrationTarget
            {
                Id = target.Id,
                Kind = target.Kind,
                Name = target.Name,
                Description = target.Description,
                CurrentPath = target.CurrentPath,
                DefaultPath = target.DefaultPath,
                ProcessName = target.ProcessName,
                KnownFolderId = target.KnownFolderId,
                Exists = true,
                SizeBytes = 0,
                FileCount = 0
            };
        }
    }

    private static (long Bytes, long Files) Measure(string root, CancellationToken token)
    {
        long bytes = 0;
        long files = 0;

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        foreach (var path in Directory.EnumerateFiles(root, "*", options))
        {
            token.ThrowIfCancellationRequested();

            try
            {
                bytes += new FileInfo(path).Length;
                files++;
            }
            catch
            {
                // 单个文件读不到就跳过
            }
        }

        return (bytes, files);
    }

    // ---------------------------------------------------------------- 迁移

    public Task<MigrationItemResult> MigrateAsync(
        MigrationRequest request,
        IProgress<MigrationProgress>? progress,
        CancellationToken cancellationToken) => Task.Run(() =>
    {
        var source = Path.GetFullPath(request.SourcePath);
        var target = Path.GetFullPath(request.TargetPath);

        if (!Directory.Exists(source))
        {
            return Fail(request.Id, "源目录不存在");
        }

        var sourceRoot = Path.GetPathRoot(source);
        var targetRoot = Path.GetPathRoot(target);
        if (string.IsNullOrEmpty(sourceRoot) || string.IsNullOrEmpty(targetRoot))
        {
            return Fail(request.Id, "路径无效");
        }

        if (string.Equals(sourceRoot, targetRoot, StringComparison.OrdinalIgnoreCase))
        {
            return Fail(request.Id, "目标分区与源分区相同，无法腾出空间");
        }

        if (IsInside(target, source) || IsInside(source, target))
        {
            return Fail(request.Id, "目标目录不能位于源目录内部，反之亦然");
        }

        if (Directory.Exists(target) && Directory.EnumerateFileSystemEntries(target).Any())
        {
            return Fail(request.Id, "目标目录已存在且不为空");
        }

        if (request.LinkBack && !string.IsNullOrWhiteSpace(GetRunningProcess(request.Id)))
        {
            return Fail(request.Id, "请先退出 " + GetRunningProcess(request.Id));
        }

        try
        {
            var files = EnumerateFiles(source, cancellationToken);
            var totalBytes = files.Sum(f => f.Length);
            if (totalBytes > 0)
            {
                var drive = new DriveInfo(targetRoot);
                if (drive.AvailableFreeSpace < totalBytes + (64L * 1024 * 1024))
                {
                    return Fail(request.Id, "目标分区剩余空间不足");
                }
            }

            progress?.Report(new MigrationProgress(request.Id, "copy", 0, 0, totalBytes));

            var copied = CopyFiles(files, source, target, request.Id, totalBytes, progress, cancellationToken);

            var (targetBytes, targetFiles) = Measure(target, cancellationToken);
            if (targetFiles < files.Count || targetBytes < totalBytes)
            {
                // 校验不通过时清掉半成品目标，源目录保持原样
                TryDeleteDirectory(target);
                return Fail(request.Id, $"复制校验失败（源 {files.Count} 个 / {totalBytes} B，目标 {targetFiles} 个 / {targetBytes} B）");
            }

            if (request.CopyOnly)
            {
                _logger.Audit("Migration.Copy", source, "-> " + target);
                return new MigrationItemResult(request.Id, true,
                    $"已复制 {copied} 个文件", targetBytes, targetFiles);
            }

            progress?.Report(new MigrationProgress(request.Id, "delete", 100, totalBytes, totalBytes));

            if (!TryDeleteDirectory(source))
            {
                return Fail(request.Id, "文件已复制，但源目录删除失败（可能被占用），系统路径未改动");
            }

            if (request.KnownFolderId != Guid.Empty)
            {
                if (!MoveKnownFolder(request.KnownFolderId, target))
                {
                    // 系统路径没切过去就别把源目录留空，恢复一个空目录至少不影响登录/桌面
                    try
                    {
                        Directory.CreateDirectory(source);
                    }
                    catch
                    {
                        // 忽略
                    }

                    return new MigrationItemResult(request.Id, false,
                        "文件已复制，但系统路径切换失败；已恢复空源目录",
                        targetBytes, targetFiles);
                }

                return new MigrationItemResult(request.Id, true,
                    "已迁移并切换系统路径", targetBytes, targetFiles, true);
            }

            if (request.LinkBack)
            {
                if (!CreateJunction(source, target))
                {
                    return new MigrationItemResult(request.Id, false,
                        "已迁移，但目录联接创建失败；请手动把应用数据指向新位置",
                        targetBytes, targetFiles);
                }
            }

            _logger.Audit("Migration.Move", source, "-> " + target);
            return new MigrationItemResult(request.Id, true,
                request.LinkBack ? "已迁移并创建目录联接" : "已迁移",
                targetBytes, targetFiles);
        }
        catch (OperationCanceledException)
        {
            return Fail(request.Id, "已取消");
        }
        catch (Exception ex)
        {
            _logger.Error("Migration", "迁移失败: " + source, ex);
            return Fail(request.Id, ex.Message);
        }
    }, cancellationToken);

    private static string GetRunningProcess(string id)
    {
        var name = id switch
        {
            var s when s.StartsWith("app-wechat", StringComparison.OrdinalIgnoreCase) => "WeChat",
            "app-qq-files" => "QQ",
            var s when s.StartsWith("app-workbuddy", StringComparison.OrdinalIgnoreCase) => "WorkBuddy",
            var s when s.StartsWith("app-tracework", StringComparison.OrdinalIgnoreCase) => "TraceWork",
            _ => null
        };

        if (name is null)
        {
            return string.Empty;
        }

        try
        {
            return Process.GetProcessesByName(name).Length > 0 ? name : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static List<(string Source, string Relative, long Length)> EnumerateFiles(string root, CancellationToken token)
    {
        var list = new List<(string, string, long)>();
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        foreach (var path in Directory.EnumerateFiles(root, "*", options))
        {
            token.ThrowIfCancellationRequested();

            try
            {
                list.Add((path, Path.GetRelativePath(root, path), new FileInfo(path).Length));
            }
            catch
            {
                // 读不到的文件跳过，校验阶段会体现差异
            }
        }

        return list;
    }

    private static long CopyFiles(
        IReadOnlyList<(string Source, string Relative, long Length)> files,
        string sourceRoot,
        string targetRoot,
        string id,
        long totalBytes,
        IProgress<MigrationProgress>? progress,
        CancellationToken cancellationToken)
    {
        long done = 0;
        var copied = 0;
        var lastReport = 0L;

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var destination = Path.Combine(targetRoot, file.Relative);
            var directory = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            try
            {
                if (File.Exists(destination) && new FileInfo(destination).Length == file.Length)
                {
                    done += file.Length;
                    copied++;
                    continue;
                }

                File.Copy(file.Source, destination, overwrite: true);
                done += file.Length;
                copied++;
            }
            catch (IOException)
            {
                // 单个文件失败：继续复制其余文件，最终由校验拦截
            }
            catch (UnauthorizedAccessException)
            {
            }

            if (done - lastReport > 32L * 1024 * 1024 || copied == files.Count)
            {
                lastReport = done;
                var percent = totalBytes <= 0 ? 100 : (int)Math.Min(100, done * 100 / totalBytes);
                progress?.Report(new MigrationProgress(id, "copy", percent, done, totalBytes));
            }
        }

        return copied;
    }

    private static bool TryDeleteDirectory(string path)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                Directory.Delete(path, recursive: true);
                return true;
            }
            catch (DirectoryNotFoundException)
            {
                return true;
            }
            catch (IOException)
            {
                Thread.Sleep(400);
            }
            catch (UnauthorizedAccessException)
            {
                Thread.Sleep(400);
            }
        }

        return false;
    }

    private static bool CreateJunction(string linkPath, string targetPath)
    {
        var result = ProcessRunner.RunAsync(
            "cmd.exe",
            ["/c", "mklink", "/J", linkPath, targetPath],
            60_000).GetAwaiter().GetResult();

        return result.ExitCode == 0 && Directory.Exists(linkPath);
    }

    private static bool MoveKnownFolder(Guid id, string target)
    {
        var switched = false;

        try
        {
            switched = SHSetKnownFolderPath(ref id, 0, IntPtr.Zero, target) == 0;
        }
        catch
        {
            switched = false;
        }

        // SHSetKnownFolderPath 失败时退回写 User Shell Folders
        if (!switched)
        {
            var name = KnownFolders.FirstOrDefault(f => f.Guid == id)?.RegistryValue;
            if (name is not null)
            {
                try
                {
                    using var key = Registry.CurrentUser.CreateSubKey(
                        @"Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders", writable: true);
                    key?.SetValue(name, target, RegistryValueKind.ExpandString);
                    switched = true;
                }
                catch
                {
                    switched = false;
                }
            }
        }

        if (switched)
        {
            SendMessageTimeout(HwndBroadcast, WmSettingChange, UIntPtr.Zero, "User Shell Folders",
                0x0002, 2000, out _);
        }

        return switched;
    }

    // ---------------------------------------------------------------- 工具

    private static bool IsReparsePoint(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsInside(string child, string parent)
    {
        var p = Path.TrimEndingDirectorySeparator(parent) + Path.DirectorySeparatorChar;
        var c = Path.TrimEndingDirectorySeparator(child) + Path.DirectorySeparatorChar;
        return c.StartsWith(p, StringComparison.OrdinalIgnoreCase);
    }

    private static MigrationItemResult Fail(string id, string message)
        => new(id, false, message);

    private static string? GetKnownFolderPath(Guid id)
    {
        try
        {
            var hr = SHGetKnownFolderPath(ref id, 0, IntPtr.Zero, out var pointer);
            if (hr != 0 || pointer == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                return Marshal.PtrToStringUni(pointer);
            }
            finally
            {
                Marshal.FreeCoTaskMem(pointer);
            }
        }
        catch
        {
            return null;
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHGetKnownFolderPath(ref Guid rfid, uint dwFlags, IntPtr hToken, out IntPtr ppszPath);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHSetKnownFolderPath(ref Guid rfid, uint dwFlags, IntPtr hToken, string pszPath);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd, uint msg, UIntPtr wParam, string lParam, uint flags, uint timeout, out UIntPtr result);

    private sealed record KnownFolderSpec(
        string Id,
        Guid Guid,
        string Leaf,
        string RegistryValue,
        LocalizedText Name);
}
