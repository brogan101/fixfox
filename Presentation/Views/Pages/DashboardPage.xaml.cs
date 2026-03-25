using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HelpDesk.Domain.Models;
using HelpDesk.Presentation.ViewModels;
using HelpDesk.Presentation.Views;
using NavPage = HelpDesk.Domain.Enums.Page;
using DashboardActionKind = HelpDesk.Domain.Enums.DashboardActionKind;
using Button = System.Windows.Controls.Button;
using Clipboard = System.Windows.Clipboard;

namespace HelpDesk.Presentation.Views.Pages;

public partial class DashboardPage : Page
{
    private readonly MainViewModel _vm;

    public DashboardPage(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = _vm;
    }

    private async void RunScan_Click(object sender, RoutedEventArgs e)
        => await _vm.RunQuickScanAsync();

    private async void RunMaintenance_Click(object sender, RoutedEventArgs e)
        => await _vm.RunRecommendedMaintenanceAsync();

    private async void ScanFix_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ScanResult r } && r.FixId is not null)
            await _vm.RunFixByIdAsync(r.FixId);
    }

    private async void WizardNext_Click(object sender, RoutedEventArgs e)
        => await _vm.WizardNextAsync();

    private void WizardCancel_Click(object sender, RoutedEventArgs e)
        => _vm.WizardCancel();

    private void OpenDeviceHealth_Click(object sender, RoutedEventArgs e)
        => NavigateTo(NavPage.SystemInfo);

    private void OpenToolbox_Click(object sender, RoutedEventArgs e)
        => NavigateTo(NavPage.Toolbox);

    private void OpenSupport_Click(object sender, RoutedEventArgs e)
        => NavigateTo(NavPage.Handoff);

    private void OpenAutomation_Click(object sender, RoutedEventArgs e)
        => NavigateTo(NavPage.Bundles);

    private async void RunAutomationQuickHealth_Click(object sender, RoutedEventArgs e)
    {
        var rule = _vm.AutomationRules.FirstOrDefault(item => item.Id == "quick-health-check");
        if (rule is not null)
            await _vm.RunAutomationRuleAsync(rule);
    }

    private async void RunAutomationMaintenance_Click(object sender, RoutedEventArgs e)
    {
        var rule = _vm.AutomationRules.FirstOrDefault(item => item.Id == "safe-maintenance");
        if (rule is not null)
            await _vm.RunAutomationRuleAsync(rule);
    }

    private async void AlertAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: DashboardAlert alert })
            return;

        switch (alert.ActionKind)
        {
            case DashboardActionKind.Fix when !string.IsNullOrWhiteSpace(alert.ActionTargetId):
                await _vm.RunFixByIdAsync(alert.ActionTargetId);
                break;
            case DashboardActionKind.Runbook when !string.IsNullOrWhiteSpace(alert.ActionTargetId):
                await _vm.RunRunbookByIdAsync(alert.ActionTargetId);
                break;
            case DashboardActionKind.Page when alert.ActionPage.HasValue:
                NavigateTo(alert.ActionPage.Value);
                break;
        }
    }

    private async void SuggestedRunbook_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: RunbookDefinition runbook })
            await _vm.RunRunbookByIdAsync(runbook.Id);
    }

    private async void SuggestedRunbookCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2 || sender is not Border { Tag: RunbookDefinition runbook })
            return;

        await _vm.RunRunbookByIdAsync(runbook.Id);
    }

    private async void SuggestedRunbookMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { CommandParameter: RunbookDefinition runbook })
            await _vm.RunRunbookByIdAsync(runbook.Id);
    }

    private async void RecommendationAction_Click(object sender, RoutedEventArgs e)
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

    private void AlertSnooze_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: DashboardAlert alert })
            _vm.SnoozeDashboardAlert(alert);
    }

    private async void RecentFixCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2 || sender is not Border { Tag: FixItem fix })
            return;

        await _vm.RunFixAsync(fix);
    }

    private async void RecentFixRunMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { CommandParameter: FixItem fix })
            await _vm.RunFixAsync(fix);
    }

    private void RecentFailureCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
            NavigateTo(NavPage.Handoff);
    }

    private async void RecentFailureSupportMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem)
            return;

        await _vm.CreateEvidenceBundleAsync();
        NavigateTo(NavPage.Handoff);
    }

    private void OpenAutomationMenu_Click(object sender, RoutedEventArgs e)
        => NavigateTo(NavPage.Bundles);

    private void RecentAutomationCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2)
            return;

        NavigateTo(NavPage.Bundles);
    }

    private void RecentAutomationDetailsMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { CommandParameter: AutomationRunReceipt receipt })
        {
            System.Windows.MessageBox.Show(
                _vm.BuildAutomationReceiptDetailText(receipt),
                $"{_vm.ProductDisplayName} - Automation Result",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }
    }

    private void RecentAutomationCopyMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { CommandParameter: AutomationRunReceipt receipt })
            Clipboard.SetText(_vm.BuildAutomationReceiptDetailText(receipt));
    }

    private void OpenActivityMenu_Click(object sender, RoutedEventArgs e)
        => NavigateTo(NavPage.History);

    private void OpenLibraryMenu_Click(object sender, RoutedEventArgs e)
        => NavigateTo(NavPage.Fixes);

    private void NavigateTo(NavPage page)
    {
        if (Window.GetWindow(this) is MainWindow shell)
            shell.NavigateToPage(page);
    }
}
