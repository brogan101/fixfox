using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using HelpDesk.Domain.Enums;
using HelpDesk.Domain.Models;
using HelpDesk.Application.Interfaces;
using Wpf.Ui.Controls;

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

/// <summary>Bool â†’ Visibility (true=Visible, false=Collapsed).</summary>
[ValueConversion(typeof(bool), typeof(Visibility))]
public sealed class BoolToVisConverter : IValueConverter
{
    public object Convert(object? v, Type t, object p, CultureInfo c) =>
        v is true ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) =>
        v is Visibility vis && vis == Visibility.Visible;
}

/// <summary>Bool â†’ Visibility inverted (false=Visible, true=Collapsed).</summary>
[ValueConversion(typeof(bool), typeof(Visibility))]
public sealed class InverseBoolToVisConverter : IValueConverter
{
    public object Convert(object? v, Type t, object p, CultureInfo c) =>
        v is true ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>Float percentage â†’ colour brush: 0-60=green, 61-80=amber, 81+=red.</summary>
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

/// <summary>ScanSeverity â†’ colour brush.</summary>
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

/// <summary>NotifLevel â†’ colour brush.</summary>
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

/// <summary>FixStatus â†’ background brush for output panel.</summary>
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

/// <summary>FixStatus â†’ foreground colour for output text.</summary>
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

/// <summary>FixStatus â†’ Visibility (Visible only when Running).</summary>
[ValueConversion(typeof(FixStatus), typeof(Visibility))]
public sealed class RunningVisConverter : IValueConverter
{
    public object Convert(object? v, Type t, object p, CultureInfo c) =>
        v is FixStatus.Running ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>FixStatus â†’ icon character (Segoe MDL2).</summary>
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

/// <summary>Bool â†’ star icon character (favourite toggle).</summary>
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

/// <summary>Bool Ã¢â€ â€™ star icon character (favourite toggle).</summary>
[ValueConversion(typeof(bool), typeof(string))]
public sealed class FavoriteIconConverter : IValueConverter
{
    public object Convert(object? v, Type t, object p, CultureInfo c) =>
        v is true ? "\uE735" : "\uE734";   // FavoriteList vs Favorite
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>Bool â†’ pin icon character.</summary>
[ValueConversion(typeof(bool), typeof(string))]
public sealed class PinIconConverter : IValueConverter
{
    public object Convert(object? v, Type t, object p, CultureInfo c) =>
        v is true ? "\uE77A" : "\uE718";   // UnPin vs Pin
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>DateTime â†’ relative time string ("just now", "5 min ago", etc.).</summary>
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

/// <summary>RequiresAdmin bool â†’ shield icon visibility.</summary>
[ValueConversion(typeof(bool), typeof(Visibility))]
public sealed class AdminShieldVisConverter : IValueConverter
{
    public object Convert(object? v, Type t, object p, CultureInfo c) =>
        v is true ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}

[ValueConversion(typeof(FixRiskLevel), typeof(Brush))]
public sealed class FixRiskLevelBackgroundConverter : IValueConverter
{
    private static readonly SolidColorBrush SafeBrush = MakeBrush(0x22, 0xC5, 0x5E, 0x22);
    private static readonly SolidColorBrush NeedsAdminBrush = MakeBrush(0xF5, 0x9E, 0x0B, 0x22);
    private static readonly SolidColorBrush RestartBrush = MakeBrush(0xF9, 0x73, 0x16, 0x22);
    private static readonly SolidColorBrush AdvancedBrush = MakeBrush(0xEF, 0x44, 0x44, 0x22);

    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture) =>
        value is FixRiskLevel risk ? risk switch
        {
            FixRiskLevel.Safe => SafeBrush,
            FixRiskLevel.NeedsAdmin => NeedsAdminBrush,
            FixRiskLevel.MayRestart => RestartBrush,
            FixRiskLevel.Advanced => AdvancedBrush,
            _ => SafeBrush
        } : SafeBrush;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();

