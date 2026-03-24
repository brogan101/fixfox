using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HelpDesk.Domain.Models;
using HelpDesk.Presentation.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Button = System.Windows.Controls.Button;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace HelpDesk.Presentation.Views.Pages;

public partial class SymptomCheckerPage : Page
{
    private readonly MainViewModel _vm;

    public SymptomCheckerPage()
    {
        InitializeComponent();
        _vm         = App.Services.GetRequiredService<MainViewModel>();
        DataContext = _vm;
    }

    private void SymptomSearch_Click(object sender, RoutedEventArgs e)
        => _vm.RunSymptomSearch();

    private void SymptomBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _vm.RunSymptomSearch();
            e.Handled = true;
        }
    }

    private async void FixButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: FixItem fix })
            await _vm.RunFixAsync(fix);
    }

    private void RecentSearch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string query })
        {
            _vm.SymptomInput = query;
            _vm.RunSymptomSearch();
        }
    }
}
