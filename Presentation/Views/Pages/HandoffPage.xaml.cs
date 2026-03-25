using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using HelpDesk.Domain.Models;
using HelpDesk.Presentation.ViewModels;
using Button = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;

namespace HelpDesk.Presentation.Views.Pages;

public partial class HandoffPage : Page
{
    private readonly MainViewModel _vm;

    public HandoffPage(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
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
            MessageBox.Show(ex.Message, $"{_vm.ProductDisplayName} - Support Package", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void ClearInterrupted_Click(object sender, RoutedEventArgs e)
        => _vm.ClearInterruptedOperation();

    private void OpenPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, $"{_vm.ProductDisplayName} - Open Item", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OpenBundleFolder_Click(object sender, RoutedEventArgs e)
        => OpenPath(_vm.LastEvidenceBundle?.BundleFolder);

    private void OpenBundleSummary_Click(object sender, RoutedEventArgs e)
        => OpenPath(_vm.LastEvidenceBundle?.SummaryPath);

    private void CopyBundleFolder_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_vm.LastEvidenceBundle?.BundleFolder))
            System.Windows.Clipboard.SetText(_vm.LastEvidenceBundle.BundleFolder);
    }

    private void CopyPreviewSummary_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_vm.LastEvidenceBundlePreviewText))
            System.Windows.Clipboard.SetText(_vm.LastEvidenceBundlePreviewText);
    }

    private void OpenKb_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: KnowledgeBaseEntry kb })
            OpenPath(kb.Url);
    }

    private void OpenSupportPortal_Click(object sender, RoutedEventArgs e)
        => OpenPath(_vm.Branding.SupportPortalUrl);

    private void EmailSupport_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_vm.SupportEmailAddress))
            return;

        OpenPath($"mailto:{_vm.SupportEmailAddress}");
    }

    private async void RecommendationRun_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ProactiveRecommendation recommendation })
            return;

        if (!string.IsNullOrWhiteSpace(recommendation.ActionFixId))
            await _vm.RunFixByIdAsync(recommendation.ActionFixId);
        else if (!string.IsNullOrWhiteSpace(recommendation.ActionRunbookId))
            await _vm.RunRunbookByIdAsync(recommendation.ActionRunbookId);
    }

    private void RecommendationIgnore_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ProactiveRecommendation recommendation })
            _vm.IgnoreRecommendation(recommendation);
    }
}
