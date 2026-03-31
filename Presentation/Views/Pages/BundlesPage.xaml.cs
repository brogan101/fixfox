using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media;
using System.Diagnostics;
using HelpDesk.Application.Interfaces;
using HelpDesk.Domain.Models;
using HelpDesk.Presentation.ViewModels;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using RadioButton = System.Windows.Controls.RadioButton;
using AutomationScheduleKind = HelpDesk.Domain.Enums.AutomationScheduleKind;
using Clipboard = System.Windows.Clipboard;
using MessageBox = System.Windows.MessageBox;

namespace HelpDesk.Presentation.Views.Pages;

public partial class BundlesPage : Page
{
    private readonly MainViewModel _vm;
    private readonly IAppLogger _logger;
    private bool _automationRuleAutoSaveEnabled;

    public BundlesPage(MainViewModel vm, IAppLogger logger)
    {
        InitializeComponent();
        _vm = vm;
        _logger = logger;
        DataContext = _vm;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var stopwatch = Stopwatch.StartNew();
        Dispatcher.BeginInvoke(
            () =>
            {
                _automationRuleAutoSaveEnabled = true;
                _logger.Info($"Automation page became interactive in {stopwatch.ElapsedMilliseconds} ms.");
            },
            DispatcherPriority.ApplicationIdle);

        if (_vm.PreferAutomationAttentionTab)
        {
            AttentionSection.BringIntoView();
            _vm.ClearAutomationAttentionViewRequest();
        }

        if (_vm.ConsumePendingAutomationReceiptInspection() is { } pendingReceipt)
            ShowAutomationReceiptDetails(pendingReceipt);
    }

    private void MaintenanceProfileDetailsButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: MaintenanceProfileDefinition profile })
            ShowMaintenanceProfileDetails(profile);
    }

    private void MaintenanceProfileDetails_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { CommandParameter: MaintenanceProfileDefinition profile })
            ShowMaintenanceProfileDetails(profile);
    }

    private void MaintenanceProfileCopy_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { CommandParameter: MaintenanceProfileDefinition profile })
            Clipboard.SetText(_vm.BuildMaintenanceProfileDetailText(profile));
    }

    private async void MaintenanceProfileRunMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { CommandParameter: MaintenanceProfileDefinition profile })
            await RunMaintenanceProfileWithConfirmationAsync(profile);
    }

    private async void MaintenanceProfileRun_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: MaintenanceProfileDefinition profile })
            return;

        await RunMaintenanceProfileWithConfirmationAsync(profile);
    }

    private async void BundleRun_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: FixBundle bundle })
            return;

        var result = MessageBox.Show(
            $"Run \"{bundle.Title}\"?\n\n" +
            $"This maintenance pack will automatically run {bundle.FixIds.Count} repairs in sequence.\n" +
            $"Estimated time: {bundle.EstTime}\n\n" +
            "Save any open work before continuing.",
            $"{_vm.ProductDisplayName} - Run Maintenance Pack",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
            await _vm.RunBundleAsync(bundle);
    }

    private void RunbookDetailsButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: RunbookDefinition runbook })
            ShowRunbookDetails(runbook);
    }

    private void RunbookDetails_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { CommandParameter: RunbookDefinition runbook })
            ShowRunbookDetails(runbook);
    }

    private void RunbookCopy_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { CommandParameter: RunbookDefinition runbook })
            Clipboard.SetText(_vm.BuildRunbookDetailText(runbook));
    }

    private async void RunbookRunMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { CommandParameter: RunbookDefinition runbook })
            await RunRunbookWithConfirmationAsync(runbook);
    }

    private void RunbookCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2 || sender is not Border { Tag: RunbookDefinition runbook })
            return;

        ShowRunbookDetails(runbook);
    }

    private async void RunbookRun_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: RunbookDefinition runbook })
            return;

        await RunRunbookWithConfirmationAsync(runbook);
    }

    private void SaveWeeklyTuneUp_Click(object sender, RoutedEventArgs e)
    {
        try { _vm.SaveWeeklyTuneUpSchedule(); }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, $"{_vm.ProductDisplayName} - Schedule Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DisableWeeklyTuneUp_Click(object sender, RoutedEventArgs e)
    {
        try { _vm.DisableWeeklyTuneUpSchedule(); }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, $"{_vm.ProductDisplayName} - Schedule Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void RunWeeklyTuneUpNow_Click(object sender, RoutedEventArgs e)
        => await _vm.RunWeeklyTuneUpNowAsync();

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

    private void PauseAutomationHour_Click(object sender, RoutedEventArgs e)
        => _vm.PauseAutomationForHour();

    private void PauseAutomationTomorrow_Click(object sender, RoutedEventArgs e)
        => _vm.PauseAutomationUntilTomorrow();

    private void ResumeAutomation_Click(object sender, RoutedEventArgs e)
        => _vm.ResumeAutomation();

    private void AutomationRuleSetting_Changed(object sender, RoutedEventArgs e)
    {
        if (!ShouldAutoSaveAutomationRuleChange(sender as FrameworkElement))
            return;

        if ((sender as FrameworkElement)?.DataContext is AutomationRuleSettings rule)
            _vm.SaveAutomationRule(rule);
    }

    private void AutomationRuleSetting_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!ShouldAutoSaveAutomationRuleChange(sender as FrameworkElement))
            return;

        if ((sender as FrameworkElement)?.DataContext is AutomationRuleSettings rule)
            _vm.SaveAutomationRule(rule);
    }

    private void AutomationRuleDetailsButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: AutomationRuleSettings rule })
            ShowAutomationRuleDetails(rule);
    }

    private void AutomationRuleDetails_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { CommandParameter: AutomationRuleSettings rule })
            ShowAutomationRuleDetails(rule);
    }

    private void AutomationRuleCopy_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { CommandParameter: AutomationRuleSettings rule })
            Clipboard.SetText(_vm.BuildAutomationRuleDetailText(rule));
    }

    private async void AutomationRuleRunMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { CommandParameter: AutomationRuleSettings rule })
            await _vm.RunAutomationRuleAsync(rule);
    }

    private async void AutomationRuleRun_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: AutomationRuleSettings rule })
            await _vm.RunAutomationRuleAsync(rule);
    }

    private void AutomationRulePauseMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { CommandParameter: AutomationRuleSettings rule })
            _vm.PauseAutomationRuleUntilTomorrow(rule);
    }

    private void AutomationRulePause_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: AutomationRuleSettings rule })
            _vm.PauseAutomationRuleUntilTomorrow(rule);
    }

    private void AutomationRuleSave_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: AutomationRuleSettings rule })
            _vm.SaveAutomationRule(rule);
    }

    private async void AutomationAttentionRetry_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: AutomationAttentionItem item })
            await _vm.RetryAutomationAttentionItemAsync(item);
    }

    private void AutomationAttentionSkip_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: AutomationAttentionItem item })
            _vm.SkipAutomationAttentionItemOnce(item);
    }

    private void AutomationAttentionDismiss_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: AutomationAttentionItem item })
            _vm.DismissAutomationAttentionItem(item);
    }

    private void AutomationAttentionViewReceipt_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: AutomationAttentionItem item })
            ShowAutomationReceiptDetails(item.Receipt);
    }

    private void AutomationRulePin_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { DataContext: AutomationRuleSettings rule })
            _vm.ToggleAutomationRulePin(rule);
    }

    private void AutomationRecurrenceMode_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { DataContext: AutomationRuleSettings rule, Tag: string modeText })
            return;

        if (!Enum.TryParse<AutomationScheduleKind>(modeText, ignoreCase: true, out var mode))
            return;

        if (rule.ScheduleKind == mode)
            return;

        rule.ScheduleKind = mode;
        if (ShouldAutoSaveAutomationRuleChange(sender as FrameworkElement))
            _vm.SaveAutomationRule(rule);
    }

    private void AutomationReceiptCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2 || sender is not Border { Tag: AutomationRunReceipt receipt })
            return;

        ShowAutomationReceiptDetails(receipt);
    }

    private void AutomationReceiptDetails_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { CommandParameter: AutomationRunReceipt receipt })
            ShowAutomationReceiptDetails(receipt);
    }

    private void AutomationReceiptDetailsButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: AutomationRunReceipt receipt })
            ShowAutomationReceiptDetails(receipt);
    }

    private void AutomationReceiptCopy_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { CommandParameter: AutomationRunReceipt receipt })
            Clipboard.SetText(_vm.BuildAutomationReceiptDetailText(receipt));
    }

    private async void AutomationReceiptRerun_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: AutomationRunReceipt receipt })
            return;

        var rule = _vm.AutomationRules.FirstOrDefault(item => item.Id == receipt.RuleId);
        if (rule is not null)
            await _vm.RunAutomationRuleAsync(rule);
    }

    private async void AutomationReceiptSupport_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { CommandParameter: AutomationRunReceipt })
            await _vm.CreateEvidenceBundleAsync();
    }

    private async void AutomationReceiptSupportButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: AutomationRunReceipt })
            await _vm.CreateEvidenceBundleAsync();
    }

    private void ClearAutomationHistory_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            $"Clear the recorded automation history from {_vm.ProductDisplayName}?\n\nThis keeps repair history and support packages, but removes the automation run list from Automation.",
            $"{_vm.ProductDisplayName} - Clear Automation History",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
            _vm.ClearAutomationHistory();
    }

    private async Task RunMaintenanceProfileWithConfirmationAsync(MaintenanceProfileDefinition profile)
    {
        var result = MessageBox.Show(
            $"Run \"{profile.Title}\"?\n\n" +
            $"{profile.Summary}\n\n" +
            $"Safety: {profile.SafetyNotes}\n" +
            $"Verification: {profile.VerificationNotes}",
            $"{_vm.ProductDisplayName} - Run Maintenance Profile",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
            await _vm.RunMaintenanceProfileAsync(profile);
    }

    private async Task RunRunbookWithConfirmationAsync(RunbookDefinition runbook)
        => await _vm.RunRunbookAsync(runbook);

    private void ShowMaintenanceProfileDetails(MaintenanceProfileDefinition profile)
    {
        MessageBox.Show(
            _vm.BuildMaintenanceProfileDetailText(profile),
            $"{_vm.ProductDisplayName} - Maintenance Profile",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void ShowRunbookDetails(RunbookDefinition runbook)
    {
        MessageBox.Show(
            _vm.BuildRunbookDetailText(runbook),
            $"{_vm.ProductDisplayName} - Workflow Details",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void ShowAutomationRuleDetails(AutomationRuleSettings rule)
    {
        MessageBox.Show(
            _vm.BuildAutomationRuleDetailText(rule),
            $"{_vm.ProductDisplayName} - Automation Rule",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void ShowAutomationReceiptDetails(AutomationRunReceipt receipt)
    {
        MessageBox.Show(
            _vm.BuildAutomationReceiptDetailText(receipt),
            $"{_vm.ProductDisplayName} - Automation Result",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private bool ShouldAutoSaveAutomationRuleChange(FrameworkElement? element)
    {
        if (!_automationRuleAutoSaveEnabled || element is null || !element.IsLoaded)
            return false;

        return element.IsKeyboardFocusWithin
               || ReferenceEquals(Keyboard.FocusedElement, element)
               || Mouse.LeftButton == MouseButtonState.Pressed
               || Mouse.RightButton == MouseButtonState.Pressed;
    }

    public void FocusFirstAutomationRule()
    {
        var button = FindVisualChildren<Button>(this)
            .FirstOrDefault(candidate => candidate.Tag is AutomationRuleSettings);
        button?.Focus();
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T typed)
                yield return typed;

            foreach (var descendant in FindVisualChildren<T>(child))
                yield return descendant;
        }
    }
}
