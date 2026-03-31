using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using System.IO;
using System.Windows.Automation;
using System.Windows.Input;
using HelpDesk.Domain.Models;
using HelpDesk.Infrastructure.Services;
using HelpDesk.Presentation.ViewModels;
using HelpDesk.Presentation.Views.Dialogs;
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
        StartupAppsList.Loaded += (_, _) => ApplyStartupAutomationIds();
        StartupAppsList.ItemContainerGenerator.StatusChanged += (_, _) => ApplyStartupAutomationIds();
        InstalledProgramsList.Loaded += (_, _) => ApplyInstalledProgramAutomationIds();
        InstalledProgramsList.ItemContainerGenerator.StatusChanged += (_, _) => ApplyInstalledProgramAutomationIds();
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

        if (_vm.BrowserExtensionSections.Count == 0)
            await _vm.LoadBrowserExtensionSectionsAsync();

        if (_vm.WorkFromHomeChecks.Count == 0)
            await _vm.LoadWorkFromHomeChecksAsync();

        ApplyStartupAutomationIds();
        ApplyInstalledProgramAutomationIds();
    }

    private async void RefreshSysInfo_Click(object sender, RoutedEventArgs e)
        => await _vm.LoadSystemInfoAsync();

    private async void RefreshPrograms_Click(object sender, RoutedEventArgs e)
    {
        await _vm.LoadInstalledProgramsAsync();
        ApplyInstalledProgramAutomationIds();
    }

    private async void RefreshStartupApps_Click(object sender, RoutedEventArgs e)
    {
        await _vm.LoadStartupAppsAsync();
        ApplyStartupAutomationIds();
    }

    private async void RefreshStorageInsights_Click(object sender, RoutedEventArgs e)
        => await _vm.LoadStorageInsightsAsync();

    private async void RefreshBrowserReview_Click(object sender, RoutedEventArgs e)
        => await _vm.LoadBrowserExtensionSectionsAsync();

    private async void RefreshWorkFromHomeChecks_Click(object sender, RoutedEventArgs e)
        => await _vm.LoadWorkFromHomeChecksAsync();

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

    private async void ProgramCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border { Tag: InstalledProgram program })
            await _vm.OpenInstalledProgramDetailAsync(program);
    }

    private async void InstalledProgramsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (InstalledProgramsList.SelectedItem is InstalledProgram program)
            await _vm.OpenInstalledProgramDetailAsync(program);
    }

    private void CloseProgramDetailPane_Click(object sender, RoutedEventArgs e)
    {
        _vm.CloseInstalledProgramDetail();
        InstalledProgramsList.SelectedItem = null;
    }

    private void CloseProgramDetailOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _vm.CloseInstalledProgramDetail();
        InstalledProgramsList.SelectedItem = null;
    }

    private async void RepairProgram_Click(object sender, RoutedEventArgs e)
    {
        var program = sender switch
        {
            Button { Tag: InstalledProgram buttonProgram } => buttonProgram,
            _ => _vm.SelectedInstalledProgram
        };

        if (program is null)
            return;

        var confirmationText = program.IsMsiApp
            ? $"Repair {program.Name}? This will launch the Windows Installer repair flow for this app."
            : $"Repair {program.Name}? This will re-register the Store app package without removing its saved data.";
        if (MessageBox.Show(confirmationText, $"{_vm.ProductDisplayName} - Repair App", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
            return;

        try
        {
            await _vm.RepairInstalledProgramAsync(program);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"{_vm.ProductDisplayName} could not repair {program.Name}.\n\n{ex.Message}", $"{_vm.ProductDisplayName} - Repair Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void ResetProgram_Click(object sender, RoutedEventArgs e)
    {
        var program = sender switch
        {
            Button { Tag: InstalledProgram buttonProgram } => buttonProgram,
            _ => _vm.SelectedInstalledProgram
        };

        if (program is null)
            return;

        var message = $"Reset {program.Name}? This will delete the app's saved data and preferences. The app itself will not be uninstalled.";
        if (MessageBox.Show(message, $"{_vm.ProductDisplayName} - Reset App Data", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK)
            return;

        try
        {
            await _vm.ResetInstalledProgramAsync(program);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"{_vm.ProductDisplayName} could not reset {program.Name}.\n\n{ex.Message}", $"{_vm.ProductDisplayName} - Reset Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OpenDefaultApps_Click(object sender, RoutedEventArgs e)
        => TryStart(new ProcessStartInfo("ms-settings:defaultapps") { UseShellExecute = true }, "open Default Apps");

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

        var confirm = MessageBox.Show(
            $"Uninstall {program.Name}? This cannot be undone.",
            $"{_vm.ProductDisplayName} - Uninstall App",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.OK) return;

        if (program.IsStoreApp)
        {
            TryStart(new ProcessStartInfo("ms-settings:appsfeatures") { UseShellExecute = true }, $"open Installed Apps for {program.Name}");
            return;
        }

        if (string.IsNullOrWhiteSpace(program.UninstallCommand) &&
            string.IsNullOrWhiteSpace(program.QuietUninstallCommand))
            return;

        var command = string.IsNullOrWhiteSpace(program.QuietUninstallCommand)
            ? program.UninstallCommand
            : program.QuietUninstallCommand;

        TryStart(new ProcessStartInfo("cmd.exe", $"/c {command}") { UseShellExecute = false, CreateNoWindow = false }, $"launch the uninstaller for {program.Name}");
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

        if (program is null || string.IsNullOrWhiteSpace(program.InstallLocation))
            return;

        var selectPath = program.HasInstallLocation
            ? program.InstallLocation
            : program.DisplayIconPath;
        if (string.IsNullOrWhiteSpace(selectPath))
            return;

        TryStart(new ProcessStartInfo("explorer.exe", $"/select,\"{selectPath}\"") { UseShellExecute = true }, $"open the install location for {program.Name}");
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

    private async void DisableStartupItem_Click(object sender, RoutedEventArgs e)
    {
        var entry = sender switch
        {
            Button { Tag: StartupAppEntry buttonEntry } => buttonEntry,
            MenuItem { CommandParameter: StartupAppEntry menuEntry } => menuEntry,
            _ => null
        };

        if (entry is null)
            return;

        if (entry.RequiresDisableConfirmation)
        {
            var warning = $"{entry.WhatItDoes}\n\n{entry.WhatMayBreakIfDisabled}\n\nYou can re-enable it later from Startup Apps.";
            if (MessageBox.Show($"Disable {entry.Name} at startup?\n\n{warning}", $"{_vm.ProductDisplayName} - Disable Startup Item", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK)
                return;
        }

        await _vm.DisableStartupAppAsync(entry);
    }

    private void OpenTaskManager_Click(object sender, RoutedEventArgs e)
        => TryStart(new ProcessStartInfo("taskmgr.exe") { UseShellExecute = true }, "open Task Manager");

    private void OpenBrowserExtensionManager_Click(object sender, RoutedEventArgs e)
    {
        var extension = sender is Button { Tag: BrowserExtensionEntry browserExtension } ? browserExtension : null;

        if (extension is null || string.IsNullOrWhiteSpace(extension.DisableUri))
            return;

        TryStart(new ProcessStartInfo(extension.DisableUri) { UseShellExecute = true }, $"open the extension manager for {extension.BrowserName}");
    }

    private void AddBrowserAllowlistSite_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new TextPromptWindow("Add Allowed Site", "Enter a domain to keep signed in when browser cleanup removes session data.", "Add site")
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true)
            _vm.AddBrowserAllowlistedSite(dialog.ResponseText);
    }

    private void RemoveBrowserAllowlistSite_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string domain })
            _vm.RemoveBrowserAllowlistedSite(domain);
    }

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

    private void DeviceHealthSectionToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Primitives.ToggleButton { Tag: string sectionKey })
            _vm.ToggleDeviceHealthSection(sectionKey);
    }

    private void RecoveryAnswer_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string answerKey })
            _vm.RecoveryDecisionTree.SelectAnswer(answerKey);
    }

    private void RecoveryBack_Click(object sender, RoutedEventArgs e)
        => _vm.RecoveryDecisionTree.GoBack();

    private async void RecoveryRecommendation_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: RecoveryRecommendationCard recommendation })
            return;

        switch (recommendation.ActionTarget)
        {
            case "run-sfc":
                await _vm.RunFixByIdAsync("run-sfc");
                break;

            case "run-dism":
                await _vm.RunFixByIdAsync("run-dism");
                break;

            case "ms-settings:startupapps":
            case "ms-settings:appsfeatures":
            case "ms-settings:recovery":
                TryStart(new ProcessStartInfo(recommendation.ActionTarget) { UseShellExecute = true }, $"open {recommendation.Title}");
                break;

            case "perfmon /rel":
                TryStart(new ProcessStartInfo("perfmon.exe", "/rel") { UseShellExecute = true }, "open Reliability Monitor");
                break;

            case "shutdown /r /o /f /t 0":
                var result = MessageBox.Show(
                    "This will restart your PC into recovery mode. Save your work first.",
                    $"{_vm.ProductDisplayName} - Restart Into Recovery",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.OK)
                    TryStart(new ProcessStartInfo("shutdown.exe", "/r /o /f /t 0") { UseShellExecute = true }, "restart into recovery mode");
                break;
        }
    }

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

    private void ApplyStartupAutomationIds()
    {
        ApplyListBoxContainerAutomationIds(StartupAppsList, "Startup_Item_", "_Container");
    }

    private void ApplyInstalledProgramAutomationIds()
    {
        ApplyListBoxContainerAutomationIds(InstalledProgramsList, "AppCenter_Item_", "_Card");
    }

    private static void ApplyListBoxContainerAutomationIds(System.Windows.Controls.ListBox listBox, string prefix, string suffix)
    {
        if (listBox.ItemContainerGenerator.Status != System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
            return;

        for (var index = 0; index < listBox.Items.Count; index++)
        {
            if (listBox.ItemContainerGenerator.ContainerFromIndex(index) is not System.Windows.Controls.ListBoxItem item)
                continue;

            AutomationProperties.SetAutomationId(item, $"{prefix}{index + 1}{suffix}");
            item.Focusable = true;
        }
    }
}
