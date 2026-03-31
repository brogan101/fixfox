using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using HelpDesk.Domain.Models;
using HelpDesk.Presentation.ViewModels;
using HelpDesk.Presentation.Views;
using NavPage = HelpDesk.Domain.Enums.Page;
using DashboardActionKind = HelpDesk.Domain.Enums.DashboardActionKind;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using Clipboard = System.Windows.Clipboard;

namespace HelpDesk.Presentation.Views.Pages;

public partial class DashboardPage : Page
{
    private readonly MainViewModel _vm;
    private const double ExpandedStatusBarHeight = 76;

    public DashboardPage(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = _vm;
        Loaded += DashboardPage_Loaded;
        Unloaded += DashboardPage_Unloaded;
    }

    private void DashboardPage_Loaded(object sender, RoutedEventArgs e)
    {
        _vm.PropertyChanged += ViewModel_PropertyChanged;
        AnimateStatusBar(expand: !_vm.DashboardStatusBarCollapsed, animate: false);
        _ = _vm.RefreshDashboardSuggestionsAsync();
    }

    private void DashboardPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _vm.PropertyChanged -= ViewModel_PropertyChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.DashboardStatusBarCollapsed))
            Dispatcher.BeginInvoke(() => AnimateStatusBar(expand: !_vm.DashboardStatusBarCollapsed, animate: true));
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

    private void OpenAutomationAttention_Click(object sender, RoutedEventArgs e)
    {
        _vm.RequestAutomationAttentionView();
        NavigateTo(NavPage.Bundles);
    }

    private async void DashboardSuggestionRun_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: DashboardSuggestion suggestion })
            await _vm.RunDashboardSuggestionAsync(suggestion);
    }

    private void DashboardSuggestionDismiss_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: DashboardSuggestion suggestion })
            _vm.DismissDashboardSuggestion(suggestion);
    }

    private async void HealthAlertAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: HealthAlert alert })
            await _vm.ExecuteHealthAlertActionAsync(alert);
    }

    private async void HealthAlertDismiss_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: HealthAlert alert } button)
            return;

        if (FindAncestor<Border>(button) is not { } hostBorder)
        {
            _vm.DismissHealthAlert(alert);
            return;
        }

        var animation = new DoubleAnimation(0, TimeSpan.FromMilliseconds(200));
        hostBorder.BeginAnimation(OpacityProperty, animation);
        await Task.Delay(200);
        _vm.DismissHealthAlert(alert);
    }

    private async void RecentQuickActionRun_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: RecentQuickActionEntry entry })
            return;

        if (entry.RiskLevel is Domain.Enums.FixRiskLevel.MayRestart or Domain.Enums.FixRiskLevel.Advanced)
        {
            var result = System.Windows.MessageBox.Show(
                $"Run '{entry.DisplayTitle}' again? This action may restart Windows or make deeper system changes.",
                $"{_vm.ProductDisplayName} - Run Again",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.OK)
                return;
        }

        await _vm.RunRecentQuickActionAsync(entry);
    }

    private void RecentQuickActionOpen_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: RecentQuickActionEntry entry })
            return;

        if (!entry.IsRunbook)
            _vm.OpenFixInLibrary(entry.FixId);
        else
            NavigateTo(NavPage.Bundles);
    }

    private void DismissOnboarding_Click(object sender, RoutedEventArgs e)
        => _vm.DismissOnboardingChecklist();

    private void OnboardingItem_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox { Tag: string key } checkBox)
            return;

        _vm.SetOnboardingChecklistItemState(key, checkBox.IsChecked == true);
    }

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
        var shell = Window.GetWindow(this) as MainWindow
            ?? System.Windows.Application.Current?.MainWindow as MainWindow;
        shell?.NavigateToPage(page);
    }

    private static T? FindAncestor<T>(DependencyObject? source) where T : DependencyObject
    {
        while (source is not null)
        {
            if (source is T typed)
                return typed;

            source = System.Windows.Media.VisualTreeHelper.GetParent(source);
        }

        return null;
    }

    private void AnimateStatusBar(bool expand, bool animate)
    {
        StatusBarHost.Visibility = Visibility.Visible;
        var targetHeight = expand ? ExpandedStatusBarHeight : 0d;
        var targetOpacity = expand ? 1d : 0d;

        if (!animate)
        {
            StatusBarHost.Height = targetHeight;
            StatusBarHost.Opacity = targetOpacity;
            StatusBarHost.Visibility = expand ? Visibility.Visible : Visibility.Collapsed;
            return;
        }

        var duration = TimeSpan.FromMilliseconds(150);
        var easing = new QuadraticEase { EasingMode = EasingMode.EaseInOut };
        var heightAnimation = new DoubleAnimation(targetHeight, duration) { EasingFunction = easing };
        var opacityAnimation = new DoubleAnimation(targetOpacity, duration) { EasingFunction = easing };

        if (!expand)
        {
            heightAnimation.Completed += (_, _) =>
            {
                StatusBarHost.Visibility = Visibility.Collapsed;
                StatusBarHost.Height = 0;
            };
        }

        StatusBarHost.BeginAnimation(HeightProperty, heightAnimation);
        StatusBarHost.BeginAnimation(OpacityProperty, opacityAnimation);
    }
}
