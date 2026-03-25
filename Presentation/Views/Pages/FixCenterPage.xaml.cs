using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Threading.Tasks;
using System.Linq;
using HelpDesk.Domain.Models;
using HelpDesk.Presentation.ViewModels;
using HelpDesk.Presentation.Views;
using NavPage = HelpDesk.Domain.Enums.Page;
using Button = System.Windows.Controls.Button;
using Clipboard = System.Windows.Clipboard;
using MessageBox = System.Windows.MessageBox;

namespace HelpDesk.Presentation.Views.Pages;

public partial class FixCenterPage : Page
{
    private readonly MainViewModel _vm;

    public FixCenterPage(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = _vm;
    }

    private void ClearSearch_Click(object sender, RoutedEventArgs e)
        => _vm.SearchText = "";

    private void OpenGuidedDiagnosis_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow shell)
            shell.NavigateToPage(NavPage.SymptomChecker);
    }

    private async void FixButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: FixItem fix })
            await _vm.RunFixAsync(fix);
    }

    private void GuidedButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: FixItem fix })
            _vm.StartWizard(fix);
    }

    private void TogglePin_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: FixItem fix })
            _vm.TogglePin(fix);
    }

    private async void FixCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2 || sender is not Border { Tag: FixItem fix })
            return;

        await RunBestActionAsync(fix);
    }

    private async void RunBestAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { CommandParameter: FixItem fix })
            await RunBestActionAsync(fix);
    }

    private async void RunMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { CommandParameter: FixItem fix } && fix.HasScript)
            await _vm.RunFixAsync(fix);
    }

    private void GuidedMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { CommandParameter: FixItem fix } && fix.HasSteps)
            _vm.StartWizard(fix);
    }

    private void TogglePinMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { CommandParameter: FixItem fix })
            _vm.TogglePin(fix);
    }

    private void CopyFixTitle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { CommandParameter: FixItem fix })
            System.Windows.Clipboard.SetText(fix.Title);
    }

    private void FixDetailsButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: FixItem fix })
            ShowFixDetails(fix);
    }

    private void FixDetails_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { CommandParameter: FixItem fix })
            ShowFixDetails(fix);
    }

    private void CopyFixDetails_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { CommandParameter: FixItem fix })
            Clipboard.SetText(_vm.BuildRepairDetailText(fix));
    }

    private void OpenRelatedWorkflow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { CommandParameter: FixItem fix })
            return;

        var relatedRunbooks = _vm.GetRelatedRunbooks(fix);
        var owner = Window.GetWindow(this) as HelpDesk.Presentation.Views.MainWindow;
        owner?.NavigateToPage(HelpDesk.Domain.Enums.Page.Bundles);

        if (relatedRunbooks.Count == 0)
        {
            MessageBox.Show(
                $"{_vm.ProductDisplayName} opened Automation so you can choose the closest workflow manually. This repair does not have a strongly linked workflow yet.",
                $"{_vm.ProductDisplayName} - Related Workflow",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        MessageBox.Show(
            $"Recommended workflow(s):{Environment.NewLine}{Environment.NewLine}{string.Join(Environment.NewLine, relatedRunbooks.Select(runbook => $"- {runbook.Title}"))}",
            $"{_vm.ProductDisplayName} - Related Workflow",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private Task RunBestActionAsync(FixItem fix)
    {
        if (fix.HasSteps)
        {
            _vm.StartWizard(fix);
            return Task.CompletedTask;
        }

        return fix.HasScript ? _vm.RunFixAsync(fix) : Task.CompletedTask;
    }

    private void ShowFixDetails(FixItem fix)
    {
        MessageBox.Show(
            _vm.BuildRepairDetailText(fix),
            $"{_vm.ProductDisplayName} - Repair Details",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}
