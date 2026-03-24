using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using HelpDesk.Domain.Models;
using HelpDesk.Presentation.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Button = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;

namespace HelpDesk.Presentation.Views.Pages;

public partial class HandoffPage : Page
{
    private readonly MainViewModel _vm;

    public HandoffPage()
    {
        InitializeComponent();
        _vm = App.Services.GetRequiredService<MainViewModel>();
        DataContext = _vm;
    }

    private async void RunHealthCheck_Click(object sender, RoutedEventArgs e)
        => await _vm.RunFullHealthCheckAsync();

    private async void CreateBundle_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _vm.CreateEvidenceBundleAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "FixFox — Support Bundle", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void ClearInterrupted_Click(object sender, RoutedEventArgs e)
        => _vm.ClearInterruptedOperation();

    private static void OpenPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private void OpenBundleFolder_Click(object sender, RoutedEventArgs e)
        => OpenPath(_vm.LastEvidenceBundle?.BundleFolder);

    private void OpenBundleSummary_Click(object sender, RoutedEventArgs e)
        => OpenPath(_vm.LastEvidenceBundle?.SummaryPath);

    private void OpenKb_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: KnowledgeBaseEntry kb })
            OpenPath(kb.Url);
    }
}
