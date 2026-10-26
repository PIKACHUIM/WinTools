using System.Reflection;
using System.Text.Json;

namespace WinTools.Infrastructure.Updates;

/// <summary>
/// 特性 ID → 名称。
/// 数据来源有两处，后者覆盖前者：
///   1) 随程序嵌入的 Rules/feature-flags.json（内置常用条目）
///   2) %ProgramData%\WinTools\feature-flags.json（用户自己补充，不用重新编译）
/// 找不到名称时界面显示「特性 &lt;ID&gt;」，而不是只留一个裸数字。
/// </summary>
internal static class FeatureNames
{
    private static readonly Lazy<Dictionary<string, string>> Cache = new(Load, isThreadSafe: true);

    public static string? GetValueOrDefault(string featureId)
        => Cache.Value.TryGetValue(featureId, out var name) ? name : null;

    private static Dictionary<string, string> Load()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        Merge(map, ReadEmbedded());

        var userFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "WinTools",
            "feature-flags.json");
        Merge(map, ReadFile(userFile));

        return map;
    }

    private static string? ReadEmbedded()
    {
        try
        {
            var assembly = typeof(FeatureNames).Assembly;
            var name = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("feature-flags.json", StringComparison.OrdinalIgnoreCase));

            if (name is null)
            {
                return null;
            }

            using var stream = assembly.GetManifestResourceStream(name);
            using var reader = new StreamReader(stream!);
            return reader.ReadToEnd();
        }
        catch
        {
            return null;
        }
    }

    private static string? ReadFile(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>接受 [{"id":"123","name":"..."}] 或 {"123":"..."} 两种写法。</summary>
    private static void Merge(Dictionary<string, string> map, string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in root.EnumerateArray())
                {
                    if (element.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    var id = element.TryGetProperty("id", out var idNode) ? idNode.ToString() : null;
                    var name = element.TryGetProperty("name", out var nameNode) ? nameNode.GetString() : null;

                    if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(name))
                    {
                        map[id.Trim()] = name.Trim();
                    }
                }

                return;
            }

            if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in root.EnumerateObject())
                {
                    var name = property.Value.GetString();
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        map[property.Name] = name;
                    }
                }
            }
        }
        catch
        {
            // 字典文件格式不对就当作没有，不影响主流程
        }
    }
}
