using System.Windows;
using System.Windows.Controls;
using HelpDesk.Domain.Models;
using HelpDesk.Presentation.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Button = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;

namespace HelpDesk.Presentation.Views.Pages;

public partial class BundlesPage : Page
{
    private readonly MainViewModel _vm;

    public BundlesPage()
    {
        InitializeComponent();
        _vm         = App.Services.GetRequiredService<MainViewModel>();
        DataContext = _vm;
    }

    private async void BundleRun_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: FixBundle bundle }) return;

        var result = MessageBox.Show(
            $"Run \"{bundle.Title}\"?\n\n" +
            $"This will automatically run {bundle.FixIds.Count} fixes in sequence.\n" +
            $"Estimated time: {bundle.EstTime}\n\n" +
            "Save any open work before continuing.",
            "FixFox \u2014 Run Bundle",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
            await _vm.RunBundleAsync(bundle);
    }

    private async void RunbookRun_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: RunbookDefinition runbook }) return;

        var result = MessageBox.Show(
            $"Run \"{runbook.Title}\"?\n\n" +
            $"{runbook.Description}\n\n" +
            $"This runbook contains {runbook.Steps.Count} step(s).\n" +
            $"Edition requirement: {runbook.MinimumEdition}",
            "FixFox — Run Runbook",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
            await _vm.RunRunbookAsync(runbook);
    }

    private void SaveWeeklyTuneUp_Click(object sender, RoutedEventArgs e)
    {
        try { _vm.SaveWeeklyTuneUpSchedule(); }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "FixFox \u2014 Schedule Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DisableWeeklyTuneUp_Click(object sender, RoutedEventArgs e)
    {
        try { _vm.DisableWeeklyTuneUpSchedule(); }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "FixFox \u2014 Schedule Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void RunWeeklyTuneUpNow_Click(object sender, RoutedEventArgs e)
        => await _vm.RunWeeklyTuneUpNowAsync();
}
