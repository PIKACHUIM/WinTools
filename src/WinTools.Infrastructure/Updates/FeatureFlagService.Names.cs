using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace WinTools.Infrastructure.Updates;

/// <summary>
/// 特性 ID → 名称 / 说明。
/// 三层来源，优先级从低到高：
///   1) 程序内嵌的 Rules/feature-flags.json（随版本发布）
///   2) 在线名称表，缓存到 %ProgramData%\WinTools\feature-flags.cache.json
///   3) 用户自己改的 %ProgramData%\WinTools\feature-flags.json
/// 在线更新失败不影响主流程，最差情况退回内置表，再查不到就显示「特性 &lt;ID&gt;」。
/// </summary>
internal static class FeatureNames
{
    /// <summary>名称表的在线地址：指向本仓库的规则文件，改 JSON 即可对已安装用户生效。</summary>
    private const string RemoteUrl =
        "https://raw.githubusercontent.com/PIKACHUIM/WinTools/main/src/WinTools.Infrastructure/Rules/feature-flags.json";

    private static readonly object Sync = new();
    private static Dictionary<string, Entry> _map = LoadLocal();
    private static DateTime _lastRefresh = DateTime.MinValue;

    public static string? GetName(string featureId) => Find(featureId)?.Name;

    public static string? GetDescription(string featureId) => Find(featureId)?.Description;

    private static Entry? Find(string featureId)
    {
        lock (Sync)
        {
            return _map.TryGetValue(featureId, out var entry) ? entry : null;
        }
    }

    /// <summary>从在线表拉一次更新；同一进程内 6 小时内不重复请求。</summary>
    public static async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        lock (Sync)
        {
            if (DateTime.UtcNow - _lastRefresh < TimeSpan.FromHours(6))
            {
                return;
            }

            _lastRefresh = DateTime.UtcNow;
        }

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("WinTools");

            var json = await http.GetStringAsync(RemoteUrl, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            var fetched = Parse(json);
            if (fetched.Count == 0)
            {
                return;
            }

            var cachePath = CachePath();
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            await File.WriteAllTextAsync(cachePath, json, cancellationToken).ConfigureAwait(false);

            lock (Sync)
            {
                _map = Merge(Parse(EmbeddedText() ?? string.Empty), Parse(json), Parse(FileText(UserPath()) ?? string.Empty));
            }
        }
        catch
        {
            // 离线、限流、解析失败都无所谓：继续用已加载的表
        }
    }

    // ------------------------------------------------------------------

    private sealed record Entry(string? Name, string? Description);

    private static Dictionary<string, Entry> LoadLocal()
    {
        lock (Sync)
        {
            return Merge(
                Parse(EmbeddedText() ?? string.Empty),
                Parse(FileText(CachePath()) ?? string.Empty),
                Parse(FileText(UserPath()) ?? string.Empty));
        }
    }

    /// <summary>后加载的覆盖先加载的；name 与 description 分别覆盖，避免只写一半丢字段。</summary>
    private static Dictionary<string, Entry> Merge(params Dictionary<string, Entry>[] layers)
    {
        var result = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);

        foreach (var layer in layers)
        {
            foreach (var (key, value) in layer)
            {
                var existing = result.TryGetValue(key, out var current) ? current : new Entry(null, null);
                result[key] = new Entry(value.Name ?? existing.Name, value.Description ?? existing.Description);
            }
        }

        return result;
    }

    private static Dictionary<string, Entry> Parse(string json)
    {
        var map = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(json))
        {
            return map;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            // 允许两种写法：[{ "id": "123", "name": "...", "description": "..." }] 或 { "123": "名称" }
            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in root.EnumerateArray())
                {
                    if (element.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    var id = element.TryGetProperty("id", out var idNode) ? idNode.ToString() : null;
                    if (string.IsNullOrWhiteSpace(id))
                    {
                        continue;
                    }

                    map[id.Trim()] = new Entry(
                        StringOf(element, "name"),
                        StringOf(element, "description"));
                }

                return map;
            }

            if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in root.EnumerateObject())
                {
                    var name = property.Value.ValueKind == JsonValueKind.String
                        ? property.Value.GetString()
                        : null;

                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        map[property.Name] = new Entry(name, null);
                    }
                }
            }
        }
        catch
        {
            // 格式不对就当没有
        }

        return map;
    }

    private static string? StringOf(JsonElement element, string property)
        => element.TryGetProperty(property, out var node) && node.ValueKind == JsonValueKind.String
            ? node.GetString()
            : null;

    private static string CachePath() => Path.Combine(DataDirectory(), "feature-flags.cache.json");

    private static string UserPath() => Path.Combine(DataDirectory(), "feature-flags.json");

    private static string DataDirectory() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "WinTools");

    private static string? FileText(string path)
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

    private static string? EmbeddedText()
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
}
