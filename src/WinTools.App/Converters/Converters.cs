using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WinTools.App.Converters;

/// <summary>进度条填充宽度 = 轨道宽度 × 值 / 最大值</summary>
public sealed class ProgressWidthConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 3)
        {
            return 0d;
        }

        if (!double.TryParse(values[0]?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var width) ||
            !double.TryParse(values[1]?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ||
            !double.TryParse(values[2]?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var maximum) ||
            maximum <= 0)
        {
            return 0d;
        }

        return Math.Max(0d, Math.Min(width, width * value / maximum));
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Visibility.Collapsed;
}

public sealed class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class BoolToFlowDirectionConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is FlowDirection.RightToLeft;
}

public sealed class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int count && count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>字节数 → 人类可读体积。</summary>
public sealed class BytesToSizeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        try
        {
            return value is null ? "—" : Core.Utils.SizeFormatter.Format(System.Convert.ToInt64(value));
        }
        catch
        {
            return "—";
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>0~100 的数值 → "45%"。</summary>
public sealed class PercentTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        try
        {
            return value is null ? "0%" : $"{System.Convert.ToDouble(value):0}%";
        }
        catch
        {
            return "0%";
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>每秒字节 → "1.2 MB/s"。</summary>
public sealed class BytesPerSecondConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        try
        {
            var perSecond = value is null ? 0 : System.Convert.ToDouble(value);
            return Core.Utils.SizeFormatter.Format((long)perSecond) + "/s";
        }
        catch
        {
            return "0 B/s";
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Value / Maximum → 0~1 的比例（用于进度条缩放，避免绑定 ActualWidth 造成布局反馈）。</summary>
public sealed class ProgressRatioConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        try
        {
            if (values.Length < 2)
            {
                return 0d;
            }

            var value = values[0] is null ? 0d : System.Convert.ToDouble(values[0]);
            var maximum = values[1] is null ? 0d : System.Convert.ToDouble(values[1]);

            if (maximum <= 0)
            {
                maximum = 100d;
            }

            return Math.Clamp(value / maximum, 0d, 1d);
        }
        catch
        {
            return 0d;
        }
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>null → Collapsed，非 null → Visible。</summary>
public sealed class NullToCollapsedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}