using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinTools.Infrastructure;

internal static class JsonConfig
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: true) }
    };

    public static readonly JsonSerializerOptions PrettyOptions = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: true) }
    };
}