    private static SolidColorBrush MakeBrush(byte r, byte g, byte b, byte a)
    {
        var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        brush.Freeze();
        return brush;
    }
}

[ValueConversion(typeof(FixRiskLevel), typeof(Brush))]
public sealed class FixRiskLevelForegroundConverter : IValueConverter
{
    private static readonly SolidColorBrush SafeBrush = MakeBrush(0x22, 0xC5, 0x5E);
    private static readonly SolidColorBrush NeedsAdminBrush = MakeBrush(0xC2, 0x81, 0x00);
    private static readonly SolidColorBrush RestartBrush = MakeBrush(0xEA, 0x58, 0x0C);
    private static readonly SolidColorBrush AdvancedBrush = MakeBrush(0xDC, 0x26, 0x26);

    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture) =>
        value is FixRiskLevel risk ? risk switch
        {
            FixRiskLevel.Safe => SafeBrush,
            FixRiskLevel.NeedsAdmin => NeedsAdminBrush,
            FixRiskLevel.MayRestart => RestartBrush,
            FixRiskLevel.Advanced => AdvancedBrush,
            _ => SafeBrush
        } : SafeBrush;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();

    private static SolidColorBrush MakeBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}

[ValueConversion(typeof(FixRiskLevel), typeof(string))]
public sealed class FixRiskLevelTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture) =>
        value is FixRiskLevel risk ? risk switch
        {
            FixRiskLevel.NeedsAdmin => "Needs Admin",
            FixRiskLevel.MayRestart => "May Restart",
            _ => risk.ToString()
        } : "Safe";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class FixRiskLevelVariantTextConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var risk = values.Length > 0 && values[0] is FixRiskLevel level ? level : FixRiskLevel.Safe;
        var simplifiedMode = values.Length > 1 && values[1] is bool enabled && enabled;
        var key = risk switch
        {
            FixRiskLevel.NeedsAdmin => "RiskLevel_NeedsAdmin",
            FixRiskLevel.MayRestart => "RiskLevel_MayRestart",
            FixRiskLevel.Advanced => "RiskLevel_Advanced",
            _ => "RiskLevel_Safe"
        };

