namespace WinTools.Core.Utils;

/// <summary>
/// 最后一道防线：即使规则配置写错，也绝不删除这些路径本身。
/// 注意：只拦截「受保护目录本身及其祖先」，不拦截其子目录（如 C:\Windows\Temp 允许）。
/// </summary>
public static class SafetyGuard
{
    private static readonly Lazy<string[]> ProtectedRoots = new(BuildProtectedRoots, isThreadSafe: true);

    private static string[] BuildProtectedRoots()
    {
        var roots = new List<string>();
        void Add(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            try
            {
                roots.Add(Normalize(Path.GetFullPath(path)));
            }
            catch
            {
                // ignore
            }
        }

        Add(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.System));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.SystemX86));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.Windows) is { Length: > 0 } win
            ? Path.Combine(Path.GetDirectoryName(win)!, "Users")
            : null);
        Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "WinSxS"));

        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (drive.IsReady && drive.DriveType == DriveType.Fixed)
                {
                    Add(drive.RootDirectory.FullName);
                }
            }
            catch
            {
                // ignore
            }
        }

        return roots.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string Normalize(string path) => path.TrimEnd('\\', '/');

    /// <summary>返回 true 表示禁止删除该路径。</summary>
    public static bool IsProtected(string path) => IsProtected(path, out _);

    public static bool IsProtected(string path, out string reason)
    {
        reason = string.Empty;

        if (string.IsNullOrWhiteSpace(path))
        {
            reason = "empty path";
            return true;
        }

        if (path.Contains("..", StringComparison.Ordinal))
        {
            reason = "path traversal";
            return true;
        }

        string full;
        try
        {
            full = Normalize(Path.GetFullPath(path));
        }
        catch
        {
            reason = "invalid path";
            return true;
        }

        if (full.Length <= 3)
        {
            reason = "drive root";
            return true;
        }

        foreach (var root in ProtectedRoots.Value)
        {
            if (full.Equals(root, StringComparison.OrdinalIgnoreCase))
            {
                reason = "protected root: " + root;
                return true;
            }

            // full 是某个受保护目录的祖先 → 也不允许
            if (root.StartsWith(full + "\\", StringComparison.OrdinalIgnoreCase))
            {
                reason = "ancestor of protected root: " + root;
                return true;
            }
        }

        return false;
    }
}
