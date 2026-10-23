using System.Globalization;
using Microsoft.Win32;
using WinTools.Core.Models;
using WinTools.Infrastructure.Native;

namespace WinTools.Infrastructure.Tweaks;

internal static class RegistryOps
{
    public static RegistryHive ToHive(string? hive) => (hive ?? "HKLM").ToUpperInvariant() switch
    {
        "HKLM" or "HKEY_LOCAL_MACHINE" => RegistryHive.LocalMachine,
        "HKCU" or "HKEY_CURRENT_USER" => RegistryHive.CurrentUser,
        "HKCR" or "HKEY_CLASSES_ROOT" => RegistryHive.ClassesRoot,
        "HKU" or "HKEY_USERS" => RegistryHive.Users,
        "HKCC" or "HKEY_CURRENT_CONFIG" => RegistryHive.CurrentConfig,
        _ => RegistryHive.LocalMachine
    };

    public static RegistryKey? Open(string? hive, string? path, bool writable)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(ToHive(hive), RegistryView.Registry64);
            return baseKey.OpenSubKey(path ?? string.Empty, writable);
        }
        catch
        {
            return null;
        }
    }

    public static object? GetValue(string? hive, string? path, string? name)
    {
        using var key = Open(hive, path, writable: false);
        if (key is null)
        {
            return null;
        }

        try
        {
            return key.GetValue(name ?? string.Empty);
        }
        catch
        {
            return null;
        }
    }

    public static bool ValueMatches(TweakOperation op, out string? actual)
    {
        actual = null;
        var value = GetValue(op.Hive, op.Path, op.Name);
        if (value is null)
        {
            return false;
        }

        actual = value.ToString();

        try
        {
            return op.Kind switch
            {
                RegValueKind.DWord => ToInt(value) == ParseInt(op.Value),
                RegValueKind.QWord => Convert.ToInt64(value, CultureInfo.InvariantCulture) == ParseLong(op.Value),
                RegValueKind.Binary => value is byte[] bytes && Convert.ToHexString(bytes) == (op.Value ?? string.Empty).Replace(" ", string.Empty),
                RegValueKind.MultiString => value is string[] arr && arr.Length > 0 && arr[0] == op.Value,
                _ => string.Equals(value.ToString(), op.Value, StringComparison.Ordinal)
            };
        }
        catch
        {
            return false;
        }
    }

    public static void SetValue(TweakOperation op)
    {
        using var baseKey = RegistryKey.OpenBaseKey(ToHive(op.Hive), RegistryView.Registry64);
        RegistryKey? key = null;
        try
        {
            key = baseKey.OpenSubKey(op.Path ?? string.Empty, writable: true);
            if (key is null)
            {
                if (!op.CreateKey)
                {
                    throw new InvalidOperationException($"注册表项不存在: {op.Hive}\\{op.Path}");
                }

                key = baseKey.CreateSubKey(op.Path ?? string.Empty, writable: true)
                      ?? throw new InvalidOperationException($"无法创建注册表项: {op.Hive}\\{op.Path}");
            }

            var (kind, value) = ConvertValue(op);
            key.SetValue(op.Name ?? string.Empty, value, kind);
        }
        finally
        {
            key?.Dispose();
        }
    }

    public static void DeleteValue(TweakOperation op)
    {
        using var key = Open(op.Hive, op.Path, writable: true);
        if (key is null)
        {
            return;
        }

        try
        {
            key.DeleteValue(op.Name ?? string.Empty, throwOnMissingValue: false);
        }
        catch
        {
            // 忽略
        }
    }

    public static void DeleteKeyTree(TweakOperation op)
    {
        using var baseKey = RegistryKey.OpenBaseKey(ToHive(op.Hive), RegistryView.Registry64);
        try
        {
            baseKey.DeleteSubKeyTree(op.Path ?? string.Empty, throwOnMissingSubKey: false);
        }
        catch
        {
            // 忽略
        }
    }

    private static (RegistryValueKind Kind, object Value) ConvertValue(TweakOperation op)
    {
        var raw = op.Value ?? string.Empty;
        return op.Kind switch
        {
            RegValueKind.DWord => (RegistryValueKind.DWord, ParseInt(raw)),
            RegValueKind.QWord => (RegistryValueKind.QWord, ParseLong(raw)),
            RegValueKind.Binary => (RegistryValueKind.Binary, Convert.FromHexString(raw.Replace(" ", string.Empty))),
            RegValueKind.ExpandString => (RegistryValueKind.ExpandString, raw),
            RegValueKind.MultiString => (RegistryValueKind.MultiString, raw.Split('|', StringSplitOptions.RemoveEmptyEntries)),
            _ => (RegistryValueKind.String, raw)
        };
    }

    private static int ToInt(object value) => value switch
    {
        int i => i,
        long l => unchecked((int)l),
        string s => int.TryParse(s, out var parsed) ? parsed : 0,
        _ => 0
    };

    private static int ParseInt(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 0;
        }

        return uint.TryParse(raw, out var u) ? unchecked((int)u) : 0;
    }

    private static long ParseLong(string? raw)
        => long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;

    // ---------------- 服务 ----------------

    public static uint? ReadServiceStartType(string serviceName)
    {
        var value = GetValue("HKLM", $@"SYSTEM\CurrentControlSet\Services\{serviceName}", "Start");
        return value is null ? null : (uint)ToInt(value);
    }

    public static bool ChangeServiceStartType(string serviceName, uint startType)
    {
        var scm = NativeMethods.OpenSCManager(null, null, NativeMethods.SC_MANAGER_CONNECT);
        if (scm == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            var service = NativeMethods.OpenService(scm, serviceName, NativeMethods.SERVICE_CHANGE_CONFIG);
            if (service == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                return NativeMethods.ChangeServiceConfig(
                    service,
                    NativeMethods.SERVICE_NO_CHANGE,
                    startType,
                    NativeMethods.SERVICE_NO_CHANGE,
                    null, null, IntPtr.Zero, null, null, null, null);
            }
            finally
            {
                NativeMethods.CloseServiceHandle(service);
            }
        }
        finally
        {
            NativeMethods.CloseServiceHandle(scm);
        }
    }

    public static uint ToStartType(string? mode) => (mode ?? "manual").ToLowerInvariant() switch
    {
        "boot" => 0u,
        "system" => 1u,
        "auto" or "automatic" => 2u,
        "manual" or "demand" => 3u,
        "disabled" => 4u,
        _ => 3u
    };
}
