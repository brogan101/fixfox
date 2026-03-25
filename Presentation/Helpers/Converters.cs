using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using HelpDesk.Domain.Enums;
using HelpDesk.Domain.Models;

namespace HelpDesk.Presentation.Helpers;

/// <summary>Collapses element when value is null, empty string, or zero int.</summary>
[ValueConversion(typeof(object), typeof(Visibility))]
public sealed class NullToCollapsedConverter : IValueConverter
{
    public object Convert(object? value, Type t, object p, CultureInfo c) =>
        value switch
        {
            null          => Visibility.Collapsed,
            string s      => string.IsNullOrEmpty(s) ? Visibility.Collapsed : Visibility.Visible,
            int i         => i == 0 ? Visibility.Collapsed : Visibility.Visible,
            bool b        => b ? Visibility.Visible : Visibility.Collapsed,
            _             => Visibility.Visible,
        };
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>Shows an element only when a string is null, empty, or whitespace.</summary>
[ValueConversion(typeof(string), typeof(Visibility))]
public sealed class NullOrWhiteSpaceToVisibleConverter : IValueConverter
{
    public object Convert(object? value, Type t, object p, CultureInfo c) =>
        value is string s && !string.IsNullOrWhiteSpace(s)
            ? Visibility.Collapsed
            : Visibility.Visible;

    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>Inverts a bool.</summary>
[ValueConversion(typeof(bool), typeof(bool))]
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? v, Type t, object p, CultureInfo c) => v is bool b ? !b : true;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => v is bool b ? !b : false;
}

/// <summary>Bool → Visibility (true=Visible, false=Collapsed).</summary>
[ValueConversion(typeof(bool), typeof(Visibility))]
public sealed class BoolToVisConverter : IValueConverter
{
    public object Convert(object? v, Type t, object p, CultureInfo c) =>
        v is true ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) =>
        v is Visibility vis && vis == Visibility.Visible;
}

/// <summary>Bool → Visibility inverted (false=Visible, true=Collapsed).</summary>
[ValueConversion(typeof(bool), typeof(Visibility))]
public sealed class InverseBoolToVisConverter : IValueConverter
{
    public object Convert(object? v, Type t, object p, CultureInfo c) =>
        v is true ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>Float percentage → colour brush: 0-60=green, 61-80=amber, 81+=red.</summary>
[ValueConversion(typeof(float), typeof(Brush))]
public sealed class PercentColorConverter : IValueConverter
{
    private static readonly SolidColorBrush Green = Frozen(0x22, 0xC5, 0x5E);
    private static readonly SolidColorBrush Amber = Frozen(0xF5, 0x9E, 0x0B);
    private static readonly SolidColorBrush Red   = Frozen(0xEF, 0x44, 0x44);

    public object Convert(object? v, Type t, object p, CultureInfo c)
    {
        var pct = v is null ? 0 : System.Convert.ToDouble(v);
        return pct switch { <= 60 => Green, <= 80 => Amber, _ => Red };
    }
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
    private static SolidColorBrush Frozen(byte r, byte g, byte b)
    {
        var br = new SolidColorBrush(Color.FromRgb(r, g, b));
        br.Freeze();
        return br;
    }
}

/// <summary>ScanSeverity → colour brush.</summary>
[ValueConversion(typeof(ScanSeverity), typeof(Brush))]
public sealed class SeverityColorConverter : IValueConverter
{
    private static readonly SolidColorBrush Good     = MakeBrush(0x22, 0xC5, 0x5E);
    private static readonly SolidColorBrush Warning  = MakeBrush(0xF5, 0x9E, 0x0B);
    private static readonly SolidColorBrush Critical = MakeBrush(0xEF, 0x44, 0x44);

