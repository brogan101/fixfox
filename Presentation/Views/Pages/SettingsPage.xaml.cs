using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using HelpDesk.Shared;
using Microsoft.Win32;
using Wpf.Ui.Controls;
using HelpDesk.Presentation.ViewModels;
using HelpDesk.Presentation.Views;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;

namespace HelpDesk.Presentation.Views.Pages;

public partial class SettingsPage : Page
{
    private readonly MainViewModel _vm;

    public SettingsPage(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = _vm;
    }

    private void ThemeToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleSwitch ts) return;
        var theme = ts.IsChecked == true ? "Dark" : "Light";
        _vm.ApplyTheme(theme);
    }

    private void SettingsToggle_Changed(object sender, RoutedEventArgs e)
        => _vm.SaveSettings();

    private void BehaviorProfile_Changed(object sender, SelectionChangedEventArgs e)
        => _vm.SaveSettings();

    private void LandingPage_Changed(object sender, SelectionChangedEventArgs e)
        => _vm.SaveSettings();

    private void SettingsCombo_Changed(object sender, SelectionChangedEventArgs e)
        => _vm.SaveSettings();

    private void ResetSuppressed_Click(object sender, RoutedEventArgs e)
        => _vm.ResetSuppressedItems();

    private void RunAtStartup_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleSwitch ts) return;
        var enable = ts.IsChecked == true;
        SetRunAtStartupForShell(enable, _vm.ProductDisplayName);
        _vm.Settings.RunAtStartup = enable;
        _vm.SaveSettings();
    }

    public static void SetRunAtStartupForShell(bool enable, string? valueName = null)
    {
        const string keyPath   = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        valueName = string.IsNullOrWhiteSpace(valueName) ? Constants.AppName : valueName;
        using var key = Registry.CurrentUser.OpenSubKey(keyPath, writable: true);
        if (key is null) return;
        if (enable)
        {
            var exe = Environment.ProcessPath
                      ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (exe != null) key.SetValue(valueName, $"\"{exe}\"");
        }
        else
        {
            key.DeleteValue(valueName, throwOnMissingValue: false);
        }
    }

    private void OpenDataFolder_Click(object sender, RoutedEventArgs e)
        => OpenPath(Constants.AppDataDir);

    private void OpenAppLog_Click(object sender, RoutedEventArgs e)
        => OpenPath(Constants.AppLogFile, openParentIfMissing: true);

    private void OpenVerifyLog_Click(object sender, RoutedEventArgs e)
        => OpenPath(Constants.VerifyLogFile, openParentIfMissing: true);

    private void OpenDownloadPage_Click(object sender, RoutedEventArgs e)
    {
        var url = _vm.LastUpdateInfo?.DownloadUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            MessageBox.Show("No download page is available for the current update source.", $"{_vm.ProductDisplayName} - Update Status",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        TryOpenTarget(url, $"{_vm.ProductDisplayName} - Update Status");
    }

    private void OpenQuickStart_Click(object sender, RoutedEventArgs e)
        => OpenPath(_vm.QuickStartPath, openParentIfMissing: true);

    private void OpenPrivacyGuide_Click(object sender, RoutedEventArgs e)
        => OpenPath(_vm.PrivacyGuidePath, openParentIfMissing: true);

    private void OpenSupportGuide_Click(object sender, RoutedEventArgs e)
        => OpenPath(_vm.SupportBundleGuidePath, openParentIfMissing: true);

    private void OpenRecoveryGuide_Click(object sender, RoutedEventArgs e)
        => OpenPath(_vm.RecoveryGuidePath, openParentIfMissing: true);

    private void OpenTroubleshootingGuide_Click(object sender, RoutedEventArgs e)
        => OpenPath(_vm.TroubleshootingGuidePath, openParentIfMissing: true);

    private void OpenReleaseNotes_Click(object sender, RoutedEventArgs e)
        => OpenPath(_vm.ReleaseNotesPath, openParentIfMissing: true);

    private void OpenAutomationCenter_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow shell)
            shell.NavigateToPage(Domain.Enums.Page.Bundles);
    }

    private void RestoreDefaults_Click(object sender, RoutedEventArgs e)
    {
        var confirmation = MessageBox.Show(
            $"Restore {_vm.ProductDisplayName} to its default settings for this device?\n\nThis keeps your repair history, logs, and support packages, but resets behavior, profile, and startup preferences.",
            $"{_vm.ProductDisplayName} - Restore Defaults",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirmation != System.Windows.MessageBoxResult.Yes)
            return;

        _vm.RestoreDefaultSettings();
    }

    private static void OpenPath(string path, bool openParentIfMissing = false)
    {
        var targetPath = path;

        if (!Directory.Exists(path) && !File.Exists(path))
        {
            if (!openParentIfMissing)
            {
                MessageBox.Show("That item does not exist yet.", "Not Available",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            targetPath = Path.GetDirectoryName(path) ?? path;
        }

        if (!File.Exists(targetPath))
            Directory.CreateDirectory(targetPath);

        TryOpenTarget(targetPath, "Open Item");
    }

    private static void TryOpenTarget(string target, string title)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = target,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"The app could not open that item.\n\n{ex.Message}", title,
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void CopyDataFolder_Click(object sender, RoutedEventArgs e)
        => CopyText(_vm.DataFolderPath);

    private void CopyAppLog_Click(object sender, RoutedEventArgs e)
        => CopyText(_vm.AppLogPath);

    private void CopyVerifyLog_Click(object sender, RoutedEventArgs e)
        => CopyText(_vm.VerifyLogPath);

    private static void CopyText(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            System.Windows.Clipboard.SetText(value);
    }
}
