using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using HelpDesk.Infrastructure.Services;
using HelpDesk.Presentation.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Button = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;

namespace HelpDesk.Presentation.Views.Pages;

public partial class SystemInfoPage : Page
{
    private readonly MainViewModel _vm;

    public SystemInfoPage()
    {
        InitializeComponent();
        _vm         = App.Services.GetRequiredService<MainViewModel>();
        DataContext = _vm;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_vm.InstalledPrograms.Count == 0 && !_vm.InstalledProgramsLoading)
            await _vm.LoadInstalledProgramsAsync();
    }

    private async void RefreshSysInfo_Click(object sender, RoutedEventArgs e)
        => await _vm.LoadSystemInfoAsync();

    private async void RefreshPrograms_Click(object sender, RoutedEventArgs e)
        => await _vm.LoadInstalledProgramsAsync();

    private void OpenAppsSettings_Click(object sender, RoutedEventArgs e)
        => Process.Start(new ProcessStartInfo("ms-settings:appsfeatures")
        {
            UseShellExecute = true
        });

    private void RunUninstall_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: InstalledProgram program }) return;
        if (string.IsNullOrWhiteSpace(program.UninstallCommand) &&
            string.IsNullOrWhiteSpace(program.QuietUninstallCommand)) return;

        var command = string.IsNullOrWhiteSpace(program.QuietUninstallCommand)
            ? program.UninstallCommand
            : program.QuietUninstallCommand;

        var confirm = MessageBox.Show(
            $"Launch the uninstaller for \"{program.Name}\"?",
            "FixFox \u2014 Uninstall Program",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        Process.Start(new ProcessStartInfo("cmd.exe", $"/c {command}")
        {
            UseShellExecute = false,
            CreateNoWindow = false
        });
    }
}