    public object Convert(object? v, Type t, object p, CultureInfo c) =>
        v is ScanSeverity s ? s switch
        {
            ScanSeverity.Critical => Critical,
            ScanSeverity.Warning  => Warning,
            _                     => Good
        } : Good;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
    private static SolidColorBrush MakeBrush(byte r, byte g, byte b)
    {
        var br = new SolidColorBrush(Color.FromRgb(r, g, b)); br.Freeze(); return br;
    }
}

/// <summary>NotifLevel → colour brush.</summary>
[ValueConversion(typeof(NotifLevel), typeof(Brush))]
public sealed class NotifLevelColorConverter : IValueConverter
{
    private static readonly SolidColorBrush InfoBrush = MakeBrush(0x3B, 0x82, 0xF6);
    private static readonly SolidColorBrush WarnBrush = MakeBrush(0xF5, 0x9E, 0x0B);
    private static readonly SolidColorBrush CritBrush = MakeBrush(0xEF, 0x44, 0x44);

    public object Convert(object? v, Type t, object p, CultureInfo c) =>
        v is NotifLevel l ? l switch
        {
            NotifLevel.Critical => CritBrush,
            NotifLevel.Warning  => WarnBrush,
            _                   => InfoBrush
        } : InfoBrush;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
    private static SolidColorBrush MakeBrush(byte r, byte g, byte b)
    {
        var br = new SolidColorBrush(Color.FromRgb(r, g, b)); br.Freeze(); return br;
    }
}

/// <summary>FixStatus → background brush for output panel.</summary>
[ValueConversion(typeof(FixStatus), typeof(Brush))]
public sealed class StatusBgConverter : IValueConverter
{
    private static readonly SolidColorBrush SuccessBg = MakeBrush(0x22, 0xC5, 0x5E, 0x18);
    private static readonly SolidColorBrush FailBg    = MakeBrush(0xEF, 0x44, 0x44, 0x18);
    private static readonly SolidColorBrush RunBg     = MakeBrush(0xE8, 0x72, 0x0C, 0x18);
    private static readonly SolidColorBrush NoneBg    = new(Color.FromArgb(0, 0, 0, 0));

    public object Convert(object? v, Type t, object p, CultureInfo c) =>
        v is FixStatus s ? s switch
        {
            FixStatus.Success => SuccessBg,
            FixStatus.Failed  => FailBg,
            FixStatus.Running => RunBg,
            _                 => NoneBg
        } : NoneBg;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
    private static SolidColorBrush MakeBrush(byte r, byte g, byte b, byte a)
    {
        var br = new SolidColorBrush(Color.FromArgb(a, r, g, b)); br.Freeze(); return br;
    }
}

/// <summary>FixStatus → foreground colour for output text.</summary>
[ValueConversion(typeof(FixStatus), typeof(Brush))]
public sealed class StatusFgConverter : IValueConverter
{
    private static readonly SolidColorBrush SuccessFg = MakeBrush(0x22, 0xC5, 0x5E);
    private static readonly SolidColorBrush FailFg    = MakeBrush(0xEF, 0x44, 0x44);
    private static readonly SolidColorBrush RunFg     = MakeBrush(0xE8, 0x72, 0x0C);
    private static readonly SolidColorBrush DefaultFg = MakeBrush(0x7E, 0x8F, 0xAD);

