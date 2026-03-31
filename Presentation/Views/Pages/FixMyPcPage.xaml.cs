using System.Windows;
using System.Windows.Controls;
using HelpDesk.Domain.Models;
using HelpDesk.Presentation.ViewModels;

namespace HelpDesk.Presentation.Views.Pages;

public partial class FixMyPcPage : Page
{
    private readonly MainViewModel _vm;

    public FixMyPcPage(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = _vm;
    }

    private async void ProblemButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { Tag: SimplifiedProblemOption option })
            await _vm.RunSimplifiedProblemAsync(option);
    }

    private async void RecentRunAgain_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { Tag: RecentQuickActionEntry entry })
            await _vm.RunRecentQuickActionAsync(entry);
    }
}
