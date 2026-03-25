using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using HelpDesk.Domain.Models;
using HelpDesk.Infrastructure.Services;
using HelpDesk.Presentation.ViewModels;
using Button = System.Windows.Controls.Button;
using Clipboard = System.Windows.Clipboard;
using MessageBox = System.Windows.MessageBox;

namespace HelpDesk.Presentation.Views.Pages;

public partial class SystemInfoPage : Page
{
    private readonly MainViewModel _vm;

    public SystemInfoPage(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = _vm;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_vm.Snapshot is null && !_vm.SnapshotLoading)
            await _vm.LoadSystemInfoAsync();

        if (_vm.InstalledPrograms.Count == 0 && !_vm.InstalledProgramsLoading)
            await _vm.LoadInstalledProgramsAsync();

        if (_vm.StartupApps.Count == 0 && !_vm.StartupAppsLoading)
            await _vm.LoadStartupAppsAsync();

        if (_vm.StorageInsights.Count == 0 && !_vm.StorageInsightsLoading)
            await _vm.LoadStorageInsightsAsync();
    }

    private async void RefreshSysInfo_Click(object sender, RoutedEventArgs e)
        => await _vm.LoadSystemInfoAsync();

    private async void RefreshPrograms_Click(object sender, RoutedEventArgs e)
        => await _vm.LoadInstalledProgramsAsync();

    private async void RefreshStartupApps_Click(object sender, RoutedEventArgs e)
        => await _vm.LoadStartupAppsAsync();

    private async void RefreshStorageInsights_Click(object sender, RoutedEventArgs e)
        => await _vm.LoadStorageInsightsAsync();

    private async void SupportAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: SupportAction action })
            return;

        try
        {
            await _vm.RunSupportActionAsync(action);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"{_vm.ProductDisplayName} could not complete that action.\n\n{ex.Message}", $"{_vm.ProductDisplayName} - Action Failed",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void SupportCenterCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2 || sender is not Border { Tag: SupportCenterDefinition center })
            return;

        await RunSupportCenterAsync(center.PrimaryAction);
    }

    private async void SupportCenterPrimaryMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { CommandParameter: SupportCenterDefinition center })
            await RunSupportCenterAsync(center.PrimaryAction);
    }

    private async void SupportCenterSecondaryMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { CommandParameter: SupportCenterDefinition center })
            await RunSupportCenterAsync(center.SecondaryAction);
    }

    private void SupportCenterDetails_Click(object sender, RoutedEventArgs e)
    {
        var center = sender switch
        {
            Button { Tag: SupportCenterDefinition buttonCenter } => buttonCenter,
            MenuItem { CommandParameter: SupportCenterDefinition menuCenter } => menuCenter,
            _ => null
        };

        if (center is not null)
            MessageBox.Show(_vm.BuildSupportCenterDetailText(center), $"{_vm.ProductDisplayName} - Support Center Details", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void SupportCenterCopy_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { CommandParameter: SupportCenterDefinition center })
            Clipboard.SetText(_vm.BuildSupportCenterDetailText(center));
    }

    private void SupportCenterCopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: SupportCenterDefinition center })
            Clipboard.SetText(_vm.BuildSupportCenterDetailText(center));
    }

    private void OpenAppsSettings_Click(object sender, RoutedEventArgs e)
        => TryStart(new ProcessStartInfo("ms-settings:appsfeatures") { UseShellExecute = true }, "open Apps settings");

    private void RunUninstall_Click(object sender, RoutedEventArgs e)
    {
        var program = sender switch
        {
            Button { Tag: InstalledProgram buttonProgram } => buttonProgram,
            MenuItem { CommandParameter: InstalledProgram menuProgram } => menuProgram,
            _ => null
        };

        if (program is null)
            return;

        if (string.IsNullOrWhiteSpace(program.UninstallCommand) &&
            string.IsNullOrWhiteSpace(program.QuietUninstallCommand)) return;

        var command = string.IsNullOrWhiteSpace(program.QuietUninstallCommand)
            ? program.UninstallCommand
            : program.QuietUninstallCommand;

        var confirm = MessageBox.Show(
            $"Launch the uninstaller for \"{program.Name}\"?",
            $"{_vm.ProductDisplayName} - Uninstall App",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        TryStart(new ProcessStartInfo("cmd.exe", $"/c {command}")
        {
            UseShellExecute = false,
            CreateNoWindow = false
        }, $"launch the uninstaller for {program.Name}");
    }

    private void CopyProgramDetails_Click(object sender, RoutedEventArgs e)
    {
        var program = sender switch
        {
            Button { Tag: InstalledProgram buttonProgram } => buttonProgram,
            MenuItem { CommandParameter: InstalledProgram menuProgram } => menuProgram,
            _ => null
        };

        if (program is not null)
            Clipboard.SetText(_vm.BuildInstalledProgramDetailText(program));
    }

    private void ProgramDetails_Click(object sender, RoutedEventArgs e)
    {
        var program = sender switch
        {
            Button { Tag: InstalledProgram buttonProgram } => buttonProgram,
            MenuItem { CommandParameter: InstalledProgram menuProgram } => menuProgram,
            _ => null
        };

        if (program is not null)
            MessageBox.Show(_vm.BuildInstalledProgramDetailText(program), $"{_vm.ProductDisplayName} - App Details", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void CopyProgramDetailsButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: InstalledProgram program })
            Clipboard.SetText(_vm.BuildInstalledProgramDetailText(program));
    }

    private void OpenProgramLocation_Click(object sender, RoutedEventArgs e)
    {
        var program = sender switch
        {
            Button { Tag: InstalledProgram buttonProgram } => buttonProgram,
            MenuItem { CommandParameter: InstalledProgram menuProgram } => menuProgram,
            _ => null
        };

        if (program is null || !program.HasInstallLocation)
            return;

        TryStart(new ProcessStartInfo("explorer.exe", $"\"{program.InstallLocation}\"") { UseShellExecute = true }, $"open the install location for {program.Name}");
    }

    private void StartupDetails_Click(object sender, RoutedEventArgs e)
    {
        var entry = sender switch
        {
            Button { Tag: StartupAppEntry buttonEntry } => buttonEntry,
            MenuItem { CommandParameter: StartupAppEntry menuEntry } => menuEntry,
            _ => null
        };

        if (entry is not null)
            MessageBox.Show(_vm.BuildStartupAppDetailText(entry), $"{_vm.ProductDisplayName} - Startup Item Details", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void StartupCopy_Click(object sender, RoutedEventArgs e)
    {
        var entry = sender switch
        {
            Button { Tag: StartupAppEntry buttonEntry } => buttonEntry,
            MenuItem { CommandParameter: StartupAppEntry menuEntry } => menuEntry,
            _ => null
        };

        if (entry is not null)
            Clipboard.SetText(_vm.BuildStartupAppDetailText(entry));
    }

    private void StartupCopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: StartupAppEntry entry })
            Clipboard.SetText(_vm.BuildStartupAppDetailText(entry));
    }

    private void OpenStartupTarget_Click(object sender, RoutedEventArgs e)
    {
        var entry = sender switch
        {
            Button { Tag: StartupAppEntry buttonEntry } => buttonEntry,
            MenuItem { CommandParameter: StartupAppEntry menuEntry } => menuEntry,
            _ => null
        };

        if (entry is not null && entry.HasLaunchTarget && File.Exists(entry.LaunchTarget))
            TryStart(new ProcessStartInfo("explorer.exe", $"/select,\"{entry.LaunchTarget}\"") { UseShellExecute = true }, $"open the startup target for {entry.Name}");
    }

    private void OpenStartupAppsSettings_Click(object sender, RoutedEventArgs e)
        => TryStart(new ProcessStartInfo("ms-settings:startupapps") { UseShellExecute = true }, "open Startup Apps");

    private void OpenTaskManager_Click(object sender, RoutedEventArgs e)
        => TryStart(new ProcessStartInfo("taskmgr.exe") { UseShellExecute = true }, "open Task Manager");

    private void StorageDetails_Click(object sender, RoutedEventArgs e)
    {
        var insight = sender switch
        {
            Button { Tag: StorageInsight buttonInsight } => buttonInsight,
            MenuItem { CommandParameter: StorageInsight menuInsight } => menuInsight,
            _ => null
        };

        if (insight is not null)
            MessageBox.Show(_vm.BuildStorageInsightDetailText(insight), $"{_vm.ProductDisplayName} - Storage Review", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void StorageCopy_Click(object sender, RoutedEventArgs e)
    {
        var insight = sender switch
        {
            Button { Tag: StorageInsight buttonInsight } => buttonInsight,
            MenuItem { CommandParameter: StorageInsight menuInsight } => menuInsight,
            _ => null
        };

        if (insight is not null)
            Clipboard.SetText(_vm.BuildStorageInsightDetailText(insight));
    }

    private void StorageCopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: StorageInsight insight })
            Clipboard.SetText(_vm.BuildStorageInsightDetailText(insight));
    }

    private void OpenStorageItemFolder_Click(object sender, RoutedEventArgs e)
    {
        var insight = sender switch
        {
            Button { Tag: StorageInsight buttonInsight } => buttonInsight,
            MenuItem { CommandParameter: StorageInsight menuInsight } => menuInsight,
            _ => null
        };

        if (insight is not null && File.Exists(insight.FullPath))
            TryStart(new ProcessStartInfo("explorer.exe", $"/select,\"{insight.FullPath}\"") { UseShellExecute = true }, $"open the folder for {insight.DisplayName}");
    }

    private void OpenStorageSettings_Click(object sender, RoutedEventArgs e)
        => TryStart(new ProcessStartInfo("ms-settings:storagesense") { UseShellExecute = true }, "open Storage settings");

    private async Task RunSupportCenterAsync(SupportAction action)
    {
        try
        {
            await _vm.RunSupportActionAsync(action);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"{_vm.ProductDisplayName} could not complete that action.\n\n{ex.Message}", $"{_vm.ProductDisplayName} - Action Failed",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void TryStart(ProcessStartInfo info, string action)
    {
        try
        {
            Process.Start(info);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"{_vm.ProductDisplayName} could not {action}.\n\n{ex.Message}", $"{_vm.ProductDisplayName} - Action Failed",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
