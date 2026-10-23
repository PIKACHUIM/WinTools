namespace WinTools.Infrastructure.Common;

/// <summary>
/// 通过 WbemScripting COM 访问 WMI（不引入 System.Management 包，保持依赖精简）。
/// 查询结果以字典返回，属性名不区分大小写。
/// </summary>
public static class Wmi
{
    public static IReadOnlyList<Dictionary<string, object?>> Query(
        string wql,
        string scope = @"root\cimv2")
    {
        var rows = new List<Dictionary<string, object?>>();

        try
        {
            var locatorType = Type.GetTypeFromProgID("WbemScripting.SWbemLocator");
            if (locatorType is null)
            {
                return rows;
            }

            dynamic locator = Activator.CreateInstance(locatorType)!;
            dynamic service = locator.ConnectServer(".", scope, null, null);
            service.Security_.ImpersonationLevel = 3;
            dynamic results = service.ExecQuery(wql);

            foreach (dynamic item in results)
            {
                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    foreach (dynamic property in item.Properties_)
                    {
                        string name = property.Name;
                        object? value = property.Value;
                        row[name] = value;
                    }
                }
                catch
                {
                    // 单条记录取属性失败时忽略
                }

                rows.Add(row);
            }

            ReleaseCom(service);
            ReleaseCom(locator);
        }
        catch
        {
            // WMI 不可用时返回空集合，由调用方降级处理
        }

        return rows;
    }

    /// <summary>取第一条记录的某列原始值。</summary>
    public static object? First(IReadOnlyList<Dictionary<string, object?>> rows, string property)
    {
        foreach (var row in rows)
        {
            if (row.TryGetValue(property, out var value) && value is not null)
            {
                return value;
            }
        }

        return null;
    }

    public static string? FirstString(IReadOnlyList<Dictionary<string, object?>> rows, string property)
    {
        foreach (var row in rows)
        {
            if (row.TryGetValue(property, out var value) && value is not null)
            {
                var text = value.ToString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }

        return null;
    }

    private static void ReleaseCom(object? instance)
    {
        try
        {
            if (instance is not null && global::System.Runtime.InteropServices.Marshal.IsComObject(instance))
            {
                global::System.Runtime.InteropServices.Marshal.ReleaseComObject(instance);
            }
        }
        catch
        {
            // 忽略
        }
    }
}
