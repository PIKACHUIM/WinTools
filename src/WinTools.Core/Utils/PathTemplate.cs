namespace WinTools.Core.Utils;

/// <summary>
/// 路径模板展开器。支持：
/// <list type="bullet">
/// <item>%ENV% 环境变量</item>
/// <item>{Drive} 所有固定磁盘根</item>
/// <item>{AllUsers} 所有真实用户配置目录</item>
/// <item>* 段通配（可出现在任意中间段）</item>
/// </list>
/// 展开过程绝不做全盘递归，只按段枚举。
/// </summary>
public static class PathTemplate
{
    private static readonly string[] ExcludedProfiles =
    [
        "Default", "Default User", "All Users", "defaultuser0", "Public", "公用", "默认", "默认用户"
    ];

    public static IReadOnlyList<string> Expand(string template, bool lastSegmentIsFile = false, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return [];
        }

        var results = new List<string>();

        foreach (var withDrive in ExpandDrives(template))
        {
            foreach (var withUsers in ExpandAllUsers(withDrive))
            {
                var expanded = SafeExpandEnvironment(withUsers);
                if (string.IsNullOrWhiteSpace(expanded))
                {
                    continue;
                }

                results.AddRange(ExpandWildcards(expanded, lastSegmentIsFile, cancellationToken));
            }
        }

        return results
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IEnumerable<string> ExpandDrives(string template)
    {
        if (!template.Contains("{Drive}", StringComparison.OrdinalIgnoreCase))
        {
            yield return template;
            yield break;
        }

        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (drive.DriveType is not (DriveType.Fixed or DriveType.Removable) || !drive.IsReady)
                {
                    continue;
                }
            }
            catch
            {
                continue;
            }

            yield return template.Replace("{Drive}", drive.RootDirectory.FullName.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
        }
    }

    private static IEnumerable<string> ExpandAllUsers(string template)
    {
        if (!template.Contains("{AllUsers}", StringComparison.OrdinalIgnoreCase))
        {
            yield return template;
            yield break;
        }

        var usersRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "..", "Users");
        usersRoot = Path.GetFullPath(usersRoot);

        if (!Directory.Exists(usersRoot))
        {
            yield break;
        }

        string[] directories;
        try
        {
            directories = Directory.GetDirectories(usersRoot);
        }
        catch
        {
            yield break;
        }

        foreach (var dir in directories)
        {
            var name = Path.GetFileName(dir);
            if (ExcludedProfiles.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return template.Replace("{AllUsers}", dir, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string SafeExpandEnvironment(string path)
    {
        try
        {
            return Environment.ExpandEnvironmentVariables(path);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static IReadOnlyList<string> ExpandWildcards(string path, bool lastSegmentIsFile, CancellationToken cancellationToken)
    {
        // 统一分隔符，去掉根后的空段
        var normalized = path.Replace('/', '\\');
        var segments = normalized.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return [];
        }

        var current = new List<string>();

        // 第一段是盘符或 UNC 根
        if (segments[0].EndsWith(':'))
        {
            current.Add(segments[0] + "\\");
        }
        else
        {
            current.Add(segments[0]);
        }

        for (var i = 1; i < segments.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var segment = segments[i];
            var isLast = i == segments.Length - 1;
            var treatAsFile = isLast && lastSegmentIsFile;

            if (!segment.Contains('*') && !segment.Contains('?'))
            {
                for (var c = 0; c < current.Count; c++)
                {
                    current[c] = Path.Combine(current[c], segment);
                }

                continue;
            }

            var next = new List<string>();
            foreach (var baseDir in current)
            {
                try
                {
                    if (!Directory.Exists(baseDir))
                    {
                        continue;
                    }

                    var entries = treatAsFile
                        ? Directory.EnumerateFiles(baseDir, segment)
                        : Directory.EnumerateDirectories(baseDir, segment);

                    foreach (var entry in entries)
                    {
                        next.Add(entry);
                        if (next.Count >= 4096)
                        {
                            break;
                        }
                    }
                }
                catch
                {
                    // 忽略无权限目录
                }
            }

            current = next;
            if (current.Count == 0)
            {
                return [];
            }
        }

        return current;
    }
}
