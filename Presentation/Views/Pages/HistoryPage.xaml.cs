using System.Windows;
using System.Windows.Controls;
using HelpDesk.Domain.Models;
using HelpDesk.Presentation.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Button = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;

namespace HelpDesk.Presentation.Views.Pages;

public partial class HistoryPage : Page
{
    private readonly MainViewModel _vm;

    public HistoryPage()
    {
        InitializeComponent();
        _vm         = App.Services.GetRequiredService<MainViewModel>();
        DataContext = _vm;
    }

    private void ClearHistory_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(
                "Clear all fix history? This cannot be undone.",
                "FixFox \u2014 Confirm",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) == MessageBoxResult.Yes)
            _vm.ClearHistory();
    }

    private async void RerunFix_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: FixLogEntry entry })
            await _vm.RerunHistoryEntryAsync(entry);
    }
}
