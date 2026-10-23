using System.Globalization;

namespace WinTools.Core.Utils;

public static class SizeFormatter
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB", "PB"];

    public static string Format(long bytes)
    {
        if (bytes <= 0)
        {
            return "0 B";
        }

        var value = (double)bytes;
        var unit = 0;
        while (value >= 1024d && unit < Units.Length - 1)
        {
            value /= 1024d;
            unit++;
        }

        var format = unit == 0 ? "0" : value < 10 ? "0.##" : value < 100 ? "0.#" : "0";
        return value.ToString(format, CultureInfo.CurrentCulture) + " " + Units[unit];
    }

    public static string FormatPerSecond(long bytes) => Format(bytes) + "/s";
}
