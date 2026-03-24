using System.Windows;
using System.Windows.Controls;
using HelpDesk.Domain.Models;
using HelpDesk.Presentation.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Button = System.Windows.Controls.Button;

namespace HelpDesk.Presentation.Views.Pages;

public partial class FixCenterPage : Page
{
    private readonly MainViewModel _vm;

    public FixCenterPage()
    {
        InitializeComponent();
        _vm         = App.Services.GetRequiredService<MainViewModel>();
        DataContext = _vm;
    }

    private void ClearSearch_Click(object sender, RoutedEventArgs e)
        => _vm.SearchText = "";

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
}
