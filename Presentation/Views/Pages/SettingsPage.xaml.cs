using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using HelpDesk.Shared;
using Microsoft.Win32;
using Wpf.Ui.Controls;
using HelpDesk.Presentation.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;

namespace HelpDesk.Presentation.Views.Pages;

public partial class SettingsPage : Page
{
    private readonly MainViewModel _vm;

    public SettingsPage()
    {
        InitializeComponent();
        _vm         = App.Services.GetRequiredService<MainViewModel>();
        DataContext = _vm;
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
        => _vm.SaveSettings();

    private void ThemeToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleSwitch ts) return;
        var theme = ts.IsChecked == true ? "Dark" : "Light";
        _vm.ApplyTheme(theme);
    }

    private void RunAtStartup_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleSwitch ts) return;
        var enable = ts.IsChecked == true;
        SetRunAtStartup(enable);
        _vm.Settings.RunAtStartup = enable;
    }

    private static void SetRunAtStartup(bool enable)
    {
        const string keyPath   = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        const string valueName = "FixFox";
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

    private static void OpenPath(string path, bool openParentIfMissing = false)
    {
        var targetPath = path;

        if (!Directory.Exists(path) && !File.Exists(path))
        {
            if (!openParentIfMissing)
            {
                MessageBox.Show("That item does not exist yet.", "FixFox - Not Available",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            targetPath = Path.GetDirectoryName(path) ?? path;
        }

        if (!File.Exists(targetPath))
            Directory.CreateDirectory(targetPath);

        Process.Start(new ProcessStartInfo
        {
            FileName = targetPath,
            UseShellExecute = true
        });
    }
}