    public object Convert(object? v, Type t, object p, CultureInfo c) =>
        v is FixStatus s ? s switch
        {
            FixStatus.Success => SuccessFg,
            FixStatus.Failed  => FailFg,
            FixStatus.Running => RunFg,
            _                 => DefaultFg
        } : DefaultFg;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
    private static SolidColorBrush MakeBrush(byte r, byte g, byte b)
    {
        var br = new SolidColorBrush(Color.FromRgb(r, g, b)); br.Freeze(); return br;
    }
}

/// <summary>FixStatus → Visibility (Visible only when Running).</summary>
[ValueConversion(typeof(FixStatus), typeof(Visibility))]
public sealed class RunningVisConverter : IValueConverter
{
    public object Convert(object? v, Type t, object p, CultureInfo c) =>
        v is FixStatus.Running ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>FixStatus → icon character (Segoe MDL2).</summary>
[ValueConversion(typeof(FixStatus), typeof(string))]
public sealed class StatusIconConverter : IValueConverter
{
    public object Convert(object? v, Type t, object p, CultureInfo c) =>
        v is FixStatus s ? s switch
        {
            FixStatus.Success => "\uE73E",  // Accept checkmark
            FixStatus.Failed  => "\uE711",  // Error X
            FixStatus.Running => "\uE895",  // Sync/loading
            _                 => "\uE90F",  // Repair wrench
        } : "\uE90F";
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>Bool → star icon character (favourite toggle).</summary>
[ValueConversion(typeof(ScanSeverity), typeof(string))]
public sealed class SeverityIconConverter : IValueConverter
{
    public object Convert(object? v, Type t, object p, CultureInfo c) =>
        v is ScanSeverity s ? s switch
        {
            ScanSeverity.Good     => "\uE73E",
            ScanSeverity.Warning  => "\uE7BA",
            ScanSeverity.Critical => "\uE711",
            _                     => "\uE90F",
        } : "\uE90F";
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>Bool â†’ star icon character (favourite toggle).</summary>
[ValueConversion(typeof(bool), typeof(string))]
public sealed class FavoriteIconConverter : IValueConverter
{
    public object Convert(object? v, Type t, object p, CultureInfo c) =>
        v is true ? "\uE735" : "\uE734";   // FavoriteList vs Favorite
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>Bool → pin icon character.</summary>
[ValueConversion(typeof(bool), typeof(string))]
public sealed class PinIconConverter : IValueConverter
{
    public object Convert(object? v, Type t, object p, CultureInfo c) =>
        v is true ? "\uE77A" : "\uE718";   // UnPin vs Pin
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>DateTime → relative time string ("just now", "5 min ago", etc.).</summary>
[ValueConversion(typeof(DateTime), typeof(string))]
public sealed class RelativeDateConverter : IValueConverter
{
    public object Convert(object? v, Type t, object p, CultureInfo c)
    {
        if (v is not DateTime dt) return "";
        var diff = DateTime.Now - dt;
        if (diff.TotalSeconds < 60)  return "just now";
        if (diff.TotalMinutes < 60)  return $"{(int)diff.TotalMinutes} min ago";
        if (diff.TotalHours < 24)    return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 2)      return "Yesterday";
        return dt.ToString("MMM d");
    }
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>True if theme string == "Dark".</summary>
[ValueConversion(typeof(string), typeof(bool))]
public sealed class ThemeIsDarkConverter : IValueConverter
{
    public object Convert(object? v, Type t, object p, CultureInfo c) =>
        v is string s && s == "Dark";
    public object ConvertBack(object v, Type t, object p, CultureInfo c) =>
        v is true ? "Dark" : "Light";
}

/// <summary>Sidebar width: collapsed=48, expanded=222.</summary>
[ValueConversion(typeof(bool), typeof(double))]
public sealed class SidebarWidthConverter : IValueConverter
{
    public object Convert(object? v, Type t, object p, CultureInfo c) =>
        v is true ? 48.0 : 222.0;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>Sidebar padding: collapsed=centered, expanded=left-aligned.</summary>
[ValueConversion(typeof(bool), typeof(Thickness))]
public sealed class SidebarPaddingConverter : IValueConverter
{
    public object Convert(object? v, Type t, object p, CultureInfo c) =>
        v is true ? new Thickness(0, 11, 0, 11) : new Thickness(14, 11, 14, 11);
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>Sidebar HorizontalContentAlignment: collapsed=Center, expanded=Left.</summary>
[ValueConversion(typeof(bool), typeof(HorizontalAlignment))]
public sealed class SidebarAlignConverter : IValueConverter
{
    public object Convert(object? v, Type t, object p, CultureInfo c) =>
        v is true ? HorizontalAlignment.Center : HorizontalAlignment.Left;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>RequiresAdmin bool → shield icon visibility.</summary>
[ValueConversion(typeof(bool), typeof(Visibility))]
public sealed class AdminShieldVisConverter : IValueConverter
{
    public object Convert(object? v, Type t, object p, CultureInfo c) =>
        v is true ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}
