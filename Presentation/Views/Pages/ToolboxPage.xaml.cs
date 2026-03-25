using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Threading.Tasks;
using HelpDesk.Application.Interfaces;
using HelpDesk.Domain.Models;
using HelpDesk.Presentation.ViewModels;
using ToolLaunchState = HelpDesk.Domain.Enums.ToolLaunchState;
using Button = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;

namespace HelpDesk.Presentation.Views.Pages;

public partial class ToolboxPage : Page
{
    private readonly MainViewModel _vm;
    private readonly IToolboxService _toolbox;

    public ToolboxPage(MainViewModel vm, IToolboxService toolbox)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        _toolbox = toolbox;
    }

    private async void OpenTool_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ToolboxEntry entry })
            return;

        await OpenEntryAsync(entry);
    }

    private async void OpenToolMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { CommandParameter: ToolboxEntry entry })
            await OpenEntryAsync(entry);
    }

    private void CopyToolTarget_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { CommandParameter: ToolboxEntry entry })
            return;

        CopyLaunchTarget(entry);
    }

    private void CopyToolTargetButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ToolboxEntry entry })
            CopyLaunchTarget(entry);
    }

    private async void ToolCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2 || sender is not Border { Tag: ToolboxEntry entry })
            return;

        await OpenEntryAsync(entry);
    }

    private async Task OpenEntryAsync(ToolboxEntry entry)
    {
        entry.LaunchState = ToolLaunchState.Running;
        entry.LaunchSummary = "Opening Windows tool...";

        try
        {
            _toolbox.Launch(entry);
            await Task.Delay(700);
            entry.LaunchState = ToolLaunchState.Success;
            entry.LastLaunchedAt = DateTime.Now;
            entry.LaunchSummary = "Opened successfully.";
        }
        catch (Exception ex)
        {
            entry.LaunchState = ToolLaunchState.Failed;
            entry.LaunchSummary = ex.Message;
            MessageBox.Show(ex.Message, $"{_vm.ProductDisplayName} - Windows Tools", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static void CopyLaunchTarget(ToolboxEntry entry)
    {
        var target = string.IsNullOrWhiteSpace(entry.LaunchArguments)
            ? entry.LaunchTarget
            : $"{entry.LaunchTarget} {entry.LaunchArguments}";

        if (!string.IsNullOrWhiteSpace(target))
            System.Windows.Clipboard.SetText(target);
    }
}
