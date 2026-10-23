using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace WinTools.App.Controls;

/// <summary>环形图的一段。</summary>
public sealed record DonutSlice(double Value, Color Color);

/// <summary>
/// 自绘环形图（圆环分段 + 入场扫过动画）。
/// 直接用 WPF 几何绘制而不是引第三方图表库：开销低、离屏渲染与真实窗口完全一致，
/// 也避开了 Skia 画布在卡片容器里尺寸不对齐的问题。
/// </summary>
public sealed class DonutChart : FrameworkElement
{
    public static readonly DependencyProperty SlicesProperty = DependencyProperty.Register(
        nameof(Slices),
        typeof(IReadOnlyList<DonutSlice>),
        typeof(DonutChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnSlicesChanged));

    /// <summary>圆环厚度（像素）。</summary>
    public static readonly DependencyProperty ThicknessProperty = DependencyProperty.Register(
        nameof(Thickness),
        typeof(double),
        typeof(DonutChart),
        new FrameworkPropertyMetadata(16d, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>轨道颜色（没有数据或作为底环时使用）。</summary>
    public static readonly DependencyProperty TrackProperty = DependencyProperty.Register(
        nameof(Track),
        typeof(Color),
        typeof(DonutChart),
        new FrameworkPropertyMetadata(Color.FromRgb(0xD8, 0xDF, 0xE9), FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>段之间留白的角度。</summary>
    public static readonly DependencyProperty GapAngleProperty = DependencyProperty.Register(
        nameof(GapAngle),
        typeof(double),
        typeof(DonutChart),
        new FrameworkPropertyMetadata(1.5d, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>扫过进度 0~1，用于入场动画。</summary>
    public static readonly DependencyProperty SweepProperty = DependencyProperty.Register(
        nameof(Sweep),
        typeof(double),
        typeof(DonutChart),
        new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsRender));

    public DonutChart()
    {
        Loaded += (_, _) =>
        {
            var animation = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(420))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            BeginAnimation(SweepProperty, animation);
        };
    }

    public IReadOnlyList<DonutSlice>? Slices
    {
        get => (IReadOnlyList<DonutSlice>?)GetValue(SlicesProperty);
        set => SetValue(SlicesProperty, value);
    }

    public double Thickness
    {
        get => (double)GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    public Color Track
    {
        get => (Color)GetValue(TrackProperty);
        set => SetValue(TrackProperty, value);
    }

    public double GapAngle
    {
        get => (double)GetValue(GapAngleProperty);
        set => SetValue(GapAngleProperty, value);
    }

    public double Sweep
    {
        get => (double)GetValue(SweepProperty);
        set => SetValue(SweepProperty, value);
    }

    private static void OnSlicesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((DonutChart)d).InvalidateVisual();

    protected override void OnRender(DrawingContext dc)
    {
        var size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 0)
        {
            return;
        }

        var thickness = Math.Min(Thickness, size / 2);
        var radius = (size - thickness) / 2;
        var center = new Point(ActualWidth / 2, ActualHeight / 2);

        var slices = (Slices ?? []).Where(s => s.Value > 0).ToList();
        var total = slices.Sum(s => s.Value);

        if (total <= 0)
        {
            DrawArc(dc, center, radius, thickness, Track, 0, 359.999);
            return;
        }

        var sweep = Math.Clamp(Sweep, 0, 1);
        var start = -90d;

        foreach (var slice in slices)
        {
            var angle = slice.Value / total * 360d * sweep;
            if (angle <= 0)
            {
                continue;
            }

            var gap = slices.Count > 1 ? GapAngle : 0;
            var drawAngle = Math.Max(0.5, angle - gap);
            DrawArc(dc, center, radius, thickness, slice.Color, start, drawAngle);
            start += angle;
        }
    }

    /// <summary>用一段圆弧描边画出环的一部分；超过 359.9° 时退化为整圆描边。</summary>
    private static void DrawArc(
        DrawingContext dc,
        Point center,
        double radius,
        double thickness,
        Color color,
        double startAngle,
        double sweepAngle)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();

        var pen = new Pen(brush, thickness)
        {
            StartLineCap = PenLineCap.Flat,
            EndLineCap = PenLineCap.Flat
        };
        pen.Freeze();

        if (sweepAngle >= 359.9)
        {
            dc.DrawEllipse(null, pen, center, radius, radius);
            return;
        }

        var startRad = startAngle * Math.PI / 180d;
        var endRad = (startAngle + sweepAngle) * Math.PI / 180d;

        var startPoint = new Point(
            center.X + radius * Math.Cos(startRad),
            center.Y + radius * Math.Sin(startRad));

        var endPoint = new Point(
            center.X + radius * Math.Cos(endRad),
            center.Y + radius * Math.Sin(endRad));

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(startPoint, isFilled: false, isClosed: false);
            ctx.ArcTo(
                endPoint,
                new Size(radius, radius),
                0,
                sweepAngle > 180,
                SweepDirection.Clockwise,
                isStroked: true,
                isSmoothJoin: false);
        }

        geometry.Freeze();
        dc.DrawGeometry(null, pen, geometry);
    }
}
