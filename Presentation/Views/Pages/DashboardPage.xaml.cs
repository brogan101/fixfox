using System.Windows;
using System.Windows.Controls;
using HelpDesk.Domain.Models;
using HelpDesk.Presentation.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Button = System.Windows.Controls.Button;

namespace HelpDesk.Presentation.Views.Pages;

public partial class DashboardPage : Page
{
    private readonly MainViewModel _vm;

    public DashboardPage()
    {
        InitializeComponent();
        _vm         = App.Services.GetRequiredService<MainViewModel>();
        DataContext = _vm;
    }

    private async void RunScan_Click(object sender, RoutedEventArgs e)
        => await _vm.RunQuickScanAsync();

    private async void ScanFix_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ScanResult r } && r.FixId is not null)
            await _vm.RunFixByIdAsync(r.FixId);
    }

    private async void WizardNext_Click(object sender, RoutedEventArgs e)
        => await _vm.WizardNextAsync();

    private void WizardCancel_Click(object sender, RoutedEventArgs e)
        => _vm.WizardCancel();
}