        var substitutions = App.Services?.GetService(typeof(ITextSubstitutionService)) as ITextSubstitutionService;
        return substitutions?.Get(key, simplifiedModeOverride: simplifiedMode) ?? key;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

[ValueConversion(typeof(string), typeof(string))]
public sealed class FixCategorySymbolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        var title = value as string ?? string.Empty;
        return title switch
        {
            "Network & Wi-Fi" => "Wifi120",
            "Performance & Cleanup" => "Rocket20",
            "Audio & Display" => "DesktopSpeaker20",
            "Updates & Drivers" => "ArrowSync20",
            "File & Storage" => "Database20",
            "Security & Privacy" => "Shield20",
            "Blue Screen & Crashes" => "Bug20",
            "Devices & USB" => "UsbPlug20",
            "Printers & Peripherals" => "Print20",
            "App Issues" => "Wrench20",
            "Email & Office" => "Cloud20",
            "Remote Access & VPN" => "LockClosed20",
            "Sleep & Power" => "Battery1020",
            "Advanced Tools" => "Person20",
            "Gaming & Streaming" => "Games20",
            "Windows Features" => "WindowConsole20",
            _ when title.Contains("Network", StringComparison.OrdinalIgnoreCase) => "Wifi120",
            _ when title.Contains("DNS", StringComparison.OrdinalIgnoreCase) || title.Contains("Browser", StringComparison.OrdinalIgnoreCase) => "Globe20",
            _ when title.Contains("Startup", StringComparison.OrdinalIgnoreCase) => "Timer20",
            _ when title.Contains("Audio", StringComparison.OrdinalIgnoreCase) || title.Contains("Microphone", StringComparison.OrdinalIgnoreCase) => "Speaker220",
            _ when title.Contains("Display", StringComparison.OrdinalIgnoreCase) || title.Contains("Graphics", StringComparison.OrdinalIgnoreCase) => "Desktop20",
            _ when title.Contains("Update", StringComparison.OrdinalIgnoreCase) => "ArrowSync20",
            _ when title.Contains("Storage", StringComparison.OrdinalIgnoreCase) || title.Contains("File", StringComparison.OrdinalIgnoreCase) => "Database20",
            _ when title.Contains("Security", StringComparison.OrdinalIgnoreCase) => "Shield20",
            _ when title.Contains("Crash", StringComparison.OrdinalIgnoreCase) || title.Contains("BSOD", StringComparison.OrdinalIgnoreCase) => "Bug20",
            _ when title.Contains("Device", StringComparison.OrdinalIgnoreCase) || title.Contains("Peripheral", StringComparison.OrdinalIgnoreCase) => "UsbPlug20",
            _ when title.Contains("Printer", StringComparison.OrdinalIgnoreCase) || title.Contains("Scanner", StringComparison.OrdinalIgnoreCase) => "Print20",
            _ when title.Contains("App", StringComparison.OrdinalIgnoreCase) => "Wrench20",
            _ when title.Contains("Office", StringComparison.OrdinalIgnoreCase) || title.Contains("Cloud", StringComparison.OrdinalIgnoreCase) => "Cloud20",
            _ when title.Contains("Remote", StringComparison.OrdinalIgnoreCase) || title.Contains("VPN", StringComparison.OrdinalIgnoreCase) => "LockClosed20",
            _ when title.Contains("Power", StringComparison.OrdinalIgnoreCase) || title.Contains("Battery", StringComparison.OrdinalIgnoreCase) => "Battery1020",
            _ when title.Contains("Account", StringComparison.OrdinalIgnoreCase) || title.Contains("Sign-In", StringComparison.OrdinalIgnoreCase) => "Person20",
            _ when title.Contains("Gaming", StringComparison.OrdinalIgnoreCase) => "Games20",
            _ when title.Contains("Windows", StringComparison.OrdinalIgnoreCase) => "WindowConsole20",
            _ when title.Contains("Diagnosis", StringComparison.OrdinalIgnoreCase) => "Search20",
            _ => "Toolbox20"
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

[ValueConversion(typeof(double), typeof(bool))]
public sealed class WidthLessThanThresholdConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        var width = value is double actualWidth ? actualWidth : 0d;
        var threshold = parameter is string raw && double.TryParse(raw, out var parsed)
            ? parsed
            : 900d;
        return width < threshold;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

[ValueConversion(typeof(double), typeof(GridLength))]
public sealed class PageWidthToRailWidthConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        var width = value is double actualWidth ? actualWidth : 0d;
        return width < 900d ? new GridLength(48) : new GridLength(220);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

[ValueConversion(typeof(Enum), typeof(bool))]
public sealed class EnumEqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture) =>
        string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => System.Windows.Data.Binding.DoNothing;
}

[ValueConversion(typeof(Enum), typeof(Visibility))]
public sealed class EnumEqualsToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture) =>
        string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => System.Windows.Data.Binding.DoNothing;
}

[ValueConversion(typeof(PolicyState), typeof(bool))]
public sealed class LockedToIsEnabledConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        => value is not PolicyState.Locked;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => System.Windows.Data.Binding.DoNothing;
}

[ValueConversion(typeof(PolicyState), typeof(Visibility))]
public sealed class PolicyStateToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        => value is PolicyState.None or null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => System.Windows.Data.Binding.DoNothing;
}

[ValueConversion(typeof(PolicyState), typeof(string))]
public sealed class PolicyStateToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        PolicyState.Managed => "Managed",
        PolicyState.Locked => "Locked by policy",
        PolicyState.Inherited => "Inherited from profile",
        _ => string.Empty
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => System.Windows.Data.Binding.DoNothing;
}

