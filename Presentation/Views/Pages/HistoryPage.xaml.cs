using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HelpDesk.Domain.Models;
using HelpDesk.Presentation.ViewModels;
using HelpDesk.Presentation.Views;
using Button = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;
using NavPage = HelpDesk.Domain.Enums.Page;

namespace HelpDesk.Presentation.Views.Pages;

public partial class HistoryPage : Page
{
    private readonly MainViewModel _vm;

    public HistoryPage(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = _vm;
    }

    private void ClearHistory_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(
                $"Clear all recorded activity for {_vm.ProductDisplayName}? This removes repair, workflow, and automation receipts from the in-app history view.",
                $"{_vm.ProductDisplayName} - Clear Activity",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) == MessageBoxResult.Yes)
            _vm.ClearHistory();
    }

    private void ClearSearch_Click(object sender, RoutedEventArgs e)
        => _vm.HistorySearchText = string.Empty;

    private void OpenRepairLibrary_Click(object sender, RoutedEventArgs e)
        => NavigateTo(NavPage.Fixes);

    private void OpenAutomation_Click(object sender, RoutedEventArgs e)
        => NavigateTo(NavPage.Bundles);

    private async void RerunFix_Click(object sender, RoutedEventArgs e)
    {
        var entry = sender switch
        {
            Button { Tag: RepairHistoryEntry buttonEntry } => buttonEntry,
            MenuItem { CommandParameter: RepairHistoryEntry menuEntry } => menuEntry,
            _ => null
        };

        if (entry is not null)
            await _vm.RerunHistoryEntryAsync(entry);
    }

    private void CopyDetails_Click(object sender, RoutedEventArgs e)
    {
        var entry = sender switch
        {
            Button { Tag: RepairHistoryEntry buttonEntry } => buttonEntry,
            MenuItem { CommandParameter: RepairHistoryEntry menuEntry } => menuEntry,
            _ => null
        };

        if (entry is null)
            return;

        System.Windows.Clipboard.SetText(_vm.BuildReceiptDetailText(entry));
    }

    private void ReceiptDetailsButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: RepairHistoryEntry entry })
            ShowReceiptDetails(entry);
    }

    private void ReceiptDetails_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { CommandParameter: RepairHistoryEntry entry })
            ShowReceiptDetails(entry);
    }

    private async void CreateSupportPackage_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _vm.CreateEvidenceBundleAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, $"{_vm.ProductDisplayName} - Support Package", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void HistoryCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2 || sender is not Border { Tag: RepairHistoryEntry entry })
            return;

        if (string.IsNullOrWhiteSpace(entry.FixId) && string.IsNullOrWhiteSpace(entry.RunbookId))
        {
            ShowReceiptDetails(entry);
            return;
        }

        await _vm.RerunHistoryEntryAsync(entry);
    }

    private void ShowReceiptDetails(RepairHistoryEntry entry)
    {
        MessageBox.Show(
            _vm.BuildReceiptDetailText(entry),
            $"{_vm.ProductDisplayName} - Receipt Details",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void NavigateTo(NavPage page)
    {
        var shell = Window.GetWindow(this) as MainWindow
            ?? System.Windows.Application.Current?.MainWindow as MainWindow;
        shell?.NavigateToPage(page);
    }
}