[ValueConversion(typeof(int), typeof(string))]
public sealed class IndexAutomationIdConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        var index = value is int raw ? raw + 1 : 1;
        var tokens = (parameter as string ?? "Item_|").Split('|');
        var prefix = tokens.ElementAtOrDefault(0) ?? string.Empty;
        var suffix = tokens.ElementAtOrDefault(1) ?? string.Empty;
        return $"{prefix}{index}{suffix}";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => System.Windows.Data.Binding.DoNothing;
}

public sealed class IndexedValueAutomationIdConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var index = values.ElementAtOrDefault(0) is int raw ? raw + 1 : 1;
        var tokenValue = values.ElementAtOrDefault(1)?.ToString() ?? string.Empty;
        var tokens = (parameter as string ?? "Item_||").Split('|');
        var prefix = tokens.ElementAtOrDefault(0) ?? string.Empty;
        var middle = tokens.ElementAtOrDefault(1) ?? string.Empty;
        var suffix = tokens.ElementAtOrDefault(2) ?? string.Empty;
        return $"{prefix}{index}{middle}{tokenValue}{suffix}";
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        [System.Windows.Data.Binding.DoNothing, System.Windows.Data.Binding.DoNothing];
}

public sealed class StringEqualsMultiConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2)
            return false;

        var left = values[0]?.ToString();
        var right = values[1]?.ToString();
        return !string.IsNullOrWhiteSpace(left)
               && !string.IsNullOrWhiteSpace(right)
               && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        [System.Windows.Data.Binding.DoNothing, System.Windows.Data.Binding.DoNothing];
}

[ValueConversion(typeof(StartupImpactLevel), typeof(Brush))]
public sealed class StartupImpactLevelBackgroundConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        StartupImpactLevel.Low => new SolidColorBrush(Color.FromArgb(0x22, 0x22, 0xC5, 0x5E)),
        StartupImpactLevel.Medium => new SolidColorBrush(Color.FromArgb(0x22, 0xF5, 0x9E, 0x0B)),
        StartupImpactLevel.High => new SolidColorBrush(Color.FromArgb(0x22, 0xEA, 0x58, 0x0C)),
        _ => new SolidColorBrush(Color.FromArgb(0x22, 0x94, 0xA3, 0xB8))
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => System.Windows.Data.Binding.DoNothing;
}

[ValueConversion(typeof(StartupImpactLevel), typeof(Brush))]
public sealed class StartupImpactLevelForegroundConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        StartupImpactLevel.Low => new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E)),
        StartupImpactLevel.Medium => new SolidColorBrush(Color.FromRgb(0xC2, 0x81, 0x00)),
        StartupImpactLevel.High => new SolidColorBrush(Color.FromRgb(0xEA, 0x58, 0x0C)),
        _ => new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8))
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => System.Windows.Data.Binding.DoNothing;
}

[ValueConversion(typeof(BrowserPermissionRisk), typeof(Brush))]
public sealed class BrowserPermissionRiskBackgroundConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        BrowserPermissionRisk.High => new SolidColorBrush(Color.FromArgb(0x22, 0xEF, 0x44, 0x44)),
        BrowserPermissionRisk.Medium => new SolidColorBrush(Color.FromArgb(0x22, 0xF5, 0x9E, 0x0B)),
        _ => new SolidColorBrush(Color.FromArgb(0x22, 0x22, 0xC5, 0x5E))
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => System.Windows.Data.Binding.DoNothing;
}

[ValueConversion(typeof(BrowserPermissionRisk), typeof(Brush))]
public sealed class BrowserPermissionRiskForegroundConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        BrowserPermissionRisk.High => new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26)),
        BrowserPermissionRisk.Medium => new SolidColorBrush(Color.FromRgb(0xC2, 0x81, 0x00)),
        _ => new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E))
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => System.Windows.Data.Binding.DoNothing;
}
